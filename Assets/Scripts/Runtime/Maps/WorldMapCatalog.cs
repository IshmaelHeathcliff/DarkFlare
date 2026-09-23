using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkFlare
{
    public sealed class WorldMapCatalog
    {
        const string WildernessRevision4WithoutBatchesHash = "755d062a4677c29ab4e14c455387a19b725819c02a8c2894aacf7e9c85e0926c";
        const string WildernessRevision1WithoutBossOrBatchesHash = "818a95fb62d51a31e626bf0e0433ec5b0df9d74aa2434c2f6b8c49d585f42e8c";
        const string WildernessRevision7Hash = "d96663c6299ba37effbcfa140f8d033862449dbff8df49fb8dfbdc02a08f4bf9";
        readonly Dictionary<string, CompiledMapDocument> _maps = new(StringComparer.Ordinal);
        public IMapDefinitionLookup Definitions { get; }
        public IReadOnlyDictionary<string, CompiledMapDocument> Maps => _maps;

        public static WorldMapCatalog FromContent(ContentCatalog content)
        {
            return new WorldMapCatalog(content.GetAll<MapDefinition>(), new CatalogLookup(content));
        }

        public void UpgradeKnownWildernessRevision(RunSaveData run)
        {
            Upgrade(run.ActiveMapId, value => run.MapRevision = value, run.MapRevision, run.Spawner);
            foreach (MapStateDto frozen in run.FrozenMaps)
            { Upgrade(frozen.MapId, value => frozen.Revision = value, frozen.Revision, frozen.Spawner); }
        }

        void Upgrade(string id, Action<int> set, int oldRevision, MonsterSpawnerStateDto spawner)
        {
            if (id != "map:wilderness" || oldRevision is not (1 or 4 or 7)) { return; }
            CompiledMapDocument current = Get(id);
            if (current.Revision != 9) { return; }
            MapDocument source = current.CreateEditableCopy();
            if (source.BossSpawns.Length != 1 || source.SpawnBatches.Length != 11) { return; }
            source.Regions = source.Regions.Where(region => region.RegionId is "entry_safe" or "wild_spawn").ToArray();
            source.SpawnBatches = oldRevision == 7 ? new[]
            {
                new MapSpawnBatchRecord { BatchId = "east_patrol", RegionId = "wild_spawn", Count = 6,
                    Activation = MapSpawnActivation.PlayerRange, TriggerCenter = new MapPoint { X = 100, Y = 96 }, TriggerRadius = 12 },
                new MapSpawnBatchRecord { BatchId = "initial", RegionId = "wild_spawn", Count = 6,
                    Activation = MapSpawnActivation.OnEnter }
            } : Array.Empty<MapSpawnBatchRecord>();
            if (oldRevision == 1) { source.BossSpawns = Array.Empty<MapBossSpawnRecord>(); }
            source.Revision = oldRevision;
            CompiledMapDocument legacy = MapDocumentCompiler.Compile(source, Definitions);
            string expected = oldRevision == 1 ? WildernessRevision1WithoutBossOrBatchesHash
                : oldRevision == 4 ? WildernessRevision4WithoutBatchesHash : WildernessRevision7Hash;
            if (legacy.SourceHash != expected)
            { throw new InvalidOperationException("野外地图旧版语义与有限刷怪迁移基线不符"); }
            if (oldRevision == 7 && spawner.Mode == FiniteSpawnMode.Finite)
            { UpgradeFinitePlan(legacy, current, spawner); }
            set(current.Revision);
        }

        void UpgradeFinitePlan(CompiledMapDocument legacy, CompiledMapDocument current, MonsterSpawnerStateDto spawner)
        {
            MonsterSpawnerStateDto oldPlan = FiniteSpawnPlan.Create(legacy, Definitions, spawner.PlanSeed);
            if (spawner.Batches.Count != oldPlan.Batches.Count || spawner.Slots.Count != oldPlan.Slots.Count)
            { throw new InvalidOperationException("旧版刷怪计划数量不匹配"); }
            Dictionary<string, SpawnBatchStateDto> oldBatches = spawner.Batches.ToDictionary(batch => batch.BatchId, StringComparer.Ordinal);
            Dictionary<string, SpawnSlotStateDto> oldSlots = spawner.Slots.ToDictionary(slot => slot.SlotId, StringComparer.Ordinal);
            foreach (SpawnBatchStateDto batch in oldPlan.Batches)
            {
                if (!oldBatches.ContainsKey(batch.BatchId)) { throw new InvalidOperationException("旧版刷怪批次不匹配"); }
            }
            foreach (SpawnSlotStateDto slot in oldPlan.Slots)
            {
                if (!oldSlots.TryGetValue(slot.SlotId, out SpawnSlotStateDto saved)
                    || saved.BatchId != slot.BatchId || saved.MonsterDefinitionId != slot.MonsterDefinitionId
                    || saved.MonsterSeed != slot.MonsterSeed || saved.PositionSeed != slot.PositionSeed)
                { throw new InvalidOperationException("旧版刷怪名额与原计划不匹配：" + slot.SlotId); }
            }
            MonsterSpawnerStateDto expanded = FiniteSpawnPlan.Create(current, Definitions, spawner.PlanSeed);
            foreach (SpawnBatchStateDto batch in expanded.Batches)
            {
                if (oldBatches.TryGetValue(batch.BatchId, out SpawnBatchStateDto saved))
                { batch.IsActivated = saved.IsActivated; }
            }
            for (int i = 0; i < expanded.Slots.Count; i++)
            {
                if (oldSlots.TryGetValue(expanded.Slots[i].SlotId, out SpawnSlotStateDto saved))
                { expanded.Slots[i] = saved; }
            }
            spawner.Batches = expanded.Batches;
            spawner.Slots = expanded.Slots;
        }

        public void ValidateSave(RunSaveData run)
        {
            ValidateState(run.ActiveMapId, run.MapRevision, run.Player.Position, run.Monsters, run.WorldDrops, run.Spawner, run.Boss);
            foreach (MapStateDto map in run.FrozenMaps)
            { ValidateState(map.MapId, map.Revision, null, map.Monsters, map.WorldDrops, map.Spawner, map.Boss); }
        }

        void ValidateState(string id, int revision, Vector3Dto player, IEnumerable<MonsterRunStateDto> monsters,
            IEnumerable<WorldDropStateDto> drops, MonsterSpawnerStateDto spawner, BossEncounterStateDto boss)
        {
            CompiledMapDocument map = Get(id);
            if (revision != 0 && revision != map.Revision) { throw new InvalidOperationException("地图修订不匹配：" + id); }
            MapDocument document = map.CreateEditableCopy();
            if (boss == null || !boss.IsValid) { throw new InvalidOperationException("Boss 名额非法：" + id); }
            MapBossSpawnRecord point = document.BossSpawns.SingleOrDefault();
            if (point == null)
            {
                if (boss.Status is not (BossEncounterStatus.None or BossEncounterStatus.LegacyAbsent))
                { throw new InvalidOperationException("无 Boss 地图包含 Boss 名额：" + id); }
            }
            else if (boss.Status != BossEncounterStatus.LegacyAbsent
                && (boss.Status == BossEncounterStatus.None || boss.SpawnId != point.SpawnId
                    || boss.MonsterDefinitionId != point.MonsterDefinitionId))
            { throw new InvalidOperationException("Boss 点位与存档不匹配：" + id); }
            string spawn = document.Regions.Select(region => region.SpawnDefinitionId).FirstOrDefault(value => value != null);
            if (!string.Equals(spawn ?? "", spawner.SpawnContentId ?? "", StringComparison.Ordinal))
            { throw new InvalidOperationException("地图生成定义不匹配：" + id); }
            if (document.SpawnBatches.Length > 0 && spawner.Mode == FiniteSpawnMode.None)
            { throw new InvalidOperationException("有限刷怪地图缺少名额模式：" + id); }
            if (spawner.Mode == FiniteSpawnMode.Finite)
            {
                if (spawner.Batches.Count != document.SpawnBatches.Length
                    || spawner.Slots.Count != document.SpawnBatches.Sum(batch => batch.Count))
                { throw new InvalidOperationException("刷怪计划数量与地图批次不匹配：" + id); }
                foreach (MapSpawnBatchRecord batch in document.SpawnBatches)
                {
                    SpawnBatchStateDto state = spawner.Batches.SingleOrDefault(value => value.BatchId == batch.BatchId);
                    if (state == null || batch.Activation == MapSpawnActivation.OnEnter && !state.IsActivated)
                    { throw new InvalidOperationException("刷怪批次状态与地图不匹配：" + batch.BatchId); }
                    MapRegionRecord region = document.Regions.Single(value => value.RegionId == batch.RegionId);
                    MonsterSpawnDefinition pool = Definitions.Resolve<MonsterSpawnDefinition>(ContentId.Parse(region.SpawnDefinitionId));
                    HashSet<string> allowed = pool.AllMonsters.Select(value => "monster:" + value.Id).ToHashSet(StringComparer.Ordinal);
                    for (int index = 0; index < batch.Count; index++)
                    {
                        string slotId = batch.BatchId + "_" + index;
                        SpawnSlotStateDto slot = spawner.Slots.SingleOrDefault(value => value.SlotId == slotId);
                        if (slot == null || slot.BatchId != batch.BatchId || !allowed.Contains(slot.MonsterDefinitionId))
                        { throw new InvalidOperationException("刷怪名额与区域怪物池不匹配：" + slotId); }
                    }
                }
            }
            MapGeometrySnapshot geometry = MapGeometryCompiler.Compile(MapGeometryInput.Capture(map, Definitions, new MapGeometrySettings()));
            void Check(Vector3Dto point)
            {
                // 纯预检没有加载角色 Prefab；先拒绝阻挡内部，实际足迹在资源预热后校验。
                if (geometry.CheckPoint(new MapGeometryPoint(point.X, point.Y), new MapGroundProfile(0.001, 0)) != MapGeometryResult.Valid)
                { throw new InvalidOperationException("存档地图落点没有合法净空：" + id); }
            }
            if (player != null) { Check(player); }
            foreach (MonsterRunStateDto monster in monsters)
            {
                Check(monster.Position);
                if (monster.Navigation.Target == NavigationTargetKind.Point)
                {
                    Vector3Dto target = monster.Navigation.Point;
                    if (target.X < geometry.Bounds.MinX || target.X >= geometry.Bounds.MaxX
                        || target.Y < geometry.Bounds.MinY || target.Y >= geometry.Bounds.MaxY)
                    { throw new InvalidOperationException("导航固定点位于地图之外：" + id); }
                }
            }
            foreach (WorldDropStateDto drop in drops) { Check(drop.Position); }
        }

        public WorldMapCatalog(IEnumerable<MapDefinition> maps, IMapDefinitionLookup definitions)
        {
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            foreach (MapDefinition definition in maps)
            {
                CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentJson.Read(definition.CanonicalJson), definitions);
                if (!_maps.TryAdd(map.MapId, map)) { throw new InvalidOperationException("地图身份重复：" + map.MapId); }
                MapDocument document = map.CreateEditableCopy();
                if (document.Entries.Count(entry => entry.Kind == MapEntryKind.Respawn) != 1)
                { throw new InvalidOperationException("正式地图必须有唯一复活入口：" + map.MapId); }
            }
            foreach (CompiledMapDocument map in _maps.Values)
            {
                foreach (MapObjectRecord obj in map.CreateEditableCopy().Objects)
                {
                    if (obj.Interaction?.Kind == MapInteractionKind.Portal)
                    { GetEntry(obj.Interaction.TargetMapId, obj.Interaction.TargetEntryId); }
                }
            }
        }

        public CompiledMapDocument Get(string mapId)
        {
            return _maps.TryGetValue(mapId, out CompiledMapDocument map) ? map : throw new InvalidOperationException("未知地图：" + mapId);
        }

        public MapEntryRecord GetEntry(string mapId, string entryId)
        {
            return Get(mapId).CreateEditableCopy().Entries.SingleOrDefault(entry => entry.EntryId == entryId)
                ?? throw new InvalidOperationException("未知入口：" + mapId + "/" + entryId);
        }

        public Vector3 EntryPosition(string mapId, string entryId)
        {
            CompiledMapDocument map = Get(mapId);
            MapEntryRecord entry = GetEntry(mapId, entryId);
            return new Vector3((float)(map.OriginX + entry.Position.X), (float)(map.OriginY + entry.Position.Y), 0);
        }

        sealed class CatalogLookup : IMapDefinitionLookup
        {
            readonly ContentCatalog _content;
            public CatalogLookup(ContentCatalog content) { _content = content; }
            public T Resolve<T>(ContentId id) where T : ScriptableObject, IContentDefinition
            { return _content.TryResolve(id, out T result) ? result : throw new InvalidOperationException("缺少地图内容：" + id); }
        }
    }
}
