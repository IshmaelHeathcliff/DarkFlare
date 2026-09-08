using System.Collections.Generic;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class Alpha015PrimaryAttributeTests
{
    readonly List<Object> _objects = new List<Object>();

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
    }

    [Test]
    public void StatExplanation_PreservesOverrideOrderMultipliersAndPrimaryDerivation()
    {
        var stats = new StatBlock();
        stats.SetValue(StatIds.Strength, 10f);
        stats.SetValue(StatIds.MaxHealth, 100f);
        var modifiers = new[]
        {
            CreateModifier(StatIds.Strength, ModifierOperation.Flat, 5f),
            CreateModifier(StatIds.Strength, ModifierOperation.Override, 20f),
            CreateModifier(StatIds.Strength, ModifierOperation.Flat, 2f),
            CreateModifier(StatIds.Strength, ModifierOperation.Increase, 50f),
            CreateModifier(StatIds.Strength, ModifierOperation.More, 10f),
            CreateModifier(StatIds.Strength, ModifierOperation.More, 20f),
        };
        var steps = new List<StatCalculationStep>();
        StatBlock explained = CombatStatResolver.Build(stats, modifiers, steps);
        Assert.AreEqual(43.56f, explained.GetValue(StatIds.Strength), 0.001f);
        Assert.AreEqual(187.12f, explained.GetValue(StatIds.MaxHealth), 0.001f);
        CollectionAssert.AreEqual(new[] { 15f, 20f, 22f, 33f, 36.3f, 43.56f },
            steps.FindAll(step => step.StatId == StatIds.Strength).ConvertAll(step => Mathf.Round(step.Result * 100f) / 100f));
        StatCalculationStep derived = steps.Find(step => step.StatId == StatIds.MaxHealth);
        Assert.AreEqual(StatIds.Strength, derived.DerivedFrom);
        Assert.AreEqual(87.12f, derived.Operand, 0.001f);
        Assert.AreEqual(CombatStatResolver.Build(stats, modifiers).GetValue(StatIds.MaxHealth), derived.Result);
        Assert.AreEqual(10f, stats.GetValue(StatIds.Strength));
    }

    [Test]
    public void PrimaryAttributeResolver_AppliesFrozenFormulasWithoutMutatingInput()
    {
        StatBlock input = new StatBlock();
        input.SetValue(StatIds.MaxHealth, 100f);
        input.SetValue(StatIds.Mana, 40f);
        input.SetValue(StatIds.Accuracy, 20f);
        input.SetValue(StatIds.Evasion, 3f);
        input.SetValue(StatIds.Strength, 10f);
        input.SetValue(StatIds.Dexterity, 7f);
        input.SetValue(StatIds.Intelligence, 5f);

        StatBlock result = PrimaryAttributeResolver.Apply(input);

        Assert.AreEqual(120f, result.GetValue(StatIds.MaxHealth), 0.001f);
        Assert.AreEqual(50f, result.GetValue(StatIds.Mana), 0.001f);
        Assert.AreEqual(27f, result.GetValue(StatIds.Accuracy), 0.001f);
        Assert.AreEqual(10f, result.GetValue(StatIds.Evasion), 0.001f);
        Assert.AreEqual(100f, input.GetValue(StatIds.MaxHealth), 0.001f);
        Assert.AreEqual(40f, input.GetValue(StatIds.Mana), 0.001f);
        Assert.AreEqual(20f, input.GetValue(StatIds.Accuracy), 0.001f);
        Assert.AreEqual(3f, input.GetValue(StatIds.Evasion), 0.001f);
    }

    [Test]
    public void CombatStatResolver_AggregatesDirectModifiersBeforePrimaryAttributes()
    {
        StatBlock baseStats = new StatBlock();
        baseStats.SetValue(StatIds.MaxHealth, 100f);
        baseStats.SetValue(StatIds.Mana, 30f);
        baseStats.SetValue(StatIds.Accuracy, 10f);
        baseStats.SetValue(StatIds.Evasion, 20f);
        baseStats.SetValue(StatIds.Strength, 5f);
        baseStats.SetValue(StatIds.Dexterity, 3f);
        baseStats.SetValue(StatIds.Intelligence, 4f);
        ModifierInstance[] modifiers =
        {
            CreateModifier(StatIds.Strength, ModifierOperation.Flat, 5f),
            CreateModifier(StatIds.MaxHealth, ModifierOperation.Increase, 10f),
            CreateModifier(StatIds.Dexterity, ModifierOperation.More, 100f),
            CreateModifier(StatIds.Intelligence, ModifierOperation.Override, 10f),
        };

        StatBlock result = CombatStatResolver.Build(baseStats, modifiers);

        Assert.AreEqual(10f, result.GetValue(StatIds.Strength), 0.001f);
        Assert.AreEqual(6f, result.GetValue(StatIds.Dexterity), 0.001f);
        Assert.AreEqual(10f, result.GetValue(StatIds.Intelligence), 0.001f);
        Assert.AreEqual(130f, result.GetValue(StatIds.MaxHealth), 0.001f);
        Assert.AreEqual(50f, result.GetValue(StatIds.Mana), 0.001f);
        Assert.AreEqual(16f, result.GetValue(StatIds.Accuracy), 0.001f);
        Assert.AreEqual(26f, result.GetValue(StatIds.Evasion), 0.001f);
    }

    [Test]
    public void CombatActor_RepeatedModifierRebuildDoesNotStackDerivedValues()
    {
        StatBlock baseStats = new StatBlock();
        baseStats.SetValue(StatIds.Mana, 50f);
        CombatActor actor = CreateActor("primary_rebuild", baseStats);
        ModifierInstance[] modifiers =
        {
            CreateModifier(StatIds.Strength, ModifierOperation.Flat, 10f),
            CreateModifier(StatIds.Dexterity, ModifierOperation.Flat, 8f),
            CreateModifier(StatIds.Intelligence, ModifierOperation.Flat, 6f),
        };

        actor.SetModifiers(modifiers);
        actor.SetModifiers(modifiers);

        Assert.AreEqual(120f, actor.MaxHealth, 0.001f);
        Assert.AreEqual(62f, actor.MaxMana, 0.001f);
        Assert.AreEqual(8f, actor.Stats.GetValue(StatIds.Accuracy), 0.001f);
        Assert.AreEqual(8f, actor.Stats.GetValue(StatIds.Evasion), 0.001f);
    }

    [Test]
    public void CombatActor_PrimaryAttributeRebuildPreservesHealthAndManaRatios()
    {
        StatBlock baseStats = new StatBlock();
        baseStats.SetValue(StatIds.Mana, 50f);
        CombatActor actor = CreateActor("primary_resources", baseStats);
        DamageResult damage = new DamageResult(
            true,
            false,
            new Dictionary<DamageType, float> { { DamageType.Physical, 40f } },
            new Dictionary<DamageType, float> { { DamageType.Physical, 40f } });
        actor.ReceiveDamage(damage);
        Assert.IsTrue(actor.TrySpendMana(20f));

        actor.SetModifiers(new[]
        {
            CreateModifier(StatIds.Strength, ModifierOperation.Flat, 10f),
            CreateModifier(StatIds.Intelligence, ModifierOperation.Flat, 5f),
        });

        Assert.AreEqual(120f, actor.MaxHealth, 0.001f);
        Assert.AreEqual(72f, actor.CurrentHealth, 0.001f);
        Assert.AreEqual(60f, actor.MaxMana, 0.001f);
        Assert.AreEqual(36f, actor.CurrentMana, 0.001f);
    }

    [Test]
    public void CombatStatResolver_LeavesDamageModifiersForAttackResolution()
    {
        StatBlock baseStats = new StatBlock();
        baseStats.SetValue(StatIds.PhysicalDamage, 7f);

        StatBlock result = CombatStatResolver.Build(baseStats, new[]
        {
            CreateModifier(StatIds.Damage, ModifierOperation.Increase, 50f),
            CreateModifier(StatIds.PhysicalDamage, ModifierOperation.Flat, 100f),
        });

        Assert.AreEqual(0f, result.GetValue(StatIds.Damage), 0.001f);
        Assert.AreEqual(7f, result.GetValue(StatIds.PhysicalDamage), 0.001f);
    }

    [Test]
    public void HudAttributeSnapshot_ExposesEveryStatInCanonicalOrder()
    {
        HudAttributeSnapshot snapshot = new HudAttributeSnapshot(new StatBlock());

        Assert.AreEqual(StatIds.All.Count, snapshot.Values.Count);

        for (int i = 0; i < StatIds.All.Count; i++)
        {
            Assert.AreEqual(StatIds.All[i], snapshot.Values[i].StatId, $"属性索引 {i} 与 StatIds.All 不一致");
        }
    }

    CombatActor CreateActor(string actorId, StatBlock stats)
    {
        GameObject actorObject = new GameObject(actorId);
        _objects.Add(actorObject);
        CombatActor actor = actorObject.AddComponent<CombatActor>();
        actor.Configure(actorId, ActorTeam.Player, 100f, stats, TagSet.Empty);
        return actor;
    }

    static ModifierInstance CreateModifier(string statId, ModifierOperation operation, float value)
    {
        return new ModifierInstance(
            statId,
            operation,
            ModifierScope.GlobalActor,
            value,
            DamageType.Physical,
            DamageType.Physical,
            TagSet.Empty,
            TagSet.Empty);
    }
}
