using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;

namespace DBVer
{
    enum StatementMode
    {
        All,
        Table,
        Indexes,
        Constraints
    }

    internal class Writer
    {
        private readonly bool skipUseStatement;
        private readonly bool multipleFilesPerObject;

        public Writer(bool skipUseStatement, bool multipleFilesPerObject)
        {
            this.skipUseStatement = skipUseStatement;
            this.multipleFilesPerObject = multipleFilesPerObject;
        }

        public void WriteResult(StringCollection lines, string schema, string objectName, ObjectType objectType, string dbName, string outputDir)
        {
            string path = Path.Combine(outputDir, GetFolderByType(objectType));

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var result = new StringBuilder();

            if (objectType != ObjectType.Table || !multipleFilesPerObject)
            {
                AddLines(result, lines, dbName);

                string fileName = Path.Combine(path, $"{objectName}.sql");
                File.WriteAllText(fileName, result.ToString());
            }
            else
            {
                string fileName = Path.Combine(path, $"{objectName}{{0}}.sql");

                AddLines(result, lines, dbName, StatementMode.Table);
                File.WriteAllText(string.Format(fileName, ""), result.ToString());

                result.Clear();
                AddLines(result, lines, dbName, StatementMode.Indexes);
                File.WriteAllText(string.Format(fileName, "_Ind"), result.ToString());

                result.Clear();
                AddLines(result, lines, dbName, StatementMode.Constraints);
                File.WriteAllText(string.Format(fileName, "_Con"), result.ToString());
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

        private void AddHeader(StringBuilder result, StringCollection strings, string dbName)
        {
            if (!skipUseStatement)
            {
                result.AppendLine("USE [" + dbName + "]");
                result.AppendLine("GO");
            }
        }

        private string ProcessBody(string body)
        {
            return body.Replace("\r", "").Replace("\n", "\r\n").Replace("\t", "    ").TrimEnd(' ', '\t');
        }

        private void AddLines(StringBuilder result, StringCollection strings, string dbName, StatementMode mode = StatementMode.All)
        {
            AddHeader(result, strings, dbName);

            foreach (var s in strings)
            {
                var body = ProcessBody(s);

                if (IsSetStatement(s))
                {
                    result.Append(body);
                    result.AppendLine(Environment.NewLine + "GO");
                }
                else if (mode == StatementMode.All
                    || (mode == StatementMode.Table && IsTableStatement(s))
                    || (mode == StatementMode.Indexes && IsIndexStatement(s))
                    || (mode == StatementMode.Constraints && IsConstraintStatement(s)))
                {
                    result.AppendLine(body);
                }
            }

            result.AppendLine("GO");
        }

        private bool IsSetStatement(string statement)
        {
            return statement.StartsWith("SET QUOTED_IDENTIFIER") || statement.StartsWith("SET ANSI_NULLS");
        }

        private bool IsTableStatement(string statement)
        {
            return statement.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsIndexStatement(string statement)
        {
            return statement.Contains("CREATE") && statement.Contains("INDEX");
        }

        private bool IsConstraintStatement(string statement)
        {
            return statement.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
                   statement.Contains("CHECK ");
        }
    }
}
