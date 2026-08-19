using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class LocalSettingsStorageTests
    {
        string _rootPath;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(
                Path.GetTempPath(),
                "DarkFlareSettingsTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            string fullRoot = Path.GetFullPath(_rootPath);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "DarkFlareSettingsTests"));
            StringAssert.StartsWith(
                expectedParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                fullRoot);

            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }

        [Test]
        public void CommitAndLoad_KeepCurrentAndBackupAndRecoverCorruptCurrent()
        {
            LocalSettingsStorage storage = CreateStorage();
            Assert.IsTrue(storage.Commit(CreateDocument(UserLanguagePreference.SimplifiedChinese)).Succeeded);
            Assert.IsTrue(storage.Commit(CreateDocument(UserLanguagePreference.English)).Succeeded);

            LocalSettingsLoadResult current = storage.Load();
            Assert.IsTrue(current.Succeeded, Describe(current));
            Assert.AreEqual(SettingsRecoverySource.Current, current.RecoverySource);
            Assert.AreEqual(UserLanguagePreference.English, current.Document.Payload.Language);

            File.WriteAllText(Path.Combine(_rootPath, "settings.json"), "not-json");
            LocalSettingsLoadResult recovered = storage.Load();

            Assert.IsTrue(recovered.Succeeded, Describe(recovered));
            Assert.AreEqual(SettingsRecoverySource.Backup, recovered.RecoverySource);
            Assert.AreEqual(
                UserLanguagePreference.SimplifiedChinese,
                recovered.Document.Payload.Language);
            Assert.IsTrue(File.Exists(Path.Combine(_rootPath, "settings.corrupt")));
        }

        [Test]
        public void Commit_SecondMoveFailureLeavesReadableBackupAndCleansTemporaryFile()
        {
            FaultInjectingFileOperations files = new FaultInjectingFileOperations();
            LocalSettingsStorage storage = CreateStorage(files);
            Assert.IsTrue(storage.Commit(CreateDocument(UserLanguagePreference.SimplifiedChinese)).Succeeded);
            files.ThrowOnMoveNumber = files.MoveCount + 2;

            LocalSettingsCommitResult interrupted = storage.Commit(
                CreateDocument(UserLanguagePreference.English));

            Assert.AreEqual(LocalSettingsStorageCode.IoFailure, interrupted.Code);
            LocalSettingsLoadResult recovered = storage.Load();
            Assert.IsTrue(recovered.Succeeded, Describe(recovered));
            Assert.AreEqual(SettingsRecoverySource.Backup, recovered.RecoverySource);
            Assert.AreEqual(
                UserLanguagePreference.SimplifiedChinese,
                recovered.Document.Payload.Language);
            Assert.IsEmpty(Directory.GetFiles(_rootPath, "*.tmp", SearchOption.TopDirectoryOnly));
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
                LocalSettingsStorage storage = CreateStorage(files);
                Task<LocalSettingsCommitResult> first = Task.Run(() => storage.Commit(
                    CreateDocument(UserLanguagePreference.Auto)));
                Assert.IsTrue(enteredWrite.Wait(TimeSpan.FromSeconds(5)));

                LocalSettingsCommitResult concurrent = storage.Commit(
                    CreateDocument(UserLanguagePreference.English));

                Assert.AreEqual(LocalSettingsStorageCode.Busy, concurrent.Code);
                releaseWrite.Set();
                Assert.IsTrue(first.Result.Succeeded);
            }
        }

        LocalSettingsStorage CreateStorage(ILocalFileOperations files = null)
        {
            return new LocalSettingsStorage(
                new TestSettingsPathProvider(_rootPath),
                new NewtonsoftSettingsSerializer(),
                files);
        }

        static UserSettingsDocumentDto CreateDocument(UserLanguagePreference language)
        {
            UserSettingsDocumentDto document = SettingsDataContractTests.CreateValidDocument(language);
            document.UpdatedUtc = DateTimeOffset.UtcNow.ToString("O");
            return document;
        }

        static string Describe(LocalSettingsLoadResult result)
        {
            return result.Exception?.ToString()
                ?? $"{result.Code} / {result.SerializationCode} / {result.RecoverySource}";
        }

        sealed class TestSettingsPathProvider : ISettingsPathProvider
        {
            public string RootPath { get; }

            public TestSettingsPathProvider(string rootPath)
            {
                RootPath = rootPath;
            }
        }

        sealed class FaultInjectingFileOperations : ILocalFileOperations
        {
            readonly SystemLocalSaveFileOperations _inner = new SystemLocalSaveFileOperations();

            public Action BeforeWrite { get; set; }

            public int ThrowOnMoveNumber { get; set; }

            public int MoveCount { get; private set; }

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
            }

            public void MoveFileNoOverwrite(string sourcePath, string destinationPath)
            {
                MoveCount++;

                if (MoveCount == ThrowOnMoveNumber)
                {
                    throw new IOException("injected move failure");
                }

                _inner.MoveFileNoOverwrite(sourcePath, destinationPath);
            }

            public void DeleteFile(string path)
            {
                _inner.DeleteFile(path);
            }
        }
    }
}
