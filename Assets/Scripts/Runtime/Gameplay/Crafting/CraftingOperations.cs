using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static class CraftingOperations
    {
        public static bool AddRandomAffix(ItemInstance item, IReadOnlyList<AffixDefinition> affixPool, System.Random random)
        {
            return AddRandomAffix(item, affixPool, random, null);
        }

        static bool AddRandomAffix(
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            System.Random random,
            AffixType? requiredType)
        {
            if (item == null || item.BaseDefinition == null)
            {
                return false;
            }

            AffixDefinition selected = PickAffix(item, affixPool, random, requiredType);

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

            int prefixCount = item.Prefixes.Count;
            int suffixCount = item.Suffixes.Count;
            int total = prefixCount + suffixCount;

            if (total <= 0)
            {
                return false;
            }

            List<AffixInstance> previousAffixes = CollectAffixes(item);
            item.ClearAffixes();

            for (int i = 0; i < prefixCount; i++)
            {
                if (!AddRandomAffix(item, affixPool, random, AffixType.Prefix))
                {
                    RestoreAffixes(item, previousAffixes);
                    return false;
                }
            }

            for (int i = 0; i < suffixCount; i++)
            {
                if (!AddRandomAffix(item, affixPool, random, AffixType.Suffix))
                {
                    RestoreAffixes(item, previousAffixes);
                    return false;
                }
            }

            return true;
        }

        public static bool RemoveAndRerollAffix(ItemInstance item, AffixInstance target, IReadOnlyList<AffixDefinition> affixPool, System.Random random)
        {
            if (item == null || target == null || target.Definition == null || !item.RemoveAffix(target))
            {
                return false;
            }

            if (AddRandomAffix(item, affixPool, random, target.Definition.AffixType))
            {
                return true;
            }

            item.TryAddAffix(target);
            return false;
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

            if (SumValue(rerolled) <= SumValue(target) || !item.RemoveAffix(target))
            {
                return false;
            }

            if (item.TryAddAffix(rerolled))
            {
                return true;
            }

            item.TryAddAffix(target);
            return false;
        }

        static AffixDefinition PickAffix(
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            System.Random random,
            AffixType? requiredType)
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

                if (affix == null
                    || affix.Weight <= 0
                    || requiredType.HasValue && affix.AffixType != requiredType.Value
                    || !affix.CanApplyTo(item.Tags, item.ItemLevel))
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

        static List<AffixInstance> CollectAffixes(ItemInstance item)
        {
            List<AffixInstance> affixes = new List<AffixInstance>(item.Prefixes.Count + item.Suffixes.Count);
            affixes.AddRange(item.Prefixes);
            affixes.AddRange(item.Suffixes);
            return affixes;
        }

        static void RestoreAffixes(ItemInstance item, IReadOnlyList<AffixInstance> affixes)
        {
            item.ClearAffixes();

            for (int i = 0; i < affixes.Count; i++)
            {
                item.TryAddAffix(affixes[i]);
            }
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
