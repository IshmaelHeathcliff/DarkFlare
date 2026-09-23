using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class MonsterBehaviorTests
    {
        sealed class World : IMonsterBehaviorWorld
        {
            public Vector2 Position { get; set; }
            public Vector2 Velocity { get; set; }
            public bool IsAlive { get; set; } = true;
            public StatusActionBlock BlockedActions { get; set; }
            public bool TargetAvailable { get; set; } = true;
            public Vector2 TargetPosition { get; set; } = new(30, 0);
            public string TargetId => "player:test";
            public float AttackRadius => .75f;
            public float AttackInterval => .75f;
            public NavigationStatus NavigationStatus { get; set; } = NavigationStatus.Moving;
            public bool Visible { get; set; } = true;
            public bool Standable { get; set; } = true;
            public int Hits { get; set; }
            public int Samples { get; set; }
            public NavigationTargetKind Intent { get; set; }
            public Vector2 Goal { get; set; }
            public bool HasLineOfSight(Vector2 target) { return Visible; }
            public bool CanStand(Vector2 target) { Samples++; return Standable; }
            public bool CanAttack(string targetId)
            {
                return TargetAvailable && IsAlive && Visible && targetId == TargetId
                    && (BlockedActions & StatusActionBlock.Attack) == 0 && Vector2.Distance(Position, TargetPosition) <= AttackRadius;
            }
            public void Move(NavigationTargetKind kind, Vector2 target) { Intent = kind; Goal = target; }
            public void StopMoving() { Intent = NavigationTargetKind.None; }
            public void Hit(string targetId, long actionId) { Hits++; }
        }

        MonsterBehaviorDefinition _config;
        GameArchitectureTestFixture _fixture;
        IArchitecture _architecture;
        Guid _scope;
        World _world;
        MonsterBehaviorRuntime _runtime;

        [SetUp]
        public void Setup()
        {
            _fixture = new GameArchitectureTestFixture(); _architecture = _fixture.Start(); _scope = Guid.NewGuid();
            _config = ScriptableObject.CreateInstance<MonsterBehaviorDefinition>(); _world = new World();
            _runtime = Create(_world, MonsterBehaviorStateDto.Create(new Vector3Dto(), 123));
        }
        [TearDown]
        public void Cleanup() { _fixture.Stop(); UnityEngine.Object.DestroyImmediate(_config); }
        MonsterBehaviorRuntime Create(World world, MonsterBehaviorStateDto state, float cooldown = 0)
        {
            var runtime = new MonsterBehaviorRuntime(_config, world, "monster", state, cooldown);
            _architecture.SendCommand(new RegisterBehaviorCommand(_scope, runtime)); return runtime;
        }
        void Tick(int count = 1, float dt = .1f)
        {
            for (int i = 0; i < count; i++) { _architecture.GetSystem<BehaviorSystem>().Tick(_scope, dt); }
        }

        [TestCase("_activationDistance", float.NaN)]
        [TestCase("_deactivationDistance", 10f)]
        [TestCase("_sightDistance", 30f)]
        [TestCase("_perceptionInterval", .5f)]
        [TestCase("_windupSeconds", 0f)]
        [TestCase("_waitMaximum", .1f)]
        public void Configuration_RejectsInvalidRelationshipsAndNumbers(string field, float value)
        {
            typeof(MonsterBehaviorDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_config, value);
            Assert.Throws<ArgumentException>(() => _config.Validate());
        }

        [Test]
        public void ActivationHysteresis_WallOcclusion_AndLostSightMemory()
        {
            Tick(); Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Sleeping));
            Assert.That(_world.Intent, Is.EqualTo(NavigationTargetKind.None));
            _world.TargetPosition = new Vector2(13, 0); Tick(); Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Wandering));
            _world.TargetPosition = new Vector2(15, 0); Tick(); Assert.That(_runtime.IsInRange, Is.True);
            _world.TargetPosition = new Vector2(8, 0); _world.Visible = false; Tick(); Assert.That(_runtime.IsVisible, Is.False);
            _world.Visible = true; Tick(); Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Chasing));
            _world.Visible = false; _world.TargetPosition = new Vector2(5, 4); Tick();
            Assert.That(_world.Intent, Is.EqualTo(NavigationTargetKind.Point)); Assert.That(_world.Goal, Is.EqualTo(new Vector2(8, 0)));
            Tick(22); Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Wandering));
            _world.TargetPosition = new Vector2(17, 0); Tick(); Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Sleeping));
        }

        [Test]
        public void ViewCone_DoesNotDiscoverPlayerBehindActor()
        {
            _world.TargetPosition = new Vector2(-3, 0); Tick();
            Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Wandering));
            Assert.That(_runtime.IsVisible, Is.False);
        }

        [Test]
        public void Attack_WindupOnce_CancelConsumesCooldown_AndMissDoesNotHit()
        {
            _world.TargetPosition = new Vector2(.5f, 0); Tick();
            Assert.That(_runtime.AttackPhase, Is.EqualTo(MonsterAttackPhase.Windup)); Assert.That(_world.Hits, Is.Zero);
            _world.BlockedActions = StatusActionBlock.Attack; Tick();
            Assert.That(_runtime.AttackPhase, Is.EqualTo(MonsterAttackPhase.None)); Assert.That(_runtime.CooldownRemaining, Is.GreaterThan(0));
            _world.BlockedActions = 0; Tick(6); Assert.That(_world.Hits, Is.Zero);
            Tick(2); _world.Visible = false; Tick(4); Assert.That(_world.Hits, Is.Zero);
        }

        [Test]
        public void MoveAndAttackPermissions_AreIndependent_AndDeathRevokes()
        {
            _world.TargetPosition = new Vector2(.5f, 0); _world.BlockedActions = StatusActionBlock.Move;
            Tick(); Assert.That(_runtime.Presentation.Semantic, Is.EqualTo(BehaviorSemantic.AttackWindup));
            Tick(4); Assert.That(_world.Hits, Is.EqualTo(1)); Assert.That(_world.Intent, Is.EqualTo(NavigationTargetKind.None));
            _world.BlockedActions |= StatusActionBlock.Attack; Tick(); Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Controlled));
            _world.IsAlive = false; Tick(); Assert.That(_runtime.Mode, Is.EqualTo(MonsterBehaviorMode.Dead));
            Tick(20); Assert.That(_world.Hits, Is.EqualTo(1));
        }

        [Test]
        public void CaptureRestore_WindupAndRecovery_DoNotDuplicateHitOrResetCooldown()
        {
            _world.TargetPosition = new Vector2(.5f, 0); Tick(2);
            MonsterBehaviorStateDto saved = _runtime.CaptureState(); float cooldown = _runtime.CooldownRemaining;
            var restoredWorld = new World { TargetPosition = _world.TargetPosition };
            MonsterBehaviorRuntime restored = Create(restoredWorld, saved, cooldown);
            Tick(3); Assert.That(_world.Hits, Is.EqualTo(1)); Assert.That(restoredWorld.Hits, Is.EqualTo(1));
            Assert.That(restored.CooldownRemaining, Is.EqualTo(_runtime.CooldownRemaining).Within(.001));
            MonsterBehaviorStateDto recovery = restored.CaptureState(); Assert.That(recovery.HitResolved, Is.True);
            _runtime.Restore(recovery, restored.CooldownRemaining);
            Assert.That(_runtime.Presentation.Semantic, Is.EqualTo(BehaviorSemantic.Recovery));
            var thirdWorld = new World { TargetPosition = _world.TargetPosition };
            Create(thirdWorld, recovery, restored.CooldownRemaining); Tick(); Assert.That(thirdWorld.Hits, Is.Zero);
        }

        [Test]
        public void WanderSampling_IsBounded_AndIndependentOfDiagnostics()
        {
            _world.TargetPosition = new Vector2(12, 0); _world.Standable = false;
            var otherWorld = new World { TargetPosition = _world.TargetPosition, Standable = false };
            MonsterBehaviorRuntime other = Create(otherWorld, MonsterBehaviorStateDto.Create(new Vector3Dto(), 123));
            using IDisposable lease = _runtime.AcquireDiagnostics();
            Tick(); Assert.That(_world.Samples, Is.EqualTo(4)); _runtime.Capture();
            Tick(30); Assert.That(_runtime.CaptureState().RandomSequence, Is.EqualTo(other.CaptureState().RandomSequence));
            Assert.That(_runtime.CaptureState().WaitRemaining, Is.EqualTo(other.CaptureState().WaitRemaining));
            Assert.That(_world.Samples, Is.LessThan(31 * 4));
        }

        [Test]
        public void PauseAndUnregister_FreezeProgress_AndRejectLateMarkers()
        {
            _world.TargetPosition = new Vector2(.5f, 0); Tick();
            _runtime.Control(BehaviorControl.TogglePause); float remaining = _runtime.CooldownRemaining;
            Tick(20); Assert.That(_runtime.CooldownRemaining, Is.EqualTo(remaining)); Assert.That(_world.Hits, Is.Zero);
            _architecture.SendCommand(new UnregisterBehaviorCommand(_runtime.Handle));
            Assert.That(_runtime.ObserveAnimation(_runtime.CaptureState().ActionId), Is.False);
            Assert.That(_world.Intent, Is.EqualTo(NavigationTargetKind.None));
        }

        [Test]
        public void StateValidation_RejectsUnknownVersionsAndInconsistentHitPhase_AndCopyIsDetached()
        {
            MonsterBehaviorStateDto state = _runtime.CaptureState(); state.Version = 2; Assert.That(state.IsValid, Is.False);
            state.Version = 1; state.AttackPhase = MonsterAttackPhase.Recovery; Assert.That(state.IsValid, Is.False);
            state.AttackPhase = MonsterAttackPhase.None; state.Facing.X = float.PositiveInfinity; Assert.That(state.IsValid, Is.False);
            Assert.That(_runtime.CaptureState().IsValid, Is.True);
        }
    }
}
