using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class WorldSortingRunner : MonoBehaviour, IController
    {
        LifecycleScope _componentScope;
        ApplicationHost _host;

        public IArchitecture GetArchitecture()
        {
            return GameArchitectureProvider.RequireCurrent();
        }

        void OnEnable()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                _host = host;
                _host.SessionRunning += OnSessionRunning;
            }

            TryStart();
        }

        void TryStart()
        {
            if (_componentScope != null
                && _componentScope.State == LifecycleScopeState.Active)
            {
                return;
            }

            if (_host != null
                && (_host.CurrentSession == null
                    || _host.CurrentSession.SceneScope.State != LifecycleScopeState.Active
                    || (_host.CurrentSession.HasBoundScene
                        && !_host.CurrentSession.IsBoundToScene(gameObject.scene))))
            {
                return;
            }

            _componentScope = ComponentLifecycle.CreateScope(
                this,
                "world-sorting",
                this.GetCancellationTokenOnDestroy());
            _componentScope.Tasks.Run(
                "world-sorting-loop",
                RunAsync,
                failurePolicy: LifecycleTaskFailurePolicy.ReportAndStopScope);
        }

        void OnDisable()
        {
            if (_host != null)
            {
                _host.SessionRunning -= OnSessionRunning;
                _host = null;
            }

            _componentScope?.BeginStop();
            _componentScope = null;
        }

        void OnSessionRunning(GameSessionHost session)
        {
            if (session.IsBoundToScene(gameObject.scene))
            {
                TryStart();
            }
        }

        async UniTask RunAsync(System.Threading.CancellationToken token)
        {
            WorldSortingSystem sortingSystem = this.GetUtility<WorldSortingSystem>();

            while (!token.IsCancellationRequested)
            {
                sortingSystem.Tick();

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            }
        }
    }
}
