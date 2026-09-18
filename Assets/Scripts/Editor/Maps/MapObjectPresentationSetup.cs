using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare.Editor
{
    public static class MapObjectPresentationSetup
    {
        public const string Root = "Assets/Data/Maps/Presentation";
        public const string CatalogPath = Root + "/map_object_presentations.asset";
        public const string SamplePath = Root + "/world_sorting.dfmap";
        const string PrefabRoot = "Assets/Prefabs/World/MapObjects";

        [MenuItem("DarkFlare/地图/补齐对象表现样例")]
        public static void Ensure()
        {
            MapEditorSamples.Ensure();
            Folder(Root); Folder(PrefabRoot);
            Define("editor_rock", new Vector2(1.5f, 1)); Define("editor_tent", new Vector2(2, 1.5f)); Define("editor_long_barrier", new Vector2(6.5f, 1.5f));
            CreateProp("tree", "dead_tree", false); CreateProp("rock", "rock_large", false); CreateProp("tent", "tent", false);
            CreateProp("long_barrier", "rock_large", true);
            CreateInteraction("trader", "Assets/Prefabs/World/Merchant.prefab");
            CreateInteraction("crafting", "Assets/Prefabs/World/CraftingStation.prefab");
            if (!File.Exists(CatalogPath))
            {
                var catalog = ScriptableObject.CreateInstance<MapObjectPresentationCatalog>();
                var serialized = new SerializedObject(catalog); SerializedProperty entries = serialized.FindProperty("_entries");
                string[] ids = { "tree", "rock", "tent", "long_barrier", "trader", "crafting" }; entries.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    string definition = ids[i] == "tree" || ids[i] == "trader" || ids[i] == "crafting" ? MapEditorSamples.Root : Root;
                    entry.FindPropertyRelative("_definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MapObjectDefinition>(definition + "/editor_" + ids[i] + ".asset");
                    SerializedProperty appearances = entry.FindPropertyRelative("_appearances"); appearances.arraySize = 1;
                    appearances.GetArrayElementAtIndex(0).FindPropertyRelative("_variantId").stringValue = "";
                    appearances.GetArrayElementAtIndex(0).FindPropertyRelative("_prefab").FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(PrefabRoot + "/" + ids[i] + ".prefab");
                }
                serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            MapEditorDefinitionIndex.UpdateDependency();
            if (!File.Exists(SamplePath))
            {
                MapDocument map = MapEditorSamples.Blank("world_sorting", 32);
                map.Objects = new[] { Record("tree", "tree", 9, 10, 0), Record("rock", "rock", 13, 10, 0), Record("tent", "tent", 17, 10, 0),
                    Record("wall_0", "long_barrier", 10, 17, 0), Record("wall_90", "long_barrier", 20, 19, 90),
                    Record("wall_180", "long_barrier", 10, 24, 180), Record("wall_270", "long_barrier", 26, 19, 270) };
                MapDocumentPage.SaveSource(SamplePath, MapDocumentJson.Write(map));
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("DarkFlare-Map-Objects") ?? settings.CreateGroup("DarkFlare-Map-Objects", false, false, false, null,
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema), typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }).Select(AssetDatabase.GUIDToAssetPath).Append(CatalogPath))
            { settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path; }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }

        [MenuItem("DarkFlare/地图/打开世界排序样例")]
        public static void OpenSample() { Ensure(); MapEditorWindow.OpenSource(SamplePath); }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Define(string id, Vector2 footprint)
        {
            string path = Root + "/" + id + ".asset"; if (File.Exists(path)) { return; }
            var definition = ScriptableObject.CreateInstance<MapObjectDefinition>(); var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id; serialized.FindProperty("_footprint").vector2Value = footprint;
            serialized.FindProperty("_capabilities").intValue = (int)(MapObjectCapabilities.Obstacle | MapObjectCapabilities.Decoration);
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(definition, path);
        }

        static GameObject Part(GameObject root, string id, Vector3 position, WorldSortCategory category)
        {
            var part = new GameObject(id); part.transform.SetParent(root.transform, false); part.transform.localPosition = position;
            var anchor = new GameObject("SortAnchor"); anchor.transform.SetParent(part.transform, false);
            var participant = part.AddComponent<WorldSortParticipant>(); participant.ConfigureExplicit(category, "prefab:" + id, anchor.transform);
            var component = part.AddComponent<MapObjectVisualPart>(); var serialized = new SerializedObject(component);
            serialized.FindProperty("_partId").stringValue = id; serialized.ApplyModifiedPropertiesWithoutUndo(); return part;
        }

        static void CreateProp(string id, string spriteId, bool segmented)
        {
            string path = PrefabRoot + "/" + id + ".prefab"; if (File.Exists(path)) { return; }
            var root = new GameObject(id); root.SetActive(false);
            try
            {
                Material material = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/Merchant.prefab").GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
                int count = segmented ? 4 : 1;
                for (int i = 0; i < count; i++)
                {
                    GameObject part = Part(root, segmented ? "stone_" + i : "body", segmented ? new Vector3((i - 1.5f) * 1.5f, 0, 0) : Vector3.zero, WorldSortCategory.StaticProp);
                    var visual = new GameObject("Sprite"); visual.transform.SetParent(part.transform, false); visual.transform.localPosition = new Vector3(0, 0.7f, 0);
                    var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Environment/WorldProps/world_prop_" + (segmented && i % 2 != 0 ? "rock_small" : spriteId) + ".png");
                    renderer.sharedMaterial = material; renderer.sortingLayerName = "WorldObject";
                }
                MapObjectVisualPart.ValidatePrefab(root); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void CreateInteraction(string id, string source)
        {
            string path = PrefabRoot + "/" + id + ".prefab"; if (File.Exists(path)) { return; }
            var root = new GameObject(id); root.SetActive(false);
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                GameObject part = Part(root, "body", Vector3.zero, WorldSortCategory.Interactable);
                WorldSortParticipant original = prefab.GetComponent<WorldSortParticipant>();
                part.transform.Find("SortAnchor").localPosition = original.SortAnchor.localPosition;
                foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var visual = new GameObject(renderer.name); visual.transform.SetParent(part.transform, false);
                    visual.transform.localPosition = prefab.transform.InverseTransformPoint(renderer.transform.position);
                    visual.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * renderer.transform.rotation;
                    visual.transform.localScale = renderer.transform.lossyScale;
                    var copy = visual.AddComponent<SpriteRenderer>(); EditorUtility.CopySerialized(renderer, copy); copy.sortingLayerName = "WorldObject";
                }
                MapObjectVisualPart.ValidatePrefab(root); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static MapObjectRecord Record(string id, string definition, double x, double y, int rotation)
        { return new MapObjectRecord { ObjectId = id, DefinitionId = "map_object:editor_" + definition, Position = new MapPoint { X = x, Y = y }, Rotation = rotation }; }

        public static List<ContentValidationIssue> Scan()
        {
            var issues = new List<ContentValidationIssue>();
            foreach (string guid in AssetDatabase.FindAssets("t:MapObjectPresentationCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<MapObjectPresentationCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                try
                {
                    catalog.Validate();
                    foreach (MapObjectPresentationEntry entry in catalog.Entries)
                    {
                        foreach (MapObjectAppearance appearance in entry.Appearances)
                        {
                            if (AddressableAssetSettingsDefaultObject.Settings?.FindAssetEntry(appearance.Prefab.AssetGUID) == null)
                            { throw new InvalidOperationException(entry.Definition.Id + "：表现 Prefab 未登记 Addressables"); }
                            MapObjectVisualPart.ValidatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(appearance.Prefab.AssetGUID)));
                        }
                    }
                }
                catch (Exception error) { issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, catalog, error.Message)); }
            }
            return issues;
        }
    }
}
