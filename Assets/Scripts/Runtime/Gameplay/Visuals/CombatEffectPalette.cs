using UnityEngine;

namespace DarkFlare
{
    public static class CombatEffectPalette
    {
        static readonly DamageType[] DamageTypePriority =
        {
            DamageType.Physical,
            DamageType.Fire,
            DamageType.Cold,
            DamageType.Lightning,
            DamageType.Chaos,
        };

        public static Color ArcaneProjectileTint => new Color(0.35f, 0.95f, 1f, 1f);

        public static bool ShouldPlayProjectileImpact(DamageResult result)
        {
            return result != null && result.IsHit;
        }

        public static bool ShouldPlayActorHit(DamageResult result)
        {
            return result != null && result.DidDealDamage;
        }

        public static bool TryGetPrimaryDamageType(DamageResult result, out DamageType damageType)
        {
            damageType = DamageType.Physical;

            if (!ShouldPlayActorHit(result))
            {
                return false;
            }

            float largestDamage = 0f;
            bool found = false;

            for (int i = 0; i < DamageTypePriority.Length; i++)
            {
                DamageType candidate = DamageTypePriority[i];

                if (!result.Breakdowns.TryGetValue(candidate, out DamageTypeBreakdown breakdown)
                    || breakdown.FinalDamage <= largestDamage)
                {
                    continue;
                }

                damageType = candidate;
                largestDamage = breakdown.FinalDamage;
                found = true;
            }

            return found;
        }

        public static Color GetHitTint(DamageResult result)
        {
            return TryGetPrimaryDamageType(result, out DamageType damageType)
                ? GetHitTint(damageType)
                : GetHitTint(DamageType.Physical);
        }

        public static Color GetHitTint(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Fire => new Color(1f, 0.36f, 0.18f, 1f),
                DamageType.Cold => new Color(0.32f, 0.82f, 1f, 1f),
                DamageType.Lightning => new Color(1f, 0.84f, 0.24f, 1f),
                DamageType.Chaos => new Color(0.72f, 0.34f, 1f, 1f),
                _ => new Color(1f, 0.9f, 0.76f, 1f),
            };
        }
    }
}
