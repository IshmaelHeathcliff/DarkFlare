using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;

namespace DarkFlare
{
    public enum LocalSettingsStorageCode
    {
        Success,
        NotFound,
        Busy,
        Cancelled,
        SerializationFailed,
        VerificationFailed,
        NoValidFile,
        IoFailure,
    }

    public enum SettingsRecoverySource
    {
        Current,
        Backup,
        DefaultMissing,
        DefaultInvalid,
    }

    public sealed class LocalSettingsCommitResult
    {
        public LocalSettingsStorageCode Code { get; }

        public UserSettingsDocumentDto Document { get; }

        public SettingsSerializationCode SerializationCode { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == LocalSettingsStorageCode.Success && Document != null;

        internal LocalSettingsCommitResult(
            LocalSettingsStorageCode code,
            UserSettingsDocumentDto document,
            SettingsSerializationCode serializationCode,
            Exception exception)
        {
            Code = code;
            Document = document;
            SerializationCode = serializationCode;
            Exception = exception;
        }
    }

    public sealed class LocalSettingsLoadResult
    {
        public LocalSettingsStorageCode Code { get; }

        public UserSettingsDocumentDto Document { get; }

        public SettingsRecoverySource RecoverySource { get; }

        public SettingsSerializationCode SerializationCode { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == LocalSettingsStorageCode.Success && Document != null;

        internal LocalSettingsLoadResult(
            LocalSettingsStorageCode code,
            UserSettingsDocumentDto document,
            SettingsRecoverySource recoverySource,
            SettingsSerializationCode serializationCode,
            Exception exception)
        {
            Code = code;
            Document = document;
            RecoverySource = recoverySource;
            SerializationCode = serializationCode;
            Exception = exception;
        }
    }

    public interface ILocalSettingsStorage
    {
        LocalSettingsCommitResult Commit(
            UserSettingsDocumentDto document,
            CancellationToken cancellationToken = default);

        LocalSettingsLoadResult Load(CancellationToken cancellationToken = default);
    }

    public sealed class LocalSettingsStorage : ILocalSettingsStorage
    {
        const string CurrentFileName = "settings.json";
        const string BackupFileName = "settings.bak";
        const string CorruptFileName = "settings.corrupt";
        const string TemporaryExtension = ".tmp";

        static long s_temporarySequence;

        readonly string _rootPath;
        readonly ISettingsSerializer _serializer;
        readonly ILocalFileOperations _files;
        int _operationInProgress;

        public LocalSettingsStorage(
            ISettingsPathProvider pathProvider,
            ISettingsSerializer serializer,
            ILocalFileOperations files = null)
        {
            if (pathProvider == null)
            {
                throw new ArgumentNullException(nameof(pathProvider));
            }

            if (string.IsNullOrWhiteSpace(pathProvider.RootPath))
            {
                throw new ArgumentException("设置根目录为空", nameof(pathProvider));
            }

            _rootPath = Path.GetFullPath(pathProvider.RootPath);
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _files = files ?? new SystemLocalSaveFileOperations();
        }

        public LocalSettingsCommitResult Commit(
            UserSettingsDocumentDto document,
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation())
            {
                return CommitFailure(LocalSettingsStorageCode.Busy);
            }

            string temporaryPath = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                SettingsSerializationResult serialized = _serializer.Serialize(document);

                if (!serialized.Succeeded)
                {
                    return CommitFailure(
                        LocalSettingsStorageCode.SerializationFailed,
                        serialized.Code,
                        serialized.Exception);
                }

                _files.CreateDirectory(_rootPath);
                DeleteTemporaryFiles();
                temporaryPath = CreateUniqueTemporaryPath();
                _files.WriteAllBytesDurable(temporaryPath, serialized.Bytes);
                cancellationToken.ThrowIfCancellationRequested();
                SettingsSerializationResult verified = Read(temporaryPath);

                if (!verified.Succeeded)
                {
                    return CommitFailure(
                        LocalSettingsStorageCode.VerificationFailed,
                        verified.Code,
                        verified.Exception);
                }

                string currentPath = GetPath(CurrentFileName);
                string backupPath = GetPath(BackupFileName);

                if (_files.FileExists(currentPath))
                {
                    if (_files.FileExists(backupPath))
                    {
                        _files.DeleteFile(backupPath);
                    }

                    _files.MoveFileNoOverwrite(currentPath, backupPath);
                }

                cancellationToken.ThrowIfCancellationRequested();
                _files.MoveFileNoOverwrite(temporaryPath, currentPath);
                temporaryPath = null;
                SettingsSerializationResult committed = Read(currentPath);

                if (!committed.Succeeded)
                {
                    return CommitFailure(
                        LocalSettingsStorageCode.VerificationFailed,
                        committed.Code,
                        committed.Exception);
                }

                return new LocalSettingsCommitResult(
                    LocalSettingsStorageCode.Success,
                    committed.Document,
                    SettingsSerializationCode.Success,
                    null);
            }
            catch (OperationCanceledException)
            {
                return CommitFailure(LocalSettingsStorageCode.Cancelled);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return CommitFailure(LocalSettingsStorageCode.IoFailure, exception: exception);
            }
            finally
            {
                TryDelete(temporaryPath);
                EndOperation();
            }
        }

