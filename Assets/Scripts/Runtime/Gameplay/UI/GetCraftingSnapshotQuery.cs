using System.Collections.Generic;
using System.Globalization;

namespace DarkFlare
{
    public readonly struct CraftingAffixSnapshot
    {
        public AffixInstance Affix { get; }

        public AffixType Type { get; }

        public string DisplayName { get; }

        public string ModifierSummary { get; }

        public float TotalValue { get; }

        public CraftingAffixSnapshot(AffixInstance affix)
        {
            Affix = affix;
            AffixDefinition definition = affix.Definition;
            Type = definition != null ? definition.AffixType : default;
            DisplayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : "未命名词缀";
            ModifierSummary = DescribeModifiers(affix.Modifiers);
            TotalValue = SumValues(affix.Modifiers);
        }

        static string DescribeModifiers(IReadOnlyList<ModifierInstance> modifiers)
        {
            if (modifiers.Count == 0)
            {
                return "无数值修改";
            }

            List<string> descriptions = new List<string>(modifiers.Count);

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierInstance modifier = modifiers[i];
                string stat = string.IsNullOrWhiteSpace(modifier.StatId) ? "未配置属性" : modifier.StatId;
                string value = modifier.Value.ToString("0.##", CultureInfo.InvariantCulture);

                if (modifier.Operation == ModifierOperation.Increase || modifier.Operation == ModifierOperation.More)
                {
                    descriptions.Add($"{stat} +{value}%");
                }
                else if (modifier.Operation == ModifierOperation.Conversion)
                {
                    descriptions.Add($"{modifier.FromDamageType} → {modifier.ToDamageType} {value}%");
                }
                else if (modifier.Operation == ModifierOperation.GainAsExtra)
                {
                    descriptions.Add($"额外获得 {modifier.FromDamageType} → {modifier.ToDamageType} {value}%");
                }
                else if (modifier.Operation == ModifierOperation.Override)
                {
                    descriptions.Add($"{stat} = {value}");
                }
                else
                {
                    descriptions.Add($"{stat} +{value}");
                }
            }

            return string.Join(" · ", descriptions);
        }

        static float SumValues(IReadOnlyList<ModifierInstance> modifiers)
        {
            float total = 0f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                total += modifiers[i].Value;
            }

            return total;
        }
    }

    public readonly struct CraftingItemSnapshot
    {
        public ItemInstance Item { get; }

        public string DisplayName { get; }

        public ItemType Type { get; }

        public ItemRarity Rarity { get; }

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
            ItemBaseDefinition definition = item.BaseDefinition;
            DisplayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : item.InstanceId;
            Type = definition != null ? definition.ItemType : default;
            Rarity = item.Rarity;
            Value = ItemValueCalculator.GetValue(item);
            SellPrice = sellPrice;
            PrefixCount = item.Prefixes.Count;
            SuffixCount = item.Suffixes.Count;
            MaxPrefixCount = definition != null ? definition.MaxPrefixCount : 0;
            MaxSuffixCount = definition != null ? definition.MaxSuffixCount : 0;
            List<CraftingAffixSnapshot> affixes = new List<CraftingAffixSnapshot>(PrefixCount + SuffixCount);

            for (int i = 0; i < item.Prefixes.Count; i++)
            {
                affixes.Add(new CraftingAffixSnapshot(item.Prefixes[i]));
            }

            for (int i = 0; i < item.Suffixes.Count; i++)
            {
                affixes.Add(new CraftingAffixSnapshot(item.Suffixes[i]));
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
