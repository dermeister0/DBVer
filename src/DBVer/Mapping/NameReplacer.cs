using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DBVer.Configuration;

namespace DBVer.Mapping
{
    internal class NameReplacer
    {
        private Dictionary<ObjectType, Dictionary<Regex, string>> mappings;

        public NameReplacer()
        {
            Load();
        }

        private void Load()
        {
            mappings = new Dictionary<ObjectType, Dictionary<Regex, string>>();
            var exportSettingsSection = ExportSettingsSection.ReadFromConfig();

            foreach (NameReplacementGroup group in exportSettingsSection.NameReplacementGroups)
            {
                var replacementSet = group.Definitions.Cast<NameReplacementDefinition>()
                    .ToDictionary(definition => new Regex(definition.Pattern), definition => definition.Replacement);
                mappings[group.Type] = replacementSet;
            }
        }

        public string ReplaceName(string name, ObjectType type)
        {
            if (!mappings.ContainsKey(type))
                return name;

            foreach (var definition in mappings[type])
            {
                if (definition.Key.IsMatch(name))
                {
                    return definition.Key.Replace(name, definition.Value);
                }
            }

            return name;
        }
    }
}
