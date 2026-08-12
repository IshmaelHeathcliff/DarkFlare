using System.Collections.Generic;

namespace DarkFlare
{
    public static class MonsterAffixGenerator
    {
        public static List<MonsterAffixInstance> Generate(
            IReadOnlyList<MonsterAffixDefinition> pool,
            int minimumCount,
            int maximumCount,
            MonsterInstanceRandomSeeds seeds)
        {
            List<MonsterAffixDefinition> candidates = CollectCandidates(pool);
            List<MonsterAffixInstance> result = new List<MonsterAffixInstance>();

            if (candidates.Count == 0 || maximumCount <= 0)
            {
                return result;
            }

            int minimum = System.Math.Max(0, minimumCount);
            int maximum = System.Math.Max(minimum, maximumCount);
            System.Random countRandom = new System.Random(seeds.AffixCountSeed);
            int targetCount = minimum == maximum
                ? minimum
                : countRandom.Next(minimum, maximum + 1);
            System.Random selectionRandom = new System.Random(seeds.AffixSelectionSeed);

            while (result.Count < targetCount && candidates.Count > 0)
            {
                MonsterAffixDefinition selected = PickWeighted(candidates, selectionRandom);

                if (selected == null)
                {
                    break;
                }

                int valueSeed = seeds.GetAffixValueSeed(selected.Id);
                result.Add(selected.CreateInstance(valueSeed));
                RemoveGroup(candidates, selected);
            }

            return result;
        }

        static List<MonsterAffixDefinition> CollectCandidates(IReadOnlyList<MonsterAffixDefinition> pool)
        {
            List<MonsterAffixDefinition> result = new List<MonsterAffixDefinition>();

            if (pool == null)
            {
                return result;
            }

            HashSet<MonsterAffixDefinition> seen = new HashSet<MonsterAffixDefinition>();

            for (int i = 0; i < pool.Count; i++)
            {
                MonsterAffixDefinition definition = pool[i];

                if (definition != null && definition.Weight > 0 && seen.Add(definition))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        static MonsterAffixDefinition PickWeighted(
            IReadOnlyList<MonsterAffixDefinition> candidates,
            System.Random random)
        {
            long totalWeight = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].Weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            double roll = random.NextDouble() * totalWeight;
            long accumulated = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += candidates[i].Weight;

                if (roll < accumulated)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        static void RemoveGroup(
            List<MonsterAffixDefinition> candidates,
            MonsterAffixDefinition selected)
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                MonsterAffixDefinition candidate = candidates[i];
                bool sameDefinition = candidate == selected;
                bool sameGroup = !string.IsNullOrWhiteSpace(selected.GroupId)
                    && candidate.GroupId == selected.GroupId;

                if (sameDefinition || sameGroup)
                {
                    candidates.RemoveAt(i);
                }
            }
        }
    }
}
