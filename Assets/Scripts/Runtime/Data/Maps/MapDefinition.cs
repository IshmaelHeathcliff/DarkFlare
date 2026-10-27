using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    // 唯一布局源是 .dfmap；这些字段只由导入器写入。
    [ContentDefinition(ContentNamespaces.Map)]
    public sealed class MapDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField, HideInInspector] string _id;
        [SerializeField, HideInInspector] string _canonicalJson;
        [SerializeField, HideInInspector] string _sourceHash;
        [SerializeField, HideInInspector] string _previousLayoutJson;

        [ShowInInspector, ReadOnly, LabelText("稳定 ID")] public string Id => _id;
        [ShowInInspector, ReadOnly, LabelText("文档 SHA256")] public string SourceHash => _sourceHash;
        public string CanonicalJson => _canonicalJson;
        public string PreviousLayoutJson => _previousLayoutJson;

        internal void Initialize(CompiledMapDocument document, string previousLayoutJson = null)
        {
            _id = ContentId.Parse(document.MapId).LocalId;
            _canonicalJson = document.CanonicalJson;
            _sourceHash = document.SourceHash;
            _previousLayoutJson = previousLayoutJson;
        }
    }
}
