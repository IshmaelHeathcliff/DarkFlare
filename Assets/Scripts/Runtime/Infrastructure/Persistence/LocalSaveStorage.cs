using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;

namespace DarkFlare
{
    public enum LocalSaveStorageCode
    {
        Success,
        Busy,
        Cancelled,
        SlotNotFound,
        NoValidGeneration,
        CommitConflict,
        SerializationFailed,
        VerificationFailed,
        IoFailure,
    }

    public sealed class SaveGenerationFailure
    {
        public long CommitSequence { get; }

        public SaveSerializationCode SerializationCode { get; }

        public Exception Exception { get; }

        internal SaveGenerationFailure(
            long commitSequence,
            SaveSerializationCode serializationCode,
            Exception exception)
        {
            CommitSequence = commitSequence;
            SerializationCode = serializationCode;
            Exception = exception;
        }
    }

    public sealed class SaveGenerationMetadata
    {
        public SaveSlotId SlotId { get; }

        public long CommitSequence { get; }

        public string UpdatedUtc { get; }

        public SaveSummaryDto Summary { get; }

        public bool WasMigrated { get; }

        internal SaveGenerationMetadata(SaveDocumentDto document, bool wasMigrated)
        {
            SlotId = SaveSlotId.Parse(document.Header.SlotId);
            CommitSequence = document.Header.CommitSequence;
            UpdatedUtc = document.Header.UpdatedUtc;
            Summary = document.Header.Summary;
            WasMigrated = wasMigrated;
        }
    }

    public sealed class SaveSlotMetadata
    {
        public SaveGenerationMetadata Current { get; }

        public SaveGenerationMetadata Backup { get; }

        public IReadOnlyList<SaveGenerationFailure> InvalidGenerations { get; }

        internal SaveSlotMetadata(
            SaveGenerationMetadata current,
            SaveGenerationMetadata backup,
            IReadOnlyList<SaveGenerationFailure> invalidGenerations)
        {
            Current = current;
            Backup = backup;
            InvalidGenerations = invalidGenerations ?? Array.Empty<SaveGenerationFailure>();
        }
    }

    public sealed class LocalSaveCommitResult
    {
        public LocalSaveStorageCode Code { get; }

        public SaveDocumentDto Document { get; }

        public SaveSerializationCode SerializationCode { get; }

        public IReadOnlyList<SaveDataIssue> ValidationIssues { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == LocalSaveStorageCode.Success && Document != null;

        internal LocalSaveCommitResult(
            LocalSaveStorageCode code,
            SaveDocumentDto document,
            SaveSerializationCode serializationCode,
            IReadOnlyList<SaveDataIssue> validationIssues,
            Exception exception)
        {
            Code = code;
            Document = document;
            SerializationCode = serializationCode;
            ValidationIssues = validationIssues ?? Array.Empty<SaveDataIssue>();
            Exception = exception;
        }
    }

    public sealed class LocalSaveLoadResult
    {
        public LocalSaveStorageCode Code { get; }

        public SaveDocumentDto Document { get; }

        public SaveSlotMetadata Metadata { get; }

        public bool RecoveredFromBackup { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == LocalSaveStorageCode.Success && Document != null;

        internal LocalSaveLoadResult(
            LocalSaveStorageCode code,
            SaveDocumentDto document,
            SaveSlotMetadata metadata,
            bool recoveredFromBackup,
            Exception exception)
        {
            Code = code;
            Document = document;
            Metadata = metadata;
            RecoveredFromBackup = recoveredFromBackup;
            Exception = exception;
        }
    }

    public interface ILocalSaveStorage
    {
        LocalSaveCommitResult Commit(
            SaveSlotId slotId,
            SaveDocumentDto document,
            CancellationToken cancellationToken = default);

        LocalSaveLoadResult LoadLatest(
            SaveSlotId slotId,
            CancellationToken cancellationToken = default);
    }

    public interface ILocalSaveFileOperations : ILocalFileOperations
    {
    }

