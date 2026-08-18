using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using DarkFlare.Tests;
using NUnit.Framework;
using UnityEngine;

public class EquipmentPhase2Tests
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
    public void EquipmentLoadout_UsesFourSlots_AndRejectsDuplicateInstance()
    {
        EquipmentLoadout loadout = new EquipmentLoadout();
        ItemInstance weapon = CreateItem("loadout_weapon", ItemType.Weapon, EquipmentSlotMask.Weapon);
        ItemInstance armor = CreateItem("loadout_armor", ItemType.Armor, EquipmentSlotMask.Armor);
        ItemInstance ring = CreateItem("loadout_ring", ItemType.Accessory, EquipmentSlotMask.Rings);

        Assert.IsTrue(loadout.TrySet(EquipmentSlot.Weapon, weapon));
        Assert.IsTrue(loadout.TrySet(EquipmentSlot.Armor, armor));
        Assert.IsTrue(loadout.TrySet(EquipmentSlot.RingLeft, ring));
        Assert.IsFalse(loadout.TrySet(EquipmentSlot.RingRight, ring));
        Assert.IsFalse(loadout.TrySet(EquipmentSlot.Armor, weapon));
        Assert.AreSame(weapon, loadout.Get(EquipmentSlot.Weapon));
        Assert.AreSame(armor, loadout.Get(EquipmentSlot.Armor));
        Assert.AreSame(ring, loadout.Get(EquipmentSlot.RingLeft));
        Assert.IsNull(loadout.Get(EquipmentSlot.RingRight));
    }

    [Test]
    public void EquipmentSystem_EquipsReplacesAndUnequips_WithFinalStateVisibleToEvents()
    {
        CombatActor player = CreateActor("equipment_player", ActorTeam.Player, new StatBlock());
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();
        ItemInstance firstRing = CreateItem("first_ring", ItemType.Accessory, EquipmentSlotMask.Rings);
        ItemInstance secondRing = CreateItem("second_ring", ItemType.Accessory, EquipmentSlotMask.Rings);
        Assert.IsTrue(inventory.TryAddItem(firstRing));
        Assert.IsTrue(inventory.TryAddItem(secondRing));
        int equipmentEvents = 0;
        _architecture.RegisterEvent<EquipmentChangedEvent>(e =>
        {
            equipmentEvents++;
            Assert.AreSame(e.CurrentItem, equipment.GetItem(player, e.Slot));

            if (e.CurrentItem != null)
            {
                Assert.IsFalse(inventory.Grid.Placements.ContainsKey(e.CurrentItem));
            }

            if (e.PreviousItem != null)
            {
                Assert.IsTrue(inventory.Grid.Placements.ContainsKey(e.PreviousItem));
            }
        });

        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, firstRing, EquipmentSlot.RingLeft)));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, secondRing, EquipmentSlot.RingLeft)));
        Assert.IsTrue(_architecture.SendCommand(new UnequipItemCommand(player, EquipmentSlot.RingLeft)));

        Assert.IsNull(equipment.GetItem(player, EquipmentSlot.RingLeft));
        Assert.IsTrue(inventory.Grid.Placements.ContainsKey(firstRing));
        Assert.IsTrue(inventory.Grid.Placements.ContainsKey(secondRing));
        Assert.AreEqual(3, equipmentEvents);
    }

    [Test]
    public void EquipmentSystem_RollsBack_WhenReplacementOrUnequipCannotFit()
    {
        CombatActor player = CreateActor("rollback_player", ActorTeam.Player, new StatBlock());
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();
        ItemInstance largeWeapon = CreateItem(
            "large_weapon",
            ItemType.Weapon,
            EquipmentSlotMask.Weapon,
            new Vector2Int(2, 1));
        ItemInstance candidate = CreateItem("candidate", ItemType.Weapon, EquipmentSlotMask.Weapon);
        Assert.IsTrue(inventory.TryAddItem(largeWeapon));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, largeWeapon, EquipmentSlot.Weapon)));
        Assert.IsTrue(inventory.TryAddItem(candidate));

        for (int i = 0; i < 59; i++)
        {
            Assert.IsTrue(inventory.TryAddItem(CreateItem(
                $"replacement_blocker_{i}",
                ItemType.Material,
                EquipmentSlotMask.None)));
        }

        int eventCount = 0;
        _architecture.RegisterEvent<EquipmentChangedEvent>(_ => eventCount++);
        Assert.IsFalse(_architecture.SendCommand(new EquipItemCommand(player, candidate, EquipmentSlot.Weapon)));
        Assert.AreSame(largeWeapon, equipment.GetItem(player, EquipmentSlot.Weapon));
        Assert.IsTrue(inventory.Grid.Placements.ContainsKey(candidate));
        Assert.AreEqual(0, eventCount);

        _architecture = _architectureFixture.Restart();
        player = CreateActor("unequip_rollback_player", ActorTeam.Player, new StatBlock());
        inventory = _architecture.GetModel<InventoryModel>();
        equipment = _architecture.GetModel<EquipmentModel>();
        ItemInstance weapon = CreateItem("unequip_weapon", ItemType.Weapon, EquipmentSlotMask.Weapon);
        Assert.IsTrue(inventory.TryAddItem(weapon));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, weapon, EquipmentSlot.Weapon)));

        for (int i = 0; i < 60; i++)
        {
            Assert.IsTrue(inventory.TryAddItem(CreateItem(
                $"unequip_blocker_{i}",
                ItemType.Material,
                EquipmentSlotMask.None)));
        }

        Assert.IsFalse(_architecture.SendCommand(new UnequipItemCommand(player, EquipmentSlot.Weapon)));
        Assert.AreSame(weapon, equipment.GetItem(player, EquipmentSlot.Weapon));
        Assert.IsFalse(inventory.Grid.Placements.ContainsKey(weapon));
    }

    [Test]
    public void EquipmentEffects_AggregateAcrossSlots_AndKeepLocalItemOutOfActor()
    {
        StatBlock baseStats = new StatBlock();
        baseStats.SetValue(StatIds.Armor, 10f);
        CombatActor player = CreateActor("modifier_player", ActorTeam.Player, baseStats);
        ModifierInstance armorFlat = CreateModifier(StatIds.Armor, ModifierOperation.Flat, ModifierScope.GlobalActor, 50f);
        ModifierInstance armorIncrease = CreateModifier(StatIds.Armor, ModifierOperation.Increase, ModifierScope.GlobalActor, 20f);
        ModifierInstance localDamage = CreateModifier(StatIds.Damage, ModifierOperation.Increase, ModifierScope.LocalItem, 30f);
        ItemInstance armor = CreateItem(
            "modifier_armor",
            ItemType.Armor,
            EquipmentSlotMask.Armor,
            modifiers: new[] { armorFlat });
        ItemInstance ring = CreateItem(
            "modifier_ring",
            ItemType.Accessory,
            EquipmentSlotMask.Rings,
            modifiers: new[] { armorIncrease });
        ItemInstance weapon = CreateItem(
            "modifier_weapon",
            ItemType.Weapon,
            EquipmentSlotMask.Weapon,
            modifiers: new[] { localDamage });
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        Assert.IsTrue(inventory.TryAddItem(armor));
        Assert.IsTrue(inventory.TryAddItem(ring));
        Assert.IsTrue(inventory.TryAddItem(weapon));

        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, armor, EquipmentSlot.Armor)));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, ring, EquipmentSlot.RingLeft)));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, weapon, EquipmentSlot.Weapon)));

        Assert.AreEqual(72f, player.Stats.GetValue(StatIds.Armor), 0.001f);
        HudSnapshot equippedHud = _architecture.SendQuery(new GetHudSnapshotQuery());
        Assert.AreEqual(72f, equippedHud.Attributes.Armor, 0.001f);
        Assert.AreEqual(2, player.Modifiers.Count);
        CollectionAssert.DoesNotContain((System.Collections.ICollection)player.Modifiers, localDamage);

        Assert.IsTrue(_architecture.SendCommand(new UnequipItemCommand(player, EquipmentSlot.RingLeft)));
        Assert.AreEqual(60f, player.Stats.GetValue(StatIds.Armor), 0.001f);
        HudSnapshot unequippedHud = _architecture.SendQuery(new GetHudSnapshotQuery());
        Assert.AreEqual(60f, unequippedHud.Attributes.Armor, 0.001f);
        Assert.AreEqual(1, player.Modifiers.Count);
    }

    [Test]
    public void EquipmentMaxHealth_UpdatesActorAndHud_WhilePreservingHealthRatio()
    {
        CombatActor player = CreateActor("health_player", ActorTeam.Player, new StatBlock());
        DamageResult damage = new DamageResult(
            true,
            false,
            new Dictionary<DamageType, float> { { DamageType.Physical, 500f } },
            new Dictionary<DamageType, float> { { DamageType.Physical, 500f } });
        player.ReceiveDamage(damage);
        ModifierInstance maximumHealth = CreateModifier(
            StatIds.MaxHealth,
            ModifierOperation.Flat,
            ModifierScope.GlobalActor,
            200f);
        ItemInstance armor = CreateItem(
            "health_armor",
            ItemType.Armor,
            EquipmentSlotMask.Armor,
            modifiers: new[] { maximumHealth });
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        Assert.IsTrue(inventory.TryAddItem(armor));

        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, armor, EquipmentSlot.Armor)));
        HudSnapshot equippedHud = _architecture.SendQuery(new GetHudSnapshotQuery());

        Assert.AreEqual(1200f, player.Stats.GetValue(StatIds.MaxHealth), 0.001f);
        Assert.AreEqual(1200f, player.MaxHealth, 0.001f);
        Assert.AreEqual(600f, player.CurrentHealth, 0.001f);
        Assert.AreEqual(1200f, equippedHud.MaxHealth, 0.001f);
        Assert.AreEqual(600f, equippedHud.CurrentHealth, 0.001f);
        Assert.AreEqual(0.5f, equippedHud.HealthNormalized, 0.001f);

        Assert.IsTrue(_architecture.SendCommand(new UnequipItemCommand(player, EquipmentSlot.Armor)));
        HudSnapshot unequippedHud = _architecture.SendQuery(new GetHudSnapshotQuery());

        Assert.AreEqual(1000f, player.MaxHealth, 0.001f);
        Assert.AreEqual(500f, player.CurrentHealth, 0.001f);
        Assert.AreEqual(1000f, unequippedHud.MaxHealth, 0.001f);
        Assert.AreEqual(500f, unequippedHud.CurrentHealth, 0.001f);
        Assert.AreEqual(0.5f, unequippedHud.HealthNormalized, 0.001f);
    }

    [Test]
    public void AttackSnapshot_UsesWeaponDamageAndRemainsStableAfterUnequip()
    {
        CombatActor attacker = CreateActor("snapshot_attacker", ActorTeam.Player, new StatBlock());
        CombatActor defender = CreateActor("snapshot_defender", ActorTeam.Monster, new StatBlock());
        ModifierInstance localDamage = CreateModifier(
            StatIds.Damage,
            ModifierOperation.Increase,
            ModifierScope.LocalItem,
            50f);
        ItemInstance weapon = CreateWeaponWithDamage("snapshot_weapon", 20f, 40f, new[] { localDamage });
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        Assert.IsTrue(inventory.TryAddItem(weapon));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(attacker, weapon, EquipmentSlot.Weapon)));
        ProjectileSkillDefinition skill = CreateSkill(ProjectileDamageSource.EquippedWeapon);

        AttackSnapshot snapshot = AttackSnapshotFactory.CreateProjectile(
            attacker,
            skill,
            _architecture.GetModel<EquipmentModel>(),
            2718);
        DamageResult expected = Calculate(snapshot, defender);
        Assert.AreEqual(weapon.InstanceId, snapshot.SourceItemId);
        Assert.That(snapshot.BaseDamages[0].Amount, Is.InRange(20f, 40f));
        Assert.Greater(expected.TotalDamage, snapshot.BaseDamages[0].Amount);

        Assert.IsTrue(_architecture.SendCommand(new UnequipItemCommand(attacker, EquipmentSlot.Weapon)));
        attacker.SetModifiers(new[]
        {
            CreateModifier(StatIds.Damage, ModifierOperation.Increase, ModifierScope.GlobalActor, 999f),
        });
        DamageResult actual = _architecture.GetSystem<CombatSystem>().ApplyDamage(snapshot, defender);

        Assert.AreEqual(expected.TotalDamage, actual.TotalDamage, 0.001f);
    }

    [Test]
    public void AttackSnapshot_RejectsMissingWeapon_AndExcludesLocalItemFromSkillSource()
    {
        CombatActor attacker = CreateActor("fallback_attacker", ActorTeam.Player, new StatBlock());
        ProjectileSkillDefinition weaponSkill = CreateSkill(ProjectileDamageSource.EquippedWeapon);
        AttackSnapshot missingWeapon = AttackSnapshotFactory.CreateProjectile(
            attacker,
            weaponSkill,
            _architecture.GetModel<EquipmentModel>(),
            7);
        Assert.IsNull(missingWeapon);

        ModifierInstance localDamage = CreateModifier(
            StatIds.Damage,
            ModifierOperation.Increase,
            ModifierScope.LocalItem,
            100f);
        ItemInstance weapon = CreateWeaponWithDamage("skill_source_weapon", 30f, 30f, new[] { localDamage });
        Assert.IsTrue(_architecture.GetModel<InventoryModel>().TryAddItem(weapon));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(attacker, weapon, EquipmentSlot.Weapon)));
        ProjectileSkillDefinition skillSource = CreateSkill(ProjectileDamageSource.Skill);
        AttackSnapshot skillSnapshot = AttackSnapshotFactory.CreateProjectile(
            attacker,
            skillSource,
            _architecture.GetModel<EquipmentModel>(),
            7);

        Assert.AreEqual(12f, skillSnapshot.BaseDamages[0].Amount);
        Assert.IsEmpty(skillSnapshot.SourceItemId);
        Assert.AreEqual(0, skillSnapshot.AttackerModifiers.Count);
    }

    [Test]
    public void EquippedArmor_EntersDefenseSnapshotOnce()
    {
        CombatActor attacker = CreateActor("armor_attacker", ActorTeam.Player, new StatBlock());
        CombatActor defender = CreateActor("armor_defender", ActorTeam.Monster, new StatBlock());
        ModifierInstance armorModifier = CreateModifier(
            StatIds.Armor,
            ModifierOperation.Flat,
            ModifierScope.GlobalActor,
            100f);
        ItemInstance armor = CreateItem(
            "defense_armor",
            ItemType.Armor,
            EquipmentSlotMask.Armor,
            modifiers: new[] { armorModifier });
        Assert.IsTrue(_architecture.GetModel<InventoryModel>().TryAddItem(armor));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(defender, armor, EquipmentSlot.Armor)));
        AttackSnapshot attack = AttackSnapshotFactory.CreateImmediate(
            attacker,
            "armor_test",
            string.Empty,
            new[] { new DamagePacket(DamageType.Physical, 100f, TagSet.Empty) },
            TagSet.Empty,
            1);

        DamageResult result = Calculate(attack, defender);

        Assert.AreEqual(100f, defender.Stats.GetValue(StatIds.Armor));
        Assert.AreEqual(90.909f, result.TotalDamage, 0.01f);
    }

    [Test]
    public void AttackSnapshot_DeepCopiesMutableInputs()
    {
        StatBlock stats = new StatBlock();
        stats.SetValue(StatIds.PhysicalDamage, 5f);
        List<DamagePacket> packets = new List<DamagePacket>
        {
            new DamagePacket(DamageType.Physical, 10f, new TagSet(new[] { "projectile" })),
        };
        List<ModifierInstance> modifiers = new List<ModifierInstance>
        {
            CreateModifier(StatIds.Damage, ModifierOperation.Increase, ModifierScope.GlobalActor, 20f),
        };
        AttackSnapshot snapshot = new AttackSnapshot(
            "copy_attacker",
            ActorTeam.Player,
            "copy_skill",
            "copy_weapon",
            9,
            packets,
            new TagSet(new[] { "attack" }),
            stats,
            modifiers);

        packets.Clear();
        modifiers.Clear();
        stats.SetValue(StatIds.PhysicalDamage, 500f);

        Assert.AreEqual(1, snapshot.BaseDamages.Count);
        Assert.AreEqual(1, snapshot.AttackerModifiers.Count);
        Assert.AreEqual(5f, snapshot.AttackerStats.GetValue(StatIds.PhysicalDamage));
        Assert.IsTrue(snapshot.ContextTags.Contains("attack"));
    }

    [Test]
    public void EquipmentConfigurationValidator_RejectsMismatchedSlotsAndUnsupportedStatIds()
    {
        ItemBaseDefinition invalidArmor = CreateDefinition(
            "invalid_armor",
            ItemType.Armor,
            EquipmentSlotMask.Weapon);
        StatDefinition unsupportedStat = CreateScriptableObject<StatDefinition>();
        SetField(unsupportedStat, "_id", "Defence");
        StatModifierDefinition modifier = new StatModifierDefinition();
        SetField(modifier, "_stat", unsupportedStat);
        SetField(modifier, "_operation", ModifierOperation.Flat);
        SetField(invalidArmor, "_implicitModifiers", new List<StatModifierDefinition> { modifier });

        List<string> issues = EquipmentConfigurationValidator.Validate(invalidArmor);

        Assert.AreEqual(2, issues.Count);
        StringAssert.Contains("护甲", issues[0]);
        StringAssert.Contains("属性 ID", issues[1]);

        AffixDefinition invalidAffix = CreateScriptableObject<AffixDefinition>();
        SetField(invalidAffix, "_id", "invalid_affix");
        SetField(invalidAffix, "_modifiers", new List<StatModifierDefinition> { modifier });
        List<string> affixIssues = EquipmentConfigurationValidator.Validate(invalidAffix);

        Assert.AreEqual(1, affixIssues.Count);
        StringAssert.Contains("属性 ID", affixIssues[0]);

        SetField(unsupportedStat, "_id", StatIds.MaxHealth);
        Assert.IsEmpty(EquipmentConfigurationValidator.Validate(invalidAffix));
    }

    [Test]
    public void InventorySnapshot_ExposesFourSlotsAndSafeComparison()
    {
        CombatActor player = CreateActor("inventory_snapshot_player", ActorTeam.Player, new StatBlock());
        ItemInstance current = CreateWeaponWithDamage("comparison_current", 10f, 20f);
        ItemInstance candidate = CreateWeaponWithDamage("comparison_candidate", 20f, 30f);
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        Assert.IsTrue(inventory.TryAddItem(current));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, current, EquipmentSlot.Weapon)));
        Assert.IsTrue(inventory.TryAddItem(candidate));

        InventorySnapshot snapshot = _architecture.SendQuery(new GetInventorySnapshotQuery());
        EquipmentComparisonSnapshot comparison = EquipmentComparisonFactory.Create(
            EquipmentSlot.Weapon,
            current,
            candidate);

        Assert.AreEqual(4, snapshot.EquipmentSlots.Count);
        Assert.AreSame(current, snapshot.EquipmentSlots[0].Item);
        Assert.AreEqual(EquipmentSlotMask.Weapon, snapshot.Items[0].CompatibleSlots);
        Assert.IsNotEmpty(comparison.Lines);
        StringAssert.Contains("基础伤害", comparison.Lines[0]);
        StringAssert.Contains("+10", comparison.Lines[0]);
    }

    DamageResult Calculate(AttackSnapshot attack, CombatActor defender)
    {
        HitResolution resolution = HitResolutionCalculator.Resolve(
            attack.AttackerStats,
            defender.Stats,
            attack.RandomRolls);
        DamageContext context = new DamageContext(
            attack.AttackerId,
            defender.ActorId,
            attack.SkillId,
            attack.SourceItemId,
            attack.RandomSeed,
            attack.BaseDamages,
            attack.TagContext,
            attack.AttackerStats,
            defender.Stats,
            attack.AttackerModifiers,
            defender.Modifiers,
            resolution);
        return DamageCalculator.Calculate(context);
    }

    CombatActor CreateActor(string id, ActorTeam team, StatBlock stats)
    {
        if (stats.GetValue(StatIds.Accuracy) <= 0f)
        {
            stats.SetValue(StatIds.Accuracy, 100f);
        }

        GameObject gameObject = new GameObject(id);
        gameObject.SetActive(false);
        CombatActor actor = gameObject.AddComponent<CombatActor>();
        actor.Configure(id, team, 1000f, stats, TagSet.Empty);
        _objects.Add(gameObject);
        _architecture.GetSystem<CombatSystem>().RegisterActor(actor);
        return actor;
    }

    ItemInstance CreateWeaponWithDamage(
        string id,
        float minimum,
        float maximum,
        IEnumerable<ModifierInstance> modifiers = null)
    {
        DamageRollDefinition damage = new DamageRollDefinition();
        SetField(damage, "_damageType", DamageType.Physical);
        SetField(damage, "_amountRange", new Vector2(minimum, maximum));
        ItemBaseDefinition definition = CreateDefinition(id, ItemType.Weapon, EquipmentSlotMask.Weapon);
        SetField(definition, "_baseDamages", new List<DamageRollDefinition> { damage });
        return new ItemInstance(id, definition, ItemRarity.Normal, 1, 1, modifiers ?? new List<ModifierInstance>());
    }

    ItemInstance CreateItem(
        string id,
        ItemType type,
        EquipmentSlotMask slots,
        Vector2Int? gridSize = null,
        IEnumerable<ModifierInstance> modifiers = null)
    {
        ItemBaseDefinition definition = CreateDefinition(id, type, slots, gridSize);
        return new ItemInstance(id, definition, ItemRarity.Normal, 1, 1, modifiers ?? new List<ModifierInstance>());
    }

    ItemBaseDefinition CreateDefinition(
        string id,
        ItemType type,
        EquipmentSlotMask slots,
        Vector2Int? gridSize = null)
    {
        ItemBaseDefinition definition = CreateScriptableObject<ItemBaseDefinition>();
        SetField(definition, "_id", id);
        SetField(definition, "_displayName", id);
        SetField(definition, "_itemType", type);
        SetField(definition, "_allowedEquipmentSlots", slots);
        SetField(definition, "_gridSize", gridSize ?? Vector2Int.one);
        return definition;
    }

    ProjectileSkillDefinition CreateSkill(ProjectileDamageSource source)
    {
        ProjectileSkillDefinition skill = CreateScriptableObject<ProjectileSkillDefinition>();
        SetField(skill, "_id", "test_projectile");
        SetField(skill, "_damageSource", source);

        if (source == ProjectileDamageSource.Skill)
        {
            DamageRollDefinition damage = new DamageRollDefinition();
            SetField(damage, "_damageType", DamageType.Physical);
            SetField(damage, "_amountRange", new Vector2(12f, 12f));
            SetField(skill, "_baseDamages", new List<DamageRollDefinition> { damage });
        }

        return skill;
    }

    static ModifierInstance CreateModifier(
        string statId,
        ModifierOperation operation,
        ModifierScope scope,
        float value)
    {
        return new ModifierInstance(
            statId,
            operation,
            scope,
            value,
            DamageType.Physical,
            DamageType.Physical,
            TagSet.Empty,
            TagSet.Empty);
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
