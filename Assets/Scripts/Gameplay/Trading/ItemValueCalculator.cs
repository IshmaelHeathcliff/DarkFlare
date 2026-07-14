using UnityEngine;

namespace DarkFlare
{
    public static class ItemValueCalculator
    {
        public static int GetValue(ItemInstance item)
        {
            if (item == null || item.BaseDefinition == null)
            {
                return 0;
            }

            int affixCount = item.Prefixes.Count + item.Suffixes.Count;
            float value = item.BaseDefinition.BaseValue * GetRarityMultiplier(item.Rarity) * (1f + 0.25f * affixCount);
            return Mathf.RoundToInt(value);
        }

        public static int GetBuyPrice(ItemInstance item, float buyMultiplier)
        {
            return Mathf.CeilToInt(GetValue(item) * buyMultiplier);
        }

        public static int GetSellPrice(ItemInstance item, float sellMultiplier)
        {
            return Mathf.FloorToInt(GetValue(item) * sellMultiplier);
        }

        static float GetRarityMultiplier(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Magic)
            {
                return 2f;
            }

            if (rarity == ItemRarity.Rare)
            {
                return 4f;
            }

            if (rarity == ItemRarity.Unique)
            {
                return 8f;
            }

            return 1f;
        }
    }
}
