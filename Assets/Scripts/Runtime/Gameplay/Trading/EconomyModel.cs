using System.Collections.Generic;

namespace DarkFlare
{
    public class EconomyModel : AbstractModel
    {
        readonly List<ItemInstance> _merchantStock = new List<ItemInstance>();

        public IReadOnlyList<ItemInstance> MerchantStock => _merchantStock;

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
