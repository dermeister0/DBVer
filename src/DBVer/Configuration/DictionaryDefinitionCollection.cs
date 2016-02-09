using System.Configuration;

namespace DBVer.Configuration
{
    [ConfigurationCollection(typeof(DictionaryDefinitionCollection))]
    public class DictionaryDefinitionCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new DictionaryDefinition();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((DictionaryDefinition)element).Name;
        }
    }
}
