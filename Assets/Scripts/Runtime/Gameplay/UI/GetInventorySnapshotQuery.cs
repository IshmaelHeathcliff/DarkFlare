using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct InventoryItemSnapshot
    {
        public ItemInstance Item { get; }

        public RectInt Placement { get; }

        public ItemDetailSnapshot Detail { get; }

        public string DisplayName => Detail.DisplayName;

        public ItemType Type => Detail.Type;

        public ItemRarity Rarity => Detail.Rarity;

        public int AffixCount => Detail.AffixCount;

        public bool CanEquip => Type == ItemType.Weapon;

        public InventoryItemSnapshot(ItemInstance item, RectInt placement)
        {
            Item = item;
            Placement = placement;
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

        public string CurrentWeaponSummary { get; }

        public bool HasPlayer => Player != null;

        public InventorySnapshot(
            CombatActor player,
            int width,
            int height,
            IReadOnlyList<InventoryItemSnapshot> items,
            ItemInstance currentWeapon,
            string currentWeaponSummary)
        {
            Player = player;
            Width = width;
            Height = height;
            Items = items;
            CurrentWeapon = currentWeapon;
            CurrentWeaponSummary = currentWeaponSummary;
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
            return new InventorySnapshot(
                player,
                grid.Width,
                grid.Height,
                items,
                currentWeapon,
                DescribeWeapon(currentWeapon));
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

        static string DescribeWeapon(ItemInstance weapon)
        {
            if (weapon == null)
            {
                return "未装备";
            }

            ItemDetailSnapshot detail = ItemDetailSnapshotFactory.Create(weapon);
            return $"{detail.DisplayName} · {ItemDetailFormatter.GetRarityText(detail.Rarity)} · {detail.AffixCount} 条词缀";
        }
    }
}
