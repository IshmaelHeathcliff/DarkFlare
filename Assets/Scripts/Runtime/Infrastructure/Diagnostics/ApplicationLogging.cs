using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public enum ApplicationLogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal,
    }

    public readonly struct LogEventId : IEquatable<LogEventId>
    {
        public LogEventId(string value)
        {
            if (!IsValid(value))
            {
                throw new ArgumentException("日志事件 ID 必须是小写点分层级", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(LogEventId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LogEventId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Length > 96
                || value[0] == '.'
                || value[value.Length - 1] == '.')
            {
                return false;
            }

            bool previousDot = false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool valid = character >= 'a' && character <= 'z'
                    || character >= '0' && character <= '9'
                    || character == '-'
                    || character == '.';

                if (!valid || character == '.' && previousDot)
                {
                    return false;
                }

                previousDot = character == '.';
            }

            return true;
        }
    }

    public readonly struct ApplicationLogContext
    {
        public ApplicationLogContext(
            string scope = null,
            string operation = null,
            string phase = null,
            string session = null,
            string slot = null,
            string contentId = null,
            string instanceId = null)
        {
            Scope = Sanitize(scope);
            Operation = Sanitize(operation);
            Phase = Sanitize(phase);
            Session = Sanitize(session);
            Slot = Sanitize(slot);
            ContentId = Sanitize(contentId);
            InstanceId = Sanitize(instanceId);
        }

        public string Scope { get; }

        public string Operation { get; }

        public string Phase { get; }

        public string Session { get; }

        public string Slot { get; }

        public string ContentId { get; }

        public string InstanceId { get; }

        static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= 128 ? normalized : normalized.Substring(0, 128);
        }
    }

    public sealed class ApplicationLogEntry
    {
        public ApplicationLogEntry(
            long sequence,
            DateTimeOffset timestampUtc,
            ApplicationLogLevel level,
            LogEventId eventId,
            string message,
            Exception exception,
            UnityEngine.Object unityContext,
            ApplicationLogContext context,
            bool replayed = false)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            Level = level;
            EventId = eventId;
            Message = message ?? string.Empty;
            Exception = exception;
            UnityContext = unityContext;
            Context = context;
            IsReplayed = replayed;
        }

        public long Sequence { get; }

        public DateTimeOffset TimestampUtc { get; }

        public ApplicationLogLevel Level { get; }

        public LogEventId EventId { get; }

        public string Category
        {
            get
            {
                string value = EventId.Value;
                int separator = value != null ? value.IndexOf('.') : -1;
                return separator > 0 ? value.Substring(0, separator) : value ?? string.Empty;
            }
        }

        public string Message { get; }

        public Exception Exception { get; }

        public UnityEngine.Object UnityContext { get; }

        public ApplicationLogContext Context { get; }

        public bool IsReplayed { get; }
    }

    public interface IApplicationLogSink
    {
        void Write(ApplicationLogEntry entry);
    }

    public sealed class RingBufferLogSink : IApplicationLogSink
    {
        readonly object _gate = new object();
        readonly ApplicationLogEntry[] _entries;

        int _count;
        int _next;

        public RingBufferLogSink(int capacity = 256)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _entries = new ApplicationLogEntry[capacity];
        }

        public int Capacity => _entries.Length;

        public void Write(ApplicationLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (_gate)
            {
                _entries[_next] = entry;
                _next = (_next + 1) % _entries.Length;
                _count = Math.Min(_count + 1, _entries.Length);
            }
        }

        public IReadOnlyList<ApplicationLogEntry> Snapshot()
        {
            lock (_gate)
            {
                List<ApplicationLogEntry> snapshot = new List<ApplicationLogEntry>(_count);
                int start = (_next - _count + _entries.Length) % _entries.Length;

                for (int i = 0; i < _count; i++)
                {
                    snapshot.Add(_entries[(start + i) % _entries.Length]);
                }

                return snapshot;
            }
        }
    }

    public sealed class MinimumLevelLogSink : IApplicationLogSink
    {
        readonly IApplicationLogSink _inner;
        readonly ApplicationLogLevel _minimumLevel;

        public MinimumLevelLogSink(
            IApplicationLogSink inner,
            ApplicationLogLevel minimumLevel)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _minimumLevel = minimumLevel;
        }

        public void Write(ApplicationLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if ((int)entry.Level >= (int)_minimumLevel)
            {
                _inner.Write(entry);
            }
        }
    }

    public sealed class UnityConsoleLogSink : IApplicationLogSink
    {
        [ThreadStatic]
        static int s_writeDepth;

        public static bool IsWriting => s_writeDepth > 0;

        public void Write(ApplicationLogEntry entry)
        {
            if (entry == null || entry.IsReplayed)
            {
                return;
            }

            string message = BuildMessage(entry);
            s_writeDepth++;

            try
            {
                switch (entry.Level)
                {
                    case ApplicationLogLevel.Debug:
                    case ApplicationLogLevel.Info:
                        UnityEngine.Debug.Log(message, entry.UnityContext);
                        break;
                    case ApplicationLogLevel.Warning:
                        UnityEngine.Debug.LogWarning(message, entry.UnityContext);
                        break;
                    case ApplicationLogLevel.Error:
                    case ApplicationLogLevel.Fatal:
                        UnityEngine.Debug.LogError(message, entry.UnityContext);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            finally
            {
                s_writeDepth--;
            }
        }

        static string BuildMessage(ApplicationLogEntry entry)
        {
            string message = $"[{entry.EventId}] {entry.Message}";

            if (entry.Exception != null)
            {
                message += "\n" + entry.Exception;
            }

            return message;
        }
    }

    public sealed class ApplicationLogger
    {
        readonly object _gate = new object();
        readonly List<IApplicationLogSink> _sinks;
        readonly HashSet<IApplicationLogSink> _disabledSinks =
            new HashSet<IApplicationLogSink>();
        readonly ApplicationLogLevel _minimumLevel;

        long _sequence;

        public ApplicationLogger(
            IEnumerable<IApplicationLogSink> sinks,
            ApplicationLogLevel minimumLevel = ApplicationLogLevel.Debug)
        {
            if (sinks == null)
            {
                throw new ArgumentNullException(nameof(sinks));
            }

            _sinks = new List<IApplicationLogSink>();

            foreach (IApplicationLogSink sink in sinks)
            {
                if (sink != null && !_sinks.Contains(sink))
                {
                    _sinks.Add(sink);
                }
            }

            if (_sinks.Count == 0)
            {
                throw new ArgumentException("Logger 至少需要一个 Sink", nameof(sinks));
            }

            _minimumLevel = minimumLevel;
        }

        public void Write(
            ApplicationLogLevel level,
            LogEventId eventId,
            string message,
            Exception exception = null,
            UnityEngine.Object unityContext = null,
            ApplicationLogContext context = default)
        {
            if (level < _minimumLevel)
            {
                return;
            }

            if (!LogEventId.IsValid(eventId.Value))
            {
                throw new ArgumentException("日志事件 ID 无效", nameof(eventId));
            }

            ApplicationLogEntry entry = new ApplicationLogEntry(
                Interlocked.Increment(ref _sequence),
                DateTimeOffset.UtcNow,
                level,
                eventId,
                message,
                exception,
                unityContext,
                context);
            WriteToSinks(entry);
        }

        public void Import(IReadOnlyList<ApplicationLogEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ApplicationLogEntry source = entries[i];

                if (source == null)
                {
                    continue;
                }

                ApplicationLogEntry replayed = new ApplicationLogEntry(
                    Interlocked.Increment(ref _sequence),
                    source.TimestampUtc,
                    source.Level,
                    source.EventId,
                    source.Message,
                    source.Exception,
                    source.UnityContext,
                    source.Context,
                    true);
                WriteToSinks(replayed);
            }
        }

        void WriteToSinks(ApplicationLogEntry entry)
        {
            IApplicationLogSink[] sinks;

            lock (_gate)
            {
                sinks = _sinks.ToArray();
            }

            for (int i = 0; i < sinks.Length; i++)
            {
                IApplicationLogSink sink = sinks[i];

                lock (_gate)
                {
                    if (_disabledSinks.Contains(sink))
                    {
                        continue;
                    }
                }

                try
                {
                    sink.Write(entry);
                }
                catch (Exception)
                {
                    lock (_gate)
                    {
                        _disabledSinks.Add(sink);
                    }
                }
            }
        }
    }

    public static class LogEventIds
    {
        public static readonly LogEventId DataValidation = new LogEventId("content.validation.message");
        public static readonly LogEventId GameplayBootstrap = new LogEventId("gameplay.bootstrap.message");
        public static readonly LogEventId GameplayActor = new LogEventId("gameplay.actor.message");
        public static readonly LogEventId GameplayCrafting = new LogEventId("gameplay.crafting.message");
        public static readonly LogEventId GameplayInteraction = new LogEventId("gameplay.interaction.message");
        public static readonly LogEventId GameplayTrading = new LogEventId("gameplay.trading.message");
        public static readonly LogEventId GameplayUi = new LogEventId("gameplay.ui.message");
        public static readonly LogEventId GameplayCombat = new LogEventId("gameplay.combat.message");
        public static readonly LogEventId GameplayVisual = new LogEventId("gameplay.visual.message");
        public static readonly LogEventId InfrastructureUi = new LogEventId("infrastructure.ui.message");
        public static readonly LogEventId InfrastructureInput = new LogEventId("infrastructure.input.message");
        public static readonly LogEventId InfrastructureSettings = new LogEventId("infrastructure.settings.message");
        public static readonly LogEventId InfrastructureLifecycle = new LogEventId("infrastructure.lifecycle.message");
        public static readonly LogEventId ResourcePrefab = new LogEventId("resource.prefab.message");
        public static readonly LogEventId ResourceSprite = new LogEventId("resource.sprite.message");
        public static readonly LogEventId ResourceAudio = new LogEventId("resource.audio.message");
        public static readonly LogEventId ApplicationBootFailed = new LogEventId("lifecycle.application.boot-failed");
        public static readonly LogEventId LifecycleTaskFailed = new LogEventId("lifecycle.task.failed");
        public static readonly LogEventId UnityExternalError = new LogEventId("diagnostics.unity.external-error");
        public static readonly LogEventId UnityUnhandledException = new LogEventId("diagnostics.unity.unhandled-exception");
        public static readonly LogEventId AppDomainUnhandledException = new LogEventId("diagnostics.app-domain.unhandled-exception");
        public static readonly LogEventId UniTaskUnobservedException = new LogEventId("diagnostics.unitask.unobserved-exception");
        public static readonly LogEventId ResourceLoadFailed = new LogEventId("resource.asset.load-failed");
        public static readonly LogEventId ResourceOwnerReleased = new LogEventId("resource.owner.released");

        public static IReadOnlyList<LogEventId> All { get; } = new[]
        {
            DataValidation,
            GameplayBootstrap,
            GameplayActor,
            GameplayCrafting,
            GameplayInteraction,
            GameplayTrading,
            GameplayUi,
            GameplayCombat,
            GameplayVisual,
            InfrastructureUi,
            InfrastructureInput,
            InfrastructureSettings,
            InfrastructureLifecycle,
            ResourcePrefab,
            ResourceSprite,
            ResourceAudio,
            ApplicationBootFailed,
            LifecycleTaskFailed,
            UnityExternalError,
            UnityUnhandledException,
            AppDomainUnhandledException,
            UniTaskUnobservedException,
            ResourceLoadFailed,
            ResourceOwnerReleased,
        };
    }

    public static class ApplicationLog
    {
        static readonly object Gate = new object();

        static ApplicationLogger s_logger;
        static RingBufferLogSink s_earlyBuffer;
        static int s_installGeneration;

        public static ApplicationLogger Current
        {
            get
            {
                lock (Gate)
                {
                    EnsureEarlyLogger();
                    return s_logger;
                }
            }
        }

        public static IDisposable Install(ApplicationLogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            IReadOnlyList<ApplicationLogEntry> earlyEntries;
            int generation;

            lock (Gate)
            {
                EnsureEarlyLogger();
                earlyEntries = s_earlyBuffer.Snapshot();
                s_logger = logger;
                generation = ++s_installGeneration;
            }

            logger.Import(earlyEntries);
            return new Installation(generation, logger);
        }

        public static void Reset()
        {
            lock (Gate)
            {
                s_installGeneration++;
                s_logger = null;
                s_earlyBuffer = null;
                EnsureEarlyLogger();
            }
        }

        public static void Debug(
            LogEventId eventId,
            object message,
            UnityEngine.Object context = null,
            ApplicationLogContext logContext = default)
        {
            Write(ApplicationLogLevel.Debug, eventId, message, null, context, logContext);
        }

        public static void Info(
            LogEventId eventId,
            object message,
            UnityEngine.Object context = null,
            ApplicationLogContext logContext = default)
        {
            Write(ApplicationLogLevel.Info, eventId, message, null, context, logContext);
        }

        public static void Warning(
            LogEventId eventId,
            object message,
            UnityEngine.Object context = null,
            ApplicationLogContext logContext = default)
        {
            Write(ApplicationLogLevel.Warning, eventId, message, null, context, logContext);
        }

        public static void Error(
            LogEventId eventId,
            object message,
            UnityEngine.Object context = null,
            ApplicationLogContext logContext = default)
        {
            Write(ApplicationLogLevel.Error, eventId, message, null, context, logContext);
        }

        public static void Exception(
            LogEventId eventId,
            Exception exception,
            UnityEngine.Object context = null,
            string message = null,
            ApplicationLogContext logContext = default)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Write(
                ApplicationLogLevel.Error,
                eventId,
                message ?? exception.Message,
                exception,
                context,
                logContext);
        }

        public static void Fatal(
            LogEventId eventId,
            Exception exception,
            string message,
            UnityEngine.Object context = null,
            ApplicationLogContext logContext = default)
        {
            Write(ApplicationLogLevel.Fatal, eventId, message, exception, context, logContext);
        }

        static void Write(
            ApplicationLogLevel level,
            LogEventId eventId,
            object message,
            Exception exception,
            UnityEngine.Object context,
            ApplicationLogContext logContext)
        {
            Current.Write(
                level,
                eventId,
                message?.ToString() ?? string.Empty,
                exception,
                context,
                logContext);
        }

        static void EnsureEarlyLogger()
        {
            if (s_logger != null)
            {
                return;
            }

            s_earlyBuffer = new RingBufferLogSink(64);
            s_logger = new ApplicationLogger(
                new IApplicationLogSink[]
                {
                    new UnityConsoleLogSink(),
                    s_earlyBuffer,
                });
        }

        sealed class Installation : IDisposable
        {
            readonly int _generation;
            readonly ApplicationLogger _logger;

            bool _disposed;

            public Installation(int generation, ApplicationLogger logger)
            {
                _generation = generation;
                _logger = logger;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                lock (Gate)
                {
                    if (_generation != s_installGeneration
                        || !ReferenceEquals(s_logger, _logger))
                    {
                        return;
                    }

                    s_installGeneration++;
                    s_logger = null;
                    s_earlyBuffer = null;
                    EnsureEarlyLogger();
                }
            }
        }
    }

    public sealed class ApplicationFatalFailure
    {
        public ApplicationFatalFailure(LogEventId eventId, Exception exception, bool processTerminating)
        {
            EventId = eventId;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            ProcessTerminating = processTerminating;
        }

        public LogEventId EventId { get; }

        public Exception Exception { get; }

        public bool ProcessTerminating { get; }
    }

    public sealed class ApplicationFailureCoordinator
    {
        readonly object _gate = new object();

        ApplicationFatalFailure _firstFailure;
        int _duplicateCount;

        public ApplicationFatalFailure FirstFailure
        {
            get
            {
                lock (_gate)
                {
                    return _firstFailure;
                }
            }
        }

        public int DuplicateCount
        {
            get
            {
                lock (_gate)
                {
                    return _duplicateCount;
                }
            }
        }

        public event Action<ApplicationFatalFailure> FatalReported;

        public bool TryReport(ApplicationFatalFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            lock (_gate)
            {
                if (_firstFailure != null)
                {
                    _duplicateCount++;
                    return false;
                }

                _firstFailure = failure;
            }

            FatalReported?.Invoke(failure);
            return true;
        }
    }

    public sealed class ApplicationExceptionMonitor : IDisposable
    {
        readonly ApplicationFailureCoordinator _coordinator;
        readonly SynchronizationContext _mainThreadContext;

        bool _disposed;
        int _reporting;

        public ApplicationExceptionMonitor(ApplicationFailureCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _mainThreadContext = SynchronizationContext.Current;
            Application.logMessageReceivedThreaded += OnUnityLog;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            UniTaskScheduler.UnobservedTaskException += OnUnobservedUniTaskException;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Application.logMessageReceivedThreaded -= OnUnityLog;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            UniTaskScheduler.UnobservedTaskException -= OnUnobservedUniTaskException;
        }

        void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (_disposed || UnityConsoleLogSink.IsWriting)
            {
                return;
            }

            if (type == LogType.Error)
            {
                ApplicationLog.Error(
                    LogEventIds.UnityExternalError,
                    JoinConditionAndStack(condition, stackTrace));
                return;
            }

            if (type != LogType.Assert && type != LogType.Exception)
            {
                return;
            }

            ReportFatal(
                LogEventIds.UnityUnhandledException,
                new InvalidOperationException(JoinConditionAndStack(condition, stackTrace)),
                false);
        }

        void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            Exception exception = args.ExceptionObject as Exception
                ?? new InvalidOperationException(
                    args.ExceptionObject?.ToString() ?? "AppDomain 未处理异常");
            ReportFatal(
                LogEventIds.AppDomainUnhandledException,
                exception,
                args.IsTerminating);
        }

        void OnUnobservedUniTaskException(Exception exception)
        {
            if (_disposed || exception is OperationCanceledException)
            {
                return;
            }

            ReportFatal(LogEventIds.UniTaskUnobservedException, exception, false);
        }

        void ReportFatal(LogEventId eventId, Exception exception, bool processTerminating)
        {
            if (Interlocked.Exchange(ref _reporting, 1) != 0)
            {
                return;
            }

            try
            {
                ApplicationLog.Fatal(eventId, exception, exception.Message);
                ApplicationFatalFailure failure = new ApplicationFatalFailure(
                    eventId,
                    exception,
                    processTerminating);

                if (_mainThreadContext != null
                    && SynchronizationContext.Current != _mainThreadContext)
                {
                    _mainThreadContext.Post(_ => _coordinator.TryReport(failure), null);
                }
                else
                {
                    _coordinator.TryReport(failure);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _reporting, 0);
            }
        }

        static string JoinConditionAndStack(string condition, string stackTrace)
        {
            return string.IsNullOrWhiteSpace(stackTrace)
                ? condition ?? string.Empty
                : $"{condition}\n{stackTrace}";
        }
    }
}
