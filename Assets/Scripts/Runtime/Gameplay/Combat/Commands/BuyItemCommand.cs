using UnityEngine;

namespace DarkFlare
{
    public class BuyItemCommand : AbstractCommand<bool>
    {
        readonly ItemInstance _item;
        readonly Vector2Int? _origin;

        public BuyItemCommand(ItemInstance item, Vector2Int? origin = null)
        {
            _item = item;
            _origin = origin;
        }

        protected override bool OnExecute()
        {
            return this.GetSystem<TradingSystem>().BuyItem(_item, _origin);
        }
    }
}
