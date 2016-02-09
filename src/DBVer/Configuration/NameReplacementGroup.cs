using System.Configuration;

namespace DBVer.Configuration
{
    public class NameReplacementGroup : ConfigurationElement
    {
        [ConfigurationProperty("type", IsRequired = true)]
        public ObjectType Type
        {
            get { return (ObjectType)this["type"]; }
            set { this["type"] = value; }
        }

        [ConfigurationProperty("", IsDefaultCollection = true)]
        public NameReplacementDefinitionCollection Definitions => (NameReplacementDefinitionCollection)base[""];
    }
}
