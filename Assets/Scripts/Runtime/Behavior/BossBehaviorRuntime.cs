using System;
using System.Collections.Generic;
using DarkFlare.BehaviorTree;
using DarkFlare.HFSM;
using UnityEngine;

namespace DarkFlare
{
    public enum BossAttackKind { Swipe, Charge }

    public interface IBossBehaviorWorld
    {
        Vector2 Position { get; }
        Vector2 Velocity { get; }
        bool IsAlive { get; }
        float HealthFraction { get; }
        StatusActionBlock BlockedActions { get; }
        bool TargetAvailable { get; }
        Vector2 TargetPosition { get; }
        string TargetId { get; }
        bool HasLineOfSight(Vector2 target);
        bool CanBossAct(string targetId);
        void Move(NavigationTargetKind kind, Vector2 target);
        void StopMoving();
        bool TryHit(BossAttackKind kind, string targetId, long actionId, Vector2 from, Vector2 to, Vector2 facing);
    }

    public sealed class BossBehaviorRuntime : IBehaviorRuntime
    {
        sealed class State : IHfsmState<BossBehaviorRuntime>
        {
            readonly bool _runTree;
            internal State(bool runTree) { _runTree = runTree; }
            public void Enter(BossBehaviorRuntime context) { }
            public void Tick(BossBehaviorRuntime context, float deltaTime)
            {
                if (!_runTree) { return; }
                if (context._tree.Status != TreeStatus.Running) { context.Check(context._tree.Start()); }
                context.Check(context._tree.Tick(deltaTime));
            }
            public void Exit(BossBehaviorRuntime context, HfsmExitReason reason)
            {
                if (_runTree) { context.Check(context._tree.Abort()); }
            }
        }

        sealed class ActionNode : IBehaviorAction<BossBehaviorRuntime>
        {
            readonly int _kind;
            internal ActionNode(int kind) { _kind = kind; }
            public void Enter(BossBehaviorRuntime context) { }
            public NodeStatus Tick(BossBehaviorRuntime context, float deltaTime)
            {
                return _kind switch
                {
                    0 => context.Phase(deltaTime),
                    1 => context.Charge(deltaTime),
                    2 => context.Swipe(deltaTime),
                    _ => context.Approach()
                };
            }
            public void Exit(BossBehaviorRuntime context, BehaviorExitReason reason) { }
        }

        static readonly BehaviorTreeDefinition<BossBehaviorRuntime> TreeDefinition = new BehaviorTreeBuilder<BossBehaviorRuntime>()
            .AddAction("phase.transition", () => new ActionNode(0))
            .AddAction("charge", () => new ActionNode(1))
            .AddAction("swipe", () => new ActionNode(2))
            .AddAction("approach", () => new ActionNode(3))
            .AddPrioritySelector("decision",
                new BehaviorBranch<BossBehaviorRuntime>("phase.transition", c => c._state.Action == BossActionPhase.PhaseTransition),
                new BehaviorBranch<BossBehaviorRuntime>("charge", c => c._state.Action is BossActionPhase.ChargeWarning or BossActionPhase.ChargeTravel or BossActionPhase.ChargeRecovery),
                new BehaviorBranch<BossBehaviorRuntime>("swipe", c => c._state.Action is BossActionPhase.SwipeWarning or BossActionPhase.SwipeRecovery),
                new BehaviorBranch<BossBehaviorRuntime>("approach", c => true)).Build("decision");
        static readonly HfsmDefinition<BossBehaviorRuntime> MachineDefinition = new HfsmBuilder<BossBehaviorRuntime>()
            .AddState("root", () => new State(false))
            .AddState("alive", () => new State(false), "root")
            .AddState("normal", () => new State(true), "alive")
            .AddState("empowered", () => new State(true), "alive")
            .AddState("dead", () => new State(false), "root")
            .SetInitialChild("root", "alive").SetInitialChild("alive", "normal")
            .AddTransition("alive", "dead", c => !c._world.IsAlive, 1000, reason: "boss dead")
            .AddTransition("alive", "empowered", c => c._state.Empowered, 900, reason: "phase transition complete")
            .Build();

