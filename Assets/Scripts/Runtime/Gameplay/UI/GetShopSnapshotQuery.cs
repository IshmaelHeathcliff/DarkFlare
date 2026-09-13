using System.Collections.Generic;

namespace DarkFlare
{
    public enum ShopItemSource
    {
        Merchant,
        Player
    }

    public readonly struct ShopItemSnapshot
    {
        public ItemInstance Item { get; }

        public ShopItemSource Source { get; }

        public ItemDetailSnapshot Detail { get; }

        public ItemType Type => Detail.Type;

        public ItemRarity Rarity => Detail.Rarity;

        public int AffixCount => Detail.AffixCount;

        public int Price { get; }

        public ShopItemSnapshot(ItemInstance item, ShopItemSource source, int price)
        {
            Item = item;
            Source = source;
            Detail = ItemDetailSnapshotFactory.Create(item);
            Price = price;
        }
    }

    public readonly struct ShopSnapshot
    {
        public int Gold { get; }

        public IReadOnlyList<ShopItemSnapshot> MerchantItems { get; }

        public IReadOnlyList<ShopItemSnapshot> PlayerItems { get; }

        public ShopSnapshot(
            int gold,
            IReadOnlyList<ShopItemSnapshot> merchantItems,
            IReadOnlyList<ShopItemSnapshot> playerItems)
        {
            Gold = gold;
            MerchantItems = merchantItems;
            PlayerItems = playerItems;
        }
    }

    public class GetShopSnapshotQuery : AbstractQuery<ShopSnapshot>
    {
        protected override ShopSnapshot OnDo()
        {
            InventoryModel inventory = this.GetModel<InventoryModel>();
            EconomyModel economy = this.GetModel<EconomyModel>();
            TradingSystem trading = this.GetSystem<TradingSystem>();
            List<ShopItemSnapshot> merchantItems = new List<ShopItemSnapshot>(economy.MerchantStock.Count);

            for (int i = 0; i < economy.MerchantStock.Count; i++)
            {
                ItemInstance item = economy.MerchantStock[i];
                merchantItems.Add(new ShopItemSnapshot(item, ShopItemSource.Merchant, trading.GetBuyPrice(item)));
            }

            InventorySnapshot inventorySnapshot = this.SendQuery(new GetInventorySnapshotQuery());
            List<ShopItemSnapshot> playerItems = new List<ShopItemSnapshot>(inventorySnapshot.Items.Count);

            for (int i = 0; i < inventorySnapshot.Items.Count; i++)
            {
                ItemInstance item = inventorySnapshot.Items[i].Item;
                if (item.BaseDefinition.ItemType != ItemType.Currency)
                {
                    playerItems.Add(new ShopItemSnapshot(item, ShopItemSource.Player, trading.GetSellPrice(item)));
                }
            }

            return new ShopSnapshot(inventory.Gold, merchantItems, playerItems);
        }
    }
}
