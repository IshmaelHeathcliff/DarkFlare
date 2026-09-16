using System;

namespace DarkFlare
{
    internal static class ItemResourceRecovery
    {
        internal static CombatResourceSnapshot Apply(CombatResourceSnapshot before, bool health, float amount, ItemRecoveryMode mode)
        {
            double maximum = health ? before.MaxHealth : before.MaxMana;
            double change = mode == ItemRecoveryMode.Percent ? maximum * amount / 100d : amount;
            if (double.IsNaN(change) || double.IsInfinity(change) || change < 0 || change > float.MaxValue)
            {
                throw new ArgumentException("恢复量溢出或非法");
            }
            float result = CombatActor.RecoverResource(health ? before.CurrentHealth : before.CurrentMana, (float)maximum, (float)change);
            return health ? new CombatResourceSnapshot(result, before.MaxHealth, before.CurrentMana, before.MaxMana)
                : new CombatResourceSnapshot(before.CurrentHealth, before.MaxHealth, result, before.MaxMana);
        }
    }
}
