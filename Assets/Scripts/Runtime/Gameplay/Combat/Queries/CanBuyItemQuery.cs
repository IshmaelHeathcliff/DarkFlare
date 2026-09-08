using UnityEngine;

namespace DarkFlare
{
    public sealed class CanBuyItemQuery : AbstractQuery<bool>
    {
        readonly ItemInstance _item;
        readonly Vector2Int? _origin;

        public CanBuyItemQuery(ItemInstance item, Vector2Int? origin = null)
        {
            _item = item;
            _origin = origin;
        }

        protected override bool OnDo()
        {
            return this.GetSystem<TradingSystem>().CanBuyItem(_item, _origin);
        }
    }
}
