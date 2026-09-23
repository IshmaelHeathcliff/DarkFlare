using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class MonsterBehaviorPlayModeTests
    {
        readonly SceneFlowPlayModeFixture _fixture = new();
        readonly InputTestFixture _input = new();
        ApplicationHost _host;
        IArchitecture _architecture;
        MapScope _map;
        PlayerController _player;
        MonsterController _monster;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return InputTestFixtureGuard.Setup(_input);
            yield return _fixture.EnterMain(); _host = ApplicationHost.Current;
            SceneFlowResult result = null;
            yield return _host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:wilderness", "arrival")).ToCoroutine(value => result = value);
            Assert.IsTrue(result.Succeeded, result.Exception?.ToString());
            _architecture = _host.CurrentSession.Architecture; _map = _architecture.GetUtility<WorldMapRuntime>().Active;
            _map.Spawner.StopSpawning();
            foreach (MonsterController existing in Object.FindObjectsByType<MonsterController>()) { Object.Destroy(existing.gameObject); }
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerController>(); _player.enabled = false;
            PositionPlayer(new Vector2(-10, -10));
            _monster = _architecture.GetSystem<SpawnSystem>().SpawnMonster(_map.Spawner.SpawnDefinition, 349, new Vector3(9, 10));
            Assert.IsNotNull(_monster.BehaviorRuntime);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_player != null) { _player.enabled = true; }
            yield return _fixture.EnterFrontEnd();
            InputTestFixtureGuard.TearDown(_input);
        }

        void PositionPlayer(Vector2 position)
        {
            _player.transform.position = position;
            Rigidbody2D body = _player.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator FormalMonster_SleepsWandersAndChasesUsingCurrentOcclusion()
        {
            Vector3 initial = _monster.transform.position;
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(MonsterBehaviorMode.Sleeping, _monster.BehaviorRuntime.Mode);
            Assert.AreEqual(Vector2.zero, _monster.Body.linearVelocity);
            Assert.Less(Vector3.Distance(initial, _monster.transform.position), .02f);
            NavigationStampTicket wall = _architecture.SendCommand(new ChangeNavigationStampCommand(_map.Generation, "behavior-wall", new MapGeometryRect(11, 8, 12, 12)));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(NavigationStampStatus.Applied, wall.Status);
            float readyDeadline = Time.realtimeSinceStartup + 10;
            while (!_map.Navigation.IsReady && Time.realtimeSinceStartup < readyDeadline) { yield return null; }
            Assert.IsTrue(_map.Navigation.IsReady);
            PositionPlayer(new Vector2(15, 10));
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(MonsterBehaviorMode.Wandering, _monster.BehaviorRuntime.Mode,
                $"player {_player.transform.position}, monster {_monster.transform.position}, {_monster.BehaviorRuntime.Capture().Summary}");
            Assert.IsFalse(_monster.BehaviorRuntime.IsVisible);
            NavigationStampTicket removed = _architecture.SendCommand(new ChangeNavigationStampCommand(_map.Generation, "behavior-wall", default, true));
            readyDeadline = Time.realtimeSinceStartup + 10;
            while ((removed.Status == NavigationStampStatus.Queued || !_map.Navigation.IsReady) && Time.realtimeSinceStartup < readyDeadline) { yield return null; }
            Assert.AreEqual(NavigationStampStatus.Applied, removed.Status);
            Assert.IsTrue(_map.Navigation.IsReady);
            MonsterBehaviorStateDto state = _monster.CaptureBehavior(); state.Facing.X = 1; state.Facing.Y = 0;
            _monster.RestoreBehavior(state);
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(MonsterBehaviorMode.Chasing, _monster.BehaviorRuntime.Mode);
            Assert.AreEqual(NavigationTargetKind.Player, _monster.CaptureNavigation().Target);
            Assert.Greater(_monster.Body.linearVelocity.x, 0);
        }

        [UnityTest]
        public IEnumerator WindupCancellation_DoesNotConsumeDamageRng_AndRecoveryDoesNotRepeatHit()
        {
            _monster.Body.simulated = false;
            PositionPlayer(_monster.Body.position + Vector2.right * .5f);
            GameplayRandomSystem random = _architecture.GetSystem<GameplayRandomSystem>();
            int before = random.CaptureState().NextSequences[GameplayRandomChannel.MonsterAttack];
            float deadline = Time.realtimeSinceStartup + 3;
            while (_monster.BehaviorRuntime.AttackPhase != MonsterAttackPhase.Windup && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(MonsterAttackPhase.Windup, _monster.BehaviorRuntime.AttackPhase);
            StatusSystem statuses = _architecture.GetSystem<StatusSystem>();
            statuses.ApplyStatus(statuses.GetTarget(_monster.Actor), StatusMutation.Apply(new StatusRules("behavior_block", duration: 1),
                new StatusSource(StatusSourceKind.Mechanism, "behavior-test"), new StatusEffectSnapshot(blockedActions: StatusActionBlock.Attack)));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(MonsterAttackPhase.None, _monster.BehaviorRuntime.AttackPhase);
            Assert.Greater(_monster.ContactDamageCooldownRemainingSeconds, 0);
            Assert.AreEqual(before, random.CaptureState().NextSequences[GameplayRandomChannel.MonsterAttack]);
            deadline = Time.realtimeSinceStartup + 4;
            while (_monster.BehaviorRuntime.AttackPhase != MonsterAttackPhase.Recovery && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.AreEqual(MonsterAttackPhase.Recovery, _monster.BehaviorRuntime.AttackPhase);
            Assert.AreEqual(before + 1, random.CaptureState().NextSequences[GameplayRandomChannel.MonsterAttack]);
            MonsterBehaviorStateDto recovery = _monster.CaptureBehavior();
            _monster.RestoreBehavior(recovery);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(before + 1, random.CaptureState().NextSequences[GameplayRandomChannel.MonsterAttack]);
            BehaviorHandle old = _monster.BehaviorHandle;
            _monster.gameObject.SetActive(false); _monster.gameObject.SetActive(true);
            Assert.IsFalse(old.Equals(_monster.BehaviorHandle));
            Assert.IsNull(_architecture.SendQuery(new GetBehaviorSnapshotQuery(old)));
        }

        [UnityTest]
        public IEnumerator NavigationOverride_IsExplicit_AndReturnsToBehaviorDecision()
        {
            var intent = new NavigationIntentDto { Target = NavigationTargetKind.Point, MapId = _map.Navigation.MapId,
                Point = new Vector3Dto { X = 15, Y = 10 } };
            NavigationAgentHandle handle = _map.Navigation.Handle(_monster);
            Assert.IsFalse(_architecture.SendCommand(new SetNavigationIntentCommand(_monster, intent, handle)));
            Assert.IsTrue(_architecture.SendCommand(new SetNavigationIntentCommand(_monster, intent, handle, takeBehaviorControl: true)));
            Assert.IsNull(_monster.BehaviorRuntime);
            _architecture.SendCommand(new ReleaseMonsterNavigationOverrideCommand(_monster));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsNotNull(_monster.BehaviorRuntime);
            Assert.AreEqual(NavigationTargetKind.None, _monster.CaptureNavigation().Target);
            Assert.AreEqual(1, _architecture.GetModel<BehaviorModel>().Count);
        }

        [UnityTest]
        public IEnumerator FormalDiagnostics_UsesSeparateInputAndReturnsGameplayFocus()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>(); Gamepad pad = InputSystem.AddDevice<Gamepad>();
            BehaviorDebugPanel panel = Object.FindAnyObjectByType<BehaviorDebugPanel>();
            Assert.IsNotNull(panel); Assert.IsFalse(panel.IsOpen);
            _input.Press(keyboard.f8Key); yield return null; _input.Release(keyboard.f8Key); yield return null;
            Assert.IsTrue(panel.IsOpen); Assert.IsFalse(_host.Input.IsGameplayEnabled);
            Assert.AreEqual(_monster.BehaviorHandle, panel.SelectedHandle);
            VisualElement root = panel.GetComponent<RuntimePanelView>().Root;
            foreach (string name in new[] { "pause", "interrupt", "attack" })
            { Assert.AreEqual(DisplayStyle.None, root.Q<Button>(name).resolvedStyle.display); }
            _input.Press(pad.buttonEast); yield return null; _input.Release(pad.buttonEast); yield return null;
            Assert.IsFalse(panel.IsOpen); Assert.IsTrue(_host.Input.IsGameplayEnabled);
            _input.Press(pad.rightStickButton); yield return null; _input.Release(pad.rightStickButton); yield return null;
            Assert.IsTrue(panel.IsOpen);
            _input.Press(keyboard.escapeKey); yield return null; _input.Release(keyboard.escapeKey); yield return null;
            Assert.IsFalse(panel.IsOpen); Assert.IsFalse(panel.SelectedHandle.IsValid);
            Assert.IsFalse(_host.Input.HasUiContextOverride);
        }

        [UnityTest]
        public IEnumerator FailedMapCommit_RestoresWindupCooldownAndRandomWithoutOldAuthorization()
        {
            using GamePauseLease pause = _host.GameTime.AcquirePause("behavior-rollback");
            var state = _monster.CaptureBehavior();
            state.Mode = MonsterBehaviorMode.Attacking; state.AttackPhase = MonsterAttackPhase.Windup;
            state.ActionId = 7; state.TargetId = _player.Actor.CombatIdentity; state.PhaseRemaining = .17f; state.RandomSequence = 21;
            _monster.RestoreRuntime(_monster.Actor.Resources, .41f); _monster.RestoreBehavior(state);
            _map.Spawner.RegisterRestoredMonster(_monster);
            WorldMapRuntime maps = _architecture.GetUtility<WorldMapRuntime>();
            GameplayRandomState random = _architecture.GetSystem<GameplayRandomSystem>().CaptureState();
            foreach (string boundary in new[] { "prepared", "source-detached", "target-restored", "before-commit" })
            {
                maps.FaultInjection = phase => { if (phase == boundary) { throw new System.InvalidOperationException("behavior rollback " + boundary); } };
                SceneFlowResult result = null;
                yield return _host.SceneFlow.RequestAsync(SceneFlowRequest.ChangeMap("map:town", "return")).ToCoroutine(value => result = value);
                maps.FaultInjection = null;
                Assert.IsFalse(result.Succeeded); Assert.AreSame(_map, maps.Active);
                Assert.AreEqual(7, _monster.CaptureBehavior().ActionId); Assert.AreEqual(.17f, _monster.CaptureBehavior().PhaseRemaining);
                Assert.AreEqual(21, _monster.CaptureBehavior().RandomSequence); Assert.AreEqual(.41f, _monster.ContactDamageCooldownRemainingSeconds);
                Assert.AreEqual(1, _architecture.GetModel<BehaviorModel>().Count);
                CollectionAssert.AreEquivalent(random.NextSequences, _architecture.GetSystem<GameplayRandomSystem>().CaptureState().NextSequences);
                Button cancel = Object.FindAnyObjectByType<ApplicationShellBootstrap>()?.GetComponent<RuntimePanelView>().Root?.Q<Button>("application-modal-cancel");
                if (cancel != null)
                {
                    using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled(); submit.target = cancel; cancel.SendEvent(submit);
                }
            }
            Assert.IsFalse(_architecture.SendCommand(new ContactAttackCommand(_monster, _player.Actor)), "暂停时不能由外部入口接纳动作");
        }
    }
}
