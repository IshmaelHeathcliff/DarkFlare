using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public enum EquipmentSlot
    {
        Weapon = 0,
        Armor = 1,
        RingLeft = 2,
        RingRight = 3
    }

    [Flags]
    public enum EquipmentSlotMask
    {
        None = 0,
        Weapon = 1 << 0,
        Armor = 1 << 1,
        RingLeft = 1 << 2,
        RingRight = 1 << 3,
        Rings = RingLeft | RingRight,
        All = Weapon | Armor | Rings
    }

    public static class EquipmentSlots
    {
        static readonly IReadOnlyList<EquipmentSlot> Values = new[]
        {
            EquipmentSlot.Weapon,
            EquipmentSlot.Armor,
            EquipmentSlot.RingLeft,
            EquipmentSlot.RingRight,
        };

        public static IReadOnlyList<EquipmentSlot> All => Values;

        public static EquipmentSlotMask ToMask(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => EquipmentSlotMask.Weapon,
                EquipmentSlot.Armor => EquipmentSlotMask.Armor,
                EquipmentSlot.RingLeft => EquipmentSlotMask.RingLeft,
                EquipmentSlot.RingRight => EquipmentSlotMask.RingRight,
                _ => EquipmentSlotMask.None,
            };
        }

        public static string GetDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "武器",
                EquipmentSlot.Armor => "护甲",
                EquipmentSlot.RingLeft => "左戒指",
                EquipmentSlot.RingRight => "右戒指",
                _ => "未知槽位",
            };
        }
    }
}
