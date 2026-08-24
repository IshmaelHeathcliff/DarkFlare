using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class SaveCoordinatorTests
    {
        [UnityTest]
        public IEnumerator DenseRequests_AreMergedIntoOnePendingCaptureAndCompleteExactlyOnce()
        {
            return VerifyDenseRequestMergingAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator CloseAndFlush_TimesOutBoundedlyAndLateCommitRemainsValid()
        {
            return VerifyBoundedFlushAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator UnexpectedStorageException_CompletesActiveAndPendingAfterWorkerRetires()
        {
            return VerifyUnexpectedStorageFailureAsync().ToCoroutine();
        }

        static async UniTask VerifyDenseRequestMergingAsync()
        {
            CoordinatorFixture fixture = CreateFixture(true);

            try
            {
                UniTask<SaveOperationResult> first = fixture.Coordinator.SaveAsync(
                    SaveCoordinator.AutoSlot).Preserve();
                await WaitForWriteOrFailureAsync(fixture, first);
                UniTask<SaveOperationResult> second = fixture.Coordinator.SaveAsync(
                    SaveCoordinator.AutoSlot).Preserve();
                UniTask<SaveOperationResult> third = fixture.Coordinator.SaveAsync(
                    SaveCoordinator.AutoSlot).Preserve();
                fixture.Files.ReleaseWrites.Set();
                SaveOperationResult firstResult = await first;
                SaveOperationResult secondResult = await second;
                SaveOperationResult thirdResult = await third;

                Assert.IsTrue(firstResult.Succeeded, firstResult.ErrorCode.ToString());
                Assert.IsTrue(secondResult.Succeeded, secondResult.ErrorCode.ToString());
                Assert.IsTrue(thirdResult.Succeeded, thirdResult.ErrorCode.ToString());
                Assert.AreEqual(1, firstResult.CommitSequence);
                Assert.AreEqual(2, secondResult.CommitSequence);
                Assert.AreEqual(2, thirdResult.CommitSequence);
                Assert.AreEqual(2, fixture.Source.CaptureCount);
                Assert.AreEqual(2, fixture.Files.WriteCount);
                Assert.IsFalse(fixture.Coordinator.IsBusy);
                Assert.IsFalse(fixture.Coordinator.HasDirtyChanges);
            }
            finally
            {
                await fixture.DisposeAsync();
            }
        }

        static async UniTask VerifyBoundedFlushAsync()
        {
            CoordinatorFixture fixture = CreateFixture(true);

            try
            {
                UniTask<SaveOperationResult> flush = fixture.Coordinator.CloseAndFlushAsync(
                    SaveCoordinator.AutoSlot,
                    TimeSpan.FromMilliseconds(50d)).Preserve();
                await WaitUntilAsync(() => fixture.Files.WriteEntered.IsSet || !fixture.Coordinator.IsBusy);

                if (!fixture.Files.WriteEntered.IsSet)
                {
                    SaveOperationResult early = await flush;
                    Assert.Fail($"Flush 未进入存储写入：{early.ErrorCode} {early.Exception}");
                }
                SaveOperationResult timedOut = await flush;

                Assert.AreEqual(SaveErrorCode.FlushTimedOut, timedOut.ErrorCode);
                Assert.IsFalse(fixture.Coordinator.IsAccepting);
                fixture.Files.ReleaseWrites.Set();
                await WaitUntilAsync(() => !fixture.Coordinator.IsBusy);
                LocalSaveLoadResult recovered = fixture.Storage.LoadLatest(SaveCoordinator.AutoSlot);

                Assert.IsTrue(recovered.Succeeded, recovered.Code.ToString());
                Assert.AreEqual(1, recovered.Document.Header.CommitSequence);
                Assert.AreEqual(SaveErrorCode.FlushTimedOut, timedOut.ErrorCode);
            }
            finally
            {
                fixture.Files.ReleaseWrites.Set();
                await fixture.DisposeAsync();
            }
        }

        static async UniTask VerifyUnexpectedStorageFailureAsync()
        {
            CoordinatorFixture fixture = CreateFixture(true, true);

            try
            {
                UniTask<SaveOperationResult> first = fixture.Coordinator.SaveAsync(
                    SaveCoordinator.AutoSlot).Preserve();
                await WaitForWriteOrFailureAsync(fixture, first);
                UniTask<SaveOperationResult> second = fixture.Coordinator.SaveAsync(
                    SaveCoordinator.AutoSlot).Preserve();
                UniTask<SaveOperationResult> third = fixture.Coordinator.SaveAsync(
                    SaveCoordinator.AutoSlot).Preserve();
                fixture.Files.ReleaseWrites.Set();
                SaveOperationResult firstResult = await first;
                SaveOperationResult secondResult = await second;
                SaveOperationResult thirdResult = await third;

                Assert.AreEqual(SaveErrorCode.CommitFailed, firstResult.ErrorCode);
                Assert.AreEqual(SaveErrorCode.CommitFailed, secondResult.ErrorCode);
                Assert.AreEqual(SaveErrorCode.CommitFailed, thirdResult.ErrorCode);
                Assert.IsFalse(fixture.Coordinator.IsBusy);
            }
            finally
            {
                fixture.Files.ReleaseWrites.Set();
                await fixture.DisposeAsync();
            }
        }

        static CoordinatorFixture CreateFixture(
            bool blockWrites,
            bool throwUnexpectedWriteException = false)
        {
            ResetArchitectureProvider();
            LifecycleScope applicationScope = LifecycleScope.CreateRoot(
                "SaveCoordinatorTests",
                taskStopTimeout: TimeSpan.FromSeconds(1d));
            LifecycleScope profileScope = applicationScope.CreateChild("Profile");
            ApplicationInputService inputService = new ApplicationInputService();
            GameSessionHost session = CreateGameSessionHost(
                profileScope,
                1,
                inputService);
            ContentCatalogDefinition definition = AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset");
            ContentCatalogBuildResult catalogBuild = ContentCatalog.Build(definition);
            Assert.IsTrue(
                catalogBuild.Succeeded,
                string.Join("\n", catalogBuild.Issues.Select(issue => issue.Message)));
            BlockingMemoryFileOperations files = new BlockingMemoryFileOperations(
                blockWrites,
                throwUnexpectedWriteException);
            LocalSaveStorage storage = new LocalSaveStorage(
                new TestSavePathProvider("C:/DarkFlare-SaveCoordinatorTests"),
                new NewtonsoftSaveSerializer(),
                files);
            FakeSnapshotSource source = new FakeSnapshotSource(
                session.ArchitectureGeneration,
                SaveDataContractTests.CreateValidDocument().Payload);
            SaveCoordinator coordinator = new SaveCoordinator(
                profileScope,
                storage,
                catalogBuild.Catalog,
                "test");
            coordinator.BindSession(session, source);
            return new CoordinatorFixture(
                applicationScope,
                session,
                inputService,
                coordinator,
                source,
                storage,
                files);
        }

        static async UniTask WaitUntilAsync(Func<bool> condition)
        {
            for (int i = 0; i < 200 && !condition(); i++)
            {
                await UniTask.Delay(10, DelayType.Realtime);
            }

            Assert.IsTrue(condition(), "等待异步存档条件超时");
        }

        static async UniTask WaitForWriteOrFailureAsync(
            CoordinatorFixture fixture,
            UniTask<SaveOperationResult> operation)
        {
            await WaitUntilAsync(() => fixture.Files.WriteEntered.IsSet || !fixture.Coordinator.IsBusy);

            if (!fixture.Files.WriteEntered.IsSet)
            {
                SaveOperationResult early = await operation;
                Assert.Fail($"保存未进入存储写入：{early.ErrorCode} {early.Exception}");
            }
        }

        static GameSessionHost CreateGameSessionHost(
            LifecycleScope profileScope,
            int sequence,
            ApplicationInputService inputService)
        {
            ConstructorInfo constructor = typeof(GameSessionHost).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(LifecycleScope),
                    typeof(int),
                    typeof(Action<GameSessionHost, string>),
                    typeof(ApplicationInputService),
                    typeof(IItemInstanceIdGenerator)
                },
                null);
            Assert.IsNotNull(constructor);
            return (GameSessionHost)constructor.Invoke(
                new object[] { profileScope, sequence, null, inputService, null });
        }

        static void ResetArchitectureProvider()
        {
            MethodInfo method = typeof(GameArchitectureProvider).GetMethod(
                "ResetStaticState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, null);
        }

        sealed class CoordinatorFixture
        {
            readonly LifecycleScope _applicationScope;
            readonly GameSessionHost _session;
            readonly ApplicationInputService _inputService;

            public CoordinatorFixture(
                LifecycleScope applicationScope,
                GameSessionHost session,
                ApplicationInputService inputService,
                SaveCoordinator coordinator,
                FakeSnapshotSource source,
                LocalSaveStorage storage,
                BlockingMemoryFileOperations files)
            {
                _applicationScope = applicationScope;
                _session = session;
                _inputService = inputService;
                Coordinator = coordinator;
                Source = source;
                Storage = storage;
                Files = files;
            }

            public SaveCoordinator Coordinator { get; }

            public FakeSnapshotSource Source { get; }

            public LocalSaveStorage Storage { get; }

            public BlockingMemoryFileOperations Files { get; }

            public async UniTask DisposeAsync()
            {
                Coordinator.EmergencyClose();
                _session.EmergencyStop();
                _applicationScope.BeginStop();
                await _applicationScope.StopAsync();
                ResetArchitectureProvider();
                _inputService.Dispose();
            }
        }

        sealed class FakeSnapshotSource : ISessionSnapshotSource
        {
            readonly SavePayloadDto _payload;
            bool _invalidated;

            public FakeSnapshotSource(int generation, SavePayloadDto payload)
            {
                ArchitectureGeneration = generation;
                _payload = payload;
            }

            public int ArchitectureGeneration { get; }

            public bool IsAvailable => !_invalidated;

            public int CaptureCount { get; private set; }

            public SessionSnapshotResult Capture()
            {
                CaptureCount++;
                return new SessionSnapshotResult(_payload, Array.Empty<DtoMapIssue>());
            }

            public void Invalidate()
            {
                _invalidated = true;
            }
        }

        sealed class TestSavePathProvider : ISavePathProvider
        {
            public TestSavePathProvider(string rootPath)
            {
                RootPath = rootPath;
            }

            public string RootPath { get; }
        }

        sealed class BlockingMemoryFileOperations : ILocalSaveFileOperations
        {
            readonly object _gate = new object();
            readonly HashSet<string> _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            readonly bool _blockWrites;
            readonly bool _throwUnexpectedWriteException;

            public BlockingMemoryFileOperations(
                bool blockWrites,
                bool throwUnexpectedWriteException)
            {
                _blockWrites = blockWrites;
                _throwUnexpectedWriteException = throwUnexpectedWriteException;

                if (!blockWrites)
                {
                    ReleaseWrites.Set();
                }
            }

            public ManualResetEventSlim WriteEntered { get; } = new ManualResetEventSlim(false);

            public ManualResetEventSlim ReleaseWrites { get; } = new ManualResetEventSlim(false);

            public int WriteCount { get; private set; }

            public bool DirectoryExists(string path)
            {
                lock (_gate)
                {
                    return _directories.Contains(Normalize(path));
                }
            }

            public void CreateDirectory(string path)
            {
                lock (_gate)
                {
                    _directories.Add(Normalize(path));
                }
            }

            public IReadOnlyList<string> EnumerateFiles(string path)
            {
                string directory = Normalize(path).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                lock (_gate)
                {
                    return _files.Keys
                        .Where(file => file.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                }
            }

            public bool FileExists(string path)
            {
                lock (_gate)
                {
                    return _files.ContainsKey(Normalize(path));
                }
            }

            public long GetFileLength(string path)
            {
                lock (_gate)
                {
                    return _files[Normalize(path)].LongLength;
                }
            }

            public byte[] ReadAllBytes(string path)
            {
                lock (_gate)
                {
                    return _files[Normalize(path)].ToArray();
                }
            }

            public void WriteAllBytesDurable(string path, byte[] bytes)
            {
                WriteEntered.Set();

                if (_blockWrites)
                {
                    ReleaseWrites.Wait();
                }

                if (_throwUnexpectedWriteException)
                {
                    throw new InvalidOperationException("测试注入的非存储异常");
                }

                lock (_gate)
                {
                    WriteCount++;
                    _files.Add(Normalize(path), bytes.ToArray());
                }
            }

            public void MoveFileNoOverwrite(string sourcePath, string destinationPath)
            {
                lock (_gate)
                {
                    string source = Normalize(sourcePath);
                    string destination = Normalize(destinationPath);
                    _files.Add(destination, _files[source]);
                    _files.Remove(source);
                }
            }

            public void DeleteFile(string path)
            {
                lock (_gate)
                {
                    _files.Remove(Normalize(path));
                }
            }

            static string Normalize(string path)
            {
                return Path.GetFullPath(path);
            }
        }
    }
}
