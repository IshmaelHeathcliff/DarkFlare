using System;
using System.Collections.Generic;
using System.Reflection;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
        public void ContactConfiguration_PreservesCombatRangesAndIntervals()
        {
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
        public void PhysicsLayersAndPrefabs_MatchContract()
        {
            CollectionAssert.IsEmpty(GameplayPhysicsConfigurationValidator.Validate());
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

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"缺少字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
