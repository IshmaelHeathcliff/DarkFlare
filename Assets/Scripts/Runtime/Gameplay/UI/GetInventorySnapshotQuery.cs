using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct InventoryItemSnapshot
    {
        public ItemInstance Item { get; }

        public RectInt Placement { get; }

        public ItemDetailSnapshot Detail { get; }

        public ItemType Type => Detail.Type;

        public ItemRarity Rarity => Detail.Rarity;

        public int AffixCount => Detail.AffixCount;

        public EquipmentSlotMask CompatibleSlots => Item != null && Item.BaseDefinition != null
            ? Item.BaseDefinition.AllowedEquipmentSlots
            : EquipmentSlotMask.None;

        public bool CanEquip => CompatibleSlots != EquipmentSlotMask.None;

        public InventoryItemSnapshot(ItemInstance item, RectInt placement)
        {
            Item = item;
            Placement = placement;
            Detail = ItemDetailSnapshotFactory.Create(item);
        }
    }

    public readonly struct EquipmentSlotSnapshot
    {
        public EquipmentSlot Slot { get; }

        public ItemInstance Item { get; }

        public ItemDetailSnapshot Detail { get; }

        public EquipmentSlotSnapshot(EquipmentSlot slot, ItemInstance item)
        {
            Slot = slot;
            Item = item;
            Detail = ItemDetailSnapshotFactory.Create(item);
        }
    }

    public readonly struct InventorySnapshot
    {
        public CombatActor Player { get; }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<InventoryItemSnapshot> Items { get; }

        public ItemInstance CurrentWeapon { get; }

        public IReadOnlyList<EquipmentSlotSnapshot> EquipmentSlots { get; }

        public bool HasPlayer => Player != null;

        public InventorySnapshot(
            CombatActor player,
            int width,
            int height,
            IReadOnlyList<InventoryItemSnapshot> items,
            ItemInstance currentWeapon,
            IReadOnlyList<EquipmentSlotSnapshot> equipmentSlots)
        {
            Player = player;
            Width = width;
            Height = height;
            Items = items;
            CurrentWeapon = currentWeapon;
            EquipmentSlots = equipmentSlots;
        }
    }

    public class GetInventorySnapshotQuery : AbstractQuery<InventorySnapshot>
    {
        protected override InventorySnapshot OnDo()
        {
            InventoryGrid grid = this.GetModel<InventoryModel>().Grid;
            List<InventoryItemSnapshot> items = new List<InventoryItemSnapshot>(grid.Placements.Count);

            foreach (KeyValuePair<ItemInstance, RectInt> placement in grid.Placements)
            {
                items.Add(new InventoryItemSnapshot(placement.Key, placement.Value));
            }

            items.Sort(CompareItems);
            CombatActor player = GetPlayer();
            ItemInstance currentWeapon = player != null
                ? this.GetModel<EquipmentModel>().GetWeapon(player)
                : null;
            List<EquipmentSlotSnapshot> equipmentSlots = CreateEquipmentSlots(player);
            return new InventorySnapshot(
                player,
                grid.Width,
                grid.Height,
                items,
                currentWeapon,
                equipmentSlots);
        }

        List<EquipmentSlotSnapshot> CreateEquipmentSlots(CombatActor player)
        {
            IReadOnlyList<EquipmentSlot> slots = EquipmentSlots.All;
            List<EquipmentSlotSnapshot> result = new List<EquipmentSlotSnapshot>(slots.Count);
            EquipmentModel equipment = this.GetModel<EquipmentModel>();

            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlot slot = slots[i];
                ItemInstance item = player != null ? equipment.GetItem(player, slot) : null;
                result.Add(new EquipmentSlotSnapshot(slot, item));
            }

            return result;
        }

        CombatActor GetPlayer()
        {
            IReadOnlyList<CombatActor> actors = this.GetModel<CombatModel>().GetActorsByTeam(ActorTeam.Player);

            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null)
                {
                    return actors[i];
                }
            }

            return null;
        }

        static int CompareItems(InventoryItemSnapshot left, InventoryItemSnapshot right)
        {
            int rowComparison = left.Placement.y.CompareTo(right.Placement.y);
            return rowComparison != 0
                ? rowComparison
                : left.Placement.x.CompareTo(right.Placement.x);
        }

    }
}