    public sealed class SystemLocalSaveFileOperations : ILocalSaveFileOperations
    {
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public IReadOnlyList<string> EnumerateFiles(string path)
        {
            return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public long GetFileLength(string path)
        {
            return new FileInfo(path).Length;
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public void WriteAllBytesDurable(string path, byte[] bytes)
        {
            using (FileStream stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void MoveFileNoOverwrite(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    public sealed class LocalSaveStorage : ILocalSaveStorage
    {
        const string SaveExtension = ".save";
        const string TemporaryExtension = ".tmp";

        static long s_temporarySequence;

        readonly string _rootPath;
        readonly ISaveSerializer _serializer;
        readonly ILocalSaveFileOperations _files;
        int _operationInProgress;

        public LocalSaveStorage(
            ISavePathProvider pathProvider,
            ISaveSerializer serializer,
            ILocalSaveFileOperations files = null)
        {
            if (pathProvider == null)
            {
                throw new ArgumentNullException(nameof(pathProvider));
            }

            if (string.IsNullOrWhiteSpace(pathProvider.RootPath))
            {
                throw new ArgumentException("存档根目录为空", nameof(pathProvider));
            }

            _rootPath = Path.GetFullPath(pathProvider.RootPath);
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _files = files ?? new SystemLocalSaveFileOperations();
        }

        public LocalSaveCommitResult Commit(
            SaveSlotId slotId,
            SaveDocumentDto document,
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation())
            {
                return CommitFailure(LocalSaveStorageCode.Busy);
            }

            string temporaryPath = null;

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return CommitFailure(LocalSaveStorageCode.Cancelled);
                }

                SaveSerializationResult serialized = _serializer.Serialize(document);

                if (!serialized.Succeeded)
                {
                    return new LocalSaveCommitResult(
                        LocalSaveStorageCode.SerializationFailed,
                        null,
                        serialized.Code,
                        serialized.ValidationIssues,
                        serialized.Exception);
                }

                if (SaveSlotId.Parse(serialized.Document.Header.SlotId) != slotId)
                {
                    return CommitFailure(
                        LocalSaveStorageCode.SerializationFailed,
                        SaveSerializationCode.SlotMismatch);
                }

                long commitSequence = serialized.Document.Header.CommitSequence;
                string slotPath = GetSlotPath(slotId);
                _files.CreateDirectory(slotPath);
                DeleteTemporaryFiles(slotPath);
                IReadOnlyList<GenerationPath> existing = EnumerateGenerationPaths(slotPath);

                if (existing.Count > 0
                    && commitSequence <= existing.Max(generation => generation.CommitSequence))
                {
                    return CommitFailure(LocalSaveStorageCode.CommitConflict);
                }

                string targetPath = GetGenerationPath(slotPath, commitSequence);

                if (_files.FileExists(targetPath))
                {
                    return CommitFailure(LocalSaveStorageCode.CommitConflict);
                }

                cancellationToken.ThrowIfCancellationRequested();
                temporaryPath = GetUniqueTemporaryPath(slotPath, commitSequence);
                _files.WriteAllBytesDurable(temporaryPath, serialized.Bytes);
                cancellationToken.ThrowIfCancellationRequested();
                SaveDeserializationResult verification = ReadAndDeserialize(
                    temporaryPath,
                    slotId,
                    commitSequence);

                if (!verification.Succeeded)
                {
                    return CommitFailure(
                        LocalSaveStorageCode.VerificationFailed,
                        verification.Code,
                        verification.Exception);
                }

                cancellationToken.ThrowIfCancellationRequested();
                _files.MoveFileNoOverwrite(temporaryPath, targetPath);
                temporaryPath = null;
                GenerationScan scan = ScanGenerations(slotPath, slotId, CancellationToken.None);
                ValidGeneration committed = scan.Valid.FirstOrDefault(
                    generation => generation.CommitSequence == commitSequence);

                if (committed == null)
                {
                    return CommitFailure(LocalSaveStorageCode.VerificationFailed);
                }

                Exception cleanupException = CleanupOldGenerations(scan);
                return new LocalSaveCommitResult(
                    LocalSaveStorageCode.Success,
                    committed.Document,
                    SaveSerializationCode.Success,
                    Array.Empty<SaveDataIssue>(),
                    cleanupException);
            }
            catch (OperationCanceledException)
            {
                return CommitFailure(LocalSaveStorageCode.Cancelled);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return CommitFailure(LocalSaveStorageCode.IoFailure, exception: exception);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
                EndOperation();
            }
        }

        public LocalSaveLoadResult LoadLatest(
            SaveSlotId slotId,
            CancellationToken cancellationToken = default)
        {
            if (!TryBeginOperation())
            {
                return LoadFailure(LocalSaveStorageCode.Busy);
            }

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return LoadFailure(LocalSaveStorageCode.Cancelled);
                }

                string slotPath = GetSlotPath(slotId);

                if (!_files.DirectoryExists(slotPath))
                {
                    return LoadFailure(LocalSaveStorageCode.SlotNotFound);
                }

                DeleteTemporaryFiles(slotPath);
                IReadOnlyList<GenerationPath> generationPaths = EnumerateGenerationPaths(slotPath);

                if (generationPaths.Count == 0)
                {
                    return LoadFailure(LocalSaveStorageCode.SlotNotFound);
                }

                GenerationScan scan = ScanGenerations(slotPath, slotId, cancellationToken);

                if (scan.Valid.Count == 0)
                {
                    return new LocalSaveLoadResult(
                        LocalSaveStorageCode.NoValidGeneration,
                        null,
                        new SaveSlotMetadata(null, null, scan.Invalid),
                        false,
                        scan.LastException);
                }

                ValidGeneration current = scan.Valid[0];
                ValidGeneration backup = scan.Valid.Count > 1 ? scan.Valid[1] : null;
                bool recoveredFromBackup = scan.Invalid.Any(
                    failure => failure.CommitSequence > current.CommitSequence);
                SaveSlotMetadata metadata = new SaveSlotMetadata(
                    current.Metadata,
                    backup?.Metadata,
                    scan.Invalid);
                return new LocalSaveLoadResult(
                    LocalSaveStorageCode.Success,
                    current.Document,
                    metadata,
                    recoveredFromBackup,
                    null);
            }
            catch (OperationCanceledException)
            {
                return LoadFailure(LocalSaveStorageCode.Cancelled);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return LoadFailure(LocalSaveStorageCode.IoFailure, exception);
            }
            finally
            {
                EndOperation();
            }
        }

        GenerationScan ScanGenerations(
            string slotPath,
            SaveSlotId slotId,
            CancellationToken cancellationToken)
        {
            GenerationScan scan = new GenerationScan();
            IReadOnlyList<GenerationPath> generations = EnumerateGenerationPaths(slotPath)
                .OrderByDescending(generation => generation.CommitSequence)
                .ToArray();

            for (int i = 0; i < generations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GenerationPath generation = generations[i];

                try
                {
                    SaveDeserializationResult result = ReadAndDeserialize(
                        generation.Path,
                        slotId,
                        generation.CommitSequence);

                    if (result.Succeeded)
                    {
                        scan.Valid.Add(new ValidGeneration(
                            generation.Path,
                            generation.CommitSequence,
                            result.Document,
                            result.WasMigrated));
                    }
                    else
                    {
                        scan.Invalid.Add(new SaveGenerationFailure(
                            generation.CommitSequence,
                            result.Code,
                            result.Exception));
                    }
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    scan.Invalid.Add(new SaveGenerationFailure(
                        generation.CommitSequence,
                        SaveSerializationCode.InvalidJson,
                        exception));
                    scan.LastException = exception;
                }
            }

            return scan;
        }

        SaveDeserializationResult ReadAndDeserialize(
            string path,
            SaveSlotId slotId,
            long commitSequence)
        {
            long fileLength = _files.GetFileLength(path);

            if (fileLength < 0 || fileLength > LocalSaveFormat.MaximumDocumentBytes)
            {
                return new SaveDeserializationResult(
                    SaveSerializationCode.DocumentTooLarge,
                    null,
                    Array.Empty<SaveDataIssue>(),
                    null,
                    false);
            }

            return _serializer.Deserialize(_files.ReadAllBytes(path), slotId, commitSequence);
        }

        Exception CleanupOldGenerations(GenerationScan scan)
        {
            Exception cleanupException = null;

            for (int i = 2; i < scan.Valid.Count; i++)
            {
                try
                {
                    _files.DeleteFile(scan.Valid[i].Path);
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    cleanupException = exception;
                }
            }

            if (scan.Invalid.Count <= 1)
            {
                return cleanupException;
            }

            HashSet<long> retained = new HashSet<long>
            {
                scan.Invalid.Max(failure => failure.CommitSequence),
            };

            for (int i = 0; i < scan.Invalid.Count; i++)
            {
                SaveGenerationFailure invalid = scan.Invalid[i];

                if (!retained.Contains(invalid.CommitSequence))
                {
                    try
                    {
                        _files.DeleteFile(GetGenerationPath(
                            GetSlotPath(SaveSlotId.Parse(scan.Valid[0].Document.Header.SlotId)),
                            invalid.CommitSequence));
                    }
                    catch (Exception exception) when (IsStorageException(exception))
                    {
                        cleanupException = exception;
                    }
                }
            }

            return cleanupException;
        }

        IReadOnlyList<GenerationPath> EnumerateGenerationPaths(string slotPath)
        {
            if (!_files.DirectoryExists(slotPath))
            {
                return Array.Empty<GenerationPath>();
            }

            List<GenerationPath> result = new List<GenerationPath>();
            IReadOnlyList<string> paths = _files.EnumerateFiles(slotPath);

            for (int i = 0; i < paths.Count; i++)
            {
                string fileName = Path.GetFileName(paths[i]);

                if (TryParseGenerationFileName(fileName, out long commitSequence))
                {
                    result.Add(new GenerationPath(paths[i], commitSequence));
                }
            }

            return result;
        }

        void DeleteTemporaryFiles(string slotPath)
        {
            if (!_files.DirectoryExists(slotPath))
            {
                return;
            }

            IReadOnlyList<string> paths = _files.EnumerateFiles(slotPath);

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

        string GetSlotPath(SaveSlotId slotId)
        {
            string slotPath = Path.GetFullPath(Path.Combine(_rootPath, slotId.Value));
            string expectedPrefix = _rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!slotPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("存档槽位路径越出受控根目录");
            }

            return slotPath;
        }

        static string GetGenerationPath(string slotPath, long commitSequence)
        {
            return Path.Combine(
                slotPath,
                commitSequence.ToString(CultureInfo.InvariantCulture) + SaveExtension);
        }

        string GetUniqueTemporaryPath(string slotPath, long commitSequence)
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                long unique = Interlocked.Increment(ref s_temporarySequence);
                string fileName = commitSequence.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + unique.ToString("x16", CultureInfo.InvariantCulture)
                    + TemporaryExtension;
                string path = Path.Combine(slotPath, fileName);

                if (!_files.FileExists(path))
                {
                    return path;
                }
            }

