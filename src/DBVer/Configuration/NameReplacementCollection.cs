using System;
using System.Configuration;

namespace DBVer.Configuration
{
    public class NameReplacementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new NameReplacementConfig();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((NameReplacementConfig) element).Type;
        }
    }
}
