using System;
using System.Collections.Generic;
using DarkFlare.BehaviorTree;
using DarkFlare.HFSM;
using Unity.Profiling;
using UnityEngine;

namespace DarkFlare
{
    public interface IMonsterBehaviorWorld
    {
        Vector2 Position { get; }
        Vector2 Velocity { get; }
        bool IsAlive { get; }
        StatusActionBlock BlockedActions { get; }
        bool TargetAvailable { get; }
        Vector2 TargetPosition { get; }
        string TargetId { get; }
        float AttackRadius { get; }
        float AttackInterval { get; }
        NavigationStatus NavigationStatus { get; }
        bool HasLineOfSight(Vector2 target);
        bool CanStand(Vector2 target);
        bool CanAttack(string targetId);
        void Move(NavigationTargetKind kind, Vector2 target);
        void StopMoving();
        void Hit(string targetId, long actionId);
    }

    public sealed class MonsterBehaviorRuntime : IBehaviorRuntime
    {
        static readonly ProfilerMarker PerceptionMarker = new("DarkFlare.Behavior.Perception");
        static readonly ProfilerMarker DecisionMarker = new("DarkFlare.Behavior.Decision");
        sealed class State : IHfsmState<MonsterBehaviorRuntime>
        {
            readonly bool _runTree;
            internal State(bool runTree = false) { _runTree = runTree; }
            public void Enter(MonsterBehaviorRuntime context) { }
            public void Tick(MonsterBehaviorRuntime context, float deltaTime)
            {
                if (!_runTree) { return; }
                if (context._tree.Status != TreeStatus.Running) { context.Check(context._tree.Start()); }
                context.Check(context._tree.Tick(deltaTime));
            }
            public void Exit(MonsterBehaviorRuntime context, HfsmExitReason reason)
            {
                if (_runTree) { context.Check(context._tree.Abort()); }
            }
        }

        sealed class ActionNode : IBehaviorAction<MonsterBehaviorRuntime>
        {
            readonly int _kind;
            internal ActionNode(int kind) { _kind = kind; }
            public void Enter(MonsterBehaviorRuntime context) { }
            public NodeStatus Tick(MonsterBehaviorRuntime context, float deltaTime)
            {
                if (!context._authorized) { return NodeStatus.Failure; }
                return _kind switch
                {
                    0 => context.Windup(deltaTime),
                    1 => context.Strike(),
                    2 => context.Recover(deltaTime),
                    3 => context.Chase(),
                    _ => context.Wander(deltaTime)
                };
            }
            public void Exit(MonsterBehaviorRuntime context, BehaviorExitReason reason) { }
        }

        static readonly BehaviorTreeDefinition<MonsterBehaviorRuntime> TreeDefinition = new BehaviorTreeBuilder<MonsterBehaviorRuntime>()
            .AddAction("attack.windup", () => new ActionNode(0))
            .AddAction("attack.hit", () => new ActionNode(1))
            .AddAction("attack.recovery", () => new ActionNode(2))
            .AddSequence("attack", "attack.windup", "attack.hit", "attack.recovery")
            .AddAction("chase", () => new ActionNode(3))
            .AddAction("wander", () => new ActionNode(4))
            .AddPrioritySelector("decision", new BehaviorBranch<MonsterBehaviorRuntime>("attack", c => c._state.Mode == MonsterBehaviorMode.Attacking),
                new BehaviorBranch<MonsterBehaviorRuntime>("chase", c => c._state.Mode == MonsterBehaviorMode.Chasing),
                new BehaviorBranch<MonsterBehaviorRuntime>("wander", c => c._state.Mode == MonsterBehaviorMode.Wandering)).Build("decision");
        static readonly HfsmDefinition<MonsterBehaviorRuntime> MachineDefinition = BuildMachine();
        readonly MonsterBehaviorDefinition _config;
        readonly IMonsterBehaviorWorld _world;
        readonly string _name;
        readonly HfsmInstance<MonsterBehaviorRuntime> _machine;
        readonly BehaviorTreeInstance<MonsterBehaviorRuntime> _tree;
        MonsterBehaviorStateDto _state;
        Action<BehaviorPresentationEvent> _publish;
        BehaviorSemantic _semantic;
        bool _authorized;
        bool _paused;
        bool _activeRange;
        bool _visible;
        float _perceptionRemaining;
        bool _staggerPerception;
        int _geometryRevision;
        long _phaseTick;
        long _observedAction;
        long _hitPermit;
        public BehaviorHandle Handle { get; private set; }
        public float CooldownRemaining { get; private set; }
        public MonsterBehaviorMode Mode => _state.Mode;
        public MonsterAttackPhase AttackPhase => _state.AttackPhase;
        public bool IsAuthorized => _authorized && !_paused;
        public bool IsVisible => _visible;
        public bool IsInRange => _activeRange;
        public Vector2 Facing => Point(_state.Facing);
        public Vector2 Anchor => Point(_state.Anchor);
        public long TickSequence => _machine.TickSequence;
        public BehaviorPresentationEvent Presentation => new(Handle, _state.ActionId, _semantic, _world.Position, _paused,
            _state.AttackPhase == MonsterAttackPhase.Windup ? _config.WindupSeconds - _state.PhaseRemaining :
            _state.AttackPhase == MonsterAttackPhase.Recovery ? _config.RecoverySeconds - _state.PhaseRemaining : 0,
            _state.AttackPhase == MonsterAttackPhase.Windup ? _config.WindupSeconds : _config.RecoverySeconds);

