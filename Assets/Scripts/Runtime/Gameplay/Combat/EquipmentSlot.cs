using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public enum EquipmentSlot
    {
        Weapon = 0,
        Armor = 1,
        RingLeft = 2,
        RingRight = 3,
        Head = 4,
        Hands = 5,
        Legs = 6,
        OffHand = 7,
        Necklace = 8,
        Belt = 9
    }

    [Flags]
    public enum EquipmentSlotMask
    {
        None = 0,
        Weapon = 1 << 0,
        Armor = 1 << 1,
        RingLeft = 1 << 2,
        RingRight = 1 << 3,
        Head = 1 << 4,
        Hands = 1 << 5,
        Legs = 1 << 6,
        OffHand = 1 << 7,
        Necklace = 1 << 8,
        Belt = 1 << 9,
        Rings = RingLeft | RingRight,
        Defenses = Armor | Head | Hands | Legs | OffHand,
        Accessories = Rings | Necklace | Belt,
        All = Weapon | Defenses | Accessories
    }

    public static class EquipmentSlots
    {
        static readonly IReadOnlyList<EquipmentSlot> Values = new[]
        {
            EquipmentSlot.Weapon,
            EquipmentSlot.Armor,
            EquipmentSlot.RingLeft,
            EquipmentSlot.RingRight,
            EquipmentSlot.Head,
            EquipmentSlot.Hands,
            EquipmentSlot.Legs,
            EquipmentSlot.OffHand,
            EquipmentSlot.Necklace,
            EquipmentSlot.Belt,
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
                EquipmentSlot.Head => EquipmentSlotMask.Head,
                EquipmentSlot.Hands => EquipmentSlotMask.Hands,
                EquipmentSlot.Legs => EquipmentSlotMask.Legs,
                EquipmentSlot.OffHand => EquipmentSlotMask.OffHand,
                EquipmentSlot.Necklace => EquipmentSlotMask.Necklace,
                EquipmentSlot.Belt => EquipmentSlotMask.Belt,
                _ => EquipmentSlotMask.None,
            };
        }

        public static string GetDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "主武器",
                EquipmentSlot.Armor => "身体",
                EquipmentSlot.RingLeft => "左戒指",
                EquipmentSlot.RingRight => "右戒指",
                EquipmentSlot.Head => "头部",
                EquipmentSlot.Hands => "手部",
                EquipmentSlot.Legs => "腿部",
                EquipmentSlot.OffHand => "副手",
                EquipmentSlot.Necklace => "项链",
                EquipmentSlot.Belt => "腰带",
                _ => "未知槽位",
            };
        }

        public static string GetKey(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "weapon",
                EquipmentSlot.Armor => "armor",
                EquipmentSlot.RingLeft => "ring_left",
                EquipmentSlot.RingRight => "ring_right",
                EquipmentSlot.Head => "head",
                EquipmentSlot.Hands => "hands",
                EquipmentSlot.Legs => "legs",
                EquipmentSlot.OffHand => "off_hand",
                EquipmentSlot.Necklace => "necklace",
                EquipmentSlot.Belt => "belt",
                _ => "unknown",
            };
        }

        public static EquipmentSlot? GetUniqueTarget(EquipmentSlotMask mask)
        {
            EquipmentSlot? result = null;
            foreach (EquipmentSlot slot in All)
            {
                if ((mask & ToMask(slot)) == 0)
                {
                    continue;
                }
                if (result.HasValue)
                {
                    return null;
                }
                result = slot;
            }
            return (mask & ~EquipmentSlotMask.All) == 0 ? result : null;
        }
    }
}
