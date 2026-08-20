using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha024SceneFlowFoundationTests
    {
        float _originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
            GameTimeService.Shared.RestoreAll();
        }

        [TearDown]
        public void TearDown()
        {
            GameTimeService.Shared.RestoreAll();
            Time.timeScale = _originalTimeScale;
        }

        [Test]
        public void SceneFlowConfiguration_RequiresBootstrapAndMainWithUniqueMappings()
        {
            SceneFlowConfiguration valid = CreateConfiguration();

            try
            {
                Assert.That(valid.ValidateConfiguration(), Is.Empty);
                Assert.IsTrue(valid.TryResolve(SceneId.Bootstrap, out string bootstrapPath));
                Assert.AreEqual("Assets/Scenes/Bootstrap.unity", bootstrapPath);
                Assert.IsTrue(valid.TryResolve(SceneId.Main, out string mainPath));
                Assert.AreEqual("Assets/Scenes/Main.unity", mainPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(valid);
            }
        }

        [Test]
        public void GameTimeService_MultipleOwnersRestoreBaseScaleExactlyOnce()
        {
            GameTimeService service = new GameTimeService();
            service.SetBaseTimeScale(0.75f);
            List<bool> changes = new List<bool>();
            service.PauseChanged += changes.Add;
            GamePauseLease menu = service.AcquirePause("menu");
            GamePauseLease modal = service.AcquirePause("modal");

            Assert.IsTrue(service.IsPaused);
            Assert.AreEqual(2, service.PauseLeaseCount);
            Assert.AreEqual(0f, Time.timeScale);

            menu.Dispose();
            menu.Dispose();
            Assert.IsTrue(service.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            modal.Dispose();
            Assert.IsFalse(service.IsPaused);
            Assert.AreEqual(0.75f, Time.timeScale);
            CollectionAssert.AreEqual(new[] { true, false }, changes);
        }

        [UnityTest]
        public IEnumerator SceneFlow_NewGameAndPauseUseSingleStateOwner()
        {
            return VerifyNewGameAndPauseAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_LatestRequestSupersedesActiveAndCompletesExactlyOnce()
        {
            return VerifyLatestWinsAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_LoadFailureCompensatesToFrontEnd()
        {
            return VerifyLoadFailureAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_ContinuePrepareFailureNeverLoadsMain()
        {
            return VerifyContinuePrepareFailureAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_SaveFailureRestoresOriginalInGameState()
        {
            return VerifySaveFailureAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_SessionStartFailureCompensatesLoadedMain()
        {
            return VerifySessionStartFailureAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_SessionStopFailureEntersFatalError()
        {
            return VerifySessionStopFailureAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_UnloadFailureEntersFatalError()
        {
            return VerifyUnloadFailureAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_CancelledLoadCompensatesWithoutStartingSession()
        {
            return VerifyCancelledLoadAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SceneFlow_TimeoutReportsPhaseAndCompensates()
        {
            return VerifyTimeoutAsync().ToCoroutine();
        }

        static async UniTask VerifyNewGameAndPauseAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                SceneFlowResult frontEnd = await fixture.Service.RequestAsync(
                    SceneFlowRequest.EnterFrontEnd());
                SceneFlowResult start = await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));

                Assert.IsTrue(frontEnd.Succeeded);
                Assert.IsTrue(start.Succeeded);
                Assert.AreEqual(GameFlowState.InGame, fixture.Service.State);
                Assert.AreEqual(1, fixture.Application.StartCount);
                Assert.AreEqual(1, fixture.Loader.LoadCount);

                GamePauseLease lease = fixture.Time.AcquirePause("test-menu");
                Assert.AreEqual(GameFlowState.Paused, fixture.Service.State);
                lease.Dispose();
                Assert.AreEqual(GameFlowState.InGame, fixture.Service.State);
            }
        }

        static async UniTask VerifyLatestWinsAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                fixture.Loader.BlockFirstLoad = true;
                int completionCount = 0;
                fixture.Service.RequestCompleted += _ => completionCount++;
                UniTask<SceneFlowResult> first = fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));
                await fixture.Loader.FirstLoadStarted.Task;
                UniTask<SceneFlowResult> latest = fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));
                fixture.Loader.ReleaseFirstLoad();
                SceneFlowResult firstResult = await first;
                SceneFlowResult latestResult = await latest;

                Assert.AreEqual(SceneFlowErrorCode.Superseded, firstResult.ErrorCode);
                Assert.IsTrue(latestResult.Succeeded);
                Assert.AreEqual(GameFlowState.InGame, fixture.Service.State);
                Assert.AreEqual(2, fixture.Loader.LoadCount);
                Assert.AreEqual(1, fixture.Application.StartCount);
                Assert.AreEqual(2, completionCount);
            }
        }

        static async UniTask VerifyLoadFailureAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                fixture.Loader.NextLoadError = SceneFlowErrorCode.LoadFailed;
                SceneFlowResult result = await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));

                Assert.AreEqual(SceneFlowErrorCode.LoadFailed, result.ErrorCode);
                Assert.AreEqual(GameFlowState.FrontEnd, fixture.Service.State);
                Assert.AreEqual(0, fixture.Application.StartCount);
                Assert.GreaterOrEqual(fixture.Application.StopCount, 1);
            }
        }

        static async UniTask VerifyContinuePrepareFailureAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                SceneFlowResult result = await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.Continue));

                Assert.AreEqual(SceneFlowErrorCode.SavePrepareFailed, result.ErrorCode);
                Assert.AreEqual(GameFlowState.FrontEnd, fixture.Service.State);
                Assert.AreEqual(0, fixture.Loader.LoadCount);
                Assert.AreEqual(0, fixture.Application.StartCount);
            }
        }

        static async UniTask VerifySaveFailureAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));
                int stopsBeforeReturn = fixture.Application.StopCount;
                fixture.Application.SaveResult = SaveOperationResult.Failure(
                    SaveOperation.Save,
                    SaveCoordinator.AutoSlot,
                    SaveErrorCode.StorageFull);
                SceneFlowResult result = await fixture.Service.RequestAsync(
                    SceneFlowRequest.ReturnToFrontEnd());

                Assert.AreEqual(SceneFlowErrorCode.SaveBeforeExitFailed, result.ErrorCode);
                Assert.AreEqual(GameFlowState.InGame, fixture.Service.State);
                Assert.IsTrue(result.HasRecoveryAction(SceneFlowRecoveryAction.Retry));
                Assert.IsTrue(result.HasRecoveryAction(
                    SceneFlowRecoveryAction.CancelTransition));
                Assert.AreEqual(stopsBeforeReturn, fixture.Application.StopCount);
            }
        }

        static async UniTask VerifySessionStartFailureAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                fixture.Application.StartResult = LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "expected-start-failure");
                SceneFlowResult result = await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));

                Assert.AreEqual(SceneFlowErrorCode.SessionStartFailed, result.ErrorCode);
                Assert.AreEqual(GameFlowState.FrontEnd, fixture.Service.State);
                Assert.IsFalse(fixture.Loader.IsMainLoaded);
                Assert.AreEqual(1, fixture.Application.StartCount);
            }
        }

        static async UniTask VerifySessionStopFailureAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));
                fixture.Application.StopResult = LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "expected-stop-failure");
                SceneFlowResult result = await fixture.Service.RequestAsync(
                    SceneFlowRequest.ReturnToFrontEnd());

                Assert.AreEqual(SceneFlowErrorCode.FatalCleanupFailed, result.ErrorCode);
                Assert.AreEqual(GameFlowState.FatalError, fixture.Service.State);
            }
        }

        static async UniTask VerifyUnloadFailureAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));
                fixture.Loader.NextUnloadError = SceneFlowErrorCode.UnloadFailed;
                SceneFlowResult result = await fixture.Service.RequestAsync(
                    SceneFlowRequest.ReturnToFrontEnd());

                Assert.AreEqual(SceneFlowErrorCode.FatalCleanupFailed, result.ErrorCode);
                Assert.AreEqual(GameFlowState.FatalError, fixture.Service.State);
            }
        }

        static async UniTask VerifyCancelledLoadAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                fixture.Loader.BlockFirstLoad = true;
                UniTask<SceneFlowResult> request = fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));
                await fixture.Loader.FirstLoadStarted.Task;
                fixture.Service.CancelActive();
                SceneFlowResult result = await request;

                Assert.AreEqual(SceneFlowErrorCode.Cancelled, result.ErrorCode);
                Assert.AreEqual(GameFlowState.FrontEnd, fixture.Service.State);
                Assert.AreEqual(0, fixture.Application.StartCount);
                Assert.IsFalse(fixture.Loader.IsMainLoaded);
            }
        }

        static async UniTask VerifyTimeoutAsync()
        {
            using (SceneFlowFixture fixture = new SceneFlowFixture())
            {
                await fixture.Service.RequestAsync(SceneFlowRequest.EnterFrontEnd());
                fixture.SetTransitionTimeout(1f);
                fixture.Loader.BlockFirstLoad = true;
                SceneFlowResult result = await fixture.Service.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame));

                Assert.AreEqual(SceneFlowErrorCode.Timeout, result.ErrorCode);
                Assert.AreEqual(SceneFlowPhase.LoadingScene, result.Phase);
                Assert.AreEqual(GameFlowState.FrontEnd, fixture.Service.State);
                Assert.AreEqual(0, fixture.Application.StartCount);
                Assert.IsFalse(fixture.Loader.IsMainLoaded);
            }
        }

        static SceneFlowConfiguration CreateConfiguration()
        {
            SceneFlowConfiguration configuration = ScriptableObject.CreateInstance<SceneFlowConfiguration>();
            SerializedObject serialized = new SerializedObject(configuration);
            SerializedProperty scenes = serialized.FindProperty("_scenes");
            scenes.arraySize = 2;
            SetRegistration(
                scenes.GetArrayElementAtIndex(0),
                "bootstrap",
                "Assets/Scenes/Bootstrap.unity");
            SetRegistration(
                scenes.GetArrayElementAtIndex(1),
                "main",
                "Assets/Scenes/Main.unity");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return configuration;
        }

        static void SetRegistration(
            SerializedProperty registration,
            string sceneId,
            string scenePath)
        {
            registration.FindPropertyRelative("_sceneId").stringValue = sceneId;
            registration.FindPropertyRelative("_scenePath").stringValue = scenePath;
        }

        sealed class SceneFlowFixture : IDisposable
        {
            readonly GameObject _entryObject;
            readonly SceneFlowConfiguration _configuration;
            readonly Scene _bootstrapScene;
            readonly Scene _mainScene;

            public SceneFlowFixture()
            {
                _bootstrapScene = SceneManager.GetActiveScene();
                _mainScene = _bootstrapScene;
                _entryObject = FindOrCreateGameplayEntry(_mainScene);
                _configuration = CreateConfiguration();
                Time = new GameTimeService();
                Application = new FakeApplication();
                Loader = new FakeSceneLoader(_bootstrapScene, _mainScene);
                Service = new SceneFlowService(
                    Application,
                    _configuration,
                    Loader,
                    Time);
            }

            public GameTimeService Time { get; }

            public FakeApplication Application { get; }

            public FakeSceneLoader Loader { get; }

            public SceneFlowService Service { get; }

            public void SetTransitionTimeout(float seconds)
            {
                SerializedObject serialized = new SerializedObject(_configuration);
                serialized.FindProperty("_transitionTimeoutSeconds").floatValue = seconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Dispose()
            {
                Service.Dispose();
                Time.RestoreAll();
                if (_entryObject != null && _entryObject.name == "Alpha024GameplayEntry")
                {
                    UnityEngine.Object.DestroyImmediate(_entryObject);
                }

                UnityEngine.Object.DestroyImmediate(_configuration);
            }

            static GameObject FindOrCreateGameplayEntry(Scene scene)
            {
                GameObject[] roots = scene.GetRootGameObjects();

                for (int i = 0; i < roots.Length; i++)
                {
                    CombatPrototypeBootstrap entry =
                        roots[i].GetComponentInChildren<CombatPrototypeBootstrap>(true);

                    if (entry != null)
                    {
                        return entry.gameObject;
                    }
                }

                GameObject created = new GameObject("Alpha024GameplayEntry");
                created.AddComponent<CombatPrototypeBootstrap>();
                SceneManager.MoveGameObjectToScene(created, scene);
                return created;
            }
        }

        sealed class FakeSceneLoader : ISceneLoader
        {
            readonly Scene _bootstrapScene;
            readonly Scene _mainScene;
            bool _mainLoaded;
            UniTaskCompletionSource _firstLoadGate = new UniTaskCompletionSource();

            public FakeSceneLoader(Scene bootstrapScene, Scene mainScene)
            {
                _bootstrapScene = bootstrapScene;
                _mainScene = mainScene;
            }

            public int LoadCount { get; private set; }

            public bool BlockFirstLoad { get; set; }

            public bool IsMainLoaded => _mainLoaded;

            public SceneFlowErrorCode NextLoadError { get; set; }

            public SceneFlowErrorCode NextUnloadError { get; set; }

            public UniTaskCompletionSource FirstLoadStarted { get; } =
                new UniTaskCompletionSource();

            public bool IsSceneInBuild(string scenePath)
            {
                return true;
            }

            public bool TryGetLoadedScene(string scenePath, out Scene scene)
            {
                if (scenePath.EndsWith("Bootstrap.unity", StringComparison.Ordinal))
                {
                    scene = _bootstrapScene;
                    return true;
                }

                scene = _mainScene;
                return _mainLoaded;
            }

            public async UniTask<SceneLoaderResult> LoadAsync(
                SceneId sceneId,
                string scenePath,
                IProgress<float> progress,
                CancellationToken cancellationToken)
            {
                LoadCount++;

                if (LoadCount == 1 && BlockFirstLoad)
                {
                    FirstLoadStarted.TrySetResult();
                    await _firstLoadGate.Task.AttachExternalCancellation(cancellationToken);
                }

                if (NextLoadError != SceneFlowErrorCode.None)
                {
                    SceneFlowErrorCode error = NextLoadError;
                    NextLoadError = SceneFlowErrorCode.None;
                    return SceneLoaderResult.Failure(error);
                }

                _mainLoaded = true;
                progress?.Report(1f);
                return SceneLoaderResult.Success(_mainScene);
            }

            public SceneLoaderResult SetActive(Scene scene)
            {
                return SceneLoaderResult.Success(scene);
            }

            public UniTask<SceneLoaderResult> UnloadAsync(
                Scene scene,
                CancellationToken cancellationToken)
            {
                if (NextUnloadError != SceneFlowErrorCode.None)
                {
                    SceneFlowErrorCode error = NextUnloadError;
                    NextUnloadError = SceneFlowErrorCode.None;
                    return UniTask.FromResult(SceneLoaderResult.Failure(error));
                }

                if (scene == _mainScene)
                {
                    _mainLoaded = false;
                }

                return UniTask.FromResult(SceneLoaderResult.Success(scene));
            }

            public void ReleaseFirstLoad()
            {
                _firstLoadGate.TrySetResult();
            }
        }

        sealed class FakeApplication : ISceneFlowApplication
        {
            public bool IsReady => true;

            public ContentCatalog ContentCatalog => null;

            public int StartCount { get; private set; }

            public int StopCount { get; private set; }

            public LifecycleResult StartResult { get; set; } =
                LifecycleResult.Success("started");

            public LifecycleResult StopResult { get; set; } =
                LifecycleResult.AlreadyCompleted("stopped");

            public SaveOperationResult SaveResult { get; set; } =
                SaveOperationResult.Success(SaveOperation.Save, SaveCoordinator.AutoSlot);

            public UniTask<SceneFlowContinuePreparation> PrepareContinueAsync(
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(SceneFlowContinuePreparation.Failure());
            }

            public IGameSessionInitializer CreateSessionInitializer(
                GameStartIntent intent,
                GameplaySceneConfiguration configuration,
                SceneFlowContinuePreparation preparation)
            {
                return new NoOpInitializer();
            }

            public UniTask<LifecycleResult> StartSessionAsync(
                Scene scene,
                IGameSessionInitializer initializer,
                CancellationToken cancellationToken)
            {
                StartCount++;
                return UniTask.FromResult(StartResult);
            }

            public UniTask<SaveOperationResult> SaveBeforeExitAsync(
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(SaveResult);
            }

            public UniTask<LifecycleResult> StopSessionAsync()
            {
                StopCount++;
                return UniTask.FromResult(StopResult);
            }
        }

        sealed class NoOpInitializer : IGameSessionInitializer
        {
            public string Name => "scene-flow-test";

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
    }
}
