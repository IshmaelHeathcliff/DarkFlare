using System;
using System.Collections.Generic;
using DarkFlare;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;

namespace DarkFlare.Tests
{
    public sealed class Alpha015MonsterAffixContentTests
    {
        const string MonsterAffixRoot = "Assets/Data/Preset/MonsterAffixes";
        const string MonsterRoot = "Assets/Data/Preset/Monsters";

        [Test]
        public void OfficialMonsterAffixes_MatchFrozenContracts()
        {
            Dictionary<string, AffixContract> contracts = CreateContracts();
            List<MonsterAffixDefinition> affixes = LoadAssets<MonsterAffixDefinition>(MonsterAffixRoot);

            Assert.AreEqual(10, affixes.Count);

            for (int i = 0; i < affixes.Count; i++)
            {
                MonsterAffixDefinition affix = affixes[i];
                Assert.IsTrue(contracts.TryGetValue(affix.Id, out AffixContract contract), affix.Id);
                Assert.AreEqual(contract.GroupId, affix.GroupId, affix.Id);
                Assert.AreEqual(contract.Weight, affix.Weight, affix.Id);
                Assert.Greater(affix.DisplayColor.a, 0f, affix.Id);
                Assert.AreEqual(1, affix.Modifiers.Count, affix.Id);
                Assert.IsEmpty(
                    MonsterAffixConfigurationValidator.Validate(affix),
                    affix.Id);
                StatModifierDefinition modifier = affix.Modifiers[0];
                Assert.AreEqual(ModifierScope.GlobalActor, modifier.Scope, affix.Id);
                Assert.AreEqual(contract.Operation, modifier.Operation, affix.Id);
                Assert.AreEqual(contract.MinimumValue, modifier.ValueRange.x, 0.001f, affix.Id);
                Assert.AreEqual(contract.MaximumValue, modifier.ValueRange.y, 0.001f, affix.Id);
                Assert.AreEqual(contract.StatId, modifier.Stat != null ? modifier.Stat.Id : string.Empty, affix.Id);

                if (modifier.Operation == ModifierOperation.GainAsExtra)
                {
                    Assert.AreEqual(DamageType.Physical, modifier.FromDamageType, affix.Id);
                    Assert.AreEqual(contract.ToDamageType, modifier.ToDamageType, affix.Id);
                }
            }
        }

        [Test]
        public void OfficialMonsters_ExposeCompleteZeroToTwoAffixPools()
        {
            List<MonsterAffixDefinition> affixes = LoadAssets<MonsterAffixDefinition>(MonsterAffixRoot);
            List<MonsterDefinition> monsters = LoadAssets<MonsterDefinition>(MonsterRoot);

            Assert.AreEqual(3, monsters.Count);

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterDefinition monster = monsters[i];
                Assert.AreEqual(0, monster.MinimumAffixCount, monster.Id);
                Assert.AreEqual(2, monster.MaximumAffixCount, monster.Id);
                Assert.AreEqual(affixes.Count, monster.AffixPool.Count, monster.Id);
                CollectionAssert.AreEquivalent(affixes, monster.AffixPool, monster.Id);
                Assert.IsEmpty(RandomizationConfigurationValidator.Validate(monster), monster.Id);
            }
        }

        [Test]
        public void OfficialMonsterGeneration_ReplaysAndEventuallyCoversAllAffixes()
        {
            List<MonsterAffixDefinition> expected = LoadAssets<MonsterAffixDefinition>(MonsterAffixRoot);
            List<MonsterDefinition> monsters = LoadAssets<MonsterDefinition>(MonsterRoot);
            HashSet<MonsterAffixDefinition> covered = new HashSet<MonsterAffixDefinition>();

            for (int monsterIndex = 0; monsterIndex < monsters.Count; monsterIndex++)
            {
                MonsterDefinition monster = monsters[monsterIndex];

                for (int seed = 0; seed < 512; seed++)
                {
                    int rootSeed = monsterIndex * 10000 + seed;
                    MonsterInstanceData first = monster.CreateInstanceData(rootSeed);
                    MonsterInstanceData replay = monster.CreateInstanceData(rootSeed);
                    Assert.AreEqual(Describe(first), Describe(replay), $"{monster.Id}/{rootSeed}");
                    Assert.That(first.Affixes.Count, Is.InRange(0, 2), $"{monster.Id}/{rootSeed}");
                    HashSet<string> groups = new HashSet<string>(StringComparer.Ordinal);

                    for (int affixIndex = 0; affixIndex < first.Affixes.Count; affixIndex++)
                    {
                        MonsterAffixDefinition definition = first.Affixes[affixIndex].Definition;
                        Assert.IsTrue(groups.Add(definition.GroupId), $"{monster.Id}/{rootSeed} 重复组 {definition.GroupId}");
                        covered.Add(definition);
                    }
                }
            }

            CollectionAssert.AreEquivalent(expected, covered);
        }

