using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha026DiagnosticsTests
    {
        IDisposable _logInstallation;

        [SetUp]
        public void SetUp()
        {
            _logInstallation = ApplicationLog.Install(new ApplicationLogger(
                new IApplicationLogSink[] { new RingBufferLogSink(32) }));
        }

        [TearDown]
        public void TearDown()
        {
            _logInstallation?.Dispose();
            ApplicationLog.Reset();
        }

        [Test]
        public void EventCatalog_HasValidUniqueStableIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < LogEventIds.All.Count; i++)
            {
                string value = LogEventIds.All[i].Value;
                Assert.IsTrue(LogEventId.IsValid(value), value);
                Assert.IsTrue(ids.Add(value), $"日志事件 ID 重复：{value}");
            }
        }

        [Test]
        public void RingBuffer_OverwritesOldestAndKeepsSequenceOrder()
        {
            RingBufferLogSink buffer = new RingBufferLogSink(2);
            ApplicationLogger logger = new ApplicationLogger(
                new IApplicationLogSink[] { buffer });

            logger.Write(ApplicationLogLevel.Info, LogEventIds.InfrastructureLifecycle, "one");
            logger.Write(ApplicationLogLevel.Warning, LogEventIds.InfrastructureLifecycle, "two");
            logger.Write(ApplicationLogLevel.Error, LogEventIds.InfrastructureLifecycle, "three");

            IReadOnlyList<ApplicationLogEntry> snapshot = buffer.Snapshot();
            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual("two", snapshot[0].Message);
            Assert.AreEqual("three", snapshot[1].Message);
            Assert.Less(snapshot[0].Sequence, snapshot[1].Sequence);
        }

        [Test]
        public void Logger_DisablesFailingSinkWithoutBlockingHealthySink()
        {
            ThrowingSink failing = new ThrowingSink();
            RingBufferLogSink buffer = new RingBufferLogSink(4);
            ApplicationLogger logger = new ApplicationLogger(
                new IApplicationLogSink[] { failing, buffer });

            logger.Write(ApplicationLogLevel.Info, LogEventIds.InfrastructureLifecycle, "first");
            logger.Write(ApplicationLogLevel.Info, LogEventIds.InfrastructureLifecycle, "second");

            Assert.AreEqual(1, failing.WriteCount);
            Assert.AreEqual(2, buffer.Snapshot().Count);
        }

        [Test]
        public void ConsoleMinimumLevel_DoesNotDiscardLowerLevelsFromRingBuffer()
        {
            RingBufferLogSink consoleBuffer = new RingBufferLogSink(4);
            RingBufferLogSink buffer = new RingBufferLogSink(4);
            ApplicationLogger logger = new ApplicationLogger(
                new IApplicationLogSink[]
                {
                    new MinimumLevelLogSink(
                        consoleBuffer,
                        ApplicationLogLevel.Warning),
                    buffer,
                });

            logger.Write(
                ApplicationLogLevel.Info,
                LogEventIds.InfrastructureLifecycle,
                "buffer-only");
            logger.Write(
                ApplicationLogLevel.Warning,
                LogEventIds.InfrastructureLifecycle,
                "console-and-buffer");

            IReadOnlyList<ApplicationLogEntry> snapshot = buffer.Snapshot();
            IReadOnlyList<ApplicationLogEntry> consoleSnapshot = consoleBuffer.Snapshot();
            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual(1, consoleSnapshot.Count);
            Assert.AreEqual("buffer-only", snapshot[0].Message);
            Assert.AreEqual("console-and-buffer", snapshot[1].Message);
            Assert.AreEqual("console-and-buffer", consoleSnapshot[0].Message);
        }

        [Test]
        public void FailureCoordinator_PreservesFirstFailureAndCountsDuplicates()
        {
            ApplicationFailureCoordinator coordinator = new ApplicationFailureCoordinator();
            ApplicationFatalFailure first = new ApplicationFatalFailure(
                LogEventIds.UnityUnhandledException,
                new InvalidOperationException("first"),
                false);
            ApplicationFatalFailure second = new ApplicationFatalFailure(
                LogEventIds.UniTaskUnobservedException,
                new InvalidOperationException("second"),
                false);
            int callbackCount = 0;
            coordinator.FatalReported += _ => callbackCount++;

            Assert.IsTrue(coordinator.TryReport(first));
            Assert.IsFalse(coordinator.TryReport(second));
            Assert.AreSame(first, coordinator.FirstFailure);
            Assert.AreEqual(1, coordinator.DuplicateCount);
            Assert.AreEqual(1, callbackCount);
        }

        [Test]
        public void LogContext_SanitizesNewLinesAndBoundsValues()
        {
            ApplicationLogContext context = new ApplicationLogContext(
                scope: "scope\nname",
                operation: new string('x', 200));

            Assert.AreEqual("scope name", context.Scope);
            Assert.AreEqual(128, context.Operation.Length);
        }

        [Test]
        public void PlayerErrorCatalog_OnlyOffersProvenResourceActions()
        {
            PlayerErrorPresentation optional = PlayerErrorCatalog.From(
                ResourceErrorCode.LoadFailed,
                false,
                true);
            PlayerErrorPresentation required = PlayerErrorCatalog.From(
                ResourceErrorCode.LoadFailed,
                true,
                true);

            Assert.AreEqual(PlayerErrorSeverity.Error, optional.Severity);
            Assert.IsTrue(optional.HasAction(PlayerErrorAction.Dismiss));
            Assert.IsTrue(optional.HasAction(PlayerErrorAction.Retry));
            Assert.IsFalse(optional.HasAction(PlayerErrorAction.Quit));
            Assert.AreEqual(PlayerErrorSeverity.Fatal, required.Severity);
            Assert.IsTrue(required.HasAction(PlayerErrorAction.Quit));
            Assert.IsFalse(required.HasAction(PlayerErrorAction.Retry));
            Assert.AreEqual("resource.error.load_failed", required.Message.EntryKey);
        }

        [Test]
        public void PlayerErrorCatalog_UnhandledFailureIsQuitOnly()
        {
            PlayerErrorPresentation presentation = PlayerErrorCatalog.Unhandled();

            Assert.AreEqual(PlayerErrorSeverity.Fatal, presentation.Severity);
            Assert.AreEqual(PlayerErrorAction.Quit, presentation.Actions);
            Assert.AreEqual("flow.error.unhandled_exception", presentation.Message.EntryKey);
        }

        [UnityTest]
        public IEnumerator ResourceService_SingleFlightsAcrossOwnersAndReleasesExactlyOnce()
        {
            return VerifyResourceServiceSingleFlightAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator ResourceService_RejectsTypeMismatchAndClosedOwner()
        {
            return VerifyResourceServiceGuardsAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator ResourceService_FailureCancellationAndEarlyOwnerCloseReleaseEverything()
        {
            return VerifyResourceServiceFaultMatrixAsync().ToCoroutine();
        }

        static async UniTask VerifyResourceServiceSingleFlightAsync()
        {
            GameObject asset = new GameObject("ResourceService-Test");
            FakeAssetBackend backend = new FakeAssetBackend(asset);
            AddressableAssetService service = new AddressableAssetService(backend);
            AssetOwnerScope firstOwner = service.CreateOwner("first");
            AssetOwnerScope secondOwner = service.CreateOwner("second");

            try
            {
                ResourceLoadResult<GameObject> first = await service.AcquireAsync<GameObject>(
                    firstOwner,
                    "runtime-key",
                    "stable-key");
                Assert.IsNotNull(first, "首次加载必须返回结果");
                ResourceLoadResult<GameObject> second = await service.AcquireAsync<GameObject>(
                    secondOwner,
                    "runtime-key",
                    "stable-key");
                Assert.IsNotNull(second, "复用加载必须返回结果");

                Assert.IsTrue(first.Succeeded);
                Assert.IsTrue(second.Succeeded);
                Assert.AreSame(asset, first.Lease.Asset);
                Assert.AreEqual(1, backend.StartCount);
                Assert.AreEqual(2, service.GetDiagnostics().ActiveLeases);

                first.Lease.Dispose();
                Assert.AreEqual(0, backend.ReleaseCount);
                secondOwner.Close();
                Assert.AreEqual(1, backend.ReleaseCount);
                Assert.AreEqual(0, service.GetDiagnostics().ActiveEntries);
            }
            finally
            {
                TryCleanup(firstOwner.Close);
                TryCleanup(secondOwner.Close);
                TryCleanup(service.Dispose);
                TryCleanup(() => UnityEngine.Object.DestroyImmediate(asset));
            }
        }

        static async UniTask VerifyResourceServiceGuardsAsync()
        {
            GameObject asset = new GameObject("ResourceService-Type-Test");
            FakeAssetBackend backend = new FakeAssetBackend(asset);
            AddressableAssetService service = new AddressableAssetService(backend);
            AssetOwnerScope owner = service.CreateOwner("owner");

            try
            {
                ResourceLoadResult<GameObject> loaded = await service.AcquireAsync<GameObject>(
                    owner,
                    "runtime-key",
                    "stable-key");
                Assert.IsNotNull(loaded, "首次加载必须返回结果");
                ResourceLoadResult<Texture2D> mismatch = await service.AcquireAsync<Texture2D>(
                    owner,
                    "runtime-key",
                    "stable-key");
                Assert.IsNotNull(mismatch, "类型冲突必须返回失败结果");

                Assert.IsTrue(loaded.Succeeded);
                Assert.AreEqual(ResourceErrorCode.TypeMismatch, mismatch.Code);
                owner.Close();
                ResourceLoadResult<GameObject> closed = await service.AcquireAsync<GameObject>(
                    owner,
                    "runtime-key",
                    "other-key");
                Assert.IsNotNull(closed, "关闭 owner 后必须返回失败结果");
                Assert.AreEqual(ResourceErrorCode.OwnerClosed, closed.Code);
            }
            finally
            {
                TryCleanup(owner.Close);
                TryCleanup(service.Dispose);
                TryCleanup(() => UnityEngine.Object.DestroyImmediate(asset));
            }
        }

        static async UniTask VerifyResourceServiceFaultMatrixAsync()
        {
            GameObject asset = new GameObject("ResourceService-Fault-Test");
            ControlledAssetBackend backend = new ControlledAssetBackend(asset);
            AddressableAssetService service = new AddressableAssetService(backend);
            AssetOwnerScope owner = service.CreateOwner("fault-owner");

            try
            {
                backend.FailNext = true;
                ResourceLoadResult<GameObject> failed = await service.AcquireAsync<GameObject>(
                    owner,
                    "failed-runtime-key",
                    "failed-key");
                Assert.AreEqual(ResourceErrorCode.LoadFailed, failed.Code);
                Assert.AreEqual(1, backend.ReleaseCount);

                CancellationTokenSource cancellation = new CancellationTokenSource();
                UniTask<ResourceLoadResult<GameObject>> cancelledTask = service.AcquireAsync<GameObject>(
                    owner,
                    "cancelled-runtime-key",
                    "cancelled-key",
                    cancellation.Token);
                cancellation.Cancel();
                ResourceLoadResult<GameObject> cancelled = await cancelledTask;
                cancellation.Dispose();
                Assert.AreEqual(ResourceErrorCode.Cancelled, cancelled.Code);
                Assert.AreEqual(2, backend.ReleaseCount);

                UniTask<ResourceLoadResult<GameObject>> closedTask = service.AcquireAsync<GameObject>(
                    owner,
                    "closed-runtime-key",
                    "closed-key");
                owner.Close();
                backend.LastHandle.Succeed();
                ResourceLoadResult<GameObject> closed = await closedTask;
                Assert.AreEqual(ResourceErrorCode.OwnerClosed, closed.Code);
                Assert.AreEqual(3, backend.ReleaseCount);
                ResourceDiagnosticsSnapshot diagnostics = service.GetDiagnostics();
                Assert.AreEqual(0, diagnostics.ActiveEntries);
                Assert.AreEqual(0, diagnostics.ActiveLeases);
                Assert.AreEqual(0, diagnostics.InFlightLoads);
            }
            finally
            {
                TryCleanup(owner.Close);
                TryCleanup(service.Dispose);
                TryCleanup(() => UnityEngine.Object.DestroyImmediate(asset));
            }
        }

        sealed class ThrowingSink : IApplicationLogSink
        {
            public int WriteCount { get; private set; }

            public void Write(ApplicationLogEntry entry)
            {
                WriteCount++;
                throw new InvalidOperationException("sink failure");
            }
        }

        static void TryCleanup(Action cleanup)
        {
            try
            {
                cleanup.Invoke();
            }
            catch (Exception exception)
            {
                TestContext.WriteLine(exception);
            }
        }

        sealed class FakeAssetBackend : IAddressableAssetBackend
        {
            readonly UnityEngine.Object _asset;

            public FakeAssetBackend(UnityEngine.Object asset)
            {
                _asset = asset;
            }

            public int StartCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public IAddressableAssetLoadHandle<T> StartLoad<T>(object runtimeKey)
                where T : UnityEngine.Object
            {
                StartCount++;
                return new FakeAssetHandle<T>((T)_asset, () => ReleaseCount++);
            }
        }

        sealed class FakeAssetHandle<T> : IAddressableAssetLoadHandle<T>
            where T : UnityEngine.Object
        {
            readonly T _asset;
            readonly Action _released;

            bool _isReleased;

            public FakeAssetHandle(T asset, Action released)
            {
                _asset = asset;
                _released = released;
            }

            public UniTask<T> LoadAsync()
            {
                return UniTask.FromResult(_asset);
            }

            public void Release()
            {
                if (_isReleased)
                {
                    return;
                }

                _isReleased = true;
                _released.Invoke();
            }
        }

        sealed class ControlledAssetBackend : IAddressableAssetBackend
        {
            readonly UnityEngine.Object _asset;

            public ControlledAssetBackend(UnityEngine.Object asset)
            {
                _asset = asset;
            }

            public bool FailNext { get; set; }

            public int ReleaseCount { get; private set; }

            public ControlledAssetHandle LastHandle { get; private set; }

            public IAddressableAssetLoadHandle<T> StartLoad<T>(object runtimeKey)
                where T : UnityEngine.Object
            {
                ControlledAssetHandle handle = new ControlledAssetHandle(
                    (GameObject)_asset,
                    () => ReleaseCount++);
                LastHandle = handle;

                if (FailNext)
                {
                    FailNext = false;
                    handle.Fail(new InvalidOperationException("injected resource failure"));
                }

                return (IAddressableAssetLoadHandle<T>)(object)handle;
            }
        }

        sealed class ControlledAssetHandle : IAddressableAssetLoadHandle<GameObject>
        {
            readonly GameObject _asset;
            readonly Action _released;
            readonly UniTaskCompletionSource<GameObject> _completion =
                new UniTaskCompletionSource<GameObject>();

            bool _isReleased;

            public ControlledAssetHandle(GameObject asset, Action released)
            {
                _asset = asset;
                _released = released;
            }

            public UniTask<GameObject> LoadAsync()
            {
                return _completion.Task;
            }

            public void Succeed()
            {
                _completion.TrySetResult(_asset);
            }

            public void Fail(Exception exception)
            {
                _completion.TrySetException(exception);
            }

            public void Release()
            {
                if (_isReleased)
                {
                    return;
                }

                _isReleased = true;
                _released.Invoke();
                _completion.TrySetCanceled();
            }
        }
    }
}
