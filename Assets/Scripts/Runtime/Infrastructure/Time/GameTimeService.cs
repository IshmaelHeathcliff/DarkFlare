using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public sealed class GameTimeService
    {
        static readonly GameTimeService SharedInstance = new GameTimeService();

        readonly Dictionary<int, string> _pauseOwners = new Dictionary<int, string>();
        float _baseTimeScale = 1f;
        int _nextLeaseId;

        public static GameTimeService Shared => SharedInstance;

        public bool IsPaused => _pauseOwners.Count > 0;

        public int PauseLeaseCount => _pauseOwners.Count;

        public float BaseTimeScale => _baseTimeScale;

        public event Action<bool> PauseChanged;

        public GamePauseLease AcquirePause(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("暂停所有者不能为空", nameof(owner));
            }

            if (_pauseOwners.Count == 0)
            {
                _baseTimeScale = Mathf.Max(0f, Time.timeScale);
                Time.timeScale = 0f;
            }

            int leaseId = ++_nextLeaseId;
            _pauseOwners.Add(leaseId, owner);

            if (_pauseOwners.Count == 1)
            {
                PauseChanged?.Invoke(true);
            }

            return new GamePauseLease(this, leaseId, owner);
        }

        public void SetBaseTimeScale(float timeScale)
        {
            if (float.IsNaN(timeScale) || float.IsInfinity(timeScale) || timeScale < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale));
            }

            _baseTimeScale = timeScale;

            if (!IsPaused)
            {
                Time.timeScale = timeScale;
            }
        }

        public void RestoreAll()
        {
            bool wasPaused = IsPaused;
            _pauseOwners.Clear();
            Time.timeScale = _baseTimeScale;

            if (wasPaused)
            {
                PauseChanged?.Invoke(false);
            }
        }

        internal void Release(int leaseId)
        {
            if (!_pauseOwners.Remove(leaseId) || _pauseOwners.Count > 0)
            {
                return;
            }

            Time.timeScale = _baseTimeScale;
            PauseChanged?.Invoke(false);
        }
    }

    public sealed class GamePauseLease : IDisposable
    {
        GameTimeService _service;
        readonly int _leaseId;

        internal GamePauseLease(GameTimeService service, int leaseId, string owner)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _leaseId = leaseId;
            Owner = owner;
        }

        public string Owner { get; }

        public bool IsReleased => _service == null;

        public void Dispose()
        {
            GameTimeService service = _service;
            _service = null;
            service?.Release(_leaseId);
        }
    }
}
