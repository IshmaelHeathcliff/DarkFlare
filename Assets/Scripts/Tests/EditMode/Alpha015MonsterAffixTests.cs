using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class Alpha015MonsterAffixTests
    {
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
        public void Generator_ReplaysSelectionAndValuesFromRootSeed()
        {
            List<MonsterAffixDefinition> pool = CreateStandardPool();
            MonsterInstanceRandomSeeds seeds = new MonsterInstanceRandomSeeds(78123);

            List<MonsterAffixInstance> first = MonsterAffixGenerator.Generate(pool, 2, 2, seeds);
            List<MonsterAffixInstance> replay = MonsterAffixGenerator.Generate(pool, 2, 2, seeds);

            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(Describe(first), Describe(replay));
        }

        [Test]
        public void Generator_SelectsWithoutReplacementFromExclusiveGroups()
        {
            MonsterAffixDefinition fire = CreateStatAffix("fire", "elemental", 100, StatIds.FireResistance, 20f, 35f);
            MonsterAffixDefinition cold = CreateStatAffix("cold", "elemental", 100, StatIds.ColdResistance, 20f, 35f);
            MonsterAffixDefinition armor = CreateStatAffix("armor", "armor", 100, StatIds.Armor, 30f, 60f);

            List<MonsterAffixInstance> result = MonsterAffixGenerator.Generate(
                new[] { fire, cold, armor },
                2,
                2,
                new MonsterInstanceRandomSeeds(311));

            Assert.AreEqual(2, result.Count);
            Assert.AreNotEqual(result[0].Definition.GroupId, result[1].Definition.GroupId);
        }

        [Test]
        public void Generator_UsesConfiguredWeights()
        {
            MonsterAffixDefinition common = CreateStatAffix("common", "common", 1, StatIds.Armor, 1f, 1f);
            MonsterAffixDefinition rare = CreateStatAffix("rare", "rare", 9, StatIds.Evasion, 1f, 1f);
            int commonCount = 0;
            int rareCount = 0;

            for (int seed = 0; seed < 1000; seed++)
            {
                List<MonsterAffixInstance> result = MonsterAffixGenerator.Generate(
                    new[] { common, rare },
                    1,
                    1,
                    new MonsterInstanceRandomSeeds(seed));

                if (result[0].Definition == common)
                {
                    commonCount++;
                }
                else
                {
                    rareCount++;
                }
            }

            Assert.Greater(rareCount, commonCount * 4);
        }

        [Test]
        public void MonsterDefinition_SeparatesHealthSelectionAndAffixValueStreams()
        {
            MonsterAffixDefinition armor = CreateStatAffix("armored", "armor", 100, StatIds.Armor, 30f, 60f);
            MonsterDefinition definition = CreateMonster(new[] { armor }, 1, 1);
            MonsterInstanceData first = definition.CreateInstanceData(9917);

            SetField(armor.Modifiers[0], "_valueRange", new Vector2(300f, 600f));
            MonsterInstanceData changed = definition.CreateInstanceData(9917);

            Assert.AreEqual(first.BaseMaxHealth, changed.BaseMaxHealth, 0.0001f);
            Assert.AreEqual(first.Affixes[0].Definition.Id, changed.Affixes[0].Definition.Id);
            Assert.AreNotEqual(first.Modifiers[0].Value, changed.Modifiers[0].Value);
            Assert.AreEqual(first.RandomSeeds.HealthSeed, changed.RandomSeeds.HealthSeed);
            Assert.AreNotEqual(first.RandomSeeds.HealthSeed, first.RandomSeeds.AffixSelectionSeed);
        }

        [Test]
        public void MonsterDefinition_AppliesRolledGlobalStatsToEffectiveSnapshot()
        {
            MonsterAffixDefinition robust = CreateStatAffix(
                "robust",
                "health",
                100,
                StatIds.MaxHealth,
                25f,
                25f,
                ModifierOperation.More);
            MonsterDefinition definition = CreateMonster(new[] { robust }, 1, 1);

            MonsterInstanceData instance = definition.CreateInstanceData(741);

            Assert.AreEqual(instance.BaseMaxHealth * 1.25f, instance.MaxHealth, 0.001f);
            Assert.AreEqual(instance.MaxHealth, instance.EffectiveStats.GetValue(StatIds.MaxHealth), 0.001f);
            Assert.AreEqual(1, instance.Modifiers.Count);
        }

        [Test]
        public void Validator_RejectsItemScopeAndUnsupportedOperations()
        {
            StatModifierDefinition modifier = CreateModifier(
                CreateStat(StatIds.Armor),
                ModifierOperation.Override,
                ModifierScope.LocalItem,
                10f,
                10f);
            MonsterAffixDefinition definition = CreateAffix(
                "invalid",
                "invalid_group",
                100,
                new[] { modifier });

            List<string> issues = MonsterAffixConfigurationValidator.Validate(definition);

            Assert.IsTrue(issues.Exists(issue => issue.Contains("GlobalActor")));
            Assert.IsTrue(issues.Exists(issue => issue.Contains("不支持")));
        }

        List<MonsterAffixDefinition> CreateStandardPool()
        {
            return new List<MonsterAffixDefinition>
            {
                CreateStatAffix("armored", "armor", 100, StatIds.Armor, 30f, 60f),
                CreateStatAffix("elusive", "evasion", 80, StatIds.Evasion, 20f, 40f),
                CreateStatAffix("swift", "speed", 60, StatIds.MoveSpeed, 10f, 20f, ModifierOperation.Increase),
            };
        }

        MonsterDefinition CreateMonster(
            IReadOnlyList<MonsterAffixDefinition> pool,
            int minimumCount,
            int maximumCount)
        {
            MonsterDefinition definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            _objects.Add(definition);
            SetField(definition, "_id", "test_monster");
            SetField(definition, "_displayName", "测试怪物");
            SetField(definition, "_maxHealth", 100f);
            SetField(definition, "_healthMultiplierRange", new Vector2(0.8f, 1.2f));
            SetField(definition, "_affixPool", new List<MonsterAffixDefinition>(pool));
            SetField(definition, "_minimumAffixCount", minimumCount);
            SetField(definition, "_maximumAffixCount", maximumCount);
            return definition;
        }

        MonsterAffixDefinition CreateStatAffix(
            string id,
            string groupId,
            int weight,
            string statId,
            float minimumValue,
            float maximumValue,
            ModifierOperation operation = ModifierOperation.Flat)
        {
            StatModifierDefinition modifier = CreateModifier(
                CreateStat(statId),
                operation,
                ModifierScope.GlobalActor,
                minimumValue,
                maximumValue);
            return CreateAffix(id, groupId, weight, new[] { modifier });
        }

        MonsterAffixDefinition CreateAffix(
            string id,
            string groupId,
            int weight,
            IReadOnlyList<StatModifierDefinition> modifiers)
        {
            MonsterAffixDefinition definition = ScriptableObject.CreateInstance<MonsterAffixDefinition>();
            _objects.Add(definition);
            SetField(definition, "_id", id);
            SetField(definition, "_displayName", id);
            SetField(definition, "_groupId", groupId);
            SetField(definition, "_weight", weight);
            SetField(definition, "_modifiers", new List<StatModifierDefinition>(modifiers));
            return definition;
        }

        StatDefinition CreateStat(string id)
        {
            StatDefinition definition = ScriptableObject.CreateInstance<StatDefinition>();
            _objects.Add(definition);
            SetField(definition, "_id", id);
            SetField(definition, "_displayName", id);
            return definition;
        }

        static StatModifierDefinition CreateModifier(
            StatDefinition stat,
            ModifierOperation operation,
            ModifierScope scope,
            float minimumValue,
            float maximumValue)
        {
            StatModifierDefinition modifier = new StatModifierDefinition();
            SetField(modifier, "_stat", stat);
            SetField(modifier, "_operation", operation);
            SetField(modifier, "_scope", scope);
            SetField(modifier, "_valueRange", new Vector2(minimumValue, maximumValue));
            return modifier;
        }

        static string Describe(IReadOnlyList<MonsterAffixInstance> affixes)
        {
            List<string> values = new List<string>(affixes.Count);

            for (int i = 0; i < affixes.Count; i++)
            {
                List<string> modifierValues = new List<string>(affixes[i].Modifiers.Count);

                for (int modifierIndex = 0; modifierIndex < affixes[i].Modifiers.Count; modifierIndex++)
                {
                    modifierValues.Add(affixes[i].Modifiers[modifierIndex].Value.ToString("R"));
                }

                values.Add($"{affixes[i].Definition.Id}:{affixes[i].ValueSeed}:{string.Join(",", modifierValues)}");
            }

            return string.Join("|", values);
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
