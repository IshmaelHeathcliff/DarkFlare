using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class WorldSortingRunner : MonoBehaviour, IController
    {
        CancellationTokenSource _cancellation;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        void OnEnable()
        {
            _cancellation = new CancellationTokenSource();
            RunAsync(_cancellation.Token).Forget();
        }

        void OnDisable()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
        }

        async UniTaskVoid RunAsync(CancellationToken token)
        {
            WorldSortingSystem sortingSystem = this.GetUtility<WorldSortingSystem>();

            while (!token.IsCancellationRequested)
            {
                sortingSystem.Tick();

                if (await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token).SuppressCancellationThrow())
                {
                    return;
                }
            }
        }
    }
}
