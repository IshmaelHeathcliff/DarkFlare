using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public enum StatusResultCode
    {
        Success, NoChange, InvalidTarget, InvalidRequest, RuleConflict, Capacity,
        InsufficientStacks, Protected, StaleVersion, AlreadyCommitted, Busy
    }

    public enum StatusChangeReason
    {
        Applied, Refreshed, Expired, Consumed, Dispelled, SourceReleased, Replaced, TargetRemoved
    }

    public enum StatusStackSelection { Stored, Active, Instances }
    internal enum StatusMutationKind { Apply, Consume, Dispel, ReleaseSource, UpdateSource }

    public sealed class StatusFilter
    {
        public string DefinitionId { get; }
        public StatusCategory? Category { get; }
        public TagSet RequiredTags { get; }

        public StatusFilter(string definitionId = null, StatusCategory? category = null, TagSet requiredTags = null)
        {
            if ((definitionId != null && !ContentId.IsValidSegment(definitionId))
                || (category.HasValue && !Enum.IsDefined(typeof(StatusCategory), category.Value)))
            {
                throw new ArgumentException("状态筛选条件非法");
            }
            DefinitionId = definitionId;
            Category = category;
            RequiredTags = requiredTags ?? TagSet.Empty;
        }

        public bool Matches(StatusRules rules)
        {
            return rules != null && (DefinitionId == null || rules.Id.LocalId == DefinitionId)
                && (!Category.HasValue || Category == rules.Category) && rules.Tags.ContainsAll(RequiredTags);
        }
    }

    public sealed class StatusMutation
    {
        internal StatusMutationKind Kind { get; }
        public StatusRules Rules { get; }
        public StatusEffectSnapshot Effects { get; }
        public StatusSource Source { get; }
        public int Count { get; }
        public StatusFilter Filter { get; }
        public StatusStackSelection Selection { get; }
        public IReadOnlyList<long> InstanceIds { get; }
        public double? Duration { get; }
        public long? RefreshInstanceId { get; }

        internal StatusMutation WithEffects(StatusEffectSnapshot effects)
        {
            return new StatusMutation(Kind, Rules, effects, Source, Count, Filter, Selection, InstanceIds, Duration, RefreshInstanceId);
        }

        StatusMutation(StatusMutationKind kind, StatusRules rules = null, StatusEffectSnapshot effects = null,
            StatusSource source = default, int count = 1, StatusFilter filter = null,
            StatusStackSelection selection = StatusStackSelection.Stored, IEnumerable<long> instanceIds = null,
            double? duration = null, long? refreshInstanceId = null)
        {
            Kind = kind;
            Rules = rules;
            Effects = effects;
            Source = source;
            Count = count;
            Filter = filter;
            Selection = selection;
            InstanceIds = new List<long>(instanceIds ?? Array.Empty<long>()).AsReadOnly();
            Duration = duration;
            RefreshInstanceId = refreshInstanceId;
        }

        public static StatusMutation Apply(StatusRules rules, StatusSource source, StatusEffectSnapshot effects = null,
            int count = 1, double? duration = null, long? refreshInstanceId = null)
        {
            return new StatusMutation(StatusMutationKind.Apply, rules, effects ?? StatusEffectSnapshot.Marker,
                source, count, duration: duration, refreshInstanceId: refreshInstanceId);
        }

        public static StatusMutation UpdateSource(StatusRules rules, StatusSource source,
            StatusEffectSnapshot effects = null, int count = 1)
        {
            return new StatusMutation(StatusMutationKind.UpdateSource, rules, effects ?? StatusEffectSnapshot.Marker, source, count);
        }

        public static StatusMutation Consume(string definitionId, int count,
            StatusStackSelection selection = StatusStackSelection.Stored, IEnumerable<long> instanceIds = null)
        {
            if (!ContentId.IsValidSegment(definitionId)) { throw new ArgumentException("消费必须指定合法状态 ID", nameof(definitionId)); }
            return new StatusMutation(StatusMutationKind.Consume, count: count,
                filter: new StatusFilter(definitionId), selection: selection, instanceIds: instanceIds);
        }

        public static StatusMutation Dispel(StatusFilter filter, int count = int.MaxValue)
        {
            return new StatusMutation(StatusMutationKind.Dispel, count: count, filter: filter ?? new StatusFilter());
        }

        public static StatusMutation ReleaseSource(StatusSource source)
        {
            return new StatusMutation(StatusMutationKind.ReleaseSource, source: source);
        }
    }

    public sealed class StatusLayerSnapshot
    {
        public long InstanceId { get; }
        public StatusRules Rules { get; }
        public StatusEffectSnapshot Effects { get; }
        public StatusSource Source { get; }
        public double AppliedAt { get; }
        public double ExpiresAt { get; }
        public double NextTickAt { get; }
        public double RemainingSeconds { get; }
        public bool IsActive { get; }

        internal StatusLayerSnapshot(StatusLayer layer, double now, bool active)
        {
            InstanceId = layer.Id;
            Rules = layer.Rules;
            Effects = layer.Effects;
            Source = layer.Source;
            AppliedAt = layer.AppliedAt;
            ExpiresAt = layer.ExpiresAt;
            NextTickAt = layer.NextTickAt;
            RemainingSeconds = Math.Max(0, layer.ExpiresAt - now);
            IsActive = active;
        }
    }

    public sealed class StatusTargetSnapshot
    {
        public StatusTargetId Target { get; }
        public bool IsRegistered { get; }
        public long Version { get; }
        public double Time { get; }
        public IReadOnlyList<StatusLayerSnapshot> Layers { get; }

        internal StatusTargetSnapshot(StatusTargetId target, StatusTargetState state, double time)
        {
            Target = target;
            IsRegistered = state != null;
            Version = state != null ? state.Version : 0;
            Time = time;
            Layers = (state != null
                ? state.Layers.Select(layer => new StatusLayerSnapshot(layer, time, state.IsActive(layer))).ToList()
                : new List<StatusLayerSnapshot>()).AsReadOnly();
        }

        public int GetStacks(StatusFilter filter = null, bool activeOnly = false)
        {
            return Layers.Count(layer => (!activeOnly || layer.IsActive) && (filter == null || filter.Matches(layer.Rules)));
        }

        public bool HasStatus(string definitionId)
        {
            return GetStacks(new StatusFilter(definitionId)) > 0;
        }
    }

    public readonly struct StatusChange
    {
        public StatusChangeReason Reason { get; }
        public StatusLayerSnapshot Layer { get; }

        internal StatusChange(StatusChangeReason reason, StatusLayerSnapshot layer)
        {
            Reason = reason;
            Layer = layer;
        }
    }

    public sealed class StatusResult
    {
        public StatusResultCode Code { get; }
        public bool Succeeded => Code == StatusResultCode.Success || Code == StatusResultCode.NoChange;
        public StatusTargetSnapshot Snapshot { get; }
        public IReadOnlyList<StatusChange> Changes { get; }

        internal StatusResult(StatusResultCode code, StatusTargetSnapshot snapshot, IEnumerable<StatusChange> changes = null)
        {
            Code = code;
            Snapshot = snapshot;
            Changes = new List<StatusChange>(changes ?? Array.Empty<StatusChange>()).AsReadOnly();
        }
    }

    public sealed class StatusOperation
    {
        public bool IsCompleted => Result != null;
        public StatusResult Result { get; internal set; }
    }

    public readonly struct StatusTick
    {
        public StatusTargetId Target { get; }
        public double Time { get; }
        public StatusLayerSnapshot Layer { get; }

        internal StatusTick(StatusTargetId target, double time, StatusLayerSnapshot layer)
        {
            Target = target;
            Time = time;
            Layer = layer;
        }
    }

    public readonly struct StatusAdvanceResult
    {
        public StatusResultCode Code { get; }
        public int ProcessedEvents { get; }
        public double Time { get; }
        public double PendingUntil { get; }
        public bool HasPending { get; }

        internal StatusAdvanceResult(StatusResultCode code, int processedEvents, double time, double pendingUntil, bool hasPending)
        {
            Code = code;
            ProcessedEvents = processedEvents;
            Time = time;
            PendingUntil = pendingUntil;
            HasPending = hasPending;
        }
    }

    internal sealed class StatusLayer
    {
        internal long Id { get; set; }
        internal StatusRules Rules { get; set; }
        internal StatusEffectSnapshot Effects { get; set; }
        internal StatusSource Source { get; set; }
        internal double AppliedAt { get; set; }
        internal double ExpiresAt { get; set; }
        internal double NextTickAt { get; set; }
        internal long TickOrdinal { get; set; }

        internal StatusLayer Copy()
        {
            return (StatusLayer)MemberwiseClone();
        }
    }

    internal sealed class StatusTargetState
    {
        internal long Version { get; set; }
        internal long NextId { get; set; } = 1;
        internal List<StatusLayer> Layers { get; } = new List<StatusLayer>();

        internal StatusTargetState Copy()
        {
            var copy = new StatusTargetState { Version = Version, NextId = NextId };
            copy.Layers.AddRange(Layers.Select(layer => layer.Copy()));
            return copy;
        }

        internal bool IsActive(StatusLayer layer)
        {
            return layer.Rules.Repeat != StatusRepeatMode.Strongest || !Layers.Any(other => other.Rules.Id == layer.Rules.Id
                && (other.Effects.Strength > layer.Effects.Strength
                    || (other.Effects.Strength == layer.Effects.Strength && other.Id < layer.Id)));
        }
    }
}
