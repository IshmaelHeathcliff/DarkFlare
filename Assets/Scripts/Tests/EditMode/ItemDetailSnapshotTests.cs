using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using DarkFlare.Tests;
using NUnit.Framework;
using UnityEngine;

public class ItemDetailSnapshotTests
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
    public void Factory_BuildsChineseDamageImplicitPrefixAndSuffixDetails()
    {
        StatDefinition damageStat = CreateStat("damage", "伤害");
        StatModifierDefinition implicitModifier = CreateModifier(
            damageStat,
            ModifierOperation.Flat,
            5f,
            5f);
        DamageRollDefinition damage = new DamageRollDefinition();
        SetField(damage, "_damageType", DamageType.Physical);
        SetField(damage, "_amountRange", new Vector2(20f, 40f));
        ItemBaseDefinition definition = CreateItemDefinition(
            "detail_weapon",
            "详情大剑",
            new List<DamageRollDefinition> { damage },
            new List<StatModifierDefinition> { implicitModifier });
        ItemInstance item = definition.CreateInstance("detail_weapon_01", 12, 7, ItemRarity.Rare);
        AffixDefinition prefix = CreateAffix(
            "猛烈",
            AffixType.Prefix,
            CreateModifier(damageStat, ModifierOperation.Increase, 20f, 20f));
        AffixDefinition suffix = CreateAffix(
            "余烬",
            AffixType.Suffix,
            CreateModifier(null, ModifierOperation.GainAsExtra, 10f, 10f, DamageType.Physical, DamageType.Fire));
        Assert.IsTrue(item.TryAddAffix(prefix.CreateInstance(new System.Random(1))));
        Assert.IsTrue(item.TryAddAffix(suffix.CreateInstance(new System.Random(2))));

        ItemDetailSnapshot detail = ItemDetailSnapshotFactory.Create(item);

        Assert.AreEqual("detail_weapon_01", detail.InstanceId);
        Assert.AreEqual("详情大剑", detail.DisplayName);
        Assert.AreEqual(ItemType.Weapon, detail.Type);
        Assert.AreEqual(ItemRarity.Rare, detail.Rarity);
        Assert.AreEqual(12, detail.ItemLevel);
        Assert.AreEqual(1, detail.Damages.Count);
        Assert.AreEqual("物理伤害 20–40", detail.Damages[0].DisplayText);
        Assert.AreEqual("伤害 +5", detail.ImplicitModifiers[0].DisplayText);
        Assert.AreEqual("猛烈", detail.Prefixes[0].DisplayName);
        Assert.AreEqual("伤害提高 20%", detail.Prefixes[0].ModifierSummary);
        Assert.AreEqual("余烬", detail.Suffixes[0].DisplayName);
        Assert.AreEqual("获得等同于物理伤害 10% 的额外火焰伤害", detail.Suffixes[0].ModifierSummary);
        StringAssert.DoesNotContain("damage", detail.Prefixes[0].ModifierSummary);
    }

    [Test]
    public void Queries_ExposeTheSameDetailForTheSameItem()
    {
        StatDefinition damageStat = CreateStat("damage", "伤害");
        ItemBaseDefinition definition = CreateItemDefinition(
            "shared_detail",
            "共用详情武器",
            new List<DamageRollDefinition>(),
            new List<StatModifierDefinition>());
        ItemInstance item = definition.CreateInstance("shared_detail_01", 3, 11, ItemRarity.Magic);
        AffixDefinition prefix = CreateAffix(
            "锐利",
            AffixType.Prefix,
            CreateModifier(damageStat, ModifierOperation.More, 15f, 15f));
        Assert.IsTrue(item.TryAddAffix(prefix.CreateInstance(new System.Random(3))));
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        Assert.IsTrue(inventory.TryAddItem(item));

        InventorySnapshot inventorySnapshot = _architecture.SendQuery(new GetInventorySnapshotQuery());
        ShopSnapshot shopSnapshot = _architecture.SendQuery(new GetShopSnapshotQuery());
        CraftingSnapshot craftingSnapshot = _architecture.SendQuery(new GetCraftingSnapshotQuery());

        ItemDetailSnapshot inventoryDetail = inventorySnapshot.Items[0].Detail;
        ItemDetailSnapshot shopDetail = shopSnapshot.PlayerItems[0].Detail;
        ItemDetailSnapshot craftingDetail = craftingSnapshot.Items[0].Detail;
        Assert.AreEqual(inventoryDetail.InstanceId, shopDetail.InstanceId);
        Assert.AreEqual(inventoryDetail.InstanceId, craftingDetail.InstanceId);
        Assert.AreEqual(inventoryDetail.DisplayName, shopDetail.DisplayName);
        Assert.AreEqual(inventoryDetail.Prefixes[0].ModifierSummary, shopDetail.Prefixes[0].ModifierSummary);
        Assert.AreEqual(inventoryDetail.Prefixes[0].ModifierSummary, craftingDetail.Prefixes[0].ModifierSummary);
    }

    [Test]
    public void Formatter_CoversSupportedModifierOperations()
    {
        Assert.AreEqual("伤害 +12", Format(ModifierOperation.Flat, 12f));
        Assert.AreEqual("伤害提高 12%", Format(ModifierOperation.Increase, 12f));
        Assert.AreEqual("伤害总增 12%", Format(ModifierOperation.More, 12f));
        Assert.AreEqual("伤害设为 12", Format(ModifierOperation.Override, 12f));
        Assert.AreEqual(
            "物理伤害的 12% 转化为火焰伤害",
            Format(ModifierOperation.Conversion, 12f));
        Assert.AreEqual(
            "获得等同于物理伤害 12% 的额外火焰伤害",
            Format(ModifierOperation.GainAsExtra, 12f));
    }

    string Format(ModifierOperation operation, float value)
    {
        ModifierInstance modifier = new ModifierInstance(
            "damage",
            operation,
            ModifierScope.GlobalActor,
            value,
            DamageType.Physical,
            DamageType.Fire,
            TagSet.Empty,
            TagSet.Empty);
        return ItemDetailFormatter.FormatModifier(modifier, "伤害", false);
    }

    ItemBaseDefinition CreateItemDefinition(
        string id,
        string displayName,
        List<DamageRollDefinition> damages,
        List<StatModifierDefinition> implicitModifiers)
    {
        ItemBaseDefinition definition = CreateScriptableObject<ItemBaseDefinition>();
        SetField(definition, "_id", id);
        SetField(definition, "_displayName", displayName);
        SetField(definition, "_itemType", ItemType.Weapon);
        SetField(definition, "_gridSize", new Vector2Int(2, 3));
        SetField(definition, "_baseValue", 10);
        SetField(definition, "_baseDamages", damages);
        SetField(definition, "_implicitModifiers", implicitModifiers);
        return definition;
    }

    StatDefinition CreateStat(string id, string displayName)
    {
        StatDefinition stat = CreateScriptableObject<StatDefinition>();
        SetField(stat, "_id", id);
        SetField(stat, "_displayName", displayName);
        return stat;
    }

    AffixDefinition CreateAffix(
        string displayName,
        AffixType type,
        StatModifierDefinition modifier)
    {
        AffixDefinition affix = CreateScriptableObject<AffixDefinition>();
        SetField(affix, "_displayName", displayName);
        SetField(affix, "_affixType", type);
        SetField(affix, "_weight", 100);
        SetField(affix, "_modifiers", new List<StatModifierDefinition> { modifier });
        return affix;
    }

    static StatModifierDefinition CreateModifier(
        StatDefinition stat,
        ModifierOperation operation,
        float minimumValue,
        float maximumValue,
        DamageType fromDamageType = DamageType.Physical,
        DamageType toDamageType = DamageType.Fire)
    {
        StatModifierDefinition modifier = new StatModifierDefinition();
        SetField(modifier, "_stat", stat);
        SetField(modifier, "_operation", operation);
        SetField(modifier, "_valueRange", new Vector2(minimumValue, maximumValue));
        SetField(modifier, "_fromDamageType", fromDamageType);
        SetField(modifier, "_toDamageType", toDamageType);
        return modifier;
    }

    T CreateScriptableObject<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        _objects.Add(value);
        return value;
    }

    static void SetField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(target, value);
    }
}
