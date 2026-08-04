using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare.Editor
{
    public static class VisualExperiencePhase05Installer
    {
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string PlayerPrefabPath = "Assets/Prefabs/Combat/Player.prefab";
        const string LootPrefabPath = "Assets/Prefabs/Loot/LootPickup.prefab";
        const string GroundSpritePath = "Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png";
        const string GreatswordSpritePath = "Assets/Art/Sprites/Items/Equipment/weapon_greatsword.png";
        const string LootFrameSpritePath = "Assets/Art/Sprites/UI/ui_slot_focus.png";
        const string ChineseFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/QiushuiShotai SDF.asset";
        const int GroundRadius = 2;
        const float GroundTileSize = 8f;
        const float WorldHalfExtent = 16f;

        [MenuItem("Tools/DarkFlare/视觉切片/安装阶段 0.5 体验修正")]
        public static void InstallFromMenu()
        {
            ConfigurePlayerInterpolation();
            ConfigureLootPrefab();
            ConfigureMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[VisualExperiencePhase05] Rigidbody 插值、地图边界和掉落表现已接入");
        }

        static void ConfigurePlayerInterpolation()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                Rigidbody2D body = root.GetComponent<Rigidbody2D>();

                if (body == null)
                {
                    throw new InvalidOperationException("Player Prefab 缺少 Rigidbody2D");
                }

                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureLootPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LootPrefabPath);

            try
            {
                root.transform.localScale = Vector3.one;
                Sprite sprite = LoadAsset<Sprite>(GreatswordSpritePath);
                SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();

                if (rootRenderer != null)
                {
                    UnityEngine.Object.DestroyImmediate(rootRenderer, true);
                }

                Transform shadow = GetOrCreateChild(root.transform, "Shadow");
                ConfigureTransform(
                    shadow,
                    new Vector3(0f, -0.12f, 0f),
                    Quaternion.identity,
                    new Vector3(1.05f, 0.35f, 1f));
                SpriteRenderer shadowRenderer = GetOrAddComponent<SpriteRenderer>(shadow.gameObject);
                shadowRenderer.sprite = sprite;
                shadowRenderer.color = new Color(0f, 0f, 0f, 0.5f);
                shadowRenderer.sortingOrder = 4;

                Transform visual = GetOrCreateChild(root.transform, "Visual");
                ConfigureTransform(visual, new Vector3(0f, 0.1f, 0f), Quaternion.identity, Vector3.one);
                Transform halo = GetOrCreateChild(visual, "Halo");
                ConfigureTransform(halo, Vector3.zero, Quaternion.identity, new Vector3(1.15f, 1.15f, 1f));
                SpriteRenderer haloRenderer = GetOrAddComponent<SpriteRenderer>(halo.gameObject);
                haloRenderer.sprite = LoadAsset<Sprite>(LootFrameSpritePath);
                haloRenderer.color = new Color(0.82f, 0.88f, 0.94f, 0.55f);
                haloRenderer.sortingOrder = 5;

                Transform icon = GetOrCreateChild(visual, "Icon");
                ConfigureTransform(icon, Vector3.zero, Quaternion.identity, new Vector3(0.85f, 0.85f, 1f));
                SpriteRenderer iconRenderer = GetOrAddComponent<SpriteRenderer>(icon.gameObject);
                iconRenderer.sprite = sprite;
                iconRenderer.color = Color.white;
                iconRenderer.sortingOrder = 7;

                TextMeshPro label = ConfigureLabel(visual);
                LootPickupVisual visualController = GetOrAddComponent<LootPickupVisual>(root);
                SerializedObject serializedVisual = new SerializedObject(visualController);
                serializedVisual.FindProperty("_visualRoot").objectReferenceValue = visual;
                serializedVisual.FindProperty("_shadowRenderer").objectReferenceValue = shadowRenderer;
                serializedVisual.FindProperty("_haloRenderer").objectReferenceValue = haloRenderer;
                serializedVisual.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
                serializedVisual.FindProperty("_label").objectReferenceValue = label;
                serializedVisual.ApplyModifiedPropertiesWithoutUndo();

                LootPickupController pickup = root.GetComponent<LootPickupController>();

                if (pickup == null)
                {
                    throw new InvalidOperationException("LootPickup Prefab 缺少 LootPickupController");
                }

                SerializedObject serializedPickup = new SerializedObject(pickup);
                serializedPickup.FindProperty("_visual").objectReferenceValue = visualController;
                serializedPickup.ApplyModifiedPropertiesWithoutUndo();
                CircleCollider2D collider = root.GetComponent<CircleCollider2D>();

                if (collider != null)
                {
                    collider.isTrigger = true;
                    collider.radius = 0.2f;
                }

                PrefabUtility.SaveAsPrefabAsset(root, LootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static TextMeshPro ConfigureLabel(Transform visual)
        {
            Transform existing = visual.Find("Label");

            if (existing != null && existing is not RectTransform)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
                existing = null;
            }

            RectTransform rectTransform;

            if (existing == null)
            {
                GameObject labelObject = new GameObject("Label", typeof(RectTransform));
                rectTransform = (RectTransform)labelObject.transform;
                rectTransform.SetParent(visual, false);
            }
            else
            {
                rectTransform = (RectTransform)existing;
            }

            rectTransform.localPosition = new Vector3(0f, -0.72f, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = new Vector3(0.22f, 0.22f, 1f);
            rectTransform.sizeDelta = new Vector2(5f, 1.2f);
            TextMeshPro label = GetOrAddComponent<TextMeshPro>(rectTransform.gameObject);
            label.font = LoadAsset<TMP_FontAsset>(ChineseFontPath);
            label.text = "大剑 · 普通";
            label.fontSize = 2.4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.renderer.sortingOrder = 8;
            return label;
        }

        static void ConfigureMainScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.IsValid() || scene.path != MainScenePath)
            {
                throw new InvalidOperationException("请先打开 Assets/Scenes/Main.unity 再安装阶段 0.5");
            }

            ConfigureGround(scene);
            EdgeCollider2D worldBounds = ConfigureWorldBounds(scene);
            Camera camera = Camera.main;

            if (camera == null)
            {
                throw new InvalidOperationException("Main 场景缺少 Main Camera");
            }

            CameraFollowTarget follow = camera.GetComponent<CameraFollowTarget>();

            if (follow == null)
            {
                throw new InvalidOperationException("Main Camera 缺少 CameraFollowTarget");
            }

            follow.SetWorldBounds(worldBounds);
            EditorUtility.SetDirty(follow);
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>(FindObjectsInactive.Include);

            if (spawner == null)
            {
                throw new InvalidOperationException("Main 场景缺少 MonsterSpawner");
            }

            spawner.SetWorldBounds(worldBounds);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ConfigureGround(Scene scene)
        {
            GameObject root = FindRoot(scene, "VisualSliceGround");

            if (root == null)
            {
                root = new GameObject("VisualSliceGround");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            ConfigureTransform(root.transform, Vector3.zero, Quaternion.identity, Vector3.one);
            Sprite groundSprite = LoadAsset<Sprite>(GroundSpritePath);
            HashSet<string> expectedNames = new HashSet<string>();

            for (int y = -GroundRadius; y <= GroundRadius; y++)
            {
                for (int x = -GroundRadius; x <= GroundRadius; x++)
                {
                    string childName = $"Ground_{x + GroundRadius}_{y + GroundRadius}";
                    expectedNames.Add(childName);
                    Transform child = GetOrCreateChild(root.transform, childName);
                    ConfigureTransform(
                        child,
                        new Vector3(x * GroundTileSize, y * GroundTileSize, 0f),
                        Quaternion.identity,
                        Vector3.one);
                    SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(child.gameObject);
                    renderer.sprite = groundSprite;
                    renderer.color = Color.white;
                    renderer.sortingOrder = -100;
                }
            }

            List<Transform> obsoleteChildren = new List<Transform>();

            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);

                if (!expectedNames.Contains(child.name))
                {
                    obsoleteChildren.Add(child);
                }
            }

            for (int i = 0; i < obsoleteChildren.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(obsoleteChildren[i].gameObject);
            }
        }

        static EdgeCollider2D ConfigureWorldBounds(Scene scene)
        {
            GameObject root = FindRoot(scene, "WorldBounds");

            if (root == null)
            {
                root = new GameObject("WorldBounds");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            ConfigureTransform(root.transform, Vector3.zero, Quaternion.identity, Vector3.one);
            EdgeCollider2D collider = GetOrAddComponent<EdgeCollider2D>(root);
            collider.edgeRadius = 0.05f;
            collider.points = new[]
            {
                new Vector2(-WorldHalfExtent, -WorldHalfExtent),
                new Vector2(-WorldHalfExtent, WorldHalfExtent),
                new Vector2(WorldHalfExtent, WorldHalfExtent),
                new Vector2(WorldHalfExtent, -WorldHalfExtent),
                new Vector2(-WorldHalfExtent, -WorldHalfExtent),
            };
            return collider;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
        }

        static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);

            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(name);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        static void ConfigureTransform(
            Transform transform,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
        }

        static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                throw new InvalidOperationException($"无法加载资源: {path}");
            }

            return asset;
        }
    }
}
