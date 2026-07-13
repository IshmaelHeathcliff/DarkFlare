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

    static DamageContext CreateContext(
        List<DamagePacket> baseDamages,
        StatBlock attackerStats,
        StatBlock defenderStats,
        List<ModifierInstance> attackerModifiers)
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
            new List<ModifierInstance>());
    }
}
