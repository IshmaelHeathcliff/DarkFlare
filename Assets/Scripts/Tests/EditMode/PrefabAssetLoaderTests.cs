using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class PrefabAssetLoaderTests
    {
        const string TestGuid = "11111111111111111111111111111111";

        [UnityTest]
        public IEnumerator SharedLoad_FirstWaiterCancellationDoesNotCancelOtherWaiters()
        {
            return VerifyIndependentWaiterCancellationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator ReleaseAll_InvalidatesLateLoadWithoutDeletingReplacementSingleFlight()
        {
            return VerifyReleaseGenerationIsolationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator FailedLoad_PropagatesAndCanBeRetried()
        {
            return VerifyFailurePropagationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator AddressableBackend_ReleasesOnlyItsOwnHandle()
        {
            return VerifyAddressableHandleOwnershipAsync().ToCoroutine();
        }

        static async UniTask VerifyIndependentWaiterCancellationAsync()
        {
            ControlledPrefabLoadBackend backend = new ControlledPrefabLoadBackend();
            PrefabAssetLoader loader = new PrefabAssetLoader(backend);
            AssetReferenceGameObject reference = new AssetReferenceGameObject(TestGuid);
            CancellationTokenSource firstCancellation = new CancellationTokenSource();
            GameObject prefab = new GameObject("SharedPrefab");

            try
            {
                UniTask firstWaiter = loader.PreloadAsync(
                    new[] { reference },
                    firstCancellation.Token);
                UniTask secondWaiter = loader.PreloadAsync(
                    new[] { reference },
                    CancellationToken.None);

                Assert.AreEqual(1, backend.Handles.Count);
                firstCancellation.Cancel();
                bool firstWasCancelled = await firstWaiter.SuppressCancellationThrow();
                Assert.IsTrue(firstWasCancelled);

                backend.Handles[0].Complete(prefab);
                await secondWaiter;

                Assert.AreSame(prefab, loader.GetPrefab(reference));
                Assert.AreEqual(0, backend.Handles[0].ReleaseCount);
                loader.ReleaseAll();
                loader.ReleaseAll();
                Assert.AreEqual(1, backend.Handles[0].ReleaseCount);
            }
            finally
            {
                loader.ReleaseAll();
                firstCancellation.Dispose();
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        static async UniTask VerifyReleaseGenerationIsolationAsync()
        {
            ControlledPrefabLoadBackend backend = new ControlledPrefabLoadBackend();
            PrefabAssetLoader loader = new PrefabAssetLoader(backend);
            AssetReferenceGameObject reference = new AssetReferenceGameObject(TestGuid);
            GameObject stalePrefab = new GameObject("StalePrefab");
            GameObject currentPrefab = new GameObject("CurrentPrefab");

            try
            {
                UniTask staleWaiter = loader.PreloadAsync(
                    new[] { reference },
                    CancellationToken.None);
                ControlledPrefabLoadHandle staleHandle = backend.Handles[0];
                loader.ReleaseAll();
                bool staleWasCancelled = await staleWaiter.SuppressCancellationThrow();
                Assert.IsTrue(staleWasCancelled);
                Assert.AreEqual(1, staleHandle.ReleaseCount);

                UniTask replacementWaiter = loader.PreloadAsync(
                    new[] { reference },
                    CancellationToken.None);
                ControlledPrefabLoadHandle replacementHandle = backend.Handles[1];
                staleHandle.Complete(stalePrefab);

                UniTask joinedWaiter = loader.PreloadAsync(
                    new[] { reference },
                    CancellationToken.None);
                Assert.AreEqual(
                    2,
                    backend.Handles.Count,
                    "旧 generation 的 runner 不得删除 replacement 的单飞记录");

                replacementHandle.Complete(currentPrefab);
                await UniTask.WhenAll(replacementWaiter, joinedWaiter);

                Assert.AreSame(currentPrefab, loader.GetPrefab(reference));
                Assert.AreEqual(1, staleHandle.ReleaseCount, "迟到句柄必须且只能释放一次");
                Assert.AreEqual(0, replacementHandle.ReleaseCount);
                loader.ReleaseAll();
                Assert.AreEqual(1, replacementHandle.ReleaseCount);
            }
            finally
            {
                loader.ReleaseAll();
                UnityEngine.Object.DestroyImmediate(stalePrefab);
                UnityEngine.Object.DestroyImmediate(currentPrefab);
            }
        }

        static async UniTask VerifyFailurePropagationAsync()
        {
            ControlledPrefabLoadBackend backend = new ControlledPrefabLoadBackend();
            PrefabAssetLoader loader = new PrefabAssetLoader(backend);
            AssetReferenceGameObject reference = new AssetReferenceGameObject(TestGuid);
            InvalidOperationException loadFailure = new InvalidOperationException("controlled failure");
            GameObject retryPrefab = new GameObject("RetryPrefab");

            try
            {
                UniTask failedWaiter = loader.PreloadAsync(
                    new[] { reference },
                    CancellationToken.None);
                ControlledPrefabLoadHandle failedHandle = backend.Handles[0];
                failedHandle.Fail(loadFailure);
                Exception observedFailure = null;

                try
                {
                    await failedWaiter;
                }
                catch (Exception exception)
                {
                    observedFailure = exception;
                }

                Assert.IsInstanceOf<InvalidOperationException>(observedFailure);
                Assert.AreSame(loadFailure, observedFailure.InnerException);
                Assert.AreEqual(1, failedHandle.ReleaseCount);

                UniTask retryWaiter = loader.PreloadAsync(
                    new[] { reference },
                    CancellationToken.None);
                Assert.AreEqual(2, backend.Handles.Count);
                backend.Handles[1].Complete(retryPrefab);
                await retryWaiter;
                Assert.AreSame(retryPrefab, loader.GetPrefab(reference));
            }
            finally
            {
                loader.ReleaseAll();
                UnityEngine.Object.DestroyImmediate(retryPrefab);
            }
        }

        static async UniTask VerifyAddressableHandleOwnershipAsync()
        {
            CharacterDefinition character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/Data/Preset/Actors/玩家.asset");
            Assert.IsNotNull(character);
            Assert.IsNotNull(character.Prefab);
            AssetReferenceGameObject reference = character.Prefab;
            PrefabAssetLoader loader = new PrefabAssetLoader();
            AsyncOperationHandle<GameObject> externalHandle = default;

            try
            {
                externalHandle = reference.LoadAssetAsync();
                await externalHandle.ToUniTask();
                GameObject externalPrefab = externalHandle.Result;
                Assert.IsNotNull(externalPrefab);

                await loader.PreloadAsync(
                    new[] { reference },
                    CancellationToken.None);
                Assert.AreSame(externalPrefab, loader.GetPrefab(reference));
                loader.ReleaseAll();

                Assert.IsTrue(
                    externalHandle.IsValid(),
                    "PrefabAssetLoader 不得释放 AssetReference 外部所有者的句柄");
                Assert.AreSame(externalPrefab, externalHandle.Result);
            }
            finally
            {
                loader.ReleaseAll();

                if (reference.OperationHandle.IsValid())
                {
                    reference.ReleaseAsset();
                }
                else if (externalHandle.IsValid())
                {
                    Addressables.Release(externalHandle);
                }
            }
        }

        sealed class ControlledPrefabLoadBackend : IPrefabAssetLoadBackend
        {
            readonly List<ControlledPrefabLoadHandle> _handles =
                new List<ControlledPrefabLoadHandle>();

            public IReadOnlyList<ControlledPrefabLoadHandle> Handles => _handles;

            public IPrefabAssetLoadHandle StartLoad(AssetReferenceGameObject reference)
            {
                ControlledPrefabLoadHandle handle = new ControlledPrefabLoadHandle();
                _handles.Add(handle);
                return handle;
            }
        }

        sealed class ControlledPrefabLoadHandle : IPrefabAssetLoadHandle
        {
            readonly UniTaskCompletionSource<GameObject> _completion =
                new UniTaskCompletionSource<GameObject>();

            public int ReleaseCount { get; private set; }

            public UniTask<GameObject> LoadAsync()
            {
                return _completion.Task;
            }

            public void Release()
            {
                ReleaseCount++;
            }

            public void Complete(GameObject prefab)
            {
                Assert.IsTrue(_completion.TrySetResult(prefab));
            }

            public void Fail(Exception exception)
            {
                Assert.IsTrue(_completion.TrySetException(exception));
            }
        }
    }
}
