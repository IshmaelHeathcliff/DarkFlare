using System;

namespace DarkFlare
{
    public enum AudioCategory
    {
        Master,
        Music,
        SoundEffects,
        UI,
    }

    public enum AudioConcurrencyPolicy
    {
        RejectNew,
        StopOldest,
    }

    public enum AudioOperationCode
    {
        Success,
        AlreadyCompleted,
        CueNotFound,
        InvalidConfiguration,
        LoadFailed,
        ConcurrencyRejected,
        Cancelled,
        Closed,
        Failed,
    }

    public readonly struct AudioCueId : IEquatable<AudioCueId>
    {
        public string Value { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public AudioCueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Audio Cue ID 不能为空", nameof(value));
            }

            Value = value;
        }

        public bool Equals(AudioCueId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AudioCueId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(AudioCueId left, AudioCueId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AudioCueId left, AudioCueId right)
        {
            return !left.Equals(right);
        }
    }

    public static class AudioCueIds
    {
        static readonly AudioCueId UiConfirmValue = new AudioCueId("ui.confirm");

        public static AudioCueId UiConfirm => UiConfirmValue;
    }

    public sealed class AudioOperationResult
    {
        public AudioOperationCode Code { get; }

        public AudioCueId CueId { get; }

        public Exception Exception { get; }

        public bool Succeeded => Code == AudioOperationCode.Success
            || Code == AudioOperationCode.AlreadyCompleted;

        AudioOperationResult(
            AudioOperationCode code,
            AudioCueId cueId,
            Exception exception)
        {
            Code = code;
            CueId = cueId;
            Exception = exception;
        }

        public static AudioOperationResult Success(AudioCueId cueId)
        {
            return new AudioOperationResult(AudioOperationCode.Success, cueId, null);
        }

        public static AudioOperationResult AlreadyCompleted(AudioCueId cueId)
        {
            return new AudioOperationResult(AudioOperationCode.AlreadyCompleted, cueId, null);
        }

        public static AudioOperationResult Failure(
            AudioOperationCode code,
            AudioCueId cueId,
            Exception exception = null)
        {
            if (code == AudioOperationCode.Success
                || code == AudioOperationCode.AlreadyCompleted)
            {
                throw new ArgumentOutOfRangeException(nameof(code), code, "失败结果代码非法");
            }

            return new AudioOperationResult(code, cueId, exception);
        }
    }

    public sealed class AudioPlaybackResult
    {
        public AudioOperationResult Operation { get; }

        public AudioPlaybackHandle Handle { get; }

        public bool Succeeded => Operation != null && Operation.Succeeded && Handle != null;

        internal AudioPlaybackResult(
            AudioOperationResult operation,
            AudioPlaybackHandle handle)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Handle = handle;
        }
    }

    public sealed class AudioPlaybackHandle : IDisposable
    {
        AudioService _service;
        readonly int _playbackId;
        readonly AudioCueId _cueId;

        internal AudioPlaybackHandle(
            AudioService service,
            int playbackId,
            AudioCueId cueId)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _playbackId = playbackId;
            _cueId = cueId;
        }

        public bool IsReleased => _service == null;

        public AudioOperationResult Stop()
        {
            AudioService service = _service;
            _service = null;
            return service == null
                ? AudioOperationResult.AlreadyCompleted(_cueId)
                : service.StopPlayback(_playbackId, _cueId);
        }

        public void Dispose()
        {
            Stop();
        }

        internal void MarkReleased()
        {
            _service = null;
        }
    }
}
