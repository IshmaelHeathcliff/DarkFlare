using System;
using System.Collections.Generic;

namespace DarkFlare.HFSM
{
    public sealed class HfsmBuilder<TContext>
    {
        readonly List<StateSpec> _states = new List<StateSpec>();
        readonly List<TransitionSpec> _transitions = new List<TransitionSpec>();
        readonly Dictionary<string, string> _initialChildren = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly int _maxStates;
        readonly int _maxTransitions;
        readonly int _maxDepth;

        sealed class StateSpec
        {
            internal string Id { get; }
            internal string ParentId { get; }
            internal Func<IHfsmState<TContext>> Factory { get; }

            internal StateSpec(string id, string parentId, Func<IHfsmState<TContext>> factory)
            {
                Id = id;
                ParentId = parentId;
                Factory = factory;
            }
        }

        sealed class TransitionSpec
        {
            internal string Source { get; }
            internal string Target { get; }
            internal Predicate<TContext> Guard { get; }
            internal int Priority { get; }
            internal HfsmSelfTransition SelfTransition { get; }
            internal string Reason { get; }

            internal TransitionSpec(string source, string target, Predicate<TContext> guard, int priority,
                HfsmSelfTransition selfTransition, string reason)
            {
                Source = source;
                Target = target;
                Guard = guard;
                Priority = priority;
                SelfTransition = selfTransition;
                Reason = reason;
            }
        }

