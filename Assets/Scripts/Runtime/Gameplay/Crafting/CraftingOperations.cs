using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public static class CraftingOperations
    {
        readonly struct AffixCountPlan
        {
            public int PrefixCount { get; }

            public int SuffixCount { get; }

            public int TotalCount => PrefixCount + SuffixCount;

            public AffixCountPlan(int prefixCount, int suffixCount)
            {
                PrefixCount = prefixCount;
                SuffixCount = suffixCount;
            }
        }

        readonly struct AffixLocation
        {
            public AffixInstance Affix { get; }

            public AffixType Type { get; }

            public int Index { get; }

            public AffixLocation(AffixInstance affix, AffixType type, int index)
            {
                Affix = affix;
                Type = type;
                Index = index;
            }
        }

        public static CraftingFailureReason Evaluate(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool)
        {
            return TryCreateState(
                operation,
                scope,
                item,
                affixPool,
                new CraftingRandomSeeds(0),
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        public static CraftingResult TryCraft(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            int rootSeed)
        {
            if (item == null)
            {
                return CraftingResult.Failure(
                    operation,
                    scope,
                    null,
                    CraftingFailureReason.ItemMissing,
                    0,
                    0,
                    rootSeed);
            }

            int expectedRevision = item.Revision;
            ItemRarity previousRarity = item.Rarity;
            List<AffixInstance> previousPrefixes = new List<AffixInstance>(item.Prefixes);
            List<AffixInstance> previousSuffixes = new List<AffixInstance>(item.Suffixes);
            CraftingFailureReason failure = TryCreateState(
                operation,
                scope,
                item,
                affixPool,
                new CraftingRandomSeeds(rootSeed),
                out ItemRarity newRarity,
                out List<AffixInstance> newPrefixes,
                out List<AffixInstance> newSuffixes,
                out AffixInstance previousAffectedAffix,
                out AffixInstance currentAffectedAffix);

            if (failure != CraftingFailureReason.None)
            {
                return CraftingResult.Failure(operation, scope, item, failure, 0, 0, rootSeed);
            }

            if (!item.TryApplyAffixState(expectedRevision, newRarity, newPrefixes, newSuffixes))
            {
                return CraftingResult.Failure(
                    operation,
                    scope,
                    item,
                    CraftingFailureReason.CommitFailed,
                    0,
                    0,
                    rootSeed);
            }

            return new CraftingResult(
                true,
                CraftingFailureReason.None,
                operation,
                scope,
                item,
                rootSeed,
                previousRarity,
                newRarity,
                previousPrefixes,
                previousSuffixes,
                newPrefixes,
                newSuffixes,
                previousAffectedAffix,
                currentAffectedAffix,
                0,
                0);
        }

        static CraftingFailureReason TryCreateState(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            CraftingRandomSeeds seeds,
            out ItemRarity newRarity,
            out List<AffixInstance> newPrefixes,
            out List<AffixInstance> newSuffixes,
            out AffixInstance previousAffectedAffix,
            out AffixInstance currentAffectedAffix)
        {
            newRarity = item != null ? item.Rarity : ItemRarity.Normal;
            newPrefixes = item != null
                ? new List<AffixInstance>(item.Prefixes)
                : new List<AffixInstance>();
            newSuffixes = item != null
                ? new List<AffixInstance>(item.Suffixes)
                : new List<AffixInstance>();
            previousAffectedAffix = null;
            currentAffectedAffix = null;

            if (item == null || item.BaseDefinition == null)
            {
                return CraftingFailureReason.ItemMissing;
            }
            if (!item.BaseDefinition.IsEquipment)
            {
                return CraftingFailureReason.NotEquipment;
            }

            if (!Enum.IsDefined(typeof(CraftOperation), operation))
            {
                return CraftingFailureReason.InvalidOperation;
            }

            if (CraftingOperationRules.UsesAffixScope(operation)
                && !Enum.IsDefined(typeof(CraftingAffixScope), scope))
            {
                return CraftingFailureReason.InvalidScope;
            }

            switch (operation)
            {
                case CraftOperation.UpgradeRarity:
                    return TryUpgradeRarity(
                        item,
                        affixPool,
                        seeds,
                        ref newRarity,
                        newPrefixes,
                        newSuffixes);
                case CraftOperation.ResetToNormal:
                    if (item.Rarity == ItemRarity.Normal && item.Prefixes.Count == 0 && item.Suffixes.Count == 0)
                    {
                        return CraftingFailureReason.NoChange;
                    }

                    newRarity = ItemRarity.Normal;
                    newPrefixes.Clear();
                    newSuffixes.Clear();
                    return CraftingFailureReason.None;
                case CraftOperation.RerollAffixes:
                    return TryRerollAffixes(
                        item,
                        affixPool,
                        scope,
                        seeds,
                        newPrefixes,
                        newSuffixes);
                case CraftOperation.AddAffix:
                    return TryAddAffix(
                        item,
                        affixPool,
                        scope,
                        seeds,
                        newPrefixes,
                        newSuffixes,
                        out currentAffectedAffix);
                case CraftOperation.RemoveAffix:
                    return TryRemoveAffix(
                        scope,
                        seeds,
                        newPrefixes,
                        newSuffixes,
                        out previousAffectedAffix);
                case CraftOperation.RerollAffixValues:
                    return TryRerollAffixValues(
                        scope,
                        seeds,
                        newPrefixes,
                        newSuffixes,
                        out previousAffectedAffix,
                        out currentAffectedAffix);
                default:
                    return CraftingFailureReason.InvalidOperation;
            }
        }

        static CraftingFailureReason TryUpgradeRarity(
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            CraftingRandomSeeds seeds,
            ref ItemRarity newRarity,
            List<AffixInstance> newPrefixes,
            List<AffixInstance> newSuffixes)
        {
            if (!ItemRarityRules.TryGetNextRarity(item.Rarity, out ItemRarity targetRarity))
            {
                return CraftingFailureReason.MaximumRarity;
            }

            ItemAffixLimits targetLimits = ItemRarityRules.GetLimits(targetRarity);
            int currentCount = newPrefixes.Count + newSuffixes.Count;
            int minimumGenerated;
            int maximumGenerated;

            if (item.Rarity == ItemRarity.Normal)
            {
                minimumGenerated = targetLimits.MinimumTotalCount;
                maximumGenerated = targetLimits.MaximumTotalCount;
            }
            else if (item.Rarity == ItemRarity.Magic)
            {
                minimumGenerated = Math.Max(1, targetLimits.MinimumTotalCount - currentCount);
                maximumGenerated = minimumGenerated;
            }
            else
            {
                minimumGenerated = Math.Max(0, targetLimits.MinimumTotalCount - currentCount);
                maximumGenerated = minimumGenerated;
            }

            if (!TryGeneratePlan(
                    item,
                    affixPool,
                    targetRarity,
                    newPrefixes,
                    newSuffixes,
                    minimumGenerated,
                    maximumGenerated,
                    null,
                    true,
                    seeds,
                    out List<AffixInstance> generated))
            {
                return CraftingFailureReason.CannotBuildCompleteResult;
            }

            AppendByType(generated, newPrefixes, newSuffixes);
            newRarity = targetRarity;
            return CraftingFailureReason.None;
        }

        static CraftingFailureReason TryRerollAffixes(
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            CraftingAffixScope scope,
            CraftingRandomSeeds seeds,
            List<AffixInstance> newPrefixes,
            List<AffixInstance> newSuffixes)
        {
            if (item.Rarity == ItemRarity.Normal)
            {
                return CraftingFailureReason.NoCapacity;
            }

            ItemAffixLimits limits = ItemRarityRules.GetLimits(item.Rarity);
            List<AffixInstance> preservedPrefixes;
            List<AffixInstance> preservedSuffixes;
            int minimumGenerated;
            int maximumGenerated;
            AffixType? forcedType;

            if (scope == CraftingAffixScope.Any)
            {
                preservedPrefixes = new List<AffixInstance>();
                preservedSuffixes = new List<AffixInstance>();
                minimumGenerated = limits.MinimumTotalCount;
                maximumGenerated = limits.MaximumTotalCount;
                forcedType = null;
            }
            else if (scope == CraftingAffixScope.Prefix)
            {
                preservedPrefixes = new List<AffixInstance>();
                preservedSuffixes = new List<AffixInstance>(newSuffixes);
                minimumGenerated = Math.Max(1, limits.MinimumTotalCount - preservedSuffixes.Count);
                maximumGenerated = Math.Min(limits.MaxPrefixCount, limits.MaximumTotalCount - preservedSuffixes.Count);
                forcedType = AffixType.Prefix;
            }
            else
            {
                preservedPrefixes = new List<AffixInstance>(newPrefixes);
                preservedSuffixes = new List<AffixInstance>();
                minimumGenerated = Math.Max(1, limits.MinimumTotalCount - preservedPrefixes.Count);
                maximumGenerated = Math.Min(limits.MaxSuffixCount, limits.MaximumTotalCount - preservedPrefixes.Count);
                forcedType = AffixType.Suffix;
            }

            if (minimumGenerated > maximumGenerated
                || !TryGeneratePlan(
                    item,
                    affixPool,
                    item.Rarity,
                    preservedPrefixes,
                    preservedSuffixes,
                    minimumGenerated,
                    maximumGenerated,
                    forcedType,
                    true,
                    seeds,
                    out List<AffixInstance> generated))
            {
                return CraftingFailureReason.CannotBuildCompleteResult;
            }

            newPrefixes.Clear();
            newPrefixes.AddRange(preservedPrefixes);
            newSuffixes.Clear();
            newSuffixes.AddRange(preservedSuffixes);
            AppendByType(generated, newPrefixes, newSuffixes);
            return CraftingFailureReason.None;
        }

        static CraftingFailureReason TryAddAffix(
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            CraftingAffixScope scope,
            CraftingRandomSeeds seeds,
            List<AffixInstance> newPrefixes,
            List<AffixInstance> newSuffixes,
            out AffixInstance addedAffix)
        {
            addedAffix = null;
            ItemAffixLimits limits = ItemRarityRules.GetLimits(item.Rarity);
            bool hasCapacity = scope == CraftingAffixScope.Any
                ? newPrefixes.Count < limits.MaxPrefixCount || newSuffixes.Count < limits.MaxSuffixCount
                : scope == CraftingAffixScope.Prefix
                    ? newPrefixes.Count < limits.MaxPrefixCount
                    : newSuffixes.Count < limits.MaxSuffixCount;

            if (!hasCapacity)
            {
                return CraftingFailureReason.NoCapacity;
            }

            AffixType? requestedType = scope == CraftingAffixScope.Any
                ? null
                : scope == CraftingAffixScope.Prefix
                    ? AffixType.Prefix
                    : AffixType.Suffix;
            List<AffixType?> requestedTypes = new List<AffixType?> { requestedType };

            if (!AffixGenerationUtility.TryGenerate(
                    item,
                    affixPool,
                    item.Rarity,
                    newPrefixes,
                    newSuffixes,
                    requestedTypes,
                    seeds.DefinitionSeed,
                    seeds.ValueSeed,
                    out List<AffixInstance> generated)
                || generated.Count != 1)
            {
                return CraftingFailureReason.NoLegalAffix;
            }

            addedAffix = generated[0];
            AppendByType(generated, newPrefixes, newSuffixes);
            return CraftingFailureReason.None;
        }

        static CraftingFailureReason TryRemoveAffix(
            CraftingAffixScope scope,
            CraftingRandomSeeds seeds,
            List<AffixInstance> newPrefixes,
            List<AffixInstance> newSuffixes,
            out AffixInstance removedAffix)
        {
            removedAffix = null;
            List<AffixLocation> candidates = CollectLocations(scope, newPrefixes, newSuffixes, false);

            if (candidates.Count == 0)
            {
                return CraftingFailureReason.ScopeHasNoAffix;
            }

            AffixLocation selected = candidates[new System.Random(seeds.TargetSeed).Next(0, candidates.Count)];
            removedAffix = selected.Affix;

            if (selected.Type == AffixType.Prefix)
            {
                newPrefixes.RemoveAt(selected.Index);
            }
            else
            {
                newSuffixes.RemoveAt(selected.Index);
            }

            return CraftingFailureReason.None;
        }

        static CraftingFailureReason TryRerollAffixValues(
            CraftingAffixScope scope,
            CraftingRandomSeeds seeds,
            List<AffixInstance> newPrefixes,
            List<AffixInstance> newSuffixes,
            out AffixInstance previousAffix,
            out AffixInstance currentAffix)
        {
            previousAffix = null;
            currentAffix = null;
            List<AffixLocation> candidates = CollectLocations(scope, newPrefixes, newSuffixes, true);

            if (candidates.Count == 0)
            {
                return CraftingFailureReason.NoVariableAffix;
            }

            AffixLocation selected = candidates[new System.Random(seeds.TargetSeed).Next(0, candidates.Count)];
            int valueSeed = seeds.GetTargetValueSeed(selected.Affix.Definition, selected.Type, selected.Index);
            AffixInstance rerolled = selected.Affix.Definition.CreateInstance(new System.Random(valueSeed));
            previousAffix = selected.Affix;
            currentAffix = rerolled;

            if (selected.Type == AffixType.Prefix)
            {
                newPrefixes[selected.Index] = rerolled;
            }
            else
            {
                newSuffixes[selected.Index] = rerolled;
            }

            return CraftingFailureReason.None;
        }

        static bool TryGeneratePlan(
            ItemInstance item,
            IReadOnlyList<AffixDefinition> affixPool,
            ItemRarity targetRarity,
            IReadOnlyList<AffixInstance> preservedPrefixes,
            IReadOnlyList<AffixInstance> preservedSuffixes,
            int minimumGenerated,
            int maximumGenerated,
            AffixType? forcedType,
            bool requireNormalTotal,
            CraftingRandomSeeds seeds,
            out List<AffixInstance> generated)
        {
            generated = new List<AffixInstance>();
            List<AffixCountPlan> feasiblePlans = new List<AffixCountPlan>();

            for (int total = minimumGenerated; total <= maximumGenerated; total++)
            {
                for (int prefixCount = 0; prefixCount <= total; prefixCount++)
                {
                    int suffixCount = total - prefixCount;

                    if (forcedType == AffixType.Prefix && suffixCount != 0
                        || forcedType == AffixType.Suffix && prefixCount != 0)
                    {
                        continue;
                    }

                    int finalPrefixCount = preservedPrefixes.Count + prefixCount;
                    int finalSuffixCount = preservedSuffixes.Count + suffixCount;

                    if (!ItemRarityRules.IsWithinCapacity(targetRarity, finalPrefixCount, finalSuffixCount)
                        || requireNormalTotal
                        && !ItemRarityRules.IsNormalGenerationValid(
                            targetRarity,
                            finalPrefixCount,
                            finalSuffixCount))
                    {
                        continue;
                    }

                    List<AffixType?> requestedTypes = BuildRequestedTypes(prefixCount, suffixCount);

                    if (AffixGenerationUtility.TryGenerate(
                            item,
                            affixPool,
                            targetRarity,
                            preservedPrefixes,
                            preservedSuffixes,
                            requestedTypes,
                            seeds.DefinitionSeed,
                            seeds.ValueSeed,
                            out _))
                    {
                        feasiblePlans.Add(new AffixCountPlan(prefixCount, suffixCount));
                    }
                }
            }

            if (feasiblePlans.Count == 0)
            {
                return false;
            }

            List<int> feasibleTotals = new List<int>();

            for (int i = 0; i < feasiblePlans.Count; i++)
            {
                if (!feasibleTotals.Contains(feasiblePlans[i].TotalCount))
                {
                    feasibleTotals.Add(feasiblePlans[i].TotalCount);
                }
            }

            int selectedTotal = feasibleTotals[new System.Random(seeds.CountSeed).Next(0, feasibleTotals.Count)];
            List<AffixCountPlan> matchingPlans = new List<AffixCountPlan>();

            for (int i = 0; i < feasiblePlans.Count; i++)
            {
                if (feasiblePlans[i].TotalCount == selectedTotal)
                {
                    matchingPlans.Add(feasiblePlans[i]);
                }
            }

            AffixCountPlan selectedPlan = matchingPlans[
                new System.Random(seeds.DistributionSeed).Next(0, matchingPlans.Count)];
            return AffixGenerationUtility.TryGenerate(
                item,
                affixPool,
                targetRarity,
                preservedPrefixes,
                preservedSuffixes,
                BuildRequestedTypes(selectedPlan.PrefixCount, selectedPlan.SuffixCount),
                seeds.DefinitionSeed,
                seeds.ValueSeed,
                out generated);
        }

        static List<AffixType?> BuildRequestedTypes(int prefixCount, int suffixCount)
        {
            List<AffixType?> requestedTypes = new List<AffixType?>(prefixCount + suffixCount);

            for (int i = 0; i < prefixCount; i++)
            {
                requestedTypes.Add(AffixType.Prefix);
            }

            for (int i = 0; i < suffixCount; i++)
            {
                requestedTypes.Add(AffixType.Suffix);
            }

            return requestedTypes;
        }

        static void AppendByType(
            IReadOnlyList<AffixInstance> generated,
            List<AffixInstance> prefixes,
            List<AffixInstance> suffixes)
        {
            for (int i = 0; i < generated.Count; i++)
            {
                if (generated[i].Definition.AffixType == AffixType.Prefix)
                {
                    prefixes.Add(generated[i]);
                }
                else
                {
                    suffixes.Add(generated[i]);
                }
            }
        }

        static List<AffixLocation> CollectLocations(
            CraftingAffixScope scope,
            IReadOnlyList<AffixInstance> prefixes,
            IReadOnlyList<AffixInstance> suffixes,
            bool requireVariableValue)
        {
            List<AffixLocation> locations = new List<AffixLocation>();

            if (scope != CraftingAffixScope.Suffix)
            {
                AddLocations(prefixes, AffixType.Prefix, requireVariableValue, locations);
            }

            if (scope != CraftingAffixScope.Prefix)
            {
                AddLocations(suffixes, AffixType.Suffix, requireVariableValue, locations);
            }

            return locations;
        }

        static void AddLocations(
            IReadOnlyList<AffixInstance> affixes,
            AffixType type,
            bool requireVariableValue,
            List<AffixLocation> locations)
        {
            for (int i = 0; i < affixes.Count; i++)
            {
                AffixInstance affix = affixes[i];

                if (affix == null
                    || affix.Definition == null
                    || requireVariableValue && !HasVariableValue(affix.Definition))
                {
                    continue;
                }

                locations.Add(new AffixLocation(affix, type, i));
            }
        }

        static bool HasVariableValue(AffixDefinition definition)
        {
            for (int i = 0; i < definition.Modifiers.Count; i++)
            {
                if (Math.Abs(
                        definition.Modifiers[i].ValueRange.x
                        - definition.Modifiers[i].ValueRange.y)
                    > float.Epsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
