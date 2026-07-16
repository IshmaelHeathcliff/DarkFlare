namespace DarkFlare
{
    public enum InventoryChangeType
    {
        Added,
        Removed
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

    public readonly struct EquipmentChangedEvent
    {
        public CombatActor Actor { get; }

        public ItemInstance PreviousWeapon { get; }

        public ItemInstance CurrentWeapon { get; }

        public EquipmentChangedEvent(CombatActor actor, ItemInstance previousWeapon, ItemInstance currentWeapon)
        {
            Actor = actor;
            PreviousWeapon = previousWeapon;
            CurrentWeapon = currentWeapon;
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
}
