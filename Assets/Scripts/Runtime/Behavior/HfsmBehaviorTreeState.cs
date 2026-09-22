using System;
using DarkFlare.BehaviorTree;
using DarkFlare.HFSM;

namespace DarkFlare
{
    public sealed class HfsmBehaviorTreeState<TContext> : IHfsmState<TContext>
    {
        readonly Func<TContext, BehaviorTreeInstance<TContext>> _tree;
        public HfsmBehaviorTreeState(Func<TContext, BehaviorTreeInstance<TContext>> tree) { _tree = tree ?? throw new ArgumentNullException(nameof(tree)); }
        public void Enter(TContext context) { Check(_tree(context).Start()); }
        public void Tick(TContext context, float deltaTime) { Check(_tree(context).Tick(deltaTime)); }
        public void Exit(TContext context, HfsmExitReason reason) { Check(_tree(context).Abort()); }
        static void Check(TreeTickResult result)
        {
            if (result.Status == TreeStatus.Faulted) { throw new InvalidOperationException("子行为树故障", result.Fault.Cause); }
        }
    }
}
