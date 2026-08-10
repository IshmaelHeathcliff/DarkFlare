using UnityEngine;

namespace DarkFlare
{
    public class MoveInventoryItemCommand : AbstractCommand<bool>
    {
        readonly ItemInstance _item;
        readonly Vector2Int _origin;

        public MoveInventoryItemCommand(ItemInstance item, Vector2Int origin)
        {
            _item = item;
            _origin = origin;
        }

        protected override bool OnExecute()
        {
            return this.GetModel<InventoryModel>().TryMoveItem(_item, _origin);
        }
    }
}
