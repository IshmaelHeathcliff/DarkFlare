using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public class VisualExperiencePhase05Tests
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Combat/Player.prefab";
        const string LootPrefabPath = "Assets/Prefabs/Loot/LootPickup.prefab";

        readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void PlayerPrefab_UsesRigidbodyInterpolation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            Assert.IsNotNull(prefab, PlayerPrefabPath);
            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "Player Prefab 缺少 Rigidbody2D");
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation);
        }

        [Test]
        public void CameraFollowTarget_ClampsTheWholeOrthographicViewInsideBounds()
        {
            GameObject boundsObject = CreateGameObject("Bounds");
            BoxCollider2D bounds = boundsObject.AddComponent<BoxCollider2D>();
            bounds.size = new Vector2(20f, 12f);
            GameObject cameraObject = CreateGameObject("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.aspect = 16f / 9f;
            CameraFollowTarget follow = cameraObject.AddComponent<CameraFollowTarget>();
            follow.SetWorldBounds(bounds);

            Vector3 clamped = follow.ClampToWorldBounds(new Vector3(100f, -100f, -10f));

            Assert.LessOrEqual(clamped.x + camera.orthographicSize * camera.aspect, bounds.bounds.max.x);
            Assert.GreaterOrEqual(clamped.y - camera.orthographicSize, bounds.bounds.min.y);
            Assert.AreEqual(-10f, clamped.z);
        }

        [Test]
        public void MonsterSpawner_GeneratesEveryPositionInsideConfiguredBounds()
        {
            GameObject spawnerObject = CreateGameObject("Spawner", false);
            MonsterSpawner spawner = spawnerObject.AddComponent<MonsterSpawner>();
            MonsterSpawnDefinition definition = ScriptableObject.CreateInstance<MonsterSpawnDefinition>();
            _objects.Add(definition);
            SetField(definition, "_spawnRadius", 8f);
            SetField(spawner, "_spawnDefinition", definition);
            GameObject boundsObject = CreateGameObject("Bounds");
            BoxCollider2D bounds = boundsObject.AddComponent<BoxCollider2D>();
            bounds.size = new Vector2(8f, 6f);
            spawner.SetWorldBounds(bounds);
            MethodInfo getSpawnPosition = typeof(MonsterSpawner).GetMethod(
                "GetSpawnPosition",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(System.Random) },
                null);

            Assert.IsNotNull(getSpawnPosition);

            for (int i = 0; i < 100; i++)
            {
                Vector3 position = (Vector3)getSpawnPosition.Invoke(
                    spawner,
                    new object[] { new System.Random(i) });
                Assert.That(position.x, Is.InRange(bounds.bounds.min.x + 0.5f, bounds.bounds.max.x - 0.5f));
                Assert.That(position.y, Is.InRange(bounds.bounds.min.y + 0.5f, bounds.bounds.max.y - 0.5f));
            }
        }

        [Test]
        public void LootPrefab_BindsNameRarityAndIndependentVisualHierarchy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootPrefabPath);

            Assert.IsNotNull(prefab, LootPrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            _objects.Add(instance);
            instance.SetActive(false);
            LootPickupController pickup = instance.GetComponent<LootPickupController>();
            LootPickupVisual visual = instance.GetComponent<LootPickupVisual>();
            CircleCollider2D collider = instance.GetComponent<CircleCollider2D>();

            Assert.IsNotNull(pickup);
            Assert.IsNotNull(visual);
            Assert.IsNotNull(collider);
            Assert.IsNotNull(instance.transform.Find("Shadow"));
            Assert.IsNotNull(instance.transform.Find("Visual/Halo"));
            Assert.IsNotNull(instance.transform.Find("Visual/Icon"));
            Assert.IsNotNull(instance.transform.Find("Visual/Label"));
            Assert.AreEqual(visual, pickup.Visual);
            Assert.AreEqual(Vector3.one, instance.transform.localScale);

            ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
            _objects.Add(definition);
            SetField(definition, "_displayName", "测试大剑");
            ItemInstance item = definition.CreateInstance("phase05", 1, 1, ItemRarity.Rare);
            pickup.Init(item);
            TextMeshPro label = instance.GetComponentInChildren<TextMeshPro>(true);
            SpriteRenderer halo = instance.transform.Find("Visual/Halo").GetComponent<SpriteRenderer>();

            Assert.AreSame(item, pickup.Item);
            Assert.AreSame(item, visual.Item);
            Assert.AreEqual("测试大剑 · 稀有", label.text);
            Assert.AreEqual(0.55f, halo.color.a, 0.001f);
            Assert.AreNotEqual(Color.white, halo.color);
        }

        GameObject CreateGameObject(string name, bool active = true)
        {
            GameObject value = new GameObject(name);
            value.SetActive(active);
            _objects.Add(value);
            return value;
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
