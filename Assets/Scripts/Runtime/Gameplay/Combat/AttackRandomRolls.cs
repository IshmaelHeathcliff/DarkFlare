namespace DarkFlare
{
    public readonly struct AttackRandomRolls
    {
        const uint BaseDamagePurpose = 0xA511E9B3u;
        const uint HitPurpose = 0x63D83595u;
        const uint CriticalPurpose = 0xC2B2AE35u;

        public int RootSeed { get; }

        public int BaseDamageSeed { get; }

        public float HitRoll { get; }

        public float CriticalRoll { get; }

        AttackRandomRolls(int rootSeed, int baseDamageSeed, float hitRoll, float criticalRoll)
        {
            RootSeed = rootSeed;
            BaseDamageSeed = baseDamageSeed;
            HitRoll = hitRoll;
            CriticalRoll = criticalRoll;
        }

        public static AttackRandomRolls FromRootSeed(int rootSeed)
        {
            uint baseDamage = Mix(rootSeed, BaseDamagePurpose);
            uint hit = Mix(rootSeed, HitPurpose);
            uint critical = Mix(rootSeed, CriticalPurpose);
            return new AttackRandomRolls(
                rootSeed,
                (int)baseDamage,
                ToUnitInterval(hit),
                ToUnitInterval(critical));
        }

        static uint Mix(int rootSeed, uint purpose)
        {
            unchecked
            {
                uint value = (uint)rootSeed ^ purpose;
                value = (value ^ value >> 16) * 0x7FEB352Du;
                value = (value ^ value >> 15) * 0x846CA68Bu;
                return value ^ value >> 16;
            }
        }

        static float ToUnitInterval(uint value)
        {
            return (value >> 8) / 16777216f;
        }
    }
}
