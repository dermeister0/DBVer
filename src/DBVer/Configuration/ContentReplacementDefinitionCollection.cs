using System.Configuration;

namespace DBVer.Configuration
{
    [ConfigurationCollection(typeof(ContentReplacementDefinitionCollection), AddItemName = "contentReplacement")]
    public class ContentReplacementDefinitionCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new ContentReplacementDefinition();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ContentReplacementDefinition)element).Pattern;
        }
    }
}
