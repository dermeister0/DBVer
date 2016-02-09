using System.Configuration;

namespace DBVer.Configuration
{
    [ConfigurationCollection(typeof(NameReplacementDefinitionCollection))]
    public class NameReplacementDefinitionCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new NameReplacementDefinition();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((NameReplacementDefinition)element).Pattern;
        }
    }
}
