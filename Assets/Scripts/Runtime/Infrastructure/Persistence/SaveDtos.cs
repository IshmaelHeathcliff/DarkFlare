using System.Collections.Generic;

namespace DarkFlare
{
    public static class LocalSaveFormat
    {
        public const string FormatId = "darkflare-save";

        public const int FormatVersion = 1;

        public const int MaximumDocumentBytes = 16 * 1024 * 1024;

        public const int MaximumJsonDepth = 64;

        public const int MaximumItems = 8192;

        public const int MaximumMonsters = 512;

        public const int MaximumWorldDrops = 4096;
    }

    public sealed class SaveDocumentDto : IPersistenceDto
    {
        public SaveHeaderDto Header { get; set; } = new SaveHeaderDto();

        public SavePayloadDto Payload { get; set; } = new SavePayloadDto();
    }

    public sealed class SaveHeaderDto : IPersistenceDto
    {
        public string FormatId { get; set; } = LocalSaveFormat.FormatId;

        public int FormatVersion { get; set; } = LocalSaveFormat.FormatVersion;

        public int SaveSchemaVersion { get; set; } = DarkFlare.SaveSchemaVersion.Current.Value;

        public string GameVersion { get; set; } = string.Empty;

        public string CatalogId { get; set; } = string.Empty;

        public int ContentVersion { get; set; }

        public string SlotId { get; set; } = string.Empty;

        public long CommitSequence { get; set; }

        public string CreatedUtc { get; set; } = string.Empty;

        public string UpdatedUtc { get; set; } = string.Empty;

        public string PayloadSha256 { get; set; } = string.Empty;

        public SaveSummaryDto Summary { get; set; } = new SaveSummaryDto();
    }

    public sealed class SaveSummaryDto : IPersistenceDto
    {
        public string RunId { get; set; } = string.Empty;

        public int Gold { get; set; }

        public int ItemCount { get; set; }

        public float CurrentHealth { get; set; }

        public float MaxHealth { get; set; }
    }

    public sealed class SavePayloadDto : IPersistenceDto
    {
        public List<ItemInstanceDto> Items { get; set; } = new List<ItemInstanceDto>();

        public ProfileSaveData Profile { get; set; } = new ProfileSaveData();

        public RunSaveData Run { get; set; } = new RunSaveData();
    }

    public sealed class ProfileSaveData : IPersistenceDto
    {
        public string ProfileId { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;

        public int Gold { get; set; }

        public InventoryLayoutDto Inventory { get; set; } = new InventoryLayoutDto();

        public List<EquipmentLoadoutDto> Equipment { get; set; } = new List<EquipmentLoadoutDto>();
    }

    public sealed class RunInstanceIdStateDto : IPersistenceDto
    {
        public string RunId { get; set; } = string.Empty;

        public long NextMonsterSequence { get; set; } = 1;

        public long NextWorldDropSequence { get; set; } = 1;
    }

    public sealed class GameplayRandomChannelStateDto : IPersistenceDto
    {
        public GameplayRandomChannel Channel { get; set; }

        public int NextSequence { get; set; }
    }

    public sealed class GameplayRandomStateDto : IPersistenceDto
    {
        public int RootSeed { get; set; }

        public List<GameplayRandomChannelStateDto> Channels { get; set; } =
            new List<GameplayRandomChannelStateDto>();
    }

    public sealed class Vector3Dto : IPersistenceDto
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }
    }

    public sealed class CombatResourceStateDto : IPersistenceDto
    {
        public float CurrentHealth { get; set; }

        public float MaxHealth { get; set; }

        public float CurrentMana { get; set; }

        public float MaxMana { get; set; }

        public bool IsAlive { get; set; } = true;
    }

    public sealed class StatValueDto : IPersistenceDto
    {
        public string StatContentId { get; set; } = string.Empty;

        public float Value { get; set; }
    }

    public sealed class MonsterRandomSeedsDto : IPersistenceDto
    {
        public int RootSeed { get; set; }

        public int HealthSeed { get; set; }

        public int AffixCountSeed { get; set; }

        public int AffixSelectionSeed { get; set; }
    }

    public sealed class MonsterAffixInstanceDto : IPersistenceDto
    {
        public string DefinitionContentId { get; set; } = string.Empty;

        public List<ModifierInstanceDto> Modifiers { get; set; } = new List<ModifierInstanceDto>();
    }

    public sealed class PlayerRunStateDto : IPersistenceDto
    {
        public string PlayerId { get; set; } = string.Empty;

        public string DefinitionContentId { get; set; } = string.Empty;

        public string SkillContentId { get; set; } = string.Empty;

        public Vector3Dto Position { get; set; } = new Vector3Dto();

        public CombatResourceStateDto Resources { get; set; } = new CombatResourceStateDto();

        public float RespawnRemainingSeconds { get; set; }

        public float AutoCastCooldownRemainingSeconds { get; set; }
    }

    public sealed class MonsterRunStateDto : IPersistenceDto
    {
        public string InstanceId { get; set; } = string.Empty;

        public string DefinitionContentId { get; set; } = string.Empty;

        public MonsterRandomSeedsDto RandomSeeds { get; set; } = new MonsterRandomSeedsDto();

        public float BaseMaxHealth { get; set; }

        public List<StatValueDto> BaseStats { get; set; } = new List<StatValueDto>();

        public List<StatValueDto> EffectiveStats { get; set; } = new List<StatValueDto>();

        public List<MonsterAffixInstanceDto> Affixes { get; set; } =
            new List<MonsterAffixInstanceDto>();

        public List<ModifierInstanceDto> Modifiers { get; set; } =
            new List<ModifierInstanceDto>();

        public Vector3Dto Position { get; set; } = new Vector3Dto();

        public CombatResourceStateDto Resources { get; set; } = new CombatResourceStateDto();

        public float ContactDamageCooldownRemainingSeconds { get; set; }
    }

    public sealed class WorldDropStateDto : IPersistenceDto
    {
        public string InstanceId { get; set; } = string.Empty;

        public string ItemInstanceId { get; set; } = string.Empty;

        public Vector3Dto Position { get; set; } = new Vector3Dto();
    }

    public sealed class MonsterSpawnerStateDto : IPersistenceDto
    {
        public string SpawnContentId { get; set; } = string.Empty;

        public bool IsRunning { get; set; }

        public float NextSpawnRemainingSeconds { get; set; }
    }

    public sealed class RunSaveData : IPersistenceDto
    {
        public StatusSaveDto Statuses { get; set; } = new StatusSaveDto();

        public RunInstanceIdStateDto InstanceIds { get; set; } = new RunInstanceIdStateDto();

        public GameplayRandomStateDto Random { get; set; } = new GameplayRandomStateDto();

        public PlayerRunStateDto Player { get; set; } = new PlayerRunStateDto();

        public List<MonsterRunStateDto> Monsters { get; set; } = new List<MonsterRunStateDto>();

        public List<WorldDropStateDto> WorldDrops { get; set; } = new List<WorldDropStateDto>();

        public MerchantInventoryDto Merchant { get; set; } = new MerchantInventoryDto();

        public MonsterSpawnerStateDto Spawner { get; set; } = new MonsterSpawnerStateDto();
    }
}
