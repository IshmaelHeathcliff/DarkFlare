using System;
using System.Collections.Generic;

namespace DarkFlare.BehaviorTree
{
    public sealed class BehaviorTreeBuilder<TContext>
    {
        sealed class Spec
        {
            internal string Id { get; set; }
            internal BehaviorNodeKind Kind { get; set; }
            internal string[] Children { get; set; }
            internal Predicate<TContext>[] Gates { get; set; }
            internal Predicate<TContext> Condition { get; set; }
            internal Func<IBehaviorAction<TContext>> Factory { get; set; }
            internal double Seconds { get; set; }
        }

        readonly List<Spec> _specs = new List<Spec>();
        readonly int _maxNodes;
        readonly int _maxDepth;
        readonly int _visitBudget;

        public BehaviorTreeBuilder(int maxNodes = 256, int maxDepth = 32, int visitBudget = 1024)
        {
            if (maxNodes <= 0 || maxDepth <= 0 || visitBudget <= 0) { throw new ArgumentOutOfRangeException(nameof(maxNodes)); }
            _maxNodes = maxNodes;
            _maxDepth = maxDepth;
            _visitBudget = visitBudget;
        }

        public BehaviorTreeBuilder<TContext> AddAction(string id, Func<IBehaviorAction<TContext>> factory)
        {
            return Add(new Spec { Id = id, Kind = BehaviorNodeKind.Action, Factory = factory ?? throw new ArgumentNullException(nameof(factory)) });
        }

        public BehaviorTreeBuilder<TContext> AddCondition(string id, Predicate<TContext> condition)
        {
            return Add(new Spec { Id = id, Kind = BehaviorNodeKind.Condition, Condition = condition ?? throw new ArgumentNullException(nameof(condition)) });
        }

        public BehaviorTreeBuilder<TContext> AddWait(string id, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) { throw new ArgumentOutOfRangeException(nameof(seconds)); }
            return Add(new Spec { Id = id, Kind = BehaviorNodeKind.Wait, Seconds = seconds });
        }

        public BehaviorTreeBuilder<TContext> AddSequence(string id, params string[] children)
        {
            return Add(new Spec { Id = id, Kind = BehaviorNodeKind.MemorySequence, Children = CopyChildren(children) });
        }

        public BehaviorTreeBuilder<TContext> AddPrioritySelector(string id, params BehaviorBranch<TContext>[] branches)
        {
            if (branches == null || branches.Length == 0) { throw new ArgumentException("选择器需要至少一个分支", nameof(branches)); }
            var children = new string[branches.Length];
            var gates = new Predicate<TContext>[branches.Length];
            for (int i = 0; i < branches.Length; i++)
            {
                if (branches[i] == null) { throw new ArgumentException("分支不能为空", nameof(branches)); }
                children[i] = branches[i].ChildId;
                gates[i] = branches[i].Guard;
            }
            return Add(new Spec { Id = id, Kind = BehaviorNodeKind.PrioritySelector, Children = children, Gates = gates });
        }

        public BehaviorTreeBuilder<TContext> AddGuard(string id, string child, Predicate<TContext> condition)
        {
            return Add(new Spec { Id = id, Kind = BehaviorNodeKind.Guard, Children = new[] { child },
                Condition = condition ?? throw new ArgumentNullException(nameof(condition)) });
        }

        BehaviorTreeBuilder<TContext> Add(Spec spec)
        {
            if (string.IsNullOrWhiteSpace(spec.Id)) { throw new ArgumentException("节点 ID 不能为空"); }
            if (_specs.Count >= _maxNodes) { throw new InvalidOperationException("行为树节点容量超限"); }
            spec.Children ??= Array.Empty<string>();
            foreach (string child in spec.Children)
            {
                if (string.IsNullOrWhiteSpace(child)) { throw new ArgumentException("子节点 ID 不能为空"); }
            }
            _specs.Add(spec);
            return this;
        }

        static string[] CopyChildren(string[] children)
        {
            if (children == null || children.Length == 0) { throw new ArgumentException("组合节点不能为空", nameof(children)); }
            return (string[])children.Clone();
        }

        public BehaviorTreeDefinition<TContext> Build(string rootId)
        {
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _specs.Count; i++)
            {
                if (!ids.TryAdd(_specs[i].Id, i)) { throw new InvalidOperationException("重复节点: " + _specs[i].Id); }
            }
            if (rootId == null || !ids.TryGetValue(rootId, out int root)) { throw new InvalidOperationException("缺少根节点"); }
            var parents = new int[_specs.Count];
            var children = new int[_specs.Count][];
            var gates = new Predicate<TContext>[_specs.Count];
            Array.Fill(parents, -1);
            int visits = _specs.Count;
            for (int i = 0; i < _specs.Count; i++)
            {
                Spec spec = _specs[i];
                children[i] = new int[spec.Children.Length];
                for (int j = 0; j < children[i].Length; j++)
                {
                    if (!ids.TryGetValue(spec.Children[j], out int child)) { throw new InvalidOperationException("未知子节点: " + spec.Children[j]); }
                    if (parents[child] >= 0) { throw new InvalidOperationException("运行节点不允许多个父边"); }
                    parents[child] = i;
                    children[i][j] = child;
                    if (spec.Gates != null) { gates[child] = spec.Gates[j]; visits++; }
                }
            }
            if (parents[root] >= 0) { throw new InvalidOperationException("根不能有父节点"); }
            if (visits > _visitBudget) { throw new InvalidOperationException("拓扑最坏访问量超过预算"); }
            var nodes = new BehaviorNodeInfo[_specs.Count];
            var factories = new Func<IBehaviorAction<TContext>>[_specs.Count];
            var conditions = new Predicate<TContext>[_specs.Count];
            for (int i = 0; i < nodes.Length; i++)
            {
                int depth = 0;
                int ancestor = i;
                while (parents[ancestor] >= 0)
                {
                    ancestor = parents[ancestor];
                    if (++depth >= nodes.Length) { throw new InvalidOperationException("行为树成环"); }
                }
                if (ancestor != root) { throw new InvalidOperationException("存在不可达节点"); }
                if (depth >= _maxDepth) { throw new InvalidOperationException("行为树深度超限"); }
                Spec spec = _specs[i];
                nodes[i] = new BehaviorNodeInfo(spec.Id, spec.Kind, parents[i], depth, children[i], spec.Seconds);
                factories[i] = spec.Factory;
                conditions[i] = spec.Condition;
            }
            return new BehaviorTreeDefinition<TContext>(nodes, factories, conditions, gates, root, _visitBudget, visits);
        }
    }
}
