using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class LocalSaveStorageTests
    {
        string _rootPath;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(
                Path.GetTempPath(),
                "DarkFlareSaveTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            string fullRoot = Path.GetFullPath(_rootPath);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "DarkFlareSaveTests"));
            StringAssert.StartsWith(
                expectedParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                fullRoot);

            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }

        [Test]
        public void Commit_FirstAndThreeGenerationsKeepLatestTwoWithMetadata()
        {
            LocalSaveStorage storage = CreateStorage();

            for (int sequence = 1; sequence <= 3; sequence++)
            {
                LocalSaveCommitResult committed = storage.Commit(
                    SaveSlotId.Parse("auto"),
                    CreateDocument(sequence));
                Assert.IsTrue(committed.Succeeded, Describe(committed));
                Assert.AreEqual(sequence, committed.Document.Header.CommitSequence);
            }

            string slotPath = Path.Combine(_rootPath, "auto");
            CollectionAssert.AreEquivalent(
                new[] { "2.save", "3.save" },
                Directory.GetFiles(slotPath, "*.save").Select(Path.GetFileName).ToArray());

            LocalSaveLoadResult loaded = storage.LoadLatest(SaveSlotId.Parse("auto"));
            Assert.IsTrue(loaded.Succeeded, Describe(loaded));
            Assert.IsFalse(loaded.RecoveredFromBackup);
            Assert.AreEqual(3, loaded.Document.Header.CommitSequence);
            Assert.AreEqual(3, loaded.Metadata.Current.CommitSequence);
            Assert.AreEqual(2, loaded.Metadata.Backup.CommitSequence);
        }

        [Test]
        public void LoadLatest_CorruptCurrentFallsBackButAllCorruptFailsSafely()
        {
            LocalSaveStorage storage = CreateStorage();
            Assert.IsTrue(storage.Commit(SaveSlotId.Parse("auto"), CreateDocument(1)).Succeeded);
            Assert.IsTrue(storage.Commit(SaveSlotId.Parse("auto"), CreateDocument(2)).Succeeded);
            string slotPath = Path.Combine(_rootPath, "auto");
            File.WriteAllText(Path.Combine(slotPath, "2.save"), "{\"broken\":true}");

            LocalSaveLoadResult backup = storage.LoadLatest(SaveSlotId.Parse("auto"));

            Assert.IsTrue(backup.Succeeded, Describe(backup));
            Assert.IsTrue(backup.RecoveredFromBackup);
            Assert.AreEqual(1, backup.Document.Header.CommitSequence);
            Assert.That(
                backup.Metadata.InvalidGenerations.Select(failure => failure.CommitSequence),
                Has.Member(2));

            File.WriteAllText(Path.Combine(slotPath, "1.save"), "not-json");
            LocalSaveLoadResult failed = storage.LoadLatest(SaveSlotId.Parse("auto"));
            Assert.AreEqual(LocalSaveStorageCode.NoValidGeneration, failed.Code);
            Assert.IsNull(failed.Document);
        }

        [Test]
        public void Commit_MoveFailureOrCancellationPreservesPreviousGenerationAndCleansTemporaryFile()
        {
            FaultInjectingFileOperations files = new FaultInjectingFileOperations();
            LocalSaveStorage storage = CreateStorage(files);
            Assert.IsTrue(storage.Commit(SaveSlotId.Parse("auto"), CreateDocument(1)).Succeeded);
            files.BeforeMove = () => throw new IOException("injected move failure");

            LocalSaveCommitResult interrupted = storage.Commit(
                SaveSlotId.Parse("auto"),
                CreateDocument(2));

            Assert.AreEqual(LocalSaveStorageCode.IoFailure, interrupted.Code);
            AssertPreviousGenerationAndNoTemporaryFile(storage, 1);

            files.BeforeMove = null;
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                files.AfterWrite = cancellation.Cancel;
                LocalSaveCommitResult cancelled = storage.Commit(
                    SaveSlotId.Parse("auto"),
                    CreateDocument(2),
                    cancellation.Token);
                Assert.AreEqual(LocalSaveStorageCode.Cancelled, cancelled.Code);
            }

            AssertPreviousGenerationAndNoTemporaryFile(storage, 1);
        }

        [Test]
        public void LoadLatest_DeletesStaleTemporaryFilesAndKeepsSlotsIsolated()
        {
            LocalSaveStorage storage = CreateStorage();
            Assert.IsTrue(storage.Commit(SaveSlotId.Parse("auto"), CreateDocument(1)).Succeeded);
            SaveDocumentDto manual = CreateDocument(1);
            manual.Header.SlotId = "manual";
            Assert.IsTrue(storage.Commit(SaveSlotId.Parse("manual"), manual).Succeeded);
            string stale = Path.Combine(_rootPath, "auto", "stale.tmp");
            File.WriteAllText(stale, "interrupted");

            LocalSaveLoadResult auto = storage.LoadLatest(SaveSlotId.Parse("auto"));
            LocalSaveLoadResult manualResult = storage.LoadLatest(SaveSlotId.Parse("manual"));

            Assert.IsTrue(auto.Succeeded, Describe(auto));
            Assert.IsTrue(manualResult.Succeeded, Describe(manualResult));
            Assert.IsFalse(File.Exists(stale));
            Assert.AreEqual("auto", auto.Document.Header.SlotId);
            Assert.AreEqual("manual", manualResult.Document.Header.SlotId);
        }

        [Test]
        public void ConcurrentOperation_IsRejectedWhileFirstCommitOwnsStorage()
        {
            FaultInjectingFileOperations files = new FaultInjectingFileOperations();
            using (ManualResetEventSlim enteredWrite = new ManualResetEventSlim())
            using (ManualResetEventSlim releaseWrite = new ManualResetEventSlim())
            {
                files.BeforeWrite = () =>
                {
                    enteredWrite.Set();
                    releaseWrite.Wait(TimeSpan.FromSeconds(5));
                };
                LocalSaveStorage storage = CreateStorage(files);
                Task<LocalSaveCommitResult> firstTask = Task.Run(() => storage.Commit(
                    SaveSlotId.Parse("auto"),
                    CreateDocument(1)));
                Assert.IsTrue(enteredWrite.Wait(TimeSpan.FromSeconds(5)));

                LocalSaveCommitResult concurrent = storage.Commit(
                    SaveSlotId.Parse("auto"),
                    CreateDocument(2));

                Assert.AreEqual(LocalSaveStorageCode.Busy, concurrent.Code);
                releaseWrite.Set();
                Assert.IsTrue(firstTask.Result.Succeeded, Describe(firstTask.Result));
            }
        }

        [Test]
        public void Commit_RejectsNonMonotonicSequenceWithoutTouchingCurrent()
        {
            LocalSaveStorage storage = CreateStorage();
            Assert.IsTrue(storage.Commit(SaveSlotId.Parse("auto"), CreateDocument(2)).Succeeded);

            LocalSaveCommitResult conflict = storage.Commit(
                SaveSlotId.Parse("auto"),
                CreateDocument(1));

            Assert.AreEqual(LocalSaveStorageCode.CommitConflict, conflict.Code);
            LocalSaveLoadResult loaded = storage.LoadLatest(SaveSlotId.Parse("auto"));
            Assert.AreEqual(2, loaded.Document.Header.CommitSequence);
        }

        [Test]
        public void DeleteSlot_RemovesOnlyOwnedFilesAndIsIdempotent()
        {
            LocalSaveStorage storage = CreateStorage();
            SaveSlotId auto = SaveSlotId.Parse("auto");
            SaveSlotId manual = SaveSlotId.Parse("manual");
            Assert.IsTrue(storage.Commit(auto, CreateDocument(1)).Succeeded);
            Assert.IsTrue(storage.Commit(auto, CreateDocument(2)).Succeeded);
            SaveDocumentDto manualDocument = CreateDocument(1);
            manualDocument.Header.SlotId = manual.Value;
            Assert.IsTrue(storage.Commit(manual, manualDocument).Succeeded);
            string autoPath = Path.Combine(_rootPath, auto.Value);
            string temporaryPath = Path.Combine(autoPath, "interrupted.tmp");
            string unrelatedPath = Path.Combine(autoPath, "do-not-delete.txt");
            File.WriteAllText(temporaryPath, "temporary");
            File.WriteAllText(unrelatedPath, "unrelated");

            LocalSaveDeleteResult deleted = storage.DeleteSlot(auto);
            LocalSaveDeleteResult repeated = storage.DeleteSlot(auto);

            Assert.IsTrue(deleted.Succeeded, deleted.Exception?.ToString());
            Assert.AreEqual(3, deleted.DeletedFileCount);
            Assert.IsTrue(repeated.Succeeded, repeated.Exception?.ToString());
            Assert.AreEqual(0, repeated.DeletedFileCount);
            Assert.AreEqual(LocalSaveStorageCode.SlotNotFound, storage.LoadLatest(auto).Code);
            Assert.IsTrue(storage.LoadLatest(manual).Succeeded);
            Assert.IsTrue(File.Exists(unrelatedPath));
        }

        [Test]
        public void DeleteSlot_IoFailureReportsPartialStateWithoutTouchingOtherSlots()
        {
            FaultInjectingFileOperations files = new FaultInjectingFileOperations();
            LocalSaveStorage storage = CreateStorage(files);
            SaveSlotId auto = SaveSlotId.Parse("auto");
            SaveSlotId manual = SaveSlotId.Parse("manual");
            Assert.IsTrue(storage.Commit(auto, CreateDocument(1)).Succeeded);
            SaveDocumentDto manualDocument = CreateDocument(1);
            manualDocument.Header.SlotId = manual.Value;
            Assert.IsTrue(storage.Commit(manual, manualDocument).Succeeded);
            files.BeforeDelete = () => throw new IOException("injected delete failure");

            LocalSaveDeleteResult result = storage.DeleteSlot(auto);

            Assert.AreEqual(LocalSaveStorageCode.IoFailure, result.Code);
            Assert.AreEqual(0, result.DeletedFileCount);
            Assert.IsInstanceOf<IOException>(result.Exception);
            files.BeforeDelete = null;
            Assert.IsTrue(storage.LoadLatest(auto).Succeeded);
            Assert.IsTrue(storage.LoadLatest(manual).Succeeded);
        }

        LocalSaveStorage CreateStorage(ILocalSaveFileOperations files = null)
        {
            return new LocalSaveStorage(
                new TestSavePathProvider(_rootPath),
                new NewtonsoftSaveSerializer(),
                files);
        }

        static SaveDocumentDto CreateDocument(long sequence)
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            document.Header.CommitSequence = sequence;
            document.Header.UpdatedUtc = new DateTimeOffset(
                    2026,
                    8,
                    19,
                    0,
                    0,
                    0,
                    TimeSpan.Zero)
                .AddMinutes(sequence)
                .ToString("O");
            return document;
        }

        void AssertPreviousGenerationAndNoTemporaryFile(
            LocalSaveStorage storage,
            long expectedSequence)
        {
            LocalSaveLoadResult loaded = storage.LoadLatest(SaveSlotId.Parse("auto"));
            Assert.IsTrue(loaded.Succeeded, Describe(loaded));
            Assert.AreEqual(expectedSequence, loaded.Document.Header.CommitSequence);
            Assert.IsEmpty(Directory.GetFiles(
                Path.Combine(_rootPath, "auto"),
                "*.tmp",
                SearchOption.TopDirectoryOnly));
        }

        static string Describe(LocalSaveCommitResult result)
        {
            return result.Exception?.ToString()
                ?? $"{result.Code} / {result.SerializationCode} / "
                + string.Join("; ", result.ValidationIssues.Select(issue => issue.Message));
        }

        static string Describe(LocalSaveLoadResult result)
        {
            return result.Exception?.ToString() ?? result.Code.ToString();
        }

        sealed class TestSavePathProvider : ISavePathProvider
        {
            public string RootPath { get; }

            public TestSavePathProvider(string rootPath)
            {
                RootPath = rootPath;
            }
        }

        sealed class FaultInjectingFileOperations : ILocalSaveFileOperations
        {
            readonly SystemLocalSaveFileOperations _inner = new SystemLocalSaveFileOperations();

            public Action BeforeWrite { get; set; }

            public Action AfterWrite { get; set; }

            public Action BeforeMove { get; set; }

            public Action BeforeDelete { get; set; }

            public bool DirectoryExists(string path)
            {
                return _inner.DirectoryExists(path);
            }

            public void CreateDirectory(string path)
            {
                _inner.CreateDirectory(path);
            }

            public IReadOnlyList<string> EnumerateFiles(string path)
            {
                return _inner.EnumerateFiles(path);
            }

            public bool FileExists(string path)
            {
                return _inner.FileExists(path);
            }

            public long GetFileLength(string path)
            {
                return _inner.GetFileLength(path);
            }

            public byte[] ReadAllBytes(string path)
            {
                return _inner.ReadAllBytes(path);
            }

            public void WriteAllBytesDurable(string path, byte[] bytes)
            {
                BeforeWrite?.Invoke();
                _inner.WriteAllBytesDurable(path, bytes);
                AfterWrite?.Invoke();
            }

            public void MoveFileNoOverwrite(string sourcePath, string destinationPath)
            {
                BeforeMove?.Invoke();
                _inner.MoveFileNoOverwrite(sourcePath, destinationPath);
            }

            public void DeleteFile(string path)
            {
                BeforeDelete?.Invoke();
                _inner.DeleteFile(path);
            }
        }
    }
}
