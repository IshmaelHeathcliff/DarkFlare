using UnityEngine;

namespace DarkFlare
{
    public class CanMoveInventoryItemQuery : AbstractQuery<bool>
    {
        readonly ItemInstance _item;
        readonly Vector2Int _origin;

        public CanMoveInventoryItemQuery(ItemInstance item, Vector2Int origin)
        {
            _item = item;
            _origin = origin;
        }

        protected override bool OnDo()
        {
            return this.GetModel<InventoryModel>().Grid.CanMove(_item, _origin, out _);
        }
    }

    public class CanEquipItemFromGridQuery : AbstractQuery<bool>
    {
        readonly CombatActor _actor;
        readonly ItemInstance _item;
        readonly EquipmentSlot _slot;

        public CanEquipItemFromGridQuery(CombatActor actor, ItemInstance item, EquipmentSlot slot)
        {
            _actor = actor;
            _item = item;
            _slot = slot;
        }

        protected override bool OnDo()
        {
            if (_actor == null
                || _item == null
                || _item.BaseDefinition == null
                || !_item.BaseDefinition.CanEquipTo(_slot))
            {
                return false;
            }

            InventoryModel inventory = this.GetModel<InventoryModel>();

            if (!inventory.Grid.Placements.TryGetValue(_item, out RectInt sourcePlacement))
            {
                return false;
            }

            ItemInstance previousItem = this.GetModel<EquipmentModel>().GetItem(_actor, _slot);
            return inventory.CanExchangeItemAt(_item, previousItem, sourcePlacement.position);
        }
    }

    public class CanUnequipItemToGridQuery : AbstractQuery<bool>
    {
        readonly CombatActor _actor;
        readonly EquipmentSlot _slot;
        readonly Vector2Int _origin;

        public CanUnequipItemToGridQuery(CombatActor actor, EquipmentSlot slot, Vector2Int origin)
        {
            _actor = actor;
            _slot = slot;
            _origin = origin;
        }

        protected override bool OnDo()
        {
            ItemInstance item = this.GetModel<EquipmentModel>().GetItem(_actor, _slot);
            return item != null && this.GetModel<InventoryModel>().CanAddItemAt(item, _origin);
        }
    }

    public class CanMoveEquippedItemQuery : AbstractQuery<bool>
    {
        readonly CombatActor _actor;
        readonly EquipmentSlot _sourceSlot;
        readonly EquipmentSlot _targetSlot;

        public CanMoveEquippedItemQuery(
            CombatActor actor,
            EquipmentSlot sourceSlot,
            EquipmentSlot targetSlot)
        {
            _actor = actor;
            _sourceSlot = sourceSlot;
            _targetSlot = targetSlot;
        }

        protected override bool OnDo()
        {
            if (_actor == null || _sourceSlot == _targetSlot)
            {
                return false;
            }

            EquipmentModel equipment = this.GetModel<EquipmentModel>();
            ItemInstance sourceItem = equipment.GetItem(_actor, _sourceSlot);
            ItemInstance targetItem = equipment.GetItem(_actor, _targetSlot);
            return sourceItem != null
                && sourceItem.BaseDefinition != null
                && sourceItem.BaseDefinition.CanEquipTo(_targetSlot)
                && (targetItem == null
                    || (targetItem.BaseDefinition != null
                        && targetItem.BaseDefinition.CanEquipTo(_sourceSlot)));
        }
    }
}
