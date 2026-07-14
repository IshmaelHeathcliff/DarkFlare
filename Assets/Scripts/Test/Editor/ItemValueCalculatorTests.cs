using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class ItemValueCalculatorTests
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
    public void GetValue_HigherRarity_HigherValue()
    {
        int normal = ItemValueCalculator.GetValue(CreateItem(10, ItemRarity.Normal, 0));
        int magic = ItemValueCalculator.GetValue(CreateItem(10, ItemRarity.Magic, 0));
        int rare = ItemValueCalculator.GetValue(CreateItem(10, ItemRarity.Rare, 0));

        Assert.Less(normal, magic);
        Assert.Less(magic, rare);
    }

    [Test]
    public void GetValue_MoreAffixes_HigherValue()
    {
        int noAffix = ItemValueCalculator.GetValue(CreateItem(10, ItemRarity.Magic, 0));
        int twoAffix = ItemValueCalculator.GetValue(CreateItem(10, ItemRarity.Magic, 2));

        Assert.Less(noAffix, twoAffix);
    }

    [Test]
    public void SellPrice_LowerThan_BuyPrice()
    {
        ItemInstance item = CreateItem(10, ItemRarity.Magic, 0);

        int buy = ItemValueCalculator.GetBuyPrice(item, 1.5f);
        int sell = ItemValueCalculator.GetSellPrice(item, 0.4f);

        Assert.Greater(buy, sell);
    }

    [Test]
    public void GetValue_ZeroBaseValue_IsZero()
    {
        ItemInstance item = CreateItem(0, ItemRarity.Rare, 2);

        Assert.AreEqual(0, ItemValueCalculator.GetValue(item));
    }

    ItemInstance CreateItem(int baseValue, ItemRarity rarity, int prefixCount)
    {
        ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        typeof(ItemBaseDefinition).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, "test");
        typeof(ItemBaseDefinition).GetField("_baseValue", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, baseValue);
        _created.Add(definition);

        ItemInstance item = new ItemInstance("test", definition, rarity, 1, 0, new List<ModifierInstance>());

        for (int i = 0; i < prefixCount; i++)
        {
            item.TryAddAffix(new AffixInstance(CreatePrefixAffix(), new List<ModifierInstance>()));
        }

        return item;
    }

    AffixDefinition CreatePrefixAffix()
    {
        AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
        typeof(AffixDefinition).GetField("_affixType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(affix, AffixType.Prefix);
        typeof(AffixDefinition).GetField("_minItemLevel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(affix, 1);
        _created.Add(affix);
        return affix;
    }
}
