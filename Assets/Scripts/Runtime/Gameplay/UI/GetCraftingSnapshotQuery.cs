using System.Collections.Generic;

namespace DarkFlare
{
    public readonly struct CraftingActionSnapshot
    {
        public CraftOperation Operation { get; }

        public CraftingAffixScope Scope { get; }

        public CraftingFailureReason FailureReason { get; }

        public int Cost { get; }

        public ItemBaseDefinition Material { get; }
        public int MaterialCost { get; }
        public int MaterialOwned { get; }

        public bool IsAvailable => FailureReason == CraftingFailureReason.None;

        public CraftingActionSnapshot(
            CraftOperation operation,
            CraftingAffixScope scope,
            CraftingEvaluation evaluation)
        {
            Operation = operation;
            Scope = scope;
            FailureReason = evaluation.FailureReason;
            Cost = evaluation.Cost;
            Material = evaluation.Material;
            MaterialCost = evaluation.MaterialCost;
            MaterialOwned = evaluation.MaterialOwned;
        }
    }

    public readonly struct CraftingItemSnapshot
    {
        public ItemInstance Item { get; }

        public ItemType Type { get; }

        public ItemRarity Rarity { get; }

        public ItemDetailSnapshot Detail { get; }

        public int Value { get; }

        public int SellPrice { get; }

        public int PrefixCount { get; }

        public int SuffixCount { get; }

        public int MaxPrefixCount { get; }

        public int MaxSuffixCount { get; }

        public int MinimumTotalCount { get; }

        public int MaximumTotalCount { get; }

        public IReadOnlyList<CraftingActionSnapshot> Actions { get; }

        public CraftingItemSnapshot(
            ItemInstance item,
            int sellPrice,
            CraftingSystem crafting)
        {
            Item = item;
            Detail = ItemDetailSnapshotFactory.Create(item);
            Type = Detail.Type;
            Rarity = Detail.Rarity;
            Value = Detail.CalculatedValue;
            SellPrice = sellPrice;
            PrefixCount = item.Prefixes.Count;
            SuffixCount = item.Suffixes.Count;
            ItemAffixLimits limits = ItemRarityRules.GetLimits(item.Rarity);
            MaxPrefixCount = limits.MaxPrefixCount;
            MaxSuffixCount = limits.MaxSuffixCount;
            MinimumTotalCount = limits.MinimumTotalCount;
            MaximumTotalCount = limits.MaximumTotalCount;
            Actions = BuildActions(item, crafting);
        }

        public bool TryGetAction(
            CraftOperation operation,
            CraftingAffixScope scope,
            out CraftingActionSnapshot action)
        {
            for (int i = 0; i < Actions.Count; i++)
            {
                if (Actions[i].Operation == operation && Actions[i].Scope == scope)
                {
                    action = Actions[i];
                    return true;
                }
            }

            action = default;
            return false;
        }

        static IReadOnlyList<CraftingActionSnapshot> BuildActions(
            ItemInstance item,
            CraftingSystem crafting)
        {
            List<CraftingActionSnapshot> actions = new List<CraftingActionSnapshot>(14)
            {
                CreateAction(CraftOperation.UpgradeRarity, CraftingAffixScope.Any, item, crafting),
                CreateAction(CraftOperation.ResetToNormal, CraftingAffixScope.Any, item, crafting),
            };

            AddScopedActions(actions, CraftOperation.RerollAffixes, item, crafting);
            AddScopedActions(actions, CraftOperation.AddAffix, item, crafting);
            AddScopedActions(actions, CraftOperation.RemoveAffix, item, crafting);
            AddScopedActions(actions, CraftOperation.RerollAffixValues, item, crafting);
            return actions.AsReadOnly();
        }

        static void AddScopedActions(
            List<CraftingActionSnapshot> actions,
            CraftOperation operation,
            ItemInstance item,
            CraftingSystem crafting)
        {
            actions.Add(CreateAction(operation, CraftingAffixScope.Any, item, crafting));
            actions.Add(CreateAction(operation, CraftingAffixScope.Prefix, item, crafting));
            actions.Add(CreateAction(operation, CraftingAffixScope.Suffix, item, crafting));
        }

        static CraftingActionSnapshot CreateAction(
            CraftOperation operation,
            CraftingAffixScope scope,
            ItemInstance item,
            CraftingSystem crafting)
        {
            return new CraftingActionSnapshot(
                operation,
                scope,
                crafting.Evaluate(operation, scope, item));
        }
    }

    public readonly struct CraftingSnapshot
    {
        public bool IsConfigured { get; }

        public int Gold { get; }

        public IReadOnlyList<CraftingItemSnapshot> Items { get; }

        public CraftingSnapshot(
            bool isConfigured,
            int gold,
            IReadOnlyList<CraftingItemSnapshot> items)
        {
            IsConfigured = isConfigured;
            Gold = gold;
            Items = items;
        }
    }

    public class GetCraftingSnapshotQuery : AbstractQuery<CraftingSnapshot>
    {
        protected override CraftingSnapshot OnDo()
        {
            InventoryModel inventory = this.GetModel<InventoryModel>();
            CraftingSystem crafting = this.GetSystem<CraftingSystem>();
            TradingSystem trading = this.GetSystem<TradingSystem>();
            InventorySnapshot inventorySnapshot = this.SendQuery(new GetInventorySnapshotQuery());
            List<CraftingItemSnapshot> items = new List<CraftingItemSnapshot>(inventorySnapshot.Items.Count);

            for (int i = 0; i < inventorySnapshot.Items.Count; i++)
            {
                ItemInstance item = inventorySnapshot.Items[i].Item;
                if (!item.BaseDefinition.IsEquipment) { continue; }
                items.Add(new CraftingItemSnapshot(item, trading.GetSellPrice(item), crafting));
            }

            return new CraftingSnapshot(crafting.IsConfigured, inventory.Gold, items);
        }
    }
}
