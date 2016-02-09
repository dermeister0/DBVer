using System.Configuration;

namespace DBVer.Configuration
{
    public class ExportSettingsSection : ConfigurationSection
    {
        private const string NameReplacementCollectionName = "nameReplacement";
        private const string DictionaryCollectionName = "dictionaries";

        [ConfigurationProperty(NameReplacementCollectionName)]
        public NameReplacementGroupCollection NameReplacementGroups => (NameReplacementGroupCollection)base[NameReplacementCollectionName];

        [ConfigurationProperty(DictionaryCollectionName)]
        public DictionaryDefinitionCollection Dictionaries => (DictionaryDefinitionCollection)base[DictionaryCollectionName];

        public static ExportSettingsSection ReadFromConfig()
        {
            return ConfigurationManager.GetSection("exportSettings") as ExportSettingsSection;
        }
    }
}
