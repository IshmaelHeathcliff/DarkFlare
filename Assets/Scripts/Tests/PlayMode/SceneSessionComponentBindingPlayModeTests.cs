using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class SceneSessionComponentBindingPlayModeTests
    {
        const float TimeoutSeconds = 20f;

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        Scene _temporaryScene;
        LifecycleScope _isolatedApplicationScope;
        GameSessionHost _isolatedSession;
        UniTaskCompletionSource _isolatedTaskRelease;
        SceneSessionBinding _isolatedBinding;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            _isolatedBinding?.Disable();
            _isolatedBinding = null;
            _isolatedTaskRelease?.TrySetResult();

            if (_isolatedSession != null)
            {
                yield return _isolatedSession.StopAsync().ToCoroutine(result => { });
            }

            if (_isolatedApplicationScope != null)
            {
                yield return _isolatedApplicationScope.StopAsync().ToCoroutine();
            }

            if (_isolatedSession != null && _isolatedSession.IsCurrentArchitectureLease)
            {
                ResetArchitectureProvider();
            }

            _isolatedSession = null;
            _isolatedApplicationScope = null;
            _isolatedTaskRelease = null;

            if (_temporaryScene.IsValid() && _temporaryScene.isLoaded)
            {
                Scene mainScene = SceneManager.GetSceneByName("Main");

                if (!mainScene.IsValid() || !mainScene.isLoaded)
                {
                    yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
                    mainScene = SceneManager.GetSceneByName("Main");
                }

                SceneManager.SetActiveScene(mainScene);
                yield return SceneManager.UnloadSceneAsync(_temporaryScene);
                _temporaryScene = default;
            }

            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator MainToMain_PreplacedConsumersBindNewSessionExactlyOnce()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitForRunningSession();

            IArchitecture firstArchitecture = ApplicationHost.Current.CurrentSession.Architecture;
            GameInput firstInput = firstArchitecture.GetUtility<GameInput>();

            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitForRunningSession();

            IArchitecture secondArchitecture = ApplicationHost.Current.CurrentSession.Architecture;
            Assert.AreNotSame(firstArchitecture, secondArchitecture);
            Assert.AreNotSame(firstInput, secondArchitecture.GetUtility<GameInput>());
            Assert.IsFalse(firstInput.IsGameplayEnabled);
            Assert.IsFalse(firstInput.IsUiEnabled);
            AssertPreplacedConsumersUse(secondArchitecture);
            yield return AssertNewInputAndEventsDriveScene(secondArchitecture);
        }

        [UnityTest]
        public IEnumerator NoProviderEmptySceneToMain_ConsumersWaitForRunningSessionAndBindExactlyOnce()
        {
            _temporaryScene = SceneManager.CreateScene(
                $"SceneSessionBindingEmpty-{Time.frameCount}");
            SceneManager.SetActiveScene(_temporaryScene);
            Scene mainScene = SceneManager.GetSceneByName("Main");

            if (mainScene.IsValid() && mainScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(mainScene);
            }

            ApplicationHost host = ApplicationHost.Current;

            if (host.CurrentSession != null)
            {
                LifecycleResult stopResult = default;
                yield return host.StopCurrentSessionAsync()
                    .ToCoroutine(result => stopResult = result);
                Assert.IsTrue(stopResult.IsSuccess, stopResult.Message);
            }

            Assert.IsFalse(GameArchitectureProvider.HasCurrent);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
            mainScene = SceneManager.GetSceneByName("Main");
            SceneManager.SetActiveScene(mainScene);
            yield return WaitForRunningSession();

            IArchitecture architecture = ApplicationHost.Current.CurrentSession.Architecture;
            AssertPreplacedConsumersUse(architecture);
            yield return AssertNewInputAndEventsDriveScene(architecture);
            yield return SceneManager.UnloadSceneAsync(_temporaryScene);
            Assert.IsFalse(_temporaryScene.isLoaded);
            _temporaryScene = default;
        }

        [UnityTest]
        public IEnumerator RetryResult_DoesNotExposeFalseBindingAndBindsExactlyOnceAfterLaterFrame()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitForRunningSession();

            HudController owner = Object.FindAnyObjectByType<HudController>();
            IArchitecture architecture = ApplicationHost.Current.CurrentSession.Architecture;
            IArchitecture attemptedArchitecture = null;
            int attempts = 0;
            int unbinds = 0;
            SceneSessionBinding binding = new SceneSessionBinding(
                owner,
                candidate =>
                {
                    attempts++;
                    attemptedArchitecture = candidate;
                    return attempts == 1
                        ? SceneSessionBindResult.Retry
                        : SceneSessionBindResult.Success;
                },
                () => unbinds++);

            binding.Enable();

            Assert.AreEqual(1, attempts);
            Assert.AreSame(architecture, attemptedArchitecture);
            Assert.AreEqual(1, unbinds);
            Assert.IsFalse(binding.IsBound);
            Assert.AreEqual(0, binding.BindCount);
            Assert.Throws<System.InvalidOperationException>(() => binding.RequireArchitecture());

            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!binding.IsBound && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsTrue(binding.IsBound);
            Assert.AreEqual(2, attempts);
            Assert.AreEqual(1, unbinds);
            Assert.AreEqual(1, binding.BindCount);
            Assert.AreSame(architecture, binding.RequireArchitecture());
            yield return null;
            Assert.AreEqual(2, attempts);
            Assert.AreEqual(1, binding.BindCount);

            binding.Disable();
            Assert.IsFalse(binding.IsBound);
            Assert.AreEqual(2, unbinds);
        }

        [UnityTest]
        public IEnumerator BindingException_RollsBackWithoutReportingSuccess()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitForRunningSession();

            HudController owner = Object.FindAnyObjectByType<HudController>();
            int unbinds = 0;
            SceneSessionBinding binding = new SceneSessionBinding(
                owner,
                _ => throw new System.InvalidOperationException("scene-binding-probe"),
                () => unbinds++);

            LogAssert.Expect(LogType.Exception, new Regex("scene-binding-probe"));
            binding.Enable();

            Assert.IsFalse(binding.IsBound);
            Assert.AreEqual(0, binding.BindCount);
            Assert.AreEqual(1, unbinds);
            Assert.Throws<System.InvalidOperationException>(() => binding.RequireArchitecture());

            binding.Disable();
            Assert.AreEqual(1, unbinds);
        }

        [UnityTest]
        public IEnumerator TaintedScene_DisableEnableDoesNotRebindOrScheduleRetry()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitForRunningSession();

            ApplicationHost applicationHost = ApplicationHost.Current;
            HudController owner = Object.FindAnyObjectByType<HudController>();
            LifecycleResult originalStopResult = default;
            yield return applicationHost.StopCurrentSessionAsync()
                .ToCoroutine(result => originalStopResult = result);
            Assert.IsTrue(originalStopResult.IsSuccess, originalStopResult.Message);
            _isolatedApplicationScope = LifecycleScope.CreateRoot(
                "SceneSessionBinding-TaintedScene",
                taskStopTimeout: System.TimeSpan.FromMilliseconds(50d));
            LifecycleScope profileScope = _isolatedApplicationScope.CreateChild(
                "SceneSessionBinding-TaintedScene-Profile");
            _isolatedSession = CreateGameSessionHost(profileScope, 1);
            LifecycleResult sceneBindResult = _isolatedSession.BindScene(
                owner.gameObject.scene);
            Assert.IsTrue(sceneBindResult.IsSuccess, sceneBindResult.Message);
            LifecycleResult initializationResult = default;
            yield return _isolatedSession.InitializeAsync(
                    new ImmediateSessionInitializer())
                .ToCoroutine(result => initializationResult = result);
            Assert.IsTrue(initializationResult.IsSuccess, initializationResult.Message);
            int bindAttempts = 0;
            int unbinds = 0;
            _isolatedBinding = new SceneSessionBinding(
                owner,
                _ =>
                {
                    bindAttempts++;
                    return SceneSessionBindResult.Success;
                },
                () => unbinds++);

            _isolatedBinding.Enable();
            InvokeTryBind(_isolatedBinding, _isolatedSession);

            Assert.IsTrue(_isolatedBinding.IsBound);
            Assert.AreEqual(1, bindAttempts);
            Assert.AreEqual(1, _isolatedBinding.BindCount);

            _isolatedBinding.Disable();
            Assert.AreEqual(1, unbinds);
            LifecycleScope componentScope = _isolatedSession.SceneScope.CreateChild(
                "SceneSessionBinding-TaintedScene-Component");
            _isolatedTaskRelease = new UniTaskCompletionSource();
            componentScope.Tasks.Run(
                "ignore-component-cancellation",
                async token => await _isolatedTaskRelease.Task);
            yield return componentScope.StopAsync().ToCoroutine();

            Assert.AreEqual(LifecycleScopeState.Abandoned, componentScope.State);
            Assert.IsFalse(_isolatedSession.SceneScope.CanAcceptWork);
            _isolatedTaskRelease.TrySetResult();
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (componentScope.Tasks.TaskCount > 0
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.AreEqual(0, componentScope.Tasks.TaskCount);
            _isolatedBinding.Enable();
            InvokeTryBind(_isolatedBinding, _isolatedSession);
            yield return null;

            Assert.IsFalse(_isolatedBinding.IsBound);
            Assert.AreEqual(1, bindAttempts, "poisoned Scene 不得再次调用 bind");
            Assert.AreEqual(1, _isolatedBinding.BindCount);
            Assert.AreEqual(0, _isolatedSession.SceneScope.Tasks.TaskCount);
            Assert.AreEqual(1, unbinds);
        }

        static void InvokeTryBind(SceneSessionBinding binding, GameSessionHost session)
        {
            MethodInfo method = typeof(SceneSessionBinding).GetMethod(
                "TryBind",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "未找到 SceneSessionBinding.TryBind");
            method.Invoke(binding, new object[] { session });
        }

        static GameSessionHost CreateGameSessionHost(
            LifecycleScope profileScope,
            int sequence)
        {
            ConstructorInfo constructor = typeof(GameSessionHost).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(LifecycleScope),
                    typeof(int),
                    typeof(System.Action<GameSessionHost, string>),
                    typeof(IItemInstanceIdGenerator)
                },
                null);
            Assert.IsNotNull(constructor, "未找到 GameSessionHost 内部构造函数");
            return (GameSessionHost)constructor.Invoke(
                new object[] { profileScope, sequence, null, null });
        }

        static void ResetArchitectureProvider()
        {
            MethodInfo method = typeof(GameArchitectureProvider).GetMethod(
                "ResetStaticState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "未找到 GameArchitectureProvider.ResetStaticState");
            method.Invoke(null, null);
        }

        sealed class ImmediateSessionInitializer : IGameSessionInitializer
        {
            public string Name => "scene-session-binding-immediate";

            public UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                return UniTask.CompletedTask;
            }

            public UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                return UniTask.CompletedTask;
            }
        }

        static void AssertPreplacedConsumersUse(IArchitecture architecture)
        {
            HudController hud = Object.FindAnyObjectByType<HudController>();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            InventoryPanelController inventory = Object.FindAnyObjectByType<InventoryPanelController>();
            ShopPanelController shop = Object.FindAnyObjectByType<ShopPanelController>();
            CraftingPanelController crafting = Object.FindAnyObjectByType<CraftingPanelController>();
            InteractionPromptController prompt = Object.FindAnyObjectByType<InteractionPromptController>();
            WorldInteractionVisual[] interactionVisuals = Object.FindObjectsByType<WorldInteractionVisual>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Assert.IsNotNull(hud);
            Assert.IsNotNull(menu);
            Assert.IsNotNull(inventory);
            Assert.IsNotNull(shop);
            Assert.IsNotNull(crafting);
            Assert.IsNotNull(prompt);
            Assert.AreEqual(2, interactionVisuals.Length);
            Assert.AreEqual(1, hud.SessionBindCount);
            Assert.AreEqual(1, menu.SessionBindCount);
            Assert.AreEqual(1, prompt.SessionBindCount);
            Assert.AreSame(architecture, hud.GetArchitecture());
            Assert.AreSame(architecture, menu.GetArchitecture());
            Assert.AreSame(architecture, inventory.GetArchitecture());
            Assert.AreSame(architecture, shop.GetArchitecture());
            Assert.AreSame(architecture, crafting.GetArchitecture());
            Assert.AreSame(architecture, prompt.GetArchitecture());

            for (int i = 0; i < interactionVisuals.Length; i++)
            {
                Assert.AreEqual(1, interactionVisuals[i].SessionBindCount);
                Assert.AreSame(architecture, interactionVisuals[i].GetArchitecture());
            }
        }

        static IEnumerator AssertNewInputAndEventsDriveScene(IArchitecture architecture)
        {
            HudController hud = Object.FindAnyObjectByType<HudController>();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            GameInput input = architecture.GetUtility<GameInput>();
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            int previousGold = inventory.Gold;

            inventory.AddGold(7);
            Assert.AreEqual(previousGold + 7, hud.LastSnapshot.Gold);
            Assert.IsFalse(menu.IsOpen);
            input.SwitchToUi();
            yield return null;
            Assert.IsTrue(menu.IsOpen);
            input.SwitchToGameplay();
            yield return null;
            Assert.IsFalse(menu.IsOpen);
        }

        static IEnumerator WaitForRunningSession()
        {
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (Time.realtimeSinceStartup < timeout)
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                    && host.CurrentSession != null
                    && host.CurrentSession.State == GameSessionState.Running
                    && Object.FindAnyObjectByType<HudController>() is HudController hud
                    && hud.SessionBindCount == 1)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Session 未在时限内进入 Running");
        }
    }
}
