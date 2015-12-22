using System.Collections.Generic;
using System.Threading;

namespace DBVer.Mapping
{
    internal class ProcessedMap
    {
        private readonly HashSet<string> map = new HashSet<string>();
        private readonly ReaderWriterLockSlim lockObject = new ReaderWriterLockSlim();

        public void Add(string schema, string objectName, ObjectType objectType)
        {
            try
            {
                lockObject.EnterWriteLock();
                map.Add(GetFullName(schema, objectName, objectType));
            }
            finally
            {
                lockObject.ExitWriteLock();
            }
        }

        public bool Contains(string schema, string objectName, ObjectType objectType)
        {
            try
            {
                lockObject.EnterReadLock();
                return map.Contains(GetFullName(schema, objectName, objectType));
            }
            finally
            {
                lockObject.ExitReadLock();
            }
        }

        private string GetFullName(string schema, string objectName, ObjectType objectType)
        {
            return $"{objectName}_{schema}.{objectType}";
        }
    }
}
