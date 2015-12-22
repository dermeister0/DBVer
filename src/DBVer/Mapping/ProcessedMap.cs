using System.Collections.Generic;

namespace DBVer.Mapping
{
    internal class ProcessedMap
    {
        private readonly HashSet<string> map = new HashSet<string>();

        public void Add(string schema, string objectName, ObjectType objectType)
        {
            map.Add(GetFullName(schema, objectName, objectType));
        }

        public bool Contains(string schema, string objectName, ObjectType objectType)
        {
            return map.Contains(GetFullName(schema, objectName, objectType));
        }

        private string GetFullName(string schema, string objectName, ObjectType objectType)
        {
            return $"{objectName}_{schema}.{objectType}";
        }
    }
}