        public MonsterBehaviorRuntime(MonsterBehaviorDefinition config, IMonsterBehaviorWorld world, string name, MonsterBehaviorStateDto state, float cooldown = 0)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            config.Validate();
            _world = world ?? throw new ArgumentNullException(nameof(world)); _name = name;
            _tree = TreeDefinition.CreateInstance(this); _machine = MachineDefinition.CreateInstance(this);
            Restore(state, cooldown);
        }

        static HfsmDefinition<MonsterBehaviorRuntime> BuildMachine()
        {
            var builder = new HfsmBuilder<MonsterBehaviorRuntime>()
                .AddState("root", () => new State()).AddState("alive", () => new State(), "root")
                .AddState("sleep", () => new State(), "alive").AddState("active", () => new State(), "alive")
                .AddState("controlled", () => new State(), "alive").AddState("dead", () => new State(), "root")
                .AddState("wander", () => new State(true), "active").AddState("combat", () => new State(), "active")
                .AddState("chase", () => new State(true), "combat").AddState("attack", () => new State(true), "combat")
                .SetInitialChild("root", "alive").SetInitialChild("alive", "sleep")
                .SetInitialChild("active", "wander").SetInitialChild("combat", "chase")
                .AddTransition("alive", "dead", c => c.Mode == MonsterBehaviorMode.Dead, 1000, reason: "actor dead")
                .AddTransition("alive", "sleep", c => c.Mode == MonsterBehaviorMode.Sleeping, 900, reason: "outside activation / no player")
                .AddTransition("alive", "controlled", c => c.Mode == MonsterBehaviorMode.Controlled, 800, reason: "movement and attack blocked")
                .AddTransition("alive", "attack", c => c.Mode == MonsterBehaviorMode.Attacking, 700, reason: "accepted attack")
                .AddTransition("alive", "chase", c => c.Mode == MonsterBehaviorMode.Chasing, 600, reason: "visible player / last seen memory")
                .AddTransition("alive", "wander", c => c.Mode == MonsterBehaviorMode.Wandering, 500, reason: "active without target");
            return builder.Build();
        }

        public void Start(BehaviorHandle handle, Action<BehaviorPresentationEvent> publish)
        {
            Handle = handle; _publish = publish; _authorized = true; Check(_machine.Start());
            SetSemantic(_state.AttackPhase == MonsterAttackPhase.Windup ? BehaviorSemantic.AttackWindup :
                _state.AttackPhase == MonsterAttackPhase.Recovery ? BehaviorSemantic.Recovery : BehaviorSemantic.Idle, true);
        }

        public void Restore(MonsterBehaviorStateDto state, float cooldown)
        {
            if (state == null || !state.IsValid || !float.IsFinite(cooldown) || cooldown < 0) { throw new ArgumentException("怪物行为恢复数据非法"); }
            _state = state.Copy(); CooldownRemaining = cooldown; _perceptionRemaining = 0;
            _staggerPerception = true;
            _activeRange = state.Mode != MonsterBehaviorMode.Sleeping; _visible = false;
            _phaseTick = -1;
            if (_authorized)
            {
                Check(_tree.Abort()); _world.StopMoving();
                SetSemantic(_state.AttackPhase == MonsterAttackPhase.Windup ? BehaviorSemantic.AttackWindup :
                    _state.AttackPhase == MonsterAttackPhase.Recovery ? BehaviorSemantic.Recovery : BehaviorSemantic.Idle, true);
            }
        }

        public MonsterBehaviorStateDto CaptureState() { return _state.Copy(); }
        internal void RestoreCooldown(float remaining) { CooldownRemaining = remaining; }
        public void InvalidatePerception(int revision)
        {
            if (_geometryRevision != revision) { _geometryRevision = revision; _perceptionRemaining = 0; _staggerPerception = true; }
        }

