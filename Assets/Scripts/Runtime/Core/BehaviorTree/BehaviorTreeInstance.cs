using System;
using System.Collections.Generic;

namespace DarkFlare.BehaviorTree
{
    public sealed class BehaviorTreeInstance<TContext>
    {
        readonly TContext _context;
        readonly bool[] _entered;
        readonly int[] _cursor;
        readonly double[] _deadline;
        readonly BehaviorNodeState[] _states;
        readonly long[] _visited;
        readonly int[] _cleanupOrder;
        readonly int _traceCapacity;
        IBehaviorAction<TContext>[] _actions;
        long[] _guardTicks;
        bool[] _guardPassed;
        long[] _entryGuardTicks;
        bool[] _entryGuardPassed;
        BehaviorTraceRecord[] _trace;
        int _traceCount;
        int _traceNext;
        int _owners;
        int _budget;
        int _currentNode;
        bool _executing;
        bool _exitFailed;

        public BehaviorTreeDefinition<TContext> Definition { get; }
        public TreeStatus Status { get; private set; }
        public BehaviorFault Fault { get; private set; }
        public long TickSequence { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public int LastVisits { get; private set; }
        public bool DiagnosticsEnabled => _owners > 0;

        internal BehaviorTreeInstance(BehaviorTreeDefinition<TContext> definition, TContext context, int traceCapacity)
        {
            if (traceCapacity <= 0) { throw new ArgumentOutOfRangeException(nameof(traceCapacity)); }
            Definition = definition;
            _context = context;
            _traceCapacity = traceCapacity;
            int count = definition.Nodes.Count;
            _entered = new bool[count];
            _cursor = new int[count];
            _deadline = new double[count];
            _states = new BehaviorNodeState[count];
            _visited = new long[count];
            _cleanupOrder = new int[count];
            for (int i = 0; i < count; i++) { _cleanupOrder[i] = i; }
            Array.Sort(_cleanupOrder, (a, b) => definition.Nodes[b].Depth.CompareTo(definition.Nodes[a].Depth));
            _actions = definition.CreateActions();
        }

        public TreeTickResult Start()
        {
            EnsureBoundary();
            if (Status == TreeStatus.Running || Status == TreeStatus.Faulted) { throw new InvalidOperationException("运行中不可重复 Start，故障后须 Reset"); }
            Array.Clear(_entered, 0, _entered.Length);
            Array.Clear(_states, 0, _states.Length);
            Array.Clear(_visited, 0, _visited.Length);
            ClearDiagnostics();
            LastVisits = 0;
            Status = TreeStatus.Running;
            return Result();
        }

        public TreeTickResult Tick(float deltaTime, int visitBudget = 0)
        {
            EnsureBoundary();
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            if (visitBudget < 0) { throw new ArgumentOutOfRangeException(nameof(visitBudget)); }
            if (Status != TreeStatus.Running || deltaTime == 0) { return Result(); }
            _executing = true;
            _budget = visitBudget == 0 ? Definition.VisitBudget : Math.Min(visitBudget, Definition.VisitBudget);
            LastVisits = 0;
            try
            {
                TickSequence = checked(TickSequence + 1);
                ElapsedSeconds += deltaTime;
                NodeStatus result = Visit(Definition.RootIndex, deltaTime);
                if (result == NodeStatus.Success) { Status = TreeStatus.Success; }
                else if (result == NodeStatus.Failure) { Status = TreeStatus.Failure; }
            }
            catch (Exception error) { Fail(error); }
            finally { _executing = false; }
            return Result();
        }

        public TreeTickResult Abort()
        {
            EnsureBoundary();
            if (Status != TreeStatus.Running) { return Result(); }
            _executing = true;
            try
            {
                AbortNode(Definition.RootIndex, BehaviorExitReason.Aborted);
                Status = TreeStatus.Aborted;
            }
            catch (Exception error) { Fail(error); }
            finally { _executing = false; }
            return Result();
        }

        public void Reset()
        {
            EnsureBoundary();
            if (Status == TreeStatus.Running) { throw new InvalidOperationException("运行中不能 Reset"); }
            if (Fault != null && !Fault.CleanupSucceeded) { throw new InvalidOperationException("清理失败的实例不能重用"); }
            _executing = true;
            try
            {
                _actions = Definition.CreateActions();
                Array.Clear(_entered, 0, _entered.Length);
                Array.Clear(_states, 0, _states.Length);
                Array.Clear(_visited, 0, _visited.Length);
                ClearDiagnostics();
                Fault = null;
                Status = TreeStatus.NotStarted;
                TickSequence = 0;
                ElapsedSeconds = 0;
                LastVisits = 0;
            }
            finally { _executing = false; }
        }

        NodeStatus Visit(int index, float deltaTime)
        {
            Consume(index);
            BehaviorNodeInfo node = Definition.Nodes[index];
            _visited[index] = TickSequence;
            if (!_entered[index])
            {
                _entered[index] = true;
                _cursor[index] = node.Kind == BehaviorNodeKind.PrioritySelector ? -1 : 0;
                _deadline[index] = ElapsedSeconds + node.WaitSeconds;
                SetState(index, BehaviorNodeState.Running);
                _actions[index]?.Enter(_context);
            }
            NodeStatus result;
            switch (node.Kind)
            {
                case BehaviorNodeKind.Action: result = _actions[index].Tick(_context, deltaTime); break;
                case BehaviorNodeKind.Condition: result = Observe(index, false) ? NodeStatus.Success : NodeStatus.Failure; break;
                case BehaviorNodeKind.Wait: result = ElapsedSeconds >= _deadline[index] ? NodeStatus.Success : NodeStatus.Running; break;
                case BehaviorNodeKind.MemorySequence: result = Sequence(index, node, deltaTime); break;
                case BehaviorNodeKind.PrioritySelector: result = Select(index, node, deltaTime); break;
                case BehaviorNodeKind.Guard:
                    if (Observe(index, false)) { result = Visit(node.Children[0], deltaTime); }
                    else { AbortNode(node.Children[0], BehaviorExitReason.Aborted); result = NodeStatus.Failure; }
                    break;
                default: throw new InvalidOperationException("未知节点类型");
            }
            _currentNode = index;
            if (result == NodeStatus.Running) { return result; }
            if (result != NodeStatus.Success && result != NodeStatus.Failure) { throw new InvalidOperationException("动作返回非法状态"); }
            Finish(index, result == NodeStatus.Success ? BehaviorExitReason.Success : BehaviorExitReason.Failure);
            return result;
        }

        NodeStatus Sequence(int index, BehaviorNodeInfo node, float deltaTime)
        {
            while (_cursor[index] < node.Children.Count)
            {
                NodeStatus result = Visit(node.Children[_cursor[index]], deltaTime);
                if (result != NodeStatus.Success) { return result; }
                _cursor[index]++;
            }
            return NodeStatus.Success;
        }

        NodeStatus Select(int index, BehaviorNodeInfo node, float deltaTime)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                int child = node.Children[i];
                Consume(child);
                if (!Observe(child, true))
                {
                    if (_cursor[index] == i) { AbortNode(child, BehaviorExitReason.Aborted); _cursor[index] = -1; }
                    continue;
                }
                if (_cursor[index] >= 0 && _cursor[index] != i)
                {
                    AbortNode(node.Children[_cursor[index]], BehaviorExitReason.Aborted);
                }
                _cursor[index] = i;
                NodeStatus result = Visit(child, deltaTime);
                if (result != NodeStatus.Failure) { return result; }
                _cursor[index] = -1;
            }
            return NodeStatus.Failure;
        }

