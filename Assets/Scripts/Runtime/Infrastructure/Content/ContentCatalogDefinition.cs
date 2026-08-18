using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(
        menuName = "DarkFlare/Infrastructure/Content Catalog",
        fileName = "ContentCatalog")]
    public sealed class ContentCatalogDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("目录ID")]
        string _catalogId = "core";

        [SerializeField]
        [MinValue(1)]
        [LabelText("内容版本")]
        int _contentVersion = 1;

        [SerializeField]
        [ListDrawerSettings(Expanded = true)]
        [LabelText("正式内容")]
        List<ScriptableObject> _entries = new List<ScriptableObject>();

        public string CatalogId => _catalogId;

        public int ContentVersion => _contentVersion;

        public IReadOnlyList<ScriptableObject> Entries => _entries;
    }
}
