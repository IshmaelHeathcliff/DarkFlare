using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class ApplicationLifecyclePlayModeTests
    {
        const float TimeoutSeconds = 20f;

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator RepeatedPlayInitialization_ReplacesIsolatedPathsWithoutRetainingOldInstallation()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                yield return host.ShutdownAsync().ToCoroutine();
                UnityEngine.Object.Destroy(host.gameObject);
                yield return null;
            }

            MethodInfo install = typeof(Alpha027PlayModeDataEnvironment).GetMethod(
                "Install", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(install);

            try
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    string previousRoot = Alpha027PlayModeDataEnvironment.RootPath;
                    Directory.CreateDirectory(previousRoot);
                    ApplicationDataPathProviderFactory.ResetForSubsystemRegistration();
                    install.Invoke(null, null);
                    Assert.AreNotEqual(previousRoot, Alpha027PlayModeDataEnvironment.RootPath);
                    Assert.IsFalse(Directory.Exists(previousRoot), "旧测试目录必须安全回收");
                    Assert.AreEqual(Alpha027PlayModeDataEnvironment.SaveRootPath,
                        ApplicationDataPathProviderFactory.CreateSavePathProvider().RootPath);
                    Assert.AreEqual(Alpha027PlayModeDataEnvironment.SettingsRootPath,
                        ApplicationDataPathProviderFactory.CreateSettingsPathProvider().RootPath);
                }
            }
            finally
            {
                Alpha027PlayModeDataEnvironment.ReinstallForTestRun();
                GameObject hostObject = new GameObject("[ApplicationHost]");
                hostObject.AddComponent<ApplicationHost>();
            }

            var sceneFlow = new SceneFlowPlayModeFixture();
            yield return sceneFlow.ReloadBootstrap();
            yield return sceneFlow.EnterFrontEnd();
        }

        [UnityTest]
        public IEnumerator MainColdStart_HasUniqueHostArchitecturePlayerAndCommittedSpawner()
        {
            yield return _fixture.EnterMain();
            yield return WaitForRunningSession();

            ApplicationHost host = ApplicationHost.Current;
            ApplicationHost[] hosts = UnityEngine.Object.FindObjectsByType<ApplicationHost>(
                FindObjectsInactive.Include);
            PlayerController[] players = UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude);
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            GameMenuController gameMenu =
                UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
            RuntimePanelView uiDocument = gameMenu != null
                ? gameMenu.GetComponent<RuntimePanelView>()
                : null;
            Label localizedMenuTitle = uiDocument?.Root.Q<Label>(
                className: "item-window-title");

            Assert.AreEqual(1, hosts.Length, "冷启动后必须只有一个 ApplicationHost");
            Assert.AreEqual(1, players.Length, "冷启动后必须只有一个有效玩家");
            Assert.IsNotNull(host.CurrentSession);
            Assert.AreEqual(GameSessionState.Running, host.CurrentSession.State);
            Assert.IsNotNull(host.ContentCatalog);
            Assert.IsNotNull(localizedMenuTitle);
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(localizedMenuTitle.text),
                "Application Ready 后静态本地化绑定必须已经产生首份文本");
            Assert.AreEqual("core", host.ContentCatalog.CatalogId);
            Assert.AreEqual(host.ContentCatalogDefinition.ContentVersion, host.ContentCatalog.ContentVersion);
            Assert.IsNotEmpty(host.ContentCatalog.GetAll<ItemBaseDefinition>());

            ContentCatalogDefinition installedDefinition = host.ContentCatalogDefinition;
            ContentCatalogDefinition equivalentCatalog = UnityEngine.Object.Instantiate(
                installedDefinition);
            LifecycleResult repeatedCatalog = host.InstallContentCatalog(equivalentCatalog);
            Assert.AreEqual(LifecycleResultCode.AlreadyCompleted, repeatedCatalog.Code);
            Assert.AreSame(installedDefinition, host.ContentCatalogDefinition);

            ContentCatalogDefinition futureCatalog = UnityEngine.Object.Instantiate(
                installedDefinition);
            FieldInfo contentVersionField = typeof(ContentCatalogDefinition).GetField(
                "_contentVersion",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(contentVersionField);
            contentVersionField.SetValue(futureCatalog, host.ContentCatalog.ContentVersion + 1);
            LifecycleResult replacementCatalog = host.InstallContentCatalog(futureCatalog);
            Assert.AreEqual(LifecycleResultCode.InvalidState, replacementCatalog.Code);
            Assert.AreSame(installedDefinition, host.ContentCatalogDefinition);
            UnityEngine.Object.Destroy(equivalentCatalog);
            UnityEngine.Object.Destroy(futureCatalog);
            Assert.AreSame(
                host.CurrentSession.Architecture,
                GameArchitectureProvider.RequireCurrent());
            Assert.IsNotNull(spawner);
            Assert.IsTrue(spawner.gameObject.activeInHierarchy);
            Assert.IsTrue(spawner.IsSpawning, "刷怪器必须只在初始化事务提交后进入运行状态");
        }

        [UnityTest]
        public IEnumerator ExternalInitializationCancellation_RollsBackAndNeverCommits()
        {
            ApplicationHost host = ApplicationHost.Current;
            BlockingInitializer initializer = new BlockingInitializer();
            LifecycleScope cancellationOwner = LifecycleScope.CreateRoot(
                "PlayMode-InitializationCancellation");
            UniTask<LifecycleResult> initialization = host.InitializeCurrentSessionAsync(
                initializer,
                cancellationOwner.Token);

            Assert.IsTrue(initializer.Started);
            cancellationOwner.BeginStop();
            LifecycleResult result = default;
            yield return initialization.ToCoroutine(value => result = value);
            yield return cancellationOwner.StopAsync().ToCoroutine();

            Assert.AreEqual(LifecycleResultCode.Cancelled, result.Code);
            Assert.IsFalse(initializer.Committed);
            Assert.AreEqual(1, initializer.RollbackCount);
            Assert.IsNotNull(host.CurrentSession);
            Assert.AreEqual(GameSessionState.None, host.CurrentSession.State);
            Assert.IsFalse(GameArchitectureProvider.HasCurrent);
        }

        [UnityTest]
        public IEnumerator ConcurrentInitializationRequest_IsRejectedSynchronously()
        {
            ApplicationHost host = ApplicationHost.Current;
            BlockingInitializer initializer = new BlockingInitializer();
            LifecycleScope cancellationOwner = LifecycleScope.CreateRoot(
                "PlayMode-ConcurrentInitialization");
            bool callbackReceived = false;
            LifecycleResult callbackResult = default;
            LifecycleResult firstSubmission = host.BeginCurrentSessionInitialization(
                initializer,
                cancellationOwner.Token,
                result =>
                {
                    callbackResult = result;
                    callbackReceived = true;
                });
            LifecycleResult secondSubmission = host.BeginCurrentSessionInitialization(
                new BlockingInitializer());

            Assert.IsTrue(firstSubmission.IsSuccess, firstSubmission.Message);
            Assert.AreEqual(
                LifecycleResultCode.OperationInProgress,
                secondSubmission.Code);
            cancellationOwner.BeginStop();
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!callbackReceived && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            yield return cancellationOwner.StopAsync().ToCoroutine();
            Assert.IsTrue(callbackReceived, "首个初始化请求取消后未返回结果");
            Assert.AreEqual(LifecycleResultCode.Cancelled, callbackResult.Code);
            Assert.AreEqual(1, initializer.RollbackCount);
            Assert.IsFalse(GameArchitectureProvider.HasCurrent);
        }

        [UnityTest]
        public IEnumerator PreCancelledInitializationRequest_ReturnsCallbackExactlyOnce()
        {
            ApplicationHost host = ApplicationHost.Current;
            BlockingInitializer initializer = new BlockingInitializer();
            LifecycleScope cancellationOwner = LifecycleScope.CreateRoot(
                "PlayMode-PreCancelledInitialization");
            CancellationToken cancelledToken = cancellationOwner.Token;
            cancellationOwner.BeginStop();
            int callbackCount = 0;
            LifecycleResult callbackResult = default;
            LifecycleResult submission = host.BeginCurrentSessionInitialization(
                initializer,
                cancelledToken,
                result =>
                {
                    callbackCount++;
                    callbackResult = result;
                });

            Assert.IsTrue(submission.IsSuccess, submission.Message);
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (callbackCount == 0 && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            yield return cancellationOwner.StopAsync().ToCoroutine();
            Assert.AreEqual(1, callbackCount);
            Assert.AreEqual(LifecycleResultCode.Cancelled, callbackResult.Code);
            Assert.IsFalse(initializer.Started);
        }

        [UnityTest]
        public IEnumerator InvalidNewGameConfiguration_RollsBackWithoutRuntimeObjects()
        {
            ApplicationHost host = ApplicationHost.Current;
            ContentCatalogDefinition contentCatalog = host.ContentCatalogDefinition;
            bool ownsCatalog = contentCatalog == null;
            if (ownsCatalog) { contentCatalog = ScriptableObject.CreateInstance<ContentCatalogDefinition>(); }
            LifecycleResult catalogResult = host.InstallContentCatalog(contentCatalog);
            Assert.IsTrue(catalogResult.IsSuccess, catalogResult.Message);
            GameplaySceneConfiguration configuration = new GameplaySceneConfiguration(
                contentCatalog,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                Vector3.zero,
                null,
                null,
                false,
                0);
            LifecycleResult result = default;
            LogAssert.Expect(
                LogType.Error,
                new Regex("\\[ApplicationHost\\] 生命周期任务失败:.*initialize:new-game"));
            yield return host.InitializeCurrentSessionAsync(
                    new NewGameSessionInitializer(configuration))
                .ToCoroutine(value => result = value);

            Assert.AreEqual(LifecycleResultCode.Failed, result.Code);
            Assert.IsInstanceOf<GameplaySceneConfigurationException>(result.Exception);
            Assert.IsNotNull(host.CurrentSession);
            Assert.AreEqual(GameSessionState.None, host.CurrentSession.State);
            Assert.IsFalse(GameArchitectureProvider.HasCurrent);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include).Length);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<MonsterController>(
                FindObjectsInactive.Include).Length);
            if (ownsCatalog) { UnityEngine.Object.Destroy(contentCatalog); }
        }

        [UnityTest]
        public IEnumerator ConcurrentStop_IsIdempotentAndClearsOnlyTheCapturedSession()
        {
            ApplicationHost host = ApplicationHost.Current;
            UniTask<LifecycleResult> firstStop = host.StopCurrentSessionAsync();
            UniTask<LifecycleResult> secondStop = host.StopCurrentSessionAsync();
            LifecycleResult firstResult = default;
            LifecycleResult secondResult = default;

            yield return firstStop.ToCoroutine(value => firstResult = value);
            yield return secondStop.ToCoroutine(value => secondResult = value);

            Assert.IsTrue(firstResult.IsSuccess, firstResult.Message);
            Assert.IsTrue(secondResult.IsSuccess, secondResult.Message);
            Assert.IsNull(host.CurrentSession);
            Assert.IsFalse(GameArchitectureProvider.HasCurrent);
        }

        [UnityTest]
        public IEnumerator ThreeSessionRestarts_DoNotDuplicateHostPlayerOrArchitecture()
        {
            int previousGeneration = GameArchitectureProvider.Generation - 1;

            for (int i = 0; i < 3; i++)
            {
                if (i > 0)
                {
                    yield return _fixture.Restart();
                }

                yield return _fixture.EnterMain();
                yield return WaitForRunningSession();

                Assert.Greater(GameArchitectureProvider.Generation, previousGeneration);
                previousGeneration = GameArchitectureProvider.Generation;
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ApplicationHost>(
                    FindObjectsInactive.Include).Length);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerController>(
                    FindObjectsInactive.Exclude).Length);
            }
        }

        [UnityTest]
        public IEnumerator DirectMainReload_CoordinatesStopCreateBindAndInitialize()
        {
            yield return _fixture.EnterMain();
            yield return WaitForRunningSession();
            int firstGeneration = GameArchitectureProvider.Generation;

            yield return _fixture.EnterMain();
            yield return WaitForRunningSession();

            Assert.Greater(GameArchitectureProvider.Generation, firstGeneration);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ApplicationHost>(
                FindObjectsInactive.Include).Length);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude).Length);
            Assert.AreEqual(GameSessionState.Running, ApplicationHost.Current.CurrentSession.State);
        }

        [UnityTest]
        public IEnumerator UnloadingBoundMainScene_StopsSessionAndReleasesArchitecture()
        {
            yield return _fixture.EnterMain();
            yield return WaitForRunningSession();

            Scene mainScene = SceneManager.GetSceneByName("Main");
            Scene emptyScene = SceneManager.CreateScene("LifecycleEmptyScene");
            SceneManager.SetActiveScene(emptyScene);
            yield return SceneManager.UnloadSceneAsync(mainScene);
            yield return WaitForNoCurrentSession();

            Assert.IsFalse(GameArchitectureProvider.HasCurrent);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include).Length);
        }

        [UnityTest]
        public IEnumerator DestroyedSessionObject_UnregistersFromRegistry()
        {
            SessionObjectRegistry registry = GameArchitectureProvider.RequireCurrent()
                .GetUtility<SessionObjectRegistry>();
            int countBefore = registry.Count;
            DamageNumberVisual visual = DamageNumberVisual.Spawn(
                Vector3.zero,
                10f,
                ActorTeam.Player,
                CombatTextKind.Damage);

            Assert.AreEqual(countBefore + 1, registry.Count);
            UnityEngine.Object.Destroy(visual.gameObject);
            yield return null;

            Assert.AreEqual(countBefore, registry.Count);
        }

        static IEnumerator WaitForRunningSession()
        {
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (Time.realtimeSinceStartup < timeout)
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                    && host.CurrentSession != null
                    && host.CurrentSession.State == GameSessionState.Running)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Session 未在时限内进入 Running");
        }

        static IEnumerator WaitForNoCurrentSession()
        {
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (Time.realtimeSinceStartup < timeout)
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                    && host.CurrentSession == null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("场景卸载后 Session 未在时限内停止");
        }

        sealed class BlockingInitializer : IGameSessionInitializer
        {
            public string Name => "blocking-test";

            public bool Started { get; private set; }

            public bool Committed { get; private set; }

            public int RollbackCount { get; private set; }

            public async UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                Started = true;
                await UniTask.Delay(
                    TimeSpan.FromSeconds(TimeoutSeconds),
                    DelayType.Realtime,
                    cancellationToken: token);
                Committed = true;
            }

            public UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                RollbackCount++;
                return UniTask.CompletedTask;
            }
        }
    }
}