        public LocalSettingsLoadResult Load(CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation())
            {
                return LoadFailure(LocalSettingsStorageCode.Busy);
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_files.DirectoryExists(_rootPath))
                {
                    return LoadFailure(
                        LocalSettingsStorageCode.NotFound,
                        SettingsRecoverySource.DefaultMissing);
                }

                DeleteTemporaryFiles();
                string currentPath = GetPath(CurrentFileName);
                string backupPath = GetPath(BackupFileName);
                bool hasCurrent = _files.FileExists(currentPath);
                bool hasBackup = _files.FileExists(backupPath);

                if (!hasCurrent && !hasBackup)
                {
                    return LoadFailure(
                        LocalSettingsStorageCode.NotFound,
                        SettingsRecoverySource.DefaultMissing);
                }

                SettingsSerializationResult current = hasCurrent ? Read(currentPath) : null;

                if (current != null && current.Succeeded)
                {
                    return LoadSuccess(current.Document, SettingsRecoverySource.Current);
                }

                cancellationToken.ThrowIfCancellationRequested();
                SettingsSerializationResult backup = hasBackup ? Read(backupPath) : null;

                if (backup != null && backup.Succeeded)
                {
                    PreserveCorruptFile(currentPath);
                    return LoadSuccess(backup.Document, SettingsRecoverySource.Backup);
                }

                if (hasCurrent)
                {
                    PreserveCorruptFile(currentPath);
                }
                else if (hasBackup)
                {
                    PreserveCorruptFile(backupPath);
                }

                SettingsSerializationResult failure = current ?? backup;
                return LoadFailure(
                    LocalSettingsStorageCode.NoValidFile,
                    SettingsRecoverySource.DefaultInvalid,
                    failure?.Code ?? SettingsSerializationCode.InvalidJson,
                    failure?.Exception);
            }
            catch (OperationCanceledException)
            {
                return LoadFailure(LocalSettingsStorageCode.Cancelled);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return LoadFailure(
                    LocalSettingsStorageCode.IoFailure,
                    SettingsRecoverySource.DefaultInvalid,
                    exception: exception);
            }
            finally
            {
                EndOperation();
            }
        }

        SettingsSerializationResult Read(string path)
        {
            long length = _files.GetFileLength(path);

            if (length < 0 || length > LocalSettingsFormat.MaximumDocumentBytes)
            {
                return new SettingsSerializationResult(
                    SettingsSerializationCode.DocumentTooLarge,
                    null,
                    null,
                    Array.Empty<SettingsDataIssue>(),
                    null,
                    false);
            }

            return _serializer.Deserialize(_files.ReadAllBytes(path));
        }

        void DeleteTemporaryFiles()
        {
            IReadOnlyList<string> paths = _files.EnumerateFiles(_rootPath);

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(
                    Path.GetExtension(paths[i]),
                    TemporaryExtension,
                    StringComparison.OrdinalIgnoreCase))
                {
                    _files.DeleteFile(paths[i]);
                }
            }
        }

        string CreateUniqueTemporaryPath()
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                long unique = Interlocked.Increment(ref s_temporarySequence);
                string path = GetPath(unique.ToString("x16") + TemporaryExtension);

                if (!_files.FileExists(path))
                {
                    return path;
                }
            }

            throw new IOException("无法创建唯一设置临时文件名");
        }

        void PreserveCorruptFile(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !_files.FileExists(sourcePath))
            {
                return;
            }

            string corruptPath = GetPath(CorruptFileName);

            if (_files.FileExists(corruptPath))
            {
                _files.DeleteFile(corruptPath);
            }

            _files.MoveFileNoOverwrite(sourcePath, corruptPath);
        }

        string GetPath(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(_rootPath, fileName));
            string expectedPrefix = _rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("设置路径越出受控根目录");
            }

            return path;
        }

        void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (_files.FileExists(path))
                {
                    _files.DeleteFile(path);
                }
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
            }
        }

        bool TryBeginOperation()
        {
            return Interlocked.CompareExchange(ref _operationInProgress, 1, 0) == 0;
        }

        void EndOperation()
        {
            Volatile.Write(ref _operationInProgress, 0);
        }

        static bool IsStorageException(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is SecurityException
                || exception is NotSupportedException;
        }

        static LocalSettingsCommitResult CommitFailure(
            LocalSettingsStorageCode code,
            SettingsSerializationCode serializationCode = SettingsSerializationCode.Success,
            Exception exception = null)
        {
            return new LocalSettingsCommitResult(code, null, serializationCode, exception);
        }

        static LocalSettingsLoadResult LoadSuccess(
            UserSettingsDocumentDto document,
            SettingsRecoverySource source)
        {
            return new LocalSettingsLoadResult(
                LocalSettingsStorageCode.Success,
                document,
                source,
                SettingsSerializationCode.Success,
                null);
        }

        static LocalSettingsLoadResult LoadFailure(
            LocalSettingsStorageCode code,
            SettingsRecoverySource source = SettingsRecoverySource.DefaultInvalid,
            SettingsSerializationCode serializationCode = SettingsSerializationCode.Success,
            Exception exception = null)
        {
            return new LocalSettingsLoadResult(
                code,
                null,
                source,
                serializationCode,
                exception);
        }
    }
}
