using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class DamageResult
    {
        readonly Dictionary<DamageType, float> _damageBeforeDefense;
        readonly Dictionary<DamageType, float> _damageAfterDefense;

        public bool IsHit { get; }

        public bool IsCritical { get; }

        public float TotalDamage { get; }

        public IReadOnlyDictionary<DamageType, float> DamageBeforeDefense => _damageBeforeDefense;

        public IReadOnlyDictionary<DamageType, float> DamageAfterDefense => _damageAfterDefense;

        public DamageResult(
            bool isHit,
            bool isCritical,
            Dictionary<DamageType, float> damageBeforeDefense,
            Dictionary<DamageType, float> damageAfterDefense)
        {
            IsHit = isHit;
            IsCritical = isCritical;
            _damageBeforeDefense = damageBeforeDefense;
            _damageAfterDefense = damageAfterDefense;

            foreach (KeyValuePair<DamageType, float> pair in _damageAfterDefense)
            {
                TotalDamage += pair.Value;
            }
        }
    }
}

