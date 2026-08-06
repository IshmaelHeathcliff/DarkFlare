using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public class RandomizationPhase3Tests
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
        public void GameplayRandomSystem_ReplaysRootSeed_AndKeepsChannelsIndependent()
        {
            GameplayRandomSystem randomSystem = new GameplayRandomSystem();
            randomSystem.Configure(true, 24680);
            int firstPlayerAttack = randomSystem.NextSeed(GameplayRandomChannel.PlayerAttack);
            int firstLoot = randomSystem.NextSeed(GameplayRandomChannel.Loot);
            int secondPlayerAttack = randomSystem.NextSeed(GameplayRandomChannel.PlayerAttack);

            randomSystem.Configure(true, 24680);
            Assert.AreEqual(firstPlayerAttack, randomSystem.NextSeed(GameplayRandomChannel.PlayerAttack));
            Assert.AreEqual(firstLoot, randomSystem.NextSeed(GameplayRandomChannel.Loot));
            Assert.AreEqual(secondPlayerAttack, randomSystem.NextSeed(GameplayRandomChannel.PlayerAttack));

            randomSystem.Configure(true, 24680);

            for (int i = 0; i < 20; i++)
            {
                randomSystem.NextSeed(GameplayRandomChannel.SpawnPosition);
                randomSystem.NextSeed(GameplayRandomChannel.MonsterInstance);
            }

            Assert.AreEqual(firstPlayerAttack, randomSystem.NextSeed(GameplayRandomChannel.PlayerAttack));
        }

        [Test]
        public void MonsterInstanceData_IsDeterministic_AndStaysWithinHealthRange()
        {
            MonsterDefinition monster = CreateScriptableObject<MonsterDefinition>();
            SetField(monster, "_maxHealth", 100f);
            SetField(monster, "_healthMultiplierRange", new Vector2(0.85f, 1.15f));

            MonsterInstanceData first = monster.CreateInstanceData(314159);
            MonsterInstanceData replay = monster.CreateInstanceData(314159);

            Assert.AreEqual(first.Seed, replay.Seed);
            Assert.AreEqual(first.MaxHealth, replay.MaxHealth);
            Assert.AreEqual(first.MaxHealth, first.Stats.GetValue(StatIds.MaxHealth));

            for (int seed = 0; seed < 500; seed++)
            {
                MonsterInstanceData instance = monster.CreateInstanceData(seed);
                Assert.That(instance.MaxHealth, Is.InRange(85f, 115f));
            }
        }

        [Test]
        public void OfficialAttackDefinitions_UseConfiguredRanges_AndReplaySeeds()
        {
            ProjectileSkillDefinition skill = AssetDatabase.LoadAssetAtPath<ProjectileSkillDefinition>(
                "Assets/Data/Preset/Skills/基础投射物技能.asset");
            MonsterDefinition monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                "Assets/Data/Preset/Monsters/基础怪物.asset");

            Assert.IsNotNull(skill);
            Assert.IsNotNull(monster);

            for (int seed = 0; seed < 100; seed++)
            {
                List<DamagePacket> firstPlayerRoll = skill.CreateDamagePackets(seed);
                List<DamagePacket> replayPlayerRoll = skill.CreateDamagePackets(seed);
                List<DamagePacket> firstMonsterRoll = monster.CreateContactDamagePackets(seed);
                List<DamagePacket> replayMonsterRoll = monster.CreateContactDamagePackets(seed);

                Assert.AreEqual(1, firstPlayerRoll.Count);
                Assert.AreEqual(1, firstMonsterRoll.Count);
                Assert.That(firstPlayerRoll[0].Amount, Is.InRange(10f, 14f));
                Assert.That(firstMonsterRoll[0].Amount, Is.InRange(6f, 10f));
                Assert.AreEqual(firstPlayerRoll[0].Amount, replayPlayerRoll[0].Amount);
                Assert.AreEqual(firstMonsterRoll[0].Amount, replayMonsterRoll[0].Amount);
            }
        }

        [Test]
        public void LootDropChance_HandlesZeroAndOneBoundaries()
        {
            ItemBaseDefinition item = CreateScriptableObject<ItemBaseDefinition>();
            LootTableDefinition table = CreateLootTable(item, 100);

            SetField(table, "_dropChance", 0f);
            Assert.IsNull(table.GenerateLoot(new System.Random(1), "never", 1));

            SetField(table, "_dropChance", 1f);
            ItemInstance generated = table.GenerateLoot(new System.Random(1), "always", 1);
            Assert.IsNotNull(generated);
            Assert.AreEqual("always", generated.InstanceId);
        }

        [Test]
        public void LootDropChance_ThirtyFivePercent_StaysWithinStatisticalTolerance()
        {
            ItemBaseDefinition item = CreateScriptableObject<ItemBaseDefinition>();
            LootTableDefinition table = CreateLootTable(item, 100);
            SetField(table, "_dropChance", 0.35f);
            System.Random random = new System.Random(20260806);
            int dropped = 0;
            const int Total = 10000;

            for (int i = 0; i < Total; i++)
            {
                if (table.GenerateLoot(random, $"stat_{i}", 1) != null)
                {
                    dropped++;
                }
            }

            float ratio = dropped / (float)Total;
            Assert.That(ratio, Is.InRange(0.32f, 0.38f));
        }

        [Test]
        public void ConfigurationValidator_ReportsInvertedNegativeAndEmptyRules()
        {
            DamageRollDefinition invalidDamage = new DamageRollDefinition();
            SetField(invalidDamage, "_amountRange", new Vector2(10f, -2f));
            MonsterDefinition monster = CreateScriptableObject<MonsterDefinition>();
            SetField(monster, "_healthMultiplierRange", new Vector2(1.2f, 0.8f));
            SetField(monster, "_contactDamages", new List<DamageRollDefinition> { invalidDamage });
            List<string> monsterIssues = RandomizationConfigurationValidator.Validate(monster);

            Assert.IsTrue(monsterIssues.Exists(issue => issue.Contains("生命倍率范围")));
            Assert.IsTrue(monsterIssues.Exists(issue => issue.Contains("负伤害")));
            Assert.IsTrue(monsterIssues.Exists(issue => issue.Contains("伤害 0 的范围")));

            LootTableDefinition lootTable = CreateScriptableObject<LootTableDefinition>();
            SetField(lootTable, "_dropChance", 1.2f);
            List<string> emptyLootIssues = RandomizationConfigurationValidator.Validate(lootTable);
            Assert.IsTrue(emptyLootIssues.Exists(issue => issue.Contains("掉落概率")));
            Assert.IsTrue(emptyLootIssues.Exists(issue => issue.Contains("掉落池不能为空")));

            ItemBaseDefinition item = CreateScriptableObject<ItemBaseDefinition>();
            SetField(lootTable, "_entries", new List<LootTableEntry> { CreateEntry(item, 0) });
            List<string> zeroWeightIssues = RandomizationConfigurationValidator.Validate(lootTable);
            Assert.IsTrue(zeroWeightIssues.Exists(issue => issue.Contains("正权重")));
        }

        [Test]
        public void OfficialPhaseThreePresets_PassValidationAndKeepExpectedBaselines()
        {
            ProjectileSkillDefinition skill = AssetDatabase.LoadAssetAtPath<ProjectileSkillDefinition>(
                "Assets/Data/Preset/Skills/基础投射物技能.asset");
            MonsterDefinition monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                "Assets/Data/Preset/Monsters/基础怪物.asset");
            LootTableDefinition lootTable = AssetDatabase.LoadAssetAtPath<LootTableDefinition>(
                "Assets/Data/Preset/Loot/基础怪物掉落表.asset");
            ItemBaseDefinition greatSword = AssetDatabase.LoadAssetAtPath<ItemBaseDefinition>(
                "Assets/Data/Preset/Items/001大剑.asset");

            Assert.IsNotNull(skill);
            Assert.IsNotNull(monster);
            Assert.IsNotNull(lootTable);
            Assert.IsNotNull(greatSword);
            Assert.IsEmpty(RandomizationConfigurationValidator.Validate(skill));
            Assert.IsEmpty(RandomizationConfigurationValidator.Validate(monster));
            Assert.IsEmpty(RandomizationConfigurationValidator.Validate(lootTable));
            Assert.IsEmpty(EquipmentConfigurationValidator.Validate(greatSword));
            Assert.AreEqual(new Vector2(10f, 14f), skill.BaseDamages[0].AmountRange);
            Assert.AreEqual(new Vector2(6f, 10f), monster.ContactDamages[0].AmountRange);
            Assert.AreEqual(new Vector2(0.85f, 1.15f), monster.HealthMultiplierRange);
            Assert.AreEqual(0.35f, lootTable.DropChance);
            Assert.AreEqual(new Vector2(20f, 40f), greatSword.BaseDamages[0].AmountRange);
        }

        LootTableDefinition CreateLootTable(ItemBaseDefinition item, int weight)
        {
            LootTableDefinition table = CreateScriptableObject<LootTableDefinition>();
            SetField(table, "_entries", new List<LootTableEntry> { CreateEntry(item, weight) });
            return table;
        }

        static LootTableEntry CreateEntry(ItemBaseDefinition item, int weight)
        {
            LootTableEntry entry = new LootTableEntry();
            SetField(entry, "_item", item);
            SetField(entry, "_weight", weight);
            return entry;
        }

        T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _objects.Add(value);
            return value;
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"缺少字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
