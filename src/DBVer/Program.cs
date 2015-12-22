using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DBVer.Mapping;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using NDesk.Options;

namespace DBVer
{
    internal class Program
    {
        private bool skipUseStatement;
        private int currentIndex;
        private int totalRows;
        private NameReplacer nameReplacer;
        private ProcessedMap processedMap;
        private readonly object lockObject = new object();

        private static int Main(string[] args)
        {
            try
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                Console.WriteLine(assemblyName.Name + " " + assemblyName.Version);
                Console.WriteLine("https://github.com/dermeister0/DBVer");
                Console.WriteLine("");

                new Program().Run(args);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        private void Run(string[] args)
        {
            string serverHost = null, userName = null, password = null;

            string outputDir = null;
            var databases = new List<string>();

            var set = new OptionSet
            {
                {"s|server=", "Server {HOST}", v => serverHost = v},
                {"u|username=", "{USERNAME}", v => userName = v},
                {"p|password=", "{PASSWORD}", v => password = v},
                {"o|output=", "{DIR} for exported scripts", v => outputDir = v},
                {"db|database=", "{DATABASE} name to process", v => databases.Add(v)},
                {"skip-use", v => skipUseStatement = v != null}
            };

            if (args.Length == 0)
            {
                WriteHelp(set);
                return;
            }

            try
            {
                set.Parse(args);
            }
            catch
            {
                WriteHelp(set);
                return;
            }

            if (serverHost == null || userName == null || password == null || outputDir == null)
            {
                Console.WriteLine("Not all required params are specified.");
                WriteHelp(set);
                return;
            }

            if (databases.Count == 0)
            {
                Console.WriteLine("Add at least one database.");
                WriteHelp(set);
                return;
            }

            nameReplacer = new NameReplacer();

            foreach (var dbName in databases)
            {
                var path = Path.Combine(outputDir, dbName);
                Directory.CreateDirectory(path);

                processedMap = new ProcessedMap();
                ProcessDatabase(dbName, path, serverHost, userName, password);
            }
        }

        private void WriteHelp(OptionSet set)
        {
            var writer = new StringWriter();
            set.WriteOptionDescriptions(writer);
            Console.Write(writer.ToString());
        }

        private void AddLines(StringBuilder result, StringCollection strings, string dbName)
        {
            if (!skipUseStatement)
            {
                result.AppendLine("USE [" + dbName + "]");
                result.AppendLine("GO");
            }

            foreach (var s in strings)
            {
                var body = s.Replace("\r", "").Replace("\n", "\r\n").Replace("\t", "    ");
                body = body.TrimEnd(' ', '\t');

                result.Append(body);

                if (s.StartsWith("SET QUOTED_IDENTIFIER") || s.StartsWith("SET ANSI_NULLS"))
                    result.Append(Environment.NewLine + "GO");

                result.Append(Environment.NewLine);
            }

            result.AppendLine("GO");
        }

        private void ProcessDatabase(string dbName, string outputDir, string serverHost, string userName, string password)
        {
            var server = new Server(new ServerConnection(serverHost, userName, password));

            if (!server.Databases.Contains(dbName))
            {
                Console.WriteLine("Database {0} is absent.", dbName);
                return;
            }

            var db = server.Databases[dbName];
            var objectsTable = db.EnumObjects(DatabaseObjectTypes.Table | DatabaseObjectTypes.View
                                              | DatabaseObjectTypes.StoredProcedure | DatabaseObjectTypes.UserDefinedFunction);

            var filteredView = new DataView(objectsTable);
            filteredView.RowFilter = "[Schema] <> 'INFORMATION_SCHEMA' AND [Schema] NOT IN ('sys', 'guest', 'INFORMATION_SCHEMA') " +
                " AND [Schema] NOT LIKE 'db_%'";

            var baseOptions = new ScriptingOptions();
            baseOptions.IncludeHeaders = false;
            baseOptions.Indexes = true;
            baseOptions.DriAllKeys = true;
            baseOptions.NoCollation = false;
            baseOptions.SchemaQualify = true;
            baseOptions.SchemaQualifyForeignKeysReferences = true;
            baseOptions.Permissions = false;
            baseOptions.Encoding = Encoding.UTF8;

            currentIndex = 1;
            totalRows = filteredView.Count;

            Parallel.ForEach(filteredView.Cast<DataRowView>(), row =>
            {
                var objectName = row["Name"].ToString().Replace("\r\n", "");
                var objectType = ParseObjectType(row["DatabaseObjectTypes"] as string);
                var schema = row["Schema"] as string;
                var urn = row["Urn"] as string;

                var server2 = new Server(new ServerConnection(serverHost, userName, password));

                var scripter = new Scripter(server2);
                scripter.Options = baseOptions;

                ProcessObject(server2.Databases[dbName], urn, schema, objectName, objectType, outputDir, scripter);

                server2.ConnectionContext.Disconnect();
            });
        }

        private void ProcessObject(Database db, string urn, string schema, string objectName, ObjectType objectType, string outputDir, Scripter scripter)
        {
            var newName = nameReplacer.ReplaceName(objectName, objectType);
            WriteLog(schema, string.CompareOrdinal(objectName, newName) != 0 ? $"{objectName} -> {newName}" : objectName, objectType);

            if (processedMap.Contains(schema, newName, objectType))
                return;

            if (objectType == ObjectType.StoredProcedure)
            {
                var sp = db.StoredProcedures[objectName, schema];
                if (sp.ImplementationType != ImplementationType.TransactSql)
                {
                    Console.WriteLine($"Skipped unsupported type: {sp.ImplementationType}");
                    return;
                }
            }

            var urns = new Urn[1];
            urns[0] = urn;

            var lines = scripter.Script(urns);
            WriteResult(lines, schema, newName, objectType, db.Name, outputDir);

            if (objectType == ObjectType.Table)
            {
                ExportTriggers(db, schema, objectName, outputDir, scripter);
            }
        }

        private string GetFolderByType(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Table:
                    return "T";
                case ObjectType.View:
                    return "V";
                case ObjectType.StoredProcedure:
                    return "P";
                case ObjectType.UserDefinedFunction:
                    return "F";
                case ObjectType.Trigger:
                    return "Tr";
                default:
                    return "_";
            }
        }

