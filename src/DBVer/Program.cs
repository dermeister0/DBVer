﻿using System;
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
                                              | DatabaseObjectTypes.StoredProcedure |
                                              DatabaseObjectTypes.UserDefinedFunction);

            var filteredView = new DataView(objectsTable);
            filteredView.RowFilter = "[Schema] <> 'INFORMATION_SCHEMA' AND [Schema] <> 'sys'";

            filteredView.RowFilter += " AND [Schema] = 'dbo' ";

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
            var result = new StringBuilder();
            var i = 1;

            foreach (DataRowView row in filteredView)
            {
                var objectName = row["Name"].ToString().Replace("\r\n", "");
                Console.WriteLine("{0:00000} [{1}].[{2}]   {3}", i++, row["Schema"], objectName,
                    row["DatabaseObjectTypes"]);

                urns[0] = row["Urn"].ToString();

                var lines = scripter.Script(urns);
                result.Clear();

                AddLines(result, lines, dbName);

                var fileName = string.Format("{0}\\{1}.sql", outputDir, objectName);
                File.WriteAllText(fileName, result.ToString());
            }
        }
    }
}