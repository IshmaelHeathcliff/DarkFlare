using System;
using System.Collections.Generic;
using DarkFlare.BehaviorTree;
using DarkFlare.HFSM;
using UnityEngine;

namespace DarkFlare
{
    public sealed class PatrolBehaviorRuntime : IBehaviorRuntime
    {
        sealed class Context
        {
            internal PatrolBehaviorRuntime Owner { get; }
            internal BehaviorTreeInstance<Context> Tree { get; set; }
            internal bool Interrupt { get; set; }
            internal bool Attack { get; set; }
            internal Vector2 Position { get; set; }
            internal Vector2 Origin { get; }
            internal double HoldUntil { get; set; }
            internal Context(PatrolBehaviorRuntime owner, Vector2 origin) { Owner = owner; Position = origin; Origin = origin; }
        }

        sealed class EmptyState : IHfsmState<Context>
        {
            public void Enter(Context context) { }
            public void Tick(Context context, float deltaTime) { }
            public void Exit(Context context, HfsmExitReason reason) { }
        }
        sealed class InterruptedState : IHfsmState<Context>
        {
            public void Enter(Context context)
            {
                context.HoldUntil = context.Owner._machine.ElapsedSeconds + .8;
                context.Attack = false;
                context.Owner.Begin(BehaviorSemantic.Interrupted);
            }
            public void Tick(Context context, float deltaTime)
            {
                if (context.Owner._machine.ElapsedSeconds >= context.HoldUntil) { context.Interrupt = false; }
            }
            public void Exit(Context context, HfsmExitReason reason) { }
        }
        sealed class IdleAction : IBehaviorAction<Context>
        {
            public void Enter(Context context) { context.Owner.Begin(BehaviorSemantic.Idle); }
            public NodeStatus Tick(Context context, float deltaTime) { return context.Owner._authorized ? NodeStatus.Success : NodeStatus.Failure; }
            public void Exit(Context context, BehaviorExitReason reason) { }
        }
        sealed class MoveAction : IBehaviorAction<Context>
        {
            readonly bool _outbound;
            public MoveAction(bool outbound) { _outbound = outbound; }
            public void Enter(Context context) { context.Owner.Begin(BehaviorSemantic.Move); }
            public NodeStatus Tick(Context context, float deltaTime)
            {
                PatrolBehaviorRuntime owner = context.Owner;
                if (!owner._authorized) { return NodeStatus.Failure; }
                Vector2 target = context.Origin + (_outbound ? Vector2.right * owner._distance : Vector2.zero);
                context.Position = Vector2.MoveTowards(context.Position, target, owner._speed * deltaTime);
                owner.Publish();
                return context.Position == target ? NodeStatus.Success : NodeStatus.Running;
            }
            public void Exit(Context context, BehaviorExitReason reason) { }
        }
        sealed class AttackAction : IBehaviorAction<Context>
        {
            double _started;
            public void Enter(Context context)
            {
                _started = context.Tree.ElapsedSeconds;
                context.Owner.Begin(BehaviorSemantic.AttackWindup);
            }
            public NodeStatus Tick(Context context, float deltaTime)
            {
                PatrolBehaviorRuntime owner = context.Owner;
                if (!owner._authorized) { return NodeStatus.Failure; }
                double elapsed = context.Tree.ElapsedSeconds - _started;
                BehaviorSemantic phase = elapsed < owner._windup ? BehaviorSemantic.AttackWindup :
                    elapsed < owner._windup + owner._attackDuration ? BehaviorSemantic.Attack : BehaviorSemantic.Recovery;
                if (phase != owner._semantic) { owner._semantic = phase; owner.Publish(); }
                return elapsed >= owner._windup + owner._attackDuration + owner._recovery ? NodeStatus.Success : NodeStatus.Running;
            }
            public void Exit(Context context, BehaviorExitReason reason) { context.Attack = false; }
        }

