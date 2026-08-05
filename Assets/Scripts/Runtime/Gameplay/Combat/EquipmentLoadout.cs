using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class EquipmentLoadout
    {
        readonly Dictionary<EquipmentSlot, ItemInstance> _items = new Dictionary<EquipmentSlot, ItemInstance>();

        public IEnumerable<KeyValuePair<EquipmentSlot, ItemInstance>> Slots => _items;

        public ItemInstance Get(EquipmentSlot slot)
        {
            return _items.TryGetValue(slot, out ItemInstance item) ? item : null;
        }

        public bool Contains(ItemInstance item)
        {
            if (item == null)
            {
                return false;
            }

            foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in _items)
            {
                if (pair.Value == item)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TrySet(EquipmentSlot slot, ItemInstance item)
        {
            ItemInstance current = Get(slot);

            if (current == item)
            {
                return false;
            }

            if (item != null)
            {
                if (item.BaseDefinition == null || !item.BaseDefinition.CanEquipTo(slot) || Contains(item))
                {
                    return false;
                }

                _items[slot] = item;
                return true;
            }

            return _items.Remove(slot);
        }
    }
}
