using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DarkFlare
{
    // 只放在隔离诊断场景，复用 Application 并创建不写存档的专属 Session。
    [DefaultExecutionOrder(-500)]
    public sealed class BehaviorSampleHost : MonoBehaviour, IController
    {
        [SerializeField] BehaviorSampleConfig _config;
        [SerializeField] InputSystemUIInputModule _inputModule;
        readonly List<InputActionReference> _inputReferences = new List<InputActionReference>();
        LifecycleScope _scope;
        ApplicationHost _application;
        GameSessionHost _session;
        LifecycleScope _setup;
        UniTask _shutdown;
        int _version;
        public event Action Ready;
        public event Action Closing;
        public ApplicationInputService Input { get; private set; }
        public PatrolBehaviorRuntime.Template Template { get; private set; }
        public Guid ScopeId { get; private set; }
        public bool IsReady => _scope?.CanAcceptWork == true && _session?.State == GameSessionState.Running && _session.IsCurrentArchitectureLease;
        public IArchitecture GetArchitecture() { return _session?.Architecture; }
        void OnEnable()
        {
            int version = ++_version;
            _setup = LifecycleScope.CreateRoot("BehaviorSampleSetup", externalToken: this.GetCancellationTokenOnDestroy());
            _setup.Tasks.Run("initialize", token => InitializeAsync(version, token),
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }
        void OnDisable() { ++_version; Shutdown(); }
        void OnDestroy() { Shutdown(); Ready = null; Closing = null; }

        async UniTask InitializeAsync(int version, CancellationToken token)
        {
            await _shutdown;
            if (this == null || !isActiveAndEnabled || version != _version) { return; }
            try
            {
                await UniTask.WaitUntil(() => ApplicationHost.TryGetCurrent(out ApplicationHost host) && host.State == ApplicationLifecycleState.Ready,
                    cancellationToken: token);
                _application = ApplicationHost.Current;
                if (_application.CurrentSession != null) { throw new InvalidOperationException("诊断场景不能替换现有 Session"); }
                Template = new PatrolBehaviorRuntime.Template(_config);
                Check(_application.CreatePendingSession());
                _session = _application.CurrentSession;
                Check(_application.BindCurrentSessionToScene(gameObject.scene));
                Check(await _application.InitializeCurrentSessionAsync(new Initializer(), token));
                token.ThrowIfCancellationRequested();
                Input = _application.Input;
                _scope = _session.SceneScope.CreateChild("BehaviorSample");
                // 每个专属 Session 只有一个样例作用域；Handle 的架构代次隔离再次进入。
                ScopeId = Guid.Parse("bfbbf51c-ff83-4dfb-8fe0-f8ed48b71dd9");
                BindInputModule();
                foreach (BehaviorSampleActor actor in GetComponentsInChildren<BehaviorSampleActor>()) { actor.Rebind(); }
                Ready?.Invoke();
                var tick = new TickBehaviorScopeCommand(ScopeId);
                IArchitecture owner = _session.Architecture;
                _scope.Tasks.Run("behavior-sample-clock", async clockToken =>
                {
                    while (!clockToken.IsCancellationRequested)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, clockToken);
                        tick.DeltaTime = Time.deltaTime;
                        owner.SendCommand(tick);
                    }
                }, failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error) { ApplicationLog.Exception(LogEventIds.InfrastructureLifecycle, error, this); Shutdown(); }
        }
        static void Check(LifecycleResult result)
        {
            if (!result.IsSuccess) { throw new InvalidOperationException(result.Message, result.Exception); }
        }
        sealed class Initializer : IGameSessionInitializer
        {
            public string Name => "Behavior diagnostics";
            public UniTask InitializeAsync(SessionInitializationContext context, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); return UniTask.CompletedTask;
            }
            public UniTask RollbackAsync(SessionInitializationContext context, CancellationToken token) { return UniTask.CompletedTask; }
        }

        void Shutdown()
        {
            LifecycleScope setup = _setup;
            _setup = null;
            setup?.BeginStop();
            if (_session == null)
            {
                if (setup != null) { _shutdown = setup.StopAsync().Preserve(); }
                return;
            }
            Closing?.Invoke();
            LifecycleScope scope = _scope; GameSessionHost session = _session; ApplicationHost application = _application;
            _scope = null; Input = null; _session = null;
            if (_inputModule != null)
            {
                _inputModule.enabled = false; _inputModule.actionsAsset = null;
                _inputModule.point = null; _inputModule.leftClick = null; _inputModule.scrollWheel = null;
                _inputModule.move = null; _inputModule.submit = null; _inputModule.cancel = null;
            }
            foreach (InputActionReference reference in _inputReferences) { Destroy(reference); }
            _inputReferences.Clear();
            scope?.BeginStop();
            _shutdown = ShutdownAsync(scope, setup, application, session).Preserve();
        }
        static async UniTask ShutdownAsync(LifecycleScope scope, LifecycleScope setup, ApplicationHost application, GameSessionHost session)
        {
            if (scope != null) { await scope.StopAsync(); }
            if (application != null && ReferenceEquals(application.CurrentSession, session)) { await application.StopCurrentSessionAsync(); }
            if (setup != null) { await setup.StopAsync(); }
        }
        void BindInputModule()
        {
            if (_inputModule == null) { return; }
            _inputModule.enabled = false;
            _inputModule.actionsAsset = Input.ActionAsset;
            _inputModule.point = Reference("UI/Point"); _inputModule.leftClick = Reference("UI/Click");
            _inputModule.scrollWheel = Reference("UI/ScrollWheel"); _inputModule.move = Reference("UI/Navigate");
            _inputModule.submit = Reference("UI/Submit"); _inputModule.cancel = Reference("UI/Cancel");
            _inputModule.enabled = true;
        }
        InputActionReference Reference(string path)
        {
            InputActionReference reference = InputActionReference.Create(Input.ActionAsset.FindAction(path, true));
            _inputReferences.Add(reference); return reference;
        }
    }
}
