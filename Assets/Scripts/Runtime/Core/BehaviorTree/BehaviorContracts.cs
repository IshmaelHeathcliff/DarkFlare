using System;
using System.Collections.Generic;

namespace DarkFlare.BehaviorTree
{
    public enum NodeStatus { Success, Failure, Running }
    public enum TreeStatus { NotStarted, Running, Success, Failure, Aborted, Faulted }
    public enum BehaviorExitReason { Success, Failure, Aborted, Faulted }
    public enum BehaviorNodeKind { Action, Condition, Wait, MemorySequence, PrioritySelector, Guard }
    public enum BehaviorNodeState { NotVisited, Running, Success, Failure, Aborted, Faulted }

    public interface IBehaviorAction<TContext>
    {
        void Enter(TContext context);
        NodeStatus Tick(TContext context, float deltaTime);
        void Exit(TContext context, BehaviorExitReason reason);
    }

    public sealed class BehaviorBranch<TContext>
    {
        public string ChildId { get; }
        public Predicate<TContext> Guard { get; }
        public BehaviorBranch(string childId, Predicate<TContext> guard)
        {
            ChildId = childId;
            Guard = guard ?? throw new ArgumentNullException(nameof(guard));
        }
    }

    public sealed class BehaviorNodeInfo
    {
        public string Id { get; }
        public BehaviorNodeKind Kind { get; }
        public int ParentIndex { get; }
        public int Depth { get; }
        public IReadOnlyList<int> Children { get; }
        public double WaitSeconds { get; }
        internal BehaviorNodeInfo(string id, BehaviorNodeKind kind, int parentIndex, int depth, int[] children, double seconds)
        {
            Id = id;
            Kind = kind;
            ParentIndex = parentIndex;
            Depth = depth;
            Children = Array.AsReadOnly(children);
            WaitSeconds = seconds;
        }
    }

    public readonly struct BehaviorNodeObservation
    {
        public BehaviorNodeState State { get; }
        public long LastVisitedTick { get; }
        public long GuardTick { get; }
        public bool GuardPassed { get; }
        public long EntryGuardTick { get; }
        public bool EntryGuardPassed { get; }
        internal BehaviorNodeObservation(BehaviorNodeState state, long visited, long guardTick, bool guardPassed,
            long entryGuardTick, bool entryGuardPassed)
        {
            State = state;
            LastVisitedTick = visited;
            GuardTick = guardTick;
            GuardPassed = guardPassed;
            EntryGuardTick = entryGuardTick;
            EntryGuardPassed = entryGuardPassed;
        }
    }

    public readonly struct BehaviorTraceRecord
    {
        public long TickSequence { get; }
        public int NodeIndex { get; }
        public BehaviorNodeState State { get; }
        internal BehaviorTraceRecord(long tick, int node, BehaviorNodeState state)
        {
            TickSequence = tick;
            NodeIndex = node;
            State = state;
        }
    }

    public sealed class BehaviorFault
    {
        public Exception Cause { get; }
        public IReadOnlyList<Exception> CleanupErrors { get; }
        public bool CleanupSucceeded => CleanupErrors.Count == 0;
        internal BehaviorFault(Exception cause, List<Exception> errors)
        {
            Cause = cause;
            CleanupErrors = errors.AsReadOnly();
        }
    }

    public readonly struct TreeTickResult
    {
        public TreeStatus Status { get; }
        public int Visits { get; }
        public BehaviorFault Fault { get; }
        internal TreeTickResult(TreeStatus status, int visits, BehaviorFault fault)
        {
            Status = status;
            Visits = visits;
            Fault = fault;
        }
    }
}
