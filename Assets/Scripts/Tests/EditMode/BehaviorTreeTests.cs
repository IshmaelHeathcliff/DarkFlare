using System;
using System.Collections.Generic;
using DarkFlare.BehaviorTree;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class BehaviorTreeTests
    {
        sealed class Context
        {
            public List<string> Calls { get; } = new List<string>();
            public bool High { get; set; }
            public bool Low { get; set; } = true;
            public bool Complete { get; set; }
            public bool Throw { get; set; }
            public int Ticks { get; set; }
        }

        sealed class ActionNode : IBehaviorAction<Context>
        {
            readonly string _name;
            readonly Func<Context, NodeStatus> _tick;
            readonly Action<Context> _enter;
            readonly Action<Context> _exit;
            readonly bool _record;
            int _ticks;
            public ActionNode(string name, Func<Context, NodeStatus> tick = null, Action<Context> enter = null,
                Action<Context> exit = null, bool record = true)
            {
                _name = name; _tick = tick; _enter = enter; _exit = exit; _record = record;
            }
            public void Enter(Context context)
            {
                _ticks = 0;
                if (_record) { context.Calls.Add("+" + _name); }
                _enter?.Invoke(context);
            }
            public NodeStatus Tick(Context context, float deltaTime)
            {
                context.Ticks = ++_ticks;
                return _tick != null ? _tick(context) : NodeStatus.Running;
            }
            public void Exit(Context context, BehaviorExitReason reason)
            {
                if (_record) { context.Calls.Add("-" + _name + ":" + reason); }
                _exit?.Invoke(context);
            }
        }

        static BehaviorTreeDefinition<Context> Choice(bool record = true)
        {
            return new BehaviorTreeBuilder<Context>()
                .AddAction("high", () => new ActionNode("high", record: record))
                .AddAction("low", () => new ActionNode("low", record: record))
                .AddPrioritySelector("root", new BehaviorBranch<Context>("high", c => c.High),
                    new BehaviorBranch<Context>("low", c => c.Low)).Build("root");
        }

        [Test]
        public void Priority_PreemptsBeforeEntering_AndLossOfAllGuardsAbortsOnce()
        {
            var context = new Context();
            BehaviorTreeInstance<Context> tree = Choice().CreateInstance(context);
            tree.Start(); tree.Tick(.1f); tree.Tick(.1f);
            CollectionAssert.AreEqual(new[] { "+low" }, context.Calls);
            context.High = true; tree.Tick(.1f);
            CollectionAssert.AreEqual(new[] { "+low", "-low:Aborted", "+high" }, context.Calls);
            context.High = false; context.Low = false;
            Assert.That(tree.Tick(.1f).Status, Is.EqualTo(TreeStatus.Failure));
            Assert.That(context.Calls[3], Is.EqualTo("-high:Aborted"));
            tree.Abort(); tree.Tick(1);
            Assert.That(context.Calls.Count, Is.EqualTo(4));
        }

        [Test]
        public void FailedHigherBranch_FallsThroughOnce_AndSequenceKeepsItsCursor()
        {
            var context = new Context();
            BehaviorTreeInstance<Context> tree = new BehaviorTreeBuilder<Context>()
                .AddAction("reject", () => new ActionNode("reject", c => NodeStatus.Failure))
                .AddAction("once", () => new ActionNode("once", c => NodeStatus.Success))
                .AddAction("long", () => new ActionNode("long", c => c.Complete ? NodeStatus.Success : NodeStatus.Running))
                .AddSequence("sequence", "once", "long")
                .AddPrioritySelector("root", new BehaviorBranch<Context>("reject", c => true),
                    new BehaviorBranch<Context>("sequence", c => true)).Build("root").CreateInstance(context);
            tree.Start(); tree.Tick(.1f); tree.Tick(.1f);
            // 反应式选择器的高优先候选成立会先中断旧分支，再尝试该候选。
            Assert.That(context.Calls.FindAll(c => c == "+reject").Count, Is.EqualTo(2));
            context.Complete = true;
            Assert.That(tree.Tick(.1f).Status, Is.EqualTo(TreeStatus.Success));

            context.Calls.Clear(); context.Complete = false;
            tree = new BehaviorTreeBuilder<Context>()
                .AddAction("once", () => new ActionNode("once", c => NodeStatus.Success))
                .AddAction("long", () => new ActionNode("long", c => c.Complete ? NodeStatus.Success : NodeStatus.Running))
                .AddSequence("root", "once", "long").Build("root").CreateInstance(context);
            tree.Start(); tree.Tick(.1f); tree.Tick(.1f);
            Assert.That(context.Calls.FindAll(c => c == "+once").Count, Is.EqualTo(1));
            context.Complete = true; tree.Tick(.1f);
            int count = context.Calls.Count;
            tree.Tick(10);
            Assert.That(context.Calls.Count, Is.EqualTo(count));
            tree.Start(); tree.Tick(.1f);
            Assert.That(context.Calls.Count, Is.GreaterThan(count));
        }

        [Test]
        public void GuardDecorator_RechecksAndAbortsChild()
        {
            var context = new Context { High = true };
            BehaviorTreeInstance<Context> tree = new BehaviorTreeBuilder<Context>()
                .AddAction("action", () => new ActionNode("action"))
                .AddGuard("root", "action", c => c.High).Build("root").CreateInstance(context);
            tree.Start(); tree.Tick(.1f); context.High = false;
            Assert.That(tree.Tick(.1f).Status, Is.EqualTo(TreeStatus.Failure));
            CollectionAssert.AreEqual(new[] { "+action", "-action:Aborted" }, context.Calls);
        }

        [Test]
        public void Waits_ShareOneClock_AndNewWaitDoesNotConsumePreviousDelta()
        {
            BehaviorTreeInstance<Context> tree = new BehaviorTreeBuilder<Context>()
                .AddWait("one", 1).AddWait("two", 1).AddSequence("root", "one", "two")
                .Build("root").CreateInstance(new Context());
            tree.Start();
            Assert.That(tree.Tick(5).Status, Is.EqualTo(TreeStatus.Running));
            Assert.That(tree.Tick(5).Status, Is.EqualTo(TreeStatus.Running));
            Assert.That(tree.Tick(0).Status, Is.EqualTo(TreeStatus.Running));
            Assert.That(tree.Tick(1).Status, Is.EqualTo(TreeStatus.Success));
            Assert.That(tree.ElapsedSeconds, Is.EqualTo(11));
            Assert.That(tree.TickSequence, Is.EqualTo(3));
        }

        [Test]
        public void Lifecycle_RejectsInvalidTime_AbortIsIdempotent_AndInstancesAreIsolated()
        {
            BehaviorTreeDefinition<Context> definition = Choice();
            var a = new Context(); var b = new Context { High = true };
            BehaviorTreeInstance<Context> first = definition.CreateInstance(a);
            BehaviorTreeInstance<Context> second = definition.CreateInstance(b);
            first.Tick(1); Assert.That(a.Calls, Is.Empty);
            first.Start(); second.Start();
            Assert.Throws<InvalidOperationException>(() => first.Start());
            Assert.Throws<InvalidOperationException>(() => first.Reset());
            foreach (float dt in new[] { -1, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => first.Tick(dt));
            }
            first.Tick(.1f); first.Tick(.1f); second.Tick(.1f);
            Assert.That(a.Ticks, Is.EqualTo(2)); Assert.That(b.Ticks, Is.EqualTo(1));
            first.Abort(); first.Abort();
            Assert.That(a.Calls.Count, Is.EqualTo(2)); Assert.That(b.Calls.Count, Is.EqualTo(1));
            first.Reset(); Assert.That(first.TickSequence, Is.Zero);
            first.Start(); first.Tick(.1f); Assert.That(a.Ticks, Is.EqualTo(1));
        }

        [TestCase("enter")]
        [TestCase("tick")]
        [TestCase("exit")]
        [TestCase("guard")]
        [TestCase("invalid-result")]
        public void CallbackFailure_StopsAndCleansExactlyOnce(string phase)
        {
            var context = new Context { Throw = true };
            Action<Context> fail = c => { if (c.Throw) { throw new Exception("failure"); } };
            BehaviorTreeInstance<Context> tree = new BehaviorTreeBuilder<Context>()
                .AddAction("action", () => new ActionNode("action", c =>
                {
                    if (phase == "tick") { fail(c); }
                    return phase == "invalid-result" && c.Throw ? (NodeStatus)99 : phase == "exit" ? NodeStatus.Success : NodeStatus.Running;
                }, phase == "enter" ? fail : null, phase == "exit" ? fail : null))
                .AddGuard("root", "action", c => { if (phase == "guard" && c.Ticks > 0) { fail(c); } return true; })
                .Build("root").CreateInstance(context);
            tree.Start(); tree.Tick(.1f);
            if (phase == "guard") { tree.Tick(.1f); }
            Assert.That(tree.Status, Is.EqualTo(TreeStatus.Faulted));
            Assert.That(context.Calls.Count, Is.EqualTo(2));
            tree.Abort(); tree.Tick(1); Assert.That(context.Calls.Count, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => tree.Start());
            if (phase == "exit") { Assert.Throws<InvalidOperationException>(() => tree.Reset()); }
            else { context.Throw = false; tree.Reset(); tree.Start(); Assert.That(tree.Tick(.1f).Status, Is.EqualTo(TreeStatus.Running)); }
        }

        [Test]
        public void RuntimeBudgetFault_CleansRunningBranchEvenWhenNoVisitRemains()
        {
            var context = new Context();
            BehaviorTreeInstance<Context> tree = Choice().CreateInstance(context);
            tree.Start(); tree.Tick(.1f);
            TreeTickResult result = tree.Tick(.1f, visitBudget: 1);
            Assert.That(result.Status, Is.EqualTo(TreeStatus.Faulted));
            Assert.That(result.Visits, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "+low", "-low:Faulted" }, context.Calls);
            Assert.That(result.Fault.CleanupSucceeded, Is.True);
        }

        [TestCase("tick")]
        [TestCase("abort")]
        [TestCase("snapshot")]
        public void Reentry_IsFaulted(string operation)
        {
            BehaviorTreeInstance<Context> tree = null;
            tree = new BehaviorTreeBuilder<Context>().AddAction("root", () => new ActionNode("root", c =>
            {
                if (operation == "tick") { tree.Tick(.1f); }
                else if (operation == "abort") { tree.Abort(); }
                else { tree.CopyObservations(new BehaviorNodeObservation[1]); }
                return NodeStatus.Running;
            })).Build("root").CreateInstance(new Context());
            tree.Start(); Assert.That(tree.Tick(.1f).Status, Is.EqualTo(TreeStatus.Faulted));
        }

        [TestCase("duplicate")]
        [TestCase("cycle")]
        [TestCase("shared")]
        [TestCase("unknown")]
        [TestCase("unreachable")]
        [TestCase("budget")]
        [TestCase("depth")]
        public void InvalidTopology_IsRejected(string kind)
        {
            var builder = new BehaviorTreeBuilder<Context>(maxDepth: kind == "depth" ? 1 : 32, visitBudget: kind == "budget" ? 1 : 100);
            builder.AddWait("leaf", 1);
            switch (kind)
            {
                case "duplicate": builder.AddWait("leaf", 1).AddSequence("root", "leaf"); break;
                case "cycle": builder.AddSequence("root", "leaf").AddSequence("a", "b").AddSequence("b", "a"); break;
                case "shared": builder.AddSequence("root", "leaf", "leaf"); break;
                case "unknown": builder.AddSequence("root", "missing"); break;
                case "unreachable": builder.AddWait("root", 1); break;
                default: builder.AddSequence("root", "leaf"); break;
            }
            Assert.Throws<InvalidOperationException>(() => builder.Build("root"));
        }

        [Test]
        public void Definition_IsFrozen_AndFactoriesCannotShareMutableActions()
        {
            var children = new[] { "leaf" };
            var builder = new BehaviorTreeBuilder<Context>().AddWait("leaf", 1).AddSequence("root", children);
            children[0] = "missing";
            BehaviorTreeDefinition<Context> definition = builder.Build("root");
            builder.AddWait("extra", 1);
            Assert.That(definition.Nodes.Count, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<int>)definition.Nodes[1].Children).Clear());
            Assert.Throws<ArgumentException>(() => new BehaviorTreeBuilder<Context>().AddSequence("empty"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BehaviorTreeBuilder<Context>().AddWait("bad", double.NaN));
            var shared = new ActionNode("shared");
            definition = new BehaviorTreeBuilder<Context>().AddAction("root", () => shared).Build("root");
            definition.CreateInstance(new Context());
            Assert.Throws<InvalidOperationException>(() => definition.CreateInstance(new Context()));
        }

        [Test]
        public void Diagnostics_PreserveDistinctEntryAndNodeGuards_WithoutReevaluation()
        {
            int evaluations = 0;
            BehaviorTreeInstance<Context> tree = new BehaviorTreeBuilder<Context>()
                .AddCondition("condition", c => { evaluations++; return false; })
                .AddWait("wait", 1)
                .AddPrioritySelector("root", new BehaviorBranch<Context>("condition", c => { evaluations++; return true; }),
                    new BehaviorBranch<Context>("wait", c => true)).Build("root").CreateInstance(new Context(), 2);
            tree.Start();
            IDisposable first = tree.AcquireDiagnostics(); IDisposable second = tree.AcquireDiagnostics();
            tree.Tick(.1f);
            var observations = new BehaviorNodeObservation[3]; tree.CopyObservations(observations);
            Assert.That(observations[0].EntryGuardPassed, Is.True); Assert.That(observations[0].GuardPassed, Is.False);
            Assert.That(observations[0].GuardTick, Is.EqualTo(1)); Assert.That(observations[0].EntryGuardTick, Is.EqualTo(1));
            Assert.That(tree.CopyTrace(new BehaviorTraceRecord[2]), Is.EqualTo(2));
            Assert.That(evaluations, Is.EqualTo(2));
            first.Dispose(); first.Dispose(); Assert.That(tree.DiagnosticsEnabled, Is.True);
            second.Dispose(); Assert.That(tree.DiagnosticsEnabled, Is.False);
            using (tree.AcquireDiagnostics()) { Assert.That(tree.CopyTrace(new BehaviorTraceRecord[2]), Is.Zero); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WarmTickAndPreemption_DoNotAllocate(bool diagnostics)
        {
            var context = new Context();
            BehaviorTreeInstance<Context> tree = Choice(false).CreateInstance(context);
            using (diagnostics ? tree.AcquireDiagnostics() : null)
            {
                tree.Start();
                for (int i = 0; i < 1000; i++) { context.High = !context.High; tree.Tick(.02f); }
                bool previous = UnityEngine.Profiling.Profiler.enabled;
                UnityEngine.Profiling.Profiler.enabled = true;
                try
                {
                    using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,
                        "GC.Alloc", 1024, Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                    var probe = new byte[512]; recorder.Stop(); GC.KeepAlive(probe);
                    Assert.That(recorder.Valid && recorder.Count > 0, Is.True);
                    recorder.Reset(); recorder.Start();
                    for (int i = 0; i < 10000; i++) { context.High = !context.High; tree.Tick(.02f); }
                    recorder.Stop(); Assert.That(recorder.Count, Is.Zero);
                }
                finally { UnityEngine.Profiling.Profiler.enabled = previous; }
                Assert.That(tree.Status, Is.EqualTo(TreeStatus.Running));
            }
        }
    }
}
