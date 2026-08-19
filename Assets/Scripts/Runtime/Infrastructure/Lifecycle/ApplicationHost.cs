using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public sealed class ApplicationHost : MonoBehaviour
    {
        const string DefaultProfileId = "local-default";
        const string StopTimeoutOperationName = "stop-timeout";
        static readonly TimeSpan SaveFlushTimeout = TimeSpan.FromSeconds(5);

        static ApplicationHost s_current;

        LifecycleScope _applicationScope;
        LifecycleScope _profileScope;
        GameSessionHost _session;
        GameSessionHost _controlledStopSession;
        ContentCatalogDefinition _contentCatalogDefinition;
        ContentCatalog _contentCatalog;
        IItemInstanceIdGenerator _itemInstanceIds;
        ISettingsPathProvider _settingsPathProvider;
        ISettingsSerializer _settingsSerializer;
        ILocalSettingsStorage _settingsStorage;
        SettingsService _settingsService;
        LocalizationService _localizationService;
        ISavePathProvider _savePathProvider;
        ISaveSerializer _saveSerializer;
        ILocalSaveStorage _saveStorage;
        SaveCoordinator _saveCoordinator;
        SessionSaveFacade _sessionSaveFacade;
        GameSessionHost _lastNotifiedSession;
        SceneSessionInitializationRequest _activeSceneRequest;
        SceneSessionInitializationRequest _pendingSceneRequest;
        LifecycleScope _activeSceneTransitionScope;
        bool _ownsSingleton;
        bool _shutdownStarted;
        bool _sceneTransitionInProgress;
        bool _quitGateArmed;
        bool _allowQuit;
        UniTaskCompletionSource<LifecycleResult> _shutdownCompletion;
        UniTask _shutdownRunner;
        int _sessionSequence;
        int _sceneTransitionSequence;
        int _lastNotifiedSessionGeneration;

        public static bool HasCurrent => s_current != null;

        public static ApplicationHost Current
        {
            get
            {
                if (s_current == null)
                {
                    throw new InvalidOperationException("ApplicationHost 尚未创建");
                }

                return s_current;
            }
        }

        public ApplicationLifecycleState State { get; private set; }

        public string ProfileId { get; private set; }

        public GameSessionHost CurrentSession => _session;

        public LifecycleScope ApplicationScope => _applicationScope;

        public LifecycleScope ProfileScope => _profileScope;

        public LifecycleScope CurrentSceneScope => _session?.SceneScope;

        public ContentCatalog ContentCatalog => _contentCatalog;

        public ContentCatalogDefinition ContentCatalogDefinition => _contentCatalogDefinition;

        public SettingsService Settings => _settingsService;

        public LocalizationService Localization => _localizationService;

        public SaveCoordinator SaveCoordinator => _saveCoordinator;

        public SessionSaveFacade SessionSaveFacade => _sessionSaveFacade;

        public event Action<GameSessionHost> SessionRunning;

        public static bool TryGetCurrent(out ApplicationHost host)
        {
            host = s_current;
            return host != null;
        }

        public static bool TryGetCurrentSceneScope(out LifecycleScope sceneScope)
        {
            sceneScope = s_current != null ? s_current.CurrentSceneScope : null;
            return sceneScope != null && sceneScope.State == LifecycleScopeState.Active;
        }

        public LifecycleResult InstallContentCatalog(ContentCatalogDefinition definition)
        {
            if (State == ApplicationLifecycleState.ShuttingDown
                || State == ApplicationLifecycleState.Shutdown)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ApplicationShuttingDown,
                    "Application 正在关闭");
            }

            if (State != ApplicationLifecycleState.Booting
                && State != ApplicationLifecycleState.Ready)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    $"Application 当前状态 {State} 不允许安装内容目录");
            }

            if (_contentCatalog != null)
            {
                if (ReferenceEquals(_contentCatalogDefinition, definition))
                {
                    return LifecycleResult.AlreadyCompleted(
                        $"内容目录 {_contentCatalog.CatalogId} 已安装");
                }

                ContentCatalogBuildResult candidate = ContentCatalog.Build(definition);

                if (!candidate.Succeeded)
                {
                    return CreateContentCatalogBuildFailure(candidate);
                }

                return candidate.Catalog.CatalogId == _contentCatalog.CatalogId
                    && candidate.Catalog.Version.Equals(_contentCatalog.Version)
                    ? LifecycleResult.AlreadyCompleted(
                        $"内容目录 {_contentCatalog.CatalogId} v{_contentCatalog.ContentVersion} 已安装")
                    : LifecycleResult.Failure(
                        LifecycleResultCode.InvalidState,
                        $"Application 已安装不可替换的内容目录 {_contentCatalog.CatalogId} "
                        + $"v{_contentCatalog.ContentVersion}");
            }

            ContentCatalogBuildResult buildResult = ContentCatalog.Build(definition);

            if (!buildResult.Succeeded)
            {
                return CreateContentCatalogBuildFailure(buildResult);
            }

            _contentCatalogDefinition = definition;
            _contentCatalog = buildResult.Catalog;
            EnsureSaveCoordinator();
            return LifecycleResult.Success(
                $"内容目录 {_contentCatalog.CatalogId} v{_contentCatalog.ContentVersion} 已安装");
        }

        static LifecycleResult CreateContentCatalogBuildFailure(ContentCatalogBuildResult buildResult)
        {
            List<string> issues = new List<string>(buildResult.Issues.Count);

            for (int i = 0; i < buildResult.Issues.Count; i++)
            {
                issues.Add(buildResult.Issues[i].Message);
            }

            return LifecycleResult.Failure(
                LifecycleResultCode.ValidationFailed,
                $"内容目录安装失败: {string.Join("；", issues)}");
        }

        public LifecycleResult CreatePendingSession()
        {
            if (State == ApplicationLifecycleState.ShuttingDown
                || State == ApplicationLifecycleState.Shutdown)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ApplicationShuttingDown,
                    "Application 正在关闭");
            }

            if (State != ApplicationLifecycleState.Booting
                && State != ApplicationLifecycleState.Ready)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    $"Application 当前状态 {State} 不允许创建 Session");
            }

            if (_session != null && _session.State != GameSessionState.None)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.OperationInProgress,
                    $"当前 Session 状态为 {_session.State}");
            }

            _sessionSequence++;

            try
            {
                _session = new GameSessionHost(
                    _profileScope,
                    _sessionSequence,
                    OnControlledSessionStopRequested,
                    _itemInstanceIds);
                _controlledStopSession = null;
                return LifecycleResult.Success("待初始化 Session 已创建");
            }
            catch (Exception exception)
            {
                _session = null;
                return LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "创建 Session 失败",
                    exception);
            }
        }

        public LifecycleResult BindCurrentSessionToScene(Scene scene)
        {
            if (State == ApplicationLifecycleState.ShuttingDown
                || State == ApplicationLifecycleState.Shutdown)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ApplicationShuttingDown,
                    "Application 正在关闭");
            }

            if (State != ApplicationLifecycleState.Ready || _session == null)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    "没有可绑定场景的 Session");
            }

            return _session.BindScene(scene);
        }

        public async UniTask<LifecycleResult> InitializeCurrentSessionAsync(
            IGameSessionInitializer initializer,
            CancellationToken externalToken = default)
        {
            if (State == ApplicationLifecycleState.ShuttingDown
                || State == ApplicationLifecycleState.Shutdown)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ApplicationShuttingDown,
                    "Application 正在关闭");
            }

            GameSessionHost session = _session;

            if (State != ApplicationLifecycleState.Ready || session == null)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    "没有可初始化的 Session");
            }

            if (initializer is IRequiresContentCatalog && _contentCatalog == null)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ValidationFailed,
                    $"Session initializer {initializer.Name} 需要先安装内容目录");
            }

            LifecycleResult result = await session.InitializeAsync(
                initializer,
                _contentCatalog,
                externalToken);

            if (result.IsSuccess)
            {
                NotifySessionRunning(session);
            }

            return result;
        }

        public LifecycleResult BeginCurrentSessionInitialization(
            IGameSessionInitializer initializer,
            CancellationToken externalToken = default,
            Action<LifecycleResult> onCompleted = null)
        {
            if (initializer == null)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ValidationFailed,
                    "Session initializer 不能为空");
            }

            if (State != ApplicationLifecycleState.Ready || _applicationScope == null)
            {
                return LifecycleResult.Failure(
                    State == ApplicationLifecycleState.ShuttingDown
                        || State == ApplicationLifecycleState.Shutdown
                        ? LifecycleResultCode.ApplicationShuttingDown
                        : LifecycleResultCode.InvalidState,
                    $"Application 当前状态 {State} 不允许初始化 Session");
            }

            if (_sceneTransitionInProgress)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.OperationInProgress,
                    "场景 Session 切换正在进行");
            }

            if (_session != null && _session.State != GameSessionState.Created
                && _session.State != GameSessionState.None)
            {
                LifecycleResultCode code = _session.State == GameSessionState.Initializing
                    || _session.State == GameSessionState.RollingBack
                    || _session.State == GameSessionState.Stopping
                    ? LifecycleResultCode.OperationInProgress
                    : LifecycleResultCode.InvalidState;
                return LifecycleResult.Failure(
                    code,
                    $"当前 Session 状态 {_session.State} 不接受新的初始化事务");
            }

            try
            {
                _applicationScope.Tasks.Run(
                    $"session-initialization-request:{initializer.Name}",
                    token => ExecuteInitializationRequestAsync(
                        initializer,
                        token,
                        onCompleted),
                    externalToken,
                    LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    "无法登记 Session 初始化任务",
                    exception);
            }

            return LifecycleResult.Success("Session 初始化请求已提交");
        }

        public LifecycleResult BeginSceneSessionInitialization(
            Scene scene,
            IGameSessionInitializer initializer,
            CancellationToken externalToken = default,
            Action<LifecycleResult> onCompleted = null)
        {
            if (initializer == null)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ValidationFailed,
                    "Session initializer 不能为空");
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ValidationFailed,
                    "只能为已经加载的有效场景提交 Session 初始化");
            }

            if ((State != ApplicationLifecycleState.Booting
                    && State != ApplicationLifecycleState.Ready)
                || _applicationScope == null
                || _applicationScope.State != LifecycleScopeState.Active)
            {
                return LifecycleResult.Failure(
                    State == ApplicationLifecycleState.ShuttingDown
                        || State == ApplicationLifecycleState.Shutdown
                        ? LifecycleResultCode.ApplicationShuttingDown
                        : LifecycleResultCode.InvalidState,
                    $"Application 当前状态 {State} 不允许切换场景 Session");
            }

            SceneSessionInitializationRequest request = new SceneSessionInitializationRequest(
                scene,
                initializer,
                externalToken,
                onCompleted);
            SceneSessionInitializationRequest supersededPending = _pendingSceneRequest;
            _pendingSceneRequest = request;
            _activeSceneRequest?.MarkSuperseded();
            _activeSceneTransitionScope?.BeginStop();

            if (supersededPending != null)
            {
                supersededPending.MarkSuperseded();
                CompleteSceneRequest(
                    supersededPending,
                    CreateSceneRequestCancellationResult(supersededPending));
            }

            if (State == ApplicationLifecycleState.Booting)
            {
                return LifecycleResult.Success("场景 Session 初始化请求已排队等待 Application Ready");
            }

            return StartSceneInitializationCoordinator();
        }

        LifecycleResult StartSceneInitializationCoordinator()
        {
            if (_pendingSceneRequest == null)
            {
                return LifecycleResult.AlreadyCompleted("当前没有待执行的场景 Session 初始化请求");
            }

            if (State != ApplicationLifecycleState.Ready
                || _applicationScope == null
                || _applicationScope.State != LifecycleScopeState.Active)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    $"Application 当前状态 {State} 不允许启动场景 Session 协调器");
            }

            if (_sceneTransitionInProgress)
            {
                return LifecycleResult.Success("场景 Session 初始化请求已更新");
            }

            _sceneTransitionInProgress = true;
            SceneSessionInitializationRequest request = _pendingSceneRequest;

            try
            {
                _applicationScope.Tasks.Run(
                    "scene-session-initialization-coordinator",
                    ExecuteSceneInitializationQueueAsync,
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(_pendingSceneRequest, request))
                {
                    _pendingSceneRequest = null;
                }

                _sceneTransitionInProgress = false;
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    "无法登记场景 Session 初始化任务",
                    exception);
            }

            return LifecycleResult.Success("场景 Session 初始化请求已提交");
        }

        public async UniTask<LifecycleResult> StopCurrentSessionAsync()
        {
            GameSessionHost session = _session;

            if (session == null)
            {
                return LifecycleResult.AlreadyCompleted("当前没有 Session");
            }

            _saveCoordinator?.UnbindSession(session);
            _sessionSaveFacade = null;
            LifecycleResult result = await session.StopAsync();

            if (ReferenceEquals(_session, session)
                && session.State == GameSessionState.None)
            {
                _session = null;
            }
            else if (ReferenceEquals(_session, session)
                && session.State == GameSessionState.Abandoned)
            {
                State = ApplicationLifecycleState.Failed;
            }

            return result;
        }

        public void BeginShutdown()
        {
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            _shutdownCompletion = new UniTaskCompletionSource<LifecycleResult>();
            State = ApplicationLifecycleState.ShuttingDown;
            _shutdownRunner = CompleteShutdownAsync().Preserve();
        }

        public UniTask<LifecycleResult> ShutdownAsync()
        {
            BeginShutdown();
            return _shutdownCompletion.Task;
        }

        internal static void ResetStaticState()
        {
            s_current = null;
        }

        void Awake()
        {
            if (s_current != null && s_current != this)
            {
                Destroy(gameObject);
                return;
            }

            s_current = this;
            _ownsSingleton = true;
            Application.wantsToQuit += OnWantsToQuit;
            DontDestroyOnLoad(gameObject);
            Boot();
        }

        void OnApplicationQuit()
        {
            BeginShutdown();
        }

        void OnDestroy()
        {
            if (!_ownsSingleton)
            {
                return;
            }

            Application.wantsToQuit -= OnWantsToQuit;

            BeginShutdown();

            if (State != ApplicationLifecycleState.Shutdown)
            {
                EmergencyShutdown();
            }

            if (s_current == this)
            {
                s_current = null;
            }
        }

        bool OnWantsToQuit()
        {
            if (_allowQuit || State == ApplicationLifecycleState.Shutdown)
            {
                return true;
            }

            if (!_quitGateArmed)
            {
                _quitGateArmed = true;
                BeginShutdown();
            }

            return false;
        }

        void Boot()
        {
            if (State != ApplicationLifecycleState.None)
            {
                return;
            }

            State = ApplicationLifecycleState.Booting;

            try
            {
                _applicationScope = LifecycleScope.CreateRoot(
                    "Application",
                    OnLifecycleTaskFailure);
                _settingsPathProvider = new PersistentSettingsPathProvider();
                _settingsSerializer = new NewtonsoftSettingsSerializer();
                _settingsStorage = new LocalSettingsStorage(
                    _settingsPathProvider,
                    _settingsSerializer);
                _settingsService = new SettingsService(_settingsStorage);
                SettingsOperationResult settingsResult = _settingsService.Initialize();

                if (!settingsResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"用户设置初始化失败：{settingsResult.Code}",
                        settingsResult.Exception);
                }

                _localizationService = new LocalizationService(_settingsService);
                _applicationScope.Tasks.Run(
                    "application-bootstrap",
                    CompleteBootAsync,
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                FailBoot(exception);
            }
        }

        async UniTask CompleteBootAsync(CancellationToken cancellationToken)
        {
            try
            {
                LocalizationOperationResult localizationResult =
                    await _localizationService.InitializeAsync(cancellationToken);

                if (!localizationResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"本地化初始化失败：{localizationResult.Code}",
                        localizationResult.Exception);
                }

                cancellationToken.ThrowIfCancellationRequested();
                ProfileId = DefaultProfileId;
                _profileScope = _applicationScope.CreateChild($"Profile-{ProfileId}");
                _itemInstanceIds = new UuidItemInstanceIdGenerator();
                _savePathProvider = new PersistentSavePathProvider();
                _saveSerializer = new NewtonsoftSaveSerializer();
                _saveStorage = new LocalSaveStorage(_savePathProvider, _saveSerializer);
                EnsureSaveCoordinator();
                LifecycleResult sessionResult = CreatePendingSession();

                if (!sessionResult.IsSuccess)
                {
                    throw new InvalidOperationException(sessionResult.Message);
                }

                State = ApplicationLifecycleState.Ready;
                LifecycleResult coordinatorResult = StartSceneInitializationCoordinator();

                if (!coordinatorResult.IsSuccess
                    && coordinatorResult.Code != LifecycleResultCode.AlreadyCompleted)
                {
                    CompletePendingSceneRequest(false);
                    Debug.LogError(
                        $"[ApplicationHost] 场景 Session 协调器启动失败：{coordinatorResult.Message}",
                        this);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                FailBoot(exception);
            }
        }

        void FailBoot(Exception exception)
        {
            State = ApplicationLifecycleState.Failed;
            Debug.LogException(exception, this);
            _session?.BeginStop();
            _profileScope?.BeginStop();
            _localizationService?.Close();
            _settingsService?.Close();
            _applicationScope?.BeginStop();
            CompletePendingSceneRequest(false);
        }

        void OnLifecycleTaskFailure(LifecycleTaskFailure failure)
        {
            Debug.LogError(
                $"[ApplicationHost] 生命周期任务失败: {failure.ScopeName}/{failure.OperationName}\n"
                + failure.Exception,
                this);

            if (failure.OperationName != StopTimeoutOperationName)
            {
                return;
            }

            GameSessionHost session = _session;

            if (session == null
                || session.State == GameSessionState.None
                || session.State == GameSessionState.RollingBack
                || session.State == GameSessionState.Stopping
                || session.State == GameSessionState.Abandoned)
            {
                return;
            }

            OnControlledSessionStopRequested(
                session,
                $"scope-timeout:{failure.ScopeName}");
        }

        void OnControlledSessionStopRequested(GameSessionHost session, string reason)
        {
            if (!ReferenceEquals(_session, session)
                || State != ApplicationLifecycleState.Ready
                || _applicationScope == null
                || _applicationScope.State != LifecycleScopeState.Active)
            {
                return;
            }

            if (ReferenceEquals(_controlledStopSession, session))
            {
                return;
            }

            _controlledStopSession = session;

            try
            {
                _applicationScope.Tasks.Run(
                    $"controlled-session-stop:{reason}",
                    token => StopControlledSessionAsync(session),
                    failurePolicy: LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                session.BeginStop();
                State = ApplicationLifecycleState.Failed;
            }
        }

        internal void RequestCurrentSessionStop(string reason)
        {
            GameSessionHost session = _session;

            if (session != null)
            {
                OnControlledSessionStopRequested(session, reason);
            }
        }

        async UniTask StopControlledSessionAsync(GameSessionHost session)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            LifecycleResult result = await StopCurrentSessionAsync();

            if (!result.IsSuccess)
            {
                if (State == ApplicationLifecycleState.Ready
                    && (_session == null || ReferenceEquals(_session, session)))
                {
                    State = ApplicationLifecycleState.Failed;
                }

                throw result.Exception ?? new InvalidOperationException(result.Message);
            }
        }

        async UniTask ExecuteInitializationRequestAsync(
            IGameSessionInitializer initializer,
            CancellationToken token,
            Action<LifecycleResult> onCompleted)
        {
            LifecycleResult result;

            if (token.IsCancellationRequested)
            {
                result = LifecycleResult.Failure(
                    LifecycleResultCode.Cancelled,
                    "Session 初始化请求在执行前已取消");
                onCompleted?.Invoke(result);
                return;
            }

            try
            {
                if (_session == null || _session.State == GameSessionState.None)
                {
                    result = CreatePendingSession();

                    if (!result.IsSuccess)
                    {
                        onCompleted?.Invoke(result);
                        return;
                    }
                }

                result = await InitializeCurrentSessionAsync(initializer, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                result = LifecycleResult.Failure(
                    LifecycleResultCode.Cancelled,
                    "Session 初始化请求已取消");
            }
            catch (Exception exception)
            {
                result = LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "Session 初始化请求执行异常",
                    exception);
                onCompleted?.Invoke(result);
                throw;
            }

            onCompleted?.Invoke(result);
        }

        async UniTask ExecuteSceneInitializationQueueAsync(CancellationToken applicationToken)
        {
            try
            {
                while (_pendingSceneRequest != null)
                {
                    if (applicationToken.IsCancellationRequested
                        || State != ApplicationLifecycleState.Ready)
                    {
                        CompletePendingSceneRequestAsCancelled();
                        break;
                    }

                    SceneSessionInitializationRequest request = _pendingSceneRequest;
                    _pendingSceneRequest = null;
                    _activeSceneRequest = request;
                    _sceneTransitionSequence++;
                    LifecycleScope transitionScope = null;
                    LifecycleResult result;

                    try
                    {
                        transitionScope = _applicationScope.CreateChild(
                            $"SceneTransition-{_sceneTransitionSequence}",
                            request.ExternalToken);
                        _activeSceneTransitionScope = transitionScope;
                        result = await ExecuteSceneInitializationRequestAsync(
                            request,
                            transitionScope);
                    }
                    catch (Exception exception)
                    {
                        OnLifecycleTaskFailure(new LifecycleTaskFailure(
                            _applicationScope?.Name ?? "Application",
                            $"scene-session-coordinator:{request.SceneName}",
                            exception));
                        result = LifecycleResult.Failure(
                            LifecycleResultCode.Failed,
                            $"场景 {request.SceneName} 的 Session 协调失败",
                            exception);
                    }
                    finally
                    {
                        if (transitionScope != null)
                        {
                            transitionScope.BeginStop();
                            await transitionScope.StopAsync();
                        }

                        if (ReferenceEquals(_activeSceneRequest, request))
                        {
                            _activeSceneRequest = null;
                        }

                        if (ReferenceEquals(_activeSceneTransitionScope, transitionScope))
                        {
                            _activeSceneTransitionScope = null;
                        }
                    }

                    if (request.IsSuperseded && result.IsSuccess)
                    {
                        result = CreateSceneRequestCancellationResult(request);
                    }

                    CompleteSceneRequest(request, result);
                }
            }
            finally
            {
                SceneSessionInitializationRequest incompleteActive = _activeSceneRequest;
                LifecycleScope incompleteScope = _activeSceneTransitionScope;
                _activeSceneRequest = null;
                _activeSceneTransitionScope = null;

                if (incompleteScope != null)
                {
                    incompleteScope.BeginStop();
                    await incompleteScope.StopAsync();
                }

                bool cancelled = applicationToken.IsCancellationRequested
                    || State != ApplicationLifecycleState.Ready;

                if (incompleteActive != null)
                {
                    CompleteSceneRequest(
                        incompleteActive,
                        CreateSceneCoordinatorTerminationResult(
                            incompleteActive,
                            cancelled));
                }

                CompletePendingSceneRequest(cancelled);
                _sceneTransitionInProgress = false;
            }
        }

        async UniTask<LifecycleResult> ExecuteSceneInitializationRequestAsync(
            SceneSessionInitializationRequest request,
            LifecycleScope transitionScope)
        {
            LifecycleResult result = default;

            try
            {
                LifecycleTaskHandle handle = transitionScope.Tasks.Run(
                    $"scene-session-initialization:{request.SceneName}:{request.Initializer.Name}",
                    async token =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            result = CreateSceneRequestCancellationResult(request);
                            return;
                        }

                        try
                        {
                            result = await PrepareAndInitializeSceneAsync(
                                request.Scene,
                                request.Initializer,
                                token);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            result = CreateSceneRequestCancellationResult(request);
                        }
                    },
                    failurePolicy: LifecycleTaskFailurePolicy.Propagate);
                await handle.WaitAsync();
                return result;
            }
            catch (Exception exception)
            {
                OnLifecycleTaskFailure(new LifecycleTaskFailure(
                    _applicationScope?.Name ?? "Application",
                    $"scene-session-initialization:{request.SceneName}:{request.Initializer.Name}",
                    exception));
                return LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    $"场景 {request.SceneName} 的 Session 初始化执行异常",
                    exception);
            }
        }

        void CompletePendingSceneRequestAsCancelled()
        {
            CompletePendingSceneRequest(true);
        }

        void CompletePendingSceneRequest(bool cancelled)
        {
            SceneSessionInitializationRequest pending = _pendingSceneRequest;
            _pendingSceneRequest = null;

            if (pending == null)
            {
                return;
            }

            CompleteSceneRequest(
                pending,
                CreateSceneCoordinatorTerminationResult(pending, cancelled));
        }

        async UniTask<LifecycleResult> PrepareAndInitializeSceneAsync(
            Scene scene,
            IGameSessionInitializer initializer,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.Cancelled,
                    "目标场景在 Session 初始化前已经卸载");
            }

            if (_session != null && _session.State == GameSessionState.Created)
            {
                LifecycleResult existingBinding = _session.BindScene(scene);

                if (existingBinding.IsSuccess)
                {
                    return await InitializeCurrentSessionAsync(initializer, token);
                }
            }

            if (_session != null)
            {
                LifecycleResult stopResult = await StopCurrentSessionAsync();

                if (!stopResult.IsSuccess)
                {
                    return stopResult;
                }
            }

            token.ThrowIfCancellationRequested();
            LifecycleResult createResult = CreatePendingSession();

            if (!createResult.IsSuccess)
            {
                return createResult;
            }

            LifecycleResult bindingResult = BindCurrentSessionToScene(scene);

            if (!bindingResult.IsSuccess)
            {
                await StopCurrentSessionAsync();
                return bindingResult;
            }

            return await InitializeCurrentSessionAsync(initializer, token);
        }

        LifecycleResult CreateSceneRequestCancellationResult(
            SceneSessionInitializationRequest request)
        {
            string reason = request.IsSuperseded
                ? "已被更新的场景请求替代"
                : "已取消";
            return LifecycleResult.Failure(
                LifecycleResultCode.Cancelled,
                $"场景 {request.SceneName} 的 Session 初始化{reason}");
        }

        LifecycleResult CreateSceneCoordinatorTerminationResult(
            SceneSessionInitializationRequest request,
            bool cancelled)
        {
            if (cancelled || request.IsSuperseded)
            {
                return CreateSceneRequestCancellationResult(request);
            }

            return LifecycleResult.Failure(
                LifecycleResultCode.Failed,
                $"场景 {request.SceneName} 的 Session 协调器在请求完成前终止");
        }

        void CompleteSceneRequest(
            SceneSessionInitializationRequest request,
            LifecycleResult result)
        {
            if (!request.TryComplete())
            {
                return;
            }

            try
            {
                request.OnCompleted?.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        void NotifySessionRunning(GameSessionHost session)
        {
            if (session == null
                || !ReferenceEquals(_session, session)
                || session.State != GameSessionState.Running
                || !session.IsCurrentArchitectureLease)
            {
                return;
            }

            int generation = session.ArchitectureGeneration;

            if (generation <= 0
                || generation <= _lastNotifiedSessionGeneration
                || ReferenceEquals(_lastNotifiedSession, session))
            {
                return;
            }

            _lastNotifiedSession = session;
            _lastNotifiedSessionGeneration = generation;
            BindSaveSnapshotSource(session);

            Delegate[] callbacks = SessionRunning?.GetInvocationList();

            if (callbacks == null)
            {
                return;
            }

            for (int i = 0; i < callbacks.Length; i++)
            {
                try
                {
                    ((Action<GameSessionHost>)callbacks[i]).Invoke(session);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        async UniTask<LifecycleResult> ShutdownCoreAsync()
        {
            Exception shutdownException = null;

            if (_saveCoordinator != null)
            {
                SaveOperationResult flushResult = await _saveCoordinator.CloseAndFlushAsync(
                    SaveCoordinator.AutoSlot,
                    SaveFlushTimeout);

                if (!flushResult.Succeeded)
                {
                    shutdownException = CombineShutdownException(
                        shutdownException,
                        flushResult.Exception ?? new InvalidOperationException(
                            $"存档 Flush 失败：{flushResult.ErrorCode}"));
                }
            }

            if (_session != null)
            {
                _saveCoordinator?.UnbindSession(_session);
                LifecycleResult sessionResult = await _session.StopAsync();

                if (!sessionResult.IsSuccess)
                {
                    shutdownException = sessionResult.Exception
                        ?? new InvalidOperationException(sessionResult.Message);
                }

                _session = null;
            }

            if (_profileScope != null)
            {
                LifecycleScope profileScope = _profileScope;
                profileScope.BeginStop();
                await profileScope.StopAsync();

                if (profileScope.IsAbandoned)
                {
                    shutdownException = CombineShutdownException(
                        shutdownException,
                        CreateAbandonedScopeException(profileScope));
                }

                _profileScope = null;
            }

            _localizationService?.Close();
            _settingsService?.Close();

            if (_applicationScope != null)
            {
                LifecycleScope applicationScope = _applicationScope;
                applicationScope.BeginStop();
                await applicationScope.StopAsync();

                if (applicationScope.IsAbandoned)
                {
                    shutdownException = CombineShutdownException(
                        shutdownException,
                        CreateAbandonedScopeException(applicationScope));
                }

                _applicationScope = null;
            }

            _sceneTransitionInProgress = false;
            CompleteOutstandingSceneRequests(true);
            SessionRunning = null;
            _contentCatalog = null;
            _contentCatalogDefinition = null;
            _itemInstanceIds = null;
            _localizationService = null;
            _settingsService = null;
            _settingsStorage = null;
            _settingsSerializer = null;
            _settingsPathProvider = null;
            _saveCoordinator = null;
            _sessionSaveFacade = null;
            _saveStorage = null;
            _saveSerializer = null;
            _savePathProvider = null;
            ProfileId = null;
            State = ApplicationLifecycleState.Shutdown;

            if (shutdownException != null)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "Application 关闭时发生清理错误",
                    shutdownException);
            }

            return LifecycleResult.Success("Application 已关闭");
        }

        static Exception CreateAbandonedScopeException(LifecycleScope scope)
        {
            IReadOnlyList<LifecycleTaskFailure> failures = scope.Tasks.Failures;
            Exception scopeException = null;

            for (int i = 0; i < failures.Count; i++)
            {
                LifecycleTaskFailure failure = failures[i];
                Exception failureException = failure.Exception
                    ?? new InvalidOperationException("生命周期任务失败时未提供异常");
                Exception diagnostic = new InvalidOperationException(
                    $"生命周期作用域 {failure.ScopeName} 的任务 {failure.OperationName} 停止失败",
                    failureException);
                scopeException = CombineShutdownException(scopeException, diagnostic);
            }

            return scopeException ?? new InvalidOperationException(
                $"生命周期作用域 {scope.Name} 已被遗弃");
        }

        static Exception CombineShutdownException(Exception first, Exception second)
        {
            if (first == null)
            {
                return second;
            }

            if (second == null)
            {
                return first;
            }

            return new AggregateException(first, second).Flatten();
        }

        async UniTask CompleteShutdownAsync()
        {
            LifecycleResult result;

            try
            {
                result = await ShutdownCoreAsync();
            }
            catch (Exception exception)
            {
                State = ApplicationLifecycleState.Shutdown;
                result = LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "Application 关闭流程异常",
                    exception);
            }

            _shutdownCompletion.TrySetResult(result);

            if (_quitGateArmed && !_allowQuit)
            {
                _allowQuit = true;
                Application.Quit();
            }
        }

        void EmergencyShutdown()
        {
            _saveCoordinator?.EmergencyClose();
            _localizationService?.Close();
            _settingsService?.Close();
            LifecycleResult sessionResult = _session != null
                ? _session.EmergencyStop()
                : LifecycleResult.AlreadyCompleted();
            _session = null;
            _profileScope?.BeginStop();
            _applicationScope?.BeginStop();
            _profileScope = null;
            _applicationScope = null;
            _sceneTransitionInProgress = false;
            CompleteOutstandingSceneRequests(true);
            SessionRunning = null;
            _contentCatalog = null;
            _contentCatalogDefinition = null;
            _itemInstanceIds = null;
            _localizationService = null;
            _settingsService = null;
            _settingsStorage = null;
            _settingsSerializer = null;
            _settingsPathProvider = null;
            _saveCoordinator = null;
            _sessionSaveFacade = null;
            _saveStorage = null;
            _saveSerializer = null;
            _savePathProvider = null;
            ProfileId = null;
            State = ApplicationLifecycleState.Shutdown;
            LifecycleResult result = sessionResult.IsSuccess
                ? LifecycleResult.Success("Application 已应急关闭")
                : LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "Application 应急关闭发生错误",
                    sessionResult.Exception);
            _shutdownCompletion?.TrySetResult(result);
        }

        void EnsureSaveCoordinator()
        {
            if (_saveCoordinator != null || _profileScope == null || _contentCatalog == null)
            {
                return;
            }

            if (_saveStorage == null)
            {
                throw new InvalidOperationException("Application 存档存储服务尚未创建");
            }

            _saveCoordinator = new SaveCoordinator(
                _profileScope,
                _saveStorage,
                _contentCatalog,
                Application.version);
        }

        void BindSaveSnapshotSource(GameSessionHost session)
        {
            if (_saveCoordinator == null || !session.HasBoundScene)
            {
                return;
            }

            MonsterSpawner spawner = FindSceneComponent<MonsterSpawner>(session.BoundScene);
            CombatPrototypeBootstrap bootstrap = FindSceneComponent<CombatPrototypeBootstrap>(
                session.BoundScene);

            if (spawner == null || bootstrap == null)
            {
                Debug.LogWarning(
                    "[ApplicationHost] 当前 Session 场景缺少 Bootstrap 或 MonsterSpawner，存档入口不可用",
                    this);
                return;
            }

            try
            {
                _saveCoordinator.BindSession(
                    session,
                    new SessionSnapshotSource(session, _contentCatalog, spawner));
                _sessionSaveFacade = new SessionSaveFacade(
                    this,
                    session,
                    _saveCoordinator,
                    session.BoundScene,
                    bootstrap.CreateSceneConfiguration());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        void CompleteOutstandingSceneRequests(bool cancelled)
        {
            SceneSessionInitializationRequest active = _activeSceneRequest;
            SceneSessionInitializationRequest pending = _pendingSceneRequest;
            _activeSceneRequest = null;
            _pendingSceneRequest = null;
            _activeSceneTransitionScope?.BeginStop();
            _activeSceneTransitionScope = null;

            if (active != null)
            {
                CompleteSceneRequest(
                    active,
                    CreateSceneCoordinatorTerminationResult(active, cancelled));
            }

            if (pending != null)
            {
                CompleteSceneRequest(
                    pending,
                    CreateSceneCoordinatorTerminationResult(pending, cancelled));
            }
        }

        sealed class SceneSessionInitializationRequest
        {
            int _completionSignaled;

            public SceneSessionInitializationRequest(
                Scene scene,
                IGameSessionInitializer initializer,
                CancellationToken externalToken,
                Action<LifecycleResult> onCompleted)
            {
                Scene = scene;
                SceneName = scene.name;
                Initializer = initializer;
                ExternalToken = externalToken;
                OnCompleted = onCompleted;
            }

            public Scene Scene { get; }

            public string SceneName { get; }

            public IGameSessionInitializer Initializer { get; }

            public CancellationToken ExternalToken { get; }

            public Action<LifecycleResult> OnCompleted { get; }

            public bool IsSuperseded { get; private set; }

            public void MarkSuperseded()
            {
                IsSuperseded = true;
            }

            public bool TryComplete()
            {
                return Interlocked.Exchange(ref _completionSignaled, 1) == 0;
            }
        }
    }
}
