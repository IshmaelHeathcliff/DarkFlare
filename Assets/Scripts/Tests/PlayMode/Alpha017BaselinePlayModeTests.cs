using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha017BaselinePlayModeTests
    {
        readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        IArchitecture _architecture;

        [SetUp]
        public void SetUp()
        {
            GameArchitecture.Interface.Deinit();
            _architecture = GameArchitecture.Interface;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            _architecture?.Deinit();
            _architecture = null;
        }

        [UnityTest]
        public IEnumerator ProjectileTryHit_ConsumesSnapshotAndResolvesExactlyOnce()
        {
            CombatActor defender = CreateDefender();
            ProjectileSkillDefinition skill = CreateSkill();
            AttackSnapshot attack = new AttackSnapshot(
                "alpha017_projectile_attacker",
                ActorTeam.Player,
                "alpha017_projectile",
                string.Empty,
                FindSeed(roll => roll < 0.5f),
                new[] { new DamagePacket(DamageType.Physical, 10f, TagSet.Empty) },
                TagSet.Empty,
                CreateAttackerStats(),
                Array.Empty<ModifierInstance>());
            int resolvedEvents = 0;
            int damagedEvents = 0;
            _architecture.RegisterEvent<DamageResolvedEvent>(_ => resolvedEvents++);
            _architecture.RegisterEvent<ActorDamagedEvent>(_ => damagedEvents++);
            GameObject projectileObject = new GameObject("Alpha017BaselineProjectile");
            _objects.Add(projectileObject);
            ProjectileController projectile = projectileObject.AddComponent<ProjectileController>();
            projectile.Init(skill, Vector2.right, attack);

            bool firstHit = projectile.TryHit(defender, out DamageResult firstResult);
            bool secondHit = projectile.TryHit(defender, out DamageResult secondResult);

            Assert.IsTrue(firstHit);
            Assert.IsNotNull(firstResult);
            Assert.IsTrue(firstResult.DidDealDamage);
            Assert.IsFalse(secondHit);
            Assert.IsNull(secondResult);
            Assert.AreEqual(1, resolvedEvents);
            Assert.AreEqual(1, damagedEvents);

            yield return null;
            Assert.IsTrue(projectile == null);
        }

        CombatActor CreateDefender()
        {
            GameObject defenderObject = new GameObject("Alpha017BaselineDefender");
            _objects.Add(defenderObject);
            CombatActor defender = defenderObject.AddComponent<CombatActor>();
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.Evasion, 0f);
            defender.Configure("alpha017_projectile_defender", ActorTeam.Monster, 100f, stats, TagSet.Empty);
            return defender;
        }

        ProjectileSkillDefinition CreateSkill()
        {
            ProjectileSkillDefinition skill = ScriptableObject.CreateInstance<ProjectileSkillDefinition>();
            _objects.Add(skill);
            SetField(skill, "_id", "alpha017_projectile");
            SetField(skill, "_projectileRadius", 0.16f);
            SetField(skill, "_projectileSpeed", 1f);
            SetField(skill, "_projectileLifetime", 10f);
            return skill;
        }

        static StatBlock CreateAttackerStats()
        {
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.Accuracy, 100f);
            return stats;
        }

        static int FindSeed(Func<float, bool> predicate)
        {
            for (int seed = 0; seed < 100000; seed++)
            {
                if (predicate(AttackRandomRolls.FromRootSeed(seed).HitRoll))
                {
                    return seed;
                }
            }

            Assert.Fail("找不到满足命中随机条件的种子");
            return 0;
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }
    }
}
