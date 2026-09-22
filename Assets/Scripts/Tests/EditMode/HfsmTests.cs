using System;
using System.Collections.Generic;
using DarkFlare.HFSM;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class HfsmTests
    {
        sealed class Context
        {
            public List<string> Calls { get; } = new List<string>();
            public bool Go { get; set; }
            public bool Die { get; set; }
            public bool Throw { get; set; }
            public int GuardCalls { get; set; }
            public int LocalTicks { get; set; }
        }

        sealed class State : IHfsmState<Context>
        {
            readonly string _id;
            readonly Action<Context> _enter;
            readonly Action<Context> _tick;
            readonly Action<Context> _exit;
            readonly bool _record;
            int _ticks;

            public State(string id, Action<Context> enter = null, Action<Context> tick = null,
                Action<Context> exit = null, bool record = true)
            {
                _id = id;
                _enter = enter;
                _tick = tick;
                _exit = exit;
                _record = record;
            }

            public void Enter(Context context)
            {
                if (_record) { context.Calls.Add("+" + _id); }
                _enter?.Invoke(context);
            }

            public void Tick(Context context, float deltaTime)
            {
                context.LocalTicks = ++_ticks;
                if (_record) { context.Calls.Add("tick:" + _id); }
                _tick?.Invoke(context);
            }

            public void Exit(Context context, HfsmExitReason reason)
            {
                if (_record) { context.Calls.Add("-" + _id + ":" + reason); }
                _exit?.Invoke(context);
            }
        }

        static HfsmBuilder<Context> Basic()
        {
            return new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("idle", () => new State("idle"), "root")
                .AddState("work", () => new State("work"), "root")
                .SetInitialChild("root", "idle")
                .AddTransition("idle", "work", c => c.Go, reason: "start work")
                .AddTransition("work", "idle", c => !c.Go, reason: "return idle");
        }

        static string Current(HfsmInstance<Context> machine)
        {
            return machine.CurrentStateIndex < 0 ? null : machine.Definition.States[machine.CurrentStateIndex].Id;
        }

        [Test]
        public void SiblingTransition_KeepsParent_AndTicksNewPathOnce()
        {
            var context = new Context();
            HfsmInstance<Context> machine = Basic().Build().CreateInstance(context);
            Assert.That(machine.Start().Status, Is.EqualTo(HfsmStatus.Running));
            context.Go = true;
            machine.Tick(.1f);
            CollectionAssert.AreEqual(new[] { "+root", "+idle", "-idle:Transition", "+work", "tick:root", "tick:work" }, context.Calls);
            Assert.That(machine.TransitionCount, Is.EqualTo(1));
            Assert.That(machine.Definition.Transitions[machine.LastTransitionIndex].Reason, Is.EqualTo("start work"));
            machine.Stop();
            CollectionAssert.AreEqual(new[] { "-work:Stopped", "-root:Stopped" }, context.Calls.GetRange(6, 2));
        }

        [Test]
        public void CrossBranchTransition_ExitsLeafToAncestor_AndEntersTargetInitialChain()
        {
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("alive", () => new State("alive"), "root")
                .AddState("idle", () => new State("idle"), "alive")
                .AddState("dead", () => new State("dead"), "root")
                .AddState("cleanup", () => new State("cleanup"), "dead")
                .SetInitialChild("root", "alive").SetInitialChild("alive", "idle").SetInitialChild("dead", "cleanup")
                .AddTransition("alive", "dead", c => c.Die, 100);
            var context = new Context { Die = true };
            HfsmInstance<Context> machine = builder.Build().CreateInstance(context);
            machine.Start();
            context.Calls.Clear();
            machine.Tick(.1f);
            CollectionAssert.AreEqual(new[] { "-idle:Transition", "-alive:Transition", "+dead", "+cleanup", "tick:root", "tick:dead", "tick:cleanup" }, context.Calls);
        }

        [TestCase(0, "parent")]
        [TestCase(1, "child")]
        public void Priority_IsExplicit_ThenParentDepth_ThenDeclarationOrder(int childPriority, string expected)
        {
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("a", () => new State("a"), "root")
                .AddState("parent", () => new State("parent"), "root")
                .AddState("child", () => new State("child"), "root")
                .AddState("later", () => new State("later"), "root")
                .SetInitialChild("root", "a")
                .AddTransition("a", "child", c => true, childPriority)
                .AddTransition("root", "parent", c => true)
                .AddTransition("root", "later", c => true);
            HfsmInstance<Context> machine = builder.Build().CreateInstance(new Context());
            machine.Start();
            machine.Tick(.1f);
            Assert.That(Current(machine), Is.EqualTo(expected));
        }

        [Test]
        public void IgnoredSelfTransition_DoesNotStarveOtherCandidate()
        {
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("a", () => new State("a"), "root")
                .AddState("b", () => new State("b"), "root")
                .SetInitialChild("root", "a")
                .AddTransition("a", "a", c => true)
                .AddTransition("a", "b", c => true);
            var context = new Context();
            HfsmInstance<Context> machine = builder.Build().CreateInstance(context);
            machine.Start();
            machine.Tick(.1f);
            Assert.That(Current(machine), Is.EqualTo("b"));
            Assert.That(machine.TransitionCount, Is.EqualTo(1));
            Assert.That(context.Calls.FindAll(c => c == "+a").Count, Is.EqualTo(1));
        }

        [TestCase("a", "+root,+group,+a,-a:Transition,+a,tick:root,tick:group,tick:a")]
        [TestCase("group", "+root,+group,+a,-a:Transition,-group:Transition,+group,+a,tick:root,tick:group,tick:a")]
        public void ExplicitReenter_ExitsOnlyRequestedSubtree(string target, string expected)
        {
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("group", () => new State("group"), "root")
                .AddState("a", () => new State("a"), "group")
                .SetInitialChild("root", "group").SetInitialChild("group", "a")
                .AddTransition(target, target, c => true, selfTransition: HfsmSelfTransition.Reenter);
            var context = new Context();
            HfsmInstance<Context> machine = builder.Build().CreateInstance(context);
            machine.Start();
            machine.Tick(.1f);
            Assert.That(string.Join(",", context.Calls), Is.EqualTo(expected));
        }

        [Test]
        public void OneTransitionPerTick_AndTickSideEffectsWaitUntilNextTick()
        {
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root", tick: c => c.Go = true))
                .AddState("a", () => new State("a"), "root")
                .AddState("b", () => new State("b"), "root")
                .AddState("c", () => new State("c"), "root")
                .SetInitialChild("root", "a")
                .AddTransition("a", "b", c => c.Go).AddTransition("b", "c", c => true);
            HfsmInstance<Context> machine = builder.Build().CreateInstance(new Context());
            machine.Start();
            machine.Tick(.1f);
            Assert.That(Current(machine), Is.EqualTo("a"));
            machine.Tick(.1f);
            Assert.That(Current(machine), Is.EqualTo("b"));
            machine.Tick(.1f);
            Assert.That(Current(machine), Is.EqualTo("c"));
        }

        [Test]
        public void StoppedAndZeroTicks_DoNotExecute_AndInvalidTimeLeavesStateUntouched()
        {
            var context = new Context();
            HfsmInstance<Context> machine = Basic().Build().CreateInstance(context);
            machine.Tick(.1f);
            Assert.That(context.Calls, Is.Empty);
            machine.Start();
            machine.Tick(0);
            Assert.That(context.Calls.Count, Is.EqualTo(2));
            Assert.That(machine.ElapsedSeconds, Is.Zero);
            foreach (float deltaTime in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => machine.Tick(deltaTime));
            }
            Assert.That(machine.Status, Is.EqualTo(HfsmStatus.Running));
            Assert.Throws<InvalidOperationException>(() => machine.Start());
            Assert.Throws<InvalidOperationException>(() => machine.Reset());
            machine.Stop();
            int calls = context.Calls.Count;
            machine.Stop();
            machine.Tick(1);
            Assert.That(context.Calls.Count, Is.EqualTo(calls));
            machine.Start();
            Assert.That(Current(machine), Is.EqualTo("idle"));
            machine.Stop();
            machine.Reset();
            Assert.That(machine.Status, Is.EqualTo(HfsmStatus.NotStarted));
            Assert.That(machine.ActiveCount, Is.Zero);
        }

        [TestCase("duplicate")]
        [TestCase("unknown-parent")]
        [TestCase("two-roots")]
        [TestCase("cycle")]
        [TestCase("no-initial")]
        [TestCase("wrong-initial")]
        [TestCase("unknown-transition")]
        [TestCase("unreachable")]
        [TestCase("too-deep")]
        public void Build_RejectsInvalidTopology(string kind)
        {
            var builder = new HfsmBuilder<Context>(maxDepth: kind == "too-deep" ? 1 : 32);
            builder.AddState("root", () => new State("root"));
            switch (kind)
            {
                case "duplicate": builder.AddState("root", () => new State("root")); break;
                case "unknown-parent": builder.AddState("a", () => new State("a"), "unknown"); break;
                case "two-roots": builder.AddState("a", () => new State("a")); break;
                case "cycle":
                    builder.AddState("a", () => new State("a"), "b").AddState("b", () => new State("b"), "a"); break;
                case "no-initial": builder.AddState("a", () => new State("a"), "root"); break;
                case "wrong-initial": builder.SetInitialChild("root", "root"); break;
                case "unknown-transition": builder.AddTransition("root", "unknown", c => true); break;
                case "unreachable":
                    builder.AddState("a", () => new State("a"), "root").AddState("b", () => new State("b"), "root")
                        .SetInitialChild("root", "a"); break;
                case "too-deep": builder.AddState("a", () => new State("a"), "root").SetInitialChild("root", "a"); break;
            }
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void Builder_RejectsInvalidIdsAndCapacity_AndBuildDoesNotRunUserCode()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HfsmBuilder<Context>(maxStates: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HfsmBuilder<Context>(maxTransitions: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HfsmBuilder<Context>(maxDepth: 0));
            var builder = new HfsmBuilder<Context>(maxStates: 1, maxTransitions: 1);
            Assert.Throws<ArgumentException>(() => builder.AddState(" ", () => new State("bad")));
            builder.AddState("root", () => throw new Exception("factory invoked"));
            Assert.Throws<InvalidOperationException>(() => builder.AddState("extra", () => new State("extra")));
            builder.AddTransition("root", "root", c => throw new Exception("guard invoked"));
            Assert.Throws<InvalidOperationException>(() => builder.AddTransition("root", "root", c => true));
            Assert.DoesNotThrow(() => builder.Build());
        }

        [Test]
        public void Definition_IsFrozen_AndInstancesHaveIndependentHandlersAndPaths()
        {
            HfsmBuilder<Context> builder = Basic();
            HfsmDefinition<Context> definition = builder.Build();
            builder.AddState("extra", () => new State("extra"), "root").AddTransition("work", "extra", c => true);
            Assert.That(definition.FindState("extra"), Is.EqualTo(-1));
            Assert.That(builder.Build().FindState("extra"), Is.GreaterThanOrEqualTo(0));
            var firstContext = new Context { Go = true };
            var secondContext = new Context();
            HfsmInstance<Context> first = definition.CreateInstance(firstContext);
            HfsmInstance<Context> second = definition.CreateInstance(secondContext);
            first.Start();
            second.Start();
            first.Tick(.1f);
            first.Tick(.1f);
            second.Tick(.1f);
            Assert.That(firstContext.LocalTicks, Is.EqualTo(2));
            Assert.That(secondContext.LocalTicks, Is.EqualTo(1));
            Assert.That(Current(first), Is.EqualTo("work"));
            Assert.That(Current(second), Is.EqualTo("idle"));
            var path = new int[definition.MaximumDepth];
            int count = first.CopyActivePath(path);
            path[count - 1] = -1;
            Assert.That(Current(first), Is.EqualTo("work"));
            Assert.Throws<NotSupportedException>(() => ((IList<HfsmStateInfo>)definition.States).Clear());
        }

        [Test]
        public void Factories_CannotReturnNull_OrShareHandlersAcrossInstances()
        {
            HfsmDefinition<Context> missing = new HfsmBuilder<Context>().AddState("root", () => null).Build();
            Assert.Throws<InvalidOperationException>(() => missing.CreateInstance(new Context()));
            var shared = new State("root");
            HfsmDefinition<Context> definition = new HfsmBuilder<Context>().AddState("root", () => shared).Build();
            HfsmInstance<Context> first = definition.CreateInstance(new Context());
            Assert.Throws<InvalidOperationException>(() => definition.CreateInstance(new Context()));
            first.Stop();
            Assert.Throws<InvalidOperationException>(() => first.Reset());
        }

        [TestCase("enter")]
        [TestCase("tick")]
        [TestCase("guard")]
        public void UserException_FaultsAndCleansEntirePath_ThenExplicitResetCanRecover(string phase)
        {
            Action<Context> fail = c => { if (c.Throw) { throw new InvalidOperationException("primary"); } };
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("child", () => new State("child", enter: phase == "enter" ? fail : null,
                    tick: phase == "tick" ? fail : null), "root")
                .SetInitialChild("root", "child")
                .AddTransition("child", "child", c => { if (phase == "guard") { fail(c); } return false; });
            var context = new Context { Throw = true };
            HfsmInstance<Context> machine = builder.Build().CreateInstance(context);
            HfsmResult result = machine.Start();
            if (result.Status == HfsmStatus.Running) { result = machine.Tick(.1f); }
            Assert.That(result.Status, Is.EqualTo(HfsmStatus.Faulted));
            Assert.That(result.Fault.Cause.Message, Is.EqualTo("primary"));
            Assert.That(result.Fault.CleanupSucceeded, Is.True);
            CollectionAssert.AreEqual(new[] { "-child:Faulted", "-root:Faulted" }, context.Calls.GetRange(context.Calls.Count - 2, 2));
            Assert.That(machine.ActiveCount, Is.Zero);
            int count = context.Calls.Count;
            machine.Stop();
            machine.Tick(1);
            Assert.That(context.Calls.Count, Is.EqualTo(count));
            Assert.Throws<InvalidOperationException>(() => machine.Start());
            context.Throw = false;
            machine.Reset();
            Assert.That(machine.Start().Status, Is.EqualTo(HfsmStatus.Running));
        }

        [Test]
        public void TransitionEnterFailure_CleansPartialNewPath_WithoutExitingOldStateTwice()
        {
            var context = new Context();
            HfsmInstance<Context> machine = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("old", () => new State("old"), "root")
                .AddState("group", () => new State("group"), "root")
                .AddState("child", () => new State("child", enter: c => throw new Exception("enter failed")), "group")
                .SetInitialChild("root", "old").SetInitialChild("group", "child")
                .AddTransition("old", "group", c => true).Build().CreateInstance(context);
            machine.Start();
            context.Calls.Clear();
            HfsmResult result = machine.Tick(.1f);
            CollectionAssert.AreEqual(new[] { "-old:Transition", "+group", "+child",
                "-child:Faulted", "-group:Faulted", "-root:Faulted" }, context.Calls);
            Assert.That(result.Status, Is.EqualTo(HfsmStatus.Faulted));
            Assert.That(result.Fault.Cause.Message, Is.EqualTo("enter failed"));
            Assert.That(result.Fault.CleanupSucceeded, Is.True);
            Assert.That(machine.ActiveCount, Is.Zero);
            Assert.That(machine.TransitionCount, Is.Zero, "部分进入不能报告为已提交转移");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExitFailure_StillCleansAncestorsExactlyOnce_AndDoesNotClaimSafeReuse(bool duringTransition)
        {
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root", exit: c => throw new Exception("root cleanup")))
                .AddState("child", () => new State("child", exit: c => throw new Exception("child cleanup")), "root")
                .AddState("next", () => new State("next"), "root")
                .SetInitialChild("root", "child").AddTransition("child", "next", c => true);
            var context = new Context();
            HfsmInstance<Context> machine = builder.Build().CreateInstance(context);
            machine.Start();
            HfsmResult result = duringTransition ? machine.Tick(.1f) : machine.Stop();
            Assert.That(result.Status, Is.EqualTo(HfsmStatus.Faulted));
            Assert.That(result.Fault.Cause.Message, Is.EqualTo("child cleanup"));
            Assert.That(result.Fault.CleanupErrors.Count, Is.EqualTo(2));
            Assert.That(context.Calls, Does.Not.Contain("+next"));
            Assert.That(context.Calls.Count, Is.EqualTo(4));
            machine.Stop();
            Assert.That(context.Calls.Count, Is.EqualTo(4));
            Assert.Throws<InvalidOperationException>(() => machine.Reset());
        }

        [TestCase("start")]
        [TestCase("tick")]
        [TestCase("stop")]
        [TestCase("reset")]
        [TestCase("snapshot")]
        public void CallbackReentry_IsRejectedAndReported(string operation)
        {
            HfsmInstance<Context> machine = null;
            Action<Context> reenter = c =>
            {
                switch (operation)
                {
                    case "start": machine.Start(); break;
                    case "tick": machine.Tick(.1f); break;
                    case "stop": machine.Stop(); break;
                    case "reset": machine.Reset(); break;
                    case "snapshot": machine.CopyActivePath(new int[1]); break;
                }
            };
            machine = new HfsmBuilder<Context>().AddState("root", () => new State("root", tick: reenter))
                .Build().CreateInstance(new Context());
            machine.Start();
            HfsmResult result = machine.Tick(.1f);
            Assert.That(result.Status, Is.EqualTo(HfsmStatus.Faulted));
            Assert.That(result.Fault.Cause, Is.TypeOf<InvalidOperationException>());
            Assert.That(result.Fault.CleanupSucceeded, Is.True);
        }

        [Test]
        public void Diagnostics_AreBounded_Leased_AndNeverReevaluateGuards()
        {
            var builder = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root"))
                .AddState("a", () => new State("a"), "root")
                .AddState("b", () => new State("b"), "root").SetInitialChild("root", "a")
                .AddTransition("a", "b", c => { c.GuardCalls++; return true; })
                .AddTransition("b", "a", c => { c.GuardCalls++; return true; });
            var context = new Context();
            HfsmInstance<Context> machine = builder.Build().CreateInstance(context, traceCapacity: 2);
            machine.Start();
            var guards = new HfsmGuardObservation[2];
            var trace = new HfsmTransitionRecord[2];
            Assert.That(machine.CopyGuardObservations(guards), Is.Zero);
            IDisposable first = machine.AcquireDiagnostics();
            IDisposable second = machine.AcquireDiagnostics();
            for (int i = 0; i < 3; i++) { machine.Tick(.1f); }
            Assert.That(machine.CopyTransitionTrace(trace), Is.EqualTo(2));
            Assert.That(trace[0].TickSequence, Is.EqualTo(2));
            Assert.That(trace[1].TickSequence, Is.EqualTo(3));
            machine.CopyGuardObservations(guards);
            Assert.That(guards[0].TickSequence, Is.EqualTo(3));
            Assert.That(guards[1].TickSequence, Is.EqualTo(2), "未评估条件保留带时间戳的旧结果");
            Assert.That(context.GuardCalls, Is.EqualTo(3));
            first.Dispose();
            first.Dispose();
            Assert.That(machine.DiagnosticsEnabled, Is.True);
            second.Dispose();
            Assert.That(machine.DiagnosticsEnabled, Is.False);
            using (machine.AcquireDiagnostics())
            {
                Assert.That(machine.CopyTransitionTrace(trace), Is.Zero);
                machine.CopyGuardObservations(guards);
                Assert.That(guards[0].TickSequence, Is.Zero);
            }
            Assert.That(context.GuardCalls, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WarmTicksAndTransitions_DoNotAllocate(bool diagnostics)
        {
            HfsmDefinition<Context> definition = new HfsmBuilder<Context>()
                .AddState("root", () => new State("root", record: false))
                .AddState("a", () => new State("a", record: false), "root")
                .AddState("b", () => new State("b", record: false), "root")
                .SetInitialChild("root", "a").AddTransition("a", "b", c => c.Go).AddTransition("b", "a", c => !c.Go)
                .Build();
            var context = new Context();
            HfsmInstance<Context> machine = definition.CreateInstance(context);
            using (diagnostics ? machine.AcquireDiagnostics() : null)
            {
                machine.Start();
                for (int i = 0; i < 1000; i++) { context.Go = !context.Go; machine.Tick(.02f); }
                bool previous = UnityEngine.Profiling.Profiler.enabled;
                UnityEngine.Profiling.Profiler.enabled = true;
                try
                {
                    using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,
                        "GC.Alloc", 1024, Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                    var probe = new byte[512];
                    recorder.Stop();
                    GC.KeepAlive(probe);
                    Assert.That(recorder.Valid && recorder.Count > 0, Is.True, "分配计数器必须通过正向探针");
                    recorder.Reset();
                    recorder.Start();
                    for (int i = 0; i < 10000; i++) { context.Go = !context.Go; machine.Tick(.02f); }
                    recorder.Stop();
                    Assert.That(recorder.Count, Is.Zero, "预热后的 Tick 与状态切换不得产生 GC.Alloc 事件");
                }
                finally { UnityEngine.Profiling.Profiler.enabled = previous; }
                Assert.That(machine.TransitionCount, Is.EqualTo(11000));
            }
        }
    }
}
