using System.Collections.Generic;
using DarkFlare;
using NUnit.Framework;

public class DamageCalculatorTests
{
    [Test]
    public void Calculate_IncreaseDamageModifier_RaisesTotalDamage()
    {
        List<DamagePacket> baseDamages = new List<DamagePacket> { new DamagePacket(DamageType.Physical, 100f, TagSet.Empty) };
        StatBlock attackerStats = new StatBlock();
        StatBlock defenderStats = new StatBlock();

        DamageContext baseline = CreateContext(baseDamages, attackerStats, defenderStats, new List<ModifierInstance>());

        ModifierInstance increaseDamage = new ModifierInstance(
            StatIds.Damage,
            ModifierOperation.Increase,
            ModifierScope.GlobalActor,
            50f,
            DamageType.Physical,
            DamageType.Physical,
            TagSet.Empty,
            TagSet.Empty);

        DamageContext withModifier = CreateContext(baseDamages, attackerStats, defenderStats, new List<ModifierInstance> { increaseDamage });

        DamageResult baselineResult = DamageCalculator.Calculate(baseline);
        DamageResult withModifierResult = DamageCalculator.Calculate(withModifier);

        Assert.Greater(withModifierResult.TotalDamage, baselineResult.TotalDamage);
    }

    [Test]
    public void Calculate_CriticalDamage_UsesAdditionalPercentage()
    {
        List<DamagePacket> baseDamages = new List<DamagePacket>
        {
            new DamagePacket(DamageType.Physical, 100f, TagSet.Empty),
        };
        StatBlock attackerStats = new StatBlock();
        attackerStats.SetValue(StatIds.CriticalDamage, 50f);
        DamageContext context = CreateContext(
            baseDamages,
            attackerStats,
            new StatBlock(),
            new List<ModifierInstance>(),
            true);

        DamageResult result = DamageCalculator.Calculate(context);

        Assert.AreEqual(150f, result.TotalDamage, 0.001f);
    }

    static DamageContext CreateContext(
        List<DamagePacket> baseDamages,
        StatBlock attackerStats,
        StatBlock defenderStats,
        List<ModifierInstance> attackerModifiers,
        bool isCritical = false)
    {
        return new DamageContext(
            "attacker",
            "defender",
            "test_skill",
            string.Empty,
            1,
            baseDamages,
            TagSet.Empty,
            attackerStats,
            defenderStats,
            attackerModifiers,
            new List<ModifierInstance>(),
            true,
            isCritical);
    }
}
