using System;
using System.Collections.Generic;

namespace DarkFlare.HFSM
{
    public sealed class HfsmInstance<TContext>
    {
        readonly TContext _context;
        readonly int[] _active;
        readonly int[] _destination;
        readonly int _traceCapacity;
        IHfsmState<TContext>[] _states;
        HfsmGuardObservation[] _guards;
        HfsmTransitionRecord[] _trace;
        int _activeCount;
        int _traceCount;
        int _traceNext;
        int _diagnosticOwners;
        bool _executing;
        bool _exitFailed;

        public HfsmDefinition<TContext> Definition { get; }
        public HfsmStatus Status { get; private set; }
        public HfsmFault Fault { get; private set; }
        public long TickSequence { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public long TransitionCount { get; private set; }
        public int LastTransitionIndex { get; private set; } = -1;
        public bool DiagnosticsEnabled => _diagnosticOwners > 0;
        public int ActiveCount
        {
            get { EnsureNotExecuting(); return _activeCount; }
        }
        public int CurrentStateIndex
        {
            get { EnsureNotExecuting(); return _activeCount > 0 ? _active[_activeCount - 1] : -1; }
        }

        internal HfsmInstance(HfsmDefinition<TContext> definition, TContext context, int traceCapacity)
        {
            if (traceCapacity <= 0) { throw new ArgumentOutOfRangeException(nameof(traceCapacity)); }
            Definition = definition;
            _context = context;
            _traceCapacity = traceCapacity;
            _active = new int[definition.MaximumDepth];
            _destination = new int[definition.MaximumDepth];
            _states = definition.CreateStates();
        }

        public HfsmResult Start()
        {
            EnsureNotExecuting();
            if (Status == HfsmStatus.Running || Status == HfsmStatus.Faulted)
            {
                throw new InvalidOperationException("运行中不可重复启动，故障后须先 Reset");
            }
            _executing = true;
            Status = HfsmStatus.Running;
            try
            {
                int count = PreparePath(Definition.RootIndex);
                EnterPath(0, count);
            }
            catch (Exception error) { Fail(error); }
            finally { _executing = false; }
            return Result();
        }

        public HfsmResult Tick(float deltaTime)
        {
            EnsureNotExecuting();
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }
            if (Status != HfsmStatus.Running || deltaTime == 0) { return Result(); }
            _executing = true;
            try
            {
                TickSequence = checked(TickSequence + 1);
                ElapsedSeconds += deltaTime;
                for (int order = 0; order < Definition.Transitions.Count; order++)
                {
                    int index = Definition.OrderedTransition(order);
                    HfsmTransitionInfo transition = Definition.Transitions[index];
                    if (!IsActive(transition.SourceIndex)) { continue; }
                    bool passed = Definition.Evaluate(index, _context);
                    if (DiagnosticsEnabled) { _guards[index] = new HfsmGuardObservation(TickSequence, passed); }
                    if (!passed) { continue; }
                    if (Change(index, transition)) { break; }
                }
                for (int i = 0; i < _activeCount; i++) { _states[_active[i]].Tick(_context, deltaTime); }
            }
            catch (Exception error) { Fail(error); }
            finally { _executing = false; }
            return Result();
        }

        public HfsmResult Stop()
        {
            EnsureNotExecuting();
            if (Status == HfsmStatus.Faulted || Status == HfsmStatus.Stopped) { return Result(); }
            _executing = true;
            try
            {
                while (_activeCount > 0) { ExitLeaf(HfsmExitReason.Stopped); }
                Status = HfsmStatus.Stopped;
            }
            catch (Exception error) { Fail(error); }
            finally { _executing = false; }
            return Result();
        }

        public void Reset()
        {
            EnsureNotExecuting();
            if (Status != HfsmStatus.Stopped && Status != HfsmStatus.Faulted)
            {
                throw new InvalidOperationException("只有停止或故障实例可以 Reset");
            }
            if (Fault != null && !Fault.CleanupSucceeded)
            {
                throw new InvalidOperationException("退出回调清理失败，所属作用域须先处理资源，不能重用此实例");
            }
            _executing = true;
            try
            {
                // 工厂只构造处理器；资源必须在 Enter 中取得，才能配对 Exit 清理。
                _states = Definition.CreateStates();
                Status = HfsmStatus.NotStarted;
                Fault = null;
                TickSequence = 0;
                ElapsedSeconds = 0;
                TransitionCount = 0;
                LastTransitionIndex = -1;
                ClearDiagnostics();
            }
            finally { _executing = false; }
        }

        public int CopyActivePath(Span<int> destination)
        {
            EnsureNotExecuting();
            if (destination.Length < _activeCount) { throw new ArgumentException("路径缓冲不足", nameof(destination)); }
            _active.AsSpan(0, _activeCount).CopyTo(destination);
            return _activeCount;
        }

