using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DarkFlare.HFSM
{
    public sealed class HfsmDefinition<TContext>
    {
        // 弱键只拒绝错误工厂重复交付同一处理器，不保留实例、上下文或场景所有者。
        static readonly ConditionalWeakTable<IHfsmState<TContext>, object> ClaimedStates = new ConditionalWeakTable<IHfsmState<TContext>, object>();
        static readonly object Claim = new object();
        readonly Func<IHfsmState<TContext>>[] _factories;
        readonly Predicate<TContext>[] _guards;
        readonly int[] _transitionOrder;

        public IReadOnlyList<HfsmStateInfo> States { get; }
        public IReadOnlyList<HfsmTransitionInfo> Transitions { get; }
        public int RootIndex { get; }
        public int MaximumDepth { get; }

        internal HfsmDefinition(HfsmStateInfo[] states, HfsmTransitionInfo[] transitions,
            Func<IHfsmState<TContext>>[] factories, Predicate<TContext>[] guards, int[] transitionOrder,
            int rootIndex, int maximumDepth)
        {
            States = Array.AsReadOnly(states);
            Transitions = Array.AsReadOnly(transitions);
            _factories = factories;
            _guards = guards;
            _transitionOrder = transitionOrder;
            RootIndex = rootIndex;
            MaximumDepth = maximumDepth;
        }

        public HfsmInstance<TContext> CreateInstance(TContext context, int traceCapacity = 64)
        {
            return new HfsmInstance<TContext>(this, context, traceCapacity);
        }

        public int FindState(string id)
        {
            for (int i = 0; i < States.Count; i++)
            {
                if (string.Equals(States[i].Id, id, StringComparison.Ordinal)) { return i; }
            }
            return -1;
        }

        internal IHfsmState<TContext>[] CreateStates()
        {
            var result = new IHfsmState<TContext>[_factories.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = _factories[i]() ?? throw new InvalidOperationException("状态工厂返回空: " + States[i].Id);
                for (int j = 0; j < i; j++)
                {
                    if (ReferenceEquals(result[i], result[j])) { throw new InvalidOperationException("不同状态不得共享同一处理器"); }
                }
            }
            foreach (IHfsmState<TContext> state in result)
            {
                if (ClaimedStates.TryGetValue(state, out _))
                {
                    throw new InvalidOperationException("状态工厂必须为每个运行实例创建独立处理器");
                }
            }
            foreach (IHfsmState<TContext> state in result) { ClaimedStates.Add(state, Claim); }
            return result;
        }

        internal int OrderedTransition(int index)
        {
            return _transitionOrder[index];
        }

        internal bool Evaluate(int transitionIndex, TContext context)
        {
            return _guards[transitionIndex](context);
        }
    }
}
