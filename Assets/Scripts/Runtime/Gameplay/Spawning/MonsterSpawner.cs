using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    public class MonsterSpawner : MonoBehaviour, IController
    {
        const int MaxSpawnPositionAttempts = 8;
        MapGeometrySnapshot _mapGeometry;
        MapGeometryRegion[] _spawnRegions;
        CompiledMapDocument _map;
        IMapDefinitionLookup _definitions;
        MapSpawnBatchRecord[] _batches = System.Array.Empty<MapSpawnBatchRecord>();
        MonsterSpawnerStateDto _finiteState;
        Vector2 _lastPlayerPosition;
        bool _hasPlayerPosition;
        [ShowInInspector, ReadOnly, LabelText("失败尝试次数")]
        public int RejectedSpawnAttempts { get; private set; }
        [ShowInInspector, ReadOnly, LabelText("最近刷怪失败")]
        public string LastSpawnFailure { get; private set; } = string.Empty;
        [ShowInInspector, ReadOnly, LabelText("本帧刷怪耗时(ms)")]
        public double LastSpawnMilliseconds { get; private set; }
        [ShowInInspector, ReadOnly, LabelText("普通怪物名额总数")]
        public int PlannedCount => _finiteState?.Slots.Count ?? 0;
        [ShowInInspector, ReadOnly, LabelText("待生成名额")]
        public int PendingCount => _finiteState?.Slots.Count(slot => slot.Status == SpawnSlotStatus.Pending) ?? 0;
        [ShowInInspector, ReadOnly, LabelText("存活名额")]
        public int AliveCount => _finiteState?.Slots.Count(slot => slot.Status == SpawnSlotStatus.Alive) ?? 0;
        [ShowInInspector, ReadOnly, LabelText("已击败名额")]
        public int DefeatedCount => _finiteState?.Slots.Count(slot => slot.Status == SpawnSlotStatus.Defeated) ?? 0;
        [ShowInInspector, ReadOnly, LabelText("已激活批次")]
        public string ActivatedBatches => _finiteState == null ? string.Empty
            : string.Join(", ", _finiteState.Batches.Where(batch => batch.IsActivated).Select(batch => batch.BatchId));

        public void ConfigureMap(CompiledMapDocument map, MapGeometrySnapshot geometry, IMapDefinitionLookup definitions)
        {
            StopSpawning();
            _map = map;
            _mapGeometry = geometry;
            _definitions = definitions;
            _spawnRegions = geometry.Regions.Where(region => region.Kind == MapRegionKind.Spawn).ToArray();
            _batches = map.CreateEditableCopy().SpawnBatches;
            _spawnDefinition = _spawnRegions.Length == 0 ? null : definitions.Resolve<MonsterSpawnDefinition>(ContentId.Parse(_spawnRegions[0].SpawnDefinitionId));
        }

        [SerializeField]
        MonsterSpawnDefinition _spawnDefinition;

        [SerializeField]
        Transform _target;

        [SerializeField]
        Collider2D _worldBounds;

        [SerializeField]
        [Min(0f)]
        float _spawnBoundsPadding = 0.5f;

        readonly List<MonsterController> _aliveMonsters = new List<MonsterController>();
        readonly HashSet<string> _aliveIds = new HashSet<string>(System.StringComparer.Ordinal);
        LifecycleScope _spawnScope;
        IArchitecture _architecture;
        bool _isSpawning;
        uint _spawnVersion;
        float _nextSpawnAttemptTime;

        public Collider2D WorldBounds => _worldBounds;

        public MonsterSpawnDefinition SpawnDefinition => _spawnDefinition;

        public bool HasBatches => _batches.Length > 0;

        public IEnumerable<MonsterDefinition> AllMonsters => _mapGeometry == null
            ? _spawnDefinition?.AllMonsters ?? System.Array.Empty<MonsterDefinition>()
            : _spawnRegions.SelectMany(region => _definitions.Resolve<MonsterSpawnDefinition>(
                ContentId.Parse(region.SpawnDefinitionId)).AllMonsters).Distinct();

        public MonsterSpawnerStateDto CreateFreshState(int seed)
        {
            return _map == null ? new MonsterSpawnerStateDto() : FiniteSpawnPlan.Create(_map, _definitions, seed);
        }

        public void RestoreState(MonsterSpawnerStateDto state)
        {
            StopSpawning();
            _finiteState = state == null ? null : MapStateData.Copy(state);
            _hasPlayerPosition = false;
            LastSpawnFailure = string.Empty;
        }

        public MonsterSpawnerStateDto CaptureState()
        {
            if (_mapGeometry == null || _finiteState == null)
            {
                return new MonsterSpawnerStateDto
                {
                    SpawnContentId = _spawnDefinition != null ? "spawn:" + _spawnDefinition.Id : string.Empty,
                    IsRunning = _isSpawning,
                    NextSpawnRemainingSeconds = NextSpawnRemainingSeconds
                };
            }
            RefreshFiniteStatus();
            MonsterSpawnerStateDto state = MapStateData.Copy(_finiteState);
            state.SpawnContentId = _spawnDefinition != null ? "spawn:" + _spawnDefinition.Id : string.Empty;
            state.IsRunning = _isSpawning;
            state.NextSpawnRemainingSeconds = 0f;
            return state;
        }

        public bool IsSpawning => _isSpawning;

        public float NextSpawnRemainingSeconds => _isSpawning
            ? Mathf.Max(0f, _nextSpawnAttemptTime - Time.time)
            : 0f;

        public IArchitecture GetArchitecture()
        {
            return _architecture ?? GameArchitectureProvider.RequireCurrent();
        }

        public void SetWorldBounds(Collider2D worldBounds)
        {
            _worldBounds = worldBounds;
        }

        public void PrepareForInitialization(MonsterSpawnDefinition spawnDefinition)
        {
            StopSpawning();
            _spawnDefinition = spawnDefinition;
            _finiteState = null;
            _aliveMonsters.Clear();

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void BeginSpawning()
        {
            BeginSpawning(0f);
        }

        public void BeginSpawning(float initialDelaySeconds)
        {
            if (_isSpawning)
            {
                return;
            }

            if (_mapGeometry != null && _finiteState?.Mode == FiniteSpawnMode.LegacyPreserved) { return; }
            if (_mapGeometry != null && _finiteState?.Mode == FiniteSpawnMode.Finite)
            {
                if (!isActiveAndEnabled) { throw new System.InvalidOperationException("地图刷怪器未启用"); }
                _architecture = GameArchitectureProvider.RequireCurrent();
                SpawnPendingActivated(int.MaxValue, true, onlyInitial: true);
                _spawnScope = ComponentLifecycle.CreateScope(this, "finite-spawn", this.GetCancellationTokenOnDestroy());
                _isSpawning = true;
                uint finiteVersion = ++_spawnVersion;
                _spawnScope.Tasks.Run("finite-spawn", token => FiniteLoopAsync(finiteVersion, token),
                    failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
                return;
            }
            if (_mapGeometry != null || _spawnDefinition == null)
            {
                throw new System.InvalidOperationException("MonsterSpawner 缺少生成定义");
            }

            if (!isActiveAndEnabled)
            {
                throw new System.InvalidOperationException("MonsterSpawner 必须先启用再开始生成");
            }

            if (float.IsNaN(initialDelaySeconds)
                || float.IsInfinity(initialDelaySeconds)
                || initialDelaySeconds < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(initialDelaySeconds));
            }

            _spawnScope = ComponentLifecycle.CreateScope(
                this,
                "spawn-loop",
                this.GetCancellationTokenOnDestroy());
            _architecture = GameArchitectureProvider.RequireCurrent();
            _isSpawning = true;
            uint version = ++_spawnVersion;
            IArchitecture architecture = _architecture;
            _spawnScope.Tasks.Run(
                "spawn-loop",
                token => SpawnLoopAsync(version, architecture, initialDelaySeconds, token),
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        public void RegisterRestoredMonster(MonsterController monster)
        {
            if (monster != null && !_aliveMonsters.Contains(monster))
            {
                _aliveMonsters.Add(monster);
            }
        }

        public void StopSpawning()
        {
            _isSpawning = false;
            _spawnVersion++;
            _nextSpawnAttemptTime = 0f;
            _spawnScope?.BeginStop();
            _spawnScope = null;
        }

        void OnDisable()
        {
            StopSpawning();
        }

        async UniTask FiniteLoopAsync(uint version, System.Threading.CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    if (GameTimeService.Shared.IsPaused) { continue; }
                    RefreshFiniteStatus();
                    Transform target = GetTarget();
                    if (target != null)
                    {
                        Vector2 current = target.position;
                        Vector2 previous = _hasPlayerPosition ? _lastPlayerPosition : current;
                        for (int i = 0; i < _batches.Length; i++)
                        {
                            MapSpawnBatchRecord batch = _batches[i];
                            if (batch.Activation != MapSpawnActivation.PlayerRange) { continue; }
                            SpawnBatchStateDto state = _finiteState.Batches.First(item => item.BatchId == batch.BatchId);
                            Vector2 center = new Vector2((float)(_map.OriginX + batch.TriggerCenter.X),
                                (float)(_map.OriginY + batch.TriggerCenter.Y));
                            if (!state.IsActivated && SegmentDistanceSquared(previous, current, center)
                                <= batch.TriggerRadius * batch.TriggerRadius)
                            { state.IsActivated = true; }
                        }
                        _lastPlayerPosition = current;
                        _hasPlayerPosition = true;
                    }
                    SpawnPendingActivated(4, false);
                }
            }
            catch (System.OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
                { host.RequestCurrentSessionStop("finite-spawner-failure"); }
                throw;
            }
            finally
            {
                if (version == _spawnVersion) { _isSpawning = false; _spawnScope = null; }
            }
        }

        void SpawnPendingActivated(int budget, bool failFast, bool onlyInitial = false)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int attempted = 0;
            foreach (SpawnSlotStateDto slot in _finiteState.Slots)
            {
                // 范围批次最多4次，并在一次完整生成后检查2ms预算；入图准备仍全部完成后才放行。
                if (budget == 0 || !failFast && attempted > 0
                    && (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency >= 2) { break; }
                if (slot.Status != SpawnSlotStatus.Pending) { continue; }
                MapSpawnBatchRecord batch = _batches.First(item => item.BatchId == slot.BatchId);
                if (onlyInitial && batch.Activation != MapSpawnActivation.OnEnter) { continue; }
                if (!_finiteState.Batches.First(item => item.BatchId == slot.BatchId).IsActivated) { continue; }
                budget--;
                attempted++;
                MonsterDefinition definition = _definitions.Resolve<MonsterDefinition>(ContentId.Parse(slot.MonsterDefinitionId));
                if (!TryGetPlannedPosition(batch, slot, definition, out Vector3 position))
                {
                    RejectedSpawnAttempts++;
                    LastSpawnFailure = "批次 " + batch.BatchId + " 无合法落点";
                    if (failFast) { throw new System.InvalidOperationException(LastSpawnFailure); }
                    continue;
                }
                MonsterController monster = _architecture.GetSystem<SpawnSystem>().SpawnPlannedMonster(definition, slot.MonsterSeed, position);
                if (monster == null)
                {
                    LastSpawnFailure = "批次 " + batch.BatchId + " 生成对象失败";
                    if (failFast) { throw new System.InvalidOperationException(LastSpawnFailure); }
                    continue;
                }
                slot.InstanceId = monster.Instance.Id.Value;
                slot.Status = SpawnSlotStatus.Alive;
                _aliveMonsters.Add(monster);
            }
            LastSpawnMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        bool TryGetPlannedPosition(MapSpawnBatchRecord batch, SpawnSlotStateDto slot,
            MonsterDefinition definition, out Vector3 position)
        {
            MapGeometryRegion region = _spawnRegions.First(item => item.Id == batch.RegionId);
            GameObject prefab = _architecture.GetUtility<PrefabAssetLoader>().GetPrefab(definition.Prefab);
            if (prefab == null || !prefab.TryGetComponent(out CircleCollider2D collider))
            { throw new System.InvalidOperationException("刷怪 Prefab 缺少圆形碰撞：" + definition.Id); }
            Vector3 scale = collider.transform.lossyScale;
            double radius = collider.radius * System.Math.Max(System.Math.Abs(scale.x), System.Math.Abs(scale.y));
            double minX = region.Points.Min(point => point.X), maxX = region.Points.Max(point => point.X);
            double minY = region.Points.Min(point => point.Y), maxY = region.Points.Max(point => point.Y);
            var random = new System.Random(slot.PositionSeed);
            for (int attempt = 0; attempt < 256; attempt++)
            {
                var point = new MapGeometryPoint(minX + random.NextDouble() * (maxX - minX),
                    minY + random.NextDouble() * (maxY - minY));
                if (_mapGeometry.CheckPoint(point, new MapGroundProfile(radius), region.Id) != MapGeometryResult.Valid) { continue; }
                bool occupied = _aliveMonsters.Any(monster => monster != null && monster.Actor != null && monster.Actor.IsAlive
                    && Vector2.Distance(monster.transform.position, new Vector2((float)point.X, (float)point.Y)) < radius * 2);
                Transform target = GetTarget();
                if (occupied || target != null && Vector2.Distance(target.position,
                    new Vector2((float)point.X, (float)point.Y)) < radius + .5f) { continue; }
                position = new Vector3((float)point.X, (float)point.Y, 0f);
                return true;
            }
            position = default;
            return false;
        }

        void RefreshFiniteStatus()
        {
            if (_finiteState?.Mode != FiniteSpawnMode.Finite) { return; }
            CleanupAliveList();
            _aliveIds.Clear();
            foreach (MonsterController monster in _aliveMonsters) { _aliveIds.Add(monster.Instance.Id.Value); }
            foreach (SpawnSlotStateDto slot in _finiteState.Slots)
            {
                if (slot.Status != SpawnSlotStatus.Alive) { continue; }
                if (_aliveIds.Contains(slot.InstanceId)) { continue; }
                slot.Status = SpawnSlotStatus.Defeated;
                slot.InstanceId = string.Empty;
            }
        }

        static double SegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 point)
        {
            Vector2 delta = b - a;
            float denominator = delta.sqrMagnitude;
            float t = denominator > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / denominator) : 0f;
            return ((Vector2)(a + delta * t - point)).sqrMagnitude;
        }

        async UniTask SpawnLoopAsync(
            uint version,
            IArchitecture architecture,
            float initialDelaySeconds,
            System.Threading.CancellationToken token)
        {
            try
            {
                if (initialDelaySeconds > 0f)
                {
                    _nextSpawnAttemptTime = Time.time + initialDelaySeconds;
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(initialDelaySeconds),
                        cancellationToken: token);
                }
                else
                {
                    _nextSpawnAttemptTime = Time.time;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                while (!token.IsCancellationRequested)
                {
                    while (GameTimeService.Shared.IsPaused) { await UniTask.Yield(PlayerLoopTiming.Update, token); }
                    CleanupAliveList();

                    if (_aliveMonsters.Count < _spawnDefinition.MaxAliveCount)
                    {
                        SpawnOne(architecture);
                    }

                    _nextSpawnAttemptTime = Time.time + _spawnDefinition.SpawnInterval;
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_spawnDefinition.SpawnInterval),
                        cancellationToken: token);
                }
            }
            catch (System.OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
                {
                    host.RequestCurrentSessionStop("monster-spawner-failure");
                }

                throw;
            }
            finally
            {
                if (version == _spawnVersion)
                {
                    _isSpawning = false;
                    _spawnScope = null;
                    _nextSpawnAttemptTime = 0f;
                }
            }
        }

        void SpawnOne(IArchitecture architecture)
        {
            GameplayRandomSystem randomSystem = architecture.GetSystem<GameplayRandomSystem>();
            int positionSeed = randomSystem.NextSeed(GameplayRandomChannel.SpawnPosition);
            int monsterSeed = randomSystem.NextSeed(GameplayRandomChannel.MonsterInstance);
            Vector3 position;
            if (_mapGeometry != null)
            {
                if (!TryGetMapSpawnPosition(new System.Random(positionSeed), out position))
                { RejectedSpawnAttempts++; return; }
            }
            else { position = GetSpawnPosition(new System.Random(positionSeed)); }
            MonsterController monster = architecture.SendCommand(
                new SpawnMonsterCommand(_spawnDefinition, monsterSeed, position));

            if (monster != null)
            {
                _aliveMonsters.Add(monster);
            }
        }

        Vector3 GetSpawnPosition(System.Random random)
        {
            Transform target = GetTarget();
            Vector3 center = target != null ? target.position : transform.position;

            if (_worldBounds == null)
            {
                return center + (Vector3)(GetRandomDirection(random) * _spawnDefinition.SpawnRadius);
            }

            Bounds bounds = _worldBounds.bounds;

            for (int i = 0; i < MaxSpawnPositionAttempts; i++)
            {
                Vector2 offset = GetRandomDirection(random) * _spawnDefinition.SpawnRadius;
                Vector3 candidate = center + new Vector3(offset.x, offset.y, 0f);

                if (IsInsideBounds(candidate, bounds, _spawnBoundsPadding))
                {
                    return candidate;
                }
            }

            Vector2 fallbackOffset = GetRandomDirection(random) * _spawnDefinition.SpawnRadius;
            Vector3 fallback = center + new Vector3(fallbackOffset.x, fallbackOffset.y, 0f);
            return ClampToBounds(fallback, bounds, _spawnBoundsPadding);
        }

        bool TryGetMapSpawnPosition(System.Random random, out Vector3 position)
        {
            Vector3 center = GetTarget() != null ? GetTarget().position : transform.position;
            for (int i = 0; i < 32; i++)
            {
                Vector2 offset = GetRandomDirection(random) * _spawnDefinition.SpawnRadius;
                var candidate = new MapGeometryPoint(center.x + offset.x, center.y + offset.y);
                foreach (MapGeometryRegion region in _spawnRegions)
                {
                    if (_mapGeometry.CheckPoint(candidate, new MapGroundProfile(0.4), region.Id) == MapGeometryResult.Valid)
                    { position = new Vector3((float)candidate.X, (float)candidate.Y, 0); return true; }
                }
            }
            position = default;
            return false;
        }

        Transform GetTarget()
        {
            if (_target != null)
            {
                return _target;
            }

            PlayerController player = FindAnyObjectByType<PlayerController>();

            if (player != null)
            {
                _target = player.transform;
            }

            return _target;
        }

        void CleanupAliveList()
        {
            for (int i = _aliveMonsters.Count - 1; i >= 0; i--)
            {
                MonsterController monster = _aliveMonsters[i];

                if (monster == null || monster.Actor == null || !monster.Actor.IsAlive)
                {
                    _aliveMonsters.RemoveAt(i);
                }
            }
        }

        static Vector2 GetRandomDirection(System.Random random)
        {
            double angle = random.NextDouble() * System.Math.PI * 2d;
            return new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
        }

        static bool IsInsideBounds(Vector3 point, Bounds bounds, float padding)
        {
            return point.x >= bounds.min.x + padding
                   && point.x <= bounds.max.x - padding
                   && point.y >= bounds.min.y + padding
                   && point.y <= bounds.max.y - padding;
        }

        static Vector3 ClampToBounds(Vector3 point, Bounds bounds, float padding)
        {
            float minimumX = bounds.min.x + padding;
            float maximumX = bounds.max.x - padding;
            float minimumY = bounds.min.y + padding;
            float maximumY = bounds.max.y - padding;

            if (minimumX > maximumX)
            {
                minimumX = bounds.center.x;
                maximumX = bounds.center.x;
            }

            if (minimumY > maximumY)
            {
                minimumY = bounds.center.y;
                maximumY = bounds.center.y;
            }

            point.x = Mathf.Clamp(point.x, minimumX, maximumX);
            point.y = Mathf.Clamp(point.y, minimumY, maximumY);
            return point;
        }
    }
}
