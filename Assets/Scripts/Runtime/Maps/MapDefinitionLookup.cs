using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public interface IMapDefinitionLookup
    {
        T Resolve<T>(ContentId id) where T : ScriptableObject, IContentDefinition;
    }

    // 显式快照；冲突项不能回退到“扫描到的第一个”。不查询场景或 AssetDatabase。
    public sealed class MapDefinitionLookup : IMapDefinitionLookup
    {
        readonly Dictionary<ContentId, ScriptableObject> _entries = new Dictionary<ContentId, ScriptableObject>();
        readonly HashSet<ContentId> _duplicates = new HashSet<ContentId>();

        public MapDefinitionLookup(IEnumerable<ScriptableObject> entries)
        {
            foreach (ScriptableObject asset in entries)
            {
                if (asset == null || !(asset is IContentDefinition definition)
                    || !ContentDefinitionRegistry.TryGet(asset.GetType(), out ContentDefinitionMetadata metadata)
                    || !ContentId.TryCreate(metadata.Namespace, definition.Id, out ContentId id)) { continue; }
                if (_entries.ContainsKey(id)) { _duplicates.Add(id); }
                else { _entries.Add(id, asset); }
            }
        }

        public T Resolve<T>(ContentId id) where T : ScriptableObject, IContentDefinition
        {
            if (_duplicates.Contains(id)) { throw new MapDocumentException(id.ToString(), "定义 ID 冲突"); }
            if (!_entries.TryGetValue(id, out ScriptableObject asset) || !(asset is T typed))
            {
                throw new MapDocumentException(id.ToString(), $"缺少 {typeof(T).Name} 定义");
            }
            return typed;
        }
    }
}
