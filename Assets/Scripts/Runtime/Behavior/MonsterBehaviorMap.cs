using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace DarkFlare
{
    // MapScope 的单一模拟驱动；候选构造不注册、不推进任何 Actor。
    public sealed class MonsterBehaviorMap
    {
        static readonly ProfilerMarker TickMarker = new("DarkFlare.Behavior.MapTick");
        readonly Guid _scope;
        readonly NavigationMap _navigation;
        readonly LifecycleScope _lifetime;
        readonly IArchitecture _architecture;
        readonly BehaviorSystem _behaviors;
        readonly CombatModel _combat;
        readonly List<MonsterController> _monsters = new();
        LifecycleScope _driver;
        CombatActor _player;
        bool _active;
        public int Count => _monsters.Count;

        public MonsterBehaviorMap(NavigationMap navigation, LifecycleScope lifetime, IArchitecture architecture)
        {
            _navigation = navigation; _lifetime = lifetime; _architecture = architecture;
            // Map generation 在 Session 内唯一；完整句柄另含架构代次和注册序号。
            _scope = new Guid(navigation.Generation, 6, 1, 0, 0, 0, 0, 0, 0, 0, 1);
            _behaviors = architecture.GetSystem<BehaviorSystem>(); _combat = architecture.GetModel<CombatModel>();
        }

        public void Activate()
        {
            if (_active) { return; }
            _active = true; _architecture.GetSystem<NavigationSystem>().ActiveBehavior = this;
            _driver = _lifetime.CreateChild("map-simulation");
            _driver.Tasks.Run("fixed-step", RunAsync, failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        async UniTask RunAsync(CancellationToken token)
        {
            try
            {
                await foreach (AsyncUnit step in UniTaskAsyncEnumerable.EveryUpdate(PlayerLoopTiming.FixedUpdate, cancelImmediately: true).WithCancellation(token))
                {
                    Tick(Time.fixedDeltaTime);
                }
            }
            finally { _navigation.ClearVelocities(); }
        }

        internal void Tick(float deltaTime)
        {
            if (!_active || GameTimeService.Shared.IsPaused || _architecture.GetSystem<StatusSystem>().IsClockHeld)
            { _navigation.ClearVelocities(); return; }
            if (!_navigation.PrepareStep()) { return; }
            using (TickMarker.Auto())
            {
                IReadOnlyList<CombatActor> players = _combat.GetActorsByTeam(ActorTeam.Player);
                _player = players.Count > 0 ? players[0] : null;
                for (int i = 0; i < _monsters.Count; i++)
                {
                    _monsters[i].BehaviorRuntime?.InvalidatePerception(_navigation.Revision);
                    _monsters[i].BossRuntime?.InvalidatePerception(_navigation.Revision);
                }
                _behaviors.Tick(_scope, deltaTime);
            }
            _navigation.Tick(deltaTime);
        }

        internal void Register(MonsterController monster)
        {
            if (!_active || monster.NavigationOverride || monster.Definition.Behavior == null || _monsters.Contains(monster) || !monster.Actor.IsAlive) { return; }
            var world = new World(this, monster);
            _monsters.Add(monster);
            if (monster.Definition.BossBehavior != null)
            {
                var boss = new BossBehaviorRuntime(monster.Definition.BossBehavior, monster.Definition.Behavior,
                    world, monster.Instance.Id.Value, monster.CaptureBoss());
                try { _behaviors.Register(_scope, boss); monster.BindBoss(boss); }
                catch { _monsters.Remove(monster); _behaviors.Unregister(boss.Handle); throw; }
                return;
            }
            var runtime = new MonsterBehaviorRuntime(monster.Definition.Behavior, world, monster.Instance.Id.Value,
                monster.CaptureBehavior(), monster.ContactDamageCooldownRemainingSeconds);
            try { _behaviors.Register(_scope, runtime); monster.BindBehavior(runtime); }
            catch { _monsters.Remove(monster); _behaviors.Unregister(runtime.Handle); throw; }
        }

        internal void Unregister(MonsterController monster)
        {
            if (!_monsters.Remove(monster)) { return; }
            MonsterBehaviorRuntime runtime = monster.BehaviorRuntime;
            BossBehaviorRuntime boss = monster.BossRuntime;
            monster.UnbindBehavior();
            if (runtime != null) { _behaviors.Unregister(runtime.Handle); }
            if (boss != null) { _behaviors.Unregister(boss.Handle); }
        }

        public void Suspend()
        {
            _active = false; _driver?.BeginStop(); _driver = null;
            for (int i = _monsters.Count - 1; i >= 0; i--) { Unregister(_monsters[i]); }
            _behaviors.StopScope(_scope);
            NavigationSystem system = _architecture.GetSystem<NavigationSystem>();
            if (system.ActiveBehavior == this) { system.ActiveBehavior = null; }
        }

        sealed class World : IMonsterBehaviorWorld, IBossBehaviorWorld
        {
            readonly MonsterBehaviorMap _map;
            readonly MonsterController _monster;
            internal World(MonsterBehaviorMap map, MonsterController monster) { _map = map; _monster = monster; }
            public Vector2 Position => _monster.Body.position;
            public Vector2 Velocity => _monster.Body.linearVelocity;
            public bool IsAlive => _monster.Actor.IsAlive && _monster.isActiveAndEnabled;
            public float HealthFraction => _monster.Actor.MaxHealth > 0 ? _monster.Actor.CurrentHealth / _monster.Actor.MaxHealth : 0;
            public StatusActionBlock BlockedActions => _monster.Actor.BlockedActions;
            public bool TargetAvailable => _map._player != null && _map._player.IsAlive && _map._player.isActiveAndEnabled;
            public Vector2 TargetPosition => TargetAvailable ? (Vector2)_map._player.transform.position : Vector2.zero;
            public string TargetId => TargetAvailable ? _map._player.CombatIdentity : string.Empty;
            public float AttackRadius => _monster.Definition.ContactDamageRadius;
            public float AttackInterval => _monster.Definition.ContactDamageInterval;
            public NavigationStatus NavigationStatus => _map._navigation.Status(_monster);
            public bool HasLineOfSight(Vector2 target) { return _map._navigation.HasLineOfSight(Position, target); }
            public bool CanStand(Vector2 target) { return _map._navigation.CanStand(_monster, target); }
            public bool CanAttack(string targetId)
            {
                return _map._active && !GameTimeService.Shared.IsPaused && !_map._architecture.GetSystem<StatusSystem>().IsClockHeld
                    && TargetAvailable && targetId == TargetId
                    && MonsterAttackRules.CanHit(_map._architecture, _monster, _map._player);
            }
            public bool CanBossAct(string targetId)
            {
                return _map._active && !GameTimeService.Shared.IsPaused && !_map._architecture.GetSystem<StatusSystem>().IsClockHeld
                    && TargetAvailable && targetId == TargetId && IsAlive
                    && (_monster.Actor.BlockedActions & StatusActionBlock.Attack) == 0
                    && _map._architecture.GetSystem<StatusSystem>().GetTarget(_monster.Actor).IsValid;
            }
            public void Move(NavigationTargetKind kind, Vector2 target)
            {
                if (!_map._active || !IsAlive) { return; }
                float stop = kind == NavigationTargetKind.Player && _monster.Definition.BossBehavior != null
                    ? _monster.Definition.BossBehavior.SwipeRadius * .75f
                    : kind == NavigationTargetKind.Player ? _monster.Definition.ContactStopDistance : .15f;
                NavigationMode? mode = kind == NavigationTargetKind.Point ? NavigationMode.AStar : null;
                NavigationIntentDto current = _monster.NavigationIntent;
                if (current.Target == kind && current.Mode == mode && current.StopDistance == stop
                    && (kind != NavigationTargetKind.Point || current.Point.X == target.x && current.Point.Y == target.y)) { return; }
                _monster.RestoreNavigation(new NavigationIntentDto { Target = kind, Mode = mode, MapId = _map._navigation.MapId,
                    Point = new Vector3Dto { X = target.x, Y = target.y }, StopDistance = stop });
            }
            public void StopMoving()
            {
                if (_monster.NavigationIntent.Target != NavigationTargetKind.None)
                { _monster.RestoreNavigation(new NavigationIntentDto { Target = NavigationTargetKind.None }); }
            }
            public void Hit(string targetId, long actionId)
            {
                if (!CanAttack(targetId) || _monster.BehaviorRuntime?.IsAuthorized != true) { return; }
                _map._architecture.SendCommand(new ResolveMonsterHitCommand(_monster, _map._player, _monster.BehaviorRuntime.Handle, actionId));
            }
            public bool TryHit(BossAttackKind kind, string targetId, long actionId, Vector2 from, Vector2 to, Vector2 facing)
            {
                if (!CanBossAct(targetId) || _monster.BossRuntime?.IsAuthorized != true) { return false; }
                BossBehaviorDefinition config = _monster.Definition.BossBehavior;
                Vector2 point = TargetPosition;
                bool contact;
                if (kind == BossAttackKind.Swipe)
                {
                    Vector2 offset = point - from;
                    contact = offset.sqrMagnitude <= config.SwipeRadius * config.SwipeRadius
                        && (offset.sqrMagnitude < .0001f || Vector2.Dot(facing, offset.normalized) >= config.SwipeCosine);
                }
                else
                {
                    Vector2 segment = to - from;
                    float ratio = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / segment.sqrMagnitude) : 0;
                    contact = Vector2.Distance(point, from + segment * ratio) <= config.ChargeHitRadius;
                }
                if (!contact || !HasLineOfSight(point)) { return false; }
                return _map._architecture.SendCommand(new ResolveBossHitCommand(_monster, _map._player,
                    _monster.BossRuntime.Handle, actionId, kind, from, to, facing));
            }
        }
    }
}
