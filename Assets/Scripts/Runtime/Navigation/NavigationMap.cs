using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace DarkFlare
{
    // One owner and one velocity writer for an active map. Searches survive their per-tick budget.
    public sealed partial class NavigationMap : IDisposable
    {
        public const int Capacity = 1024;
        static readonly ProfilerMarker TickMarker = new("DarkFlare.Navigation.Tick");
        static readonly ProfilerMarker WaitMarker = new("DarkFlare.Navigation.SteeringWait");
        static readonly double[] Radii = { .30, .36, .50, .75 };
        readonly MapGeometrySnapshot _geometry;
        readonly LifecycleScope _lifetime;
        readonly IArchitecture _architecture;
        readonly NavigationModel _model;
        readonly CombatModel _combat;
        readonly StatusSystem _statuses;
        readonly NavigationSystem _system;
        readonly List<Agent> _agents = new(Capacity);
        readonly Dictionary<MonsterController, Agent> _lookup = new(Capacity);
        readonly Field[] _fields = new Field[8];
        readonly Work[] _work = new Work[6];
        readonly NavigationGrid[] _grids = new NavigationGrid[4];
        readonly NavigationMemory _memory;
        NativeArray<NavigationSample> _samples;
        NativeArray<NavigationNeighbor> _neighbors;
        NativeArray<float2> _velocities;
        NativeArray<int> _visits;
        JobHandle _steering;
        LifecycleScope _driver;
        int _serial;
        int _cursor;
        uint _tick;
        CombatActor _player;
        float2 _playerPosition;
        bool _playerAvailable;
        readonly int[] _playerGoals = new int[4];
        bool _disposed;
        bool _active;
        public int Generation { get; }
        public string MapId { get; }
        public int Revision { get; private set; } = 1;
        public int AgentCount => _agents.Count;
        public bool IsReady
        {
            get
            {
                for (int i = 0; i < _grids.Length; i++) { if (_grids[i] == null || !_grids[i].IsReady) { return false; } }
                return true;
            }
        }
        internal NavigationGrid Grid(int profile) { return _grids[profile]; }

        sealed class Agent : IDisposable
        {
            internal MonsterController Monster;
            internal NavigationAgentHandle Handle;
            internal NavigationIntentDto Intent;
            internal int Request;
            internal int Profile;
            internal NavigationMode Mode;
            internal NavigationStatus Status;
            internal CombatActor Target;
            internal float2 GoalPosition;
            internal int Goal = -1;
            internal int PublishedGoal = -1;
            internal double ChangedAt = -1;
            internal double AcceptedAt;
            internal double LastAccepted = -100;
            internal double LastMatchedAt = -100;
            internal Field Field;
            internal Work Work;
            internal NativeArray<int> Path;
            internal NavigationMemory Memory;
            internal int PathCount;
            internal int PathIndex;
            internal bool Registered;
            internal float2 PreviousPosition;
            internal bool HasPosition;
            internal int PositionRevision;
            internal float2 CachedPosition;
            internal int CurrentNode = -1;
            internal float2 AttachOffset;
            internal int GoalRevision;
            internal float2 PreviousGoalPosition;
            public void Dispose() { if (Path.IsCreated) { Path.Dispose(); } Memory?.Dispose(); }
        }
        sealed class Field : IDisposable
        {
            internal string Key;
            internal int Profile;
            internal int Goal = -1;
            internal int PublishedGoal = -1;
            internal int Users;
            internal int Revision;
            internal double ChangedAt = -1;
            internal double AcceptedAt;
            internal double LastAccepted = -100;
            internal double LastMatchedAt = -100;
            internal double Used;
            internal NativeArray<int> Parents;
            internal NavigationMemory Memory;
            internal Work Work;
            public void Dispose() { if (Parents.IsCreated) { Parents.Dispose(); } Memory?.Dispose(); }
        }
        sealed class Work : IDisposable
        {
            internal NavigationSearch Search;
            internal Agent Agent;
            internal Field Field;
            internal int Request;
            internal int Profile;
            internal double AcceptedAt;
            internal double RequestedAt;
            public void Dispose() { Search?.Dispose(); }
        }

        public NavigationMap(MapGeometrySnapshot geometry, string mapId, int generation, LifecycleScope lifetime, IArchitecture architecture)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            MapId = mapId; Generation = generation; _lifetime = lifetime; _architecture = architecture;
            _model = architecture.GetModel<NavigationModel>(); _combat = architecture.GetModel<CombatModel>();
            _statuses = architecture.GetSystem<StatusSystem>(); _system = architecture.GetSystem<NavigationSystem>();
            // Admit the full bounded cache/search/path capacity while the source map is retained.
            // This rejects an oversized map before activation, rather than failing its first search.
            long required = (long)geometry.Width * geometry.Height * (4 * 4 + 8 * 4 + 6 * 28)
                + geometry.Rectangles.Count * 32L * 4 + Capacity * (4096 * 4L + 128) + 6 * 16;
            if (NavigationMemory.Bytes + required > NavigationMemory.Limit)
            { throw new MapGeometryCapacityException("地图及导航搜索池超出 128 MiB 容量（含源图）"); }
            _memory = new NavigationMemory(Capacity * (Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<NavigationSample>()
                + Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<NavigationNeighbor>() + 12L));
            try
            {
                _samples = new NativeArray<NavigationSample>(Capacity, Allocator.Persistent);
                _neighbors = new NativeArray<NavigationNeighbor>(Capacity, Allocator.Persistent);
                _velocities = new NativeArray<float2>(Capacity, Allocator.Persistent);
                _visits = new NativeArray<int>(Capacity, Allocator.Persistent);
                for (int i = 0; i < _grids.Length; i++) { _grids[i] = new NavigationGrid(geometry, Radii[i]); }
                for (int i = 0; i < _work.Length; i++) { _work[i] = new Work(); }
            }
            catch { Dispose(); throw; }
        }

        public async UniTask PrepareAsync(CancellationToken token)
        {
            while (!IsReady) { await UniTask.Yield(PlayerLoopTiming.Update, token); }
            foreach (NavigationGrid grid in _grids) { grid.Complete(); }
        }
        public void Activate()
        {
            if (_disposed || !IsReady) { throw new InvalidOperationException("导航地图尚未就绪"); }
            if (_active) { return; }
            _active = true; _system.Active = this;
            _driver = _lifetime.CreateChild("navigation");
            _driver.Tasks.Run("fixed-step", RunAsync, failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }
        async UniTask RunAsync(CancellationToken token)
        {
            try
            {
                await foreach (AsyncUnit step in UniTaskAsyncEnumerable.EveryUpdate(PlayerLoopTiming.FixedUpdate, cancelImmediately: true).WithCancellation(token))
                {
                    if (_active) { Tick(Time.fixedDeltaTime); }
                }
            }
            finally { StopVelocities(); }
        }
        public void Suspend()
        {
            _active = false; _driver?.BeginStop(); _driver = null;
            _steering.Complete();
            foreach (Work work in _work) { work?.Search?.Complete(); }
            StopVelocities();
            if (_system?.Active == this) { _system.Active = null; }
        }
        void StopVelocities()
        {
            foreach (Agent agent in _agents) { if (agent.Monster != null) { agent.Monster.Body.linearVelocity = Vector2.zero; } }
        }
        internal void Register(MonsterController monster)
        {
            if (monster == null || monster.Instance == null || monster.Definition == null || _lookup.ContainsKey(monster)) { return; }
            if (_agents.Count >= Capacity) { monster.Body.linearVelocity = Vector2.zero; return; }
            var agent = new Agent { Monster = monster, Registered = true,
                Handle = new NavigationAgentHandle(GameArchitectureProvider.Generation, Generation, ++_serial) };
            _lookup.Add(monster, agent); _agents.Add(agent); RefreshIntent(agent);
        }
        internal void Unregister(MonsterController monster)
        {
            if (!_lookup.Remove(monster, out Agent agent)) { return; }
            agent.Registered = false; ReleaseRoute(agent); agent.Dispose(); _agents.Remove(agent);
            if (monster != null) { monster.Body.linearVelocity = Vector2.zero; }
        }
        public NavigationAgentHandle Handle(MonsterController monster)
        { return monster != null && _lookup.TryGetValue(monster, out Agent agent) ? agent.Handle : default; }
        public NavigationStatus Status(MonsterController monster)
        { return monster != null && _lookup.TryGetValue(monster, out Agent agent) ? agent.Status : _agents.Count >= Capacity ? NavigationStatus.BudgetExceeded : NavigationStatus.Stale; }
        internal bool Matches(MonsterController monster, NavigationAgentHandle handle)
        {
            return _active && monster != null && _lookup.TryGetValue(monster, out Agent agent) && agent.Handle.Session == handle.Session
                && agent.Handle.Map == handle.Map && agent.Handle.Serial == handle.Serial;
        }
        internal bool ValidateIntent(MonsterController monster, NavigationIntentDto intent)
        {
            if (intent == null || !intent.IsValid) { return false; }
            if (intent.Target == NavigationTargetKind.Player || intent.Target == NavigationTargetKind.None) { return true; }
            if (intent.MapId != MapId) { return false; }
            if (intent.Target == NavigationTargetKind.Point)
            { return intent.Point.X >= _geometry.Bounds.MinX && intent.Point.X < _geometry.Bounds.MaxX
                && intent.Point.Y >= _geometry.Bounds.MinY && intent.Point.Y < _geometry.Bounds.MaxY; }
            IReadOnlyList<CombatActor> players = _combat.GetActorsByTeam(ActorTeam.Player);
            for (int i = 0; i < players.Count; i++) { if (players[i].CombatIdentity == intent.ActorId) { return true; } }
            IReadOnlyList<CombatActor> monsters = _combat.GetActorsByTeam(ActorTeam.Monster);
            for (int i = 0; i < monsters.Count; i++)
            { if (monsters[i].isActiveAndEnabled && monsters[i].IsAlive && monsters[i].CombatIdentity == intent.ActorId) { return true; } }
            return false;
        }
        void ReleaseRoute(Agent agent)
        {
            if (agent.Field != null) { agent.Field.Users--; agent.Field = null; }
            if (agent.Work != null) { agent.Work.Agent = null; agent.Work = null; }
            agent.PathCount = 0; agent.PublishedGoal = -1; agent.LastMatchedAt = -100;
            agent.PositionRevision = 0; agent.GoalRevision = 0;
        }
        void RefreshIntent(Agent agent)
        {
            ReleaseRoute(agent); agent.Intent = agent.Monster.NavigationIntent; agent.Request = agent.Monster.NavigationRevision;
            agent.Profile = (int)agent.Monster.Definition.NavigationProfile;
            agent.Mode = agent.Intent.Mode ?? agent.Monster.Definition.NavigationMode;
            agent.Target = null; agent.Goal = -1; agent.Status = NavigationStatus.Pending; agent.LastAccepted = -100; agent.ChangedAt = -1;
            if (agent.Mode == NavigationMode.FlowField && agent.Path.IsCreated)
            { agent.Path.Dispose(); agent.Path = default; agent.Memory.Dispose(); agent.Memory = null; }
            if (agent.Mode == NavigationMode.AStar && !agent.Path.IsCreated)
            {
                agent.Memory = new NavigationMemory(4096 * 4);
                try { agent.Path = new NativeArray<int>(4096, Allocator.Persistent); }
                catch { agent.Memory.Dispose(); throw; }
            }
        }
        bool CanMove(Agent agent)
        {
            CombatActor actor = agent.Monster.Actor;
            return actor != null && actor.IsConfigured && actor.IsAlive && actor.isActiveAndEnabled
                && (actor.BlockedActions & StatusActionBlock.Move) == 0 && _statuses.GetTarget(actor).IsValid;
        }
        bool ResolveGoal(Agent agent)
        {
            NavigationIntentDto intent = agent.Intent;
            if (intent.Target == NavigationTargetKind.None) { return false; }
            if (intent.Target == NavigationTargetKind.Point)
            {
                if (intent.MapId != MapId) { return false; }
                agent.GoalPosition = new float2(intent.Point.X, intent.Point.Y); return true;
            }
            if (intent.Target == NavigationTargetKind.Actor && intent.MapId != MapId) { return false; }
            if (intent.Target == NavigationTargetKind.Player)
            {
                agent.Target = _player; agent.GoalPosition = _playerPosition; return _playerAvailable;
            }
            if (agent.Target == null)
            {
                IReadOnlyList<CombatActor> monsters = _combat.GetActorsByTeam(ActorTeam.Monster);
                for (int i = 0; i < monsters.Count; i++)
                { if (monsters[i].CombatIdentity == intent.ActorId) { agent.Target = monsters[i]; break; } }
                if (agent.Target == null && _player != null && _player.CombatIdentity == intent.ActorId) { agent.Target = _player; }
            }
            if (agent.Target == null || !agent.Target.IsAlive || !agent.Target.isActiveAndEnabled) { return false; }
            Vector3 position = agent.Target.transform.position;
            agent.GoalPosition = new float2(position.x, position.y); return true;
        }
        Field AcquireField(Agent agent, double now)
        {
            // Stable semantic identity, not a moving cell, owns the in-flight frontier.
            string key = agent.Intent.Target == NavigationTargetKind.Player ? "player" : agent.Intent.Target == NavigationTargetKind.Actor
                ? agent.Intent.ActorId : agent.Monster.Instance.Id.Value;
            int available = -1; double oldest = double.MaxValue;
            for (int i = 0; i < _fields.Length; i++)
            {
                Field field = _fields[i];
                if (field != null && field.Profile == agent.Profile && field.Key == key)
                { field.Users++; field.Used = now; return field; }
                if (field == null) { available = i; oldest = double.MinValue; }
                else if (field.Users == 0 && field.Work == null && field.Used < oldest) { available = i; oldest = field.Used; }
            }
            if (available < 0) { return null; }
            _fields[available]?.Dispose();
            var next = new Field { Key = key, Profile = agent.Profile, Users = 1, Used = now, Revision = Revision };
            next.Memory = new NavigationMemory(_geometry.Width * (long)_geometry.Height * 4);
            try { next.Parents = new NativeArray<int>(_geometry.Width * _geometry.Height, Allocator.Persistent); }
            catch { next.Dispose(); _fields[available] = null; throw; }
            _fields[available] = next; return next;
        }
        internal void Tick(float deltaTime)
        {
            BeginAllocationMeasurement();
            long started = Stopwatch.GetTimestamp();
            using (TickMarker.Auto())
            {
                ApplyStamps();
                double now = Time.realtimeSinceStartupAsDouble;
                _model.LastPathLatencyMilliseconds = 0; _model.FlowCacheHits = 0; _model.FlowCacheMisses = 0;
                _model.CandidateVisits = 0; _model.SaturatedAgents = 0; _model.PendingSearches = 0;
                for (int i = 0; i < _grids.Length; i++) { if (!_grids[i].IsReady) { StopVelocities(); EndAllocationMeasurement(); return; } _grids[i].Complete(); }
                IReadOnlyList<CombatActor> players = _combat.GetActorsByTeam(ActorTeam.Player);
                _player = players.Count > 0 ? players[0] : null;
                _playerAvailable = _player != null && _player.IsAlive && _player.isActiveAndEnabled;
                if (_playerAvailable)
                {
                    Vector3 p = _player.transform.position; _playerPosition = new float2(p.x, p.y);
                    for (int i = 0; i < _playerGoals.Length; i++) { _playerGoals[i] = _grids[i].View.LegalNode(_playerPosition); }
                }
                long phase = Stopwatch.GetTimestamp();
                CollectSearches(now);
                _model.SearchCollectMilliseconds = Elapsed(phase);
                phase = Stopwatch.GetTimestamp();
                for (int i = 0; i < _agents.Count; i++)
                {
                    Agent agent = _agents[i];
                    if (agent.Request != agent.Monster.NavigationRevision) { RefreshIntent(agent); }
                    float speed = agent.Monster.Actor.Stats.GetValue(StatIds.MoveSpeed);
                    Vector2 position = agent.Monster.Body.position;
                    var currentPosition = new float2(position.x, position.y);
                    float expectedStep = speed * deltaTime * 3;
                    if (agent.HasPosition && math.distancesq(currentPosition, agent.PreviousPosition) > math.max(1f,
                        expectedStep * expectedStep))
                    { ReleaseRoute(agent); agent.Goal = -1; agent.LastAccepted = -100; agent.Status = NavigationStatus.Pending; }
                    agent.PreviousPosition = currentPosition; agent.HasPosition = true;
                    var sample = new NavigationSample { Position = new float2(position.x, position.y), Radius = agent.Monster.NavigationRadius,
                        Identity = (ulong)agent.Monster.Instance.Id.Sequence, Speed = speed,
                        SeparationRadius = math.clamp(agent.Monster.Definition.SeparationRadius, .01f, 4f),
                        SeparationWeight = agent.Monster.Definition.SeparationWeight };
                    if (agent.Profile < 0 || agent.Profile >= Radii.Length || sample.Radius > Radii[agent.Profile] + .0001)
                    { agent.Status = NavigationStatus.BudgetExceeded; }
                    else if (!ResolveGoal(agent)) { ReleaseRoute(agent); agent.Status = NavigationStatus.TargetUnavailable; }
                    else
                    {
                        NavigationGridView grid = _grids[agent.Profile].View;
                        if (agent.PositionRevision != Revision || math.any(currentPosition != agent.CachedPosition))
                        {
                            agent.CurrentNode = grid.LegalNode(sample.Position);
                            agent.AttachOffset = agent.CurrentNode >= 0 ? grid.Position(agent.CurrentNode) - sample.Position : float2.zero;
                            agent.CachedPosition = currentPosition;
                            agent.PositionRevision = Revision;
                        }
                        int attach = agent.CurrentNode;
                        sample.AttachOffset = agent.AttachOffset;
                        int goal = agent.Goal;
                        if (agent.Intent.Target == NavigationTargetKind.Player) { goal = _playerGoals[agent.Profile]; }
                        else if (agent.GoalRevision != Revision || math.any(agent.PreviousGoalPosition != agent.GoalPosition))
                        {
                            goal = grid.LegalNode(agent.GoalPosition);
                            agent.PreviousGoalPosition = agent.GoalPosition; agent.GoalRevision = Revision;
                        }
                        if (goal != agent.Goal) { if (agent.ChangedAt < 0) { agent.ChangedAt = now; } agent.Goal = goal; }
                        if (goal == agent.PublishedGoal) { agent.LastMatchedAt = now; }
                        if (goal < 0) { agent.Status = NavigationStatus.TargetUnavailable; }
                        else
                        {
                            if (agent.Mode == NavigationMode.FlowField)
                            {
                                agent.Field ??= AcquireField(agent, now);
                                if (agent.Field != null)
                                {
                                    Field field = agent.Field;
                                    if (field.Goal != goal) { if (field.ChangedAt < 0) { field.ChangedAt = now; } field.Goal = goal; }
                                    if (goal == field.PublishedGoal) { field.LastMatchedAt = now; }
                                    field.Used = now;
                                }
                            }
                            sample.CanMove = CanMove(agent);
                            sample.Desired = Direction(agent, sample.Position, attach, now);
                            sample.CanMove &= agent.Status == NavigationStatus.Moving || agent.Status == NavigationStatus.Arrived;
                        }
                    }
                    _samples[i] = sample;
                }
                _model.AgentSampleMilliseconds = Elapsed(phase);
                phase = Stopwatch.GetTimestamp();
                ScheduleSearches(now);
                _model.SearchScheduleMilliseconds = Elapsed(phase);
                int count = _agents.Count;
                if (count > 0)
                {
                    var samples = _samples.GetSubArray(0, count); var neighbors = _neighbors.GetSubArray(0, count);
                    JobHandle hash = new NavigationHashJob { Samples = samples, Sorted = neighbors }.Schedule();
                    _steering = new NavigationSteeringJob { Geometry = _grids[0].View.Geometry, Samples = samples, Sorted = neighbors,
                        Velocities = _velocities.GetSubArray(0, count), Visits = _visits.GetSubArray(0, count), DeltaTime = deltaTime,
                        ContactTolerance = Physics2D.defaultContactOffset, Tick = ++_tick }.Schedule(count, 32, hash);
                    long wait = Stopwatch.GetTimestamp();
                    using (WaitMarker.Auto()) { _steering.Complete(); }
                    _model.SteeringWaitMilliseconds = Elapsed(wait);
                    for (int i = 0; i < count; i++)
                    {
                        Agent agent = _agents[i]; float2 velocity = _velocities[i];
                        agent.Monster.Body.linearVelocity = new Vector2(velocity.x, velocity.y);
                        _model.CandidateVisits += _visits[i]; if (_visits[i] >= 96) { _model.SaturatedAgents++; }
                    }
                }
                _model.AgentCount = count; _model.GridRevision = Revision; _model.TickSequence++;
            }
            _model.MainThreadMilliseconds = Elapsed(started);
            EndAllocationMeasurement();
            // Combat retains its own Commands and accounting, outside the navigation allocation marker.
            for (int i = 0; i < _agents.Count; i++)
            {
                Agent agent = _agents[i]; MonsterController monster = agent.Monster;
                if (agent.Target != null && (monster.Actor.BlockedActions & StatusActionBlock.Attack) == 0
                    && monster.Definition.ContactDamages.Count > 0 && monster.ContactDamageCooldownRemainingSeconds <= 0
                    && ((Vector2)agent.Target.transform.position - monster.Body.position).sqrMagnitude <= monster.Definition.ContactDamageRadius * monster.Definition.ContactDamageRadius)
                { _architecture.SendCommand(new ContactAttackCommand(monster, agent.Target)); }
            }
        }
        float2 Direction(Agent agent, float2 position, int current, double now)
        {
            NavigationGridView grid = _grids[agent.Profile].View;
            float2 offset = agent.GoalPosition - position;
            float stop = agent.Intent.StopDistance ?? agent.Monster.Definition.ContactStopDistance;
            if (math.lengthsq(offset) <= math.max(stop * stop, .5625f) && grid.Segment(position, agent.GoalPosition, grid.Radius))
            {
                if (math.lengthsq(offset) <= stop * stop) { agent.Status = NavigationStatus.Arrived; return float2.zero; }
                agent.Status = NavigationStatus.Moving; return offset;
            }
            double maximumAge = _agents.Count > 500 ? .4 : .2;
            int next = -1;
            if (agent.Mode == NavigationMode.FlowField)
            {
                Field field = agent.Field;
                if (field == null) { _model.FlowCacheMisses++; agent.Status = NavigationStatus.Busy; return float2.zero; }
                if (field.PublishedGoal < 0 || field.Revision != Revision || field.PublishedGoal != field.Goal && now - Math.Max(field.AcceptedAt, field.LastMatchedAt) > maximumAge)
                { _model.FlowCacheMisses++; agent.Status = NavigationStatus.Pending; return float2.zero; }
                _model.FlowCacheHits++;
                if (current >= 0) { next = field.Parents[current]; if (next < 0 && current == field.PublishedGoal) { next = current; } }
                if (next < 0) { agent.Status = NavigationStatus.Unreachable; return float2.zero; }
            }
            else
            {
                if (agent.PathCount == 0 || agent.PublishedGoal != agent.Goal && now - Math.Max(agent.AcceptedAt, agent.LastMatchedAt) > maximumAge)
                { if (agent.Status != NavigationStatus.Unreachable && agent.Status != NavigationStatus.BudgetExceeded) { agent.Status = NavigationStatus.Pending; } return float2.zero; }
                while (agent.PathIndex + 1 < agent.PathCount && math.distancesq(position, grid.Position(agent.Path[agent.PathIndex])) < .04f) { agent.PathIndex++; }
                next = agent.Path[agent.PathIndex];
            }
            agent.Status = NavigationStatus.Moving;
            return grid.Position(next) - position;
        }
        void CollectSearches(double now)
        {
            for (int i = 0; i < _work.Length; i++)
            {
                Work work = _work[i];
                if (work.Search == null || !work.Search.Poll()) { continue; }
                if (work.Agent == null && work.Field == null) { continue; }
                if (work.Search.Status == 0) { continue; }
                if (work.Search.Revision == Revision)
                {
                    if (work.Field != null)
                    {
                        Field field = work.Field;
                        NativeArray<int>.Copy(work.Search.Parent, field.Parents);
                        field.PublishedGoal = work.Search.Goal; field.AcceptedAt = work.AcceptedAt; field.Revision = Revision;
                        _model.LastPathLatencyMilliseconds = Math.Max(_model.LastPathLatencyMilliseconds, (now - work.RequestedAt) * 1000); _model.ResultSequence++;
                    }
                    else if (work.Agent.Registered && work.Agent.Request == work.Request && work.Agent.Monster.NavigationRevision == work.Request)
                    {
                        Agent agent = work.Agent;
                        agent.PathCount = 0; agent.PathIndex = 0;
                        if (work.Search.Status == 1)
                        {
                            int node = work.Search.Goal, count = 0;
                            while (node >= 0 && count < agent.Path.Length) { agent.Path[count++] = node; node = work.Search.Parent[node]; }
                            if (node >= 0) { agent.Status = NavigationStatus.BudgetExceeded; }
                            else
                            {
                                for (int a = 0, b = count - 1; a < b; a++, b--) { int value = agent.Path[a]; agent.Path[a] = agent.Path[b]; agent.Path[b] = value; }
                                agent.PathCount = count; agent.Status = NavigationStatus.Moving;
                            }
                        }
                        else { agent.Status = NavigationStatus.Unreachable; }
                        agent.PublishedGoal = work.Search.Goal; agent.AcceptedAt = work.AcceptedAt;
                        _model.LastPathLatencyMilliseconds = Math.Max(_model.LastPathLatencyMilliseconds, (now - work.RequestedAt) * 1000); _model.ResultSequence++;
                    }
                }
                if (work.Field != null) { work.Field.Work = null; }
                if (work.Agent != null) { work.Agent.Work = null; }
                work.Field = null; work.Agent = null;
            }
        }
        void BeginWork(Work work, int profile, int start, int goal, bool reverse, double now)
        {
            work.Search ??= new NavigationSearch(_geometry.Width * _geometry.Height);
            work.Profile = profile; work.AcceptedAt = now; work.Search.Begin(start, goal, reverse, Revision);
        }
        void ScheduleSearches(double now)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                Work work = _work[slot];
                if (work.Field == null)
                {
                    Field oldest = null;
                    foreach (Field field in _fields)
                    {
                        if (field == null || field.Users == 0 || field.Work != null || field.Goal < 0
                            || field.PublishedGoal == field.Goal || now - field.LastAccepted < .1) { continue; }
                        if (oldest == null || field.LastAccepted < oldest.LastAccepted) { oldest = field; }
                    }
                    if (oldest != null)
                    {
                        work.Field = oldest; oldest.Work = work; oldest.LastAccepted = now;
                        work.RequestedAt = oldest.ChangedAt < 0 ? now : oldest.ChangedAt; oldest.ChangedAt = -1;
                        BeginWork(work, oldest.Profile, -1, oldest.Goal, true, now);
                    }
                }
            }
            for (int slot = 2; slot < _work.Length; slot++)
            {
                Work work = _work[slot]; if (work.Agent != null || work.Search?.Running == true) { continue; }
                for (int n = 0; n < _agents.Count; n++)
                {
                    _cursor %= _agents.Count; Agent agent = _agents[_cursor++];
                    if (agent.Mode != NavigationMode.AStar || agent.Goal < 0 || agent.Work != null
                        || agent.PublishedGoal == agent.Goal || now - agent.LastAccepted < .1 || agent.Status == NavigationStatus.TargetUnavailable
                        || agent.Profile < 0 || agent.Profile >= _grids.Length) { continue; }
                    Vector2 p = agent.Monster.Body.position;
                    int start = _grids[agent.Profile].View.LegalNode(new float2(p.x, p.y));
                    work.Agent = agent; work.Request = agent.Request; agent.Work = work; agent.LastAccepted = now;
                    work.RequestedAt = agent.ChangedAt < 0 ? now : agent.ChangedAt; agent.ChangedAt = -1;
                    BeginWork(work, agent.Profile, start, agent.Goal, false, now); break;
                }
            }
            foreach (Work work in _work)
            {
                if (work.Agent == null && work.Field == null) { continue; }
                _model.PendingSearches++;
                if (work.Search.Poll() && work.Search.Status == 0) { work.Search.Schedule(_grids[work.Profile].View, work.Field != null ? 65536 : 8192); }
            }
        }
        static double Elapsed(long started) { return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency; }
        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            return !_disposed && _grids[0].View.Geometry.Segment(new float2(from.x, from.y), new float2(to.x, to.y), .001);
        }
        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true; Suspend(); DisposeStamps(); DisableAllocationMeasurement();
            foreach (Agent agent in _agents) { agent.Dispose(); } _agents.Clear(); _lookup.Clear();
            foreach (Work work in _work) { work?.Dispose(); }
            foreach (Field field in _fields) { field?.Dispose(); }
            foreach (NavigationGrid grid in _grids) { grid?.Dispose(); }
            if (_samples.IsCreated) { _samples.Dispose(); } if (_neighbors.IsCreated) { _neighbors.Dispose(); }
            if (_velocities.IsCreated) { _velocities.Dispose(); } if (_visits.IsCreated) { _visits.Dispose(); }
            _memory?.Dispose();
        }
    }
}
