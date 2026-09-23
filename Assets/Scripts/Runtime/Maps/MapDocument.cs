using System;
using Newtonsoft.Json;

namespace DarkFlare
{
    // 可编辑交换 DTO；编译后仅通过副本暴露，不是 Unity 配置资产。
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapDocument
    {
        [JsonProperty("formatVersion", Required = Required.Always)] public int FormatVersion { get; set; } = 4;
        [JsonProperty("mapId", Required = Required.Always)] public string MapId { get; set; }
        [JsonProperty("revision", Required = Required.Always)] public int Revision { get; set; }
        [JsonProperty("seed", Required = Required.Always)] public int Seed { get; set; }
        [JsonProperty("origin", Required = Required.Always)] public MapPoint Origin { get; set; } = new MapPoint();
        [JsonProperty("cellSize", Required = Required.Always)] public double CellSize { get; set; } = 1;
        [JsonProperty("width", Required = Required.Always)] public int Width { get; set; }
        [JsonProperty("height", Required = Required.Always)] public int Height { get; set; }
        [JsonProperty("terrainSetId", Required = Required.Always)] public string TerrainSetId { get; set; }
        [JsonProperty("terrainPalette", Required = Required.Always)] public string[] TerrainPalette { get; set; } = Array.Empty<string>();
        [JsonProperty("defaultTerrainId", Required = Required.Always)] public string DefaultTerrainId { get; set; }
        [JsonProperty("chunks", Required = Required.Always)] public MapChunk[] Chunks { get; set; } = Array.Empty<MapChunk>();
        [JsonProperty("variantOverrides", Required = Required.Always)] public MapVariantOverride[] VariantOverrides { get; set; } = Array.Empty<MapVariantOverride>();
        [JsonProperty("objects", Required = Required.Always)] public MapObjectRecord[] Objects { get; set; } = Array.Empty<MapObjectRecord>();
        [JsonProperty("regions", Required = Required.Always)] public MapRegionRecord[] Regions { get; set; } = Array.Empty<MapRegionRecord>();
        [JsonProperty("entries", Required = Required.Always)] public MapEntryRecord[] Entries { get; set; } = Array.Empty<MapEntryRecord>();
        [JsonProperty("bossSpawns", Required = Required.Always)] public MapBossSpawnRecord[] BossSpawns { get; set; } = Array.Empty<MapBossSpawnRecord>();
        [JsonProperty("spawnBatches", Required = Required.Always)] public MapSpawnBatchRecord[] SpawnBatches { get; set; } = Array.Empty<MapSpawnBatchRecord>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapPoint
    {
        [JsonProperty("x", Required = Required.Always)] public double X { get; set; }
        [JsonProperty("y", Required = Required.Always)] public double Y { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapChunk
    {
        [JsonProperty("x", Required = Required.Always)] public int X { get; set; }
        [JsonProperty("y", Required = Required.Always)] public int Y { get; set; }
        [JsonProperty("cells", Required = Required.Always)] public int[] Cells { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapVariantOverride
    {
        [JsonProperty("x", Required = Required.Always)] public int X { get; set; }
        [JsonProperty("y", Required = Required.Always)] public int Y { get; set; }
        [JsonProperty("variantId", Required = Required.Always)] public string VariantId { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapInteractionBinding
    {
        [JsonProperty("kind", Required = Required.Always)] public MapInteractionKind Kind { get; set; }
        [JsonProperty("contentId", Required = Required.AllowNull)] public string ContentId { get; set; }
        [JsonProperty("targetMapId", Required = Required.AllowNull)] public string TargetMapId { get; set; }
        [JsonProperty("targetEntryId", Required = Required.AllowNull)] public string TargetEntryId { get; set; }
    }

    public enum MapEntryKind
    {
        [Sirenix.OdinInspector.LabelText("到达 / 返回")] Arrival,
        [Sirenix.OdinInspector.LabelText("新游戏")] NewGame,
        [Sirenix.OdinInspector.LabelText("复活")] Respawn
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapEntryRecord
    {
        [JsonProperty("entryId", Required = Required.Always)] public string EntryId { get; set; }
        [JsonProperty("kind", Required = Required.Always)] public MapEntryKind Kind { get; set; }
        [JsonProperty("position", Required = Required.Always)] public MapPoint Position { get; set; } = new MapPoint();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapBossSpawnRecord
    {
        [JsonProperty("spawnId", Required = Required.Always)] public string SpawnId { get; set; }
        [JsonProperty("monsterDefinitionId", Required = Required.Always)] public string MonsterDefinitionId { get; set; }
        [JsonProperty("position", Required = Required.Always)] public MapPoint Position { get; set; } = new MapPoint();
        [JsonProperty("facing", Required = Required.Always)] public MapPoint Facing { get; set; } = new MapPoint { X = 1 };
    }

    public enum MapSpawnActivation
    {
        [Sirenix.OdinInspector.LabelText("入图生成")]
        OnEnter,
        [Sirenix.OdinInspector.LabelText("玩家进入范围")]
        PlayerRange
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapSpawnBatchRecord
    {
        [JsonProperty("batchId", Required = Required.Always)] public string BatchId { get; set; }
        [JsonProperty("regionId", Required = Required.Always)] public string RegionId { get; set; }
        [JsonProperty("count", Required = Required.Always)] public int Count { get; set; }
        [JsonProperty("activation", Required = Required.Always)] public MapSpawnActivation Activation { get; set; }
        [JsonProperty("triggerCenter", Required = Required.Always)] public MapPoint TriggerCenter { get; set; } = new MapPoint();
        [JsonProperty("triggerRadius", Required = Required.Always)] public double TriggerRadius { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapObjectRecord
    {
        [JsonProperty("objectId", Required = Required.Always)] public string ObjectId { get; set; }
        [JsonProperty("definitionId", Required = Required.Always)] public string DefinitionId { get; set; }
        [JsonProperty("position", Required = Required.Always)] public MapPoint Position { get; set; } = new MapPoint();
        [JsonProperty("rotation", Required = Required.Always)] public int Rotation { get; set; }
        [JsonProperty("appearanceVariant", Required = Required.AllowNull)] public string AppearanceVariant { get; set; }
        [JsonProperty("interaction", Required = Required.AllowNull)] public MapInteractionBinding Interaction { get; set; }
    }

    public enum MapRegionShape { Rectangle, Polygon }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MapRegionRecord
    {
        [JsonProperty("regionId", Required = Required.Always)] public string RegionId { get; set; }
        [JsonProperty("definitionId", Required = Required.Always)] public string DefinitionId { get; set; }
        [JsonProperty("priority", Required = Required.Always)] public int Priority { get; set; }
        [JsonProperty("shape", Required = Required.Always)] public MapRegionShape Shape { get; set; }
        [JsonProperty("points", Required = Required.Always)] public MapPoint[] Points { get; set; } = Array.Empty<MapPoint>();
        [JsonProperty("spawnDefinitionId", Required = Required.AllowNull)] public string SpawnDefinitionId { get; set; }
    }
}
