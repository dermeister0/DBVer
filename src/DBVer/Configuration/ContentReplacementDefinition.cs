using System.Configuration;

namespace DBVer.Configuration
{
    public class ContentReplacementDefinition : ConfigurationElement
    {
        [ConfigurationProperty("pattern", IsRequired = true)]
        public string Pattern
        {
            get { return (string)this["pattern"]; }
            set { this["pattern"] = value; }
        }

        [ConfigurationProperty("replacement", IsRequired = true)]
        public string Replacement
        {
            get { return (string)this["replacement"]; }
            set { this["replacement"] = value; }
        }
    }
}
