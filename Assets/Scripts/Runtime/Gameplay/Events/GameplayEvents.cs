namespace DarkFlare
{
    public enum InventoryChangeType
    {
        Added,
        Removed
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

        public InventoryChangedEvent(ItemInstance item, InventoryChangeType changeType)
        {
            Item = item;
            ChangeType = changeType;
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
        public CraftOperation Operation { get; }

        public ItemInstance Item { get; }

        public ItemCraftedEvent(CraftOperation operation, ItemInstance item)
        {
            Operation = operation;
            Item = item;
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

        public GameMenuOpenRequestedEvent(GameMenuPage page, GameMenuAccess availablePages)
        {
            Page = page;
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
