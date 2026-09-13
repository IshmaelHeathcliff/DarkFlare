using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public sealed partial class StatusStore
    {
        public StatusAdvanceResult Advance(double elapsed, int eventBudget = 256)
        {
            if (_disposed || !StatusRules.IsFinite(elapsed) || elapsed < 0 || eventBudget <= 0
                || !StatusRules.IsFinite(_time + elapsed) || _time + elapsed > StatusRules.MaximumTime)
            {
                return AdvanceResult(StatusResultCode.InvalidRequest, 0);
            }
            if (_advancing || _notifying || _locks.Count > 0 || (_pending && elapsed != 0))
            {
                return AdvanceResult(StatusResultCode.Busy, 0);
            }
            _eventBoundary = true;
            try { DrainOperations(); }
            finally { _eventBoundary = false; }
            if (_queue.Count > 0) { return AdvanceResult(StatusResultCode.Busy, 0); }
            if (!_pending) { _pendingUntil = _time + elapsed; _pending = true; }
            _advancing = true;
            int processed = 0;
            try
            {
                while (!_disposed)
                {
                    if (!FindNext(out StatusTargetId target, out StatusLayer layer, out double due, out bool tick)
                        || (due > _pendingUntil && !SameTime(due, _pendingUntil)))
                    {
                        _time = _pendingUntil;
                        _pending = false;
                        break;
                    }
                    if (processed >= eventBudget) { break; }
                    _pendingUntil = Math.Max(_pendingUntil, due);
                    double previousTime = _time;
                    _time = Math.Max(_time, due);
                    StatusTargetState state = _targets[target];
                    // 复制后写入，使之前准备的事务不能跨越同刻周期 / 到期提交。
                    StatusTargetState next = state.Copy();
                    StatusLayer nextLayer = next.Layers.Find(item => item.Id == layer.Id);
                    next.Version = checked(next.Version + 1);
                    _targets[target] = next;
                    processed++;
                    _eventBoundary = true;
                    try
                    {
                        if (tick)
                        {
                            bool active = state.IsActive(layer);
                            var snapshot = new StatusLayerSnapshot(layer, _time, active);
                            nextLayer.TickOrdinal = checked(nextLayer.TickOrdinal + 1);
                            double following = nextLayer.AppliedAt + nextLayer.TickOrdinal * nextLayer.Rules.Interval;
                            // 周期由施加时间和序号推导，避免逐跳加法漂移。
                            nextLayer.NextTickAt = SameTime(following, nextLayer.ExpiresAt)
                                ? nextLayer.ExpiresAt : following;
                            if (active)
                            {
                                var record = new StatusTick(target, _time, snapshot);
                                if (_participants.TryGetValue(target, out IStatusParticipant participant))
                                {
                                    participant.Tick(record);
                                    DrainOperations();
                                }
                                Notify(() => Ticked?.Invoke(record));
                            }
                        }
                        else
                        {
                            var changes = new List<StatusChange>();
                            StatusMutationResolver.Remove(next, new List<StatusLayer> { nextLayer }, StatusChangeReason.Expired, _time, changes);
                            IStatusProjection projection;
                            try
                            {
                                projection = _participants.TryGetValue(target, out IStatusParticipant participant)
                                    ? participant.Prepare(Capture(target)) : null;
                            }
                            catch (ArgumentException)
                            {
                                _targets[target] = state;
                                _time = previousTime;
                                return AdvanceResult(StatusResultCode.InvalidRequest, processed - 1);
                            }
                            projection?.Apply();
                            var result = new StatusResult(StatusResultCode.Success, Capture(target), changes);
                            Notify(() => { projection?.Publish(); Changed?.Invoke(result); });
                        }
                        DrainOperations();
                        if (_queue.Count > 0 || _locks.Count > 0) { break; }
                    }
                    finally { _eventBoundary = false; }
                }
            }
            finally { _advancing = false; }
            return AdvanceResult(StatusResultCode.Success, processed);
        }

        bool FindNext(out StatusTargetId target, out StatusLayer layer, out double due, out bool tick)
        {
            target = default;
            layer = null;
            due = double.PositiveInfinity;
            tick = false;
            foreach (KeyValuePair<StatusTargetId, StatusTargetState> pair in _targets)
            {
                foreach (StatusLayer candidate in pair.Value.Layers)
                {
                    bool candidateTick = candidate.NextTickAt <= candidate.ExpiresAt;
                    double candidateTime = candidateTick ? candidate.NextTickAt : candidate.ExpiresAt;
                    if (double.IsPositiveInfinity(candidateTime)) { continue; }
                    if (candidateTime < due || (candidateTime == due && (layer == null
                        || (candidateTick && !tick) || (candidateTick == tick
                            && (pair.Key.Registration < target.Registration
                                || (pair.Key.Registration == target.Registration && candidate.Id < layer.Id))))))
                    {
                        target = pair.Key;
                        layer = candidate;
                        due = candidateTime;
                        tick = candidateTick;
                    }
                }
            }
            return layer != null;
        }

        StatusAdvanceResult AdvanceResult(StatusResultCode code, int processed)
        {
            return new StatusAdvanceResult(code, processed, _time, _pendingUntil, _pending);
        }

        static bool SameTime(double first, double second)
        {
            return StatusRules.IsFinite(first) && StatusRules.IsFinite(second)
                && Math.Abs(first - second) <= Math.Min(0.000000001,
                    Math.Max(1, Math.Max(Math.Abs(first), Math.Abs(second))) * 0.000000000000002);
        }
    }
}
