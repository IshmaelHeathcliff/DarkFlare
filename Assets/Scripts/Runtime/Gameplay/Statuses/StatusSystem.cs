using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class StatusModel : AbstractModel
    {
        internal StatusStore Store { get; private set; }

        protected override void OnInit()
        {
            Store = new StatusStore(GameArchitectureProvider.Generation);
        }

        protected override void OnDeinit()
        {
            Store.Dispose();
        }
    }

    public readonly struct StatusChangedEvent
    {
        public StatusResult Result { get; }
        public StatusChangedEvent(StatusResult result) { Result = result; }
    }

    public readonly struct StatusTickEvent
    {
        public StatusTick Tick { get; }
        public StatusTickEvent(StatusTick tick) { Tick = tick; }
    }

    public sealed partial class StatusSystem : AbstractSystem
    {
        StatusStore _store;
        public StatusStore Store => _store;

        protected override void OnInit()
        {
            _store = this.GetModel<StatusModel>().Store;
            _store.Changed += OnChanged;
            _store.Ticked += OnTick;
        }

        protected override void OnDeinit()
        {
            foreach (CombatActor actor in new List<CombatActor>(_actorTargets.Keys)) { Unbind(actor); }
            _actorTargets.Clear();
            _targetActors.Clear();
            _store.Changed -= OnChanged;
            _store.Ticked -= OnTick;
        }

        public StatusOperation ApplyStatus(StatusTargetId target, StatusMutation request, long? expectedVersion = null)
        {
            return _store.Execute(target, new[] { request }, expectedVersion);
        }

        public StatusOperation TryConsumeStatusStacks(StatusTargetId target, string definitionId, int count,
            StatusStackSelection selection = StatusStackSelection.Stored, IEnumerable<long> instanceIds = null, long? expectedVersion = null)
        {
            return ApplyStatus(target, StatusMutation.Consume(definitionId, count, selection, instanceIds), expectedVersion);
        }

        public StatusOperation DispelStatuses(StatusTargetId target, StatusFilter filter, int count = int.MaxValue)
        {
            return ApplyStatus(target, StatusMutation.Dispel(filter, count));
        }

        public StatusOperation ReleaseStatusSource(StatusTargetId target, StatusSource source)
        {
            return ApplyStatus(target, StatusMutation.ReleaseSource(source));
        }

        public StatusTargetSnapshot GetStatusSnapshot(StatusTargetId target)
        {
            return _store.Capture(target);
        }

        void OnChanged(StatusResult result)
        {
            this.SendEvent(new StatusChangedEvent(result));
        }

        void OnTick(StatusTick tick)
        {
            this.SendEvent(new StatusTickEvent(tick));
        }
    }
}
