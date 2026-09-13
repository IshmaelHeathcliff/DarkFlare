using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class StatusPreparation
    {
        internal StatusStore Owner { get; }
        internal StatusTargetState Before { get; }
        internal StatusTargetState After { get; }
        internal double Time { get; }
        internal bool Used { get; set; }
        internal IStatusProjection Projection { get; set; }
        public StatusTargetId Target { get; }
        public StatusResult Preview { get; }

        internal StatusPreparation(StatusStore owner, StatusTargetId target, StatusTargetState before,
            StatusTargetState after, double time, StatusResult preview)
        {
            Owner = owner;
            Target = target;
            Before = before;
            After = after;
            Time = time;
            Preview = preview;
        }
    }

    // 提交后必须 Publish 或 Rollback。未发布时锁定目标和时钟，宿主可精确恢复。
    public sealed class StatusCommit : IDisposable
    {
        readonly StatusStore _owner;
        internal StatusPreparation Preparation { get; }
        internal bool Resolved { get; set; }
        public StatusResult Result { get; internal set; }

        internal StatusCommit(StatusStore owner, StatusPreparation preparation, StatusResult result, bool resolved)
        {
            _owner = owner;
            Preparation = preparation;
            Result = result;
            Resolved = resolved;
        }

        public bool Publish()
        {
            return _owner.Resolve(this, false);
        }

        public bool Rollback()
        {
            return _owner.Resolve(this, true);
        }

        public void Dispose()
        {
            if (!Resolved) { Rollback(); }
        }
    }

    public sealed partial class StatusStore : IDisposable
    {
        readonly Dictionary<StatusTargetId, StatusTargetState> _targets = new Dictionary<StatusTargetId, StatusTargetState>();
        readonly Dictionary<string, StatusTargetId> _registrations = new Dictionary<string, StatusTargetId>(StringComparer.Ordinal);
        readonly Dictionary<StatusTargetId, IStatusParticipant> _participants = new Dictionary<StatusTargetId, IStatusParticipant>();
        internal void Attach(StatusTargetId target, IStatusParticipant participant) { _participants.Add(target, participant); }
        readonly Dictionary<StatusTargetId, StatusCommit> _locks = new Dictionary<StatusTargetId, StatusCommit>();
        readonly Queue<(StatusOperation Operation, Func<StatusResult> Execute)> _queue = new Queue<(StatusOperation, Func<StatusResult>)>();
        long _nextRegistration = 1;
        bool _notifying;
        bool _draining;
        bool _eventBoundary;
        bool _disposed;
        double _time;
        double _pendingUntil;
        bool _pending;
        bool _advancing;

        public int Generation { get; }
        public double Time => _time;
        public bool HasPendingTime => _pending;
        internal bool IsRegistered(StatusTargetId target) { return _targets.ContainsKey(target); }
        public int PendingOperations => _queue.Count;
        public event Action<StatusResult> Changed;
        public event Action<StatusTick> Ticked;

        public StatusStore(int generation)
        {
            if (generation <= 0) { throw new ArgumentOutOfRangeException(nameof(generation)); }
            Generation = generation;
        }

        public StatusTargetId RegisterTarget(PlayerId player)
        {
            if (!ContentId.IsValidSegment(player.Value)) { throw new ArgumentException("玩家身份非法"); }
            return Register("player:" + player.Value);
        }

        public StatusTargetId RegisterTarget(MonsterInstanceId monster)
        {
            if (!MonsterInstanceId.TryParse(monster.Value, out _)) { throw new ArgumentException("怪物身份非法"); }
            return Register(monster.Value);
        }

        StatusTargetId Register(string key)
        {
            if (_disposed || _notifying || _pending || _advancing || _locks.Count > 0 || _queue.Count > 0)
            {
                throw new InvalidOperationException("状态系统当前不能登记目标");
            }
            if (_registrations.TryGetValue(key, out StatusTargetId existing)) { return existing; }
            if (_nextRegistration == long.MaxValue) { throw new InvalidOperationException("状态目标序号已耗尽"); }
            var target = new StatusTargetId(Generation, _nextRegistration++, key);
            _targets.Add(target, new StatusTargetState());
            _registrations.Add(key, target);
            return target;
        }

        public StatusTargetSnapshot Capture(StatusTargetId target)
        {
            _targets.TryGetValue(target, out StatusTargetState state);
            // 暂存提交尚未发布时，公开查询仍呈现提交前状态。
            if (_locks.TryGetValue(target, out StatusCommit pending)) { state = pending.Preparation.Before; }
            return new StatusTargetSnapshot(target, state, _time);
        }

        public StatusPreparation Prepare(StatusTargetId target, IEnumerable<StatusMutation> mutations, long? expectedVersion = null)
        {
            StatusResultCode code = CheckWrite(target);
            _targets.TryGetValue(target, out StatusTargetState before);
            if (code == StatusResultCode.Success && expectedVersion.HasValue && expectedVersion != before.Version)
            {
                code = StatusResultCode.StaleVersion;
            }
            var changes = new List<StatusChange>();
            StatusTargetState after = null;
            if (code == StatusResultCode.Success)
            {
                after = before.Copy();
                if (mutations == null) { code = StatusResultCode.InvalidRequest; }
                else
                {
                    int count = 0;
                    long work = 0;
                    foreach (StatusMutation mutation in mutations)
                    {
                        if (++count > StatusMutationResolver.MaxTargetLayers) { code = StatusResultCode.Capacity; break; }
                        long requestedWork = mutation == null ? 1 : Math.Max(1L, mutation.Count);
                        if (mutation != null && mutation.Kind != StatusMutationKind.Apply && mutation.Kind != StatusMutationKind.UpdateSource)
                        {
                            requestedWork = mutation.Kind == StatusMutationKind.ReleaseSource
                                ? Math.Max(1, after.Layers.Count) : Math.Min(requestedWork, Math.Max(1, after.Layers.Count));
                        }
                        work += requestedWork;
                        if (work > StatusMutationResolver.MaxTargetLayers) { code = StatusResultCode.Capacity; break; }
                        if (mutation != null && mutation.Source.Actor.IsValid && mutation.Source.Actor.Generation != Generation)
                        {
                            code = StatusResultCode.InvalidRequest;
                            break;
                        }
                        try
                        {
                            StatusMutation normalized = _participants.TryGetValue(target, out IStatusParticipant participant)
                                ? participant.Normalize(target, mutation) : mutation;
                            code = StatusMutationResolver.Apply(after, normalized, _time, changes);
                        }
                        catch (ArgumentException) { code = StatusResultCode.InvalidRequest; }
                        if (code != StatusResultCode.Success) { break; }
                    }
                }
                if (code == StatusResultCode.Success)
                {
                    if (changes.Count == 0) { code = StatusResultCode.NoChange; }
                    else if (before.Version == long.MaxValue) { code = StatusResultCode.Capacity; }
                    else { after.Version = before.Version + 1; }
                }
            }
            bool succeeded = code == StatusResultCode.Success || code == StatusResultCode.NoChange;
            var preparation = new StatusPreparation(this, target, before, succeeded ? after : null, _time,
                new StatusResult(code, succeeded ? new StatusTargetSnapshot(target, after, _time) : Capture(target),
                    succeeded ? changes : null));
            if (succeeded && _participants.TryGetValue(target, out IStatusParticipant effectParticipant))
            {
                try { preparation.Projection = effectParticipant.Prepare(preparation.Preview.Snapshot); }
                catch (ArgumentException)
                {
                    return new StatusPreparation(this, target, before, null, _time,
                        new StatusResult(StatusResultCode.InvalidRequest, Capture(target)));
                }
            }
            return preparation;
        }

        public StatusCommit Commit(StatusPreparation preparation)
        {
            if (preparation == null || !ReferenceEquals(preparation.Owner, this))
            {
                return Rejected(preparation, StatusResultCode.InvalidRequest);
            }
            if (preparation.Used) { return Rejected(preparation, StatusResultCode.AlreadyCommitted); }
            preparation.Used = true;
            if (!preparation.Preview.Succeeded) { return Rejected(preparation, preparation.Preview.Code); }
            StatusResultCode code = CheckWrite(preparation.Target);
            if (code != StatusResultCode.Success) { return Rejected(preparation, code); }
            if (!ReferenceEquals(_targets[preparation.Target], preparation.Before) || preparation.Time != _time
                || preparation.Projection != null && !preparation.Projection.IsCurrent)
            {
                return Rejected(preparation, StatusResultCode.StaleVersion);
            }
            bool changed = preparation.Preview.Code == StatusResultCode.Success;
            var commit = new StatusCommit(this, preparation, preparation.Preview, !changed);
            if (changed)
            {
                _targets[preparation.Target] = preparation.After;
                _locks.Add(preparation.Target, commit);
            }
            return commit;
        }

        StatusCommit Rejected(StatusPreparation preparation, StatusResultCode code)
        {
            return new StatusCommit(this, preparation,
                new StatusResult(code, Capture(preparation != null ? preparation.Target : default)), true);
        }

        internal bool Resolve(StatusCommit commit, bool rollback)
        {
            StatusPreparation preparation = commit.Preparation;
            if (commit.Resolved || preparation == null
                || !_locks.TryGetValue(preparation.Target, out StatusCommit current) || !ReferenceEquals(current, commit))
            {
                return false;
            }
            if (!rollback && preparation.Projection != null && !preparation.Projection.IsCurrent)
            {
                rollback = true;
                commit.Result = new StatusResult(StatusResultCode.StaleVersion, new StatusTargetSnapshot(preparation.Target, preparation.Before, _time));
            }
            commit.Resolved = true;
            _locks.Remove(preparation.Target);
            if (rollback) { _targets[preparation.Target] = preparation.Before; }
            else
            {
                preparation.Projection?.Apply();
                Notify(() => { preparation.Projection?.Publish(); Changed?.Invoke(commit.Result); });
                DrainOperations();
            }
            return commit.Result.Succeeded;
        }

        public StatusOperation Execute(StatusTargetId target, IEnumerable<StatusMutation> mutations, long? expectedVersion = null)
        {
            // 冻结调用方序列，排队期间不能被替换。
            StatusMutation[] frozen = mutations?.Take(StatusMutationResolver.MaxTargetLayers + 1).ToArray();
            return Submit(() =>
            {
                using (StatusCommit commit = Commit(Prepare(target, frozen, expectedVersion)))
                {
                    commit.Publish();
                    return commit.Result;
                }
            });
        }

        internal StatusOperation ReleaseBoundSource(StatusTargetId target, StatusSource source)
        {
            return Submit(() =>
            {
                bool previous = _eventBoundary;
                _eventBoundary = true;
                try
                {
                    using (StatusCommit commit = Commit(Prepare(target, new[] { StatusMutation.ReleaseSource(source) })))
                    {
                        commit.Publish();
                        return commit.Result;
                    }
                }
                finally { _eventBoundary = previous; }
            });
        }

        public StatusOperation UnregisterTarget(StatusTargetId target)
        {
            return UnregisterTarget(target, false);
        }

        internal StatusOperation UnregisterBoundTarget(StatusTargetId target)
        {
            if (_locks.TryGetValue(target, out StatusCommit commit)) { commit.Rollback(); }
            return UnregisterTarget(target, true);
        }

        StatusOperation UnregisterTarget(StatusTargetId target, bool lifecycle)
        {
            return Submit(() =>
            {
                StatusResultCode code = CheckWrite(target, lifecycle);
                if (code != StatusResultCode.Success) { return new StatusResult(code, Capture(target)); }
                StatusTargetState state = _targets[target];
                var changes = new List<StatusChange>();
                foreach (StatusLayer layer in state.Layers)
                {
                    changes.Add(new StatusChange(StatusChangeReason.TargetRemoved, new StatusLayerSnapshot(layer, _time, state.IsActive(layer))));
                }
                IStatusProjection projection = _participants.TryGetValue(target, out IStatusParticipant participant)
                    ? participant.Prepare(new StatusTargetSnapshot(target, null, _time)) : null;
                projection?.Apply();
                _targets.Remove(target);
                _registrations.Remove(target.ActorKey);
                _participants.Remove(target);
                var result = new StatusResult(StatusResultCode.Success, Capture(target), changes);
                Notify(() => { projection?.Publish(); Changed?.Invoke(result); });
                return result;
            });
        }

        StatusResultCode CheckWrite(StatusTargetId target, bool lifecycle = false)
        {
            if (_disposed || !target.IsValid || !_targets.ContainsKey(target)) { return StatusResultCode.InvalidTarget; }
            if (_notifying || _locks.ContainsKey(target) || (_queue.Count > 0 && !_draining)
                || (_pending && !_eventBoundary && !lifecycle)) { return StatusResultCode.Busy; }
            return StatusResultCode.Success;
        }

        StatusOperation Submit(Func<StatusResult> action)
        {
            var operation = new StatusOperation();
            if (_notifying || _draining || _queue.Count > 0)
            {
                if (_queue.Count >= StatusMutationResolver.MaxTargetLayers)
                {
                    operation.Result = new StatusResult(StatusResultCode.Busy, Capture(default));
                }
                else { _queue.Enqueue((operation, action)); }
            }
            else
            {
                operation.Result = action();
                DrainOperations();
            }
            return operation;
        }

        public void DrainOperations(int budget = 256)
        {
            if (budget <= 0) { throw new ArgumentOutOfRangeException(nameof(budget)); }
            if (_notifying || _draining || (_pending && !_eventBoundary)) { return; }
            _draining = true;
            try
            {
                while (_queue.Count > 0 && budget-- > 0)
                {
                    (StatusOperation operation, Func<StatusResult> execute) = _queue.Dequeue();
                    operation.Result = execute();
                }
            }
            finally { _draining = false; }
        }

        void Notify(Action notification)
        {
            _notifying = true;
            try { notification(); }
            finally { _notifying = false; }
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            foreach (StatusCommit commit in _locks.Values) { commit.Resolved = true; }
            _locks.Clear();
            _targets.Clear();
            _participants.Clear();
            _registrations.Clear();
            while (_queue.Count > 0)
            {
                var item = _queue.Dequeue();
                item.Operation.Result = new StatusResult(StatusResultCode.InvalidTarget, Capture(default));
            }
            _pending = false;
            _pendingUntil = _time;
            Changed = null;
            Ticked = null;
        }
    }
}
