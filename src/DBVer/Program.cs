using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DBVer.Mapping;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using NDesk.Options;
using DBVer.Configuration;
using DBVer.Tools;
using System.Data.SqlClient;

namespace DBVer
{
    internal class Program
    {
        private int currentIndex;
        private int totalRows;
        private NameReplacer nameReplacer;
        private ProcessedMap processedMap;
        private readonly object lockObject = new object();
        private Writer outputWriter;
        private DictionaryExporter dictionaryExporter;

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

            bool skipUseStatement = bool.Parse(ConfigurationManager.AppSettings["SkipUseStatement"]);
            bool multipleFilesPerObject = bool.Parse(ConfigurationManager.AppSettings["MultipleFilesPerObject"]);

            var exportSettingsSection = ExportSettingsSection.ReadFromConfig();

            nameReplacer = new NameReplacer(exportSettingsSection);
            dictionaryExporter = new DictionaryExporter(exportSettingsSection);
            outputWriter = new Writer(skipUseStatement, multipleFilesPerObject);

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

        private void ProcessDatabase(string dbName, string outputDir, string serverHost, string userName, string password)
        {
            var mainServerInfo = new ServerInfo(serverHost, userName, password);

            try
            {
                if (!mainServerInfo.Server.Databases.Contains(dbName))
                {
                    Console.WriteLine("Database {0} is absent.", dbName);
                    return;
                }

                var db = mainServerInfo.Server.Databases[dbName];
                var objectsTable = db.EnumObjects(DatabaseObjectTypes.Table | DatabaseObjectTypes.View
                                                  | DatabaseObjectTypes.StoredProcedure | DatabaseObjectTypes.UserDefinedFunction);

                var filteredView = new DataView(objectsTable);
                filteredView.RowFilter = "[Schema] <> 'INFORMATION_SCHEMA' AND [Schema] NOT IN ('sys', 'guest', 'INFORMATION_SCHEMA') " +
                    " AND [Schema] NOT LIKE 'db_%'";

                currentIndex = 1;
                totalRows = filteredView.Count;

                var options = new ParallelOptions() {  MaxDegreeOfParallelism = bool.Parse(ConfigurationManager.AppSettings["SingleThread"]) ? 1 : -1 };

                Parallel.ForEach(filteredView.Cast<DataRowView>(),
                    options,
                    () => new ServerInfo(serverHost, userName, password),
                    (row, state, i, info) =>
                        {
                            var objectName = row["Name"].ToString().Replace("\r\n", "");
                            var objectType = ParseObjectType(row["DatabaseObjectTypes"] as string);
                            var schema = row["Schema"] as string;
                            var urn = row["Urn"] as string;

                            ProcessObject(info, urn, schema, objectName, objectType, dbName, outputDir);

                            return info;
                        }, serverInfo => { serverInfo.Disconnect(); });
            }
            finally
            {
                mainServerInfo.Disconnect();
            }

            var builder = new SqlConnectionStringBuilder()
                { DataSource = serverHost, InitialCatalog = dbName, UserID = userName, Password = password };
            using (var connection = new SqlConnection())
            {
                dictionaryExporter.Run(connection);
            }
        }

        private void ProcessObject(ServerInfo info, string urn, string schema, string objectName, ObjectType objectType, string dbName, string outputDir)
        {
            var newName = nameReplacer.ReplaceName(objectName, objectType);
            if (string.IsNullOrEmpty(newName))
                return;

            WriteLog(schema, string.CompareOrdinal(objectName, newName) != 0 ? $"{objectName} -> {newName}" : objectName, objectType);

            if (processedMap.Contains(schema, newName, objectType))
                return;

            var db = info.Server.Databases[dbName];

            if (objectType == ObjectType.StoredProcedure)
            {
                var sp = db.StoredProcedures[objectName, schema];
                if (sp == null)
                {
                    Console.WriteLine($"Warning: SP {objectName} not found.");
                }
                else if (sp.ImplementationType != ImplementationType.TransactSql)
                {
                    Console.WriteLine($"Skipped unsupported type: {sp.ImplementationType}");
                    return;
                }
            }

            var urns = new Urn[1];
            urns[0] = urn;

            var lines = info.Scripter.Script(urns);
            WriteResult(lines, schema, newName, objectType, db.Name, outputDir);

            if (objectType == ObjectType.Table)
            {
                ExportTriggers(db, schema, objectName, outputDir, info.Scripter);
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
                if (string.IsNullOrEmpty(newName))
                    continue;

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
            outputWriter.WriteResult(lines, schema, objectName, objectType, dbName, outputDir);

            processedMap.Add(schema, objectName, objectType);
        }
    }
}