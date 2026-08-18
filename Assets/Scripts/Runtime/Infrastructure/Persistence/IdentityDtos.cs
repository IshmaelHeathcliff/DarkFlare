using System.Collections.Generic;

namespace DarkFlare
{
    public interface IPersistenceDto
    {
    }

    public sealed class ModifierInstanceDto : IPersistenceDto
    {
        public string StatContentId { get; set; } = string.Empty;

        public ModifierOperation Operation { get; set; }

        public ModifierScope Scope { get; set; }

        public float Value { get; set; }

        public DamageType FromDamageType { get; set; }

        public DamageType ToDamageType { get; set; }

        public CombatTagScope QueryScope { get; set; }

        public bool UsesLegacyTagMatching { get; set; }

        public List<string> RequiredAllTagIds { get; set; } = new List<string>();

        public List<string> RequiredAnyTagIds { get; set; } = new List<string>();

        public List<string> BlockedTagIds { get; set; } = new List<string>();
    }

    public sealed class AffixInstanceDto : IPersistenceDto
    {
        public string DefinitionContentId { get; set; } = string.Empty;

        public List<ModifierInstanceDto> Modifiers { get; set; } = new List<ModifierInstanceDto>();
    }

    public sealed class ItemInstanceDto : IPersistenceDto
    {
        public string InstanceId { get; set; } = string.Empty;

        public string BaseContentId { get; set; } = string.Empty;

        public ItemRarity Rarity { get; set; }

        public int ItemLevel { get; set; }

        public int Seed { get; set; }

        public int Quality { get; set; }

        public float Durability { get; set; } = 1f;

        public List<ModifierInstanceDto> ImplicitModifiers { get; set; } = new List<ModifierInstanceDto>();

        public List<AffixInstanceDto> Prefixes { get; set; } = new List<AffixInstanceDto>();

        public List<AffixInstanceDto> Suffixes { get; set; } = new List<AffixInstanceDto>();
    }

    public sealed class InventoryPlacementDto : IPersistenceDto
    {
        public string ItemInstanceId { get; set; } = string.Empty;

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }

    public sealed class InventoryLayoutDto : IPersistenceDto
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public List<InventoryPlacementDto> Placements { get; set; } = new List<InventoryPlacementDto>();
    }

    public sealed class EquipmentEntryDto : IPersistenceDto
    {
        public EquipmentSlot Slot { get; set; }

        public string ItemInstanceId { get; set; } = string.Empty;
    }

    public sealed class EquipmentLoadoutDto : IPersistenceDto
    {
        public string ActorInstanceId { get; set; } = string.Empty;

        public List<EquipmentEntryDto> Entries { get; set; } = new List<EquipmentEntryDto>();
    }

    public sealed class MerchantInventoryDto : IPersistenceDto
    {
        public string TraderContentId { get; set; } = string.Empty;

        public List<string> OrderedItemInstanceIds { get; set; } = new List<string>();
    }

    public enum ActorIdentityKind
    {
        Player = 0,
        Monster = 1,
    }

    public sealed class ActorIdentityDto : IPersistenceDto
    {
        public ActorIdentityKind Kind { get; set; }

        public string InstanceId { get; set; } = string.Empty;

        public string DefinitionContentId { get; set; } = string.Empty;
    }

    public sealed class RuntimeStateDto : IPersistenceDto
    {
        public List<ItemInstanceDto> Items { get; set; } = new List<ItemInstanceDto>();

        public InventoryLayoutDto Inventory { get; set; } = new InventoryLayoutDto();

        public List<EquipmentLoadoutDto> Equipment { get; set; } = new List<EquipmentLoadoutDto>();

        public MerchantInventoryDto Merchant { get; set; } = new MerchantInventoryDto();

        public List<ActorIdentityDto> Actors { get; set; } = new List<ActorIdentityDto>();
    }
}
