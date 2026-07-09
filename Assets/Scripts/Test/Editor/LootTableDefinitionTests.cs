using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class LootTableDefinitionTests
{
    [Test]
    public void PickItem_ReturnsNull_WhenNoEntries()
    {
        LootTableDefinition definition = ScriptableObject.CreateInstance<LootTableDefinition>();

        ItemBaseDefinition result = definition.PickItem(new System.Random(1));

        Assert.IsNull(result);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void PickItem_ReturnsNull_WhenAllWeightsAreZero()
    {
        ItemBaseDefinition item = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        LootTableDefinition definition = ScriptableObject.CreateInstance<LootTableDefinition>();
        SetEntries(definition, new List<LootTableEntry> { CreateEntry(item, 0) });

        ItemBaseDefinition result = definition.PickItem(new System.Random(1));

        Assert.IsNull(result);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(item);
    }

    [Test]
    public void PickItem_RespectsRelativeWeights()
    {
        ItemBaseDefinition heavy = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        ItemBaseDefinition light = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        LootTableDefinition definition = ScriptableObject.CreateInstance<LootTableDefinition>();
        SetEntries(definition, new List<LootTableEntry>
        {
            CreateEntry(heavy, 90),
            CreateEntry(light, 10),
        });

        System.Random random = new System.Random(42);
        int heavyCount = 0;
        int total = 2000;

        for (int i = 0; i < total; i++)
        {
            if (definition.PickItem(random) == heavy)
            {
                heavyCount++;
            }
        }

        float heavyRatio = heavyCount / (float)total;
        Assert.Greater(heavyRatio, 0.8f);
        Assert.Less(heavyRatio, 1.0f);

        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(heavy);
        Object.DestroyImmediate(light);
    }

    static LootTableEntry CreateEntry(ItemBaseDefinition item, int weight)
    {
        LootTableEntry entry = new LootTableEntry();
        typeof(LootTableEntry).GetField("_item", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(entry, item);
        typeof(LootTableEntry).GetField("_weight", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(entry, weight);
        return entry;
    }

    static void SetEntries(LootTableDefinition definition, List<LootTableEntry> entries)
    {
        typeof(LootTableDefinition).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, entries);
    }
}
