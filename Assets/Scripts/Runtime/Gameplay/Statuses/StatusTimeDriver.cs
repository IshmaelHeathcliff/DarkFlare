using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct StatusTimeChangedEvent { }

    public sealed partial class StatusSystem
    {
        bool _clockRunning;
        bool _clockHeld;
        double _elapsed;

        public bool IsClockRunning => _clockRunning;
        public double PendingElapsed => _elapsed;

        public async UniTask RunClockAsync(Func<bool> canAdvance, CancellationToken token)
        {
            if (_clockRunning) { throw new InvalidOperationException("状态时间任务已启动"); }
            _clockRunning = true;
            try
            {
                while (true)
                {
                    await UniTask.Yield(PlayerLoopTiming.LastUpdate, token);
                    if (!_clockHeld && canAdvance() && !GameTimeService.Shared.IsPaused)
                    {
                        _elapsed += Time.deltaTime;
                        PumpClock();
                    }
                }
            }
            finally { _clockRunning = false; }
        }

        void PumpClock()
        {
            double elapsed = _store.HasPendingTime ? 0 : _elapsed;
            StatusAdvanceResult result = _store.Advance(elapsed);
            if (result.Code == StatusResultCode.Busy) { return; }
            if (result.Code != StatusResultCode.Success)
            {
                throw new InvalidOperationException("状态时间推进失败：" + result.Code);
            }
            _elapsed -= elapsed;
            if (elapsed > 0 && _store.HasLayers) { this.SendEvent(new StatusTimeChangedEvent()); }
        }

        internal async UniTask<IDisposable> HoldClockAsync(CancellationToken token)
        {
            if (_clockHeld) { throw new InvalidOperationException("状态时钟已被占用"); }
            _clockHeld = true;
            GamePauseLease pause = GameTimeService.Shared.AcquirePause("status-snapshot");
            try
            {
                // 先离开领域通知栈，不能在伤害或状态回调内递归结算。
                await UniTask.Yield(PlayerLoopTiming.LastUpdate, token);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    PumpClock();
                    if (_store.IsQuiescent && _elapsed == 0) { break; }
                    await UniTask.Yield(PlayerLoopTiming.LastUpdate, token);
                }
                return new ClockHold(this, pause);
            }
            catch
            {
                _clockHeld = false;
                pause.Dispose();
                throw;
            }
        }

        public StatusAdvanceResult AdvanceManually(double elapsed)
        {
            if (_clockHeld || (_clockRunning && !GameTimeService.Shared.IsPaused) || _elapsed > 0)
            {
                return new StatusAdvanceResult(StatusResultCode.Busy, 0, _store.Time, _store.Time, _store.HasPendingTime);
            }
            return _store.Advance(elapsed);
        }

        sealed class ClockHold : IDisposable
        {
            StatusSystem _owner;
            readonly GamePauseLease _pause;
            internal ClockHold(StatusSystem owner, GamePauseLease pause) { _owner = owner; _pause = pause; }
            public void Dispose()
            {
                if (_owner == null) { return; }
                _owner._clockHeld = false;
                _owner = null;
                _pause.Dispose();
            }
        }
    }
}
