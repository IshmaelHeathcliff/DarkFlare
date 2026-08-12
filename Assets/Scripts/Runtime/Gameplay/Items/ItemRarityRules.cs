using System;

namespace DarkFlare
{
    public readonly struct ItemAffixLimits
    {
        public int MaxPrefixCount { get; }

        public int MaxSuffixCount { get; }

        public int MinimumTotalCount { get; }

        public int MaximumTotalCount { get; }

        public ItemAffixLimits(
            int maxPrefixCount,
            int maxSuffixCount,
            int minimumTotalCount,
            int maximumTotalCount)
        {
            MaxPrefixCount = maxPrefixCount;
            MaxSuffixCount = maxSuffixCount;
            MinimumTotalCount = minimumTotalCount;
            MaximumTotalCount = maximumTotalCount;
        }
    }

    public static class ItemRarityRules
    {
        public static ItemAffixLimits GetLimits(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Normal:
                    return new ItemAffixLimits(0, 0, 0, 0);
                case ItemRarity.Magic:
                    return new ItemAffixLimits(1, 1, 1, 2);
                case ItemRarity.Rare:
                    return new ItemAffixLimits(3, 3, 2, 6);
                case ItemRarity.Unique:
                    return new ItemAffixLimits(3, 3, 4, 6);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "未知物品稀有度");
            }
        }

        public static bool IsWithinCapacity(ItemRarity rarity, int prefixCount, int suffixCount)
        {
            if (prefixCount < 0 || suffixCount < 0 || !Enum.IsDefined(typeof(ItemRarity), rarity))
            {
                return false;
            }

            ItemAffixLimits limits = GetLimits(rarity);
            return prefixCount <= limits.MaxPrefixCount && suffixCount <= limits.MaxSuffixCount;
        }

        public static bool IsNormalGenerationValid(ItemRarity rarity, int prefixCount, int suffixCount)
        {
            if (!IsWithinCapacity(rarity, prefixCount, suffixCount))
            {
                return false;
            }

            ItemAffixLimits limits = GetLimits(rarity);
            int totalCount = prefixCount + suffixCount;
            return totalCount >= limits.MinimumTotalCount && totalCount <= limits.MaximumTotalCount;
        }

        public static bool TryGetNextRarity(ItemRarity current, out ItemRarity next)
        {
            switch (current)
            {
                case ItemRarity.Normal:
                    next = ItemRarity.Magic;
                    return true;
                case ItemRarity.Magic:
                    next = ItemRarity.Rare;
                    return true;
                case ItemRarity.Rare:
                    next = ItemRarity.Unique;
                    return true;
                default:
                    next = current;
                    return false;
            }
        }
    }
}
