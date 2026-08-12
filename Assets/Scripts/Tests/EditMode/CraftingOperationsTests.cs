using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class CraftingOperationsTests
{
    readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            Object.DestroyImmediate(_created[i]);
        }

        _created.Clear();
    }

    [TestCase(ItemRarity.Normal, 0, 0, 0, 0)]
    [TestCase(ItemRarity.Magic, 1, 1, 1, 2)]
    [TestCase(ItemRarity.Rare, 3, 3, 2, 6)]
    [TestCase(ItemRarity.Unique, 3, 3, 4, 6)]
    public void RarityRules_AreGlobalAndStable(
        ItemRarity rarity,
        int maxPrefixes,
        int maxSuffixes,
        int minimumTotal,
        int maximumTotal)
    {
        ItemAffixLimits limits = ItemRarityRules.GetLimits(rarity);

        Assert.AreEqual(maxPrefixes, limits.MaxPrefixCount);
        Assert.AreEqual(maxSuffixes, limits.MaxSuffixCount);
        Assert.AreEqual(minimumTotal, limits.MinimumTotalCount);
        Assert.AreEqual(maximumTotal, limits.MaximumTotalCount);
    }

    [Test]
    public void UpgradeRarity_AdvancesSequentiallyAndMeetsMinimums()
    {
        ItemInstance item = CreateItem(ItemRarity.Normal);
        List<AffixDefinition> pool = CreatePool();

        CraftingResult magic = CraftingOperations.TryCraft(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            item,
            pool,
            10);
        Assert.IsTrue(magic.Succeeded);
        AssertNormalState(item, ItemRarity.Magic);
        int magicCount = CountAffixes(item);

        CraftingResult rare = CraftingOperations.TryCraft(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            item,
            pool,
            11);
        Assert.IsTrue(rare.Succeeded);
        Assert.AreEqual(magicCount + 1, CountAffixes(item));
        AssertNormalState(item, ItemRarity.Rare);

        CraftingResult unique = CraftingOperations.TryCraft(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            item,
            pool,
            12);
        Assert.IsTrue(unique.Succeeded);
        AssertNormalState(item, ItemRarity.Unique);
        Assert.GreaterOrEqual(CountAffixes(item), 4);

        CraftingResult maximum = CraftingOperations.TryCraft(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            item,
            pool,
            13);
        Assert.IsFalse(maximum.Succeeded);
        Assert.AreEqual(CraftingFailureReason.MaximumRarity, maximum.FailureReason);
    }

    [Test]
    public void ResetToNormal_RemovesAllAffixesAndRarity()
    {
        ItemInstance item = CreateItem(ItemRarity.Rare);
        Add(item, CreateAffix(AffixType.Prefix, "p", 10f, 20f), 1);
        Add(item, CreateAffix(AffixType.Suffix, "s", 10f, 20f), 2);

        CraftingResult result = CraftingOperations.TryCraft(
            CraftOperation.ResetToNormal,
            CraftingAffixScope.Any,
            item,
            new List<AffixDefinition>(),
            20);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ItemRarity.Normal, item.Rarity);
        Assert.AreEqual(0, CountAffixes(item));
    }

    [Test]
    public void RerollPrefix_PreservesSuffixAndBuildsNormalTotal()
    {
        ItemInstance item = CreateItem(ItemRarity.Rare);
        Add(item, CreateAffix(AffixType.Prefix, "old_prefix", 10f, 20f), 1);
        AffixInstance preservedSuffix = Add(
            item,
            CreateAffix(AffixType.Suffix, "old_suffix", 10f, 20f),
            2);

        CraftingResult result = CraftingOperations.TryCraft(
            CraftOperation.RerollAffixes,
            CraftingAffixScope.Prefix,
            item,
            CreatePool(),
            30);

        Assert.IsTrue(result.Succeeded);
        Assert.AreSame(preservedSuffix, item.Suffixes[0]);
        Assert.GreaterOrEqual(item.Prefixes.Count, 1);
        AssertNormalState(item, ItemRarity.Rare);
    }

    [Test]
    public void AddAndRemove_RespectScopeCapacityAndRemovalMayBreakMinimum()
    {
        ItemInstance item = CreateItem(ItemRarity.Magic);
        List<AffixDefinition> pool = CreatePool();

        CraftingResult addPrefix = CraftingOperations.TryCraft(
            CraftOperation.AddAffix,
            CraftingAffixScope.Prefix,
            item,
            pool,
            40);
        Assert.IsTrue(addPrefix.Succeeded);
        Assert.AreEqual(1, item.Prefixes.Count);

        CraftingResult fullPrefix = CraftingOperations.TryCraft(
            CraftOperation.AddAffix,
            CraftingAffixScope.Prefix,
            item,
            pool,
            41);
        Assert.IsFalse(fullPrefix.Succeeded);
        Assert.AreEqual(CraftingFailureReason.NoCapacity, fullPrefix.FailureReason);

        CraftingResult removePrefix = CraftingOperations.TryCraft(
            CraftOperation.RemoveAffix,
            CraftingAffixScope.Prefix,
            item,
            pool,
            42);
        Assert.IsTrue(removePrefix.Succeeded);
        Assert.AreEqual(ItemRarity.Magic, item.Rarity);
        Assert.AreEqual(0, CountAffixes(item));
    }

    [Test]
    public void RerollValues_KeepsDefinitionAndMayMoveEitherDirection()
    {
        ItemInstance item = CreateItem(ItemRarity.Magic);
        AffixDefinition definition = CreateAffix(AffixType.Prefix, "variable", 1f, 100f);
        AffixInstance previous = Add(item, definition, 5);

        CraftingResult result = CraftingOperations.TryCraft(
            CraftOperation.RerollAffixValues,
            CraftingAffixScope.Prefix,
            item,
            new List<AffixDefinition> { definition },
            50);

        Assert.IsTrue(result.Succeeded);
        Assert.AreSame(previous, result.PreviousAffectedAffix);
        Assert.AreNotSame(previous, result.CurrentAffectedAffix);
        Assert.AreSame(definition, item.Prefixes[0].Definition);
    }

    [Test]
    public void FailedCraft_IsAtomic()
    {
        ItemInstance item = CreateItem(ItemRarity.Rare);
        AffixInstance original = Add(
            item,
            CreateAffix(AffixType.Suffix, "original", 10f, 20f),
            1);
        int revision = item.Revision;

        CraftingResult result = CraftingOperations.TryCraft(
            CraftOperation.AddAffix,
            CraftingAffixScope.Prefix,
            item,
            new List<AffixDefinition>(),
            60);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(CraftingFailureReason.NoLegalAffix, result.FailureReason);
        Assert.AreEqual(revision, item.Revision);
        Assert.AreSame(original, item.Suffixes[0]);
    }

    [Test]
    public void SameRootSeed_ProducesSameDefinitionsAndValues()
    {
        ItemInstance first = CreateItem(ItemRarity.Normal, "first");
        ItemInstance second = CreateItem(ItemRarity.Normal, "second");
        List<AffixDefinition> pool = CreatePool();

        CraftingResult firstResult = CraftingOperations.TryCraft(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            first,
            pool,
            777);
        CraftingResult secondResult = CraftingOperations.TryCraft(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            second,
            pool,
            777);

        Assert.IsTrue(firstResult.Succeeded);
        Assert.IsTrue(secondResult.Succeeded);
        CollectionAssert.AreEqual(Describe(first), Describe(second));
    }

    [Test]
    public void CraftingDefinition_PreciseScopesCostExactlyThreeTimesAny()
    {
        CraftingDefinition definition = ScriptableObject.CreateInstance<CraftingDefinition>();
        _created.Add(definition);

        Assert.AreEqual(15, definition.GetCost(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            ItemRarity.Normal));
        Assert.AreEqual(60, definition.GetCost(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            ItemRarity.Magic));
        Assert.AreEqual(160, definition.GetCost(
            CraftOperation.UpgradeRarity,
            CraftingAffixScope.Any,
            ItemRarity.Rare));
        Assert.AreEqual(40, definition.GetCost(
            CraftOperation.RerollAffixes,
            CraftingAffixScope.Any,
            ItemRarity.Rare));
        Assert.AreEqual(120, definition.GetCost(
            CraftOperation.RerollAffixes,
            CraftingAffixScope.Prefix,
            ItemRarity.Rare));
        Assert.AreEqual(240, definition.GetCost(
            CraftOperation.RerollAffixValues,
            CraftingAffixScope.Suffix,
            ItemRarity.Rare));
    }

    ItemInstance CreateItem(ItemRarity rarity, string id = "item")
    {
        ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        SetField(definition, "_id", id);
        SetField(definition, "_displayName", id);
        _created.Add(definition);
        return new ItemInstance(id, definition, rarity, 10, 0, new List<ModifierInstance>());
    }

    List<AffixDefinition> CreatePool()
    {
        List<AffixDefinition> pool = new List<AffixDefinition>();

        for (int i = 0; i < 4; i++)
        {
            pool.Add(CreateAffix(AffixType.Prefix, $"prefix_{i}", 1f + i, 20f + i));
            pool.Add(CreateAffix(AffixType.Suffix, $"suffix_{i}", 1f + i, 20f + i));
        }

        return pool;
    }

    AffixDefinition CreateAffix(
        AffixType type,
        string id,
        float valueMin,
        float valueMax)
    {
        StatModifierDefinition modifier = new StatModifierDefinition();
        SetField(modifier, "_operation", ModifierOperation.Increase);
        SetField(modifier, "_valueRange", new Vector2(valueMin, valueMax));
        AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
        SetField(affix, "_id", id);
        SetField(affix, "_displayName", id);
        SetField(affix, "_affixType", type);
        SetField(affix, "_groupId", id);
        SetField(affix, "_minItemLevel", 1);
        SetField(affix, "_weight", 100);
        SetField(affix, "_modifiers", new List<StatModifierDefinition> { modifier });
        _created.Add(affix);
        return affix;
    }

    static AffixInstance Add(ItemInstance item, AffixDefinition definition, int seed)
    {
        AffixInstance affix = definition.CreateInstance(new System.Random(seed));
        Assert.IsTrue(item.TryAddAffix(affix));
        return affix;
    }

    static int CountAffixes(ItemInstance item)
    {
        return item.Prefixes.Count + item.Suffixes.Count;
    }

    static void AssertNormalState(ItemInstance item, ItemRarity rarity)
    {
        ItemAffixLimits limits = ItemRarityRules.GetLimits(rarity);
        Assert.AreEqual(rarity, item.Rarity);
        Assert.LessOrEqual(item.Prefixes.Count, limits.MaxPrefixCount);
        Assert.LessOrEqual(item.Suffixes.Count, limits.MaxSuffixCount);
        Assert.That(CountAffixes(item), Is.InRange(limits.MinimumTotalCount, limits.MaximumTotalCount));
    }

    static List<string> Describe(ItemInstance item)
    {
        return item.Prefixes
            .Concat(item.Suffixes)
            .Select(affix => $"{affix.Definition.Id}:{string.Join(",", affix.Modifiers.Select(modifier => modifier.Value.ToString("R")))}")
            .ToList();
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }
}
