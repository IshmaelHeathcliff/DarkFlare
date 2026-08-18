namespace DarkFlare
{
    public readonly struct ItemGenerationOptions
    {
        public ItemInstanceId Id { get; }

        public string InstanceId => Id.Value;

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
            : this(
                ItemInstanceId.FromLegacy(instanceId),
                itemLevel,
                seed,
                rarity,
                prefixCount,
                suffixCount)
        {
        }

        public ItemGenerationOptions(
            ItemInstanceId id,
            int itemLevel,
            int seed,
            ItemRarity rarity,
            int prefixCount,
            int suffixCount)
        {
            Id = id;
            ItemLevel = itemLevel;
            Seed = seed;
            Rarity = rarity;
            PrefixCount = prefixCount;
            SuffixCount = suffixCount;
        }
    }
}
