using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace DarkFlare.Editor
{
    // 幂等补齐正式资产；既有 .dfmap 不覆盖，后续布局修改走地图工作台。
    public static class WorldMapSetup
    {
        public const string Root = "Assets/Data/Maps/World";
        public const string ConfigurationPath = Root + "/world_maps.asset";

        [MenuItem("DarkFlare/地图/安装城镇与野外")]
        public static void Ensure()
        {
            if (EditorApplication.isPlaying) { throw new InvalidOperationException("请退出 Play Mode"); }
            MapObjectPresentationSetup.Ensure();
            if (!AssetDatabase.IsValidFolder(Root)) { AssetDatabase.CreateFolder("Assets/Data/Maps", "World"); }
            MapObjectDefinition portal = AssetDatabase.LoadAssetAtPath<MapObjectDefinition>(Root + "/portal.asset");
            if (portal == null)
            {
                portal = ScriptableObject.CreateInstance<MapObjectDefinition>();
                var edit = new SerializedObject(portal);
                edit.FindProperty("_id").stringValue = "portal";
                edit.FindProperty("_capabilities").intValue = (int)(MapObjectCapabilities.Decoration | MapObjectCapabilities.Interaction);
                edit.FindProperty("_interactionKind").enumValueIndex = (int)MapInteractionKind.Portal;
                edit.FindProperty("_footprint").vector2Value = new Vector2(0.5f, 0.5f);
                edit.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(portal, Root + "/portal.asset");
            }
            EnsurePortalPresentation(portal);
            MapEditorDefinitionIndex.UpdateDependency();
            EnsureMap(false); EnsureMap(true);
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>("Assets/Data/Preset/Content/正式内容目录.asset");
            var entries = new HashSet<ScriptableObject>(catalog.Entries);
            foreach (ScriptableObject definition in MapEditorDefinitionIndex.LoadDefinitions())
            {
                string id = ((IContentDefinition)definition).Id;
                if (definition is TerrainDefinition && new[] { "dirt", "grass", "water", "road" }.Contains(id)
                    || definition is TerrainSetDefinition && id == "basic"
                    || definition is MapObjectDefinition && (id.StartsWith("editor_", StringComparison.Ordinal) || id == "portal")
                    || definition is MapRegionDefinition && id.StartsWith("editor_", StringComparison.Ordinal))
                { entries.Add(definition); }
            }
            entries.Add(AssetDatabase.LoadAssetAtPath<MapDefinition>(Root + "/town.dfmap"));
            entries.Add(AssetDatabase.LoadAssetAtPath<MapDefinition>(Root + "/wilderness.dfmap"));
            var catalogEdit = new SerializedObject(catalog);
            catalogEdit.FindProperty("_contentVersion").intValue = 6;
            SerializedProperty records = catalogEdit.FindProperty("_entries"); records.arraySize = entries.Count;
            int index = 0;
            foreach (ScriptableObject entry in entries.OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal))
            { records.GetArrayElementAtIndex(index++).objectReferenceValue = entry; }
            catalogEdit.ApplyModifiedPropertiesWithoutUndo();
            WorldMapConfiguration configuration = AssetDatabase.LoadAssetAtPath<WorldMapConfiguration>(ConfigurationPath);
            if (configuration == null)
            {
                configuration = ScriptableObject.CreateInstance<WorldMapConfiguration>();
                var edit = new SerializedObject(configuration);
                edit.FindProperty("_startingMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MapDefinition>(Root + "/town.dfmap");
                edit.FindProperty("_objects").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MapObjectPresentationCatalog>(MapObjectPresentationSetup.CatalogPath);
                TerrainPresentationDefinition presentation = AssetDatabase.FindAssets("t:TerrainPresentationDefinition").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<TerrainPresentationDefinition>).Single(value => value.TerrainSet.Id == "basic");
                edit.FindProperty("_terrainPresentation").FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(presentation));
                edit.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(configuration, ConfigurationPath);
            }
            Localize();
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/docs/maps/map-document.schema.json", MapDocumentJson.CreateSchema(), new System.Text.UTF8Encoding(false));
            InstallScene(configuration);
        }

        static void EnsureMap(bool wild)
        {
            string path = Root + (wild ? "/wilderness.dfmap" : "/town.dfmap");
            if (File.Exists(path)) { return; }
            int size = wild ? 128 : 64;
            MapDocument map = MapEditorSamples.Blank(wild ? "wilderness" : "town", size);
            map.Origin = new MapPoint { X = -size / 2, Y = -size / 2 };
            map.Revision = 1;
            int[] cells = Enumerable.Repeat(1, size * size).ToArray();
            void Rect(int x, int y, int width, int height, int terrain)
            {
                for (int row = y; row < y + height; row++)
                for (int col = x; col < x + width; col++) { cells[col + row * size] = terrain; }
            }
            Rect(size / 2 - 3, 1, 6, size - 2, 3);
            Rect(1, size / 2 - 3, size - 2, 6, 3);
            Rect(6, size - 24, 14, 14, 2);
            Rect(size - 27, 8, 18, 16, 0);
            // 旧 Main 的全部有效范围位于无阻挡中央区域；道路和草地均可通行。
            if (wild) { Rect(44, 44, 40, 40, 0); Rect(61, 44, 6, 40, 3); }
            var chunks = new List<MapChunk>();
            for (int y = 0; y < size / 32; y++)
            for (int x = 0; x < size / 32; x++)
            {
                var chunk = new MapChunk { X = x, Y = y, Cells = new int[1024] };
                for (int i = 0; i < 1024; i++) { chunk.Cells[i] = cells[x * 32 + i % 32 + (y * 32 + i / 32) * size]; }
                chunks.Add(chunk);
            }
            map.Chunks = chunks.ToArray();
            double center = size / 2.0;
            map.Entries = new[]
            {
                new MapEntryRecord { EntryId = wild ? "arrival" : "new_game", Kind = wild ? MapEntryKind.Arrival : MapEntryKind.NewGame, Position = new MapPoint { X = center, Y = center } },
                new MapEntryRecord { EntryId = "return", Position = new MapPoint { X = center, Y = center - 2 } },
                new MapEntryRecord { EntryId = "respawn", Kind = MapEntryKind.Respawn, Position = new MapPoint { X = center + 1, Y = center } }
            };
            var objects = new List<MapObjectRecord>
            {
                Record("exit", "portal", center, center + 5, new MapInteractionBinding { Kind = MapInteractionKind.Portal,
                    TargetMapId = wild ? "map:town" : "map:wilderness", TargetEntryId = wild ? "return" : "arrival" })
            };
            if (!wild)
            {
                TraderDefinition trader = First<TraderDefinition>(); CraftingDefinition craft = First<CraftingDefinition>();
                objects.Add(Record("merchant", "editor_trader", center - 4, center + 1, new MapInteractionBinding { Kind = MapInteractionKind.Trader, ContentId = "trader:" + trader.Id }));
                objects.Add(Record("crafting", "editor_crafting", center + 4, center + 1, new MapInteractionBinding { Kind = MapInteractionKind.Crafting, ContentId = "crafting:" + craft.Id }));
                objects.Add(Record("tent", "editor_tent", center - 6, center + 6));
                objects.Add(Record("stock_rock", "editor_rock", center + 7, center + 6));
            }
            for (int i = 0; i < 12; i++)
            {
                objects.Add(Record("tree_" + i, "editor_tree", 24 + i % 3 * 3, 8 + i / 3 * 5));
            }
            if (wild)
            {
                for (int i = 0; i < 5; i++) { objects.Add(Record("barrier_" + i, "editor_long_barrier", 90, 44 + i * 8)); }
            }
            map.Objects = objects.ToArray();
            string spawn = "spawn:" + First<MonsterSpawnDefinition>().Id;
            map.Regions = wild ? new[] { Region("wild_spawn", "editor_spawn", 2, 2, size - 2, size - 2, spawn),
                Region("entry_safe", "editor_safe", center - 3, center - 3, center + 3, center + 7) }
                : new[] { Region("town_safe", "editor_safe", 0, 0, size, size) };
            MapDocumentPage.SaveSource(path, MapDocumentJson.Write(map));
        }

        static T First<T>() where T : ScriptableObject, IContentDefinition
        {
            return AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>("Assets/Data/Preset/Content/正式内容目录.asset").Entries.OfType<T>().First();
        }
        static MapObjectRecord Record(string id, string definition, double x, double y, MapInteractionBinding interaction = null)
        {
            return new MapObjectRecord { ObjectId = id, DefinitionId = "map_object:" + definition, Position = new MapPoint { X = x, Y = y }, Interaction = interaction };
        }
        static MapRegionRecord Region(string id, string definition, double x, double y, double maxX, double maxY, string spawn = null)
        {
            return new MapRegionRecord { RegionId = id, DefinitionId = "map_region:" + definition, Priority = spawn == null ? 2 : 1,
                Points = new[] { new MapPoint { X = x, Y = y }, new MapPoint { X = maxX, Y = maxY } }, SpawnDefinitionId = spawn };
        }

        static void EnsurePortalPresentation(MapObjectDefinition portal)
        {
            const string prefabPath = "Assets/Prefabs/World/MapObjects/portal.prefab";
            if (!File.Exists(prefabPath))
            {
                GameObject instance = PrefabUtility.LoadPrefabContents("Assets/Prefabs/World/MapObjects/tree.prefab");
                try
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Environment/WorldProps/world_prop_tattered_banner.png");
                    foreach (SpriteRenderer renderer in instance.GetComponentsInChildren<SpriteRenderer>()) { renderer.sprite = sprite; }
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(instance); }
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(prefabPath), settings.FindGroup("DarkFlare-Map-Objects")).address = prefabPath;
            EditorUtility.SetDirty(settings);
            var catalog = AssetDatabase.LoadAssetAtPath<MapObjectPresentationCatalog>(MapObjectPresentationSetup.CatalogPath);
            var edit = new SerializedObject(catalog); SerializedProperty entries = edit.FindProperty("_entries");
            for (int i = 0; i < entries.arraySize; i++)
            { if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("_definition").objectReferenceValue == portal) { return; } }
            int index = entries.arraySize++; SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_definition").objectReferenceValue = portal;
            SerializedProperty appearances = entry.FindPropertyRelative("_appearances"); appearances.arraySize = 1;
            appearances.GetArrayElementAtIndex(0).FindPropertyRelative("_variantId").stringValue = "";
            appearances.GetArrayElementAtIndex(0).FindPropertyRelative("_prefab").FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(prefabPath);
            edit.ApplyModifiedPropertiesWithoutUndo();
        }

        static void InstallScene(WorldMapConfiguration configuration)
        {
            const string path = "Assets/Scenes/Main.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                CombatPrototypeBootstrap bootstrap = roots.SelectMany(root => root.GetComponentsInChildren<CombatPrototypeBootstrap>(true)).Single();
                var edit = new SerializedObject(bootstrap); edit.FindProperty("_worldMaps").objectReferenceValue = configuration; edit.ApplyModifiedPropertiesWithoutUndo();
                foreach (GameObject root in roots)
                {
                    if (root.name is "WorldVisuals" or "GroundGrid" or "WorldBounds" or "MonsterSpawner") { root.SetActive(false); }
                    foreach (WorldInteractionTarget interaction in root.GetComponentsInChildren<WorldInteractionTarget>(true)) { interaction.gameObject.SetActive(false); }
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) { EditorSceneManager.CloseScene(scene, true); } }
        }

        static void Localize()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection("ui");
            foreach (var text in new[] { ("world.map_exit", "地图出口", "Map exit"), ("world.trader", "商人", "Trader"), ("world.crafting", "打造台", "Crafting station") })
            {
                var shared = collection.SharedData.GetEntry(text.Item1) ?? collection.SharedData.AddKey(text.Item1);
                foreach (string locale in new[] { "zh-Hans", "en" })
                {
                    var table = (StringTable)collection.GetTable(new LocaleIdentifier(locale));
                    string value = locale == "zh-Hans" ? text.Item2 : locale == "en" ? text.Item3 : "[!! " + text.Item3 + " !!]";
                    var entry = table.GetEntry(shared.Id) ?? table.AddEntry(shared.Id, value); entry.Value = value; EditorUtility.SetDirty(table);
                }
            }
            EditorUtility.SetDirty(collection.SharedData);
        }
    }
}
