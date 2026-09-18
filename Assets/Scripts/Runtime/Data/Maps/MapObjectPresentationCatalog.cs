using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    [Serializable]
    public sealed class MapObjectAppearance
    {
        [SerializeField, LabelText("外观 ID（空为默认）")] string _variantId = "";
        [SerializeField, LabelText("纯表现 Prefab")] AssetReferenceGameObject _prefab;
        public string VariantId => _variantId ?? "";
        public AssetReferenceGameObject Prefab => _prefab;
    }

    [Serializable]
    public sealed class MapObjectPresentationEntry
    {
        [SerializeField, LabelText("对象定义")] MapObjectDefinition _definition;
        [SerializeField, LabelText("外观绑定")] MapObjectAppearance[] _appearances = Array.Empty<MapObjectAppearance>();
        public MapObjectDefinition Definition => _definition;
        public IReadOnlyList<MapObjectAppearance> Appearances => _appearances;
    }

    [CreateAssetMenu(menuName = "DarkFlare/Maps/Object Presentation Catalog")]
    public sealed class MapObjectPresentationCatalog : ScriptableObject
    {
        [SerializeField, LabelText("对象表现目录")] MapObjectPresentationEntry[] _entries = Array.Empty<MapObjectPresentationEntry>();
        public IReadOnlyList<MapObjectPresentationEntry> Entries => _entries;

        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MapObjectPresentationEntry entry in _entries)
            {
                if (entry?.Definition == null || !ids.Add(entry.Definition.Id)) { throw new InvalidOperationException("表现目录对象定义缺失或重复"); }
                var variants = new HashSet<string>(StringComparer.Ordinal);
                foreach (MapObjectAppearance appearance in entry.Appearances)
                {
                    if (appearance == null || !variants.Add(appearance.VariantId) || appearance.Prefab == null || !appearance.Prefab.RuntimeKeyIsValid())
                    { throw new InvalidOperationException(entry.Definition.Id + "：外观重复或资源引用无效"); }
                    if (appearance.VariantId.Length > 0 && !Contains(entry.Definition.AppearanceVariants, appearance.VariantId))
                    { throw new InvalidOperationException(entry.Definition.Id + "：外观不属于对象定义"); }
                }
                if (!variants.Contains("")) { throw new InvalidOperationException(entry.Definition.Id + "：缺少默认外观"); }
                foreach (string variant in entry.Definition.AppearanceVariants)
                { if (!variants.Contains(variant)) { throw new InvalidOperationException(entry.Definition.Id + "：缺少外观 " + variant); } }
            }
        }

        public AssetReferenceGameObject Resolve(string definitionId, string variantId)
        {
            foreach (MapObjectPresentationEntry entry in _entries)
            {
                if (entry?.Definition == null || "map_object:" + entry.Definition.Id != definitionId) { continue; }
                foreach (MapObjectAppearance appearance in entry.Appearances)
                { if (appearance.VariantId == (variantId ?? "")) { return appearance.Prefab; } }
                break;
            }
            throw new InvalidOperationException("缺少正式表现：" + definitionId + "/" + variantId);
        }

        static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++) { if (values[i] == value) { return true; } }
            return false;
        }
    }
}
