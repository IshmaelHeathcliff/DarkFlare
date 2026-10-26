using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed partial class Alpha014ResourceSystemPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_ProjectileGroupCommitsOnceAndPiercesDistinctTargets()
        {
            yield return _fixture.EnterMain();
            IArchitecture architecture = ApplicationHost.Current.CurrentSession.Architecture;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            var configured = EquipProjectileGroup(architecture, player);
            var targets = new List<CombatActor>();
            var casts = new List<PlayerSkillCastEvent>();
            int notifications = 0;
            IUnRegister castRegistration = architecture.RegisterEvent<PlayerSkillCastEvent>(casts.Add);
            IUnRegister attackRegistration = architecture.RegisterEvent<ActorAttackedEvent>(e => { if (e.Actor == player.Actor) { notifications++; } });
            SkillCastResult result = default;
            try
            {
                float mana = player.Actor.CurrentMana;
                result = CastProjectileGroup(architecture, player, configured.Item);
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(3, result.Projectiles.Count);
                Assert.AreEqual(mana - configured.Skill.ManaCost, player.Actor.CurrentMana, .001f);
                Assert.Greater(player.NormalSkillCooldownRemainingSeconds, 0);
                Assert.AreEqual(1, notifications);
                Assert.AreEqual(1, casts.Count);
                Assert.AreEqual(3, casts[0].Attacks.Count);
                Assert.AreSame(result.Projectile.Attack, casts[0].Attack);
                Assert.AreEqual(3, casts[0].Attacks.Select(a => a.RandomSeed).Distinct().Count());
                for (int i = 0; i < 3; i++)
                {
                    Vector2 heading = result.Projectiles[i].GetComponent<Rigidbody2D>().linearVelocity.normalized;
                    Assert.AreEqual(configured.Skill.GetProjectileAngle(i), Vector2.SignedAngle(Vector2.right, heading), .01f);
                }

                for (int i = 0; i < 4; i++)
                {
                    var targetObject = new GameObject("Projectile target " + i);
                    CombatActor target = targetObject.AddComponent<CombatActor>();
                    target.Configure("projectile_target_" + i, ActorTeam.Monster, 100000, new StatBlock(), TagSet.Empty);
                    targets.Add(target);
                    if (i < 3) { architecture.GetSystem<CombatSystem>().RegisterActor(target); }
                }
                foreach (ProjectileController projectile in result.Projectiles)
                {
                    Assert.IsFalse(projectile.TryHit(player.Actor, out _), "友方不消耗次数");
                    Assert.IsFalse(projectile.TryHit(targets[3], out _), "未登记目标不消耗次数");
                    IUnRegister reentry = architecture.RegisterEvent<DamageResolvedEvent>(e =>
                    {
                        if (e.Actor == targets[0]) { Assert.IsFalse(projectile.TryHit(targets[0], out _), "重入不得重复结算"); }
                    });
                    try { Assert.IsTrue(projectile.TryHit(targets[0], out _)); }
                    finally { reentry.UnRegister(); }
                    float health = targets[0].CurrentHealth;
                    Assert.IsFalse(projectile.TryHit(targets[0], out _));
                    Assert.AreEqual(health, targets[0].CurrentHealth);
                    Assert.IsTrue(projectile.TryHit(targets[1], out _));
                    Assert.IsFalse(projectile.TryHit(targets[2], out _), "第二个目标后停止");
                    Assert.IsFalse(projectile.gameObject.activeSelf);
                }
            }
            finally
            {
                castRegistration.UnRegister();
                attackRegistration.UnRegister();
                foreach (ProjectileController projectile in result.Projectiles)
                {
                    if (projectile != null) { architecture.GetUtility<SessionObjectRegistry>().Release(projectile.gameObject); }
                }
                foreach (CombatActor target in targets)
                {
                    architecture.GetSystem<CombatSystem>().UnregisterActor(target);
                    Object.Destroy(target.gameObject);
                }
                Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(player.Actor, configured.Original)));
                Assert.IsTrue(architecture.GetModel<InventoryModel>().RemoveItem(configured.Item));
                Object.Destroy(configured.Definition);
                Object.Destroy(configured.Skill);
            }
        }

        [UnityTest]
        public IEnumerator MainScene_PartialProjectilePreparationRollsBack()
        {
            yield return _fixture.EnterMain();
            IArchitecture architecture = ApplicationHost.Current.CurrentSession.Architecture;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            var configured = EquipProjectileGroup(architecture, player);
            SpawnSystem original = architecture.GetSystem<SpawnSystem>();
            int casts = 0;
            IUnRegister registration = architecture.RegisterEvent<PlayerSkillCastEvent>(_ => casts++);
            try
            {
                for (int failAt = 1; failAt <= 2; failAt++)
                {
                    var failure = new FailingProjectileSpawnSystem(failAt);
                    architecture.RegisterSystem<SpawnSystem>(failure);
                    float mana = player.Actor.CurrentMana;
                    int registered = architecture.GetUtility<SessionObjectRegistry>().Count;
                    LogAssert.Expect(LogType.Warning, new Regex("准备投射物失败:"));
                    SkillCastResult result = CastProjectileGroup(architecture, player, configured.Item);
                    Assert.AreEqual(SkillCastStatus.SpawnFailed, result.Status);
                    Assert.AreEqual(mana, player.Actor.CurrentMana);
                    Assert.AreEqual(0, player.NormalSkillCooldownRemainingSeconds);
                    Assert.AreEqual(0, casts);
                    Assert.AreEqual(registered, architecture.GetUtility<SessionObjectRegistry>().Count);
                    Assert.IsTrue(failure.Created.All(p => p == null || !p.activeSelf), "失败组须立即停用");
                }
            }
            finally
            {
                architecture.RegisterSystem(original);
                registration.UnRegister();
                Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(player.Actor, configured.Original)));
                Assert.IsTrue(architecture.GetModel<InventoryModel>().RemoveItem(configured.Item));
                Object.Destroy(configured.Definition);
                Object.Destroy(configured.Skill);
            }
        }

        static (ProjectileSkillDefinition Skill, ItemBaseDefinition Definition, ItemInstance Item, ItemInstance Original) EquipProjectileGroup(
            IArchitecture architecture, PlayerController player)
        {
            ProjectileSkillDefinition skill = Object.Instantiate(player.DefaultSkill);
            typeof(ProjectileSkillDefinition).GetField("_projectileCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, 3);
            typeof(ProjectileSkillDefinition).GetField("_spreadAngleDegrees", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, 20f);
            typeof(ProjectileSkillDefinition).GetField("_pierceCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skill, 1);
            ItemInstance original = architecture.GetModel<EquipmentModel>().GetWeapon(player.Actor);
            ItemBaseDefinition definition = Object.Instantiate(original.BaseDefinition);
            typeof(ItemBaseDefinition).GetField("_normalSkill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, skill);
            ItemInstance item = definition.CreateInstance(architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 12, ItemRarity.Normal);
            Assert.IsTrue(architecture.GetModel<InventoryModel>().TryAddItem(item));
            Assert.IsTrue(architecture.SendCommand(new EquipItemCommand(player.Actor, item)));
            return (skill, definition, item, original);
        }

        static SkillCastResult CastProjectileGroup(IArchitecture architecture, PlayerController player, ItemInstance item)
        {
            return architecture.SendCommand(new CastPlayerSkillCommand(player.Actor, -1, item.InstanceId,
                player.transform.position + Vector3.up * 10, Vector2.right));
        }

        sealed class FailingProjectileSpawnSystem : SpawnSystem
        {
            readonly int _failAt;
            public List<GameObject> Created { get; } = new List<GameObject>();
            public FailingProjectileSpawnSystem(int failAt) { _failAt = failAt; }
            protected override GameObject CreateProjectileInstance(GameObject prefab, Vector3 position, int index)
            {
                if (index == _failAt && _failAt == 1) { throw new System.InvalidOperationException("模拟本枚创建失败"); }
                GameObject instance = base.CreateProjectileInstance(prefab, position, index);
                Created.Add(instance);
                if (index == _failAt) { instance.GetComponent<ProjectileController>().enabled = false; }
                return instance;
            }
        }
    }
}
