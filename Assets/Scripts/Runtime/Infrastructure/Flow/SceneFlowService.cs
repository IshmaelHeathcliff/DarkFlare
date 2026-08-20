using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    public sealed class SceneFlowContinuePreparation
    {
        SceneFlowContinuePreparation(
            bool succeeded,
            PreparedRestore preparedRestore,
            Exception exception)
        {
            Succeeded = succeeded;
            PreparedRestore = preparedRestore;
            Exception = exception;
        }

        public bool Succeeded { get; }

        public Exception Exception { get; }

        internal PreparedRestore PreparedRestore { get; }

        public static SceneFlowContinuePreparation Success(PreparedRestore preparedRestore)
        {
            return new SceneFlowContinuePreparation(
                preparedRestore != null,
                preparedRestore,
                null);
        }

        public static SceneFlowContinuePreparation Failure(Exception exception = null)
        {
            return new SceneFlowContinuePreparation(false, null, exception);
        }
    }

    public interface ISceneFlowApplication
    {
        bool IsReady { get; }

        ContentCatalog ContentCatalog { get; }

        UniTask<SceneFlowContinuePreparation> PrepareContinueAsync(
            CancellationToken cancellationToken);

        IGameSessionInitializer CreateSessionInitializer(
            GameStartIntent intent,
            GameplaySceneConfiguration configuration,
            SceneFlowContinuePreparation preparation);

        UniTask<LifecycleResult> StartSessionAsync(
            Scene scene,
            IGameSessionInitializer initializer,
            CancellationToken cancellationToken);

        UniTask<SaveOperationResult> SaveBeforeExitAsync(
            CancellationToken cancellationToken);

        UniTask<LifecycleResult> StopSessionAsync();
    }

    public sealed class SceneFlowService : IDisposable
    {
        sealed class DirectProgress<T> : IProgress<T>
        {
            readonly Action<T> _handler;

            public DirectProgress(Action<T> handler)
            {
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            }

            public void Report(T value)
            {
                _handler.Invoke(value);
            }
        }

        sealed class QueuedRequest
        {
            int _completed;

            public QueuedRequest(
                SceneFlowRequest request,
                CancellationToken cancellationToken)
            {
                Request = request;
                CancellationToken = cancellationToken;
                Completion = new UniTaskCompletionSource<SceneFlowResult>();
            }

            public SceneFlowRequest Request { get; }

            public CancellationToken CancellationToken { get; }

            public UniTaskCompletionSource<SceneFlowResult> Completion { get; }

            public CancellationTokenSource ActiveCancellation { get; set; }

            public bool IsSuperseded { get; private set; }

            public void MarkSuperseded()
            {
                IsSuperseded = true;
                ActiveCancellation?.Cancel();
            }

            public void Complete(SceneFlowResult result)
            {
                if (Interlocked.Exchange(ref _completed, 1) == 0)
                {
                    Completion.TrySetResult(result);
                }
            }
        }

        readonly ISceneFlowApplication _application;
        readonly SceneFlowConfiguration _configuration;
        readonly ISceneLoader _loader;
        readonly GameTimeService _time;
        readonly CancellationToken _lifetimeToken;

        QueuedRequest _active;
        QueuedRequest _pending;
        UniTask _runner;
        SceneFlowPhase _activePhase;
        bool _runnerActive;
        bool _disposed;

        public SceneFlowService(
            ISceneFlowApplication application,
            SceneFlowConfiguration configuration,
            ISceneLoader loader,
            GameTimeService time,
            CancellationToken lifetimeToken = default)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _lifetimeToken = lifetimeToken;

            IReadOnlyList<string> errors = configuration.ValidateConfiguration();

            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    $"Scene Flow 配置无效：{string.Join("；", errors)}",
                    nameof(configuration));
            }

            State = GameFlowState.Boot;
            _time.PauseChanged += OnPauseChanged;
        }

        public GameFlowState State { get; private set; }

        public bool IsBusy => _runnerActive;

        public SceneFlowRequest? ActiveRequest => _active?.Request;

        public event Action<GameFlowState, GameFlowState> StateChanged;

        public event Action<SceneFlowProgress> ProgressChanged;

        public event Action<SceneFlowResult> RequestCompleted;

        public UniTask<SceneFlowResult> RequestAsync(
            SceneFlowRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_disposed || !_application.IsReady)
            {
                return UniTask.FromResult(CreateFailure(
                    request,
                    SceneFlowErrorCode.ApplicationUnavailable,
                    State,
                    SceneFlowPhase.Preparing,
                    SceneFlowRecoveryAction.Quit));
            }

            if (!request.IsValid)
            {
                return UniTask.FromResult(CreateFailure(
                    request,
                    SceneFlowErrorCode.InvalidState,
                    State,
                    SceneFlowPhase.Preparing));
            }

            QueuedRequest queued = new QueuedRequest(request, cancellationToken);
            QueuedRequest supersededPending = _pending;
            _pending = queued;
            _active?.MarkSuperseded();

            if (supersededPending != null)
            {
                supersededPending.MarkSuperseded();
                Complete(
                    supersededPending,
                    CreateFailure(
                        supersededPending.Request,
                        SceneFlowErrorCode.Superseded,
                        State,
                        SceneFlowPhase.Preparing));
            }

            if (!_runnerActive)
            {
                _runnerActive = true;
                _runner = RunQueueAsync().Preserve();
            }

            return queued.Completion.Task;
        }

        public void CancelActive()
        {
            _active?.ActiveCancellation?.Cancel();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _time.PauseChanged -= OnPauseChanged;
            _active?.ActiveCancellation?.Cancel();
            QueuedRequest pending = _pending;
            _pending = null;

            if (pending != null)
            {
                Complete(
                    pending,
                    CreateFailure(
                        pending.Request,
                        SceneFlowErrorCode.Cancelled,
                        State,
                        SceneFlowPhase.Preparing));
            }
        }

        async UniTask RunQueueAsync()
        {
            try
            {
                while (!_disposed && _pending != null)
                {
                    QueuedRequest request = _pending;
                    _pending = null;
                    _active = request;
                    SceneFlowResult result = await ExecuteRequestAsync(request);
                    Complete(request, result);

                    if (ReferenceEquals(_active, request))
                    {
                        _active = null;
                    }
                }
            }
            finally
            {
                _active = null;
                _runnerActive = false;

                if (!_disposed && _pending != null)
                {
                    _runnerActive = true;
                    _runner = RunQueueAsync().Preserve();
                }
            }
        }

        async UniTask<SceneFlowResult> ExecuteRequestAsync(QueuedRequest queued)
        {
            GameFlowState fromState = State;
            _activePhase = SceneFlowPhase.Preparing;
            CancellationTokenSource timeout = new CancellationTokenSource();
            IDisposable timeoutRegistration = timeout.CancelAfterSlim(
                _configuration.TransitionTimeout,
                DelayType.Realtime);
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken,
                queued.CancellationToken,
                timeout.Token);
            queued.ActiveCancellation = linked;

            try
            {
                if (queued.CancellationToken.IsCancellationRequested)
                {
                    return CreateFailure(
                        queued.Request,
                        SceneFlowErrorCode.Cancelled,
                        fromState,
                        SceneFlowPhase.Preparing);
                }

                if (!CanExecute(queued.Request, fromState))
                {
                    return CreateFailure(
                        queued.Request,
                        SceneFlowErrorCode.InvalidState,
                        fromState,
                        SceneFlowPhase.Preparing);
                }

                switch (queued.Request.Operation)
                {
                    case SceneFlowOperation.EnterFrontEnd:
                        return await EnterFrontEndAsync(queued, fromState, linked.Token);
                    case SceneFlowOperation.StartGame:
                        return await StartGameAsync(queued, fromState, linked.Token);
                    case SceneFlowOperation.ReturnToFrontEnd:
                        return await ReturnToFrontEndAsync(queued, fromState, linked.Token);
                    default:
                        return CreateFailure(
                            queued.Request,
                            SceneFlowErrorCode.InvalidState,
                            fromState,
                            SceneFlowPhase.Preparing);
                }
            }
            catch (OperationCanceledException exception)
            {
                SceneFlowErrorCode code = queued.IsSuperseded
                    ? SceneFlowErrorCode.Superseded
                    : timeout.IsCancellationRequested
                        ? SceneFlowErrorCode.Timeout
                        : SceneFlowErrorCode.Cancelled;
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    code,
                    ResolveFailedPhase(),
                    exception);
            }
            catch (Exception exception)
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    SceneFlowErrorCode.FatalCleanupFailed,
                    ResolveFailedPhase(),
                    exception);
            }
            finally
            {
                queued.ActiveCancellation = null;
                linked.Dispose();
                timeoutRegistration.Dispose();
                timeout.Dispose();
            }
        }

        async UniTask<SceneFlowResult> EnterFrontEndAsync(
            QueuedRequest queued,
            GameFlowState fromState,
            CancellationToken cancellationToken)
        {
            Report(SceneFlowPhase.Preparing, SceneId.Bootstrap, null, false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!_configuration.TryResolve(SceneId.Bootstrap, out string bootstrapPath)
                || !_loader.TryGetLoadedScene(bootstrapPath, out Scene bootstrapScene))
            {
                return CreateFailure(
                    queued.Request,
                    SceneFlowErrorCode.ConfigurationInvalid,
                    fromState,
                    SceneFlowPhase.Preparing,
                    SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit);
            }

            SceneLoaderResult activation = _loader.SetActive(bootstrapScene);

            if (!activation.Succeeded)
            {
                return CreateFailure(
                    queued.Request,
                    activation.ErrorCode,
                    fromState,
                    SceneFlowPhase.ActivatingScene,
                    SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                    activation.Exception);
            }

            LifecycleResult stop = await _application.StopSessionAsync();

            if (!stop.IsSuccess)
            {
                return CreateFailure(
                    queued.Request,
                    SceneFlowErrorCode.SessionStopFailed,
                    fromState,
                    SceneFlowPhase.StoppingSession,
                    SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                    stop.Exception);
            }

            if (_configuration.TryResolve(SceneId.Main, out string mainPath)
                && _loader.TryGetLoadedScene(mainPath, out Scene mainScene))
            {
                SceneLoaderResult unload = await _loader.UnloadAsync(
                    mainScene,
                    CancellationToken.None);

                if (!unload.Succeeded)
                {
                    SetState(GameFlowState.FatalError);
                    return CreateFailure(
                        queued.Request,
                        SceneFlowErrorCode.FatalCleanupFailed,
                        fromState,
                        SceneFlowPhase.UnloadingScene,
                        SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                        unload.Exception);
                }
            }

            _time.RestoreAll();
            SetState(GameFlowState.FrontEnd);
            Report(SceneFlowPhase.Completed, SceneId.Bootstrap, 1f, false);
            return SceneFlowResult.Success(
                queued.Request.Operation,
                fromState,
                GameFlowState.FrontEnd,
                SceneId.Bootstrap);
        }

        async UniTask<SceneFlowResult> StartGameAsync(
            QueuedRequest queued,
            GameFlowState fromState,
            CancellationToken cancellationToken)
        {
            SetState(GameFlowState.Loading);
            Report(SceneFlowPhase.Preparing, SceneId.Main, null, true);
            SceneFlowContinuePreparation preparation = null;

            if (queued.Request.StartIntent == GameStartIntent.Continue)
            {
                Report(SceneFlowPhase.PreparingSave, SceneId.Main, null, true);
                preparation = await _application.PrepareContinueAsync(cancellationToken);

                if (!preparation.Succeeded)
                {
                    return await RecoverAsync(
                        queued.Request,
                        fromState,
                        SceneFlowErrorCode.SavePrepareFailed,
                        SceneFlowPhase.PreparingSave,
                        preparation.Exception);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!_configuration.TryResolve(SceneId.Main, out string mainPath))
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    SceneFlowErrorCode.ConfigurationInvalid,
                    SceneFlowPhase.Preparing);
            }

            if (!_loader.IsSceneInBuild(mainPath))
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    SceneFlowErrorCode.SceneNotInBuild,
                    SceneFlowPhase.LoadingScene);
            }

            IProgress<float> loadProgress = new DirectProgress<float>(value =>
                Report(SceneFlowPhase.LoadingScene, SceneId.Main, value, true));
            Report(SceneFlowPhase.LoadingScene, SceneId.Main, 0f, true);
            SceneLoaderResult load = await _loader.LoadAsync(
                SceneId.Main,
                mainPath,
                loadProgress,
                cancellationToken);

            if (!load.Succeeded)
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    load.ErrorCode,
                    SceneFlowPhase.LoadingScene,
                    load.Exception);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report(SceneFlowPhase.ActivatingScene, SceneId.Main, null, false);
            SceneLoaderResult activation = _loader.SetActive(load.Scene);

            if (!activation.Succeeded)
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    activation.ErrorCode,
                    SceneFlowPhase.ActivatingScene,
                    activation.Exception);
            }

            if (!TryFindGameplayEntry(
                    load.Scene,
                    out GameplaySceneConfiguration gameplayConfiguration,
                    out SceneFlowErrorCode entryError))
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    entryError,
                    SceneFlowPhase.StartingSession);
            }

            cancellationToken.ThrowIfCancellationRequested();
            IGameSessionInitializer initializer = _application.CreateSessionInitializer(
                queued.Request.StartIntent,
                gameplayConfiguration,
                preparation);

            if (initializer == null)
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    SceneFlowErrorCode.ConfigurationInvalid,
                    SceneFlowPhase.StartingSession);
            }

            Report(SceneFlowPhase.StartingSession, SceneId.Main, null, false);
            LifecycleResult start = await _application.StartSessionAsync(
                load.Scene,
                initializer,
                cancellationToken);

            if (!start.IsSuccess)
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    SceneFlowErrorCode.SessionStartFailed,
                    SceneFlowPhase.StartingSession,
                    start.Exception);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SetState(GameFlowState.InGame);
            Report(SceneFlowPhase.Completed, SceneId.Main, 1f, false);
            return SceneFlowResult.Success(
                queued.Request.Operation,
                fromState,
                GameFlowState.InGame,
                SceneId.Main);
        }

        async UniTask<SceneFlowResult> ReturnToFrontEndAsync(
            QueuedRequest queued,
            GameFlowState fromState,
            CancellationToken cancellationToken)
        {
            SetState(GameFlowState.Loading);
            Report(SceneFlowPhase.SavingBeforeExit, SceneId.Main, null, false);
            SaveOperationResult save = await _application.SaveBeforeExitAsync(cancellationToken);

            if (save == null || !save.Succeeded)
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    SceneFlowErrorCode.SaveBeforeExitFailed,
                    SceneFlowPhase.SavingBeforeExit,
                    save?.Exception);
            }

            Report(SceneFlowPhase.StoppingSession, SceneId.Main, null, false);
            LifecycleResult stop = await _application.StopSessionAsync();

            if (!stop.IsSuccess)
            {
                return await RecoverAsync(
                    queued.Request,
                    fromState,
                    SceneFlowErrorCode.SessionStopFailed,
                    SceneFlowPhase.StoppingSession,
                    stop.Exception);
            }

            if (!_configuration.TryResolve(SceneId.Bootstrap, out string bootstrapPath)
                || !_loader.TryGetLoadedScene(bootstrapPath, out Scene bootstrapScene))
            {
                SetState(GameFlowState.FatalError);
                return CreateFailure(
                    queued.Request,
                    SceneFlowErrorCode.FatalCleanupFailed,
                    fromState,
                    SceneFlowPhase.ActivatingScene,
                    SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit);
            }

            SceneLoaderResult activation = _loader.SetActive(bootstrapScene);

            if (!activation.Succeeded)
            {
                SetState(GameFlowState.FatalError);
                return CreateFailure(
                    queued.Request,
                    SceneFlowErrorCode.FatalCleanupFailed,
                    fromState,
                    SceneFlowPhase.ActivatingScene,
                    SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                    activation.Exception);
            }

            Report(SceneFlowPhase.UnloadingScene, SceneId.Main, null, false);

            if (_configuration.TryResolve(SceneId.Main, out string mainPath)
                && _loader.TryGetLoadedScene(mainPath, out Scene mainScene))
            {
                SceneLoaderResult unload = await _loader.UnloadAsync(
                    mainScene,
                    CancellationToken.None);

                if (!unload.Succeeded)
                {
                    SetState(GameFlowState.FatalError);
                    return CreateFailure(
                        queued.Request,
                        SceneFlowErrorCode.FatalCleanupFailed,
                        fromState,
                        SceneFlowPhase.UnloadingScene,
                        SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                        unload.Exception);
                }
            }

            _time.RestoreAll();
            SetState(GameFlowState.FrontEnd);
            Report(SceneFlowPhase.Completed, SceneId.Bootstrap, 1f, false);
            return SceneFlowResult.Success(
                queued.Request.Operation,
                fromState,
                GameFlowState.FrontEnd,
                SceneId.Bootstrap);
        }

        async UniTask<SceneFlowResult> RecoverAsync(
            SceneFlowRequest request,
            GameFlowState stableState,
            SceneFlowErrorCode errorCode,
            SceneFlowPhase failedPhase,
            Exception exception = null)
        {
            if (State == GameFlowState.Loading)
            {
                SetState(GameFlowState.Recovering);
            }

            Report(SceneFlowPhase.Recovering, request.TargetScene, null, false);

            if (request.Operation == SceneFlowOperation.ReturnToFrontEnd
                && failedPhase == SceneFlowPhase.SavingBeforeExit
                && (stableState == GameFlowState.InGame
                    || stableState == GameFlowState.Paused))
            {
                SetState(stableState);
                return CreateFailure(
                    request,
                    errorCode,
                    stableState,
                    failedPhase,
                    SceneFlowRecoveryAction.Retry
                        | SceneFlowRecoveryAction.CancelTransition,
                    exception);
            }

            LifecycleResult stop = await _application.StopSessionAsync();

            if (!stop.IsSuccess)
            {
                SetState(GameFlowState.FatalError);
                return CreateFailure(
                    request,
                    SceneFlowErrorCode.FatalCleanupFailed,
                    stableState,
                    SceneFlowPhase.Recovering,
                    SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                    stop.Exception ?? exception);
            }

            if (_configuration.TryResolve(SceneId.Bootstrap, out string bootstrapPath)
                && _loader.TryGetLoadedScene(bootstrapPath, out Scene bootstrapScene))
            {
                SceneLoaderResult activation = _loader.SetActive(bootstrapScene);

                if (!activation.Succeeded)
                {
                    SetState(GameFlowState.FatalError);
                    return CreateFailure(
                        request,
                        SceneFlowErrorCode.FatalCleanupFailed,
                        stableState,
                        SceneFlowPhase.Recovering,
                        SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                        activation.Exception ?? exception);
                }
            }

            if (_configuration.TryResolve(SceneId.Main, out string mainPath)
                && _loader.TryGetLoadedScene(mainPath, out Scene mainScene))
            {
                SceneLoaderResult unload = await _loader.UnloadAsync(
                    mainScene,
                    CancellationToken.None);

                if (!unload.Succeeded)
                {
                    SetState(GameFlowState.FatalError);
                    return CreateFailure(
                        request,
                        SceneFlowErrorCode.FatalCleanupFailed,
                        stableState,
                        SceneFlowPhase.Recovering,
                        SceneFlowRecoveryAction.RetrySafeBoot | SceneFlowRecoveryAction.Quit,
                        unload.Exception ?? exception);
                }
            }

            _time.RestoreAll();
            SetState(GameFlowState.FrontEnd);
            SceneFlowRecoveryAction actions = errorCode == SceneFlowErrorCode.Cancelled
                || errorCode == SceneFlowErrorCode.Superseded
                ? SceneFlowRecoveryAction.None
                : SceneFlowRecoveryAction.Retry | SceneFlowRecoveryAction.Quit;
            return CreateFailure(
                request,
                errorCode,
                stableState,
                failedPhase,
                actions,
                exception);
        }

        bool CanExecute(SceneFlowRequest request, GameFlowState state)
        {
            switch (request.Operation)
            {
                case SceneFlowOperation.EnterFrontEnd:
                    return state == GameFlowState.Boot
                        || state == GameFlowState.FrontEnd;
                case SceneFlowOperation.StartGame:
                    return state == GameFlowState.FrontEnd;
                case SceneFlowOperation.ReturnToFrontEnd:
                    return state == GameFlowState.InGame || state == GameFlowState.Paused;
                default:
                    return false;
            }
        }

        void SetState(GameFlowState nextState)
        {
            if (State == nextState)
            {
                return;
            }

            if (!GameFlowTransitionRules.CanTransition(State, nextState))
            {
                throw new InvalidOperationException(
                    $"非法 Game Flow 状态转换：{State} → {nextState}");
            }

            GameFlowState previous = State;
            State = nextState;
            StateChanged?.Invoke(previous, nextState);
        }

        void OnPauseChanged(bool paused)
        {
            if (paused && State == GameFlowState.InGame)
            {
                SetState(GameFlowState.Paused);
            }
            else if (!paused && State == GameFlowState.Paused)
            {
                SetState(GameFlowState.InGame);
            }
        }

        void Report(
            SceneFlowPhase phase,
            SceneId sceneId,
            float? progress,
            bool canCancel)
        {
            _activePhase = phase;
            SceneFlowProgress value = progress.HasValue
                ? SceneFlowProgress.Measured(phase, sceneId, progress.Value, canCancel)
                : SceneFlowProgress.Indeterminate(phase, sceneId, canCancel);
            ProgressChanged?.Invoke(value);
        }

        SceneFlowPhase ResolveFailedPhase()
        {
            return _activePhase == SceneFlowPhase.None
                || _activePhase == SceneFlowPhase.Completed
                ? SceneFlowPhase.Preparing
                : _activePhase;
        }

        SceneFlowResult CreateFailure(
            SceneFlowRequest request,
            SceneFlowErrorCode errorCode,
            GameFlowState fromState,
            SceneFlowPhase phase,
            SceneFlowRecoveryAction recoveryActions = SceneFlowRecoveryAction.None,
            Exception exception = null)
        {
            SceneFlowOperation operation = request.Operation == SceneFlowOperation.None
                ? SceneFlowOperation.EnterFrontEnd
                : request.Operation;
            SceneId sceneId = request.TargetScene.IsValid
                ? request.TargetScene
                : SceneId.Bootstrap;
            return SceneFlowResult.Failure(
                operation,
                errorCode,
                fromState,
                ResolveTargetState(operation),
                phase,
                sceneId,
                recoveryActions,
                LocalizedMessage.Ui($"flow.error.{ToSnakeCase(errorCode.ToString())}"),
                exception);
        }

        void Complete(QueuedRequest request, SceneFlowResult result)
        {
            request.Complete(result);
            RequestCompleted?.Invoke(result);
        }

        static GameFlowState ResolveTargetState(SceneFlowOperation operation)
        {
            return operation == SceneFlowOperation.StartGame
                ? GameFlowState.InGame
                : GameFlowState.FrontEnd;
        }

        static bool TryFindGameplayEntry(
            Scene scene,
            out GameplaySceneConfiguration configuration,
            out SceneFlowErrorCode errorCode)
        {
            List<CombatPrototypeBootstrap> entries = new List<CombatPrototypeBootstrap>();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                entries.AddRange(roots[i].GetComponentsInChildren<CombatPrototypeBootstrap>(true));
            }

            if (entries.Count == 0)
            {
                configuration = null;
                errorCode = SceneFlowErrorCode.EntryMissing;
                return false;
            }

            if (entries.Count > 1)
            {
                configuration = null;
                errorCode = SceneFlowErrorCode.EntryDuplicate;
                return false;
            }

            configuration = entries[0].CreateSceneConfiguration();
            errorCode = SceneFlowErrorCode.None;
            return configuration != null;
        }

        static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            List<char> characters = new List<char>(value.Length + 8);

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (char.IsUpper(character) && i > 0)
                {
                    characters.Add('_');
                }

                characters.Add(char.ToLowerInvariant(character));
            }

            return new string(characters.ToArray());
        }
    }
}
