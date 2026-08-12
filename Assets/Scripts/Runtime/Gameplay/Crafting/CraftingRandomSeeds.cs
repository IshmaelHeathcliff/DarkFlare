namespace DarkFlare
{
    public readonly struct CraftingRandomSeeds
    {
        const int CountStream = 1;
        const int DistributionStream = 2;
        const int TargetStream = 3;
        const int DefinitionStream = 4;
        const int ValueStream = 5;

        public int RootSeed { get; }

        public int CountSeed { get; }

        public int DistributionSeed { get; }

        public int TargetSeed { get; }

        public int DefinitionSeed { get; }

        public int ValueSeed { get; }

        public CraftingRandomSeeds(int rootSeed)
        {
            RootSeed = rootSeed;
            CountSeed = Derive(rootSeed, CountStream, 0);
            DistributionSeed = Derive(rootSeed, DistributionStream, 0);
            TargetSeed = Derive(rootSeed, TargetStream, 0);
            DefinitionSeed = Derive(rootSeed, DefinitionStream, 0);
            ValueSeed = Derive(rootSeed, ValueStream, 0);
        }

        public int GetTargetValueSeed(AffixDefinition definition, AffixType type, int index)
        {
            int discriminator = StableHash(definition != null ? definition.Id : string.Empty);
            discriminator = unchecked(discriminator * 397 ^ (int)type * 31 ^ index);
            return Derive(RootSeed, ValueStream, discriminator);
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

        static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string safeText = text ?? string.Empty;

                for (int i = 0; i < safeText.Length; i++)
                {
                    hash ^= safeText[i];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
