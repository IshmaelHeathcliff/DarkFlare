using System;
using System.Collections.Generic;

namespace DarkFlare
{
    static class AffixGenerationUtility
    {
        public static bool TryGenerate(
            ItemInstance item,
            IEnumerable<AffixDefinition> affixPool,
            ItemRarity targetRarity,
            IReadOnlyList<AffixInstance> preservedPrefixes,
            IReadOnlyList<AffixInstance> preservedSuffixes,
            IReadOnlyList<AffixType?> requestedTypes,
            int selectionSeed,
            int valueSeed,
            out List<AffixInstance> generated)
        {
            generated = new List<AffixInstance>();

            if (item == null
                || item.BaseDefinition == null
                || preservedPrefixes == null
                || preservedSuffixes == null
                || requestedTypes == null)
            {
                return false;
            }

            int prefixCount = preservedPrefixes.Count;
            int suffixCount = preservedSuffixes.Count;

            if (!ItemRarityRules.IsWithinCapacity(targetRarity, prefixCount, suffixCount))
            {
                return false;
            }

            List<AffixDefinition> definitions = CollectDefinitions(affixPool);
            HashSet<AffixDefinition> usedDefinitions = new HashSet<AffixDefinition>();
            HashSet<string> usedGroups = new HashSet<string>(StringComparer.Ordinal);
            CollectUsed(preservedPrefixes, usedDefinitions, usedGroups);
            CollectUsed(preservedSuffixes, usedDefinitions, usedGroups);
            List<AffixDefinition> selected = new List<AffixDefinition>(requestedTypes.Count);
            System.Random selectionRandom = new System.Random(selectionSeed);

            if (!TrySelect(
                    item,
                    targetRarity,
                    definitions,
                    requestedTypes,
                    0,
                    prefixCount,
                    suffixCount,
                    usedDefinitions,
                    usedGroups,
                    selected,
                    selectionRandom))
            {
                return false;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                AffixDefinition definition = selected[i];
                int instanceSeed = DeriveValueSeed(valueSeed, definition, i);
                generated.Add(definition.CreateInstance(new System.Random(instanceSeed)));
            }

            return true;
        }

        static List<AffixDefinition> CollectDefinitions(IEnumerable<AffixDefinition> affixPool)
        {
            List<AffixDefinition> definitions = new List<AffixDefinition>();

            if (affixPool == null)
            {
                return definitions;
            }

            foreach (AffixDefinition definition in affixPool)
            {
                if (definition != null && definition.Weight > 0)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        static void CollectUsed(
            IReadOnlyList<AffixInstance> affixes,
            HashSet<AffixDefinition> usedDefinitions,
            HashSet<string> usedGroups)
        {
            for (int i = 0; i < affixes.Count; i++)
            {
                AffixDefinition definition = affixes[i] != null ? affixes[i].Definition : null;

                if (definition == null)
                {
                    continue;
                }

                usedDefinitions.Add(definition);

                if (!string.IsNullOrWhiteSpace(definition.GroupId))
                {
                    usedGroups.Add(definition.GroupId);
                }
            }
        }

        static bool TrySelect(
            ItemInstance item,
            ItemRarity targetRarity,
            IReadOnlyList<AffixDefinition> definitions,
            IReadOnlyList<AffixType?> requestedTypes,
            int requestIndex,
            int prefixCount,
            int suffixCount,
            HashSet<AffixDefinition> usedDefinitions,
            HashSet<string> usedGroups,
            List<AffixDefinition> selected,
            System.Random random)
        {
            if (requestIndex >= requestedTypes.Count)
            {
                return true;
            }

            AffixType? requestedType = requestedTypes[requestIndex];
            List<AffixDefinition> candidates = new List<AffixDefinition>();
            ItemAffixLimits limits = ItemRarityRules.GetLimits(targetRarity);

            for (int i = 0; i < definitions.Count; i++)
            {
                AffixDefinition definition = definitions[i];

                if (definition.AffixType != AffixType.Prefix && definition.AffixType != AffixType.Suffix)
                {
                    continue;
                }

                if (requestedType.HasValue && definition.AffixType != requestedType.Value)
                {
                    continue;
                }

                if (definition.AffixType == AffixType.Prefix && prefixCount >= limits.MaxPrefixCount
                    || definition.AffixType == AffixType.Suffix && suffixCount >= limits.MaxSuffixCount)
                {
                    continue;
                }

                if (usedDefinitions.Contains(definition)
                    || !string.IsNullOrWhiteSpace(definition.GroupId) && usedGroups.Contains(definition.GroupId)
                    || !definition.CanApplyTo(item.Tags, item.ItemLevel))
                {
                    continue;
                }

                candidates.Add(definition);
            }

            List<AffixDefinition> ordered = CreateWeightedOrder(candidates, random);

            for (int i = 0; i < ordered.Count; i++)
            {
                AffixDefinition candidate = ordered[i];
                bool hasGroup = !string.IsNullOrWhiteSpace(candidate.GroupId);
                usedDefinitions.Add(candidate);

                if (hasGroup)
                {
                    usedGroups.Add(candidate.GroupId);
                }

                selected.Add(candidate);
                int nextPrefixCount = prefixCount + (candidate.AffixType == AffixType.Prefix ? 1 : 0);
                int nextSuffixCount = suffixCount + (candidate.AffixType == AffixType.Suffix ? 1 : 0);

                if (TrySelect(
                        item,
                        targetRarity,
                        definitions,
                        requestedTypes,
                        requestIndex + 1,
                        nextPrefixCount,
                        nextSuffixCount,
                        usedDefinitions,
                        usedGroups,
                        selected,
                        random))
                {
                    return true;
                }

                selected.RemoveAt(selected.Count - 1);
                usedDefinitions.Remove(candidate);

                if (hasGroup)
                {
                    usedGroups.Remove(candidate.GroupId);
                }
            }

            return false;
        }

        static List<AffixDefinition> CreateWeightedOrder(
            IReadOnlyList<AffixDefinition> candidates,
            System.Random random)
        {
            List<AffixDefinition> remaining = new List<AffixDefinition>(candidates);
            List<AffixDefinition> ordered = new List<AffixDefinition>(candidates.Count);

            while (remaining.Count > 0)
            {
                int totalWeight = 0;

                for (int i = 0; i < remaining.Count; i++)
                {
                    totalWeight += remaining[i].Weight;
                }

                if (totalWeight <= 0)
                {
                    break;
                }

                int roll = random.Next(0, totalWeight);
                int selectedIndex = remaining.Count - 1;

                for (int i = 0; i < remaining.Count; i++)
                {
                    roll -= remaining[i].Weight;

                    if (roll < 0)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                ordered.Add(remaining[selectedIndex]);
                remaining.RemoveAt(selectedIndex);
            }

            return ordered;
        }

        static int DeriveValueSeed(int rootSeed, AffixDefinition definition, int slotIndex)
        {
            unchecked
            {
                uint value = (uint)rootSeed;
                value ^= 0x9E3779B9u + (uint)(slotIndex + 1) * 0x85EBCA6Bu;
                value = (value ^ value >> 16) * 0x7FEB352Du;
                value ^= (uint)StableHash(definition != null ? definition.Id : string.Empty) * 0x846CA68Bu;
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
