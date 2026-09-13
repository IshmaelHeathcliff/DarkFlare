using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public enum ItemSourceKind { Inventory, Equipment, Merchant, Crafting }
    public enum ItemActionKind { None, Move, Equip, Unequip, Buy, Sell, Place, Return, Discard }

    public readonly struct ItemActionSource
    {
        public ItemInstance Item { get; }
        public ItemSourceKind Kind { get; }
        public GameMenuPage Window { get; }
        public EquipmentSlot Slot { get; }
        public Button Element { get; }

        public ItemActionSource(ItemInstance item, ItemSourceKind kind, GameMenuPage window, Button element, EquipmentSlot slot = default)
        {
            Item = item;
            Kind = kind;
            Window = window;
            Element = element;
            Slot = slot;
        }
    }

    public readonly struct ItemActionTarget
    {
        public ItemActionKind Kind { get; }
        public VisualElement Element { get; }
        public Vector2Int? Origin { get; }
        public EquipmentSlot Slot { get; }

        public ItemActionTarget(ItemActionKind kind, VisualElement element = null, Vector2Int? origin = null, EquipmentSlot slot = default)
        {
            Kind = kind;
            Element = element;
            Origin = origin;
            Slot = slot;
        }
    }

    public sealed class ItemActionRouter : IController
    {
        readonly GameMenuController _menu;
        readonly CraftingPanelController _crafting;
        readonly IArchitecture _architecture;
        readonly LifecycleScope _scope;

        public ItemActionRouter(GameMenuController menu)
        {
            _menu = menu;
            _crafting = menu.GetComponent<CraftingPanelController>();
            _architecture = menu.GetArchitecture();
            GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope scope);
            _scope = scope;
        }

        public IArchitecture GetArchitecture()
        {
            return _architecture;
        }

        public InventorySnapshot Inventory => this.SendQuery(new GetInventorySnapshotQuery());

        public bool IsCurrent(ItemActionSource source)
        {
            if (_scope?.CanAcceptWork != true || source.Item == null || !_menu.IsWindowVisible(source.Window)
                || source.Element != null && !GameMenuController.IsNavigable(source.Element)) { return false; }
            InventorySnapshot snapshot = Inventory;
            if (!snapshot.HasPlayer || !snapshot.Player.IsAlive) { return false; }
            if (source.Kind == ItemSourceKind.Equipment)
            {
                foreach (EquipmentSlotSnapshot slot in snapshot.EquipmentSlots)
                {
                    if (slot.Slot == source.Slot) { return slot.Item == source.Item; }
                }
                return false;
            }
            if (source.Kind == ItemSourceKind.Merchant)
            {
                foreach (ShopItemSnapshot item in this.SendQuery(new GetShopSnapshotQuery()).MerchantItems)
                {
                    if (item.Item == source.Item) { return true; }
                }
                return false;
            }
            foreach (InventoryItemSnapshot item in snapshot.Items)
            {
                if (item.Item == source.Item)
                {
                    return source.Kind == ItemSourceKind.Crafting
                        ? _menu.Workspace.CraftingItem == source.Item
                        : _menu.Workspace.CraftingItem != source.Item;
                }
            }
            return false;
        }

        public ItemActionTarget Primary(ItemActionSource source)
        {
            if (source.Kind == ItemSourceKind.Merchant) { return new ItemActionTarget(ItemActionKind.Buy); }
            if (source.Kind == ItemSourceKind.Equipment) { return new ItemActionTarget(ItemActionKind.Unequip); }
            if (source.Kind == ItemSourceKind.Crafting) { return new ItemActionTarget(ItemActionKind.Return); }
            if (_menu.IsWindowVisible(GameMenuPage.Shop)) { return new ItemActionTarget(ItemActionKind.Sell); }
            if (_menu.IsWindowVisible(GameMenuPage.Crafting)) { return new ItemActionTarget(ItemActionKind.Place); }
            EquipmentSlot? compatible = null;
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                if (!source.Item.BaseDefinition.CanEquipTo(slot)) { continue; }
                if (compatible.HasValue) { return default; }
                compatible = slot;
            }
            return compatible.HasValue ? new ItemActionTarget(ItemActionKind.Equip, slot: compatible.Value) : default;
        }

        public string Validate(ItemActionSource source, ItemActionTarget target)
        {
            if (!IsCurrent(source)) { return "item.action.stale"; }
            InventorySnapshot inventory = Inventory;
            bool carried = source.Kind == ItemSourceKind.Inventory;
            switch (target.Kind)
            {
                case ItemActionKind.Move:
                    if (!target.Origin.HasValue || !_menu.IsWindowVisible(GameMenuPage.Inventory)) { break; }
                    if (IsOriginalCraftingCell(source, target.Origin.Value)) { return null; }
                    if ((carried || source.Kind == ItemSourceKind.Crafting)
                        && this.SendQuery(new CanMoveInventoryItemQuery(source.Item, target.Origin.Value))) { return null; }
                    break;
                case ItemActionKind.Equip:
                    if (!_menu.IsWindowVisible(GameMenuPage.Inventory)) { break; }
                    if (carried && this.SendQuery(new CanEquipItemFromGridQuery(inventory.Player, source.Item, target.Slot))) { return null; }
                    if (source.Kind == ItemSourceKind.Equipment
                        && this.SendQuery(new CanMoveEquippedItemQuery(inventory.Player, source.Slot, target.Slot))) { return null; }
                    break;
                case ItemActionKind.Unequip:
                    if (source.Kind == ItemSourceKind.Equipment
                        && FindUnequipOrigin(source, target.Origin, out _)) { return null; }
                    break;
                case ItemActionKind.Buy:
                    if (source.Kind != ItemSourceKind.Merchant || !_menu.IsWindowVisible(GameMenuPage.Shop)) { break; }
                    if (target.Origin.HasValue && !_menu.IsWindowVisible(GameMenuPage.Inventory)) { break; }
                    if (this.SendQuery(new CanBuyItemQuery(source.Item, target.Origin))) { return null; }
                    return this.SendQuery(new GetShopSnapshotQuery()).Gold < Price(source.Item, true)
                        ? "item.action.no_gold" : "item.action.no_space";
                case ItemActionKind.Sell:
                    if (source.Item.BaseDefinition.ItemType == ItemType.Currency) { break; }
                    if (carried && _menu.IsWindowVisible(GameMenuPage.Shop)
                        && (long)this.SendQuery(new GetShopSnapshotQuery()).Gold + Price(source.Item, false) <= int.MaxValue) { return null; }
                    break;
                case ItemActionKind.Place:
                    if (carried && _menu.IsWindowVisible(GameMenuPage.Crafting) && _crafting.CanAcceptItem(source.Item)) { return null; }
                    break;
                case ItemActionKind.Return:
                    if (source.Kind == ItemSourceKind.Crafting && _menu.IsWindowVisible(GameMenuPage.Crafting)) { return null; }
                    break;
                case ItemActionKind.Discard:
                    if (carried && this.SendQuery(new CanDiscardItemQuery(source.Item, inventory.Player))) { return null; }
                    return "item.action.no_drop_position";
            }
            return "item.action.invalid";
        }

        public bool Execute(ItemActionSource source, ItemActionTarget target)
        {
            if (Validate(source, target) != null) { return false; }
            CombatActor player = Inventory.Player;
            switch (target.Kind)
            {
                case ItemActionKind.Move:
                    if (IsOriginalCraftingCell(source, target.Origin.Value)) { return _crafting.ReturnItem(); }
                    bool moved = this.SendCommand(new MoveInventoryItemCommand(source.Item, target.Origin.Value));
                    if (moved && source.Kind == ItemSourceKind.Crafting) { _crafting.ReturnItem(); }
                    return moved;
                case ItemActionKind.Equip:
                    return source.Kind == ItemSourceKind.Equipment
                        ? this.SendCommand(new MoveEquippedItemCommand(player, source.Slot, target.Slot))
                        : this.SendCommand(new EquipItemFromGridCommand(player, source.Item, target.Slot));
                case ItemActionKind.Unequip:
                    return FindUnequipOrigin(source, target.Origin, out Vector2Int origin)
                        && this.SendCommand(new UnequipItemToGridCommand(player, source.Slot, origin));
                case ItemActionKind.Buy:
                    return this.SendCommand(new BuyItemCommand(source.Item, target.Origin));
                case ItemActionKind.Sell:
                    return this.SendCommand(new SellItemCommand(source.Item));
                case ItemActionKind.Place:
                    return _crafting.PlaceItem(source.Item);
                case ItemActionKind.Return:
                    return _crafting.ReturnItem();
                case ItemActionKind.Discard:
                    return this.SendCommand(new DiscardItemCommand(source.Item, player));
                default:
                    return false;
            }
        }

        public int Price(ItemInstance item, bool buying)
        {
            ShopSnapshot shop = this.SendQuery(new GetShopSnapshotQuery());
            IReadOnlyList<ShopItemSnapshot> items = buying ? shop.MerchantItems : shop.PlayerItems;
            foreach (ShopItemSnapshot candidate in items) { if (candidate.Item == item) { return candidate.Price; } }
            return 0;
        }

        bool FindUnequipOrigin(ItemActionSource source, Vector2Int? requested, out Vector2Int origin)
        {
            InventorySnapshot inventory = Inventory;
            origin = requested ?? Vector2Int.zero;
            if (requested.HasValue)
            {
                return this.SendQuery(new CanUnequipItemToGridQuery(inventory.Player, source.Slot, origin));
            }
            for (int y = 0; y < inventory.Height; y++)
            {
                for (int x = 0; x < inventory.Width; x++)
                {
                    origin = new Vector2Int(x, y);
                    if (this.SendQuery(new CanUnequipItemToGridQuery(inventory.Player, source.Slot, origin))) { return true; }
                }
            }
            return false;
        }

        bool IsOriginalCraftingCell(ItemActionSource source, Vector2Int origin)
        {
            if (source.Kind != ItemSourceKind.Crafting) { return false; }
            foreach (InventoryItemSnapshot item in Inventory.Items)
            {
                if (item.Item == source.Item) { return item.Placement.position == origin; }
            }
            return false;
        }
    }
}
