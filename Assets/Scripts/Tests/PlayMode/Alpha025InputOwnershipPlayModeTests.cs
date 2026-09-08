using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class Alpha025InputOwnershipPlayModeTests
    {
        readonly SceneFlowPlayModeFixture _fixture = new SceneFlowPlayModeFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.EnterFrontEnd();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                && host.SceneFlow != null
                && (host.SceneFlow.State == GameFlowState.InGame
                    || host.SceneFlow.State == GameFlowState.Paused))
            {
                SceneFlowResult result = null;
                yield return host.SceneFlow.RequestAsync(
                        SceneFlowRequest.ReturnToFrontEnd())
                    .ToCoroutine(value => result = value);
                Assert.IsTrue(result.Succeeded, result.Exception?.ToString());
            }

            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BootstrapAndThreeSessionsShareOneRuntimeActionAsset()
        {
            ApplicationHost host = ApplicationHost.Current;
            ApplicationInputService input = host.Input;
            InputSystemUIInputModule inputModule =
                Object.FindAnyObjectByType<InputSystemUIInputModule>();
            ApplicationInputModuleBinder binder =
                Object.FindAnyObjectByType<ApplicationInputModuleBinder>();

            Assert.IsNotNull(input);
            Assert.IsFalse(input.IsClosed);
            Assert.IsNotNull(input.ActionAsset);
            Assert.IsNotNull(inputModule);
            Assert.IsTrue(inputModule.enabled);
            Assert.IsNotNull(binder);
            Assert.IsTrue(binder.IsBound);
            Assert.AreSame(input.ActionAsset, inputModule.actionsAsset);
            Assert.AreSame(input.ActionAsset, inputModule.submit.action.actionMap.asset);
            Assert.AreSame(input.ActionAsset, inputModule.move.action.actionMap.asset);
            Assert.AreEqual(InputContext.UI, input.CurrentContext);
            int actionAssetInstanceId = input.ActionAsset.GetInstanceID();

            for (int cycle = 0; cycle < 3; cycle++)
            {
                SceneFlowResult startResult = null;
                yield return host.SceneFlow.RequestAsync(
                        SceneFlowRequest.StartGame(GameStartIntent.NewGame))
                    .ToCoroutine(result => startResult = result);

                Assert.IsTrue(startResult.Succeeded, startResult.Exception?.ToString());
                Assert.AreEqual(actionAssetInstanceId, input.ActionAsset.GetInstanceID());
                Assert.AreSame(input.ActionAsset, inputModule.actionsAsset);
                Assert.AreEqual(InputContext.Gameplay, input.CurrentContext);
                Assert.IsTrue(input.IsGameplayEnabled);
                Assert.IsFalse(input.IsUiEnabled);
                Assert.IsNotNull(
                    host.CurrentSession.Architecture.GetUtility<GameInput>(),
                    $"cycle={cycle}");

                SceneFlowResult returnResult = null;
                yield return host.SceneFlow.RequestAsync(
                        SceneFlowRequest.ReturnToFrontEnd())
                    .ToCoroutine(result => returnResult = result);

                Assert.IsTrue(returnResult.Succeeded, returnResult.Exception?.ToString());
                Assert.AreEqual(actionAssetInstanceId, input.ActionAsset.GetInstanceID());
                Assert.AreSame(input.ActionAsset, inputModule.actionsAsset);
                Assert.AreEqual(InputContext.UI, input.CurrentContext);
                Assert.IsFalse(input.IsGameplayEnabled);
                Assert.IsTrue(input.IsUiEnabled);
                Assert.IsFalse(GameArchitectureProvider.HasCurrent);
            }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AudioAccessibilityAndPlatformServicesUseApplicationLifetime()
        {
            ApplicationHost host = ApplicationHost.Current;
            Assert.IsNotNull(host.Audio);
            Assert.IsTrue(host.Audio.IsReady);
            Assert.IsNotNull(Object.FindAnyObjectByType<AudioListener>());
            Assert.IsNotNull(host.Accessibility);
            Assert.AreEqual(
                host.Settings.Current.ReduceMotion,
                host.Accessibility.Profile.ReduceMotion);
            Assert.IsNotNull(host.PlatformLifecycle);
            yield return host.PlatformLifecycle.HandlePauseChangedAsync(false)
                .ToCoroutine(result => { });
            yield return host.PlatformLifecycle.HandleFocusChangedAsync(true)
                .ToCoroutine(result => { });
            Assert.AreEqual(PlatformLifecycleState.Active, host.PlatformLifecycle.State);
            AudioPlaybackResult playback = null;

            yield return host.Audio.PlayAsync(AudioCueIds.UiConfirm, this)
                .ToCoroutine(result => playback = result);

            Assert.IsTrue(playback.Succeeded, playback.Operation.Exception?.ToString());
            Assert.AreEqual(1, host.Audio.ActivePlaybackCount);
            Assert.AreEqual(1, host.Audio.LoadedHandleCount);
            Assert.AreEqual(AudioOperationCode.Success, playback.Handle.Stop().Code);
            Assert.AreEqual(0, host.Audio.ActivePlaybackCount);
            Assert.AreEqual(0, host.Audio.LoadedHandleCount);
            PlatformLifecycleResult focusLost = null;

            yield return host.PlatformLifecycle.HandleFocusChangedAsync(false)
                .ToCoroutine(result => focusLost = result);

            Assert.AreEqual(
                PlatformLifecycleResultCode.SessionUnavailable,
                focusLost.Code);
            Assert.IsTrue(host.Input.IsSuspended);
            Assert.IsTrue(host.Audio.IsSuspended);
            Assert.IsTrue(host.GameTime.IsPaused);
            PlatformLifecycleResult restored = null;

            yield return host.PlatformLifecycle.HandleFocusChangedAsync(true)
                .ToCoroutine(result => restored = result);

            Assert.IsTrue(restored.Succeeded);
            Assert.AreEqual(PlatformLifecycleState.Active, host.PlatformLifecycle.State);
            Assert.IsFalse(host.Input.IsSuspended);
            Assert.IsFalse(host.Audio.IsSuspended);
            Assert.IsFalse(host.GameTime.IsPaused);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SettingsPageOpensFromFrontEndAndPausedMenuWithoutReleasingPause()
        {
            ApplicationHost host = ApplicationHost.Current;
            ApplicationShellController shell = host.ApplicationShell;
            Assert.IsNotNull(shell);
            Button frontEndSettings = FindButton("front-end-settings");
            Button settingsBack = FindButton("settings-back");

            InvokeButton(frontEndSettings);
            yield return null;

            Assert.IsTrue(shell.IsSettingsOpen);
            Assert.AreEqual(InputContext.UI, host.Input.CurrentContext);
            InvokeButton(settingsBack);
            yield return null;
            Assert.IsFalse(shell.IsSettingsOpen);

            SceneFlowResult startResult = null;
            yield return host.SceneFlow.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame))
                .ToCoroutine(result => startResult = result);
            Assert.IsTrue(startResult.Succeeded, startResult.Exception?.ToString());

            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            Assert.IsNotNull(menu);
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(host.GameTime.IsPaused);
            menu.TogglePause();
            yield return null;
            Button menuSettings = FindButton("game-menu-settings");

            InvokeButton(menuSettings);
            yield return null;

            Assert.IsTrue(shell.IsSettingsOpen);
            Assert.IsFalse(menu.IsOpen, "设置接管时必须隐藏底层玩法菜单");
            Assert.AreEqual(InputContext.UI, host.Input.RequestedContext,
                "隐藏底层不能清除原来的菜单请求");
            Assert.AreEqual(InputContext.UI, host.Input.CurrentContext);
            Assert.IsTrue(host.GameTime.IsPaused);
            InvokeButton(FindButton("settings-back"));
            yield return null;
            Assert.IsFalse(shell.IsSettingsOpen);
            Assert.IsTrue(menu.IsOpen);
            Assert.AreEqual(InputContext.UI, host.Input.CurrentContext);
            Assert.IsTrue(host.GameTime.IsPaused);
        }

        static Button FindButton(string name)
        {
            UIDocument[] documents = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < documents.Length; i++)
            {
                Button button = documents[i].rootVisualElement?.Q<Button>(name);

                if (button != null)
                {
                    return button;
                }
            }

            Assert.Fail($"未找到 UI Button：{name}");
            return null;
        }

        static void InvokeButton(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new System.MissingMethodException(
                    typeof(Clickable).FullName,
                    "Invoke");
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