        readonly BossBehaviorDefinition _config;
        readonly MonsterBehaviorDefinition _sense;
        readonly IBossBehaviorWorld _world;
        readonly string _name;
        readonly HfsmInstance<BossBehaviorRuntime> _machine;
        readonly BehaviorTreeInstance<BossBehaviorRuntime> _tree;
        BossBehaviorStateDto _state;
        Action<BehaviorPresentationEvent> _publish;
        BehaviorSemantic _semantic;
        bool _authorized;
        bool _paused;
        bool _visible;
        bool _activeRange;
        float _perceptionRemaining;
        int _geometryRevision;
        long _hitPermit;
        long _observedAction;

        public BehaviorHandle Handle { get; private set; }
        public BehaviorPresentationEvent Presentation => new(Handle, _state.ActionId, _semantic, _world.Position, _paused,
            _state.Action == BossActionPhase.SwipeWarning ? _config.SwipeWarningSeconds - _state.PhaseRemaining
                : _state.Action == BossActionPhase.ChargeWarning ? _config.ChargeWarningSeconds - _state.PhaseRemaining : 0,
            _state.Action == BossActionPhase.SwipeWarning ? _config.SwipeWarningSeconds
                : _state.Action == BossActionPhase.ChargeWarning ? _config.ChargeWarningSeconds : 0);
        public bool IsAuthorized => _authorized && !_paused;
        public BossActionPhase Action => _state.Action;
        public bool ChargeRequested => IsAuthorized && _state.Action == BossActionPhase.ChargeTravel;
        public Vector2 ChargeDirection => new(_state.Facing.X, _state.Facing.Y);
        public Vector2 Facing => ChargeDirection;
        public float ChargeSpeed => _config.ChargeSpeed;
        public float ChargeRemaining => _state.ChargeRemaining;
        public float CooldownRemaining => Mathf.Max(_state.SwipeCooldown, _state.ChargeCooldown);

        public BossBehaviorRuntime(BossBehaviorDefinition config, MonsterBehaviorDefinition sense, IBossBehaviorWorld world,
            string name, BossBehaviorStateDto state)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _sense = sense != null ? sense : throw new ArgumentNullException(nameof(sense));
            _world = world ?? throw new ArgumentNullException(nameof(world)); _name = name;
            _config.Validate(); _sense.Validate();
            _tree = TreeDefinition.CreateInstance(this); _machine = MachineDefinition.CreateInstance(this);
            Restore(state);
        }

        public void Start(BehaviorHandle handle, Action<BehaviorPresentationEvent> publish)
        {
            Handle = handle; _publish = publish; _authorized = true; Check(_machine.Start());
            SetSemantic(Semantic(), true);
        }

        public void Restore(BossBehaviorStateDto state)
        {
            if (state == null || !state.IsValid) { throw new ArgumentException("Boss 行为状态非法"); }
            _state = state.Copy(); _perceptionRemaining = 0; _activeRange = true; _visible = false;
            if (_authorized) { Check(_tree.Abort()); _world.StopMoving(); SetSemantic(Semantic(), true); }
        }

        public BossBehaviorStateDto CaptureState() { return _state.Copy(); }
        public void InvalidatePerception(int revision)
        {
            if (_geometryRevision == revision) { return; }
            _geometryRevision = revision; _perceptionRemaining = 0;
        }

