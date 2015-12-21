using System.Configuration;

namespace DBVer.Configuration
{
    public class MappingSection : ConfigurationSection
    {
        private const string NameReplacementCollectionName = "nameReplacement";

        [ConfigurationProperty(NameReplacementCollectionName)]
        [ConfigurationCollection(typeof(NameReplacementCollection))]
        public NameReplacementCollection NameReplacementConfigs {get { return (NameReplacementCollection)base[NameReplacementCollectionName]; } }

        public static MappingSection ReadFromConfig()
        {
            return ConfigurationManager.GetSection("mapping") as MappingSection;
        }
    }
}
