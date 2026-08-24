using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare
{
    public sealed class AccessibilityService : IDisposable
    {
        readonly SettingsService _settings;

        bool _closed;

        public AccessibilityService(SettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Profile = MotionProfile.FromReduceMotion(settings.Current.ReduceMotion);
            _settings.Changed += OnSettingsChanged;
        }

        public MotionProfile Profile { get; private set; }

        public bool IsClosed => _closed;

        public event Action<MotionProfile> ProfileChanged;

        public UniTask<SettingsOperationResult> SetReduceMotionAsync(
            bool reduceMotion,
            CancellationToken cancellationToken = default)
        {
            if (_closed)
            {
                return UniTask.FromResult(new SettingsOperationResult(
                    SettingsOperationCode.Closed,
                    null,
                    SettingsRecoverySource.DefaultInvalid,
                    null));
            }

            return _settings.UpdateAsync(
                _settings.Current.WithReduceMotion(reduceMotion),
                cancellationToken);
        }

        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _settings.Changed -= OnSettingsChanged;
            ProfileChanged = null;
        }

        void OnSettingsChanged(UserSettingsSnapshot settings)
        {
            MotionProfile next = MotionProfile.FromReduceMotion(settings.ReduceMotion);

            if (next.Equals(Profile))
            {
                return;
            }

            Profile = next;
            ProfileChanged?.Invoke(next);
        }
    }
}
