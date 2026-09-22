using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class BehaviorPlayModeTests
    {
        readonly InputTestFixture _input = new InputTestFixture();
        BehaviorSampleHost _sample;
        Keyboard _keyboard;
        Gamepad _pad;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return InputTestFixtureGuard.Setup(_input);
            if (ApplicationHost.TryGetCurrent(out ApplicationHost previous))
            {
                yield return previous.ShutdownAsync().ToCoroutine();
                UnityEngine.Object.Destroy(previous.gameObject); yield return null;
            }
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Diagnostics/BehaviorSample.unity", new LoadSceneParameters(LoadSceneMode.Single));
            new GameObject("[ApplicationHost]").AddComponent<ApplicationHost>();
            _sample = UnityEngine.Object.FindAnyObjectByType<BehaviorSampleHost>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (!_sample.IsReady && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.That(_sample.IsReady, Is.True);
            _keyboard = InputSystem.AddDevice<Keyboard>(); _pad = InputSystem.AddDevice<Gamepad>();
            yield return null; yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_sample != null) { _sample.enabled = false; }
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host)) { yield return host.StopCurrentSessionAsync().ToCoroutine(); }
            InputTestFixtureGuard.TearDown(_input);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Bootstrap.unity", new LoadSceneParameters(LoadSceneMode.Single));
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }

        [UnityTest]
        public IEnumerator DebugPanel_KeyboardAndGamepad_SelectionLocalizationAndRelease()
        {
            BehaviorDebugPanel panel = UnityEngine.Object.FindAnyObjectByType<BehaviorDebugPanel>();
            RuntimePanelView view = panel.GetComponent<RuntimePanelView>();
            Assert.That(panel.IsOpen, Is.False);
            _input.Press(_keyboard.tabKey); yield return null; _input.Release(_keyboard.tabKey); yield return null;
            Assert.That(panel.IsOpen, Is.True); Assert.That(_sample.Input.IsGameplayEnabled, Is.False);
            Assert.That(view.Root.Q<Button>("pause").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Root.Q<Button>("interrupt").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Root.Q<Button>("attack").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            BehaviorHandle first = panel.SelectedHandle;
            _input.Press(_pad.buttonSouth); yield return null; _input.Release(_pad.buttonSouth); yield return null;
            Assert.That(panel.SelectedHandle.Equals(first), Is.False);
            _input.Press(_pad.buttonEast); yield return null; _input.Release(_pad.buttonEast); yield return null;
            Assert.That(panel.IsOpen, Is.False); Assert.That(_sample.Input.IsGameplayEnabled, Is.True);
            _input.Press(_pad.startButton); yield return null; _input.Release(_pad.startButton); yield return null;
            Assert.That(panel.IsOpen, Is.True);
            _input.Press(_keyboard.escapeKey); yield return null; _input.Release(_keyboard.escapeKey); yield return null;
            Assert.That(panel.IsOpen, Is.False);
            panel.Toggle();
            foreach (string locale in new[] { "zh-Hans", "en", "qps-ploc" })
            {
                yield return new UnityLocalizationRuntime().ApplyLocaleAsync(locale, new[] { "ui" }, default).ToCoroutine();
                yield return null; yield return null;
                Button next = view.Root.Q<Button>("next");
                Assert.That(next.worldBound.width, Is.GreaterThan(0));
                Assert.That(next.worldBound.xMax, Is.LessThanOrEqualTo(view.Root.worldBound.xMax + 1));
                Assert.That(view.Root.Q<Label>("title").text, Does.Not.Contain("No translation found"));
            }
            yield return new UnityLocalizationRuntime().ApplyLocaleAsync("zh-Hans", new[] { "ui" }, default).ToCoroutine();
            panel.Toggle();
            Assert.That(_sample.Input.HasUiContextOverride, Is.False);
            Assert.That(panel.SelectedHandle.IsValid, Is.False);
            _sample.enabled = false;
            for (int i = 0; i < 5; i++) { yield return null; }
            Assert.That(GameArchitectureProvider.HasCurrent, Is.False);
            Assert.That(panel.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator AnimatorBridge_OptionalAndSingleOwner_RejectsLateCallbacksAndReuse()
        {
            BehaviorSampleActor actor = UnityEngine.Object.FindObjectsByType<BehaviorSampleActor>()[0];
            BehaviorAnimatorBridge bridge = actor.GetComponent<BehaviorAnimatorBridge>();
            Animator animator = actor.GetComponent<Animator>();
            IArchitecture architecture = _sample.GetArchitecture();
            BehaviorHandle handle = actor.Handle;
            architecture.SendCommand(new ControlBehaviorCommand(handle, BehaviorControl.RequestAttack));
            float deadline = Time.realtimeSinceStartup + 5;
            BehaviorDebugSnapshot frame = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                frame = architecture.SendQuery(new GetBehaviorSnapshotQuery(handle));
                if (frame.Presentation.Semantic == BehaviorSemantic.Attack) { break; }
                yield return null;
            }
            Assert.That(frame.Presentation.Semantic, Is.EqualTo(BehaviorSemantic.Attack));
            long action = frame.Presentation.ActionInstanceId;
            int attacks = bridge.AppliedAttackCount;
            Assert.That(attacks, Is.GreaterThan(0));
            bridge.enabled = false; bridge.enabled = true;
            Assert.That(bridge.AppliedAttackCount, Is.EqualTo(attacks));
            Assert.That(bridge.ReportMarker(handle, action), Is.True);
            Assert.That(bridge.ReportMarker(handle, action), Is.False);
            architecture.SendCommand(new ControlBehaviorCommand(handle, BehaviorControl.Interrupt)); yield return null; yield return null;
            Assert.That(bridge.ReportMarker(handle, action), Is.False);
            animator.enabled = false;
            Vector2 before = actor.transform.position;
            for (int i = 0; i < 45; i++) { architecture.GetSystem<BehaviorSystem>().Tick(_sample.ScopeId, .1f); }
            Assert.That((Vector2)actor.transform.position, Is.Not.EqualTo(before));
            Assert.That(architecture.SendQuery(new GetBehaviorSnapshotQuery(handle)).Summary, Does.Not.Contain("Faulted"));
            animator.enabled = true;
            var missing = new BehaviorAnimatorPresenter(null, null); missing.Bind(handle); missing.Apply(frame.Presentation);
            Assert.That(missing.AppliedAttackCount, Is.Zero);

            var wrong = new UnityEditor.Animations.AnimatorController();
            wrong.AddParameter("Moving", AnimatorControllerParameterType.Float);
            wrong.AddParameter("Attack", AnimatorControllerParameterType.Bool);
            RuntimeAnimatorController saved = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = wrong;
            var mismatch = new BehaviorAnimatorPresenter(animator, null); mismatch.Bind(handle); mismatch.Apply(frame.Presentation);
            Assert.That(mismatch.ValidationIssueCount, Is.EqualTo(2)); Assert.That(mismatch.AppliedAttackCount, Is.Zero);
            mismatch.Clear(); animator.runtimeAnimatorController = saved; UnityEngine.Object.Destroy(wrong);

            actor.gameObject.SetActive(false); actor.gameObject.SetActive(true); yield return null;
            Assert.That(actor.Handle.Equals(handle), Is.False);
            Assert.That(bridge.ReportMarker(handle, action), Is.False);
            Assert.That(architecture.GetModel<BehaviorModel>().Count, Is.EqualTo(2));
        }
    }
}
