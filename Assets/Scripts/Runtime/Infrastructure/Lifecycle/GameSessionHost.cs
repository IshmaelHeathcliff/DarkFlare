using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    public interface IGameSessionInitializer
    {
        string Name { get; }

        UniTask InitializeAsync(SessionInitializationContext context, CancellationToken token);

        UniTask RollbackAsync(SessionInitializationContext context, CancellationToken token);
    }

    public interface IRequiresContentCatalog
    {
    }

    public sealed class SessionInitializationContext
    {
        internal SessionInitializationContext(
            IArchitecture architecture,
            LifecycleScope sessionScope,
            LifecycleScope sceneScope,
            ContentCatalog contentCatalog)
        {
            Architecture = architecture;
            SessionScope = sessionScope;
            SceneScope = sceneScope;
            ContentCatalog = contentCatalog;
        }

        public IArchitecture Architecture { get; }

        public LifecycleScope SessionScope { get; }

        public LifecycleScope SceneScope { get; }

        public ContentCatalog ContentCatalog { get; }
    }

    public sealed class GameSessionHost
    {
        sealed class RollbackOutcome
        {
            public bool Completed { get; set; }

            public bool Skipped { get; set; }

            public Exception Error { get; set; }
        }

        readonly IArchitecture _architecture;
        readonly GameArchitectureSessionLease _architectureLease;
        readonly Action<GameSessionHost, string> _controlledStopRequested;
        readonly UniTaskCompletionSource _rollbackRequested = new UniTaskCompletionSource();
        readonly RollbackOutcome _rollbackOutcome = new RollbackOutcome();

        IGameSessionInitializer _initializer;
        SessionInitializationContext _initializationContext;
        UniTask<LifecycleResult> _initializationTask;
        LifecycleTaskHandle _rollbackHandle;
        bool _stopStarted;
        UniTaskCompletionSource<LifecycleResult> _stopCompletion;
        UniTask _stopRunner;
        bool _sceneBound;
        Scene _boundScene;

        internal GameSessionHost(
            LifecycleScope profileScope,
            int sequence,
            Action<GameSessionHost, string> controlledStopRequested,
            IItemInstanceIdGenerator itemInstanceIds = null)
        {
            _controlledStopRequested = controlledStopRequested;
            SessionScope = profileScope.CreateChild($"Session-{sequence}");
            SceneScope = SessionScope.CreateChild($"Scene-{sequence}");

            try
            {
                _architectureLease = GameArchitectureProvider.StartOwnedSession(SessionScope);
                _architecture = _architectureLease.Architecture;

                if (itemInstanceIds != null)
                {
                    _architecture.RegisterUtility<IItemInstanceIdGenerator>(itemInstanceIds);
                }
                State = GameSessionState.Created;
                StartSessionTasks();
            }
            catch
            {
                SceneScope.BeginStop();
                SessionScope.BeginStop();

                if (_architectureLease.IsValid
                    && GameArchitectureProvider.IsCurrent(_architectureLease))
                {
                    GameArchitectureProvider.EmergencyStopOwnedSession(_architectureLease);
                }

                State = GameSessionState.None;
                throw;
            }
        }

        public GameSessionState State { get; private set; }

        public LifecycleScope SessionScope { get; }

        public LifecycleScope SceneScope { get; }

        public IArchitecture Architecture => _architecture;

        public int ArchitectureGeneration => _architectureLease.Generation;

        public bool IsCurrentArchitectureLease =>
            GameArchitectureProvider.IsCurrent(_architectureLease);

        public bool HasBoundScene => _sceneBound;

        public Scene BoundScene => _sceneBound ? _boundScene : default;

        public bool IsBoundToScene(Scene scene)
        {
            return _sceneBound
                && scene.IsValid()
                && scene.handle == _boundScene.handle;
        }

        public LifecycleResult BindScene(Scene scene)
        {
            if (State != GameSessionState.Created)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    $"Session 当前状态 {State} 不允许绑定场景");
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.ValidationFailed,
                    "只能绑定已经加载的有效场景");
            }

            if (_sceneBound)
            {
                return _boundScene.handle == scene.handle
                    ? LifecycleResult.AlreadyCompleted($"场景 {scene.name} 已绑定")
                    : LifecycleResult.Failure(
                        LifecycleResultCode.InvalidState,
                        $"Session 已绑定场景 {_boundScene.name}");
            }

            _boundScene = scene;
            _sceneBound = true;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            return LifecycleResult.Success($"场景 {scene.name} 已绑定");
        }

        public UniTask<LifecycleResult> InitializeAsync(
            IGameSessionInitializer initializer,
            CancellationToken externalToken = default)
        {
            return InitializeAsync(initializer, null, externalToken);
        }

        internal UniTask<LifecycleResult> InitializeAsync(
            IGameSessionInitializer initializer,
            ContentCatalog contentCatalog,
            CancellationToken externalToken = default)
        {
            if (initializer == null)
            {
                return UniTask.FromResult(LifecycleResult.Failure(
                    LifecycleResultCode.ValidationFailed,
                    "Session initializer 不能为空"));
            }

            if (State == GameSessionState.Initializing)
            {
                return UniTask.FromResult(LifecycleResult.Failure(
                    LifecycleResultCode.OperationInProgress,
                    "Session 正在初始化"));
            }

            if (State == GameSessionState.Running)
            {
                return UniTask.FromResult(LifecycleResult.AlreadyCompleted("Session 已进入 Running"));
            }

            if (State != GameSessionState.Created)
            {
                return UniTask.FromResult(LifecycleResult.Failure(
                    LifecycleResultCode.InvalidState,
                    $"Session 当前状态 {State} 不允许初始化"));
            }

            if (SessionScope.State != LifecycleScopeState.Active
                || SceneScope.State != LifecycleScopeState.Active)
            {
                MarkAbandoned();
                return UniTask.FromResult(CreateAbandonedResult(
                    "Session 作用域已开始停止，不能再启动初始化"));
            }

            _initializationContext = new SessionInitializationContext(
                _architecture,
                SessionScope,
                SceneScope,
                contentCatalog);
            _initializer = initializer;
            State = GameSessionState.Initializing;
            LifecycleTaskHandle handle;

            try
            {
                handle = SceneScope.Tasks.Run(
                    $"initialize:{initializer.Name}",
                    token => initializer.InitializeAsync(_initializationContext, token),
                    externalToken,
                    failurePolicy: LifecycleTaskFailurePolicy.Propagate);
            }
            catch (Exception exception)
            {
                MarkAbandoned();
                SceneScope.BeginStop();
                SessionScope.BeginStop();
                return UniTask.FromResult(CreateAbandonedResult(
                    "Session 初始化任务未能登记，已隔离当前代际",
                    exception));
            }

            _initializationTask = InitializeCoreAsync(handle, externalToken).Preserve();
            return _initializationTask;
        }

        public void BeginStop()
        {
            StartStop(false);
        }

        public UniTask<LifecycleResult> StopAsync()
        {
            return StartStop(false);
        }

        internal LifecycleResult EmergencyStop()
        {
            if (State == GameSessionState.None)
            {
                return LifecycleResult.AlreadyCompleted("Session 已停止");
            }

            if (State == GameSessionState.Abandoned)
            {
                return CreateAbandonedResult(
                    "Session 已处于不可恢复的隔离状态，应急停止不能视为清理成功");
            }

            _stopStarted = true;
            _stopCompletion ??= new UniTaskCompletionSource<LifecycleResult>();
            UnbindScene();
            MarkAbandoned();
            SceneScope.BeginStop();
            SessionScope.BeginStop();
            LifecycleResult result = LifecycleResult.Failure(
                LifecycleResultCode.Failed,
                "Session 已应急隔离；架构将在下次静态重置时释放",
                new InvalidOperationException("Session 未等待异步任务收敛，禁止启动下一代 Session"));
            _stopCompletion.TrySetResult(result);
            return result;
        }

        UniTask<LifecycleResult> StartStop(bool rollback)
        {
            if (_stopStarted)
            {
                return _stopCompletion.Task;
            }

            if (State == GameSessionState.None)
            {
                _stopStarted = true;
                _stopCompletion = new UniTaskCompletionSource<LifecycleResult>();
                _stopCompletion.TrySetResult(
                    LifecycleResult.AlreadyCompleted("Session 已停止"));
                return _stopCompletion.Task;
            }

            if (State == GameSessionState.Abandoned)
            {
                _stopStarted = true;
                _stopCompletion = new UniTaskCompletionSource<LifecycleResult>();
                _stopCompletion.TrySetResult(CreateAbandonedResult(
                    "Session 已进入不可恢复的隔离状态"));
                return _stopCompletion.Task;
            }

            _stopStarted = true;
            _stopCompletion = new UniTaskCompletionSource<LifecycleResult>();
            UnbindScene();
            SetState(rollback ? GameSessionState.RollingBack : GameSessionState.Stopping);
            SceneScope.BeginStop();
            RequestRollback();
            _stopRunner = CompleteStopAsync().Preserve();
            return _stopCompletion.Task;
        }

        void StartSessionTasks()
        {
            _rollbackHandle = SessionScope.Tasks.Run(
                "session-rollback-coordinator",
                RunRollbackCoordinatorAsync,
                failurePolicy: LifecycleTaskFailurePolicy.Report);
            ResourceRegenerationSystem regenerationSystem =
                _architecture.GetSystem<ResourceRegenerationSystem>();
            SessionScope.Tasks.Run(
                "resource-regeneration",
                token => RunResourceRegenerationAsync(regenerationSystem, token),
                failurePolicy: LifecycleTaskFailurePolicy.Report);
        }

        async UniTask RunResourceRegenerationAsync(
            ResourceRegenerationSystem regenerationSystem,
            CancellationToken token)
        {
            try
            {
                await regenerationSystem.RunAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                _controlledStopRequested?.Invoke(this, "resource-regeneration-failure");
                throw;
            }
        }

        async UniTask<LifecycleResult> InitializeCoreAsync(
            LifecycleTaskHandle handle,
            CancellationToken externalToken)
        {
            try
            {
                await handle.WaitAsync();

                if (State == GameSessionState.Abandoned)
                {
                    return CreateAbandonedResult(
                        "Session 初始化在应急隔离后才完成，结果已丢弃");
                }

                if (SceneScope.Token.IsCancellationRequested
                    || externalToken.IsCancellationRequested)
                {
                    LifecycleResult cleanupResult = await StartStop(true);

                    if (!cleanupResult.IsSuccess)
                    {
                        return LifecycleResult.Failure(
                            LifecycleResultCode.Cancelled,
                            "Session 初始化已取消，且回滚发生错误",
                            cleanupResult.Exception);
                    }

                    return LifecycleResult.Failure(
                        LifecycleResultCode.Cancelled,
                        "Session 初始化已取消");
                }

                if (State != GameSessionState.Initializing
                    || !GameArchitectureProvider.IsCurrent(_architectureLease))
                {
                    MarkAbandoned();
                    SceneScope.BeginStop();
                    SessionScope.BeginStop();
                    return CreateAbandonedResult(
                        "Session 初始化完成时所有权已经失效，结果已隔离");
                }

                SetState(GameSessionState.Running);
                return LifecycleResult.Success("Session 初始化完成");
            }
            catch (Exception exception)
            {
                if (State == GameSessionState.Abandoned)
                {
                    return CreateAbandonedResult(
                        "Session 初始化在应急隔离后失败，迟到异常已隔离",
                        exception);
                }

                LifecycleResult cleanupResult = await StartStop(true);

                if (State == GameSessionState.Abandoned)
                {
                    return CreateAbandonedResult(
                        "Session 初始化失败后的清理进入隔离状态",
                        Combine(exception, cleanupResult.Exception));
                }

                Exception resultException = cleanupResult.Exception == null
                    ? exception
                    : new AggregateException(exception, cleanupResult.Exception);
                return LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    $"Session 初始化失败: {_initializer?.Name ?? "未知 initializer"}",
                    resultException);
            }
        }

        async UniTask<LifecycleResult> StopCoreAsync()
        {
            Exception cleanupException = null;
            bool abandoned = false;

            try
            {
                await SceneScope.StopAsync();
                abandoned |= SceneScope.IsAbandoned;
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            try
            {
                RequestRollback();
                SessionScope.BeginStop();
                await SessionScope.StopAsync();
                abandoned |= SessionScope.IsAbandoned;
            }
            catch (Exception exception)
            {
                cleanupException = Combine(cleanupException, exception);
            }

            if (!abandoned && State != GameSessionState.Abandoned)
            {
                try
                {
                    if (_rollbackHandle != null)
                    {
                        await _rollbackHandle.WaitAsync();
                    }
                }
                catch (Exception exception)
                {
                    cleanupException = Combine(cleanupException, exception);
                }

                if (!_rollbackOutcome.Completed || _rollbackOutcome.Skipped)
                {
                    abandoned = true;
                    cleanupException = Combine(
                        cleanupException,
                        new InvalidOperationException(
                            "Session 回滚协调任务未形成可提交的完成结果"));
                }
                else
                {
                    cleanupException = Combine(
                        cleanupException,
                        _rollbackOutcome.Error);
                }
            }

            if (State == GameSessionState.Abandoned)
            {
                return CreateAbandonedResult(
                    "Session 停止 continuation 在应急隔离后完成，结果已丢弃",
                    cleanupException);
            }

            if (abandoned)
            {
                MarkAbandoned();
                cleanupException = Combine(
                    cleanupException,
                    new TimeoutException(
                        "Session 中存在未在时限内停止的任务；架构保持隔离且不会启动下一代 Session"));
                return LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "Session 停止超时，已进入隔离状态",
                    cleanupException);
            }

            if (!GameArchitectureProvider.IsCurrent(_architectureLease))
            {
                MarkAbandoned();
                cleanupException = Combine(
                    cleanupException,
                    new InvalidOperationException(
                        $"Session 架构所有权租约已失效，代际 {_architectureLease.Generation} 不再是当前代际"));
                return CreateAbandonedResult(
                    "Session 停止时架构所有权已失效，已保持隔离",
                    cleanupException);
            }

            try
            {
                GameArchitectureProvider.StopOwnedSession(_architectureLease);
            }
            catch (Exception exception)
            {
                cleanupException = Combine(cleanupException, exception);
            }

            SetState(GameSessionState.None);

            if (State == GameSessionState.Abandoned)
            {
                return CreateAbandonedResult(
                    "Session 在架构释放期间进入应急隔离状态",
                    cleanupException);
            }

            if (cleanupException != null)
            {
                return LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "Session 清理失败",
                    cleanupException);
            }

            return LifecycleResult.Success("Session 已停止");
        }

        async UniTask RunRollbackCoordinatorAsync(CancellationToken token)
        {
            CancellationTokenRegistration registration = token.Register(RequestRollback);

            try
            {
                await _rollbackRequested.Task;
                await UniTask.Yield();
                await SceneScope.StopAsync();

                if (SceneScope.IsAbandoned || State == GameSessionState.Abandoned)
                {
                    _rollbackOutcome.Skipped = true;
                    return;
                }

                IGameSessionInitializer initializer = _initializer;

                if (initializer != null)
                {
                    try
                    {
                        await initializer.RollbackAsync(_initializationContext, token);
                    }
                    catch (Exception exception)
                    {
                        _rollbackOutcome.Error = exception;
                    }
                }
            }
            finally
            {
                registration.Dispose();
                _rollbackOutcome.Completed = true;
            }
        }

        void RequestRollback()
        {
            _rollbackRequested.TrySetResult();
        }

        async UniTask CompleteStopAsync()
        {
            LifecycleResult result;

            try
            {
                result = await StopCoreAsync();
            }
            catch (Exception exception)
            {
                result = LifecycleResult.Failure(
                    LifecycleResultCode.Failed,
                    "Session 停止流程异常",
                    exception);
            }

            _stopCompletion.TrySetResult(result);
        }

        static Exception Combine(Exception first, Exception second)
        {
            if (first == null)
            {
                return second;
            }

            if (second == null)
            {
                return first;
            }

            return new AggregateException(first, second);
        }

        LifecycleResult CreateAbandonedResult(
            string message,
            Exception exception = null)
        {
            return LifecycleResult.Failure(
                LifecycleResultCode.Failed,
                message,
                exception ?? new InvalidOperationException(
                    "Session 已进入 Abandoned，不再接受迟到 continuation 的状态写入或架构操作"));
        }

        void MarkAbandoned()
        {
            State = GameSessionState.Abandoned;
        }

        void SetState(GameSessionState state)
        {
            if (State == GameSessionState.Abandoned)
            {
                return;
            }

            State = state;
        }

        void OnSceneUnloaded(Scene scene)
        {
            if (!_sceneBound || scene.handle != _boundScene.handle)
            {
                return;
            }

            string sceneName = scene.name;
            UnbindScene();
            _controlledStopRequested?.Invoke(this, $"scene-unloaded:{sceneName}");
        }

        void UnbindScene()
        {
            if (!_sceneBound)
            {
                return;
            }

            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _boundScene = default;
            _sceneBound = false;
        }
    }
}
