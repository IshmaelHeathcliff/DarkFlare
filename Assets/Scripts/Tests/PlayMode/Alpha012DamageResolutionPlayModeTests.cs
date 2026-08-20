using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha012DamageResolutionPlayModeTests
    {
        const float SetupTimeoutSeconds = 20f;

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
        public IEnumerator MainScene_EquipsStartingWeaponBeforeSpawnerAndSupportsUnequipRecovery()
        {
            yield return _fixture.EnterMain();
            PlayerController player = null;
            MonsterSpawner spawner = null;
            CombatPrototypeBootstrap bootstrap = null;
            float timeout = Time.realtimeSinceStartup + SetupTimeoutSeconds;

            while ((player == null
                    || spawner == null
                    || bootstrap == null
                    || !spawner.gameObject.activeInHierarchy)
                   && Time.realtimeSinceStartup < timeout)
            {
                player = Object.FindAnyObjectByType<PlayerController>();
                spawner = Object.FindAnyObjectByType<MonsterSpawner>();
                bootstrap = Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
                yield return null;
            }

            Assert.IsNotNull(player, "Main 场景未生成玩家");
            Assert.IsNotNull(spawner, "Main 场景未启用刷怪器");
            Assert.IsNotNull(bootstrap, "Main 场景缺少 CombatPrototypeBootstrap");
            Assert.IsNotNull(PlayerSkillField, "未找到玩家技能字段");
            ProjectileSkillDefinition skill = PlayerSkillField.GetValue(bootstrap) as ProjectileSkillDefinition;
            Assert.IsNotNull(skill, "Main 场景未配置玩家技能");
            Assert.AreEqual(ProjectileDamageSource.EquippedWeapon, skill.DamageSource);
            Assert.IsEmpty(skill.BaseDamages, "武器来源技能不应保留技能基础伤害");

            IArchitecture architecture = GameArchitectureProvider.RequireCurrent();
            EquipmentModel equipment = architecture.GetModel<EquipmentModel>();
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            ItemInstance startingWeapon = equipment.GetWeapon(player.Actor);
            Assert.IsNotNull(startingWeapon, "刷怪器启用时玩家尚未装备初始武器");
            Assert.AreEqual("great_sword", startingWeapon.BaseDefinition.Id);
            Assert.AreEqual(ItemRarity.Normal, startingWeapon.Rarity);
            Assert.AreEqual(1, startingWeapon.ItemLevel);
            Assert.AreEqual(100f, player.Actor.Stats.GetValue(StatIds.Accuracy), 0.001f);
            Assert.AreEqual(20f, player.Actor.Stats.GetValue(StatIds.Evasion), 0.001f);
            Assert.AreEqual(5f, player.Actor.Stats.GetValue(StatIds.CriticalChance), 0.001f);
            Assert.AreEqual(50f, player.Actor.Stats.GetValue(StatIds.CriticalDamage), 0.001f);

            player.enabled = false;
            spawner.enabled = false;
            Assert.IsTrue(architecture.SendCommand(new UnequipItemCommand(player.Actor, EquipmentSlot.Weapon)));
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(startingWeapon));
            Assert.IsFalse(AttackSnapshotFactory.CanCreateProjectile(player.Actor, skill, equipment));
            Assert.IsNull(AttackSnapshotFactory.CreateProjectile(player.Actor, skill, equipment, 11));

            Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(
                player.Actor,
                startingWeapon,
                EquipmentSlot.Weapon)));
            Assert.AreSame(startingWeapon, equipment.GetWeapon(player.Actor));
            Assert.IsTrue(AttackSnapshotFactory.CanCreateProjectile(player.Actor, skill, equipment));
            AttackSnapshot restored = AttackSnapshotFactory.CreateProjectile(
                player.Actor,
                skill,
                equipment,
                11);
            Assert.IsNotNull(restored);
            Assert.That(restored.BaseDamages[0].Amount, Is.InRange(20f, 40f));
        }
    }
}