        public void Tick(float deltaTime)
        {
            if (!_authorized || _paused || deltaTime == 0) { return; }
            if (!float.IsFinite(deltaTime) || deltaTime < 0) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            CooldownRemaining = Mathf.Max(0, CooldownRemaining - deltaTime);
            _state.MemoryRemaining = Mathf.Max(0, _state.MemoryRemaining - deltaTime);
            _perceptionRemaining -= deltaTime;
            if (_perceptionRemaining <= 0 || !_world.TargetAvailable)
            {
                using (PerceptionMarker.Auto()) { Sense(); }
                // 首次 / 几何改变立即感知，下一轮按稳定种子错峰，不消费行为随机序列。
                _perceptionRemaining = _config.PerceptionInterval * (_staggerPerception
                    ? .1f + ((uint)_state.RandomSeed * 2654435761u % 997) / 997f * .9f : 1);
                _staggerPerception = false;
            }
            bool moveBlocked = (_world.BlockedActions & StatusActionBlock.Move) != 0;
            bool attackBlocked = (_world.BlockedActions & StatusActionBlock.Attack) != 0;
            if (!_world.IsAlive || !_activeRange || attackBlocked || !_world.TargetAvailable
                || _state.AttackPhase != MonsterAttackPhase.None && _state.TargetId != _world.TargetId)
            {
                CancelAttack();
            }
            if (!_world.IsAlive) { _state.Mode = MonsterBehaviorMode.Dead; }
            else if (!_activeRange) { _state.Mode = MonsterBehaviorMode.Sleeping; }
            else if (moveBlocked && attackBlocked) { _state.Mode = MonsterBehaviorMode.Controlled; }
            else if (_state.AttackPhase != MonsterAttackPhase.None || TryAcceptAttack()) { _state.Mode = MonsterBehaviorMode.Attacking; }
            else if (_visible || _state.MemoryRemaining > 0) { _state.Mode = MonsterBehaviorMode.Chasing; }
            else { _state.Mode = MonsterBehaviorMode.Wandering; }
            if (moveBlocked || Mode == MonsterBehaviorMode.Sleeping || Mode == MonsterBehaviorMode.Controlled || Mode == MonsterBehaviorMode.Dead)
            {
                _world.StopMoving();
                if (Mode != MonsterBehaviorMode.Attacking) { SetSemantic(Mode == MonsterBehaviorMode.Dead ? BehaviorSemantic.Stopped : BehaviorSemantic.Idle); }
            }
            using (DecisionMarker.Auto()) { Check(_machine.Tick(deltaTime)); }
            Vector2 velocity = _world.Velocity;
            if (velocity.sqrMagnitude > .0025f) { Set(_state.Facing, velocity.normalized); }
        }

        void Sense()
        {
            _visible = false;
            if (!_world.TargetAvailable) { _activeRange = false; _state.MemoryRemaining = 0; return; }
            Vector2 offset = _world.TargetPosition - _world.Position;
            float distance = offset.sqrMagnitude;
            float limit = _activeRange ? _config.DeactivationDistance : _config.ActivationDistance;
            _activeRange = distance <= limit * limit;
            if (!_activeRange) { _state.MemoryRemaining = 0; return; }
            _visible = distance <= _config.SightDistance * _config.SightDistance
                && (distance < .0001f || Vector2.Dot(Facing.normalized, offset.normalized) >= _config.SightCosine)
                && _world.HasLineOfSight(_world.TargetPosition);
            if (_visible)
            {
                Set(_state.LastSeen, _world.TargetPosition); _state.MemoryRemaining = _config.MemorySeconds;
            }
        }

        public bool TryAcceptAttack()
        {
            if (!IsAuthorized || !_activeRange || !_visible || _state.AttackPhase != MonsterAttackPhase.None
                || CooldownRemaining > 0 || !_world.CanAttack(_world.TargetId)) { return false; }
            _state.ActionId = checked(_state.ActionId + 1); _state.TargetId = _world.TargetId;
            _state.Mode = MonsterBehaviorMode.Attacking; _state.AttackPhase = MonsterAttackPhase.Windup;
            _state.PhaseRemaining = _config.WindupSeconds; _state.HitResolved = false;
            _phaseTick = _machine.TickSequence + 1; CooldownRemaining = _world.AttackInterval;
            Set(_state.Facing, (_world.TargetPosition - _world.Position).sqrMagnitude > .0001f ? (_world.TargetPosition - _world.Position).normalized : Facing);
            _world.StopMoving(); SetSemantic(BehaviorSemantic.AttackWindup, true); return true;
        }

