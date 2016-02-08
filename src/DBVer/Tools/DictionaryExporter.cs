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
                cmd.CommandText = "select object_id from sys.tables where name = @Name";
                cmd.Parameters.AddWithValue("@Name", dictionary.Name);

                var result = cmd.ExecuteScalar();
                if (Convert.IsDBNull(result))
                {
                    Console.WriteLine("Warning: Dictionary {0} not found.", dictionary.Name);
                }

                cmd.CommandText = $"select * from {dictionary.Name}";

                // TODO: Finish the export.
            }
        }
    }
}
