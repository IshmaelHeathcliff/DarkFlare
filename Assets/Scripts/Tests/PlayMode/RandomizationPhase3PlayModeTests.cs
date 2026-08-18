using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public class RandomizationPhase3PlayModeTests
    {
        const int FixedSeed = 24681357;
        const int MonsterSampleCount = 12;
        const float RunTimeoutSeconds = 35f;

        static readonly FieldInfo UseFixedSeedField = typeof(CombatPrototypeBootstrap).GetField(
            "_useFixedRandomSeed",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo FixedSeedField = typeof(CombatPrototypeBootstrap).GetField(
            "_fixedRandomSeed",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo PlayerSkillField = typeof(CombatPrototypeBootstrap).GetField(
            "_playerSkill",
            BindingFlags.Instance | BindingFlags.NonPublic);

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator MainScene_SameFixedSeedReplaysMonsterHealthDamageAndLoot()
        {
            RunSnapshot first = null;
            yield return CaptureMainRun(snapshot => first = snapshot);

            yield return _fixture.Restart();
            RunSnapshot second = null;
            yield return CaptureMainRun(snapshot => second = snapshot);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            CollectionAssert.AreEqual(first.Monsters, second.Monsters, "怪物实例生命序列未能按固定种子复现");
            CollectionAssert.AreEquivalent(
                new[] { "wasteland_wraith", "razor_hound", "iron_husk" },
                first.MonsterIds,
                "Main 前 12 个生成实例未覆盖三种怪物");
            CollectionAssert.AreEqual(first.PlayerDamages, second.PlayerDamages, "玩家攻击伤害序列未能按固定种子复现");
            CollectionAssert.AreEqual(first.MonsterDamages, second.MonsterDamages, "怪物攻击伤害序列未能按固定种子复现");
            CollectionAssert.AreEqual(first.Loot, second.Loot, "掉落结果序列未能按固定种子复现");
            Debug.Log($"[Phase3RandomizationPlayMode] 固定种子 {FixedSeed} 的两次 Main 运行结果一致: {first}");
        }

        [UnityTest]
        public IEnumerator MainScene_DefaultRandomSeedChangesBetweenRuns()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            CombatPrototypeBootstrap firstBootstrap = UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
            Assert.IsNotNull(firstBootstrap);
            Assert.IsNotNull(UseFixedSeedField, "未找到固定种子开关字段");
            Assert.IsFalse((bool)UseFixedSeedField.GetValue(firstBootstrap), "Main 场景的固定种子开关应默认关闭");
            int firstRootSeed = GameArchitectureProvider.RequireCurrent()
                .GetSystem<GameplayRandomSystem>()
                .RootSeed;

            yield return _fixture.Restart();
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            CombatPrototypeBootstrap secondBootstrap = UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
            Assert.IsNotNull(secondBootstrap);
            Assert.IsFalse((bool)UseFixedSeedField.GetValue(secondBootstrap), "Main 场景的固定种子开关应默认关闭");
            int secondRootSeed = GameArchitectureProvider.RequireCurrent()
                .GetSystem<GameplayRandomSystem>()
                .RootSeed;

            Assert.AreNotEqual(firstRootSeed, secondRootSeed, "关闭固定种子后，不同 Main 启动应生成不同根种子");
            Debug.Log($"[Phase3RandomizationPlayMode] 随机启动根种子已变化: {firstRootSeed} -> {secondRootSeed}");
        }

        static IEnumerator CaptureMainRun(Action<RunSnapshot> onCompleted)
        {
            SceneManager.sceneLoaded += ConfigureBootstrapSeed;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= ConfigureBootstrapSeed;

            CombatPrototypeBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
            Assert.IsNotNull(bootstrap, "Main 场景缺少 CombatPrototypeBootstrap");
            Assert.IsNotNull(PlayerSkillField, "未找到玩家技能序列化字段");
            ProjectileSkillDefinition skill = PlayerSkillField.GetValue(bootstrap) as ProjectileSkillDefinition;
            Assert.IsNotNull(skill, "Main 场景未配置玩家技能");

            PlayerController player = null;
            float timeout = Time.realtimeSinceStartup + RunTimeoutSeconds;

            while (player == null && Time.realtimeSinceStartup < timeout)
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                yield return null;
            }

            Assert.IsNotNull(player, "Main 场景未在时限内生成玩家");
            player.enabled = false;

            List<MonsterRecord> monsters = new List<MonsterRecord>(MonsterSampleCount);
            HashSet<int> capturedSeeds = new HashSet<int>();
            timeout = Time.realtimeSinceStartup + RunTimeoutSeconds;

            while (monsters.Count < MonsterSampleCount && Time.realtimeSinceStartup < timeout)
            {
                MonsterController[] current = UnityEngine.Object.FindObjectsByType<MonsterController>();

                for (int i = 0; i < current.Length && monsters.Count < MonsterSampleCount; i++)
                {
                    MonsterController monster = current[i];

                    if (monster.Instance == null || !capturedSeeds.Add(monster.Instance.Seed))
                    {
                        continue;
                    }

                    monster.enabled = false;
                    monsters.Add(new MonsterRecord(
                        monster.Definition.Id,
                        monster.Instance.Seed,
                        monster.Instance.MaxHealth));
                }

                yield return null;
            }

            Assert.AreEqual(MonsterSampleCount, monsters.Count, "Main 场景未在时限内生成足够的怪物样本");
            GameplayRandomSystem randomSystem = GameArchitectureProvider.RequireCurrent()
                .GetSystem<GameplayRandomSystem>();
            randomSystem.Configure(true, FixedSeed);

            RunSnapshot result = new RunSnapshot(monsters);
            EquipmentModel equipment = GameArchitectureProvider.RequireCurrent()
                .GetModel<EquipmentModel>();
            MonsterDefinition monsterDefinition = FindMonsterDefinition("wasteland_wraith");
            Assert.IsNotNull(monsterDefinition, "Main 场景未生成用于伤害和掉落复现的荒原游魂");
            Assert.IsNotNull(monsterDefinition.LootTable, "Main 场景怪物未配置掉落表");

            for (int i = 0; i < MonsterSampleCount; i++)
            {
                int playerSeed = randomSystem.NextSeed(GameplayRandomChannel.PlayerAttack);
                AttackSnapshot playerAttack = AttackSnapshotFactory.CreateProjectile(player.Actor, skill, equipment, playerSeed);
                result.PlayerDamages.Add(FormatDamage(playerAttack.BaseDamages));

                int monsterSeed = randomSystem.NextSeed(GameplayRandomChannel.MonsterAttack);
                AttackRandomRolls monsterRolls = AttackRandomRolls.FromRootSeed(monsterSeed);
                result.MonsterDamages.Add(FormatDamage(monsterDefinition.CreateContactDamagePackets(
                    monsterRolls.BaseDamageSeed)));

                int lootSeed = randomSystem.NextSeed(GameplayRandomChannel.Loot);
                ItemInstance item = monsterDefinition.LootTable.GenerateLoot(
                    new System.Random(lootSeed),
                    $"test_{lootSeed:x8}",
                    1);
                result.Loot.Add(item == null ? "none" : $"{item.BaseDefinition.Id}:{item.Seed}");
            }

            onCompleted(result);
        }

        static MonsterDefinition FindMonsterDefinition(string id)
        {
            MonsterController[] monsters = UnityEngine.Object.FindObjectsByType<MonsterController>();

            for (int i = 0; i < monsters.Length; i++)
            {
                if (monsters[i].Definition != null && monsters[i].Definition.Id == id)
                {
                    return monsters[i].Definition;
                }
            }

            return null;
        }

        static void ConfigureBootstrapSeed(Scene scene, LoadSceneMode mode)
        {
            CombatPrototypeBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>(FindObjectsInactive.Include);
            Assert.IsNotNull(bootstrap, $"场景 {scene.name} 缺少 CombatPrototypeBootstrap");
            Assert.IsNotNull(UseFixedSeedField, "未找到固定种子开关字段");
            Assert.IsNotNull(FixedSeedField, "未找到固定种子值字段");
            UseFixedSeedField.SetValue(bootstrap, true);
            FixedSeedField.SetValue(bootstrap, FixedSeed);
        }

        static string FormatDamage(IReadOnlyList<DamagePacket> packets)
        {
            List<string> values = new List<string>(packets.Count);

            for (int i = 0; i < packets.Count; i++)
            {
                values.Add($"{packets[i].DamageType}:{packets[i].Amount.ToString("R", CultureInfo.InvariantCulture)}");
            }

            return string.Join(",", values);
        }

        sealed class RunSnapshot
        {
            public readonly List<MonsterRecord> Monsters;
            public readonly HashSet<string> MonsterIds = new HashSet<string>();
            public readonly List<string> PlayerDamages = new List<string>();
            public readonly List<string> MonsterDamages = new List<string>();
            public readonly List<string> Loot = new List<string>();

            public RunSnapshot(List<MonsterRecord> monsters)
            {
                Monsters = monsters;

                for (int i = 0; i < monsters.Count; i++)
                {
                    MonsterIds.Add(monsters[i].MonsterId);
                }
            }

            public override string ToString()
            {
                return $"monsters=[{string.Join(",", Monsters)}], player=[{string.Join(",", PlayerDamages)}], monster=[{string.Join(",", MonsterDamages)}], loot=[{string.Join(",", Loot)}]";
            }
        }

        readonly struct MonsterRecord : IEquatable<MonsterRecord>
        {
            public string MonsterId { get; }

            readonly int _seed;
            readonly float _maxHealth;

            public MonsterRecord(string monsterId, int seed, float maxHealth)
            {
                MonsterId = monsterId;
                _seed = seed;
                _maxHealth = maxHealth;
            }

            public bool Equals(MonsterRecord other)
            {
                return MonsterId == other.MonsterId
                       && _seed == other._seed
                       && Math.Abs(_maxHealth - other._maxHealth) < float.Epsilon;
            }

            public override bool Equals(object obj)
            {
                return obj is MonsterRecord other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(MonsterId, _seed, _maxHealth);
            }

            public override string ToString()
            {
                return $"{MonsterId}:{_seed}:{_maxHealth.ToString("R", CultureInfo.InvariantCulture)}";
            }
        }
    }
}