        public void Tick(float deltaTime)
        {
            if (!IsAuthorized || deltaTime == 0) { return; }
            if (!float.IsFinite(deltaTime) || deltaTime < 0) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            _state.SwipeCooldown = Mathf.Max(0, _state.SwipeCooldown - deltaTime);
            _state.ChargeCooldown = Mathf.Max(0, _state.ChargeCooldown - deltaTime);
            _state.MemoryRemaining = Mathf.Max(0, _state.MemoryRemaining - deltaTime);
            _perceptionRemaining -= deltaTime;
            if (_perceptionRemaining <= 0 || !_world.TargetAvailable)
            {
                Sense(); _perceptionRemaining = _sense.PerceptionInterval;
            }
            if (!_world.IsAlive || (_world.BlockedActions & StatusActionBlock.Attack) != 0
                || !_world.TargetAvailable || !_activeRange || _state.Action != BossActionPhase.None && _state.TargetId.Length > 0 && _state.TargetId != _world.TargetId)
            { CancelAction(); }
            if ((_world.BlockedActions & StatusActionBlock.Move) != 0 && _state.Action == BossActionPhase.ChargeTravel)
            { EndCharge(); }
            if (_world.IsAlive && _state.Action == BossActionPhase.None) { ChooseAction(); }
            if (_state.Action != BossActionPhase.None || !_visible && _state.MemoryRemaining == 0) { _world.StopMoving(); }
            Check(_machine.Tick(deltaTime));
        }

        void Sense()
        {
            _visible = false;
            if (!_world.TargetAvailable) { _activeRange = false; _state.MemoryRemaining = 0; return; }
            Vector2 offset = _world.TargetPosition - _world.Position;
            float limit = _activeRange ? _sense.DeactivationDistance : _sense.ActivationDistance;
            _activeRange = offset.sqrMagnitude <= limit * limit;
            if (!_activeRange) { _state.MemoryRemaining = 0; return; }
            Vector2 facing = Facing;
            _visible = offset.sqrMagnitude <= _sense.SightDistance * _sense.SightDistance
                && (offset.sqrMagnitude < .0001f || Vector2.Dot(facing, offset.normalized) >= _sense.SightCosine)
                && _world.HasLineOfSight(_world.TargetPosition);
            if (_visible)
            {
                _state.LastSeen.X = _world.TargetPosition.x; _state.LastSeen.Y = _world.TargetPosition.y;
                _state.MemoryRemaining = _sense.MemorySeconds;
            }
        }

        void ChooseAction()
        {
            if (!_world.IsAlive || !_activeRange || !_world.TargetAvailable
                || (_world.BlockedActions & StatusActionBlock.Attack) != 0) { return; }
            if (_world.HealthFraction <= _config.PhaseThreshold && !_state.Empowered)
            {
                Begin(BossActionPhase.PhaseTransition, _config.PhaseSeconds, string.Empty);
                return;
            }
            if (!_visible || (_world.BlockedActions & StatusActionBlock.Attack) != 0 || !_world.CanBossAct(_world.TargetId)) { return; }
            Vector2 offset = _world.TargetPosition - _world.Position;
            float distance = offset.magnitude;
            if (_state.ChargeCooldown <= 0 && distance >= _config.ChargeMinimumDistance && distance <= _sense.SightDistance)
            {
                Begin(BossActionPhase.ChargeWarning, _config.ChargeWarningSeconds, _world.TargetId);
                _state.ChargeRemaining = _config.ChargeDistance; Face(offset);
                _state.ChargeCooldown = _config.ChargeCooldownSeconds * (_state.Empowered ? _config.EmpoweredCooldownScale : 1);
            }
            else if (_state.SwipeCooldown <= 0 && distance <= _config.SwipeRadius)
            {
                Begin(BossActionPhase.SwipeWarning, _config.SwipeWarningSeconds, _world.TargetId);
                Face(offset);
                _state.SwipeCooldown = _config.SwipeCooldownSeconds * (_state.Empowered ? _config.EmpoweredCooldownScale : 1);
            }
        }

        void Begin(BossActionPhase action, float seconds, string targetId)
        {
            _state.ActionId = checked(_state.ActionId + 1); _state.Action = action; _state.PhaseRemaining = seconds;
            _state.TargetId = targetId; _state.HitResolved = false; _world.StopMoving(); SetSemantic(BehaviorSemantic.AttackWindup, true);
        }