        public HfsmBuilder(int maxStates = 256, int maxTransitions = 1024, int maxDepth = 32)
        {
            if (maxStates <= 0 || maxTransitions <= 0 || maxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStates), "HFSM 容量必须为正数");
            }
            _maxStates = maxStates;
            _maxTransitions = maxTransitions;
            _maxDepth = maxDepth;
        }

        public HfsmBuilder<TContext> AddState(string id, Func<IHfsmState<TContext>> factory, string parentId = null)
        {
            RequireId(id);
            if (parentId != null) { RequireId(parentId); }
            if (_states.Count >= _maxStates) { throw new InvalidOperationException("HFSM 状态容量超限"); }
            _states.Add(new StateSpec(id, parentId, factory ?? throw new ArgumentNullException(nameof(factory))));
            return this;
        }

        public HfsmBuilder<TContext> SetInitialChild(string parentId, string childId)
        {
            RequireId(parentId);
            RequireId(childId);
            _initialChildren.Add(parentId, childId);
            return this;
        }

        public HfsmBuilder<TContext> AddTransition(string sourceId, string targetId, Predicate<TContext> guard,
            int priority = 0, HfsmSelfTransition selfTransition = HfsmSelfTransition.Ignore, string reason = "")
        {
            RequireId(sourceId);
            RequireId(targetId);
            if (selfTransition != HfsmSelfTransition.Ignore && selfTransition != HfsmSelfTransition.Reenter)
            {
                throw new ArgumentOutOfRangeException(nameof(selfTransition));
            }
            if (_transitions.Count >= _maxTransitions) { throw new InvalidOperationException("HFSM 转移容量超限"); }
            _transitions.Add(new TransitionSpec(sourceId, targetId, guard ?? throw new ArgumentNullException(nameof(guard)),
                priority, selfTransition, reason ?? string.Empty));
            return this;
        }

        public HfsmDefinition<TContext> Build()
        {
            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _states.Count; i++)
            {
                if (!indices.TryAdd(_states[i].Id, i)) { throw new InvalidOperationException("重复状态 ID: " + _states[i].Id); }
            }
            var parents = new int[_states.Count];
            var initial = new int[_states.Count];
            var childCounts = new int[_states.Count];
            int root = -1;
            for (int i = 0; i < _states.Count; i++)
            {
                initial[i] = -1;
                parents[i] = _states[i].ParentId == null ? -1 : Resolve(indices, _states[i].ParentId);
                if (parents[i] >= 0) { childCounts[parents[i]]++; }
                else if (root >= 0) { throw new InvalidOperationException("HFSM 只能有一个根状态"); }
                else { root = i; }
            }
            if (root < 0) { throw new InvalidOperationException("HFSM 缺少根状态"); }
            foreach (KeyValuePair<string, string> pair in _initialChildren)
            {
                int parent = Resolve(indices, pair.Key);
                int child = Resolve(indices, pair.Value);
                if (parents[child] != parent) { throw new InvalidOperationException("初始状态必须是直接子状态: " + pair.Key); }
                initial[parent] = child;
            }
            var states = new HfsmStateInfo[_states.Count];
            var factories = new Func<IHfsmState<TContext>>[_states.Count];
            int maximumDepth = 0;
            for (int i = 0; i < states.Length; i++)
            {
                int depth = 0;
                for (int ancestor = parents[i]; ancestor >= 0; ancestor = parents[ancestor])
                {
                    if (++depth >= states.Length) { throw new InvalidOperationException("HFSM 父子关系成环"); }
                }
                if (depth >= _maxDepth) { throw new InvalidOperationException("HFSM 层级深度超限"); }
                if (childCounts[i] > 0 && initial[i] < 0)
                {
                    throw new InvalidOperationException("复合状态缺少初始子状态: " + _states[i].Id);
                }
                maximumDepth = Math.Max(maximumDepth, depth + 1);
                states[i] = new HfsmStateInfo(_states[i].Id, parents[i], initial[i], depth);
                factories[i] = _states[i].Factory;
            }
            var transitions = new HfsmTransitionInfo[_transitions.Count];
            var guards = new Predicate<TContext>[_transitions.Count];
            for (int i = 0; i < transitions.Length; i++)
            {
                TransitionSpec spec = _transitions[i];
                transitions[i] = new HfsmTransitionInfo(Resolve(indices, spec.Source), Resolve(indices, spec.Target),
                    spec.Priority, spec.SelfTransition, spec.Reason);
                guards[i] = spec.Guard;
            }
            ValidateReachability(root, states, transitions);
            var order = new int[transitions.Length];
            for (int i = 0; i < order.Length; i++) { order[i] = i; }
            Array.Sort(order, (left, right) =>
            {
                int priority = transitions[right].Priority.CompareTo(transitions[left].Priority);
                if (priority != 0) { return priority; }
                int depth = states[transitions[left].SourceIndex].Depth.CompareTo(states[transitions[right].SourceIndex].Depth);
                return depth != 0 ? depth : left.CompareTo(right);
            });
            return new HfsmDefinition<TContext>(states, transitions, factories, guards, order, root, maximumDepth);
        }

        static void ValidateReachability(int root, HfsmStateInfo[] states, HfsmTransitionInfo[] transitions)
        {
            var reachable = new bool[states.Length];
            MarkPath(root, states, reachable);
            bool changed;
            do
            {
                changed = false;
                foreach (HfsmTransitionInfo transition in transitions)
                {
                    if (reachable[transition.SourceIndex]) { changed |= MarkPath(transition.TargetIndex, states, reachable); }
                }
            } while (changed);
            for (int i = 0; i < states.Length; i++)
            {
                if (!reachable[i]) { throw new InvalidOperationException("不可达状态: " + states[i].Id); }
            }
        }

        static bool MarkPath(int target, HfsmStateInfo[] states, bool[] reachable)
        {
            while (states[target].InitialChildIndex >= 0) { target = states[target].InitialChildIndex; }
            bool changed = false;
            for (int state = target; state >= 0; state = states[state].ParentIndex)
            {
                changed |= !reachable[state];
                reachable[state] = true;
            }
            return changed;
        }

        static int Resolve(Dictionary<string, int> indices, string id)
        {
            return indices.TryGetValue(id, out int index) ? index : throw new InvalidOperationException("未登记状态: " + id);
        }

        static void RequireId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) { throw new ArgumentException("状态 ID 不能为空", nameof(id)); }
        }
    }
}
