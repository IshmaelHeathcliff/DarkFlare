using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class BehaviorRuntimeTests
    {
        GameArchitectureTestFixture _fixture;
        IArchitecture _architecture;
        BehaviorSampleConfig _config;
        PatrolBehaviorRuntime.Template _template;
        Guid _scope;
        [SetUp]
        public void SetUp()
        {
            _fixture = new GameArchitectureTestFixture(); _architecture = _fixture.Start();
            _config = ScriptableObject.CreateInstance<BehaviorSampleConfig>();
            _template = new PatrolBehaviorRuntime.Template(_config); _scope = Guid.NewGuid();
        }
        [TearDown]
        public void TearDown() { _fixture.Stop(); UnityEngine.Object.DestroyImmediate(_config); }
        BehaviorHandle Register(PatrolBehaviorRuntime runtime)
        {
            return _architecture.SendCommand(new RegisterBehaviorCommand(_scope, runtime));
        }
        void Tick(float dt = .1f) { _architecture.GetSystem<BehaviorSystem>().Tick(_scope, dt); }

        [Test]
        public void SharedTemplate_PatrolInstancesAreIndependent_AndPauseFreezesClock()
        {
            PatrolBehaviorRuntime first = _template.Create("a", Vector2.zero);
            PatrolBehaviorRuntime second = _template.Create("b", Vector2.one);
            BehaviorHandle a = Register(first); Register(second);
            Tick(); _architecture.SendCommand(new ControlBehaviorCommand(a, BehaviorControl.TogglePause));
            for (int i = 0; i < 20; i++) { Tick(); }
            Assert.That(first.TickSequence, Is.EqualTo(1)); Assert.That(second.TickSequence, Is.EqualTo(21));
            Assert.That(first.Presentation.Position, Is.EqualTo(Vector2.zero));
            Assert.That(second.Presentation.Position.x, Is.GreaterThan(1));
            _architecture.SendCommand(new ControlBehaviorCommand(a, BehaviorControl.TogglePause)); Tick();
            Assert.That(first.TickSequence, Is.EqualTo(2));
        }

        [Test]
        public void HfsmInterrupt_AbortsRunningTreeBeforeHolding_AndRestartsFresh()
        {
            PatrolBehaviorRuntime runtime = _template.Create("a", Vector2.zero); BehaviorHandle handle = Register(runtime);
            for (int i = 0; i < 20; i++) { Tick(); }
            Vector2 position = runtime.Presentation.Position;
            _architecture.SendCommand(new ControlBehaviorCommand(handle, BehaviorControl.Interrupt)); Tick();
            BehaviorDebugSnapshot frame = runtime.Capture();
            Assert.That(frame.Hfsm.Single(n => n.Id == "interrupted").State, Is.EqualTo("Running"));
            Assert.That(frame.Tree.Any(n => n.State == "Running"), Is.False);
            Assert.That(frame.Tree.Single(n => n.Id == "move.out").State, Is.EqualTo("Aborted"));
            for (int i = 0; i < 3; i++) { Tick(); }
            Assert.That(runtime.Presentation.Position, Is.EqualTo(position));
            for (int i = 0; i < 30; i++) { Tick(); }
            Assert.That(runtime.Capture().Hfsm.Single(n => n.Id == "active").State, Is.EqualTo("Running"));
        }

        [Test]
        public void UnregisterDuringCallback_RevokesImmediately_AndDoesNotSkipOtherActors()
        {
            PatrolBehaviorRuntime first = _template.Create("a", Vector2.zero);
            PatrolBehaviorRuntime second = _template.Create("b", Vector2.one);
            BehaviorHandle a = Register(first); Register(second);
            IUnRegister subscription = _architecture.RegisterEvent<BehaviorPresentationEvent>(signal =>
            {
                if (signal.Handle.Equals(a) && signal.Semantic == BehaviorSemantic.AttackWindup)
                {
                    _architecture.SendCommand(new UnregisterBehaviorCommand(a));
                }
            });
            _architecture.SendCommand(new ControlBehaviorCommand(a, BehaviorControl.RequestAttack)); Tick();
            Assert.That(first.Presentation.Position, Is.EqualTo(Vector2.zero));
            Assert.That(first.Presentation.Semantic, Is.EqualTo(BehaviorSemantic.Stopped));
            Assert.That(second.TickSequence, Is.EqualTo(1));
            Assert.That(_architecture.GetModel<BehaviorModel>().Count, Is.EqualTo(1));
            Assert.That(_architecture.SendQuery(new GetBehaviorSnapshotQuery(a)), Is.Null);
            subscription.UnRegister();
        }

        [Test]
        public void ScopeStop_IsSelective_AndLateCallbacksCannotAffectReplacement()
        {
            PatrolBehaviorRuntime first = _template.Create("a", Vector2.zero); BehaviorHandle old = Register(first);
            Guid otherScope = Guid.NewGuid();
            BehaviorHandle other = _architecture.SendCommand(new RegisterBehaviorCommand(otherScope, _template.Create("b", Vector2.one)));
            _architecture.GetSystem<BehaviorSystem>().StopScope(_scope);
            BehaviorHandle replacement = Register(_template.Create("replacement", Vector2.zero));
            Assert.That(replacement.Equals(old), Is.False);
            Assert.That(_architecture.SendCommand(new ObserveBehaviorAnimationCommand(old, 1)), Is.False);
            _architecture.SendCommand(new UnregisterBehaviorCommand(old));
            Assert.That(_architecture.SendQuery(new GetBehaviorSnapshotQuery(other)), Is.Not.Null);
            Assert.That(_architecture.SendQuery(new GetBehaviorSnapshotQuery(replacement)), Is.Not.Null);
        }

        [Test]
        public void AnimatorObservations_AreOptional_Deduplicated_AndActionScoped()
        {
            PatrolBehaviorRuntime runtime = _template.Create("a", Vector2.zero); BehaviorHandle handle = Register(runtime);
            _architecture.SendCommand(new ControlBehaviorCommand(handle, BehaviorControl.RequestAttack)); Tick();
            long action = runtime.Presentation.ActionInstanceId;
            Assert.That(runtime.ObserveAnimation(action), Is.False, "预备阶段不接受完成标记");
            for (int i = 0; i < 6; i++) { Tick(); }
            Assert.That(runtime.ObserveAnimation(action), Is.True);
            Assert.That(runtime.ObserveAnimation(action), Is.False);
            for (int i = 0; i < 20; i++) { Tick(); }
            Assert.That(runtime.Presentation.ActionInstanceId, Is.GreaterThan(action));
            Assert.That(runtime.ObserveAnimation(action), Is.False);
            Assert.That(runtime.TickSequence, Is.EqualTo(27), "回执不会推进或阻塞模拟");
        }

        [Test]
        public void DebugSelection_ReleasesLeaseOnSwitchCloseAndSessionEnd()
        {
            PatrolBehaviorRuntime first = _template.Create("a", Vector2.zero);
            PatrolBehaviorRuntime second = _template.Create("b", Vector2.one);
            BehaviorHandle a = Register(first); BehaviorHandle b = Register(second);
            using var selection = new BehaviorDebugSelection();
            selection.Select(_architecture, a); Assert.That(first.DiagnosticsEnabled, Is.True);
            selection.Select(_architecture, b); Assert.That(first.DiagnosticsEnabled, Is.False); Assert.That(second.DiagnosticsEnabled, Is.True);
            _fixture.Stop();
            Assert.That(selection.Capture(), Is.Null); Assert.That(second.DiagnosticsEnabled, Is.False);
            _architecture = _fixture.Start();
            Assert.That(_architecture.GetModel<BehaviorModel>().Count, Is.Zero);
            Assert.That(_architecture.SendQuery(new GetBehaviorSnapshotQuery(b)), Is.Null);
        }
    }
}
