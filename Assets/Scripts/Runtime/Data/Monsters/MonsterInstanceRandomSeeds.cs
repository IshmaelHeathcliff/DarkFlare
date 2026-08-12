namespace DarkFlare
{
    public readonly struct MonsterInstanceRandomSeeds
    {
        const int HealthStream = 1;
        const int AffixCountStream = 2;
        const int AffixSelectionStream = 3;
        const int AffixValueStream = 4;

        public int RootSeed { get; }

        public int HealthSeed { get; }

        public int AffixCountSeed { get; }

        public int AffixSelectionSeed { get; }

        public MonsterInstanceRandomSeeds(int rootSeed)
        {
            RootSeed = rootSeed;
            HealthSeed = Derive(rootSeed, HealthStream, 0);
            AffixCountSeed = Derive(rootSeed, AffixCountStream, 0);
            AffixSelectionSeed = Derive(rootSeed, AffixSelectionStream, 0);
        }

        public int GetAffixValueSeed(string affixId)
        {
            return Derive(RootSeed, AffixValueStream, StableHash(affixId));
        }

        static int Derive(int rootSeed, int stream, int discriminator)
        {
            unchecked
            {
                uint value = (uint)rootSeed;
                value ^= 0x9E3779B9u + (uint)stream * 0x85EBCA6Bu;
                value = (value ^ value >> 16) * 0x7FEB352Du;
                value ^= (uint)discriminator * 0x846CA68Bu;
                value = (value ^ value >> 15) * 0x846CA68Bu;
                return (int)(value ^ value >> 16);
            }
        }

        static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string safeValue = value ?? string.Empty;

                for (int i = 0; i < safeValue.Length; i++)
                {
                    hash ^= safeValue[i];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
