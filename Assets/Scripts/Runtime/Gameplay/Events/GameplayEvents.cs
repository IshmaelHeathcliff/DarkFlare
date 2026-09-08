using UnityEngine;

namespace DarkFlare
{
    public enum InventoryChangeType
    {
        Added,
        Removed,
        Moved
    }

    public enum TradeOperation
    {
        Buy,
        Sell
    }

    public readonly struct GoldChangedEvent
    {
        public int PreviousGold { get; }

        public int CurrentGold { get; }

        public GoldChangedEvent(int previousGold, int currentGold)
        {
            PreviousGold = previousGold;
            CurrentGold = currentGold;
        }
    }

    public readonly struct InventoryChangedEvent
    {
        public ItemInstance Item { get; }

        public InventoryChangeType ChangeType { get; }

        public RectInt PreviousPlacement { get; }

        public RectInt CurrentPlacement { get; }

        public InventoryChangedEvent(ItemInstance item, InventoryChangeType changeType)
            : this(item, changeType, default, default)
        {
        }

        public InventoryChangedEvent(
            ItemInstance item,
            InventoryChangeType changeType,
            RectInt previousPlacement,
            RectInt currentPlacement)
        {
            Item = item;
            ChangeType = changeType;
            PreviousPlacement = previousPlacement;
            CurrentPlacement = currentPlacement;
        }
    }

    public readonly struct TradeCompletedEvent
    {
        public TradeOperation Operation { get; }

        public ItemInstance Item { get; }

        public int Price { get; }

        public TradeCompletedEvent(TradeOperation operation, ItemInstance item, int price)
        {
            Operation = operation;
            Item = item;
            Price = price;
        }
    }

    public readonly struct EquipmentChangedEvent
    {
        public CombatActor Actor { get; }

        public EquipmentSlot Slot { get; }

        public ItemInstance PreviousItem { get; }

        public ItemInstance CurrentItem { get; }

        public ItemInstance PreviousWeapon => Slot == EquipmentSlot.Weapon ? PreviousItem : null;

        public ItemInstance CurrentWeapon => Slot == EquipmentSlot.Weapon ? CurrentItem : null;

        public EquipmentChangedEvent(
            CombatActor actor,
            EquipmentSlot slot,
            ItemInstance previousItem,
            ItemInstance currentItem)
        {
            Actor = actor;
            Slot = slot;
            PreviousItem = previousItem;
            CurrentItem = currentItem;
        }

        public EquipmentChangedEvent(CombatActor actor, ItemInstance previousWeapon, ItemInstance currentWeapon)
            : this(actor, EquipmentSlot.Weapon, previousWeapon, currentWeapon)
        {
        }
    }

    public readonly struct ItemCraftedEvent
    {
        public CraftingResult Result { get; }

        public CraftOperation Operation => Result.Operation;

        public ItemInstance Item => Result.Item;

        public ItemCraftedEvent(CraftingResult result)
        {
            Result = result;
        }
    }

    public readonly struct ActorRegisteredEvent
    {
        public CombatActor Actor { get; }

        public ActorRegisteredEvent(CombatActor actor)
        {
            Actor = actor;
        }
    }

    public readonly struct ActorUnregisteredEvent
    {
        public CombatActor Actor { get; }

        public ActorUnregisteredEvent(CombatActor actor)
        {
            Actor = actor;
        }
    }

    public readonly struct InteractionFocusChangedEvent
    {
        public WorldInteractionTarget Target { get; }

        public InteractionFocusChangedEvent(WorldInteractionTarget target)
        {
            Target = target;
        }
    }

    public readonly struct GameMenuOpenRequestedEvent
    {
        public GameMenuPage Page { get; }

        public GameMenuAccess AvailablePages { get; }

        public WorldInteractionTarget Target { get; }

        public GameMenuOpenRequestedEvent(GameMenuPage page, GameMenuAccess availablePages, WorldInteractionTarget target = null)
        {
            Page = page;
            Target = target;
            AvailablePages = availablePages | GameMenuAccess.Inventory;
        }
    }

    public readonly struct GameplayPauseChangedEvent
    {
        public bool IsPaused { get; }

        public GameplayPauseChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
