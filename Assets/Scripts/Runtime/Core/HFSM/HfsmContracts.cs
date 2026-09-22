using System;
using System.Collections.Generic;

namespace DarkFlare.HFSM
{
    public enum HfsmStatus { NotStarted, Running, Stopped, Faulted }
    public enum HfsmExitReason { Transition, Stopped, Faulted }
    public enum HfsmSelfTransition { Ignore, Reenter }

    public interface IHfsmState<TContext>
    {
        void Enter(TContext context);
        void Tick(TContext context, float deltaTime);
        void Exit(TContext context, HfsmExitReason reason);
    }

    public sealed class HfsmStateInfo
    {
        public string Id { get; }
        public int ParentIndex { get; }
        public int InitialChildIndex { get; }
        public int Depth { get; }

        internal HfsmStateInfo(string id, int parentIndex, int initialChildIndex, int depth)
        {
            Id = id;
            ParentIndex = parentIndex;
            InitialChildIndex = initialChildIndex;
            Depth = depth;
        }
    }

    public sealed class HfsmTransitionInfo
    {
        public int SourceIndex { get; }
        public int TargetIndex { get; }
        public int Priority { get; }
        public HfsmSelfTransition SelfTransition { get; }
        public string Reason { get; }

        internal HfsmTransitionInfo(int sourceIndex, int targetIndex, int priority,
            HfsmSelfTransition selfTransition, string reason)
        {
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            Priority = priority;
            SelfTransition = selfTransition;
            Reason = reason;
        }
    }

    public sealed class HfsmFault
    {
        public Exception Cause { get; }
        public IReadOnlyList<Exception> CleanupErrors { get; }
        public bool CleanupSucceeded => CleanupErrors.Count == 0;

        internal HfsmFault(Exception cause, List<Exception> cleanupErrors)
        {
            Cause = cause;
            CleanupErrors = cleanupErrors.AsReadOnly();
        }
    }

    public readonly struct HfsmResult
    {
        public HfsmStatus Status { get; }
        public HfsmFault Fault { get; }

        internal HfsmResult(HfsmStatus status, HfsmFault fault)
        {
            Status = status;
            Fault = fault;
        }
    }

    public readonly struct HfsmGuardObservation
    {
        // 0 表示尚未采集；与实例 TickSequence 不同表示旧结果，不能当作本步判断。
        public long TickSequence { get; }
        public bool Passed { get; }

        internal HfsmGuardObservation(long tickSequence, bool passed)
        {
            TickSequence = tickSequence;
            Passed = passed;
        }
    }

    public readonly struct HfsmTransitionRecord
    {
        public long TickSequence { get; }
        public int TransitionIndex { get; }
        public int PreviousLeafIndex { get; }
        public int CurrentLeafIndex { get; }

        internal HfsmTransitionRecord(long tickSequence, int transitionIndex, int previousLeafIndex, int currentLeafIndex)
        {
            TickSequence = tickSequence;
            TransitionIndex = transitionIndex;
            PreviousLeafIndex = previousLeafIndex;
            CurrentLeafIndex = currentLeafIndex;
        }
    }
}