        public sealed class Template
        {
            readonly BehaviorTreeDefinition<Context> _tree;
            readonly HfsmDefinition<Context> _hfsm;
            readonly float _speed, _distance, _windup, _attack, _recovery, _wait;
            public Template(BehaviorSampleConfig config)
            {
                if (config == null) { throw new ArgumentNullException(nameof(config)); }
                config.Validate();
                _speed = config.Speed; _distance = config.Distance; _windup = config.WindupSeconds;
                _attack = config.AttackSeconds; _recovery = config.RecoverySeconds;
                _wait = config.WaitSeconds;
                _tree = new BehaviorTreeBuilder<Context>()
                    .AddAction("attack", () => new AttackAction())
                    .AddAction("idle.out", () => new IdleAction()).AddWait("wait.out", config.WaitSeconds)
                    .AddAction("move.out", () => new MoveAction(true))
                    .AddAction("idle.back", () => new IdleAction()).AddWait("wait.back", config.WaitSeconds)
                    .AddAction("move.back", () => new MoveAction(false))
                    .AddSequence("patrol", "idle.out", "wait.out", "move.out", "idle.back", "wait.back", "move.back")
                    .AddPrioritySelector("choose", new BehaviorBranch<Context>("attack", c => c.Attack),
                        new BehaviorBranch<Context>("patrol", c => true)).Build("choose");
                _hfsm = new HfsmBuilder<Context>().AddState("root", () => new EmptyState())
                    .AddState("active", () => new HfsmBehaviorTreeState<Context>(c => c.Tree), "root")
                    .AddState("interrupted", () => new InterruptedState(), "root").SetInitialChild("root", "active")
                    .AddTransition("active", "interrupted", c => c.Interrupt, 100, reason: "interrupt requested")
                    .AddTransition("interrupted", "active", c => !c.Interrupt, reason: "hold complete")
                    .AddTransition("active", "active", c => c.Tree.Status == TreeStatus.Success || c.Tree.Status == TreeStatus.Failure,
                        selfTransition: HfsmSelfTransition.Reenter, reason: "begin next patrol").Build();
            }
            public PatrolBehaviorRuntime Create(string name, Vector2 origin)
            {
                return new PatrolBehaviorRuntime(_tree, _hfsm, name, origin, _speed, _distance, _windup, _attack, _recovery, _wait);
            }
        }

        readonly string _name;
        readonly Context _context;
        readonly HfsmInstance<Context> _machine;
        readonly float _speed, _distance, _windup, _attackDuration, _recovery, _wait;
        BehaviorHandle _handle;
        Action<BehaviorPresentationEvent> _publish;
        BehaviorSemantic _semantic;
        long _actionId;
        long _observedAction;
        double _actionStarted;
        double _actionDuration;
        bool _authorized;
        bool _paused;

        PatrolBehaviorRuntime(BehaviorTreeDefinition<Context> tree, HfsmDefinition<Context> hfsm, string name,
            Vector2 origin, float speed, float distance, float windup, float attack, float recovery, float wait)
        {
            _name = name; _speed = speed; _distance = distance;
            _windup = windup; _attackDuration = attack; _recovery = recovery;
            _wait = wait;
            _context = new Context(this, origin);
            _context.Tree = tree.CreateInstance(_context);
            _machine = hfsm.CreateInstance(_context);
        }
        public BehaviorPresentationEvent Presentation => new BehaviorPresentationEvent(_handle, _actionId, _semantic, _context.Position, _paused,
            Math.Max(0, _machine.ElapsedSeconds - _actionStarted), _actionDuration);
        public long TickSequence => _machine.TickSequence;
        public int LastTreeVisits => _context.Tree.LastVisits;
        public bool DiagnosticsEnabled => _machine.DiagnosticsEnabled || _context.Tree.DiagnosticsEnabled;

