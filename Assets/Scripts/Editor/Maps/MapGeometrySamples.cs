using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public static class MapGeometrySamples
    {
        public const string Path = MapDiagnosticSamples.Root + "/terrain_geometry.dfmap";

        [MenuItem("DarkFlare/地图/打开同源几何样例")]
        public static void Open()
        {
            Ensure();
            MapTerrainPreviewWindow.OpenGeometry(Path, AssetDatabase.LoadAssetAtPath<TerrainTextureSources>(MapFormalSamples.SourcePath));
        }

        public static void Ensure()
        {
            CreateObstacle("geometry_pillar", new Vector2(0.5f, 3));
            CreateObstacle("geometry_bar", new Vector2(3.5f, 0.5f));
            CreateRegion("geometry_safe", MapRegionKind.Safe);
            CreateRegion("geometry_spawn", MapRegionKind.Spawn);
            MapEditorDefinitionIndex.UpdateDependency();
            if (!File.Exists(Path)) { MapDocumentPage.SaveSource(Path, MapDocumentJson.Write(CreateDocument())); }
        }

        public static MapDocument CreateDocument()
        {
            MapDocument doc = MapFormalSamples.CreateTopologyDocument();
            doc.MapId = "map:terrain_geometry";
            doc.Objects = new[]
            {
                new MapObjectRecord { ObjectId = "west_pillar", DefinitionId = "map_object:geometry_pillar", Position = new MapPoint { X = 2, Y = 3 } },
                new MapObjectRecord { ObjectId = "east_pillar", DefinitionId = "map_object:geometry_pillar", Position = new MapPoint { X = 5, Y = 3 } },
                new MapObjectRecord { ObjectId = "bar", DefinitionId = "map_object:geometry_bar", Position = new MapPoint { X = 3.5, Y = 4.5 } }
            };
            var spawn = AssetDatabase.LoadAssetAtPath<MonsterSpawnDefinition>("Assets/Data/Preset/Monsters/基础刷怪表.asset");
            if (spawn == null) { throw new InvalidOperationException("缺少基础刷怪表"); }
            doc.Regions = new[]
            {
                new MapRegionRecord { RegionId = "spawn", DefinitionId = "map_region:geometry_spawn", Priority = 1,
                    SpawnDefinitionId = new ContentId(ContentNamespaces.Spawn, spawn.Id).ToString(),
                    Points = new[] { new MapPoint { X = 8, Y = 1 }, new MapPoint { X = 15, Y = 7 } } },
                new MapRegionRecord { RegionId = "safe", DefinitionId = "map_region:geometry_safe", Priority = 2,
                    Points = new[] { new MapPoint { X = 8, Y = 1 }, new MapPoint { X = 11, Y = 4 } } }
            };
            return doc;
        }

        static void CreateObstacle(string id, Vector2 size)
        {
            Create<MapObjectDefinition>(id, serialized =>
            {
                serialized.FindProperty("_capabilities").intValue = (int)MapObjectCapabilities.Obstacle;
                serialized.FindProperty("_footprint").vector2Value = size;
            });
        }
        static void CreateRegion(string id, MapRegionKind kind)
        {
            Create<MapRegionDefinition>(id, serialized => serialized.FindProperty("_kind").enumValueIndex = (int)kind);
        }
        static void Create<T>(string id, Action<SerializedObject> configure) where T : ScriptableObject
        {
            string path = MapDiagnosticSamples.Root + "/" + id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) { return; }
            if (File.Exists(path)) { throw new InvalidOperationException("路径已有其他资产：" + path); }
            var asset = ScriptableObject.CreateInstance<T>();
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_id").stringValue = id;
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
