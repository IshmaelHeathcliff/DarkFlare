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
            return (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Round((double)value * item.Quantity)));
        }

        public static int GetBuyPrice(ItemInstance item, float buyMultiplier)
        {
            return (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Ceiling((double)GetValue(item) * buyMultiplier)));
        }

        public static int GetSellPrice(ItemInstance item, float sellMultiplier)
        {
            return (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Floor((double)GetValue(item) * sellMultiplier)));
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
