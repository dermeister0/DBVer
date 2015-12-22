using System.Configuration;

namespace DBVer.Configuration
{
    [ConfigurationCollection(typeof(NameReplacementGroupCollection), AddItemName = "group")]
    public class NameReplacementGroupCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new NameReplacementGroup();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((NameReplacementGroup) element).Type;
        }
    }
}
