using System.Collections.Generic;

namespace DarkFlare
{
    public enum CraftOperation
    {
        UpgradeRarity,
        ResetToNormal,
        RerollAffixes,
        AddAffix,
        RemoveAffix,
        RerollAffixValues
    }

    public enum CraftingAffixScope
    {
        Any,
        Prefix,
        Suffix
    }

    public enum CraftingFailureReason
    {
        None,
        NotConfigured,
        ItemMissing,
        ItemNotInInventory,
        InsufficientGold,
        InvalidOperation,
        InvalidScope,
        MaximumRarity,
        NoChange,
        NoCapacity,
        ScopeHasNoAffix,
        NoVariableAffix,
        NoLegalAffix,
        CannotBuildCompleteResult,
        CommitFailed,
        InsufficientMaterial,
        NotEquipment
    }

    public readonly struct CraftingEvaluation
    {
        public bool IsAvailable => FailureReason == CraftingFailureReason.None;

        public CraftingFailureReason FailureReason { get; }

        public int Cost { get; }

        public ItemBaseDefinition Material { get; }

        public int MaterialCost { get; }

        public int MaterialOwned { get; }

        public CraftingEvaluation(CraftingFailureReason failureReason, int cost,
            ItemBaseDefinition material = null, int materialCost = 0, int materialOwned = 0)
        {
            FailureReason = failureReason;
            Cost = cost;
            Material = material;
            MaterialCost = materialCost;
            MaterialOwned = materialOwned;
        }
    }

    public sealed class CraftingResult
    {
        public bool Succeeded { get; }

        public CraftingFailureReason FailureReason { get; }

        public CraftOperation Operation { get; }

        public CraftingAffixScope Scope { get; }

        public ItemInstance Item { get; }

        public int? RootSeed { get; }

        public ItemRarity PreviousRarity { get; }

        public ItemRarity CurrentRarity { get; }

        public IReadOnlyList<AffixInstance> PreviousPrefixes { get; }

        public IReadOnlyList<AffixInstance> PreviousSuffixes { get; }

        public IReadOnlyList<AffixInstance> CurrentPrefixes { get; }

        public IReadOnlyList<AffixInstance> CurrentSuffixes { get; }

        public AffixInstance PreviousAffectedAffix { get; }

        public AffixInstance CurrentAffectedAffix { get; }

        public int Cost { get; }

        public int GoldAfter { get; }

        internal CraftingResult(
            bool succeeded,
            CraftingFailureReason failureReason,
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item,
            int? rootSeed,
            ItemRarity previousRarity,
            ItemRarity currentRarity,
            IReadOnlyList<AffixInstance> previousPrefixes,
            IReadOnlyList<AffixInstance> previousSuffixes,
            IReadOnlyList<AffixInstance> currentPrefixes,
            IReadOnlyList<AffixInstance> currentSuffixes,
            AffixInstance previousAffectedAffix,
            AffixInstance currentAffectedAffix,
            int cost,
            int goldAfter)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Operation = operation;
            Scope = scope;
            Item = item;
            RootSeed = rootSeed;
            PreviousRarity = previousRarity;
            CurrentRarity = currentRarity;
            PreviousPrefixes = Copy(previousPrefixes);
            PreviousSuffixes = Copy(previousSuffixes);
            CurrentPrefixes = Copy(currentPrefixes);
            CurrentSuffixes = Copy(currentSuffixes);
            PreviousAffectedAffix = previousAffectedAffix;
            CurrentAffectedAffix = currentAffectedAffix;
            Cost = cost;
            GoldAfter = goldAfter;
        }

        internal CraftingResult WithEconomy(int cost, int goldAfter)
        {
            return new CraftingResult(
                Succeeded,
                FailureReason,
                Operation,
                Scope,
                Item,
                RootSeed,
                PreviousRarity,
                CurrentRarity,
                PreviousPrefixes,
                PreviousSuffixes,
                CurrentPrefixes,
                CurrentSuffixes,
                PreviousAffectedAffix,
                CurrentAffectedAffix,
                cost,
                goldAfter);
        }

        internal static CraftingResult Failure(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item,
            CraftingFailureReason reason,
            int cost,
            int goldAfter,
            int? rootSeed = null)
        {
            ItemRarity rarity = item != null ? item.Rarity : ItemRarity.Normal;
            IReadOnlyList<AffixInstance> prefixes = item != null
                ? item.Prefixes
                : new List<AffixInstance>();
            IReadOnlyList<AffixInstance> suffixes = item != null
                ? item.Suffixes
                : new List<AffixInstance>();
            return new CraftingResult(
                false,
                reason,
                operation,
                scope,
                item,
                rootSeed,
                rarity,
                rarity,
                prefixes,
                suffixes,
                prefixes,
                suffixes,
                null,
                null,
                cost,
                goldAfter);
        }

        static IReadOnlyList<AffixInstance> Copy(IReadOnlyList<AffixInstance> source)
        {
            return source != null
                ? new List<AffixInstance>(source).AsReadOnly()
                : new List<AffixInstance>().AsReadOnly();
        }
    }

    public static class CraftingOperationRules
    {
        public static bool UsesAffixScope(CraftOperation operation)
        {
            return operation == CraftOperation.RerollAffixes
                || operation == CraftOperation.AddAffix
                || operation == CraftOperation.RemoveAffix
                || operation == CraftOperation.RerollAffixValues;
        }
    }
}
