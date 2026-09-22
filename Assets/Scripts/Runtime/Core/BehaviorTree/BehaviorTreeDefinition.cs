using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DarkFlare.BehaviorTree
{
    public sealed class BehaviorTreeDefinition<TContext>
    {
        static readonly ConditionalWeakTable<IBehaviorAction<TContext>, object> ClaimedActions = new ConditionalWeakTable<IBehaviorAction<TContext>, object>();
        static readonly object Claim = new object();
        readonly Func<IBehaviorAction<TContext>>[] _factories;
        readonly Predicate<TContext>[] _conditions;
        readonly Predicate<TContext>[] _gates;
        public IReadOnlyList<BehaviorNodeInfo> Nodes { get; }
        public int RootIndex { get; }
        public int VisitBudget { get; }
        public int MaximumVisits { get; }

        internal BehaviorTreeDefinition(BehaviorNodeInfo[] nodes, Func<IBehaviorAction<TContext>>[] factories,
            Predicate<TContext>[] conditions, Predicate<TContext>[] gates, int root, int budget, int maximumVisits)
        {
            Nodes = Array.AsReadOnly(nodes);
            _factories = factories;
            _conditions = conditions;
            _gates = gates;
            RootIndex = root;
            VisitBudget = budget;
            MaximumVisits = maximumVisits;
        }

        public BehaviorTreeInstance<TContext> CreateInstance(TContext context, int traceCapacity = 64)
        {
            return new BehaviorTreeInstance<TContext>(this, context, traceCapacity);
        }

        internal IBehaviorAction<TContext>[] CreateActions()
        {
            var result = new IBehaviorAction<TContext>[_factories.Length];
            for (int i = 0; i < result.Length; i++)
            {
                if (_factories[i] == null) { continue; }
                result[i] = _factories[i]() ?? throw new InvalidOperationException("动作工厂返回空");
                if (ClaimedActions.TryGetValue(result[i], out _)) { throw new InvalidOperationException("动作处理器不能跨实例重用"); }
                for (int j = 0; j < i; j++)
                {
                    if (ReferenceEquals(result[i], result[j])) { throw new InvalidOperationException("节点不能共享动作处理器"); }
                }
            }
            foreach (IBehaviorAction<TContext> action in result)
            {
                if (action != null) { ClaimedActions.Add(action, Claim); }
            }
            return result;
        }

        internal bool Evaluate(int node, TContext context, bool gate)
        {
            return (gate ? _gates[node] : _conditions[node])(context);
        }
    }
}
