using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static class CraftingOperations
    {
        public static bool AddRandomAffix(ItemInstance item, IReadOnlyList<AffixDefinition> affixPool, System.Random random)
        {
            if (item == null || item.BaseDefinition == null)
            {
                return false;
            }

            AffixDefinition selected = PickAffix(item, affixPool, random);

            if (selected == null)
            {
                return false;
            }

            return item.TryAddAffix(selected.CreateInstance(random));
        }

        public static bool RerollAllAffixes(ItemInstance item, IReadOnlyList<AffixDefinition> affixPool, System.Random random)
        {
            if (item == null)
            {
                return false;
            }

            int total = item.Prefixes.Count + item.Suffixes.Count;

            if (total <= 0)
            {
                return false;
            }

            item.ClearAffixes();

            for (int i = 0; i < total; i++)
            {
                AddRandomAffix(item, affixPool, random);
            }

            return true;
        }

        public static bool RemoveAndRerollAffix(ItemInstance item, AffixInstance target, IReadOnlyList<AffixDefinition> affixPool, System.Random random)
        {
            if (item == null || !item.RemoveAffix(target))
            {
                return false;
            }

            AddRandomAffix(item, affixPool, random);
            return true;
        }

        public static bool UpgradeAffix(ItemInstance item, AffixInstance target, System.Random random)
        {
            if (item == null || target == null || target.Definition == null)
            {
                return false;
            }

            if (!item.Prefixes.Contains(target) && !item.Suffixes.Contains(target))
            {
                return false;
            }

            AffixInstance rerolled = target.Definition.CreateInstance(random);

            if (SumValue(rerolled) > SumValue(target) && item.RemoveAffix(target))
            {
                item.TryAddAffix(rerolled);
            }

            return true;
        }

        static AffixDefinition PickAffix(ItemInstance item, IReadOnlyList<AffixDefinition> affixPool, System.Random random)
        {
            if (affixPool == null)
            {
                return null;
            }

            List<AffixDefinition> candidates = new List<AffixDefinition>();
            int totalWeight = 0;

            for (int i = 0; i < affixPool.Count; i++)
            {
                AffixDefinition affix = affixPool[i];

                if (affix == null || affix.Weight <= 0 || !affix.CanApplyTo(item.Tags, item.ItemLevel))
                {
                    continue;
                }

                if (!HasRoom(item, affix))
                {
                    continue;
                }

                candidates.Add(affix);
                totalWeight += affix.Weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = random.Next(0, totalWeight);

            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].Weight;

                if (roll < 0)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        static bool HasRoom(ItemInstance item, AffixDefinition affix)
        {
            if (affix.AffixType == AffixType.Prefix)
            {
                return item.Prefixes.Count < item.BaseDefinition.MaxPrefixCount;
            }

            if (affix.AffixType == AffixType.Suffix)
            {
                return item.Suffixes.Count < item.BaseDefinition.MaxSuffixCount;
            }

            return false;
        }

        static float SumValue(AffixInstance affix)
        {
            float sum = 0f;

            for (int i = 0; i < affix.Modifiers.Count; i++)
            {
                sum += affix.Modifiers[i].Value;
            }

            return sum;
        }
    }
}
