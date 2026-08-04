using System.Collections.Generic;

namespace DarkFlare
{
    public readonly struct CraftingAffixSnapshot
    {
        public AffixInstance Affix { get; }

        public AffixType Type { get; }

        public string DisplayName { get; }

        public string ModifierSummary { get; }

        public float TotalValue { get; }

        public AffixDetailSnapshot Detail { get; }

        public CraftingAffixSnapshot(AffixDetailSnapshot detail)
        {
            Detail = detail;
            Affix = detail.Affix;
            Type = detail.Type;
            DisplayName = detail.DisplayName;
            ModifierSummary = detail.ModifierSummary;
            TotalValue = detail.TotalValue;
        }
    }

    public readonly struct CraftingItemSnapshot
    {
        public ItemInstance Item { get; }

        public string DisplayName { get; }

        public ItemType Type { get; }

        public ItemRarity Rarity { get; }

        public ItemDetailSnapshot Detail { get; }

        public int Value { get; }

        public int SellPrice { get; }

        public int PrefixCount { get; }

        public int SuffixCount { get; }

        public int MaxPrefixCount { get; }

        public int MaxSuffixCount { get; }

        public IReadOnlyList<CraftingAffixSnapshot> Affixes { get; }

        public bool HasAffixCapacity => PrefixCount < MaxPrefixCount || SuffixCount < MaxSuffixCount;

        public CraftingItemSnapshot(ItemInstance item, int sellPrice)
        {
            Item = item;
            Detail = ItemDetailSnapshotFactory.Create(item);
            ItemBaseDefinition definition = item.BaseDefinition;
            DisplayName = Detail.DisplayName;
            Type = Detail.Type;
            Rarity = Detail.Rarity;
            Value = Detail.CalculatedValue;
            SellPrice = sellPrice;
            PrefixCount = item.Prefixes.Count;
            SuffixCount = item.Suffixes.Count;
            MaxPrefixCount = definition != null ? definition.MaxPrefixCount : 0;
            MaxSuffixCount = definition != null ? definition.MaxSuffixCount : 0;
            List<CraftingAffixSnapshot> affixes = new List<CraftingAffixSnapshot>(PrefixCount + SuffixCount);

            for (int i = 0; i < Detail.Prefixes.Count; i++)
            {
                affixes.Add(new CraftingAffixSnapshot(Detail.Prefixes[i]));
            }

            for (int i = 0; i < Detail.Suffixes.Count; i++)
            {
                affixes.Add(new CraftingAffixSnapshot(Detail.Suffixes[i]));
            }

            Affixes = affixes;
        }
    }

    public readonly struct CraftingSnapshot
    {
        public bool IsConfigured { get; }

        public int Gold { get; }

        public IReadOnlyList<CraftingItemSnapshot> Items { get; }

        public int AddAffixCost { get; }

        public int RerollAllCost { get; }

        public int RemoveRerollCost { get; }

        public int UpgradeAffixCost { get; }

        public CraftingSnapshot(
            bool isConfigured,
            int gold,
            IReadOnlyList<CraftingItemSnapshot> items,
            int addAffixCost,
            int rerollAllCost,
            int removeRerollCost,
            int upgradeAffixCost)
        {
            IsConfigured = isConfigured;
            Gold = gold;
            Items = items;
            AddAffixCost = addAffixCost;
            RerollAllCost = rerollAllCost;
            RemoveRerollCost = removeRerollCost;
            UpgradeAffixCost = upgradeAffixCost;
        }

        public int GetCost(CraftOperation operation)
        {
            if (operation == CraftOperation.AddAffix)
            {
                return AddAffixCost;
            }

            if (operation == CraftOperation.RerollAll)
            {
                return RerollAllCost;
            }

            if (operation == CraftOperation.RemoveReroll)
            {
                return RemoveRerollCost;
            }

            return UpgradeAffixCost;
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
                items.Add(new CraftingItemSnapshot(item, trading.GetSellPrice(item)));
            }

            return new CraftingSnapshot(
                crafting.IsConfigured,
                inventory.Gold,
                items,
                crafting.GetCost(CraftOperation.AddAffix),
                crafting.GetCost(CraftOperation.RerollAll),
                crafting.GetCost(CraftOperation.RemoveReroll),
                crafting.GetCost(CraftOperation.UpgradeAffix));
        }
    }
}
