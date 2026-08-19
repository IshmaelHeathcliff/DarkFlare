using System.Collections.Generic;

namespace DarkFlare
{
    public class EconomyModel : AbstractModel
    {
        readonly List<ItemInstance> _merchantStock = new List<ItemInstance>();

        public IReadOnlyList<ItemInstance> MerchantStock => _merchantStock;

        public TraderDefinition Trader { get; private set; }

        public float BuyMultiplier { get; private set; } = 1.5f;

        public float SellMultiplier { get; private set; } = 0.4f;

        protected override void OnInit()
        {
        }

        public void SetMultipliers(float buyMultiplier, float sellMultiplier)
        {
            BuyMultiplier = buyMultiplier;
            SellMultiplier = sellMultiplier;
        }

        public void RestoreState(
            TraderDefinition trader,
            IReadOnlyList<ItemInstance> stock,
            float buyMultiplier,
            float sellMultiplier)
        {
            if (trader == null)
            {
                throw new System.ArgumentNullException(nameof(trader));
            }

            if (stock == null)
            {
                throw new System.ArgumentNullException(nameof(stock));
            }

            if (!float.IsFinite(buyMultiplier)
                || !float.IsFinite(sellMultiplier)
                || buyMultiplier <= 0f
                || sellMultiplier <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(buyMultiplier));
            }

            Trader = trader;
            _merchantStock.Clear();

            for (int i = 0; i < stock.Count; i++)
            {
                if (stock[i] == null || _merchantStock.Contains(stock[i]))
                {
                    throw new System.ArgumentException("商人库存包含空项或重复项", nameof(stock));
                }

                _merchantStock.Add(stock[i]);
            }

            BuyMultiplier = buyMultiplier;
            SellMultiplier = sellMultiplier;
        }

        public void ClearStock()
        {
            _merchantStock.Clear();
        }

        public void AddStock(ItemInstance item)
        {
            if (item != null && !_merchantStock.Contains(item))
            {
                _merchantStock.Add(item);
            }
        }

        public bool RemoveStock(ItemInstance item)
        {
            return _merchantStock.Remove(item);
        }

        public bool HasStock(ItemInstance item)
        {
            return item != null && _merchantStock.Contains(item);
        }
    }
}
