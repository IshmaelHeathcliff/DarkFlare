using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public readonly struct LocalizedMessage
    {
        readonly object[] _arguments;

        public string TableName { get; }

        public string EntryKey { get; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(TableName)
            || string.IsNullOrWhiteSpace(EntryKey);

        internal IList<object> Arguments => _arguments ?? Array.Empty<object>();

        public LocalizedMessage(
            string tableName,
            string entryKey,
            params object[] arguments)
        {
            TableName = tableName ?? string.Empty;
            EntryKey = entryKey ?? string.Empty;
            _arguments = arguments == null || arguments.Length == 0
                ? Array.Empty<object>()
                : (object[])arguments.Clone();
        }

        public static LocalizedMessage Ui(string entryKey, params object[] arguments)
        {
            return new LocalizedMessage("ui", entryKey, arguments);
        }
    }
}
