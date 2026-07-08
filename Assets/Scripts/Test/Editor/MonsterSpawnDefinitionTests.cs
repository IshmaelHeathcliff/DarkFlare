using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class MonsterSpawnDefinitionTests
{
    [Test]
    public void PickMonster_ReturnsNull_WhenNoRules()
    {
        MonsterSpawnDefinition definition = ScriptableObject.CreateInstance<MonsterSpawnDefinition>();

        MonsterDefinition result = definition.PickMonster(new System.Random(1));

        Assert.IsNull(result);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void PickMonster_ReturnsNull_WhenAllWeightsAreZero()
    {
        MonsterDefinition monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        MonsterSpawnDefinition definition = ScriptableObject.CreateInstance<MonsterSpawnDefinition>();
        SetRules(definition, new List<MonsterSpawnRule> { CreateRule(monster, 0) });

        MonsterDefinition result = definition.PickMonster(new System.Random(1));

        Assert.IsNull(result);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(monster);
    }

    [Test]
    public void PickMonster_RespectsRelativeWeights()
    {
        MonsterDefinition heavy = ScriptableObject.CreateInstance<MonsterDefinition>();
        MonsterDefinition light = ScriptableObject.CreateInstance<MonsterDefinition>();
        MonsterSpawnDefinition definition = ScriptableObject.CreateInstance<MonsterSpawnDefinition>();
        SetRules(definition, new List<MonsterSpawnRule>
        {
            CreateRule(heavy, 90),
            CreateRule(light, 10),
        });

        System.Random random = new System.Random(42);
        int heavyCount = 0;
        int total = 2000;

        for (int i = 0; i < total; i++)
        {
            if (definition.PickMonster(random) == heavy)
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

    static MonsterSpawnRule CreateRule(MonsterDefinition monster, int weight)
    {
        MonsterSpawnRule rule = new MonsterSpawnRule();
        typeof(MonsterSpawnRule).GetField("_monster", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(rule, monster);
        typeof(MonsterSpawnRule).GetField("_weight", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(rule, weight);
        return rule;
    }

    static void SetRules(MonsterSpawnDefinition definition, List<MonsterSpawnRule> rules)
    {
        typeof(MonsterSpawnDefinition).GetField("_rules", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, rules);
    }
}