        void Consume(int node)
        {
            _currentNode = node;
            if (LastVisits >= _budget) { throw new InvalidOperationException("行为树本步访问预算耗尽"); }
            LastVisits++;
        }

        bool Observe(int node, bool gate)
        {
            _currentNode = node;
            bool result = Definition.Evaluate(node, _context, gate);
            if (DiagnosticsEnabled)
            {
                if (gate) { _entryGuardTicks[node] = TickSequence; _entryGuardPassed[node] = result; }
                else { _guardTicks[node] = TickSequence; _guardPassed[node] = result; }
            }
            return result;
        }

        void Finish(int index, BehaviorExitReason reason)
        {
            _currentNode = index;
            _entered[index] = false;
            SetState(index, reason == BehaviorExitReason.Success ? BehaviorNodeState.Success :
                reason == BehaviorExitReason.Failure ? BehaviorNodeState.Failure : BehaviorNodeState.Aborted);
            try { _actions[index]?.Exit(_context, reason); }
            catch { _exitFailed = true; throw; }
        }

        void AbortNode(int index, BehaviorExitReason reason)
        {
            if (!_entered[index]) { return; }
            IReadOnlyList<int> children = Definition.Nodes[index].Children;
            for (int i = children.Count - 1; i >= 0; i--) { AbortNode(children[i], reason); }
            Finish(index, reason);
        }