        public void Start(BehaviorHandle handle, Action<BehaviorPresentationEvent> publish)
        {
            _handle = handle; _publish = publish; _authorized = true;
            Check(_machine.Start());
            Begin(BehaviorSemantic.Idle);
        }
        public void Tick(float deltaTime) { if (_authorized && !_paused) { Check(_machine.Tick(deltaTime)); } }
        public void Revoke() { _authorized = false; }
        public void Stop()
        {
            _authorized = false; _publish = null;
            Check(_machine.Stop());
            _semantic = BehaviorSemantic.Stopped;
        }
        public void Control(BehaviorControl control)
        {
            if (!_authorized) { return; }
            if (control == BehaviorControl.TogglePause) { _paused = !_paused; Publish(); }
            else if (control == BehaviorControl.Interrupt) { _context.Interrupt = true; }
            else if (control == BehaviorControl.RequestAttack) { _context.Attack = true; }
        }
        public bool ObserveAnimation(long actionInstanceId)
        {
            if (!_authorized || _paused || actionInstanceId != _actionId || actionInstanceId <= _observedAction
                || (_semantic != BehaviorSemantic.Attack && _semantic != BehaviorSemantic.Recovery)) { return false; }
            _observedAction = actionInstanceId;
            return true;
        }
        void Begin(BehaviorSemantic semantic)
        {
            if (!_authorized) { return; }
            _actionId = checked(_actionId + 1); _semantic = semantic; _actionStarted = _machine.ElapsedSeconds;
            _actionDuration = semantic == BehaviorSemantic.AttackWindup ? _windup + _attackDuration + _recovery :
                semantic == BehaviorSemantic.Move ? _distance / _speed : semantic == BehaviorSemantic.Interrupted ? .8 : _wait;
            Publish();
        }
        void Publish() { if (_authorized) { _publish?.Invoke(Presentation); } }
        static void Check(HfsmResult result)
        {
            if (result.Status == HfsmStatus.Faulted) { throw new InvalidOperationException("HFSM 行为故障", result.Fault.Cause); }
        }
        public IDisposable AcquireDiagnostics()
        {
            return new DiagnosticLease(_machine.AcquireDiagnostics(), _context.Tree.AcquireDiagnostics());
        }
        sealed class DiagnosticLease : IDisposable
        {
            IDisposable _hfsm, _tree;
            internal DiagnosticLease(IDisposable hfsm, IDisposable tree) { _hfsm = hfsm; _tree = tree; }
            public void Dispose() { _hfsm?.Dispose(); _tree?.Dispose(); _hfsm = null; _tree = null; }
        }

        public BehaviorDebugSnapshot Capture()
        {
            var path = new int[_machine.Definition.MaximumDepth];
            int count = _machine.CopyActivePath(path);
            var hfsm = new BehaviorDebugNode[_machine.Definition.States.Count];
            for (int i = 0; i < hfsm.Length; i++)
            {
                HfsmStateInfo info = _machine.Definition.States[i];
                bool active = Array.IndexOf(path, i, 0, count) >= 0;
                hfsm[i] = new BehaviorDebugNode(info.Id, info.ParentIndex, info.Depth, active ? "Running" : "Inactive");
            }
            var observations = new BehaviorNodeObservation[_context.Tree.Definition.Nodes.Count];
            _context.Tree.CopyObservations(observations);
            var nodes = new BehaviorDebugNode[observations.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                BehaviorNodeInfo info = _context.Tree.Definition.Nodes[i];
                BehaviorNodeObservation observation = observations[i];
                string gate = observation.EntryGuardTick == 0 ? "unobserved" : $"{observation.EntryGuardPassed} @ {observation.EntryGuardTick}";
                nodes[i] = new BehaviorDebugNode(info.Id, info.ParentIndex, info.Depth, observation.State.ToString(), $"{info.Kind} | gate {gate}");
            }
            var trace = new List<string>();
            var transitions = new HfsmTransitionRecord[64];
            int transitionCount = _machine.CopyTransitionTrace(transitions);
            for (int i = 0; i < transitionCount; i++)
            {
                HfsmTransitionInfo info = _machine.Definition.Transitions[transitions[i].TransitionIndex];
                trace.Add($"#{transitions[i].TickSequence} {info.Reason}");
            }
            var guards = new HfsmGuardObservation[_machine.Definition.Transitions.Count];
            int guardCount = _machine.CopyGuardObservations(guards);
            for (int i = 0; i < guardCount; i++)
            {
                trace.Add($"{_machine.Definition.Transitions[i].Reason}: {(guards[i].TickSequence == 0 ? "unobserved" : $"{guards[i].Passed} @ {guards[i].TickSequence}")}");
            }
            return new BehaviorDebugSnapshot(_handle, _name,
                $"HFSM {_machine.Status} / BT {_context.Tree.Status} | tick {_machine.TickSequence} | t {_machine.ElapsedSeconds:F2}s | visits {_context.Tree.LastVisits} | action {_actionId}",
                hfsm, nodes, trace.ToArray(), Presentation);
        }
    }
}
