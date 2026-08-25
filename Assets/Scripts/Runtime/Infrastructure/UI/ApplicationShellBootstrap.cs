using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ApplicationShellBootstrap : MonoBehaviour
    {
        [SerializeField]
        SceneFlowConfiguration _sceneFlowConfiguration;

        [SerializeField]
        ContentCatalogDefinition _contentCatalog;

        [SerializeField]
        UIDocument _document;

        ApplicationShellController _controller;

        void Awake()
        {
            EnsureComponents();
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
                () => _document != null
                    && _document.rootVisualElement != null
                    && _document.rootVisualElement.panel != null,
                cancellationToken: cancellationToken);
            _controller = new ApplicationShellController(_document, host);
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
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }
        }
    }
}