        private void ExportTriggers(Database db, string schema, string tableName, string outputDir, Scripter scripter)
        {
            string dbName = db.Name;
            var table = db.Tables[tableName, schema];          
            var urns = new Urn[1];
            var objectType = ObjectType.Trigger;

            foreach (Trigger trigger in table.Triggers)
            {
                var newName = nameReplacer.ReplaceName(trigger.Name, objectType);
                var changedName = string.CompareOrdinal(trigger.Name, newName) != 0 ? $"{trigger.Name} -> {newName}" : newName;
                Console.WriteLine($"    [{schema}].[{changedName}]   {objectType}");

                if (processedMap.Contains(schema, newName, objectType))
                    continue;

                urns[0] = trigger.Urn;
                var lines = scripter.Script(urns);
                WriteResult(lines, schema, newName, objectType, dbName, outputDir);
            }
        }

        private void WriteLog(string schema, string objectName, ObjectType objectType)
        {
            lock (lockObject)
            {
                Console.WriteLine("{0:00000}/{1:00000} [{2}].[{3}]   {4}", currentIndex++, totalRows, schema, objectName, objectType);
            }
        }

        ObjectType ParseObjectType(string objectType)
        {
            return (ObjectType) Enum.Parse(typeof (ObjectType), objectType);
        }

        private void WriteResult(StringCollection lines, string schema, string objectName, ObjectType objectType, string dbName, string outputDir)
        {
            var result = new StringBuilder();
            AddLines(result, lines, dbName);

            string path = Path.Combine(outputDir, GetFolderByType(objectType));

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string fileName = Path.Combine(path, $"{objectName}.sql");
            File.WriteAllText(fileName, result.ToString());

            processedMap.Add(schema, objectName, objectType);
        }
    }
}