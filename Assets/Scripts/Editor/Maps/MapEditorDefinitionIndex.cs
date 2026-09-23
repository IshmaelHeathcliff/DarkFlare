using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DarkFlare.Editor
{
    [InitializeOnLoad]
    public static class MapEditorDefinitionIndex
    {
        public const string DependencyName = "DarkFlare/MapDefinitionIndex";

        static Hash128 s_registeredHash;
        static bool s_registered;

        static readonly Type[] DefinitionTypes =
        {
            typeof(TerrainDefinition), typeof(TerrainSetDefinition), typeof(MapObjectDefinition),
            typeof(MapRegionDefinition), typeof(TraderDefinition), typeof(CraftingDefinition), typeof(MonsterSpawnDefinition),
            typeof(MonsterDefinition)
        };

        static MapEditorDefinitionIndex()
        {
            ScheduleUpdate();
        }

        public static List<ScriptableObject> LoadDefinitions()
        {
            var assets = new List<ScriptableObject>();
            foreach (Type type in DefinitionTypes)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets" }).OrderBy(value => value, StringComparer.Ordinal))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    // 地图和 ContentCatalog 不属于索引，避免导入依赖循环。
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (asset != null && asset.GetType() == type) { assets.Add((ScriptableObject)asset); }
                    }
                }
            }
            return assets;
        }

        public static IMapDefinitionLookup Capture(AssetImportContext context = null)
        {
            List<ScriptableObject> entries = LoadDefinitions();
            if (context != null)
            {
                // ID 索引读取了这些源的身份。即使 Resolve 失败也保留这些依赖：
                // 原生 SO 的 SaveAssets 不保证再次经过 OnPostprocessAllAssets。
                foreach (ScriptableObject entry in entries)
                {
                    context.DependsOnSourceAsset(AssetDatabase.GetAssetPath(entry));
                }
            }
            return new TrackingLookup(new MapDefinitionLookup(entries), context);
        }

        // 只能在导入回调之外注册；同一 Hash 不会使地图反复失效。
        public static void UpdateDependency()
        {
            EditorApplication.delayCall -= UpdateDependency;
            var text = new StringBuilder();
            foreach (ScriptableObject asset in LoadDefinitions())
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
                text.Append(asset.GetType().FullName).Append('|').Append(((IContentDefinition)asset).Id)
                    .Append('|').Append(guid).Append('|').Append(localId).Append('|').Append(AssetDatabase.GetAssetPath(asset)).Append('\n');
            }
            Hash128 hash = Hash128.Compute(text.ToString());
            if (s_registered && hash == s_registeredHash) { return; }
            AssetDatabase.RegisterCustomDependency(DependencyName, hash);
            s_registeredHash = hash;
            s_registered = true;
            // 注册只标记依赖失效；受控刷新让新增/删除定义立即驱动恢复。
            // 本方法在 delayCall 或显式编辑入口运行，绝不从 OnImportAsset 调用。
            AssetDatabase.Refresh();
        }

        internal static void ScheduleUpdate()
        {
            EditorApplication.delayCall -= UpdateDependency;
            EditorApplication.delayCall += UpdateDependency;
        }

        sealed class TrackingLookup : IMapDefinitionLookup
        {
            readonly MapDefinitionLookup _lookup;
            readonly AssetImportContext _context;

            public TrackingLookup(MapDefinitionLookup lookup, AssetImportContext context)
            {
                _lookup = lookup;
                _context = context;
            }

            public T Resolve<T>(ContentId id) where T : ScriptableObject, IContentDefinition
            {
                T asset = _lookup.Resolve<T>(id);
                if (_context != null)
                {
                    string path = AssetDatabase.GetAssetPath(asset);
                    _context.DependsOnSourceAsset(path);
                    _context.DependsOnArtifact(path);
                }
                return asset;
            }
        }
    }

    sealed class MapDefinitionIndexPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)))
            {
                MapEditorDefinitionIndex.ScheduleUpdate();
            }
        }
    }
}
