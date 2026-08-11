using System;
using System.Collections.Generic;
using System.Reflection;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DarkFlare.Tests
{
    public class Alpha013CollisionSafetyTests
    {
        static readonly string[] ActorPrefabPaths =
        {
            "Assets/Prefabs/Combat/Player.prefab",
            "Assets/Prefabs/Combat/Monster_Basic.prefab",
            "Assets/Prefabs/Combat/Monster_Swift.prefab",
            "Assets/Prefabs/Combat/Monster_Heavy.prefab",
        };

        static readonly string[] MonsterAssetPaths =
        {
            "Assets/Data/Preset/Monsters/基础怪物.asset",
            "Assets/Data/Preset/Monsters/裂爪猎犬.asset",
            "Assets/Data/Preset/Monsters/铁壳尸傀.asset",
        };

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
        public void ActorPrefabs_KeepDynamicSolidMovementColliders()
        {
            for (int i = 0; i < ActorPrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPrefabPaths[i]);

                Assert.IsNotNull(prefab, ActorPrefabPaths[i]);
                Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
                CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
                Assert.IsNotNull(body, $"{ActorPrefabPaths[i]} 缺少 Rigidbody2D");
                Assert.IsNotNull(collider, $"{ActorPrefabPaths[i]} 缺少 CircleCollider2D");
                Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
                Assert.IsFalse(collider.isTrigger);
                Assert.AreEqual(0.4f, collider.radius, 0.001f);
            }
        }

        [Test]
        public void ContactDamage_RemainsDistanceDrivenAndIndependentlyTimed()
        {
            Assert.IsNotNull(GetInstanceMethod("TryDealContactDamage"));
            Assert.IsNull(GetInstanceMethod("OnCollisionEnter2D"));
            Assert.IsNull(GetInstanceMethod("OnCollisionStay2D"));
            Assert.IsNull(GetInstanceMethod("OnTriggerEnter2D"));
            Assert.IsNull(GetInstanceMethod("OnTriggerStay2D"));
            FieldInfo cooldown = typeof(MonsterController).GetField(
                "_lastContactDamageTime",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cooldown);
            Assert.AreEqual(typeof(float), cooldown.FieldType);
            float[] expectedIntervals = { 0.75f, 0.55f, 1f };

            for (int i = 0; i < MonsterAssetPaths.Length; i++)
            {
                MonsterDefinition definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(MonsterAssetPaths[i]);
                Assert.IsNotNull(definition, MonsterAssetPaths[i]);
                Assert.AreEqual(expectedIntervals[i], definition.ContactDamageInterval, 0.001f);
                Assert.AreEqual(0.75f, definition.ContactDamageRadius, 0.001f);
                Assert.AreEqual(0.6f, definition.ContactStopDistance, 0.001f);
                Assert.AreEqual(0.8f, definition.SeparationRadius, 0.001f);
                Assert.AreEqual(0.65f, definition.SeparationWeight, 0.001f);
            }
        }

        [Test]
        public void Steering_StopsInsideContactRangeAndKeepsSpeedBounded()
        {
            Assert.AreEqual(
                Vector2.zero,
                MonsterSteeringCalculator.GetPursuitDirection(new Vector2(0.6f, 0f), 0.6f));
            Assert.AreEqual(
                Vector2.right,
                MonsterSteeringCalculator.GetPursuitDirection(new Vector2(0.61f, 0f), 0.6f));

            Vector2 separation = MonsterSteeringCalculator.GetSeparationContribution(
                Vector2.zero,
                new Vector2(0.4f, 0f),
                0.8f,
                123);
            Assert.AreEqual(new Vector2(-0.5f, 0f), separation);
            Vector2 combined = MonsterSteeringCalculator.Combine(Vector2.right, separation, 0.65f);
            Assert.LessOrEqual(combined.magnitude, 1f);
        }

        [Test]
        public void Steering_UsesStableSeedDirectionForExactOverlap()
        {
            Vector2 first = MonsterSteeringCalculator.GetSeparationContribution(
                Vector2.zero,
                Vector2.zero,
                0.8f,
                24681357);
            Vector2 replay = MonsterSteeringCalculator.GetSeparationContribution(
                Vector2.zero,
                Vector2.zero,
                0.8f,
                24681357);
            Vector2 other = MonsterSteeringCalculator.GetSeparationContribution(
                Vector2.zero,
                Vector2.zero,
                0.8f,
                24681358);

            Assert.AreEqual(first, replay);
            Assert.AreEqual(1f, first.magnitude, 0.001f);
            Assert.AreNotEqual(first, other);
        }

        [Test]
        public void PhysicsLayersPrefabsAndMainWorldBounds_MatchContract()
        {
            CollectionAssert.IsEmpty(GameplayPhysicsConfigurationValidator.Validate());
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
                GameObject worldBounds = GameObject.Find("WorldBounds");
                Assert.IsNotNull(worldBounds);
                Assert.AreEqual(GameplayPhysicsLayers.WorldObstacle, LayerMask.LayerToName(worldBounds.layer));
                EdgeCollider2D collider = worldBounds.GetComponent<EdgeCollider2D>();
                Assert.IsNotNull(collider);
                Assert.IsFalse(collider.isTrigger);
                Assert.AreEqual(5, collider.pointCount);
                Assert.AreEqual(new Vector2(-16f, -16f), collider.points[0]);
                Assert.AreEqual(new Vector2(-16f, -16f), collider.points[4]);
                Assert.IsTrue(scene.IsValid());
            }
            finally
            {
                if (setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        [Test]
        public void MonsterMovementValidation_RejectsInvalidRanges()
        {
            MonsterDefinition definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            _objects.Add(definition);
            SetField(definition, "_contactDamageRadius", 0.75f);
            SetField(definition, "_contactStopDistance", 0.8f);
            SetField(definition, "_separationRadius", 0f);
            SetField(definition, "_separationWeight", 2.1f);
            List<string> issues = RandomizationConfigurationValidator.Validate(definition);

            Assert.IsTrue(issues.Exists(issue => issue.Contains("停止距离")));
            Assert.IsTrue(issues.Exists(issue => issue.Contains("软分离半径")));
            Assert.IsTrue(issues.Exists(issue => issue.Contains("软分离权重")));
        }

        static MethodInfo GetInstanceMethod(string name)
        {
            return typeof(MonsterController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"缺少字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
