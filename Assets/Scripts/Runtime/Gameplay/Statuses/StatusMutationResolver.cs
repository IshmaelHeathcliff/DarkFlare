using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    internal static class StatusMutationResolver
    {
        internal const int MaxTargetLayers = 4096;

        internal static StatusResultCode Apply(StatusTargetState state, StatusMutation request, double now, List<StatusChange> changes)
        {
            if (request == null) { return StatusResultCode.InvalidRequest; }
            if (request.Kind == StatusMutationKind.Apply || request.Kind == StatusMutationKind.UpdateSource)
            {
                return Add(state, request, now, changes);
            }
            if (request.Kind == StatusMutationKind.ReleaseSource)
            {
                if (!request.Source.IsValid) { return StatusResultCode.InvalidRequest; }
                Remove(state, state.Layers.Where(layer => layer.Rules.Lifetime == StatusLifetime.SourceOwned
                    && layer.Source.Equals(request.Source)).ToList(), StatusChangeReason.SourceReleased, now, changes);
                return StatusResultCode.Success;
            }
            if (request.Count <= 0 || request.Filter == null || !Enum.IsDefined(typeof(StatusStackSelection), request.Selection))
            {
                return StatusResultCode.InvalidRequest;
            }
            List<StatusLayer> matching = state.Layers.Where(layer => request.Filter.Matches(layer.Rules)).ToList();
            if (request.Kind == StatusMutationKind.Dispel)
            {
                Remove(state, Ordered(matching.Where(layer => layer.Rules.CanDispel)).Take(request.Count).ToList(),
                    StatusChangeReason.Dispelled, now, changes);
                return StatusResultCode.Success;
            }
            if (matching.Any(layer => !layer.Rules.CanConsume)) { return StatusResultCode.Protected; }
            if (request.Selection == StatusStackSelection.Active) { matching.RemoveAll(layer => !state.IsActive(layer)); }
            if (request.Selection == StatusStackSelection.Instances)
            {
                if (request.InstanceIds.Count != request.Count || request.InstanceIds.Distinct().Count() != request.Count
                    || request.InstanceIds.Any(id => id <= 0)) { return StatusResultCode.InvalidRequest; }
                matching.RemoveAll(layer => !request.InstanceIds.Contains(layer.Id));
                if (matching.Count != request.Count) { return StatusResultCode.StaleVersion; }
            }
            else if (request.InstanceIds.Count > 0) { return StatusResultCode.InvalidRequest; }
            if (matching.Count < request.Count) { return StatusResultCode.InsufficientStacks; }
            Remove(state, Ordered(matching).Take(request.Count).ToList(), StatusChangeReason.Consumed, now, changes);
            return StatusResultCode.Success;
        }

        static StatusResultCode Add(StatusTargetState state, StatusMutation request, double now, List<StatusChange> changes)
        {
            StatusRules rules = request.Rules;
            if (rules == null || request.Effects == null || !request.Source.IsValid
                || request.Count <= 0 || request.Count > MaxTargetLayers
                || (request.Effects.PeriodicDamage.Count > 0 && rules.Interval <= 0)
                || (request.Duration.HasValue && (rules.Lifetime != StatusLifetime.Timed
                    || !StatusRules.IsFinite(request.Duration.Value) || request.Duration <= 0 || request.Duration > StatusRules.MaximumTime)))
            {
                return StatusResultCode.InvalidRequest;
            }
            double duration = request.Duration ?? rules.Duration;
            double expiry = rules.Lifetime == StatusLifetime.Timed ? now + duration : double.PositiveInfinity;
            double nextTick = rules.Interval > 0 ? now + rules.Interval : double.PositiveInfinity;
            if ((rules.Lifetime == StatusLifetime.Timed && (!StatusRules.IsFinite(expiry) || expiry <= now))
                || (rules.Interval > 0 && (!StatusRules.IsFinite(nextTick) || nextTick <= now)))
            {
                return StatusResultCode.InvalidRequest;
            }
            if (rules.Repeat == StatusRepeatMode.Uniform && request.Effects.Modifiers.Any(modifier =>
                Math.Abs((double)modifier.Value) * rules.MaxStacks > float.MaxValue))
            {
                return StatusResultCode.InvalidRequest;
            }
            List<StatusLayer> group = state.Layers.Where(layer => layer.Rules.Id == rules.Id).ToList();
            if (group.Any(layer => !layer.Rules.Matches(rules))) { return StatusResultCode.RuleConflict; }
            if (request.RefreshInstanceId.HasValue && (rules.Repeat != StatusRepeatMode.Independent
                || rules.Overflow != StatusOverflow.RefreshTime || group.Count != rules.MaxStacks
                || !group.Any(layer => layer.Id == request.RefreshInstanceId)))
            {
                return StatusResultCode.StaleVersion;
            }
            if (request.Kind == StatusMutationKind.UpdateSource && rules.Lifetime != StatusLifetime.SourceOwned)
            {
                return StatusResultCode.InvalidRequest;
            }
            if (rules.Lifetime == StatusLifetime.SourceOwned)
            {
                List<StatusLayer> owned = group.Where(layer => layer.Source.Equals(request.Source)).ToList();
                if (owned.Count > 0 && request.Kind == StatusMutationKind.Apply) { return StatusResultCode.Success; }
                if (request.Kind == StatusMutationKind.UpdateSource)
                {
                    Remove(state, owned, StatusChangeReason.Replaced, now, changes);
                    group.RemoveAll(layer => owned.Contains(layer));
                }
                // 新维持来源必须真正拥有至少一层，不能借刷新改写其他提供方的效果。
                if (rules.Repeat == StatusRepeatMode.Uniform && group.Count == rules.MaxStacks)
                {
                    return StatusResultCode.Capacity;
                }
            }
            if (rules.Repeat == StatusRepeatMode.Uniform)
            {
                if (group.Count == rules.MaxStacks && rules.Overflow == StatusOverflow.Reject)
                {
                    return StatusResultCode.Capacity;
                }
                bool timeOnly = group.Count == rules.MaxStacks && rules.Overflow == StatusOverflow.RefreshTime;
                bool refreshTime = rules.RefreshExistingLayers || group.Count == rules.MaxStacks;
                foreach (StatusLayer layer in group)
                {
                    bool changed = (refreshTime && layer.ExpiresAt != expiry) || (!timeOnly && !layer.Effects.Matches(request.Effects));
                    if (refreshTime) { layer.ExpiresAt = expiry; }
                    if (!timeOnly) { layer.Effects = request.Effects; }
                    if (changed) { Record(state, layer, StatusChangeReason.Refreshed, now, changes); }
                }
                int accepted = Math.Min(request.Count, rules.MaxStacks - group.Count);
                for (int i = 0; i < accepted; i++)
                {
                    if (!AddLayer(state, request, now, expiry, nextTick, changes)) { return StatusResultCode.Capacity; }
                }
                return StatusResultCode.Success;
            }
            for (int i = 0; i < request.Count; i++)
            {
                group = state.Layers.Where(layer => layer.Rules.Id == rules.Id).ToList();
                if (group.Count >= rules.MaxStacks)
                {
                    if (rules.Overflow == StatusOverflow.Reject) { return StatusResultCode.Capacity; }
                    StatusLayer selected = rules.Repeat == StatusRepeatMode.Strongest
                        ? group.OrderBy(layer => layer.Effects.Strength).ThenByDescending(layer => layer.Id).First()
                        : Ordered(group).First();
                    if (rules.Repeat == StatusRepeatMode.Strongest && selected.Effects.Strength >= request.Effects.Strength)
                    {
                        continue;
                    }
                    if (rules.Overflow == StatusOverflow.RefreshTime)
                    {
                        if (request.RefreshInstanceId.HasValue) { selected = group.Single(layer => layer.Id == request.RefreshInstanceId); }
                        if (selected.ExpiresAt != expiry)
                        {
                            selected.ExpiresAt = expiry;
                            Record(state, selected, StatusChangeReason.Refreshed, now, changes);
                        }
                        continue;
                    }
                    Remove(state, new List<StatusLayer> { selected }, StatusChangeReason.Replaced, now, changes);
                }
                if (!AddLayer(state, request, now, expiry, nextTick, changes)) { return StatusResultCode.Capacity; }
            }
            return StatusResultCode.Success;
        }

        static bool AddLayer(StatusTargetState state, StatusMutation request, double now, double expiry,
            double nextTick, List<StatusChange> changes)
        {
            if (state.Layers.Count >= MaxTargetLayers || state.NextId == long.MaxValue) { return false; }
            var layer = new StatusLayer
            {
                Id = state.NextId++, Rules = request.Rules, Effects = request.Effects, Source = request.Source,
                AppliedAt = now, ExpiresAt = expiry, NextTickAt = nextTick, TickOrdinal = 1
            };
            state.Layers.Add(layer);
            Record(state, layer, StatusChangeReason.Applied, now, changes);
            return true;
        }

        internal static IOrderedEnumerable<StatusLayer> Ordered(IEnumerable<StatusLayer> layers)
        {
            return layers.OrderBy(layer => layer.ExpiresAt).ThenBy(layer => layer.AppliedAt).ThenBy(layer => layer.Id);
        }

        internal static void Remove(StatusTargetState state, List<StatusLayer> layers, StatusChangeReason reason,
            double now, List<StatusChange> changes)
        {
            foreach (StatusLayer layer in layers) { Record(state, layer, reason, now, changes); }
            foreach (StatusLayer layer in layers) { state.Layers.Remove(layer); }
        }

        static void Record(StatusTargetState state, StatusLayer layer, StatusChangeReason reason,
            double now, List<StatusChange> changes)
        {
            changes.Add(new StatusChange(reason, new StatusLayerSnapshot(layer, now, state.IsActive(layer))));
        }
    }
}
