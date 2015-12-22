using System.Configuration;

namespace DBVer.Configuration
{
    public class MappingSection : ConfigurationSection
    {
        private const string NameReplacementCollectionName = "nameReplacement";

        [ConfigurationProperty(NameReplacementCollectionName)]
        public NameReplacementGroupCollection NameReplacementGroups => (NameReplacementGroupCollection)base[NameReplacementCollectionName];

        public static MappingSection ReadFromConfig()
        {
            return ConfigurationManager.GetSection("mapping") as MappingSection;
        }
    }
}
