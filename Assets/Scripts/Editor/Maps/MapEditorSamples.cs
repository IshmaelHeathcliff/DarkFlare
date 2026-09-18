using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public static class MapEditorSamples
    {
        public const string Root = "Assets/Data/Maps/EditorSamples";
        public const string TownPath = Root + "/town_draft.dfmap";
        public const string WildernessPath = Root + "/wilderness_draft.dfmap";

        [MenuItem("DarkFlare/地图/编辑样例/城镇布局草稿")]
        public static void OpenTown() { Ensure(); MapEditorWindow.OpenSource(TownPath); }
        [MenuItem("DarkFlare/地图/编辑样例/野外布局草稿")]
        public static void OpenWilderness() { Ensure(); MapEditorWindow.OpenSource(WildernessPath); }

        public static void Ensure()
        {
            if (!AssetDatabase.IsValidFolder(Root)) { AssetDatabase.CreateFolder("Assets/Data/Maps", "EditorSamples"); }
            DefineObject("editor_tree", MapObjectCapabilities.Obstacle | MapObjectCapabilities.Decoration, MapInteractionKind.None, new Vector2(1.5f, 1.5f));
            DefineObject("editor_trader", MapObjectCapabilities.Obstacle | MapObjectCapabilities.Decoration | MapObjectCapabilities.Interaction, MapInteractionKind.Trader, Vector2.one);
            DefineObject("editor_crafting", MapObjectCapabilities.Obstacle | MapObjectCapabilities.Decoration | MapObjectCapabilities.Interaction, MapInteractionKind.Crafting, new Vector2(2, 1));
            DefineRegion("editor_safe", MapRegionKind.Safe); DefineRegion("editor_spawn", MapRegionKind.Spawn); DefineRegion("editor_named", MapRegionKind.Named);
            AssetDatabase.SaveAssets(); MapEditorDefinitionIndex.UpdateDependency();
            EnsureMap(TownPath, false); EnsureMap(WildernessPath, true);
        }

        public static MapDocument Blank(string id, int size = 64)
        {
            return new MapDocument
            {
                MapId = "map:" + id, Width = size, Height = size, TerrainSetId = "terrain_set:basic",
                TerrainPalette = new[] { "terrain:dirt", "terrain:grass", "terrain:water", "terrain:road" }, DefaultTerrainId = "terrain:dirt", Seed = 1805
            };
        }

        static void EnsureMap(string path, bool wild)
        {
            if (File.Exists(path)) { return; }
            MapEditSession session = MapEditSessions.Create(path, Blank(wild ? "wilderness_draft" : "town_draft"));
            JObject patch = CreatePatch(session.Current, wild);
            session.Apply(patch); session.Save();
        }

        public static JObject CreatePatch(CompiledMapDocument map, bool wild)
        {
            var operations = new List<JObject>
            {
                Rect("grass", 1, 1, 62, 62), Rect("road", 29, 1, 5, 62), Rect("road", 1, 29, 62, 5),
                Rect("water", 6, 43, 14, 12), Rect("dirt", 38, 38, 18, 16)
            };
            string trader = "trader:" + MapEditorDefinitionIndex.LoadDefinitions().OfType<TraderDefinition>().First().Id;
            string crafting = "crafting:" + MapEditorDefinitionIndex.LoadDefinitions().OfType<CraftingDefinition>().First().Id;
            string spawn = "spawn:" + MapEditorDefinitionIndex.LoadDefinitions().OfType<MonsterSpawnDefinition>().First().Id;
            if (wild)
            {
                operations.Add(Rect("water", 44, 6, 8, 15));
                for (int i = 0; i < 12; i++) { operations.Add(Object("tree_" + i, "editor_tree", 7 + i % 4 * 5, 8 + i / 4 * 5)); }
                operations.Add(Region("wild_spawn", "editor_spawn", 5, 5, 58, 58, spawn));
                operations.Add(Region("arrival_safe", "editor_safe", 27, 1, 36, 8));
            }
            else
            {
                operations.Add(Rect("road", 23, 23, 18, 18));
                operations.Add(Object("town_trader", "editor_trader", 26.5, 37.5, "Trader", trader));
                operations.Add(Object("town_crafting", "editor_crafting", 37.5, 37.5, "Crafting", crafting));
                for (int i = 0; i < 6; i++) { operations.Add(Object("tree_" + i, "editor_tree", 13 + i * 7, 20)); }
                operations.Add(Region("town_safe", "editor_safe", 1, 1, 63, 63));
                operations.Add(Region("market", "editor_named", 23, 23, 41, 41));
            }
            return MapEditService.Patch(map, operations.ToArray());
        }

        static JObject Rect(string terrain, int x, int y, int width, int height)
        {
            return new JObject { ["type"] = "FillRect", ["terrainId"] = "terrain:" + terrain, ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height };
        }
        static JObject Object(string id, string definition, double x, double y, string kind = null, string content = null)
        {
            return new JObject { ["type"] = "UpsertObject", ["record"] = new JObject
            {
                ["objectId"] = id, ["definitionId"] = "map_object:" + definition, ["position"] = new JObject { ["x"] = x, ["y"] = y },
                ["rotation"] = 0, ["appearanceVariant"] = null, ["interaction"] = kind == null ? null : new JObject { ["kind"] = kind, ["contentId"] = content }
            } };
        }
        static JObject Region(string id, string definition, int x, int y, int endX, int endY, string spawn = null)
        {
            return new JObject { ["type"] = "UpsertRegion", ["record"] = new JObject
            {
                ["regionId"] = id, ["definitionId"] = "map_region:" + definition, ["priority"] = spawn == null ? 2 : 1, ["shape"] = "Rectangle",
                ["points"] = new JArray(new JObject { ["x"] = x, ["y"] = y }, new JObject { ["x"] = endX, ["y"] = endY }), ["spawnDefinitionId"] = spawn
            } };
        }
        static void DefineObject(string id, MapObjectCapabilities capabilities, MapInteractionKind kind, Vector2 footprint)
        {
            Create<MapObjectDefinition>(id, serialized =>
            {
                serialized.FindProperty("_capabilities").intValue = (int)capabilities;
                serialized.FindProperty("_interactionKind").enumValueIndex = (int)kind;
                serialized.FindProperty("_footprint").vector2Value = footprint;
            });
        }
        static void DefineRegion(string id, MapRegionKind kind) { Create<MapRegionDefinition>(id, serialized => serialized.FindProperty("_kind").enumValueIndex = (int)kind); }
        static void Create<T>(string id, Action<SerializedObject> configure) where T : ScriptableObject
        {
            string path = Root + "/" + id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) { return; }
            if (File.Exists(path)) { throw new InvalidOperationException("路径已被其他资产占用：" + path); }
            var asset = ScriptableObject.CreateInstance<T>();
            var serialized = new SerializedObject(asset); serialized.FindProperty("_id").stringValue = id;
            configure(serialized); serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(asset, path);
        }
    }
}