        public bool RequestAttack(string targetId)
        {
            return targetId == _world.TargetId && TryAcceptAttack();
        }

        NodeStatus Windup(float dt)
        {
            _world.StopMoving();
            if (_state.AttackPhase != MonsterAttackPhase.Windup) { return NodeStatus.Success; }
            if (_machine.TickSequence != _phaseTick) { _state.PhaseRemaining = Mathf.Max(0, _state.PhaseRemaining - dt); }
            return _state.PhaseRemaining == 0 ? NodeStatus.Success : NodeStatus.Running;
        }

        NodeStatus Strike()
        {
            if (_state.AttackPhase != MonsterAttackPhase.Windup || _state.HitResolved) { return NodeStatus.Success; }
            _state.HitResolved = true; _state.AttackPhase = MonsterAttackPhase.Recovery;
            _state.PhaseRemaining = _config.RecoverySeconds; _phaseTick = _machine.TickSequence;
            if (_authorized && _world.CanAttack(_state.TargetId))
            {
                _hitPermit = _state.ActionId;
                try { _world.Hit(_state.TargetId, _state.ActionId); }
                finally { _hitPermit = 0; }
            }
            SetSemantic(BehaviorSemantic.Recovery); return NodeStatus.Success;
        }

        NodeStatus Recover(float dt)
        {
            _world.StopMoving();
            if (_machine.TickSequence != _phaseTick) { _state.PhaseRemaining = Mathf.Max(0, _state.PhaseRemaining - dt); }
            if (_state.PhaseRemaining > 0) { return NodeStatus.Running; }
            CancelAttack(); return NodeStatus.Success;
        }

        void CancelAttack()
        {
            _state.AttackPhase = MonsterAttackPhase.None; _state.PhaseRemaining = 0; _state.HitResolved = false;
            _state.TargetId = string.Empty;
        }

        internal bool TryConsumeHit(long action)
        {
            if (!IsAuthorized || action <= 0 || action != _hitPermit) { return false; }
            _hitPermit = 0; return true;
        }

        NodeStatus Chase()
        {
            if ((_world.BlockedActions & StatusActionBlock.Move) != 0) { _world.StopMoving(); return NodeStatus.Running; }
            _world.Move(_visible ? NavigationTargetKind.Player : NavigationTargetKind.Point, Point(_state.LastSeen));
            SetMovementSemantic(_world.NavigationStatus); return NodeStatus.Running;
        }