            throw new IOException("无法创建唯一存档临时文件名");
        }

        void TryDeleteTemporaryFile(string path)
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

        static bool TryParseGenerationFileName(string fileName, out long commitSequence)
        {
            commitSequence = 0;

            if (string.IsNullOrEmpty(fileName)
                || !fileName.EndsWith(SaveExtension, StringComparison.Ordinal)
                || fileName.Length <= SaveExtension.Length)
            {
                return false;
            }

            string sequence = fileName.Substring(0, fileName.Length - SaveExtension.Length);

            if (sequence.Length > 1 && sequence[0] == '0')
            {
                return false;
            }

            return long.TryParse(
                    sequence,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out commitSequence)
                && commitSequence > 0;
        }

        static bool IsStorageException(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is SecurityException
                || exception is NotSupportedException;
        }

        static LocalSaveCommitResult CommitFailure(
            LocalSaveStorageCode code,
            SaveSerializationCode serializationCode = SaveSerializationCode.Success,
            Exception exception = null)
        {
            return new LocalSaveCommitResult(
                code,
                null,
                serializationCode,
                Array.Empty<SaveDataIssue>(),
                exception);
        }

        static LocalSaveLoadResult LoadFailure(
            LocalSaveStorageCode code,
            Exception exception = null)
        {
            return new LocalSaveLoadResult(code, null, null, false, exception);
        }

        sealed class GenerationPath
        {
            public string Path { get; }

            public long CommitSequence { get; }

            public GenerationPath(string path, long commitSequence)
            {
                Path = path;
                CommitSequence = commitSequence;
            }
        }

        sealed class ValidGeneration
        {
            public string Path { get; }

            public long CommitSequence { get; }

            public SaveDocumentDto Document { get; }

            public SaveGenerationMetadata Metadata { get; }

            public ValidGeneration(
                string path,
                long commitSequence,
                SaveDocumentDto document,
                bool wasMigrated)
            {
                Path = path;
                CommitSequence = commitSequence;
                Document = document;
                Metadata = new SaveGenerationMetadata(document, wasMigrated);
            }
        }

        sealed class GenerationScan
        {
            public List<ValidGeneration> Valid { get; } = new List<ValidGeneration>();

            public List<SaveGenerationFailure> Invalid { get; } = new List<SaveGenerationFailure>();

            public Exception LastException { get; set; }
        }
    }
}
