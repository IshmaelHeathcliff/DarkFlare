using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public static class MapDiagnosticSamples
    {
        public const string Root = "Assets/Data/Development/Maps";
        public const string SetPath = Root + "/preview_terrain_set.asset";
        public const string TopologyPath = Root + "/preview_topology.dfmap";
        public const string BoundaryPath = Root + "/preview_boundary.dfmap";
        static readonly string[] TerrainNames = { "dirt", "grass", "water", "road" };

        public static void EnsureSamples()
        {
            EnsureFolder(Root);
            var terrains = new TerrainDefinition[4];
            for (int i = 0; i < terrains.Length; i++)
            {
                string path = Root + "/preview_" + TerrainNames[i] + ".asset";
                terrains[i] = AssetDatabase.LoadAssetAtPath<TerrainDefinition>(path);
                if (terrains[i] != null) { continue; }
                RequireMissing(path);
                var terrain = ScriptableObject.CreateInstance<TerrainDefinition>();
                var serialized = new SerializedObject(terrain);
                serialized.FindProperty("_id").stringValue = "preview_" + TerrainNames[i];
                serialized.FindProperty("_walkable").boolValue = i != 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(terrain, path);
                terrains[i] = terrain;
            }
            if (AssetDatabase.LoadAssetAtPath<TerrainSetDefinition>(SetPath) == null)
            {
                RequireMissing(SetPath);
                var set = ScriptableObject.CreateInstance<TerrainSetDefinition>();
                var serialized = new SerializedObject(set);
                serialized.FindProperty("_id").stringValue = "preview_set";
                SerializedProperty palette = serialized.FindProperty("_terrains");
                palette.arraySize = terrains.Length;
                for (int i = 0; i < terrains.Length; i++) { palette.GetArrayElementAtIndex(i).objectReferenceValue = terrains[i]; }
                SerializedProperty variants = serialized.FindProperty("_variants");
                variants.arraySize = 8;
                for (int i = 0; i < variants.arraySize; i++)
                {
                    SerializedProperty variant = variants.GetArrayElementAtIndex(i);
                    variant.FindPropertyRelative("_id").stringValue = VariantId(i / 2, i % 2);
                    foreach (string corner in new[] { "_northWest", "_northEast", "_southEast", "_southWest" })
                    {
                        variant.FindPropertyRelative(corner).objectReferenceValue = terrains[i / 2];
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            MapEditorDefinitionIndex.UpdateDependency();
            if (!File.Exists(TopologyPath)) { MapDocumentPage.SaveSource(TopologyPath, MapDocumentJson.Write(CreateTopologyDocument())); }
            if (!File.Exists(BoundaryPath)) { MapDocumentPage.SaveSource(BoundaryPath, MapDocumentJson.Write(CreateBoundaryDocument())); }
        }

        [MenuItem("DarkFlare/地图/打开拓扑诊断样例")]
        public static void OpenTopology()
        {
            EnsureSamples();
            MapTerrainPreviewWindow.OpenSource(TopologyPath);
        }

        [MenuItem("DarkFlare/地图/打开边界诊断样例")]
        public static void OpenBoundary()
        {
            EnsureSamples();
            MapTerrainPreviewWindow.OpenSource(BoundaryPath);
        }

        [MenuItem("DarkFlare/地图/生成并预览 256×256 压力地图")]
        public static void OpenStress()
        {
            EnsureSamples();
            CompiledMapDocument map = MapDocumentCompiler.Compile(CreateStressDocument(), MapEditorDefinitionIndex.Capture());
            const string path = "Temp/DarkFlare/preview_stress.dfmap";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, MapDocumentJson.Encode(map.CanonicalJson));
            MapTerrainPreviewWindow.OpenSource(path);
        }

        public static MapDocument CreateTopologyDocument()
        {
            MapDocument source = Create("preview_topology", 40, 24, (x, y) =>
            {
                int panel = x / 8;
                int u = x % 8;
                int v = y % 8;
                if (y < 8)
                {
                    if (panel < 4) { return panel; }
                    return u >= 2 && u <= 5 && v >= 2 && v <= 5 ? ((u + v) % 2 == 0 ? 2 : 1) : 1;
                }
                if (y < 16)
                {
                    bool road = panel switch
                    {
                        0 => v == 3 && u >= 1 && u <= 6,
                        1 => (u == 2 && v >= 2 && v <= 6) || (v == 2 && u >= 2 && u <= 6),
                        2 => (v == 5 && u >= 1 && u <= 6) || (u == 3 && v >= 1 && v <= 5),
                        3 => (v == 3 && u >= 1 && u <= 6) || (u == 3 && v >= 1 && v <= 6),
                        _ => u == 3 && v == 3
                    };
                    return road ? 3 : 1;
                }
                if (panel == 0) { return u == 3 && v == 3 ? 2 : 1; }
                if (panel == 1) { return u >= 1 && u <= 6 && v >= 1 && v <= 6 && !(u >= 3 && u <= 4 && v >= 3 && v <= 4) ? 2 : 1; }
                if (panel == 2) { return u < 4 ? (v < 4 ? 0 : 2) : 1; }
                if (panel == 3) { return u < 4 ? (v < 4 ? 0 : 1) : (v < 4 ? 3 : 2); }
                return (u == 3 && v == 3) || (u == 4 && v == 4) ? 2 : 1;
            });
            source.VariantOverrides = Enumerable.Range(0, 8).Select(i => new MapVariantOverride
            {
                X = i / 2 * 8 + 1 + i % 2 * 3, Y = 2, VariantId = VariantId(i / 2, i % 2)
            }).ToArray();
            return source;
        }

        public static MapDocument CreateBoundaryDocument()
        {
            MapDocument source = Create("preview_boundary", 35, 33, (x, y) =>
                x >= 30 || y >= 30 ? (x + y) % 4 : x < 12 ? 1 : x < 23 ? 2 : 3);
            source.Origin = new MapPoint { X = -13.25, Y = 7.5 };
            source.CellSize = 0.5;
            source.Chunks = source.Chunks.Where(chunk => chunk.X != 1 || chunk.Y != 1).ToArray();
            return source;
        }

        public static MapDocument CreateStressDocument(int seed = 19260817)
        {
            MapDocument source = Create("preview_stress", 256, 256, (x, y) =>
            {
                uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ seed));
                hash ^= hash >> 13;
                hash = unchecked(hash * 1274126177);
                return (int)((hash ^ (hash >> 16)) & 3);
            });
            source.Seed = seed;
            return source;
        }

        static MapDocument Create(string id, int width, int height, Func<int, int, int> terrain)
        {
            var chunks = new List<MapChunk>();
            for (int cy = 0; cy * 32 < height; cy++)
            {
                for (int cx = 0; cx * 32 < width; cx++)
                {
                    var chunk = new MapChunk { X = cx, Y = cy, Cells = new int[1024] };
                    for (int y = 0; y < 32 && cy * 32 + y < height; y++)
                    {
                        for (int x = 0; x < 32 && cx * 32 + x < width; x++) { chunk.Cells[x + y * 32] = terrain(cx * 32 + x, cy * 32 + y); }
                    }
                    chunks.Add(chunk);
                }
            }
            return new MapDocument
            {
                MapId = "map:" + id, Width = width, Height = height, TerrainSetId = "terrain_set:preview_set",
                TerrainPalette = TerrainNames.Select(name => "terrain:preview_" + name).ToArray(),
                DefaultTerrainId = "terrain:preview_dirt", Chunks = chunks.ToArray()
            };
        }

        static string VariantId(int terrain, int variant)
        {
            return "preview_" + TerrainNames[terrain] + (variant == 0 ? "_a" : "_b");
        }

        static void RequireMissing(string path)
        {
            if (File.Exists(path)) { throw new InvalidOperationException("路径已有其他资产，拒绝覆盖：" + path); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
