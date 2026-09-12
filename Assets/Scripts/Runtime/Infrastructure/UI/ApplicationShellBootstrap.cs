using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimePanelView))]
    public sealed class ApplicationShellBootstrap : MonoBehaviour
    {
        [SerializeField]
        SceneFlowConfiguration _sceneFlowConfiguration;

        [SerializeField]
        ContentCatalogDefinition _contentCatalog;

        [SerializeField]
        RuntimePanelView _uiPanel;

        ApplicationShellController _controller;

        void Awake()
        {
            EnsureComponents();
        }

        void OnEnable()
        {
            EnsureComponents();
            _uiPanel.Reloading += OnPanelReloading;
            _uiPanel.Reloaded += OnPanelReloaded;
            if (_uiPanel.Root?.panel != null) { OnPanelReloaded(); }
        }

        void OnDisable()
        {
            _uiPanel.Reloading -= OnPanelReloading;
            _uiPanel.Reloaded -= OnPanelReloaded;
        }

        void OnPanelReloading()
        {
            _controller?.PrepareReload();
        }

        void OnPanelReloaded()
        {
            _controller?.Reload(_uiPanel.Root);
        }

        void Start()
        {
            if (!ApplicationHost.TryGetCurrent(out ApplicationHost host)
                || host.ApplicationScope == null
                || !host.ApplicationScope.CanAcceptWork)
            {
                ApplicationLog.Error(LogEventIds.InfrastructureUi, "[ApplicationShellBootstrap] ApplicationHost 尚未就绪", this);
                return;
            }

            try
            {
                host.ApplicationScope.Tasks.Run(
                    "application-shell-bootstrap",
                    InitializeAsync,
                    this.GetCancellationTokenOnDestroy(),
                    LifecycleTaskFailurePolicy.Report);
            }
            catch (Exception exception)
            {
                ApplicationLog.Exception(LogEventIds.InfrastructureUi, exception, this);
            }
        }

        void OnDestroy()
        {
            if (_controller != null
                && ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                host.UnregisterApplicationShell(_controller);
            }

            _controller?.Dispose();
            _controller = null;
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            ApplicationHost host = ApplicationHost.Current;
            await UniTask.WaitUntil(
                () => host.State != ApplicationLifecycleState.Booting,
                cancellationToken: cancellationToken);

            if (host.State != ApplicationLifecycleState.Ready)
            {
                throw new InvalidOperationException(
                    $"Application 启动未完成：{host.State}");
            }

            if (_sceneFlowConfiguration == null || _contentCatalog == null)
            {
                throw new InvalidOperationException("Application Shell 缺少场景流或内容目录配置");
            }

            LifecycleResult catalog = host.InstallContentCatalog(_contentCatalog);

            if (!catalog.IsSuccess)
            {
                throw new InvalidOperationException(catalog.Message, catalog.Exception);
            }

            LifecycleResult flow = host.ConfigureSceneFlow(_sceneFlowConfiguration);

            if (!flow.IsSuccess)
            {
                throw new InvalidOperationException(flow.Message, flow.Exception);
            }

            await UniTask.WaitUntil(
                () => _uiPanel != null
                    && _uiPanel.Root != null
                    && _uiPanel.Root.panel != null,
                cancellationToken: cancellationToken);
            _controller = new ApplicationShellController(_uiPanel.Root, host);
            _controller.Bind();
            host.RegisterApplicationShell(_controller);
            SceneFlowResult frontEnd = await host.SceneFlow.RequestAsync(
                SceneFlowRequest.EnterFrontEnd(),
                cancellationToken);

            if (!frontEnd.Succeeded)
            {
                _controller.ShowFailure(frontEnd);
            }
        }

        void EnsureComponents()
        {
            if (_uiPanel == null)
            {
                _uiPanel = GetComponent<RuntimePanelView>();
            }

            if (_uiPanel == null)
            {
                _uiPanel = gameObject.AddComponent<RuntimePanelView>();
            }
        }
    }
}
