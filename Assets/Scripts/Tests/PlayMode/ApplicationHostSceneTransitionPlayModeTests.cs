using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class ApplicationHostSceneTransitionPlayModeTests
    {
        const float TimeoutSeconds = 20f;

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();
        readonly List<Scene> _createdScenes = new List<Scene>();
        readonly List<RollbackGateInitializer> _rollbackGates =
            new List<RollbackGateInitializer>();

        GameSessionHost _abandonedSession;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            for (int i = 0; i < _rollbackGates.Count; i++)
            {
                _rollbackGates[i].ReleaseRollback();
            }

            if (_abandonedSession != null)
            {
                float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

                while (_abandonedSession.SessionScope.Tasks.TaskCount > 0
                       && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
            }

            if (ApplicationHost.TryGetCurrent(out ApplicationHost currentHost)
                && (currentHost.State == ApplicationLifecycleState.Shutdown
                    || currentHost.State == ApplicationLifecycleState.Failed))
            {
                UnityEngine.Object.Destroy(currentHost.gameObject);
                yield return null;
                RecreateApplicationHost();
                yield return null;
            }

            yield return _fixture.Restart();

            for (int i = _createdScenes.Count - 1; i >= 0; i--)
            {
                Scene scene = _createdScenes[i];

                if (scene.IsValid() && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            _rollbackGates.Clear();
            _createdScenes.Clear();
            _abandonedSession = null;
        }

        [UnityTest]
        public IEnumerator ThreeRapidSceneRequests_OnlyLatestRequestReachesRunning()
        {
            ApplicationHost host = ApplicationHost.Current;
            Scene firstScene = CreateScene("RapidFirst");
            Scene secondScene = CreateScene("RapidSecond");
            Scene latestScene = CreateScene("RapidLatest");
            BlockingInitializer firstInitializer = new BlockingInitializer("rapid-first");
            BlockingInitializer secondInitializer = new BlockingInitializer("rapid-second");
            ImmediateInitializer latestInitializer = new ImmediateInitializer("rapid-latest");
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;
            int latestCallbackCount = 0;
            int runningNotificationCount = 0;
            LifecycleResult firstResult = default;
            LifecycleResult secondResult = default;
            LifecycleResult latestResult = default;
            Action<GameSessionHost> onSessionRunning = session => runningNotificationCount++;
            host.SessionRunning += onSessionRunning;

            try
            {
                LifecycleResult firstSubmission = host.BeginSceneSessionInitialization(
                    firstScene,
                    firstInitializer,
                    onCompleted: result =>
                    {
                        firstCallbackCount++;
                        firstResult = result;
                    });
                LifecycleResult secondSubmission = host.BeginSceneSessionInitialization(
                    secondScene,
                    secondInitializer,
                    onCompleted: result =>
                    {
                        secondCallbackCount++;
                        secondResult = result;
                    });
                LifecycleResult latestSubmission = host.BeginSceneSessionInitialization(
                    latestScene,
                    latestInitializer,
                    onCompleted: result =>
                    {
                        latestCallbackCount++;
                        latestResult = result;
                    });

                Assert.IsTrue(firstSubmission.IsSuccess, firstSubmission.Message);
                Assert.IsTrue(secondSubmission.IsSuccess, secondSubmission.Message);
                Assert.IsTrue(latestSubmission.IsSuccess, latestSubmission.Message);
                yield return WaitForCondition(
                    () => firstCallbackCount == 1
                        && secondCallbackCount == 1
                        && latestCallbackCount == 1
                        && host.CurrentSession != null
                        && host.CurrentSession.State == GameSessionState.Running,
                    "连续场景请求未收敛到最新 Running Session");

                Assert.AreEqual(LifecycleResultCode.Cancelled, firstResult.Code);
                Assert.AreEqual(LifecycleResultCode.Cancelled, secondResult.Code);
                Assert.IsTrue(latestResult.IsSuccess, latestResult.Message);
                Assert.AreEqual(1, firstCallbackCount);
                Assert.AreEqual(1, secondCallbackCount);
                Assert.AreEqual(1, latestCallbackCount);
                Assert.AreEqual(1, runningNotificationCount);
                Assert.IsFalse(firstInitializer.Committed);
                Assert.IsFalse(secondInitializer.Committed);
                Assert.IsTrue(latestInitializer.Committed);
                Assert.IsTrue(host.CurrentSession.IsBoundToScene(latestScene));
            }
            finally
            {
                host.SessionRunning -= onSessionRunning;
            }
        }

        [UnityTest]
        public IEnumerator MainReloadDuringPreviousRollback_LatestInitializationEventuallyRuns()
        {
            ApplicationHost host = ApplicationHost.Current;
            Scene previousScene = CreateScene("ReloadPrevious");
            GameObject cancellationOwner = new GameObject("ReloadCancellationOwner");
            SceneManager.MoveGameObjectToScene(cancellationOwner, previousScene);
            RollbackGateInitializer previousInitializer = new RollbackGateInitializer();
            _rollbackGates.Add(previousInitializer);
            int previousCallbackCount = 0;
            LifecycleResult previousResult = default;
            int generationBeforeReload = host.CurrentSession.ArchitectureGeneration;
            LifecycleResult submission = host.BeginSceneSessionInitialization(
                previousScene,
                previousInitializer,
                cancellationOwner.GetCancellationTokenOnDestroy(),
                result =>
                {
                    previousCallbackCount++;
                    previousResult = result;
                });

            Assert.IsTrue(submission.IsSuccess, submission.Message);
            Assert.IsTrue(previousInitializer.Started);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;

            Scene mainScene = SceneManager.GetSceneByName("Main");
            CombatPrototypeBootstrap bootstrap =
                UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
            Assert.IsNotNull(bootstrap);
            GameplaySceneConfiguration configuration = bootstrap.CreateSceneConfiguration();
            LifecycleResult catalog = host.InstallContentCatalog(
                configuration.ContentCatalogDefinition);
            Assert.IsTrue(catalog.IsSuccess, catalog.Message);
            IGameSessionInitializer latestInitializer = host.CreateSessionInitializer(
                GameStartIntent.NewGame,
                configuration,
                null);
            int latestCallbackCount = 0;
            LifecycleResult latestResult = default;
            LifecycleResult latestSubmission = host.BeginSceneSessionInitialization(
                mainScene,
                latestInitializer,
                onCompleted: result =>
                {
                    latestCallbackCount++;
                    latestResult = result;
                });
            Assert.IsTrue(latestSubmission.IsSuccess, latestSubmission.Message);

            Assert.IsTrue(
                previousInitializer.RollbackStarted,
                "旧场景卸载后必须先进入受控回滚");
            Assert.AreEqual(0, previousCallbackCount, "回滚闸门释放前旧请求不应提前完成");
            previousInitializer.ReleaseRollback();
            yield return WaitForCondition(
                () => previousCallbackCount == 1 && latestCallbackCount == 1,
                "场景初始化请求未各自完成一次");

            Assert.AreEqual(LifecycleResultCode.Cancelled, previousResult.Code);
            Assert.IsTrue(latestResult.IsSuccess, latestResult.Message);
            Assert.AreEqual(1, previousCallbackCount);
            Assert.AreEqual(1, latestCallbackCount);
            Assert.IsNotNull(host.CurrentSession);
            Assert.AreEqual(GameSessionState.Running, host.CurrentSession.State);
            Assert.IsTrue(host.CurrentSession.IsBoundToScene(mainScene));
            Assert.Greater(host.CurrentSession.ArchitectureGeneration, generationBeforeReload);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ApplicationHost>(
                FindObjectsInactive.Include).Length);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude).Length);
        }

        [UnityTest]
        public IEnumerator DirectCreateBindInitialize_NotifiesExactlyOncePerGeneration()
        {
            ApplicationHost host = ApplicationHost.Current;
            LifecycleResult initialStop = default;
            yield return host.StopCurrentSessionAsync()
                .ToCoroutine(result => initialStop = result);
            Assert.IsTrue(initialStop.IsSuccess, initialStop.Message);
            List<int> notifiedGenerations = new List<int>();
            Action<GameSessionHost> onSessionRunning = session =>
                notifiedGenerations.Add(session.ArchitectureGeneration);
            host.SessionRunning += onSessionRunning;

            try
            {
                for (int i = 0; i < 2; i++)
                {
                    Scene scene = CreateScene($"DirectInitialization{i}");
                    LifecycleResult createResult = host.CreatePendingSession();
                    Assert.IsTrue(createResult.IsSuccess, createResult.Message);
                    LifecycleResult bindResult = host.BindCurrentSessionToScene(scene);
                    Assert.IsTrue(bindResult.IsSuccess, bindResult.Message);
                    ImmediateInitializer initializer = new ImmediateInitializer($"direct-{i}");
                    LifecycleResult initializationResult = default;
                    yield return host.InitializeCurrentSessionAsync(initializer)
                        .ToCoroutine(result => initializationResult = result);

                    Assert.IsTrue(initializationResult.IsSuccess, initializationResult.Message);
                    Assert.AreEqual(i + 1, notifiedGenerations.Count);
                    Assert.AreEqual(
                        host.CurrentSession.ArchitectureGeneration,
                        notifiedGenerations[i]);
                    LifecycleResult repeatedResult = default;
                    yield return host.InitializeCurrentSessionAsync(initializer)
                        .ToCoroutine(result => repeatedResult = result);
                    Assert.AreEqual(LifecycleResultCode.AlreadyCompleted, repeatedResult.Code);
                    Assert.AreEqual(i + 1, notifiedGenerations.Count);

                    if (i == 0)
                    {
                        LifecycleResult stopResult = default;
                        yield return host.StopCurrentSessionAsync()
                            .ToCoroutine(result => stopResult = result);
                        Assert.IsTrue(stopResult.IsSuccess, stopResult.Message);
                    }
                }

                Assert.AreEqual(2, notifiedGenerations.Count);
                Assert.Greater(notifiedGenerations[1], notifiedGenerations[0]);
            }
            finally
            {
                host.SessionRunning -= onSessionRunning;
            }
        }

        [UnityTest]
        public IEnumerator HangingRollback_SettlesRequestsAndShutdownWithoutStartingLatestGeneration()
        {
            LogAssert.ignoreFailingMessages = true;
            ApplicationHost host = ApplicationHost.Current;
            Scene blockedScene = CreateScene("HangingRollbackBlocked");
            Scene latestScene = CreateScene("HangingRollbackLatest");
            RollbackGateInitializer blockedInitializer = new RollbackGateInitializer();
            ImmediateInitializer latestInitializer = new ImmediateInitializer(
                "hanging-rollback-latest");
            _rollbackGates.Add(blockedInitializer);
            int blockedCallbackCount = 0;
            int latestCallbackCount = 0;
            LifecycleResult blockedResult = default;
            LifecycleResult latestResult = default;
            LifecycleResult blockedSubmission = host.BeginSceneSessionInitialization(
                blockedScene,
                blockedInitializer,
                onCompleted: result =>
                {
                    blockedCallbackCount++;
                    blockedResult = result;
                });

            Assert.IsTrue(blockedSubmission.IsSuccess, blockedSubmission.Message);
            Assert.IsTrue(blockedInitializer.Started);
            _abandonedSession = host.CurrentSession;
            int generation = _abandonedSession.ArchitectureGeneration;
            LifecycleResult latestSubmission = host.BeginSceneSessionInitialization(
                latestScene,
                latestInitializer,
                onCompleted: result =>
                {
                    latestCallbackCount++;
                    latestResult = result;
                });

            Assert.IsTrue(latestSubmission.IsSuccess, latestSubmission.Message);
            LifecycleResult shutdownResult = default;
            yield return host.ShutdownAsync()
                .ToCoroutine(result => shutdownResult = result);
            yield return WaitForCondition(
                () => blockedCallbackCount == 1 && latestCallbackCount == 1,
                "挂起回滚后场景请求回调未有界收敛");

            Assert.IsFalse(shutdownResult.IsSuccess, "挂起回滚不得被误报为成功关闭");
            Assert.AreEqual(ApplicationLifecycleState.Shutdown, host.State);
            Assert.IsFalse(blockedResult.IsSuccess, "被 supersede 的旧请求不得成功");
            Assert.AreEqual(LifecycleResultCode.Cancelled, latestResult.Code);
            Assert.AreEqual(1, blockedCallbackCount);
            Assert.AreEqual(1, latestCallbackCount);
            Assert.IsFalse(latestInitializer.Committed, "最新请求不得越过旧代 Abandoned 启动");
            Assert.AreEqual(GameSessionState.Abandoned, _abandonedSession.State);
            Assert.IsTrue(_abandonedSession.IsCurrentArchitectureLease);
            Assert.AreEqual(generation, GameArchitectureProvider.Generation);

            blockedInitializer.ReleaseRollback();
            yield return WaitForCondition(
                () => _abandonedSession.SessionScope.Tasks.TaskCount == 0,
                "迟到回滚释放后仍残留活动任务记录");

            Assert.AreEqual(1, blockedCallbackCount);
            Assert.AreEqual(1, latestCallbackCount);
            Assert.AreEqual(GameSessionState.Abandoned, _abandonedSession.State);
            Assert.IsTrue(_abandonedSession.IsCurrentArchitectureLease);
            Assert.AreEqual(generation, GameArchitectureProvider.Generation);
        }

        [UnityTest]
        public IEnumerator MultipleComponentTimeouts_StopSessionOnceAndPreserveAbandonedLease()
        {
            LogAssert.ignoreFailingMessages = true;
            ApplicationHost host = ApplicationHost.Current;
            LifecycleResult initialStopResult = default;
            yield return host.StopCurrentSessionAsync()
                .ToCoroutine(result => initialStopResult = result);
            Assert.IsTrue(initialStopResult.IsSuccess, initialStopResult.Message);

            LifecycleScope originalApplicationScope = host.ApplicationScope;
            yield return originalApplicationScope.StopAsync().ToCoroutine();

            LifecycleScope applicationScope = LifecycleScope.CreateRoot(
                "Timeout-Controlled-Stop-Application",
                CreateLifecycleFailureHandler(host),
                taskStopTimeout: TimeSpan.FromMilliseconds(100d));
            LifecycleScope profileScope = applicationScope.CreateChild(
                "Timeout-Controlled-Stop-Profile");
            SetInstanceField(host, "_applicationScope", applicationScope);
            SetInstanceField(host, "_profileScope", profileScope);
            LifecycleResult createResult = host.CreatePendingSession();
            Assert.IsTrue(createResult.IsSuccess, createResult.Message);
            Scene runningScene = CreateScene("Timeout-Controlled-Stop-Running");
            LifecycleResult bindingResult = host.BindCurrentSessionToScene(runningScene);
            Assert.IsTrue(bindingResult.IsSuccess, bindingResult.Message);
            ImmediateInitializer initializer = new ImmediateInitializer(
                "timeout-controlled-stop-running");
            LifecycleResult initializationResult = default;
            yield return host.InitializeCurrentSessionAsync(initializer)
                .ToCoroutine(result => initializationResult = result);
            Assert.IsTrue(initializationResult.IsSuccess, initializationResult.Message);
            GameSessionHost session = host.CurrentSession;
            Assert.AreEqual(GameSessionState.Running, session.State);
            Assert.IsTrue(session.IsBoundToScene(runningScene));
            CancellationToken sceneToken = session.SceneScope.Token;
            _abandonedSession = session;
            int generation = session.ArchitectureGeneration;
            LifecycleScope firstComponentScope = session.SceneScope.CreateChild(
                "Timeout-Controlled-Stop-Component-First");
            LifecycleScope secondComponentScope = session.SceneScope.CreateChild(
                "Timeout-Controlled-Stop-Component-Second");
            UniTaskCompletionSource firstRelease = new UniTaskCompletionSource();
            UniTaskCompletionSource secondRelease = new UniTaskCompletionSource();

            try
            {
                firstComponentScope.Tasks.Run(
                    "ignores-first-component-stop",
                    async token => await firstRelease.Task);
                secondComponentScope.Tasks.Run(
                    "ignores-second-component-stop",
                    async token => await secondRelease.Task);
                firstComponentScope.BeginStop();
                secondComponentScope.BeginStop();

                yield return WaitForCondition(
                    () => session.State == GameSessionState.Abandoned
                        && host.State == ApplicationLifecycleState.Failed
                        && CountControlledStopFailures(applicationScope) == 1,
                    "Component timeout 未自动且唯一地收敛当前 Session");

                Assert.AreEqual(LifecycleScopeState.Abandoned, firstComponentScope.State);
                Assert.AreEqual(LifecycleScopeState.Abandoned, secondComponentScope.State);
                Assert.AreEqual(LifecycleScopeState.Abandoned, session.SceneScope.State);
                Assert.AreEqual(GameSessionState.Abandoned, session.State);
                Assert.IsTrue(sceneToken.IsCancellationRequested, "SceneScope token 未取消，已绑定组件无法解绑");
                Assert.AreSame(session, host.CurrentSession);
                Assert.IsTrue(session.IsCurrentArchitectureLease);
                Assert.AreEqual(generation, GameArchitectureProvider.Generation);
                Assert.AreEqual(
                    1,
                    CountControlledStopFailures(applicationScope),
                    "多个 timeout 不得重复启动受控停止");

                firstRelease.TrySetResult();
                secondRelease.TrySetResult();
                yield return WaitForCondition(
                    () => firstComponentScope.Tasks.TaskCount == 0
                        && secondComponentScope.Tasks.TaskCount == 0,
                    "迟到 Component 任务未移出跟踪集");

                LifecycleResult shutdownResult = default;
                yield return host.ShutdownAsync()
                    .Timeout(TimeSpan.FromSeconds(2d), DelayType.Realtime)
                    .ToCoroutine(result => shutdownResult = result);
                Assert.IsFalse(shutdownResult.IsSuccess);
                Assert.AreEqual(ApplicationLifecycleState.Shutdown, host.State);
            }
            finally
            {
                firstRelease.TrySetResult();
                secondRelease.TrySetResult();
            }
        }

        [UnityTest]
        public IEnumerator EmergencyShutdown_LateControlledStopCannotOverwriteShutdown()
        {
            LogAssert.ignoreFailingMessages = true;
            ApplicationHost host = ApplicationHost.Current;
            Scene runningScene = CreateScene("Late-Controlled-Stop-Running");
            LifecycleResult bindingResult = host.BindCurrentSessionToScene(runningScene);
            Assert.IsTrue(bindingResult.IsSuccess, bindingResult.Message);
            CommittedRollbackGateInitializer initializer =
                new CommittedRollbackGateInitializer();
            LifecycleResult initializationResult = default;
            yield return host.InitializeCurrentSessionAsync(initializer)
                .ToCoroutine(result => initializationResult = result);
            Assert.IsTrue(initializationResult.IsSuccess, initializationResult.Message);
            GameSessionHost session = host.CurrentSession;
            LifecycleScope applicationScope = host.ApplicationScope;
            _abandonedSession = session;
            Assert.AreEqual(GameSessionState.Running, session.State);

            try
            {
                InvokeControlledSessionStop(host, session, "late-emergency-test");
                yield return WaitForCondition(
                    () => initializer.RollbackStarted,
                    "受控停止未进入可延迟的回滚阶段");

                InvokeEmergencyShutdown(host);
                Assert.AreEqual(ApplicationLifecycleState.Shutdown, host.State);
                Assert.IsNull(host.CurrentSession);

                initializer.ReleaseRollback();
                yield return WaitForCondition(
                    () => applicationScope.Tasks.TaskCount == 0
                        && session.SessionScope.Tasks.TaskCount == 0,
                    "应急关闭后的迟到受控停止未收敛");
                yield return null;
                yield return null;

                Assert.AreEqual(
                    ApplicationLifecycleState.Shutdown,
                    host.State,
                    "迟到受控停止失败不得覆盖 Shutdown 终态");
                Assert.IsNull(host.CurrentSession);
            }
            finally
            {
                initializer.ReleaseRollback();
            }
        }

        [UnityTest]
        public IEnumerator ControlledStop_CleanupFailureAfterSessionClearsMarksApplicationFailed()
        {
            LogAssert.ignoreFailingMessages = true;
            ApplicationHost host = ApplicationHost.Current;
            Scene runningScene = CreateScene("Controlled-Stop-Cleanup-Failure");
            LifecycleResult bindingResult = host.BindCurrentSessionToScene(runningScene);
            Assert.IsTrue(bindingResult.IsSuccess, bindingResult.Message);
            ThrowingRollbackInitializer initializer = new ThrowingRollbackInitializer();
            LifecycleResult initializationResult = default;
            yield return host.InitializeCurrentSessionAsync(initializer)
                .ToCoroutine(result => initializationResult = result);
            Assert.IsTrue(initializationResult.IsSuccess, initializationResult.Message);
            GameSessionHost session = host.CurrentSession;
            LifecycleScope applicationScope = host.ApplicationScope;
            Assert.AreEqual(GameSessionState.Running, session.State);

            InvokeControlledSessionStop(host, session, "cleanup-failure-test");
            yield return WaitForCondition(
                () => host.State == ApplicationLifecycleState.Failed
                    && host.CurrentSession == null
                    && CountControlledStopFailures(applicationScope) == 1,
                "Session 清理失败并清除引用后，Application 未进入 Failed");

            Assert.AreEqual(GameSessionState.None, session.State);
            Assert.IsNull(host.CurrentSession);
            Assert.AreEqual(ApplicationLifecycleState.Failed, host.State);
            Assert.IsFalse(GameArchitectureProvider.HasCurrent);
            Assert.AreEqual(1, CountControlledStopFailures(applicationScope));
        }

        [UnityTest]
        public IEnumerator ProfileScopeTimeout_ShutdownReturnsFailureAndKeepsDiagnostics()
        {
            return VerifyScopeTimeoutShutdownAsync(true);
        }

        [UnityTest]
        public IEnumerator ApplicationScopeTimeout_ShutdownReturnsFailureAndKeepsDiagnostics()
        {
            return VerifyScopeTimeoutShutdownAsync(false);
        }

        IEnumerator VerifyScopeTimeoutShutdownAsync(bool hangProfileScope)
        {
            ApplicationHost host = ApplicationHost.Current;
            LifecycleResult sessionStopResult = default;
            yield return host.StopCurrentSessionAsync()
                .ToCoroutine(result => sessionStopResult = result);
            Assert.IsTrue(sessionStopResult.IsSuccess, sessionStopResult.Message);

            LifecycleScope originalApplicationScope = host.ApplicationScope;
            yield return originalApplicationScope.StopAsync().ToCoroutine();

            const string ApplicationScopeName = "Shutdown-Timeout-Application";
            const string ProfileScopeName = "Shutdown-Timeout-Profile";
            const string OperationName = "shutdown-timeout-probe";
            LifecycleScope applicationScope = LifecycleScope.CreateRoot(
                ApplicationScopeName,
                taskStopTimeout: TimeSpan.FromMilliseconds(100d));
            LifecycleScope profileScope = applicationScope.CreateChild(ProfileScopeName);
            SetInstanceField(host, "_applicationScope", applicationScope);
            SetInstanceField(host, "_profileScope", profileScope);
            LifecycleScope targetScope = hangProfileScope ? profileScope : applicationScope;
            UniTaskCompletionSource release = new UniTaskCompletionSource();
            targetScope.Tasks.Run(
                OperationName,
                async token => await release.Task);

            int completionCount = 0;
            LifecycleResult shutdownResult = default;
            yield return host.ShutdownAsync()
                .Timeout(TimeSpan.FromSeconds(2d), DelayType.Realtime)
                .ToCoroutine(result =>
                {
                    completionCount++;
                    shutdownResult = result;
                });

            Assert.AreEqual(1, completionCount);
            Assert.IsFalse(shutdownResult.IsSuccess, "Abandoned scope 不得被误报为成功关闭");
            Assert.AreEqual(LifecycleResultCode.Failed, shutdownResult.Code);
            Assert.AreEqual(ApplicationLifecycleState.Shutdown, host.State);
            Assert.AreEqual(LifecycleScopeState.Abandoned, targetScope.State);
            Assert.IsNotNull(shutdownResult.Exception);
            StringAssert.Contains(targetScope.Name, shutdownResult.Exception.ToString());
            StringAssert.Contains(OperationName, shutdownResult.Exception.ToString());
            Assert.IsTrue(
                ContainsException<TimeoutException>(shutdownResult.Exception),
                "关闭结果未保留 scope timeout 诊断");

            Exception settledException = shutdownResult.Exception;
            release.TrySetResult();
            yield return WaitForCondition(
                () => targetScope.Tasks.TaskCount == 0,
                "迟到任务完成后未从作用域跟踪集移除");

            LifecycleResult repeatedResult = default;
            yield return host.ShutdownAsync()
                .ToCoroutine(result => repeatedResult = result);

            Assert.AreEqual(1, completionCount, "迟到任务不得再次完成关闭回调");
            Assert.IsFalse(repeatedResult.IsSuccess);
            Assert.AreSame(settledException, repeatedResult.Exception, "迟到任务不得改写已结算结果");
            Assert.AreEqual(ApplicationLifecycleState.Shutdown, host.State);
        }

        Scene CreateScene(string prefix)
        {
            Scene scene = SceneManager.CreateScene($"{prefix}-{Guid.NewGuid():N}");
            _createdScenes.Add(scene);
            return scene;
        }

        [UnityTest]
        public IEnumerator BootCancellation_DoesNotReclassifyCancelledLocalizationAsFailure()
        {
            yield return new SceneFlowPlayModeFixture().EnterFrontEnd();
            ApplicationHost host = ApplicationHost.Current;
            LocalizationService original = host.Localization;
            var runtime = new CancelledBootLocalization();
            var replacement = new LocalizationService(host.Settings, runtime);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            MethodInfo completeBoot = typeof(ApplicationHost).GetMethod(
                "CompleteBootAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(completeBoot);

            try
            {
                SetInstanceField(host, "_localizationService", replacement);
                UniTask operation = (UniTask)completeBoot.Invoke(host, new object[] { cancellation.Token });
                yield return operation.ToCoroutine();
                Assert.IsTrue(runtime.Called, "必须覆盖本地化返回 Cancelled 的路径");
                Assert.AreEqual(ApplicationLifecycleState.Ready, host.State,
                    "已取消的启动 continuation 不得覆盖宿主状态");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                replacement.Close();
                SetInstanceField(host, "_localizationService", original);
            }
        }

        sealed class CancelledBootLocalization : ILocalizationRuntime
        {
            public bool Called { get; private set; }
            public string AutomaticLocaleCode => "en";
            public bool IsLocaleAvailable(string localeCode)
            {
                return true;
            }
            public UniTask InitializeAsync(CancellationToken cancellationToken)
            {
                Called = true;
                return UniTask.FromCanceled(cancellationToken);
            }
            public UniTask ApplyLocaleAsync(string localeCode, IReadOnlyList<string> tables, CancellationToken token)
            {
                throw new InvalidOperationException("取消后不得切换语言");
            }
            public UniTask<string> GetStringAsync(string table, string key, string locale, IList<object> arguments, CancellationToken token)
            {
                throw new InvalidOperationException("取消后不得加载文本");
            }
            public string GetString(string table, string key, string locale, IList<object> arguments)
            {
                throw new InvalidOperationException("取消后不得读取文本");
            }
        }

        static void RecreateApplicationHost()
        {
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo reset = typeof(ApplicationBootstrap).GetMethod(
                "ResetStaticState",
                Flags);
            MethodInfo create = typeof(ApplicationBootstrap).GetMethod(
                "CreateApplicationHost",
                Flags);

            Assert.IsNotNull(reset, "未找到 ApplicationBootstrap.ResetStaticState");
            Assert.IsNotNull(create, "未找到 ApplicationBootstrap.CreateApplicationHost");
            reset.Invoke(null, null);
            create.Invoke(null, null);
        }

        static void SetInstanceField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        static Action<LifecycleTaskFailure> CreateLifecycleFailureHandler(ApplicationHost host)
        {
            MethodInfo method = typeof(ApplicationHost).GetMethod(
                "OnLifecycleTaskFailure",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "未找到 ApplicationHost.OnLifecycleTaskFailure");
            return (Action<LifecycleTaskFailure>)method.CreateDelegate(
                typeof(Action<LifecycleTaskFailure>),
                host);
        }

        static void InvokeControlledSessionStop(
            ApplicationHost host,
            GameSessionHost session,
            string reason)
        {
            MethodInfo method = typeof(ApplicationHost).GetMethod(
                "OnControlledSessionStopRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "未找到 ApplicationHost.OnControlledSessionStopRequested");
            method.Invoke(host, new object[]
            {
                session,
                reason
            });
        }

        static void InvokeEmergencyShutdown(ApplicationHost host)
        {
            MethodInfo method = typeof(ApplicationHost).GetMethod(
                "EmergencyShutdown",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "未找到 ApplicationHost.EmergencyShutdown");
            method.Invoke(host, null);
        }

        static int CountControlledStopFailures(LifecycleScope applicationScope)
        {
            IReadOnlyList<LifecycleTaskFailure> failures = applicationScope.Tasks.Failures;
            int count = 0;

            for (int i = 0; i < failures.Count; i++)
            {
                if (failures[i].OperationName.StartsWith(
                        "controlled-session-stop:",
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        static bool ContainsException<TException>(Exception exception)
            where TException : Exception
        {
            if (exception is TException)
            {
                return true;
            }

            if (exception is AggregateException aggregateException)
            {
                IReadOnlyList<Exception> innerExceptions = aggregateException.InnerExceptions;

                for (int i = 0; i < innerExceptions.Count; i++)
                {
                    if (ContainsException<TException>(innerExceptions[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            return exception?.InnerException != null
                && ContainsException<TException>(exception.InnerException);
        }

        static IEnumerator WaitForCondition(Func<bool> condition, string failureMessage)
        {
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!condition() && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), failureMessage);
        }

        sealed class BlockingInitializer : IGameSessionInitializer
        {
            readonly string _name;

            public BlockingInitializer(string name)
            {
                _name = name;
            }

            public string Name => _name;

            public bool Committed { get; private set; }

            public async UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
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
                return UniTask.CompletedTask;
            }
        }

        sealed class ImmediateInitializer : IGameSessionInitializer
        {
            readonly string _name;

            public ImmediateInitializer(string name)
            {
                _name = name;
            }

            public string Name => _name;

            public bool Committed { get; private set; }

            public UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Committed = true;
                return UniTask.CompletedTask;
            }

            public UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                Committed = false;
                return UniTask.CompletedTask;
            }
        }

        sealed class RollbackGateInitializer : IGameSessionInitializer
        {
            readonly UniTaskCompletionSource _rollbackRelease = new UniTaskCompletionSource();

            public string Name => "reload-rollback-gate";

            public bool Started { get; private set; }

            public bool RollbackStarted { get; private set; }

            public async UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                Started = true;
                await UniTask.Delay(
                    TimeSpan.FromSeconds(TimeoutSeconds),
                    DelayType.Realtime,
                    cancellationToken: token);
            }

            public async UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                RollbackStarted = true;
                await _rollbackRelease.Task;
            }

            public void ReleaseRollback()
            {
                _rollbackRelease.TrySetResult();
            }
        }

        sealed class CommittedRollbackGateInitializer : IGameSessionInitializer
        {
            readonly UniTaskCompletionSource _rollbackRelease = new UniTaskCompletionSource();

            public string Name => "committed-rollback-gate";

            public bool RollbackStarted { get; private set; }

            public UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return UniTask.CompletedTask;
            }

            public async UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                RollbackStarted = true;
                await _rollbackRelease.Task;
            }

            public void ReleaseRollback()
            {
                _rollbackRelease.TrySetResult();
            }
        }

        sealed class ThrowingRollbackInitializer : IGameSessionInitializer
        {
            public string Name => "throwing-rollback";

            public UniTask InitializeAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return UniTask.CompletedTask;
            }

            public UniTask RollbackAsync(
                SessionInitializationContext context,
                CancellationToken token)
            {
                throw new InvalidOperationException("controlled stop rollback failure");
            }
        }
    }
}
