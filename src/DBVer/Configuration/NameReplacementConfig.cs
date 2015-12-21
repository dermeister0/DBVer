using System.Configuration;

namespace DBVer.Configuration
{
    public class NameReplacementConfig : ConfigurationElement
    {
        [ConfigurationProperty("type", IsRequired = true)]
        public ObjectType Type
        {
            get { return (ObjectType)this["type"]; }
            set { this["type"] = value; }
        }

        [ConfigurationProperty("pattern", IsRequired = true)]
        public string Pattern
        {
            get { return (string)this["pattern"]; }
            set { this["pattern"] = value; }
        }

        [ConfigurationProperty("replacement", IsRequired = true)]
        public string Replacement
        {
            get { return (string) this["replacement"]; }
            set { this["replacement"] = value; }
        }
    }
}
