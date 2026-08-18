using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class Alpha012DamageResolutionTests
    {
        readonly List<Object> _objects = new List<Object>();
        readonly GameArchitectureTestFixture _architectureFixture = new GameArchitectureTestFixture();

        IArchitecture _architecture;

        [SetUp]
        public void SetUp()
        {
            _architecture = _architectureFixture.Start();
        }

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
            _architectureFixture.Stop();
            _architecture = null;
        }

        [Test]
        public void AttackRandomRolls_ReplayRootSeed_AndExposeIndependentRanges()
        {
            AttackRandomRolls first = AttackRandomRolls.FromRootSeed(20260811);
            AttackRandomRolls replay = AttackRandomRolls.FromRootSeed(20260811);

            Assert.AreEqual(first.BaseDamageSeed, replay.BaseDamageSeed);
            Assert.AreEqual(first.HitRoll, replay.HitRoll);
            Assert.AreEqual(first.CriticalRoll, replay.CriticalRoll);
            Assert.That(first.HitRoll, Is.InRange(0f, 0.9999999f));
            Assert.That(first.CriticalRoll, Is.InRange(0f, 0.9999999f));
            Assert.AreNotEqual(first.BaseDamageSeed, AttackRandomRolls.FromRootSeed(20260812).BaseDamageSeed);
        }

        [Test]
        public void HitResolution_SeparatesHitEvadeAndNaturalMiss()
        {
            StatBlock attacker = CreateStats(100f, 0f, 0f, 0f);
            StatBlock defender = CreateStats(0f, 25f, 0f, 0f);

            HitResolution hit = HitResolutionCalculator.Resolve(attacker, defender, 0.79f, 0.5f);
            HitResolution evaded = HitResolutionCalculator.Resolve(attacker, defender, 0.85f, 0.5f);
            HitResolution missed = HitResolutionCalculator.Resolve(attacker, defender, 0.97f, 0.5f);

            Assert.AreEqual(0.8f, hit.HitChance, 0.0001f);
            Assert.AreEqual(HitOutcome.Hit, hit.Outcome);
            Assert.AreEqual(HitOutcome.Evaded, evaded.Outcome);
            Assert.AreEqual(HitOutcome.Missed, missed.Outcome);

            StatBlock noAccuracy = CreateStats(0f, 0f, 0f, 0f);
            Assert.AreEqual(
                HitOutcome.Hit,
                HitResolutionCalculator.Resolve(noAccuracy, new StatBlock(), 0.049f, 0f).Outcome);
            Assert.AreEqual(
                HitOutcome.Missed,
                HitResolutionCalculator.Resolve(noAccuracy, new StatBlock(), 0.05f, 0f).Outcome);
        }

        [Test]
        public void HitResolution_CriticalUsesOneAttackWideDecision()
        {
            StatBlock attacker = CreateStats(100f, 0f, 5f, 50f);
            StatBlock defender = new StatBlock();

            Assert.IsTrue(HitResolutionCalculator.Resolve(attacker, defender, 0.1f, 0.049f).IsCritical);
            Assert.IsFalse(HitResolutionCalculator.Resolve(attacker, defender, 0.1f, 0.05f).IsCritical);
            Assert.IsFalse(HitResolutionCalculator.Resolve(attacker, defender, 0.99f, 0f).IsCritical);
        }

        [Test]
        public void DamageCalculator_ExplainsArmorResistanceAndNoDamage()
        {
            StatBlock defender = new StatBlock();
            defender.SetValue(StatIds.Armor, 100f);
            defender.SetValue(StatIds.FireResistance, 50f);
            defender.SetValue(StatIds.ColdResistance, 75f);
            defender.SetValue(StatIds.LightningResistance, -25f);
            defender.SetValue(StatIds.ChaosResistance, 100f);
            DamageContext context = new DamageContext(
                "attacker",
                "defender",
                "mixed",
                string.Empty,
                1,
                new[]
                {
                    new DamagePacket(DamageType.Physical, 100f, TagSet.Empty),
                    new DamagePacket(DamageType.Fire, 100f, TagSet.Empty),
                    new DamagePacket(DamageType.Cold, 100f, TagSet.Empty),
                    new DamagePacket(DamageType.Lightning, 100f, TagSet.Empty),
                    new DamagePacket(DamageType.Chaos, 100f, TagSet.Empty),
                },
                TagSet.Empty,
                new StatBlock(),
                defender,
                new List<ModifierInstance>(),
                new List<ModifierInstance>());

            DamageResult result = DamageCalculator.Calculate(context);

            Assert.AreEqual(90.909f, result.DamageAfterDefense[DamageType.Physical], 0.01f);
            Assert.AreEqual(50f, result.DamageAfterDefense[DamageType.Fire], 0.001f);
            Assert.AreEqual(25f, result.DamageAfterDefense[DamageType.Cold], 0.001f);
            Assert.AreEqual(125f, result.DamageAfterDefense[DamageType.Lightning], 0.001f);
            Assert.AreEqual(25f, result.DamageAfterDefense[DamageType.Chaos], 0.001f);
            Assert.AreEqual(100f, result.Breakdowns[DamageType.Physical].Armor);
            Assert.AreEqual(0.0909f, result.Breakdowns[DamageType.Physical].ArmorReduction, 0.0001f);
            Assert.AreEqual(75f, result.Breakdowns[DamageType.Chaos].EffectiveResistance);
            StringAssert.Contains("Physical", result.ToDebugString());

            DamageResult noDamage = DamageCalculator.Calculate(new DamageContext(
                "attacker",
                "defender",
                "empty",
                string.Empty,
                2,
                new List<DamagePacket>(),
                TagSet.Empty,
                new StatBlock(),
                new StatBlock(),
                new List<ModifierInstance>(),
                new List<ModifierInstance>()));
            Assert.AreEqual(HitOutcome.NoDamage, noDamage.Outcome);
            Assert.IsTrue(noDamage.IsHit);
            Assert.IsFalse(noDamage.DidDealDamage);
        }

        [Test]
        public void CombatSystem_EmitsResolutionForEvade_AndDamageOnlyForHealthLoss()
        {
            CombatActor attacker = CreateActor(
                "resolution_attacker",
                ActorTeam.Player,
                CreateStats(100f, 0f, 0f, 0f));
            CombatActor defender = CreateActor(
                "resolution_defender",
                ActorTeam.Monster,
                CreateStats(100f, 100f, 0f, 0f));
            int resolutionEvents = 0;
            int damagedEvents = 0;
            _architecture.RegisterEvent<DamageResolvedEvent>(_ => resolutionEvents++);
            _architecture.RegisterEvent<ActorDamagedEvent>(_ => damagedEvents++);
            int evadeSeed = FindSeed(roll => roll >= 0.5f && roll < 0.95f);
            AttackSnapshot evade = AttackSnapshotFactory.CreateImmediate(
                attacker,
                "evade",
                string.Empty,
                new[] { new DamagePacket(DamageType.Physical, 10f, TagSet.Empty) },
                evadeSeed);
            float healthBefore = defender.CurrentHealth;

            DamageResult evadeResult = _architecture.GetSystem<CombatSystem>().ApplyDamage(evade, defender);

            Assert.AreEqual(HitOutcome.Evaded, evadeResult.Outcome);
            Assert.AreEqual(healthBefore, defender.CurrentHealth);
            Assert.AreEqual(1, resolutionEvents);
            Assert.AreEqual(0, damagedEvents);

            int hitSeed = FindSeed(roll => roll < 0.5f);
            AttackSnapshot hit = AttackSnapshotFactory.CreateImmediate(
                attacker,
                "hit",
                string.Empty,
                new[] { new DamagePacket(DamageType.Physical, 10f, TagSet.Empty) },
                hitSeed);
            DamageResult hitResult = _architecture.GetSystem<CombatSystem>().ApplyDamage(hit, defender);

            Assert.IsTrue(hitResult.DidDealDamage);
            Assert.Less(defender.CurrentHealth, healthBefore);
            Assert.AreEqual(2, resolutionEvents);
            Assert.AreEqual(1, damagedEvents);
        }

        [Test]
        public void ProjectileFactory_RejectsMissingSources_AndKeepsSkillSourceIndependent()
        {
            CombatActor attacker = CreateActor(
                "source_attacker",
                ActorTeam.Player,
                CreateStats(100f, 0f, 0f, 0f));
            ProjectileSkillDefinition weaponSkill = CreateSkill(
                ProjectileDamageSource.EquippedWeapon,
                null);
            EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();

            Assert.IsFalse(AttackSnapshotFactory.CanCreateProjectile(attacker, weaponSkill, equipment));
            Assert.IsNull(AttackSnapshotFactory.CreateProjectile(attacker, weaponSkill, equipment, 1));

            ProjectileSkillDefinition emptySkill = CreateSkill(ProjectileDamageSource.Skill, null);
            Assert.IsFalse(AttackSnapshotFactory.CanCreateProjectile(attacker, emptySkill, equipment));

            DamageRollDefinition skillDamage = CreateDamage(12f, 12f);
            ProjectileSkillDefinition configuredSkill = CreateSkill(
                ProjectileDamageSource.Skill,
                new List<DamageRollDefinition> { skillDamage });
            AttackSnapshot snapshot = AttackSnapshotFactory.CreateProjectile(
                attacker,
                configuredSkill,
                equipment,
                7);

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(12f, snapshot.BaseDamages[0].Amount);
            Assert.IsEmpty(snapshot.SourceItemId);
        }

        [Test]
        public void FireProjectileCommand_MissingWeaponDoesNotConsumeAttackSeedOrAttackEvent()
        {
            CombatActor attacker = CreateActor(
                "seed_attacker",
                ActorTeam.Player,
                CreateStats(100f, 0f, 0f, 0f));
            ProjectileSkillDefinition weaponSkill = CreateSkill(
                ProjectileDamageSource.EquippedWeapon,
                null);
            GameplayRandomSystem random = _architecture.GetSystem<GameplayRandomSystem>();
            random.Configure(true, 24680);
            int expectedFirstSeed = random.NextSeed(GameplayRandomChannel.PlayerAttack);
            random.Configure(true, 24680);
            int attackEvents = 0;
            _architecture.RegisterEvent<ActorAttackedEvent>(_ => attackEvents++);

            _architecture.SendCommand(new FireProjectileCommand(
                attacker,
                weaponSkill,
                Vector3.zero,
                Vector2.right));

            Assert.AreEqual(expectedFirstSeed, random.NextSeed(GameplayRandomChannel.PlayerAttack));
            Assert.AreEqual(0, attackEvents);
        }

        [Test]
        public void StartingWeaponTransaction_IsAtomicAndIdempotent()
        {
            CombatActor actor = CreateActor(
                "startup_player",
                ActorTeam.Player,
                CreateStats(100f, 0f, 0f, 0f));
            ItemBaseDefinition weapon = CreateItemDefinition(
                "startup_sword",
                ItemType.Weapon,
                EquipmentSlotMask.Weapon,
                new List<DamageRollDefinition> { CreateDamage(20f, 40f) });
            int equipmentEvents = 0;
            _architecture.RegisterEvent<EquipmentChangedEvent>(_ => equipmentEvents++);

            Assert.IsTrue(_architecture.SendCommand(new GrantStartingWeaponCommand(actor, weapon)));
            ItemInstance equipped = _architecture.GetModel<EquipmentModel>().GetWeapon(actor);
            Assert.IsNotNull(equipped);
            Assert.AreEqual(ItemRarity.Normal, equipped.Rarity);
            Assert.AreEqual(1, equipped.ItemLevel);
            Assert.AreEqual("starting_weapon_startup_player", equipped.InstanceId);
            Assert.IsFalse(_architecture.GetModel<InventoryModel>().Grid.Placements.ContainsKey(equipped));

            Assert.IsTrue(_architecture.SendCommand(new GrantStartingWeaponCommand(actor, weapon)));
            Assert.AreSame(equipped, _architecture.GetModel<EquipmentModel>().GetWeapon(actor));
            Assert.AreEqual(1, equipmentEvents);

            CombatActor invalidActor = CreateActor(
                "startup_invalid",
                ActorTeam.Player,
                CreateStats(100f, 0f, 0f, 0f));
            ItemBaseDefinition armor = CreateItemDefinition(
                "startup_armor",
                ItemType.Armor,
                EquipmentSlotMask.Armor,
                null);
            int itemCountBefore = _architecture.GetModel<InventoryModel>().Grid.Placements.Count;
            Assert.IsFalse(_architecture.SendCommand(new GrantStartingWeaponCommand(invalidActor, armor)));
            Assert.AreEqual(itemCountBefore, _architecture.GetModel<InventoryModel>().Grid.Placements.Count);
            Assert.IsNull(_architecture.GetModel<EquipmentModel>().GetWeapon(invalidActor));
        }

        [Test]
        public void Validators_EnforceDamageOwnershipAndCombatStatBounds()
        {
            ItemBaseDefinition nonWeapon = CreateItemDefinition(
                "damage_armor",
                ItemType.Armor,
                EquipmentSlotMask.Armor,
                new List<DamageRollDefinition> { CreateDamage(1f, 2f) });
            Assert.IsTrue(EquipmentConfigurationValidator.Validate(nonWeapon)
                .Exists(issue => issue.Contains("只有武器")));

            ItemBaseDefinition emptyWeapon = CreateItemDefinition(
                "empty_weapon",
                ItemType.Weapon,
                EquipmentSlotMask.Weapon,
                null);
            Assert.IsTrue(EquipmentConfigurationValidator.Validate(emptyWeapon)
                .Exists(issue => issue.Contains("基础伤害池不能为空")));

            ProjectileSkillDefinition weaponSkill = CreateSkill(
                ProjectileDamageSource.EquippedWeapon,
                new List<DamageRollDefinition> { CreateDamage(1f, 2f) });
            Assert.IsTrue(RandomizationConfigurationValidator.Validate(weaponSkill)
                .Exists(issue => issue.Contains("武器来源技能")));

            ProjectileSkillDefinition emptySkill = CreateSkill(ProjectileDamageSource.Skill, null);
            Assert.IsTrue(RandomizationConfigurationValidator.Validate(emptySkill)
                .Exists(issue => issue.Contains("基础伤害池不能为空")));
        }

        CombatActor CreateActor(string id, ActorTeam team, StatBlock stats)
        {
            GameObject gameObject = new GameObject(id);
            gameObject.SetActive(false);
            CombatActor actor = gameObject.AddComponent<CombatActor>();
            actor.Configure(id, team, 100f, stats, TagSet.Empty);
            _objects.Add(gameObject);
            _architecture.GetSystem<CombatSystem>().RegisterActor(actor);
            return actor;
        }

        ItemBaseDefinition CreateItemDefinition(
            string id,
            ItemType itemType,
            EquipmentSlotMask slots,
            List<DamageRollDefinition> damages)
        {
            ItemBaseDefinition definition = CreateScriptableObject<ItemBaseDefinition>();
            SetField(definition, "_id", id);
            SetField(definition, "_displayName", id);
            SetField(definition, "_itemType", itemType);
            SetField(definition, "_allowedEquipmentSlots", slots);
            SetField(definition, "_gridSize", Vector2Int.one);

            if (damages != null)
            {
                SetField(definition, "_baseDamages", damages);
            }

            return definition;
        }

        ProjectileSkillDefinition CreateSkill(
            ProjectileDamageSource damageSource,
            List<DamageRollDefinition> damages)
        {
            ProjectileSkillDefinition skill = CreateScriptableObject<ProjectileSkillDefinition>();
            SetField(skill, "_id", $"test_{damageSource}");
            SetField(skill, "_damageSource", damageSource);

            if (damages != null)
            {
                SetField(skill, "_baseDamages", damages);
            }

            return skill;
        }

        static DamageRollDefinition CreateDamage(float minimum, float maximum)
        {
            DamageRollDefinition damage = new DamageRollDefinition();
            SetField(damage, "_damageType", DamageType.Physical);
            SetField(damage, "_amountRange", new Vector2(minimum, maximum));
            return damage;
        }

        T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _objects.Add(value);
            return value;
        }

        static StatBlock CreateStats(
            float accuracy,
            float evasion,
            float criticalChance,
            float criticalDamage)
        {
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.Accuracy, accuracy);
            stats.SetValue(StatIds.Evasion, evasion);
            stats.SetValue(StatIds.CriticalChance, criticalChance);
            stats.SetValue(StatIds.CriticalDamage, criticalDamage);
            return stats;
        }

        static int FindSeed(System.Predicate<float> predicate)
        {
            for (int seed = 0; seed < 100000; seed++)
            {
                if (predicate(AttackRandomRolls.FromRootSeed(seed).HitRoll))
                {
                    return seed;
                }
            }

            Assert.Fail("未找到满足命中 Roll 条件的测试种子");
            return 0;
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"缺少字段 {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