        void Fail(Exception cause)
        {
            Status = TreeStatus.Faulted;
            int failedNode = _currentNode;
            var errors = new List<Exception>();
            if (_exitFailed) { errors.Add(cause); }
            _exitFailed = false;
            foreach (int node in _cleanupOrder)
            {
                if (!_entered[node]) { continue; }
                _entered[node] = false;
                SetState(node, BehaviorNodeState.Aborted);
                try { _actions[node]?.Exit(_context, BehaviorExitReason.Faulted); }
                catch (Exception error) { errors.Add(error); }
            }
            SetState(failedNode, BehaviorNodeState.Faulted);
            Fault = new BehaviorFault(cause, errors);
        }

        void SetState(int node, BehaviorNodeState state)
        {
            if (_states[node] == state) { return; }
            _states[node] = state;
            if (!DiagnosticsEnabled) { return; }
            _trace[_traceNext] = new BehaviorTraceRecord(TickSequence, node, state);
            _traceNext = (_traceNext + 1) % _traceCapacity;
            _traceCount = Math.Min(_traceCount + 1, _traceCapacity);
        }

        public int CopyObservations(Span<BehaviorNodeObservation> destination)
        {
            EnsureBoundary();
            if (destination.Length < _states.Length) { throw new ArgumentException("节点缓冲不足", nameof(destination)); }
            for (int i = 0; i < _states.Length; i++)
            {
                destination[i] = new BehaviorNodeObservation(_states[i], _visited[i],
                    DiagnosticsEnabled ? _guardTicks[i] : 0, DiagnosticsEnabled && _guardPassed[i],
                    DiagnosticsEnabled ? _entryGuardTicks[i] : 0, DiagnosticsEnabled && _entryGuardPassed[i]);
            }
            return _states.Length;
        }

        public IDisposable AcquireDiagnostics()
        {
            EnsureBoundary();
            if (_owners == 0)
            {
                _guardTicks ??= new long[_states.Length];
                _guardPassed ??= new bool[_states.Length];
                _entryGuardTicks ??= new long[_states.Length];
                _entryGuardPassed ??= new bool[_states.Length];
                _trace ??= new BehaviorTraceRecord[_traceCapacity];
                ClearDiagnostics();
            }
            _owners++;
            return new Lease(this);
        }

        public int CopyTrace(Span<BehaviorTraceRecord> destination)
        {
            EnsureBoundary();
            if (!DiagnosticsEnabled) { return 0; }
            if (destination.Length < _traceCount) { throw new ArgumentException("轨迹缓冲不足", nameof(destination)); }
            int start = (_traceNext - _traceCount + _traceCapacity) % _traceCapacity;
            for (int i = 0; i < _traceCount; i++) { destination[i] = _trace[(start + i) % _traceCapacity]; }
            return _traceCount;
        }

        void ClearDiagnostics()
        {
            if (_guardTicks != null) { Array.Clear(_guardTicks, 0, _guardTicks.Length); }
            if (_entryGuardTicks != null) { Array.Clear(_entryGuardTicks, 0, _entryGuardTicks.Length); }
            if (_trace != null) { Array.Clear(_trace, 0, _trace.Length); }
            _traceCount = 0;
            _traceNext = 0;
        }

        void EnsureBoundary()
        {
            if (_executing) { throw new InvalidOperationException("禁止行为树回调重入执行或读取未提交快照"); }
        }

        TreeTickResult Result()
        {
            return new TreeTickResult(Status, LastVisits, Fault);
        }

        sealed class Lease : IDisposable
        {
            BehaviorTreeInstance<TContext> _owner;
            internal Lease(BehaviorTreeInstance<TContext> owner) { _owner = owner; }
            public void Dispose()
            {
                if (_owner == null) { return; }
                _owner.EnsureBoundary();
                if (--_owner._owners == 0) { _owner.ClearDiagnostics(); }
                _owner = null;
            }
        }
    }
}
