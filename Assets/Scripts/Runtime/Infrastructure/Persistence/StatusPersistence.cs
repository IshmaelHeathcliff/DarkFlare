using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed partial class StatusStore
    {
        internal StatusSaveDto CaptureSave(ContentCatalog catalog, List<DtoMapIssue> issues)
        {
            if (!IsQuiescent) { throw new InvalidOperationException("状态尚未到达保存边界"); }
            var dto = new StatusSaveDto { Time = _time };
            foreach (var pair in _targets.OrderBy(pair => pair.Value.EventOrder))
            {
                var actor = new StatusActorDto
                {
                    ActorKey = pair.Key.ActorKey, Registration = pair.Key.Registration,
                    Order = pair.Value.EventOrder, NextId = pair.Value.NextId
                };
                foreach (StatusLayer layer in pair.Value.Layers)
                {
                    actor.Layers.Add(new StatusLayerDto
                    {
                        Id = layer.Id, Rules = StatusEffectMapper.Capture(layer.Rules),
                        Effects = StatusEffectMapper.Capture(layer.Effects, catalog, issues),
                        SourceKind = layer.Source.Kind, SourceKey = layer.Source.Key,
                        SourceActorKey = layer.Source.Actor.ActorKey, SourceRegistration = layer.Source.Actor.Registration,
                        AppliedAt = layer.AppliedAt, ExpiresAt = FiniteOrNull(layer.ExpiresAt),
                        NextTickAt = FiniteOrNull(layer.NextTickAt), TickOrdinal = layer.TickOrdinal
                    });
                }
                dto.Actors.Add(actor);
            }
            return dto;
        }

        internal void ImportSave(StatusSaveDto dto, ContentCatalog catalog, List<DtoMapIssue> issues)
        {
            if (!IsQuiescent) { throw new InvalidOperationException("状态尚未到达恢复边界"); }
            var candidates = new Dictionary<StatusTargetId, StatusTargetState>();
            var detached = new Dictionary<(string, long), StatusTargetId>();
            foreach (StatusActorDto actor in dto.Actors)
            {
                if (!_registrations.TryGetValue(actor.ActorKey, out StatusTargetId target))
                {
                    throw new ArgumentException("状态目标尚未建立：" + actor.ActorKey);
                }
                var state = new StatusTargetState { EventOrder = actor.Order, NextId = actor.NextId, Version = 1 };
                foreach (StatusLayerDto layer in actor.Layers)
                {
                    StatusTargetId sourceActor = default;
                    if (!string.IsNullOrEmpty(layer.SourceActorKey))
                    {
                        StatusActorDto live = dto.Actors.FirstOrDefault(value => value.ActorKey == layer.SourceActorKey
                            && value.Registration == layer.SourceRegistration);
                        if (live != null) { sourceActor = _registrations[live.ActorKey]; }
                        else
                        {
                            var key = (layer.SourceActorKey, layer.SourceRegistration);
                            if (!detached.TryGetValue(key, out sourceActor))
                            {
                                sourceActor = new StatusTargetId(Generation, _nextRegistration++, layer.SourceActorKey);
                                detached.Add(key, sourceActor);
                            }
                        }
                    }
                    state.Layers.Add(new StatusLayer
                    {
                        Id = layer.Id, Rules = StatusEffectMapper.Restore(layer.Rules),
                        Effects = StatusEffectMapper.Restore(layer.Effects, catalog, issues),
                        Source = new StatusSource(layer.SourceKind, layer.SourceKey, sourceActor),
                        AppliedAt = layer.AppliedAt, ExpiresAt = layer.ExpiresAt ?? double.PositiveInfinity,
                        NextTickAt = layer.NextTickAt ?? double.PositiveInfinity, TickOrdinal = layer.TickOrdinal
                    });
                }
                candidates.Add(target, state);
            }
            if (issues.Count > 0) { throw new ArgumentException("状态效果恢复失败"); }
            var projections = new List<IStatusProjection>();
            foreach (var pair in candidates)
            {
                if (_participants.TryGetValue(pair.Key, out IStatusParticipant participant))
                {
                    projections.Add(participant.Prepare(new StatusTargetSnapshot(pair.Key, pair.Value, dto.Time)));
                }
            }
            foreach (IStatusProjection projection in projections)
            {
                if (!projection.IsCurrent) { throw new ArgumentException("状态恢复投影过期"); }
            }
            _time = dto.Time;
            _pendingUntil = _time;
            foreach (var pair in candidates) { _targets[pair.Key] = pair.Value; }
            _nextEventOrder = Math.Max(_nextEventOrder, dto.Actors.Select(actor => actor.Order).DefaultIfEmpty(0).Max() + 1);
            // 恢复期间不发送 Apply / Tick 事件，由 Session 完整恢复事件统一通知。
            foreach (IStatusProjection projection in projections) { projection.Apply(); }
        }

        static double? FiniteOrNull(double value) { return double.IsPositiveInfinity(value) ? null : value; }
    }

    internal static class StatusSaveValidation
    {
        internal const int MaximumLayers = 16384;
        static readonly IStatusSourceRestoreProvider[] Providers = { new EquipmentStatusSourceRestoreProvider() };

        internal static void Validate(StatusSaveDto dto, SavePayloadDto payload, ContentCatalog catalog, List<DtoMapIssue> issues,
            IReadOnlyList<IStatusSourceRestoreProvider> providers = null)
        {
            providers ??= Providers;
            try
            {
                if (dto == null || dto.Actors == null || dto.Actors.Count > LocalSaveFormat.MaximumMonsters + 1
                    || !StatusRules.IsFinite(dto.Time) || dto.Time < 0 || dto.Time > StatusRules.MaximumTime)
                {
                    throw new ArgumentException("状态时钟或目标容量非法");
                }
                var keys = new HashSet<string>(StringComparer.Ordinal);
                var orders = new HashSet<long>();
                var registrations = new HashSet<long>();
                int count = 0;
                foreach (StatusActorDto actor in dto.Actors)
                {
                    if (actor == null || !keys.Add(actor.ActorKey) || actor.Order <= 0 || actor.Order == long.MaxValue
                        || !orders.Add(actor.Order) || actor.Registration <= 0 || !registrations.Add(actor.Registration)
                        || actor.NextId <= 0 || actor.Layers == null || actor.Layers.Count > 4096
                        || (count += actor.Layers.Count) > MaximumLayers)
                    {
                        throw new ArgumentException("状态目标、身份或层容量非法");
                    }
                    bool player = actor.ActorKey == "player:" + payload.Profile.PlayerId;
                    if (player ? !payload.Run.Player.Resources.IsAlive : !payload.Run.Monsters.Any(monster => monster.InstanceId == actor.ActorKey))
                    {
                        throw new ArgumentException("状态目标不在存活角色图中");
                    }
                    var ids = new HashSet<long>();
                    var groups = new Dictionary<string, List<StatusLayerDto>>();
                    foreach (StatusLayerDto layer in actor.Layers)
                    {
                        if (layer == null || layer.Id <= 0 || layer.Id >= actor.NextId || !ids.Add(layer.Id)
                            || !Enum.IsDefined(typeof(StatusSourceKind), layer.SourceKind) || string.IsNullOrWhiteSpace(layer.SourceKey)
                            || layer.SourceRegistration < 0 || (string.IsNullOrEmpty(layer.SourceActorKey) != (layer.SourceRegistration == 0)))
                        {
                            throw new ArgumentException("状态层身份或来源非法");
                        }
                        StatusRules rules = StatusEffectMapper.Restore(layer.Rules);
                        if (catalog != null)
                        {
                            RuntimeStateMapper.Resolve<StatusDefinition>(rules.Id.ToString(), catalog, "statuses.definition", issues);
                            StatusEffectSnapshot effects = StatusEffectMapper.Restore(layer.Effects, catalog, issues);
                            AilmentResistanceResolver.Validate(rules, effects);
                            if ((effects.PeriodicDamage.Count > 0 && (effects.DamageStage != StatusDamageStage.SourceResolved
                                    || effects.DamageSource == null || rules.Interval <= 0))
                                || (effects.Resistance != null && effects.Resistance.Ailment != rules.Ailment))
                            {
                                throw new ArgumentException("状态效果快照非法");
                            }
                        }
                        ValidateTime(dto.Time, layer, rules);
                        if (!groups.TryGetValue(rules.Id.LocalId, out List<StatusLayerDto> group))
                        {
                            group = new List<StatusLayerDto>(); groups.Add(rules.Id.LocalId, group);
                        }
                        if (group.Count > 0 && (!rules.Matches(StatusEffectMapper.Restore(group[0].Rules))
                            || (rules.Clock == StatusClockMode.Shared && layer.ExpiresAt != group[0].ExpiresAt)
                            || (rules.Repeat == StatusRepeatMode.Uniform && !Newtonsoft.Json.Linq.JToken.DeepEquals(
                                Newtonsoft.Json.Linq.JToken.FromObject(layer.Effects), Newtonsoft.Json.Linq.JToken.FromObject(group[0].Effects)))))
                        {
                            throw new ArgumentException("同组状态规则或共享时钟冲突");
                        }
                        if (rules.Lifetime == StatusLifetime.SourceOwned)
                        {
                            IStatusSourceRestoreProvider provider = providers.FirstOrDefault(value => value.Kind == layer.SourceKind);
                            if (provider == null) { throw new ArgumentException("来源维持状态缺少可恢复的提供方"); }
                            provider.Validate(actor, layer, payload, catalog, issues);
                        }
                        group.Add(layer);
                        if (group.Count > rules.MaxStacks) { throw new ArgumentException("状态超出层数上限"); }
                    }
                }
            }
            catch (ArgumentException exception)
            {
                issues.Add(new DtoMapIssue(DtoMapIssueCode.InvalidValue, "payload.run.statuses", exception.Message));
            }
        }

        static void ValidateTime(double time, StatusLayerDto layer, StatusRules rules)
        {
            if (!StatusRules.IsFinite(layer.AppliedAt) || layer.AppliedAt < 0 || layer.AppliedAt > time
                || (rules.Lifetime == StatusLifetime.Timed) != layer.ExpiresAt.HasValue
                || (layer.ExpiresAt.HasValue && (!StatusRules.IsFinite(layer.ExpiresAt.Value) || layer.ExpiresAt <= time
                    || layer.ExpiresAt > StatusRules.MaximumTime))
                || (rules.Interval > 0) != layer.NextTickAt.HasValue || layer.TickOrdinal <= 0
                || (layer.NextTickAt.HasValue && (!StatusRules.IsFinite(layer.NextTickAt.Value) || layer.NextTickAt <= time
                    || Math.Abs(layer.NextTickAt.Value - (layer.AppliedAt + layer.TickOrdinal * rules.Interval)) > 0.00000001)))
            {
                throw new ArgumentException("状态剩余时间或周期相位非法");
            }
        }

    }
}
