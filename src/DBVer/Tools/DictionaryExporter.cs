using DBVer.Configuration;
using System;
using System.Data;
using System.Data.SqlClient;

namespace DBVer.Tools
{
    internal class DictionaryExporter
    {
        private ExportSettingsSection exportSettingsSection;

        public DictionaryExporter(ExportSettingsSection exportSettingsSection)
        {
            this.exportSettingsSection = exportSettingsSection;
        }

        public void Run(SqlConnection connection)
        {
            foreach (DictionaryDefinition dictionary in exportSettingsSection.Dictionaries)
            {
                ExportDictionary(connection, dictionary);
            }
        }

        private void ExportDictionary(SqlConnection connection, DictionaryDefinition dictionary)
        {
            if (string.IsNullOrWhiteSpace(dictionary.Name))
                throw new InvalidOperationException("Dictionary name is empty.");

            if (dictionary.Name.Contains("."))
                throw new NotSupportedException("Only default schema is supported.");

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "select '[' + S.name + '].[' + T.name + ']' FROM sys.tables T JOIN sys.schemas S ON T.schema_id = S.schema_id where T.name = @Name";
                cmd.Parameters.AddWithValue("@Name", dictionary.Name);

                var fullName = cmd.ExecuteScalar();
                if (fullName == null)
                {
                    Console.WriteLine("Warning: Dictionary {0} not found.", dictionary.Name);
                    return;
                }

                Console.WriteLine(fullName);

                cmd.CommandText = $"select * from {dictionary.Name}";
                using (var reader = cmd.ExecuteReader())
                {
                    for (int i = 0; i < reader.FieldCount; ++i)
                    {
                        Console.Write("{0};", reader.GetName(i));
                    }
                    Console.WriteLine();

                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; ++i)
                        {
                            Console.Write("{0};", reader.GetValue(i));
                        }
                        Console.WriteLine();
                    }
                }

                // TODO: Finish the export.
            }
        }
    }
}