        [Test]
        public void OfficialMonsterAffixes_ProduceObservableCombatEffects()
        {
            List<MonsterAffixDefinition> affixes = LoadAssets<MonsterAffixDefinition>(MonsterAffixRoot);

            for (int i = 0; i < affixes.Count; i++)
            {
                MonsterAffixDefinition affix = affixes[i];
                MonsterAffixInstance instance = affix.CreateInstance(3200 + i);
                StatModifierDefinition definition = affix.Modifiers[0];

                if (definition.Operation == ModifierOperation.GainAsExtra)
                {
                    DamageResult result = CalculatePhysicalDamage(instance.Modifiers);
                    Assert.IsTrue(result.DamageBeforeDefense.ContainsKey(definition.ToDamageType), affix.Id);
                    Assert.That(
                        result.DamageBeforeDefense[definition.ToDamageType],
                        Is.InRange(definition.ValueRange.x, definition.ValueRange.y),
                        affix.Id);
                    continue;
                }

                StatBlock baseStats = new StatBlock();
                baseStats.SetValue(definition.Stat.Id, 100f);
                StatBlock effective = CombatStatResolver.Build(baseStats, instance.Modifiers);
                Assert.AreNotEqual(100f, effective.GetValue(definition.Stat.Id), affix.Id);
            }
        }

        [Test]
        public void OfficialContent_PassesMonsterAffixValidation()
        {
            List<ContentValidationIssue> issues = ContentConfigurationValidator.Scan();

            Assert.IsEmpty(issues, string.Join("\n", issues.ConvertAll(issue => $"{issue.AssetPath}: {issue.Message}")));
        }

        static Dictionary<string, AffixContract> CreateContracts()
        {
            return new Dictionary<string, AffixContract>(StringComparer.Ordinal)
            {
                { "monster_flame_touched", Extra("monster_elemental_damage", 70, DamageType.Fire) },
                { "monster_frost_touched", Extra("monster_elemental_damage", 70, DamageType.Cold) },
                { "monster_storm_touched", Extra("monster_elemental_damage", 70, DamageType.Lightning) },
                { "monster_fire_guarded", Stat("monster_elemental_guard", 80, StatIds.FireResistance, ModifierOperation.Flat, 20f, 35f) },
                { "monster_cold_guarded", Stat("monster_elemental_guard", 80, StatIds.ColdResistance, ModifierOperation.Flat, 20f, 35f) },
                { "monster_lightning_guarded", Stat("monster_elemental_guard", 80, StatIds.LightningResistance, ModifierOperation.Flat, 20f, 35f) },
                { "monster_robust", Stat("monster_health", 100, StatIds.MaxHealth, ModifierOperation.More, 25f, 40f) },
                { "monster_armored", Stat("monster_armor", 100, StatIds.Armor, ModifierOperation.Flat, 30f, 60f) },
                { "monster_elusive", Stat("monster_evasion", 90, StatIds.Evasion, ModifierOperation.Flat, 20f, 40f) },
                { "monster_swift", Stat("monster_speed", 80, StatIds.MoveSpeed, ModifierOperation.Increase, 10f, 20f) },
            };
        }

        static AffixContract Extra(string groupId, int weight, DamageType toDamageType)
        {
            return new AffixContract(
                groupId,
                weight,
                string.Empty,
                ModifierOperation.GainAsExtra,
                15f,
                25f,
                toDamageType);
        }

        static AffixContract Stat(
            string groupId,
            int weight,
            string statId,
            ModifierOperation operation,
            float minimumValue,
            float maximumValue)
        {
            return new AffixContract(
                groupId,
                weight,
                statId,
                operation,
                minimumValue,
                maximumValue,
                DamageType.Physical);
        }

        static DamageResult CalculatePhysicalDamage(IReadOnlyList<ModifierInstance> modifiers)
        {
            return DamageCalculator.Calculate(new DamageContext(
                "monster",
                "player",
                "monster_contact",
                string.Empty,
                1,
                new[]
                {
                    new DamagePacket(DamageType.Physical, 100f, new TagSet(new[] { "damage", "physical" })),
                },
                new TagSet(new[] { "melee" }),
                new StatBlock(),
                new StatBlock(),
                modifiers,
                new List<ModifierInstance>()));
        }

        static string Describe(MonsterInstanceData instance)
        {
            List<string> parts = new List<string>
            {
                instance.BaseMaxHealth.ToString("R"),
            };

            for (int affixIndex = 0; affixIndex < instance.Affixes.Count; affixIndex++)
            {
                MonsterAffixInstance affix = instance.Affixes[affixIndex];
                parts.Add(affix.Definition.Id);
                parts.Add(affix.ValueSeed.ToString());

                for (int modifierIndex = 0; modifierIndex < affix.Modifiers.Count; modifierIndex++)
                {
                    parts.Add(affix.Modifiers[modifierIndex].Value.ToString("R"));
                }
            }

            return string.Join("|", parts);
        }

        static List<T> LoadAssets<T>(string root) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root });
            List<T> assets = new List<T>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        sealed class AffixContract
        {
            public string GroupId { get; }

            public int Weight { get; }

            public string StatId { get; }

            public ModifierOperation Operation { get; }

            public float MinimumValue { get; }

            public float MaximumValue { get; }

            public DamageType ToDamageType { get; }

            public AffixContract(
                string groupId,
                int weight,
                string statId,
                ModifierOperation operation,
                float minimumValue,
                float maximumValue,
                DamageType toDamageType)
            {
                GroupId = groupId;
                Weight = weight;
                StatId = statId;
                Operation = operation;
                MinimumValue = minimumValue;
                MaximumValue = maximumValue;
                ToDamageType = toDamageType;
            }
        }
    }
}
