namespace DarkFlare
{
    public readonly struct ItemGenerationOptions
    {
        public string InstanceId { get; }

        public int ItemLevel { get; }

        public int Seed { get; }

        public ItemRarity Rarity { get; }

        public int PrefixCount { get; }

        public int SuffixCount { get; }

        public ItemGenerationOptions(
            string instanceId,
            int itemLevel,
            int seed,
            ItemRarity rarity,
            int prefixCount,
            int suffixCount)
        {
            InstanceId = instanceId;
            ItemLevel = itemLevel;
            Seed = seed;
            Rarity = rarity;
            PrefixCount = prefixCount;
            SuffixCount = suffixCount;
        }
    }
}

