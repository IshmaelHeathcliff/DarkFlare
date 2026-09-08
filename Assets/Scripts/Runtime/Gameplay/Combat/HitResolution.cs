namespace DarkFlare
{
    public enum HitOutcome
    {
        InvalidTarget,
        Missed,
        Evaded,
        Hit,
        NoDamage,
        Invulnerable
    }

    public readonly struct HitResolution
    {
        public HitOutcome Outcome { get; }

        public float HitChance { get; }

        public float HitRoll { get; }

        public float CriticalChance { get; }

        public float CriticalRoll { get; }

        public bool IsCritical { get; }

        public bool IsHit => Outcome == HitOutcome.Hit || Outcome == HitOutcome.NoDamage;

        public HitResolution(
            HitOutcome outcome,
            float hitChance,
            float hitRoll,
            float criticalChance,
            float criticalRoll,
            bool isCritical)
        {
            Outcome = outcome;
            HitChance = hitChance;
            HitRoll = hitRoll;
            CriticalChance = criticalChance;
            CriticalRoll = criticalRoll;
            IsCritical = isCritical && (outcome == HitOutcome.Hit || outcome == HitOutcome.NoDamage);
        }
    }

    public static class HitResolutionCalculator
    {
        const float MinimumHitChance = 0.05f;
        const float MaximumHitChance = 0.95f;

        public static HitResolution Resolve(
            StatBlock attackerStats,
            StatBlock defenderStats,
            AttackRandomRolls rolls)
        {
            return Resolve(
                attackerStats,
                defenderStats,
                rolls.HitRoll,
                rolls.CriticalRoll);
        }

        public static HitResolution Resolve(
            StatBlock attackerStats,
            StatBlock defenderStats,
            float hitRoll,
            float criticalRoll)
        {
            float accuracy = Max(0f, attackerStats != null
                ? attackerStats.GetValue(StatIds.Accuracy)
                : 0f);
            float evasion = Max(0f, defenderStats != null
                ? defenderStats.GetValue(StatIds.Evasion)
                : 0f);
            float hitChance = accuracy <= 0f
                ? MinimumHitChance
                : Clamp(accuracy / (accuracy + evasion), MinimumHitChance, MaximumHitChance);
            HitOutcome outcome;

            if (hitRoll < hitChance)
            {
                outcome = HitOutcome.Hit;
            }
            else if (evasion > 0f && hitRoll < MaximumHitChance)
            {
                outcome = HitOutcome.Evaded;
            }
            else
            {
                outcome = HitOutcome.Missed;
            }

            float criticalChance = CombatStatValues.CriticalChance(
                attackerStats != null ? attackerStats.GetValue(StatIds.CriticalChance) : 0f) / 100f;
            bool isCritical = outcome == HitOutcome.Hit && criticalRoll < criticalChance;
            return new HitResolution(
                outcome,
                hitChance,
                hitRoll,
                criticalChance,
                criticalRoll,
                isCritical);
        }

        public static HitResolution Invalid(AttackRandomRolls rolls)
        {
            return new HitResolution(
                HitOutcome.InvalidTarget,
                0f,
                rolls.HitRoll,
                0f,
                rolls.CriticalRoll,
                false);
        }

        static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        static float Max(float left, float right)
        {
            return left > right ? left : right;
        }
    }
}
