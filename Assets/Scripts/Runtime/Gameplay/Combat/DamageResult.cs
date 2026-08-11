using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DarkFlare
{
    public sealed class DamageTypeBreakdown
    {
        public DamageType DamageType { get; }

        public float BeforeDefense { get; }

        public float AfterTargetTaken { get; }

        public float EffectiveResistance { get; }

        public float Armor { get; }

        public float ArmorReduction { get; }

        public float FinalDamage { get; }

        public DamageTypeBreakdown(
            DamageType damageType,
            float beforeDefense,
            float afterTargetTaken,
            float effectiveResistance,
            float armor,
            float armorReduction,
            float finalDamage)
        {
            DamageType = damageType;
            BeforeDefense = beforeDefense;
            AfterTargetTaken = afterTargetTaken;
            EffectiveResistance = effectiveResistance;
            Armor = armor;
            ArmorReduction = armorReduction;
            FinalDamage = finalDamage;
        }
    }

    public sealed class DamageResult
    {
        public const float DamageEpsilon = 0.0001f;

        readonly Dictionary<DamageType, float> _damageBeforeDefense;
        readonly Dictionary<DamageType, float> _damageAfterDefense;
        readonly Dictionary<DamageType, DamageTypeBreakdown> _breakdowns;

        public HitOutcome Outcome { get; }

        public bool IsHit => Outcome == HitOutcome.Hit || Outcome == HitOutcome.NoDamage;

        public bool IsCritical { get; }

        public bool DidDealDamage => Outcome == HitOutcome.Hit && TotalDamage > DamageEpsilon;

        public float HitChance { get; }

        public float HitRoll { get; }

        public float CriticalChance { get; }

        public float CriticalRoll { get; }

        public float TotalDamage { get; }

        public IReadOnlyDictionary<DamageType, float> DamageBeforeDefense => _damageBeforeDefense;

        public IReadOnlyDictionary<DamageType, float> DamageAfterDefense => _damageAfterDefense;

        public IReadOnlyDictionary<DamageType, DamageTypeBreakdown> Breakdowns => _breakdowns;

        public DamageResult(
            bool isHit,
            bool isCritical,
            Dictionary<DamageType, float> damageBeforeDefense,
            Dictionary<DamageType, float> damageAfterDefense)
            : this(
                ResolveLegacyOutcome(isHit, damageAfterDefense),
                isCritical,
                isHit ? 1f : 0f,
                0f,
                isCritical ? 1f : 0f,
                0f,
                CreateLegacyBreakdowns(damageBeforeDefense, damageAfterDefense))
        {
        }

        public DamageResult(
            HitOutcome outcome,
            bool isCritical,
            float hitChance,
            float hitRoll,
            float criticalChance,
            float criticalRoll,
            Dictionary<DamageType, DamageTypeBreakdown> breakdowns)
        {
            Outcome = outcome;
            IsCritical = isCritical && IsHit;
            HitChance = hitChance;
            HitRoll = hitRoll;
            CriticalChance = criticalChance;
            CriticalRoll = criticalRoll;
            _breakdowns = breakdowns ?? new Dictionary<DamageType, DamageTypeBreakdown>();
            _damageBeforeDefense = new Dictionary<DamageType, float>(_breakdowns.Count);
            _damageAfterDefense = new Dictionary<DamageType, float>(_breakdowns.Count);

            foreach (KeyValuePair<DamageType, DamageTypeBreakdown> pair in _breakdowns)
            {
                DamageTypeBreakdown breakdown = pair.Value;
                _damageBeforeDefense[pair.Key] = breakdown.BeforeDefense;
                _damageAfterDefense[pair.Key] = breakdown.FinalDamage;
                TotalDamage += breakdown.FinalDamage;
            }

            if (Outcome == HitOutcome.Hit && TotalDamage <= DamageEpsilon)
            {
                Outcome = HitOutcome.NoDamage;
            }
        }

        public static DamageResult CreateWithoutDamage(HitResolution resolution)
        {
            return new DamageResult(
                resolution.Outcome,
                false,
                resolution.HitChance,
                resolution.HitRoll,
                resolution.CriticalChance,
                resolution.CriticalRoll,
                new Dictionary<DamageType, DamageTypeBreakdown>());
        }

        public string ToDebugString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Outcome=").Append(Outcome);
            builder.Append(" Hit=").Append(HitRoll.ToString("0.####", CultureInfo.InvariantCulture));
            builder.Append('/').Append(HitChance.ToString("0.####", CultureInfo.InvariantCulture));
            builder.Append(" Critical=").Append(IsCritical);
            builder.Append(' ').Append(CriticalRoll.ToString("0.####", CultureInfo.InvariantCulture));
            builder.Append('/').Append(CriticalChance.ToString("0.####", CultureInfo.InvariantCulture));

            foreach (DamageType damageType in GetDamageTypes())
            {
                if (!_breakdowns.TryGetValue(damageType, out DamageTypeBreakdown breakdown))
                {
                    continue;
                }

                builder.Append(" | ").Append(damageType);
                builder.Append(": before=").Append(breakdown.BeforeDefense.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(" taken=").Append(breakdown.AfterTargetTaken.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(" resistance=").Append(breakdown.EffectiveResistance.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(" armor=").Append(breakdown.Armor.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(" armorReduction=").Append(breakdown.ArmorReduction.ToString("0.####", CultureInfo.InvariantCulture));
                builder.Append(" final=").Append(breakdown.FinalDamage.ToString("0.###", CultureInfo.InvariantCulture));
            }

            builder.Append(" | total=").Append(TotalDamage.ToString("0.###", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        static HitOutcome ResolveLegacyOutcome(
            bool isHit,
            IReadOnlyDictionary<DamageType, float> damageAfterDefense)
        {
            if (!isHit)
            {
                return HitOutcome.Missed;
            }

            float total = 0f;

            if (damageAfterDefense != null)
            {
                foreach (KeyValuePair<DamageType, float> pair in damageAfterDefense)
                {
                    total += pair.Value;
                }
            }

            return total > DamageEpsilon ? HitOutcome.Hit : HitOutcome.NoDamage;
        }

        static Dictionary<DamageType, DamageTypeBreakdown> CreateLegacyBreakdowns(
            IReadOnlyDictionary<DamageType, float> before,
            IReadOnlyDictionary<DamageType, float> after)
        {
            Dictionary<DamageType, DamageTypeBreakdown> result = new Dictionary<DamageType, DamageTypeBreakdown>();

            foreach (DamageType damageType in GetDamageTypes())
            {
                float beforeValue = before != null && before.TryGetValue(damageType, out float storedBefore)
                    ? storedBefore
                    : 0f;
                float afterValue = after != null && after.TryGetValue(damageType, out float storedAfter)
                    ? storedAfter
                    : 0f;

                if (beforeValue <= 0f && afterValue <= 0f)
                {
                    continue;
                }

                result[damageType] = new DamageTypeBreakdown(
                    damageType,
                    beforeValue,
                    beforeValue,
                    0f,
                    0f,
                    0f,
                    afterValue);
            }

            return result;
        }

        static IEnumerable<DamageType> GetDamageTypes()
        {
            yield return DamageType.Physical;
            yield return DamageType.Fire;
            yield return DamageType.Cold;
            yield return DamageType.Lightning;
            yield return DamageType.Chaos;
        }
    }
}
