using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha015MonsterAffixPlayModeTests
    {
        readonly List<Object> _objects = new List<Object>();

        IArchitecture _architecture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameArchitecture.Interface.Deinit();
            _architecture = GameArchitecture.Interface;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.Destroy(_objects[i]);
                }
            }

            _objects.Clear();
            yield return null;
            _architecture?.Deinit();
            _architecture = null;
        }

        [UnityTest]
        public IEnumerator Controller_AppliesModifiersAndVisualTracksLifecycle()
        {
            MonsterAffixDefinition definition = CreateAffix("armored", "装甲", Color.yellow);
            ModifierInstance modifier = CreateModifier(StatIds.Armor, ModifierOperation.Flat, 35f);
            MonsterAffixInstance affix = new MonsterAffixInstance(definition, 17, new[] { modifier });
            MonsterInstanceData instance = CreateInstance(new[] { affix }, new[] { modifier });
            MonsterController controller = CreateMonsterController(out MonsterAffixVisual visual);
            MonsterDefinition monster = CreateMonsterDefinition();
            controller.transform.localScale = new Vector3(-0.75f, 0.75f, 1f);

            controller.Configure(monster, instance);
            yield return null;

            Assert.AreEqual(1, controller.Actor.Modifiers.Count);
            Assert.AreEqual(40f, controller.Actor.Stats.GetValue(StatIds.Armor), 0.001f);
            Assert.IsTrue(visual.IsVisible);
            StringAssert.Contains("装甲", visual.DisplayText);
            Assert.AreEqual(1f, visual.LabelWorldScale.x, 0.001f);
            Assert.AreEqual(1f, visual.LabelWorldScale.y, 0.001f);

            _architecture.SendEvent(new ActorDiedEvent { Actor = controller.Actor });
            yield return null;
            Assert.IsFalse(visual.IsVisible);

            controller.Actor.Revive(controller.transform.position);
            _architecture.SendEvent(new ActorRevivedEvent { Actor = controller.Actor });
            yield return null;
            Assert.IsTrue(visual.IsVisible);
        }

        [UnityTest]
        public IEnumerator Visual_HidesEmptyInstancesAndCapsPresentationAtTwoLines()
        {
            MonsterController controller = CreateMonsterController(out MonsterAffixVisual visual);
            MonsterDefinition monster = CreateMonsterDefinition();
            MonsterInstanceData empty = CreateInstance(
                new List<MonsterAffixInstance>(),
                new List<ModifierInstance>());

            controller.Configure(monster, empty);
            yield return null;
            Assert.IsFalse(visual.IsVisible);

            MonsterAffixInstance first = new MonsterAffixInstance(
                CreateAffix("first", "第一", Color.red),
                1,
                new List<ModifierInstance>());
            MonsterAffixInstance second = new MonsterAffixInstance(
                CreateAffix("second", "第二", Color.cyan),
                2,
                new List<ModifierInstance>());
            MonsterAffixInstance third = new MonsterAffixInstance(
                CreateAffix("third", "第三", Color.green),
                3,
                new List<ModifierInstance>());
            controller.Configure(
                monster,
                CreateInstance(new[] { first, second, third }, new List<ModifierInstance>()));
            yield return null;

            Assert.IsTrue(visual.IsVisible);
            StringAssert.Contains("第一", visual.DisplayText);
            StringAssert.Contains("第二", visual.DisplayText);
            StringAssert.DoesNotContain("第三", visual.DisplayText);
            Assert.AreEqual(2, visual.DisplayText.Split('\n').Length);
        }

        MonsterController CreateMonsterController(out MonsterAffixVisual visual)
        {
            GameObject instance = new GameObject("Alpha015Monster");
            _objects.Add(instance);
            instance.AddComponent<SpriteRenderer>();
            instance.AddComponent<CombatActor>();
            visual = instance.AddComponent<MonsterAffixVisual>();
            MonsterController controller = instance.AddComponent<MonsterController>();
            controller.enabled = false;
            return controller;
        }

        MonsterDefinition CreateMonsterDefinition()
        {
            MonsterDefinition definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            _objects.Add(definition);
            SetField(definition, "_id", "alpha015_monster");
            SetField(definition, "_displayName", "测试怪物");
            return definition;
        }

        MonsterAffixDefinition CreateAffix(string id, string displayName, Color color)
        {
            MonsterAffixDefinition definition = ScriptableObject.CreateInstance<MonsterAffixDefinition>();
            _objects.Add(definition);
            SetField(definition, "_id", id);
            SetField(definition, "_displayName", displayName);
            SetField(definition, "_groupId", id);
            SetField(definition, "_weight", 100);
            SetField(definition, "_displayColor", color);
            return definition;
        }

        static MonsterInstanceData CreateInstance(
            IEnumerable<MonsterAffixInstance> affixes,
            IEnumerable<ModifierInstance> modifiers)
        {
            StatBlock baseStats = new StatBlock();
            baseStats.SetValue(StatIds.MaxHealth, 100f);
            baseStats.SetValue(StatIds.Armor, 5f);
            baseStats.SetValue(StatIds.MoveSpeed, 2f);
            List<ModifierInstance> modifierList = new List<ModifierInstance>(modifiers);
            StatBlock effectiveStats = CombatStatResolver.Build(baseStats, modifierList);
            return new MonsterInstanceData(
                new MonsterInstanceRandomSeeds(71),
                100f,
                baseStats,
                effectiveStats,
                affixes,
                modifierList);
        }

        static ModifierInstance CreateModifier(string statId, ModifierOperation operation, float value)
        {
            return new ModifierInstance(
                statId,
                operation,
                ModifierScope.GlobalActor,
                value,
                DamageType.Physical,
                DamageType.Physical,
                TagSet.Empty,
                TagSet.Empty);
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
