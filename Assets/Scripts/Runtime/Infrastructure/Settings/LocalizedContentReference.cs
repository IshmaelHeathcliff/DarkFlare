using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Serializable]
    public sealed class LocalizedContentReference
    {
        [SerializeField]
        [LabelText("本地化表")]
        string _tableName = string.Empty;

        [SerializeField]
        [LabelText("本地化条目")]
        string _entryKey = string.Empty;

        public string TableName => _tableName;

        public string EntryKey => _entryKey;

        public bool IsValid => !string.IsNullOrWhiteSpace(_tableName)
            && !string.IsNullOrWhiteSpace(_entryKey);

        public LocalizedMessage Message => new LocalizedMessage(_tableName, _entryKey);

        public LocalizedContentReference()
        {
        }

        public LocalizedContentReference(string tableName, string entryKey)
        {
            _tableName = tableName ?? string.Empty;
            _entryKey = entryKey ?? string.Empty;
        }
    }
}
