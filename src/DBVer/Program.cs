using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
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
        private const string TriggerObjectType = "Trigger";

        private static int Main(string[] args)
        {
            try
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                Console.WriteLine(assemblyName.Name + " " + assemblyName.Version);

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

            try
            {
                set.Parse(args);
            }
            catch
            {
                var writer = new StringWriter();
                set.WriteOptionDescriptions(writer);
                Console.Write(writer.ToString());
                return;
            }

            if (serverHost == null || userName == null || password == null || outputDir == null)
            {
                Console.WriteLine("Not all required params are specified.");
                return;
            }

            if (databases.Count == 0)
            {
                Console.WriteLine("Add at least one database.");
                return;
            }

            var server = new Server(new ServerConnection(serverHost, userName, password));

            foreach (var dbName in databases)
            {
                var path = Path.Combine(outputDir, dbName);
                Directory.CreateDirectory(path);

                ProcessDatabase(server, dbName, path);
            }
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

        private void ProcessDatabase(Server server, string dbName, string outputDir)
        {
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

            var scripter = new Scripter(server);
            scripter.Options = baseOptions;

            var urns = new Urn[1];

            currentIndex = 1;
            totalRows = filteredView.Count;

            foreach (DataRowView row in filteredView)
            {
                var objectName = row["Name"].ToString().Replace("\r\n", "");
                var objectType = row["DatabaseObjectTypes"] as string;
                var schema = row["Schema"] as string;
                WriteLog(schema, objectName, objectType);

                urns[0] = row["Urn"].ToString();

                var lines = scripter.Script(urns);
                WriteResult(lines, objectName, objectType, dbName, outputDir);

                if (ParseObjectType(objectType) == DatabaseObjectTypes.Table)
                {
                    ExportTriggers(db, schema, objectName, scripter, outputDir);
                }
            }
        }

        private string GetFolderByType(DatabaseObjectTypes type)
        {
            switch (type)
            {
                case DatabaseObjectTypes.Table:
                    return "T";
                case DatabaseObjectTypes.View:
                    return "V";
                case DatabaseObjectTypes.StoredProcedure:
                    return "P";
                case DatabaseObjectTypes.UserDefinedFunction:
                    return "F";
                default:
                    return "_";
            }
        }

        private void ExportTriggers(Database db, string schema, string tableName, Scripter scripter, string outputDir)
        {
            string dbName = db.Name;
            var table = db.Tables[tableName, schema];
            var urns = new Urn[1];

            foreach (Trigger trigger in table.Triggers)
            {
                Console.WriteLine($"    [{schema}].{trigger.Name}   {TriggerObjectType}");

                urns[0] = trigger.Urn;
                var lines = scripter.Script(urns);
                WriteResult(lines, trigger.Name, TriggerObjectType, dbName, outputDir);
            }
        }

        private void WriteLog(string schema, string objectName, string objectType)
        {
            Console.WriteLine("{0:00000}/{1:00000} [{2}].[{3}]   {4}", currentIndex++, totalRows, schema, objectName, objectType);
        }

        DatabaseObjectTypes ParseObjectType(string objectType)
        {
            return (DatabaseObjectTypes) Enum.Parse(typeof (DatabaseObjectTypes), objectType);
        }

        private void WriteResult(StringCollection lines, string objectName, string objectType, string dbName, string outputDir)
        {
            var result = new StringBuilder();
            AddLines(result, lines, dbName);

            var subDir = string.CompareOrdinal(objectType, TriggerObjectType) == 0 ? "Tr" : GetFolderByType(ParseObjectType(objectType));
            string path = Path.Combine(outputDir, subDir);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string fileName = Path.Combine(path, $"{objectName}.sql");
            File.WriteAllText(fileName, result.ToString());
        }
    }
}