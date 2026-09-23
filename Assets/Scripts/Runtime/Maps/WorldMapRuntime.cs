using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct WorldMapChangedEvent
    {
        public string MapId { get; }
        public int Generation { get; }
        public WorldMapChangedEvent(string mapId, int generation) { MapId = mapId; Generation = generation; }
    }

    public sealed class MapTransitionFatalException : Exception
    {
        public MapTransitionFatalException(Exception error) : base("地图事务无法安全恢复", error) { }
    }

    // Session 所有者。冻结数据不持有 Unity 对象；所有异步入口由调用者任务组拥有。
    public sealed class WorldMapRuntime : IUtility
    {
        readonly Dictionary<string, MapStateDto> _frozen = new(StringComparer.Ordinal);
        BossEncounterStateDto _activeBoss = new();
        IArchitecture _architecture;
        SessionInitializationContext _context;
        GameplaySceneConfiguration _configuration;
        WorldMapCatalog _catalog;
        ApplicationHost _host;
        SessionSnapshotSource _snapshots;
        int _generation;
        public MapScope Active { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool IsConfigured => _catalog != null;
        public int FrozenCount => _frozen.Count;
        internal Action<string> FaultInjection { get; set; }

        public async UniTask InitializeAsync(SessionInitializationContext context, GameplaySceneConfiguration configuration,
            RunSaveData restored, CancellationToken token)
        {
            _context = context; _configuration = configuration; _architecture = context.Architecture;
            _host = ApplicationHost.Current;
            configuration.WorldMaps.Validate();
            _catalog = WorldMapCatalog.FromContent(context.ContentCatalog);
            foreach (CompiledMapDocument map in _catalog.Maps.Values)
            foreach (MapObjectRecord obj in map.CreateEditableCopy().Objects)
            {
                MapInteractionBinding binding = obj.Interaction;
                if (binding?.Kind == MapInteractionKind.Trader && binding.ContentId != "trader:" + configuration.Trader.Id
                    || binding?.Kind == MapInteractionKind.Crafting && binding.ContentId != "crafting:" + configuration.CraftingDefinition.Id)
                { throw new InvalidOperationException("地图交互与 Session 共享服务配置不匹配：" + obj.ObjectId); }
            }
            string mapId = restored?.ActiveMapId ?? "map:" + configuration.WorldMaps.StartingMap.Id;
            if (restored != null)
            {
                foreach (MapStateDto frozen in restored.FrozenMaps)
                {
                    _catalog.Get(frozen.MapId);
                    _frozen.Add(frozen.MapId, MapStateData.CopyState(frozen));
                }
            }
            Active = await PrepareAsync(mapId, token);
            _activeBoss = restored?.Boss?.Copy() ?? FreshBoss(Active.Map);
            if (restored == null && Active.Spawner.SpawnDefinition != null)
            {
                MonsterDefinition boss = BossDefinition(Active.Map);
                await _architecture.GetSystem<SpawnSystem>().PreloadAsync(null, null,
                    (Active.Spawner.SpawnDefinition?.AllMonsters ?? Array.Empty<MonsterDefinition>())
                        .Concat(boss != null ? new[] { boss } : Array.Empty<MonsterDefinition>()), token);
            }
            if (restored != null)
            {
                ValidatePositions(Active, restored.Player.Position, restored.Monsters, restored.WorldDrops);
                if (restored.MapRevision != 0 && restored.MapRevision != Active.Map.Revision)
                { throw new InvalidOperationException("存档地图修订不匹配"); }
            }
            _architecture.GetUtility<SessionObjectRegistry>().MapOwner = Active;
            Active.Activate(_architecture.GetUtility<WorldSortingSystem>());
            configuration.CameraFollowTarget.SetMapBounds(Active.Geometry.Bounds);
            _snapshots = new SessionSnapshotSource(_host.CurrentSession, context.ContentCatalog, Active.Spawner);
        }

        public Vector3 StartingPosition => _catalog.EntryPosition(Active.Map.MapId, _configuration.WorldMaps.StartingEntryId);
        public Vector3 RespawnPosition
        {
            get
            {
                string id = Active.Map.CreateEditableCopy().Entries.Single(entry => entry.Kind == MapEntryKind.Respawn).EntryId;
                return _catalog.EntryPosition(Active.Map.MapId, id);
            }
        }

        async UniTask<MapScope> PrepareAsync(string mapId, CancellationToken token)
        {
            MapScope candidate = await MapScope.PrepareAsync(_catalog.Get(mapId), checked(++_generation), _configuration.WorldMaps,
                _catalog.Definitions, _context.SceneScope, _host.Resources, _configuration.CameraFollowTarget.gameObject.scene, token);
            try
            {
                foreach (MapEntryRecord entry in candidate.Map.CreateEditableCopy().Entries)
                { RequirePoint(candidate, _catalog.EntryPosition(mapId, entry.EntryId), 0.4); }
                MapBossSpawnRecord boss = candidate.Map.CreateEditableCopy().BossSpawns.SingleOrDefault();
                if (boss != null)
                {
                    MonsterDefinition definition = _catalog.Definitions.Resolve<MonsterDefinition>(ContentId.Parse(boss.MonsterDefinitionId));
                    await _architecture.GetSystem<SpawnSystem>().PreloadAsync(null, null, new[] { definition }, token);
                    GameObject prefab = _architecture.GetUtility<PrefabAssetLoader>().GetPrefab(definition.Prefab);
                    if (prefab == null || !prefab.TryGetComponent(out CircleCollider2D collider))
                    { throw new InvalidOperationException("Boss 点位缺少圆形碰撞 Prefab：" + boss.SpawnId); }
                    Vector3 scale = collider.transform.lossyScale;
                    double radius = collider.radius * Math.Max(Math.Abs(scale.x), Math.Abs(scale.y));
                    RequirePoint(candidate, BossPosition(candidate.Map, boss), radius);
                }
                return candidate;
            }
            catch { await candidate.DisposeAsync(); throw; }
        }

        public async UniTask WaitStableAsync(CancellationToken token)
        {
            while (IsTransitioning) { await UniTask.Yield(PlayerLoopTiming.Update, token); }
        }

        public void AppendFrozenStates(SavePayloadDto payload)
        {
            if (Active == null) { return; }
            payload.Run.ActiveMapId = Active.Map.MapId;
            payload.Run.MapRevision = Active.Map.Revision;
            foreach (MapStateDto state in _frozen.Values.OrderBy(state => state.MapId, StringComparer.Ordinal))
            {
                MapStateDto copy = MapStateData.CopyState(state);
                payload.Run.FrozenMaps.Add(copy);
                payload.Items.AddRange(copy.Items);
            }
        }

        public BossEncounterStateDto CaptureBoss(IReadOnlyList<MonsterController> monsters)
        {
            if (_activeBoss.Status == BossEncounterStatus.Alive
                && !monsters.Any(monster => monster != null && monster.Actor.IsAlive
                    && monster.Instance.Id.Value == _activeBoss.InstanceId))
            { _activeBoss.Status = BossEncounterStatus.Defeated; }
            return _activeBoss.Copy();
        }

        public void SpawnFreshBoss()
        {
            if (Active == null || _activeBoss.Status != BossEncounterStatus.Ready) { return; }
            SpawnBoss(Active, _activeBoss);
        }

        public async UniTask ChangeAsync(string mapId, string entryId, CancellationToken token)
        {
            if (Active == null || IsTransitioning) { throw new InvalidOperationException("地图当前不可切换"); }
            Vector3 position = _catalog.EntryPosition(mapId, entryId);
            if (mapId == Active.Map.MapId) { return; }
            StatusSystem statuses = _architecture.GetSystem<StatusSystem>();
            while (statuses.IsClockHeld) { await UniTask.Yield(PlayerLoopTiming.LastUpdate, token); }
            token.ThrowIfCancellationRequested();
            IsTransitioning = true;
            MapScope source = Active, candidate = null;
            bool committed = false;
            try
            {
                using (await statuses.HoldClockAsync(token))
                {
                    MapStateDto sourceState = _snapshots.CaptureMap(source);
                    var issues = new List<DtoMapIssue>();
                    StatusSaveDto sourceStatuses = statuses.Store.CaptureSave(_context.ContentCatalog, issues);
                    if (issues.Count != 0) { throw new InvalidOperationException("无法冻结状态"); }
                    candidate = await PrepareAsync(mapId, token);
                    FaultInjection?.Invoke("prepared");
                    MapStateDto targetState = _frozen.TryGetValue(mapId, out MapStateDto frozen) ? MapStateData.CopyState(frozen) : FreshState(candidate);
                    if (targetState.Revision != 0 && targetState.Revision != candidate.Map.Revision)
                    { throw new InvalidOperationException("冻结地图修订不匹配"); }
                    List<PreparedMonsterRestore> monsters = SaveRestorePreparer.RestoreMonsters(targetState.Monsters, _context.ContentCatalog, issues);
                    var items = new Dictionary<string, ItemInstance>(StringComparer.Ordinal);
                    foreach (ItemInstanceDto dto in targetState.Items)
                    {
                        DtoMapResult<ItemInstance> result = RuntimeStateMapper.FromDto(dto, _context.ContentCatalog);
                        if (!result.Succeeded) { issues.AddRange(result.Issues); } else { items.Add(dto.InstanceId, result.Value); }
                    }
                    GameplayRandomState random = SaveRestorePreparer.RestoreRandomState(targetState.Random, issues);
                    if (issues.Count != 0) { throw new InvalidOperationException(string.Join("; ", issues.Select(issue => issue.Message))); }
                    MonsterDefinition bossDefinition = BossDefinition(candidate.Map);
                    await _architecture.GetSystem<SpawnSystem>().PreloadAsync(null, null,
                        monsters.Select(monster => monster.Definition)
                            .Concat(candidate.Spawner.SpawnDefinition?.AllMonsters ?? Array.Empty<MonsterDefinition>())
                            .Concat(bossDefinition != null ? new[] { bossDefinition } : Array.Empty<MonsterDefinition>()).Distinct(), token);
                    token.ThrowIfCancellationRequested();
                    ValidatePositions(candidate, Vector(position), targetState.Monsters, targetState.WorldDrops);
                    SessionObjectRegistry registry = _architecture.GetUtility<SessionObjectRegistry>();
                    PlayerController player = registry.CaptureObjects().Select(obj => obj.GetComponent<PlayerController>()).Single(value => value != null);
                    Vector3 oldPosition = player.transform.position;
                    CombatResourceSnapshot playerResources = player.Actor.Resources;
                    IReadOnlyList<GameObject> sourceObjects = registry.CaptureOwner(source);
                    try
                    {
                        source.Deactivate();
                        foreach (GameObject obj in sourceObjects) { if (obj != null) { obj.SetActive(false); } }
                        FaultInjection?.Invoke("source-detached");
                        Active = candidate; registry.MapOwner = candidate;
                        _architecture.GetSystem<GameplayRandomSystem>().RestoreMapState(random);
                        candidate.Activate(_architecture.GetUtility<WorldSortingSystem>());
                        ImportMap(targetState, monsters, items, sourceStatuses.Time);
                        FaultInjection?.Invoke("target-restored");
                        player.transform.position = position;
                        if (player.TryGetComponent(out Rigidbody2D body)) { body.position = position; body.linearVelocity = Vector2.zero; }
                        _configuration.CameraFollowTarget.SetMapBounds(candidate.Geometry.Bounds);
                        FaultInjection?.Invoke("before-commit");
                        _frozen.Remove(mapId); _frozen[source.Map.MapId] = sourceState;
                        committed = true;
                    }
                    catch (Exception failure)
                    {
                        try
                        {
                            candidate.Deactivate();
                            foreach (GameObject obj in registry.CaptureOwner(candidate)) { if (obj != null) { obj.SetActive(false); } }
                            Active = source; registry.MapOwner = source;
                            _activeBoss = sourceState.Boss.Copy();
                            source.Activate(_architecture.GetUtility<WorldSortingSystem>());
                            foreach (GameObject obj in sourceObjects)
                            {
                                if (obj == null) { continue; }
                                obj.SetActive(true);
                                if (obj.TryGetComponent(out MonsterController monster) && monster.Actor.IsAlive)
                                { statuses.Bind(monster.Actor, monster.Instance.Id); }
                            }
                            statuses.Store.ImportSave(sourceStatuses, _context.ContentCatalog, new List<DtoMapIssue>());
                            foreach (GameObject obj in sourceObjects)
                            {
                                if (obj != null && obj.TryGetComponent(out MonsterController monster))
                                {
                                    MonsterRunStateDto dto = sourceState.Monsters.FirstOrDefault(value => value.InstanceId == monster.Instance.Id.Value);
                                    if (dto != null)
                                    {
                                        monster.RestoreRuntime(Resources(dto.Resources), dto.ContactDamageCooldownRemainingSeconds);
                                        monster.RestoreBehavior(dto.Behavior);
                                        if (dto.Boss != null) { monster.RestoreBoss(dto.Boss); }
                                    }
                                }
                            }
                            player.transform.position = oldPosition;
                            if (player.TryGetComponent(out Rigidbody2D oldBody)) { oldBody.position = oldPosition; oldBody.linearVelocity = Vector2.zero; }
                            player.Actor.RestoreResources(playerResources, player.Actor.IsAlive);
                            _configuration.CameraFollowTarget.SetMapBounds(source.Geometry.Bounds);
                            _architecture.GetSystem<GameplayRandomSystem>().RestoreMapState(SaveRestorePreparer.RestoreRandomState(sourceState.Random, new List<DtoMapIssue>()));
                            if (sourceState.Spawner.IsRunning) { source.Spawner.BeginSpawning(sourceState.Spawner.NextSpawnRemainingSeconds); }
                        }
                        catch (Exception rollback) { throw new MapTransitionFatalException(new AggregateException(failure, rollback)); }
                        throw;
                    }
                    // 提交之后清理不可取消，不能在此返回“取消且未切图”。
                    try
                    {
                        await registry.ReleaseOwnerAsync(source);
                        await source.DisposeAsync();
                        FaultInjection?.Invoke("source-released");
                    }
                    catch (Exception error) { throw new MapTransitionFatalException(error); }
                }
            }
            finally
            {
                try
                {
                    if (!committed && candidate != null)
                    {
                        try
                        {
                            await _architecture.GetUtility<SessionObjectRegistry>().ReleaseOwnerAsync(candidate);
                            await candidate.DisposeAsync();
                        }
                        catch (Exception error) { throw new MapTransitionFatalException(error); }
                    }
                }
                finally { IsTransitioning = false; }
            }
            _architecture.SendEvent(new WorldMapChangedEvent(Active.Map.MapId, Active.Generation));
        }

        void ImportMap(MapStateDto state, IReadOnlyList<PreparedMonsterRestore> monsters, IReadOnlyDictionary<string, ItemInstance> items, double time)
        {
            var controllers = new List<MonsterController>();
            foreach (PreparedMonsterRestore monster in monsters)
            {
                MonsterController controller = _architecture.GetSystem<SpawnSystem>().SpawnRestoredMonster(monster.Definition, monster.Instance,
                    Position(monster.State.Position), Resources(monster.State.Resources), monster.State.ContactDamageCooldownRemainingSeconds, true);
                if (controller == null) { throw new InvalidOperationException("恢复怪物失败"); }
                controller.RestoreNavigation(monster.State.Navigation);
                controllers.Add(controller);
                if (monster.Definition.BossBehavior == null) { Active.Spawner.RegisterRestoredMonster(controller); }
            }
            StatusSaveDto shifted = MapStateData.AtTime(state.Statuses, time);
            _architecture.GetSystem<StatusSystem>().Store.ImportSave(shifted, _context.ContentCatalog, new List<DtoMapIssue>());
            for (int i = 0; i < controllers.Count; i++)
            {
                controllers[i].RestoreRuntime(Resources(monsters[i].State.Resources), monsters[i].State.ContactDamageCooldownRemainingSeconds);
                controllers[i].RestoreBehavior(monsters[i].State.Behavior);
                if (monsters[i].State.Boss != null) { controllers[i].RestoreBoss(monsters[i].State.Boss); }
            }
            _activeBoss = state.Boss.Copy();
            if (_activeBoss.Status == BossEncounterStatus.Ready) { SpawnBoss(Active, _activeBoss); }
            foreach (WorldDropStateDto drop in state.WorldDrops)
            {
                if (_architecture.GetSystem<LootSystem>().SpawnRestoredPickup(WorldDropId.Parse(drop.InstanceId), items[drop.ItemInstanceId], Position(drop.Position)) == null)
                { throw new InvalidOperationException("恢复掉落失败"); }
            }
            if (state.Spawner.IsRunning && Active.Spawner.SpawnDefinition != null)
            { Active.Spawner.BeginSpawning(state.Spawner.NextSpawnRemainingSeconds); }
        }

        MapStateDto FreshState(MapScope scope)
        {
            return new MapStateDto
            {
                MapId = scope.Map.MapId, Revision = scope.Map.Revision,
                Random = SessionSnapshotSource.MapRandom(_architecture.GetSystem<GameplayRandomSystem>().CreateMapState(scope.Map.MapId)),
                Spawner = new MonsterSpawnerStateDto { IsRunning = scope.Spawner.SpawnDefinition != null,
                    SpawnContentId = scope.Spawner.SpawnDefinition != null ? "spawn:" + scope.Spawner.SpawnDefinition.Id : "" },
                Statuses = new StatusSaveDto { Time = _architecture.GetSystem<StatusSystem>().Store.Time }
                , Boss = FreshBoss(scope.Map)
            };
        }

        BossEncounterStateDto FreshBoss(CompiledMapDocument map)
        {
            MapBossSpawnRecord point = map.CreateEditableCopy().BossSpawns.SingleOrDefault();
            return point == null ? new BossEncounterStateDto() : new BossEncounterStateDto
            {
                Status = BossEncounterStatus.Ready, SpawnId = point.SpawnId,
                MonsterDefinitionId = point.MonsterDefinitionId
            };
        }

        MonsterDefinition BossDefinition(CompiledMapDocument map)
        {
            MapBossSpawnRecord point = map.CreateEditableCopy().BossSpawns.SingleOrDefault();
            return point == null ? null : _catalog.Definitions.Resolve<MonsterDefinition>(ContentId.Parse(point.MonsterDefinitionId));
        }

        void SpawnBoss(MapScope scope, BossEncounterStateDto state)
        {
            MapBossSpawnRecord point = scope.Map.CreateEditableCopy().BossSpawns.Single(item => item.SpawnId == state.SpawnId);
            MonsterDefinition definition = BossDefinition(scope.Map);
            int seed = _architecture.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.MonsterInstance);
            MonsterController boss = _architecture.GetSystem<SpawnSystem>().SpawnFixedMonster(definition, seed, BossPosition(scope.Map, point));
            if (boss == null) { throw new InvalidOperationException("Boss 生成失败：" + point.SpawnId); }
            BossBehaviorStateDto behavior = boss.CaptureBoss();
            behavior.Facing.X = (float)point.Facing.X; behavior.Facing.Y = (float)point.Facing.Y;
            boss.RestoreBoss(behavior);
            state.Status = BossEncounterStatus.Alive; state.InstanceId = boss.Instance.Id.Value;
        }

        static Vector3 BossPosition(CompiledMapDocument map, MapBossSpawnRecord point)
        {
            return new Vector3((float)(map.OriginX + point.Position.X), (float)(map.OriginY + point.Position.Y));
        }

        public async UniTask DisposeAsync()
        {
            if (Active != null)
            {
                await _architecture.GetUtility<SessionObjectRegistry>().ReleaseOwnerAsync(Active);
                await Active.DisposeAsync();
                Active = null;
            }
            _frozen.Clear();
        }

        void ValidatePositions(MapScope scope, Vector3Dto player, IEnumerable<MonsterRunStateDto> monsters, IEnumerable<WorldDropStateDto> drops)
        {
            PrefabAssetLoader loader = _architecture.GetUtility<PrefabAssetLoader>();
            CheckSavedPoint(scope, player, loader.GetPrefab(_configuration.PlayerCharacter.Prefab));
            foreach (MonsterRunStateDto monster in monsters)
            {
                MonsterDefinition definition = _catalog.Definitions.Resolve<MonsterDefinition>(ContentId.Parse(monster.DefinitionContentId));
                CheckSavedPoint(scope, monster.Position, loader.GetPrefab(definition.Prefab));
            }
            foreach (WorldDropStateDto drop in drops) { CheckSavedPoint(scope, drop.Position, loader.GetPrefab(_configuration.LootPickupPrefab)); }
        }

        static void CheckSavedPoint(MapScope scope, Vector3Dto point, GameObject prefab)
        {
            if (prefab == null || !prefab.TryGetComponent(out CircleCollider2D collider))
            { throw new InvalidOperationException("恢复落点校验缺少圆形碰撞 Prefab"); }
            Vector3 scale = collider.transform.lossyScale;
            // 保存物理接触位置必须允许求解器的接触偏差，不能套用导航出生点的额外净空。
            double radius = Math.Max(0.001, collider.radius * Math.Max(Math.Abs(scale.x), Math.Abs(scale.y)) - Physics2D.defaultContactOffset);
            if (scope.Geometry.CheckPoint(new MapGeometryPoint(point.X, point.Y), new MapGroundProfile(radius, 0)) != MapGeometryResult.Valid)
            { throw new InvalidOperationException("存档对象足迹侵入地图阻挡：" + scope.Map.MapId); }
        }

        static void RequirePoint(MapScope scope, Vector3 point, double radius)
        {
            if (scope.Geometry.CheckPoint(new MapGeometryPoint(point.x, point.y), new MapGroundProfile(radius)) != MapGeometryResult.Valid)
            { throw new InvalidOperationException("地图落点没有合法净空：" + scope.Map.MapId + " " + point); }
        }

        static Vector3 Position(Vector3Dto p) { return new Vector3(p.X, p.Y, p.Z); }
        static Vector3Dto Vector(Vector3 p) { return new Vector3Dto { X = p.x, Y = p.y, Z = p.z }; }
        static CombatResourceSnapshot Resources(CombatResourceStateDto r) { return new CombatResourceSnapshot(r.CurrentHealth, r.MaxHealth, r.CurrentMana, r.MaxMana); }

    }
}
