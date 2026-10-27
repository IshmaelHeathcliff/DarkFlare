using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed partial class WorldMapCatalog
    {
        readonly Dictionary<string, CompiledMapDocument> _previousMaps = new(StringComparer.Ordinal);

        void RegisterPreviousLayout(MapDefinition definition, CompiledMapDocument current)
        {
            if (string.IsNullOrEmpty(definition.PreviousLayoutJson)) { return; }
            CompiledMapDocument previous = MapDocumentCompiler.Compile(MapDocumentJson.Read(definition.PreviousLayoutJson), Definitions);
            string expected = current.MapId == "map:town" ? "b6ec696bb709ff61eca7a724efafdfd7421b54e44bae41a89a63e80f0458dc4d"
                : current.MapId == "map:wilderness" ? "877be5069af7e1b6e440a0bf278873d9ac7b30467b4d0894576c76c82a1fdb8b" : null;
            if (previous.MapId != current.MapId || previous.SourceHash != expected)
            { throw new InvalidOperationException("地图迁移输入与已发布布局不符：" + current.MapId); }
            _previousMaps.Add(current.MapId, previous);
        }

        CompiledMapDocument ResolveSpawnLayout(CompiledMapDocument map, MonsterSpawnerStateDto saved)
        {
            if (PlanMatches(FiniteSpawnPlan.Create(map, Definitions, saved.PlanSeed), saved)) { return map; }
            CompiledMapDocument historical = _previousMaps.TryGetValue(map.MapId, out CompiledMapDocument previous) ? previous : map;
            if (historical.MapId == "map:wilderness" && historical.Revision == 9)
            {
                if (PlanMatches(FiniteSpawnPlan.Create(historical, Definitions, saved.PlanSeed), saved)) { return historical; }
                MapDocument seven = historical.CreateEditableCopy();
                seven.Revision = 7;
                seven.Regions = seven.Regions.Where(r => r.RegionId is "entry_safe" or "wild_spawn").ToArray();
                seven.SpawnBatches = new[]
                {
                    new MapSpawnBatchRecord { BatchId = "east_patrol", RegionId = "wild_spawn", Count = 6,
                        Activation = MapSpawnActivation.PlayerRange, TriggerCenter = new MapPoint { X = 100, Y = 96 }, TriggerRadius = 12 },
                    new MapSpawnBatchRecord { BatchId = "initial", RegionId = "wild_spawn", Count = 6, Activation = MapSpawnActivation.OnEnter }
                };
                CompiledMapDocument legacy = MapDocumentCompiler.Compile(seven, Definitions);
                if (legacy.SourceHash != WildernessRevision7Hash) { throw new InvalidOperationException("旧刷怪计划迁移基线不符"); }
                MonsterSpawnerStateDto expanded = FiniteSpawnPlan.Create(legacy, Definitions, saved.PlanSeed);
                UpgradeFinitePlan(legacy, historical, expanded);
                if (PlanMatches(expanded, saved)) { return historical; }
            }
            throw new InvalidOperationException("刷怪计划身份或预选种子与已登记计划不匹配：" + map.MapId);
        }

        static bool PlanMatches(MonsterSpawnerStateDto expected, MonsterSpawnerStateDto saved)
        {
            if (expected.Batches.Count != saved.Batches.Count || expected.Slots.Count != saved.Slots.Count) { return false; }
            var batches = saved.Batches.ToDictionary(b => b.BatchId, StringComparer.Ordinal);
            if (expected.Batches.Any(b => !batches.ContainsKey(b.BatchId))) { return false; }
            var slots = saved.Slots.ToDictionary(s => s.SlotId, StringComparer.Ordinal);
            return expected.Slots.All(s => slots.TryGetValue(s.SlotId, out SpawnSlotStateDto actual)
                && actual.BatchId == s.BatchId && actual.MonsterDefinitionId == s.MonsterDefinitionId
                && actual.MonsterSeed == s.MonsterSeed && actual.PositionSeed == s.PositionSeed);
        }

        void UpgradeCompactLayouts(RunSaveData run)
        {
            int originalRevision = run.MapRevision;
            run.MapRevision = UpgradeCompact(run.ActiveMapId, run.MapRevision, run.Player.Position, run.Monsters, run.WorldDrops, run.Spawner, run.Boss);
            if (originalRevision == 0 && run.MapRevision > 0 && run.MapRandom == null) { run.MapRandom = MapStateData.Copy(run.Random); }
            foreach (MapStateDto frozen in run.FrozenMaps)
            { frozen.Revision = UpgradeCompact(frozen.MapId, frozen.Revision, null, frozen.Monsters, frozen.WorldDrops, frozen.Spawner, frozen.Boss); }
        }

        int UpgradeCompact(string id, int revision, Vector3Dto player, IReadOnlyList<MonsterRunStateDto> monsters,
            IReadOnlyList<WorldDropStateDto> drops, MonsterSpawnerStateDto spawner, BossEncounterStateDto boss)
        {
            CompiledMapDocument current = Get(id);
            if (!_previousMaps.TryGetValue(id, out CompiledMapDocument previous) || revision == current.Revision
                || current.Width * 2 != previous.Width || current.Height * 2 != previous.Height || current.Revision <= previous.Revision)
            { return revision; }
            bool unversioned = revision == 0 && (id == "map:wilderness" && spawner.Mode == FiniteSpawnMode.LegacyPreserved
                || id == "map:town" && spawner.Mode == FiniteSpawnMode.None && monsters.Count == 0);
            if (revision != previous.Revision && !unversioned)
            { throw new InvalidOperationException("地图没有已登记的布局迁移：" + id + "/" + revision); }
            ValidateState(id, revision, player, monsters, drops, spawner, boss, previous);
            var projection = new CompactMapProjection(previous, current, Definitions);
            if (player != null) { projection.Actor(player, false); }
            foreach (MonsterRunStateDto monster in monsters.OrderBy(m => m.Boss == null ? 1 : 0).ThenBy(m => m.InstanceId, StringComparer.Ordinal))
            {
                projection.Actor(monster.Position, true);
                projection.Point(monster.Behavior.Anchor);
                if (monster.Behavior.MemoryRemaining > 0) { projection.Point(monster.Behavior.LastSeen); }
                if (monster.Behavior.HasWanderGoal) { projection.Point(monster.Behavior.WanderGoal); }
                if (monster.Boss?.MemoryRemaining > 0) { projection.Point(monster.Boss.LastSeen); }
                if (monster.Navigation.Target == NavigationTargetKind.Point) { projection.Point(monster.Navigation.Point); }
            }
            foreach (WorldDropStateDto drop in drops.OrderBy(d => d.InstanceId, StringComparer.Ordinal)) { projection.Drop(drop.Position); }
            return current.Revision;
        }
    }
}
