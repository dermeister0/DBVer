using CsvHelper;
using DBVer.Configuration;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using CsvHelper.Configuration;

namespace DBVer.Tools
{
    internal class DictionaryExporter
    {
        private ExportSettingsSection exportSettingsSection;

        public DictionaryExporter(ExportSettingsSection exportSettingsSection)
        {
            this.exportSettingsSection = exportSettingsSection;
        }

        public void Run(SqlConnection connection, string outputDir)
        {
            var dataDir = Path.Combine(outputDir, "Data");
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            foreach (DictionaryDefinition dictionary in exportSettingsSection.Dictionaries)
            {
                ExportDictionary(connection, dictionary, dataDir);
            }
        }

        private void ExportDictionary(SqlConnection connection, DictionaryDefinition dictionary, string dataDir)
        {
            if (string.IsNullOrWhiteSpace(dictionary.Name))
                throw new InvalidOperationException("Dictionary name is empty.");

            if (dictionary.Name.Contains("."))
                throw new NotSupportedException("Only default schema is supported.");

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "select S.name, T.name from sys.tables T JOIN sys.schemas S ON T.schema_id = S.schema_id where T.name = @Name";
                cmd.Parameters.AddWithValue("@Name", dictionary.Name);

                string schema;
                string name;
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("Warning: Dictionary {0} not found.", dictionary.Name);
                        return;
                    }

                    reader.Read();
                    schema = reader.GetString(0);
                    name = reader.GetString(1);
                }

                Console.WriteLine($"[{schema}].[{name}]");

                cmd.CommandText = $"select * from {dictionary.Name}";
                StreamWriter streamWriter = null;
                CsvWriter csvWriter = null;
                try
                {
                    streamWriter = new StreamWriter(Path.Combine(dataDir, $"{schema}.{name}.csv"));
                    csvWriter = new CsvWriter(streamWriter, new CsvConfiguration());

                    using (var reader = cmd.ExecuteReader())
                    {
                        for (int i = 0; i < reader.FieldCount; ++i)
                        {
                            csvWriter.WriteField(reader.GetName(i));
                        }
                        csvWriter.NextRecord();

                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; ++i)
                            {
                                csvWriter.WriteField(reader.GetValue(i));
                            }
                            csvWriter.NextRecord();
                        }
                    }
                }
                finally
                {
                    if (csvWriter != null)
                        csvWriter.Dispose();

                    if (streamWriter != null)
                        streamWriter.Dispose();
                }
            }
        }
    }
}
