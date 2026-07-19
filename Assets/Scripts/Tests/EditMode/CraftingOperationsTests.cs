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
        foreach (Object obj in _created)
        {
            Object.DestroyImmediate(obj);
        }

        _created.Clear();
    }

    [Test]
    public void AddRandomAffix_AddsOne_ThenFailsWhenFull()
    {
        ItemInstance item = CreateItem(maxPrefix: 2);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(10f, 30f) };
        System.Random random = new System.Random(1);

        Assert.IsTrue(CraftingOperations.AddRandomAffix(item, pool, random));
        Assert.AreEqual(1, item.Prefixes.Count);

        Assert.IsTrue(CraftingOperations.AddRandomAffix(item, pool, random));
        Assert.AreEqual(2, item.Prefixes.Count);

        Assert.IsFalse(CraftingOperations.AddRandomAffix(item, pool, random));
        Assert.AreEqual(2, item.Prefixes.Count);
    }

    [Test]
    public void RerollAllAffixes_KeepsCount_AndFailsOnEmptyItem()
    {
        ItemInstance item = CreateItem(maxPrefix: 3);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(10f, 30f) };
        System.Random random = new System.Random(2);

        Assert.IsFalse(CraftingOperations.RerollAllAffixes(item, pool, random));

        CraftingOperations.AddRandomAffix(item, pool, random);
        CraftingOperations.AddRandomAffix(item, pool, random);
        int before = item.Prefixes.Count + item.Suffixes.Count;

        Assert.IsTrue(CraftingOperations.RerollAllAffixes(item, pool, random));
        Assert.AreEqual(before, item.Prefixes.Count + item.Suffixes.Count);
    }

    [Test]
    public void RemoveAndRerollAffix_ReplacesTarget_KeepsCount()
    {
        ItemInstance item = CreateItem(maxPrefix: 3);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(10f, 30f) };
        System.Random random = new System.Random(3);

        CraftingOperations.AddRandomAffix(item, pool, random);
        AffixInstance target = item.Prefixes[0];

        Assert.IsTrue(CraftingOperations.RemoveAndRerollAffix(item, target, pool, random));
        Assert.AreEqual(1, item.Prefixes.Count);
        Assert.IsFalse(item.Prefixes.Contains(target));
    }

    [Test]
    public void RerollAllAffixes_RollsBack_WhenPoolCannotReplaceAffixes()
    {
        ItemInstance item = CreateItem(maxPrefix: 3);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(10f, 30f) };
        System.Random random = new System.Random(31);
        Assert.IsTrue(CraftingOperations.AddRandomAffix(item, pool, random));
        AffixInstance original = item.Prefixes[0];

        bool rerolled = CraftingOperations.RerollAllAffixes(
            item,
            new List<AffixDefinition>(),
            random);

        Assert.IsFalse(rerolled);
        Assert.AreEqual(1, item.Prefixes.Count);
        Assert.AreSame(original, item.Prefixes[0]);
    }

    [Test]
    public void RemoveAndRerollAffix_RollsBack_WhenPoolCannotReplaceTarget()
    {
        ItemInstance item = CreateItem(maxPrefix: 3);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(10f, 30f) };
        System.Random random = new System.Random(32);
        Assert.IsTrue(CraftingOperations.AddRandomAffix(item, pool, random));
        AffixInstance original = item.Prefixes[0];

        bool rerolled = CraftingOperations.RemoveAndRerollAffix(
            item,
            original,
            new List<AffixDefinition>(),
            random);

        Assert.IsFalse(rerolled);
        Assert.AreEqual(1, item.Prefixes.Count);
        Assert.AreSame(original, item.Prefixes[0]);
    }

    [Test]
    public void RemoveAndRerollAffix_ReturnsFalse_WhenTargetNotOnItem()
    {
        ItemInstance item = CreateItem(maxPrefix: 3);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(10f, 30f) };
        AffixInstance stray = CreatePrefixAffix(10f, 30f).CreateInstance(new System.Random(9));

        Assert.IsFalse(CraftingOperations.RemoveAndRerollAffix(item, stray, pool, new System.Random(4)));
    }

    [Test]
    public void UpgradeAffix_NeverLowersValue()
    {
        ItemInstance item = CreateItem(maxPrefix: 3);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(1f, 100f) };
        System.Random random = new System.Random(5);

        CraftingOperations.AddRandomAffix(item, pool, random);
        float initial = SumValue(item.Prefixes[0]);
        float previous = initial;

        for (int i = 0; i < 50; i++)
        {
            bool upgraded = CraftingOperations.UpgradeAffix(item, item.Prefixes[0], random);
            float current = SumValue(item.Prefixes[0]);
            Assert.GreaterOrEqual(current, previous);

            if (upgraded)
            {
                Assert.Greater(current, previous);
            }
            else
            {
                Assert.AreEqual(previous, current);
            }

            previous = current;
        }

        Assert.GreaterOrEqual(previous, initial);
    }

    [Test]
    public void UpgradeAffix_ReturnsFalse_WhenValueDoesNotIncrease()
    {
        ItemInstance item = CreateItem(maxPrefix: 3);
        List<AffixDefinition> pool = new List<AffixDefinition> { CreatePrefixAffix(20f, 20f) };
        System.Random random = new System.Random(33);
        Assert.IsTrue(CraftingOperations.AddRandomAffix(item, pool, random));
        AffixInstance original = item.Prefixes[0];

        bool upgraded = CraftingOperations.UpgradeAffix(item, original, random);

        Assert.IsFalse(upgraded);
        Assert.AreSame(original, item.Prefixes[0]);
        Assert.AreEqual(20f, SumValue(item.Prefixes[0]));
    }

    static float SumValue(AffixInstance affix)
    {
        float sum = 0f;

        for (int i = 0; i < affix.Modifiers.Count; i++)
        {
            sum += affix.Modifiers[i].Value;
        }

        return sum;
    }

    ItemInstance CreateItem(int maxPrefix)
    {
        ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        typeof(ItemBaseDefinition).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, "test");
        typeof(ItemBaseDefinition).GetField("_maxPrefixCount", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, maxPrefix);
        _created.Add(definition);

        return new ItemInstance("test", definition, ItemRarity.Normal, 1, 0, new List<ModifierInstance>());
    }

    AffixDefinition CreatePrefixAffix(float valueMin, float valueMax)
    {
        StatModifierDefinition modifier = new StatModifierDefinition();
        typeof(StatModifierDefinition).GetField("_operation", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(modifier, ModifierOperation.Increase);
        typeof(StatModifierDefinition).GetField("_valueRange", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(modifier, new Vector2(valueMin, valueMax));

        AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
        typeof(AffixDefinition).GetField("_affixType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(affix, AffixType.Prefix);
        typeof(AffixDefinition).GetField("_minItemLevel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(affix, 1);
        typeof(AffixDefinition).GetField("_weight", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(affix, 100);
        typeof(AffixDefinition).GetField("_modifiers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(affix, new List<StatModifierDefinition> { modifier });
        _created.Add(affix);
        return affix;
    }
}
