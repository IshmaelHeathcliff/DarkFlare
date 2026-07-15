using System.Collections.Generic;

namespace DarkFlare
{
    public static class ItemGenerator
    {
        public static ItemInstance Generate(
            ItemBaseDefinition baseDefinition,
            IEnumerable<AffixDefinition> affixPool,
            ItemGenerationOptions options)
        {
            if (baseDefinition == null)
            {
                throw new System.ArgumentNullException(nameof(baseDefinition));
            }

            System.Random random = new System.Random(options.Seed);
            ItemInstance item = baseDefinition.CreateInstance(options.InstanceId, options.ItemLevel, options.Seed, options.Rarity);
            int prefixCount = Clamp(options.PrefixCount, 0, baseDefinition.MaxPrefixCount);
            int suffixCount = Clamp(options.SuffixCount, 0, baseDefinition.MaxSuffixCount);

            AddAffixes(item, affixPool, AffixType.Prefix, prefixCount, random);
            AddAffixes(item, affixPool, AffixType.Suffix, suffixCount, random);

            return item;
        }

        static void AddAffixes(
            ItemInstance item,
            IEnumerable<AffixDefinition> affixPool,
            AffixType affixType,
            int count,
            System.Random random)
        {
            List<AffixDefinition> candidates = CollectCandidates(item, affixPool, affixType);

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                AffixDefinition selected = PickWeighted(candidates, random);

                if (selected == null)
                {
                    return;
                }

                item.TryAddAffix(selected.CreateInstance(random));
                candidates.Remove(selected);
                candidates = CollectCandidates(item, candidates, affixType);
            }
        }

        static List<AffixDefinition> CollectCandidates(ItemInstance item, IEnumerable<AffixDefinition> affixPool, AffixType affixType)
        {
            List<AffixDefinition> candidates = new List<AffixDefinition>();

            foreach (AffixDefinition affix in affixPool)
            {
                if (affix == null || affix.AffixType != affixType || affix.Weight <= 0)
                {
                    continue;
                }

                if (affix.CanApplyTo(item.Tags, item.ItemLevel))
                {
                    candidates.Add(affix);
                }
            }

            return candidates;
        }

        static AffixDefinition PickWeighted(List<AffixDefinition> candidates, System.Random random)
        {
            int totalWeight = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].Weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = random.Next(0, totalWeight);
            int current = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                current += candidates[i].Weight;

                if (roll < current)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