        public IDisposable AcquireDiagnostics()
        {
            EnsureNotExecuting();
            if (_diagnosticOwners == 0)
            {
                _guards ??= new HfsmGuardObservation[Definition.Transitions.Count];
                _trace ??= new HfsmTransitionRecord[_traceCapacity];
                ClearDiagnostics();
            }
            _diagnosticOwners++;
            return new DiagnosticLease(this);
        }

        public int CopyGuardObservations(Span<HfsmGuardObservation> destination)
        {
            EnsureNotExecuting();
            if (!DiagnosticsEnabled) { return 0; }
            if (destination.Length < _guards.Length) { throw new ArgumentException("条件缓冲不足", nameof(destination)); }
            _guards.AsSpan().CopyTo(destination);
            return _guards.Length;
        }

        public int CopyTransitionTrace(Span<HfsmTransitionRecord> destination)
        {
            EnsureNotExecuting();
            if (!DiagnosticsEnabled) { return 0; }
            if (destination.Length < _traceCount) { throw new ArgumentException("轨迹缓冲不足", nameof(destination)); }
            int first = (_traceNext - _traceCount + _traceCapacity) % _traceCapacity;
            for (int i = 0; i < _traceCount; i++) { destination[i] = _trace[(first + i) % _traceCapacity]; }
            return _traceCount;
        }

        bool Change(int index, HfsmTransitionInfo transition)
        {
            int target = transition.TargetIndex;
            int count = PreparePath(target);
            int previousLeaf = _active[_activeCount - 1];
            bool reenter = transition.SelfTransition == HfsmSelfTransition.Reenter;
            if (!reenter && (previousLeaf == _destination[count - 1]
                || transition.SourceIndex == target && IsActive(target))) { return false; }

            int common = 0;
            while (common < _activeCount && common < count && _active[common] == _destination[common]) { common++; }
            if (reenter && IsActive(target)) { common = Math.Min(common, Definition.States[target].Depth); }
            while (_activeCount > common) { ExitLeaf(HfsmExitReason.Transition); }
            EnterPath(common, count);
            LastTransitionIndex = index;
            TransitionCount++;
            if (DiagnosticsEnabled)
            {
                _trace[_traceNext] = new HfsmTransitionRecord(TickSequence, index, previousLeaf, _active[_activeCount - 1]);
                _traceNext = (_traceNext + 1) % _traceCapacity;
                _traceCount = Math.Min(_traceCount + 1, _traceCapacity);
            }
            return true;
        }

        int PreparePath(int target)
        {
            while (Definition.States[target].InitialChildIndex >= 0) { target = Definition.States[target].InitialChildIndex; }
            int count = Definition.States[target].Depth + 1;
            for (int state = target; state >= 0; state = Definition.States[state].ParentIndex)
            {
                _destination[Definition.States[state].Depth] = state;
            }
            return count;
        }

        void EnterPath(int start, int count)
        {
            for (int i = start; i < count; i++)
            {
                int state = _destination[i];
                _active[_activeCount++] = state;
                // 即使 Enter 只执行了一半就抛错，也恰好调用一次 Exit。
                _states[state].Enter(_context);
            }
        }

        void ExitLeaf(HfsmExitReason reason)
        {
            int state = _active[--_activeCount];
            try { _states[state].Exit(_context, reason); }
            catch { _exitFailed = true; throw; }
        }

        bool IsActive(int state)
        {
            int depth = Definition.States[state].Depth;
            return depth < _activeCount && _active[depth] == state;
        }

        void Fail(Exception cause)
        {
            Status = HfsmStatus.Faulted;
            var cleanupErrors = new List<Exception>();
            if (_exitFailed) { cleanupErrors.Add(cause); }
            _exitFailed = false;
            while (_activeCount > 0)
            {
                int state = _active[--_activeCount];
                try { _states[state].Exit(_context, HfsmExitReason.Faulted); }
                catch (Exception error) { cleanupErrors.Add(error); }
            }
            Fault = new HfsmFault(cause, cleanupErrors);
        }

        void ClearDiagnostics()
        {
            if (_guards != null) { Array.Clear(_guards, 0, _guards.Length); }
            if (_trace != null) { Array.Clear(_trace, 0, _trace.Length); }
            _traceCount = 0;
            _traceNext = 0;
        }

        void EnsureNotExecuting()
        {
            if (_executing) { throw new InvalidOperationException("禁止在 HFSM 回调内重入执行或读取未提交快照"); }
        }

        HfsmResult Result()
        {
            return new HfsmResult(Status, Fault);
        }

        sealed class DiagnosticLease : IDisposable
        {
            HfsmInstance<TContext> _owner;
            internal DiagnosticLease(HfsmInstance<TContext> owner) { _owner = owner; }
            public void Dispose()
            {
                if (_owner == null) { return; }
                _owner.EnsureNotExecuting();
                if (--_owner._diagnosticOwners == 0) { _owner.ClearDiagnostics(); }
                _owner = null;
            }
        }
    }
}