        NodeStatus Wander(float dt)
        {
            if ((_world.BlockedActions & StatusActionBlock.Move) != 0) { _world.StopMoving(); return NodeStatus.Running; }
            if (_state.HasWanderGoal)
            {
                NavigationStatus status = _world.NavigationStatus;
                if (Vector2.Distance(_world.Position, Point(_state.WanderGoal)) <= .2f
                    || status == NavigationStatus.Unreachable || status == NavigationStatus.BudgetExceeded)
                {
                    _state.HasWanderGoal = false; _state.WaitRemaining = RandomWait(); _world.StopMoving();
                }
                else
                {
                    _world.Move(NavigationTargetKind.Point, Point(_state.WanderGoal));
                    SetMovementSemantic(status); return NodeStatus.Running;
                }
            }
            SetSemantic(BehaviorSemantic.Idle); _world.StopMoving();
            _state.WaitRemaining = Mathf.Max(0, _state.WaitRemaining - dt);
            if (_state.WaitRemaining > 0) { return NodeStatus.Running; }
            Vector2 anchor = Point(_state.Anchor);
            if (Vector2.Distance(_world.Position, anchor) > _config.WanderRadius)
            {
                Set(_state.WanderGoal, anchor); _state.HasWanderGoal = _world.CanStand(anchor);
            }
            else
            {
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    float angle = NextRandom() * Mathf.PI * 2;
                    Vector2 goal = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (_config.WanderRadius * Mathf.Sqrt(NextRandom()));
                    if (!_world.CanStand(goal)) { continue; }
                    Set(_state.WanderGoal, goal); _state.HasWanderGoal = true; break;
                }
            }
            if (!_state.HasWanderGoal) { _state.WaitRemaining = RandomWait(); }
            return NodeStatus.Running;
        }

        void SetMovementSemantic(NavigationStatus status)
        {
            // 重算路径不结束已开始的移动表现；实际到达、失败或行动切换仍立即停止。
            bool moving = status == NavigationStatus.Moving
                || _semantic == BehaviorSemantic.Move && status is NavigationStatus.Pending or NavigationStatus.Busy;
            SetSemantic(moving ? BehaviorSemantic.Move : BehaviorSemantic.Idle);
        }

        float RandomWait() { return Mathf.Lerp(_config.WaitMinimum, _config.WaitMaximum, NextRandom()); }
        float NextRandom()
        {
            ulong value = unchecked((ulong)(uint)_state.RandomSeed + (ulong)checked(++_state.RandomSequence) * 0x9E3779B97F4A7C15UL);
            value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
            value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
            return ((value ^ (value >> 31)) & 0xffffff) / 16777216f;
        }

        public void Revoke() { _authorized = false; _world.StopMoving(); }
        public void Stop() { Revoke(); Check(_machine.Stop()); _publish = null; _semantic = BehaviorSemantic.Stopped; }
        public void Control(BehaviorControl control)
        {
            if (!_authorized) { return; }
            if (control == BehaviorControl.TogglePause) { _paused = !_paused; _world.StopMoving(); SetSemantic(_semantic, true); }
            else if (control == BehaviorControl.Interrupt) { CancelAttack(); Check(_tree.Abort()); _world.StopMoving(); SetSemantic(BehaviorSemantic.Interrupted); }
            else if (control == BehaviorControl.RequestAttack) { TryAcceptAttack(); }
        }
        public bool ObserveAnimation(long actionInstanceId)
        {
            if (!IsAuthorized || actionInstanceId != _state.ActionId || actionInstanceId <= _observedAction || _state.AttackPhase == MonsterAttackPhase.None) { return false; }
            _observedAction = actionInstanceId; return true;
        }
        void SetSemantic(BehaviorSemantic semantic, bool force = false)
        {
            if (_semantic == semantic && !force) { return; }
            _semantic = semantic;
            if (_authorized) { _publish?.Invoke(Presentation); }
        }
        static Vector2 Point(Vector3Dto point) { return new Vector2(point.X, point.Y); }
        static void Set(Vector3Dto target, Vector2 value) { target.X = value.x; target.Y = value.y; target.Z = 0; }
        static void Check(HfsmResult result)
        {
            if (result.Status == HfsmStatus.Faulted) { throw new InvalidOperationException("怪物状态机故障", result.Fault.Cause); }
        }
        void Check(TreeTickResult result)
        {
            if (result.Status == TreeStatus.Faulted) { throw new InvalidOperationException("怪物行为树故障", result.Fault.Cause); }
        }
        public IDisposable AcquireDiagnostics() { return new Diagnostics(_machine.AcquireDiagnostics(), _tree.AcquireDiagnostics()); }
        sealed class Diagnostics : IDisposable
        {
            IDisposable _machine, _tree;
            internal Diagnostics(IDisposable machine, IDisposable tree) { _machine = machine; _tree = tree; }
            public void Dispose() { _machine?.Dispose(); _tree?.Dispose(); _machine = null; _tree = null; }
        }
        public BehaviorDebugSnapshot Capture()
        {
            var path = new int[MachineDefinition.MaximumDepth]; int count = _machine.CopyActivePath(path);
            var states = new BehaviorDebugNode[MachineDefinition.States.Count];
            for (int i = 0; i < states.Length; i++)
            {
                HfsmStateInfo state = MachineDefinition.States[i];
                states[i] = new BehaviorDebugNode(state.Id, state.ParentIndex, state.Depth, Array.IndexOf(path, i, 0, count) >= 0 ? "Running" : "Inactive");
            }
            var observations = new BehaviorNodeObservation[TreeDefinition.Nodes.Count]; _tree.CopyObservations(observations);
            var nodes = new BehaviorDebugNode[observations.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                BehaviorNodeInfo node = TreeDefinition.Nodes[i];
                nodes[i] = new BehaviorDebugNode(node.Id, node.ParentIndex, node.Depth, observations[i].State.ToString(), $"gate {observations[i].EntryGuardPassed} @ {observations[i].EntryGuardTick}");
            }
            var records = new HfsmTransitionRecord[64]; int recordCount = _machine.CopyTransitionTrace(records);
            var trace = new List<string>();
            for (int i = 0; i < recordCount; i++) { trace.Add($"#{records[i].TickSequence} {MachineDefinition.Transitions[records[i].TransitionIndex].Reason}"); }
            return new BehaviorDebugSnapshot(Handle, _name,
                $"{Mode} | range {_activeRange} | visible {_visible} | memory {_state.MemoryRemaining:F2}s | cooldown {CooldownRemaining:F2}s | {_state.AttackPhase} {_state.PhaseRemaining:F2}s | action {_state.ActionId}",
                states, nodes, trace.ToArray(), Presentation);
        }
    }
}