        void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < .0001f) { return; }
            Vector2 facing = direction.normalized; _state.Facing.X = facing.x; _state.Facing.Y = facing.y;
        }

        NodeStatus Phase(float dt)
        {
            _world.StopMoving(); _state.PhaseRemaining = Mathf.Max(0, _state.PhaseRemaining - dt);
            if (_state.PhaseRemaining > 0) { return NodeStatus.Running; }
            _state.Empowered = true; CancelAction(); SetSemantic(BehaviorSemantic.Idle); return NodeStatus.Success;
        }

        NodeStatus Swipe(float dt)
        {
            _world.StopMoving(); _state.PhaseRemaining = Mathf.Max(0, _state.PhaseRemaining - dt);
            if (_state.PhaseRemaining > 0) { return NodeStatus.Running; }
            if (_state.Action == BossActionPhase.SwipeWarning)
            {
                _state.HitResolved = true; _hitPermit = _state.ActionId;
                try { _world.TryHit(BossAttackKind.Swipe, _state.TargetId, _state.ActionId, _world.Position, _world.Position, Facing); }
                finally { _hitPermit = 0; }
                _state.Action = BossActionPhase.SwipeRecovery; _state.PhaseRemaining = _config.SwipeRecoverySeconds;
                SetSemantic(BehaviorSemantic.Recovery); return NodeStatus.Running;
            }
            CancelAction(); SetSemantic(BehaviorSemantic.Idle); return NodeStatus.Success;
        }

        NodeStatus Charge(float dt)
        {
            _world.StopMoving();
            if (_state.Action == BossActionPhase.ChargeTravel) { return NodeStatus.Running; }
            _state.PhaseRemaining = Mathf.Max(0, _state.PhaseRemaining - dt);
            if (_state.PhaseRemaining > 0) { return NodeStatus.Running; }
            if (_state.Action == BossActionPhase.ChargeWarning)
            {
                _state.Action = BossActionPhase.ChargeTravel; SetSemantic(BehaviorSemantic.Attack); return NodeStatus.Running;
            }
            CancelAction(); SetSemantic(BehaviorSemantic.Idle); return NodeStatus.Success;
        }

        public void OnSteered(Vector2 from, Vector2 velocity, float deltaTime)
        {
            if (!ChargeRequested) { return; }
            if (velocity.sqrMagnitude < .0001f) { EndCharge(); return; }
            Vector2 to = from + velocity * deltaTime;
            if (!_state.HitResolved)
            {
                _hitPermit = _state.ActionId;
                try
                {
                    _state.HitResolved = _world.TryHit(BossAttackKind.Charge, _state.TargetId, _state.ActionId, from, to, Facing);
                }
                finally { _hitPermit = 0; }
            }
            _state.ChargeRemaining = Mathf.Max(0, _state.ChargeRemaining - velocity.magnitude * deltaTime);
            if (_state.ChargeRemaining <= .001f) { EndCharge(); }
        }

        void EndCharge()
        {
            _state.Action = BossActionPhase.ChargeRecovery; _state.ChargeRemaining = 0;
            _state.PhaseRemaining = _config.ChargeRecoverySeconds; SetSemantic(BehaviorSemantic.Recovery);
        }

        NodeStatus Approach()
        {
            if ((_world.BlockedActions & StatusActionBlock.Move) != 0 || !_activeRange || !_world.TargetAvailable)
            { _world.StopMoving(); SetSemantic(BehaviorSemantic.Idle); return NodeStatus.Running; }
            if (_visible || _state.MemoryRemaining > 0)
            {
                _world.Move(_visible ? NavigationTargetKind.Player : NavigationTargetKind.Point,
                    _visible ? _world.TargetPosition : new Vector2(_state.LastSeen.X, _state.LastSeen.Y));
                Vector2 velocity = _world.Velocity;
                if (velocity.sqrMagnitude > .01f) { Face(velocity); }
                SetSemantic(BehaviorSemantic.Move);
            }
            else { _world.StopMoving(); SetSemantic(BehaviorSemantic.Idle); }
            return NodeStatus.Running;
        }

        void CancelAction()
        {
            if (_state.Action == BossActionPhase.None) { return; }
            _state.Action = BossActionPhase.None; _state.PhaseRemaining = 0; _state.ChargeRemaining = 0;
            _state.TargetId = string.Empty; _state.HitResolved = false; _hitPermit = 0; _world.StopMoving();
            SetSemantic(BehaviorSemantic.Interrupted);
        }

        public bool TryConsumeHit(long actionId)
        {
            if (!IsAuthorized || actionId <= 0 || actionId != _hitPermit) { return false; }
            _hitPermit = 0; return true;
        }

        BehaviorSemantic Semantic()
        {
            return _state.Action is BossActionPhase.SwipeWarning or BossActionPhase.ChargeWarning or BossActionPhase.PhaseTransition
                ? BehaviorSemantic.AttackWindup : _state.Action == BossActionPhase.ChargeTravel ? BehaviorSemantic.Attack
                : _state.Action is BossActionPhase.SwipeRecovery or BossActionPhase.ChargeRecovery ? BehaviorSemantic.Recovery : BehaviorSemantic.Idle;
        }

        void SetSemantic(BehaviorSemantic semantic, bool force = false)
        {
            if (_semantic == semantic && !force) { return; }
            _semantic = semantic; if (_authorized) { _publish?.Invoke(Presentation); }
        }

        public void Revoke() { _authorized = false; _hitPermit = 0; _world.StopMoving(); }
        public void Stop() { Revoke(); Check(_machine.Stop()); _publish = null; _semantic = BehaviorSemantic.Stopped; }
        public void Control(BehaviorControl control)
        {
            if (!_authorized) { return; }
            if (control == BehaviorControl.TogglePause) { _paused = !_paused; _world.StopMoving(); SetSemantic(_semantic, true); }
            else if (control == BehaviorControl.Interrupt) { CancelAction(); Check(_tree.Abort()); _world.StopMoving(); }
            else if (control == BehaviorControl.RequestAttack) { ChooseAction(); }
        }
        public bool ObserveAnimation(long actionInstanceId)
        {
            if (!IsAuthorized || actionInstanceId != _state.ActionId || actionInstanceId <= _observedAction || _state.Action == BossActionPhase.None) { return false; }
            _observedAction = actionInstanceId; return true;
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
                nodes[i] = new BehaviorDebugNode(node.Id, node.ParentIndex, node.Depth, observations[i].State.ToString(),
                    $"{_state.Action} {_state.PhaseRemaining:F2}s");
            }
            var records = new HfsmTransitionRecord[64]; int recordCount = _machine.CopyTransitionTrace(records);
            var trace = new List<string>();
            for (int i = 0; i < recordCount; i++) { trace.Add($"#{records[i].TickSequence} {MachineDefinition.Transitions[records[i].TransitionIndex].Reason}"); }
            return new BehaviorDebugSnapshot(Handle, _name,
                $"{(_state.Empowered ? "Empowered" : "Normal")} | {_state.Action} {_state.PhaseRemaining:F2}s | charge {_state.ChargeRemaining:F2}m | swipe {_state.SwipeCooldown:F2}s | charge {_state.ChargeCooldown:F2}s | action {_state.ActionId}",
                states, nodes, trace.ToArray(), Presentation);
        }
        static void Check(HfsmResult result)
        {
            if (result.Status == HfsmStatus.Faulted) { throw new InvalidOperationException("Boss 状态机故障", result.Fault.Cause); }
        }
        void Check(TreeTickResult result)
        {
            if (result.Status == TreeStatus.Faulted) { throw new InvalidOperationException("Boss 行为树故障", result.Fault.Cause); }
        }
    }
}
