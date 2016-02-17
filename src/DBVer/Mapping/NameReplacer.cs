using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DBVer.Configuration;

namespace DBVer.Mapping
{
    internal class ReplacementConfig
    {
        public string NamePattern { get; set; }
        public Dictionary<Regex, string> ContentMappings { get; set; }
    }

    internal class NameReplacementResult
    {
        private ReplacementConfig config;

        public string OldName { get; }
        public string NewName { get; }

        public NameReplacementResult(ReplacementConfig config, string oldName, string newName)
        {
            this.config = config;
            OldName = oldName;
            NewName = newName;
        }
    }

    internal class NameReplacer
    {
        private Dictionary<ObjectType, Dictionary<Regex, ReplacementConfig>> mappings;
        private ExportSettingsSection exportSettingsSection;

        public NameReplacer(ExportSettingsSection exportSettingsSection)
        {
            this.exportSettingsSection = exportSettingsSection;

            Load();
        }

        private void Load()
        {
            mappings = new Dictionary<ObjectType, Dictionary<Regex, ReplacementConfig>>();

            foreach (NameReplacementGroup group in exportSettingsSection.NameReplacementGroups)
            {
                var replacementSet = group.Definitions.Cast<NameReplacementDefinition>()
                    .ToDictionary(definition => new Regex(definition.Pattern),
                        definition => new ReplacementConfig
                            {
                                NamePattern = definition.Replacement,
                                ContentMappings = definition.ContentReplacements.Count > 0
                                    ? definition.ContentReplacements.Cast<ContentReplacementDefinition>().ToDictionary(d => new Regex(d.Pattern), d => d.Replacement)
                                    : null
                            });
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
                    return definition.Key.Replace(name, definition.Value.NamePattern);
                }
            }

            return name;
        }
    }
}
