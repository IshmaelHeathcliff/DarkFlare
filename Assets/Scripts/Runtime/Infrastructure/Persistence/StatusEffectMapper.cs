using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    internal static class StatusEffectMapper
    {
        internal static StatusRulesDto Capture(StatusRules value)
        {
            return new StatusRulesDto
            {
                Id = value.Id.LocalId, Repeat = value.Repeat, Clock = value.Clock, Lifetime = value.Lifetime,
                Overflow = value.Overflow, Category = value.Category, Ailment = value.Ailment,
                MaxStacks = value.MaxStacks, Duration = value.Duration, Interval = value.Interval,
                CanDispel = value.CanDispel, CanConsume = value.CanConsume,
                RefreshExistingLayers = value.RefreshExistingLayers, Tags = value.Tags.Ids.ToList()
            };
        }

        internal static StatusRules Restore(StatusRulesDto value)
        {
            if (value == null) { throw new ArgumentException("缺少状态规则"); }
            return new StatusRules(value.Id, value.Repeat, value.MaxStacks, value.Duration, value.Interval,
                value.Lifetime, value.Clock, value.Overflow, value.Category, value.CanDispel, value.CanConsume,
                Tags(value.Tags), value.RefreshExistingLayers, value.Ailment);
        }

        internal static StatusEffectDto Capture(StatusEffectSnapshot value, ContentCatalog catalog, List<DtoMapIssue> issues)
        {
            DamageSourceSnapshot source = value.DamageSource;
            return new StatusEffectDto
            {
                Strength = value.Strength, BlockedActions = value.BlockedActions, DamageStage = value.DamageStage,
                Modifiers = RuntimeStateMapper.MapModifiersToDto(value.Modifiers, catalog, "statuses.effects", issues),
                Damage = value.PeriodicDamage.Select(packet => new StatusDamageDto
                {
                    Type = packet.CurrentType, ScalingTypes = packet.ScalingTypes, Amount = packet.Amount, Tags = packet.CustomTags.Ids.ToList()
                }).ToList(),
                DamageSource = source == null ? null : new StatusDamageSourceDto
                {
                    ActorKey = source.ActorKey, Team = source.Team, SkillId = source.SkillId, ItemId = source.ItemId,
                    Tags = new[] { source.Tags.SourceActorTags, source.Tags.TargetActorTags, source.Tags.SkillTags,
                        source.Tags.SourceItemTags, source.Tags.AttackTags, source.Tags.DamageTags, source.Tags.LegacyTags }
                        .Select(tags => tags.Ids.ToList()).ToList()
                },
                Resistance = value.Resistance == null ? null : new StatusResistanceDto
                {
                    Ailment = value.Resistance.Ailment, Raw = value.Resistance.RawResistance,
                    OriginalDuration = value.Resistance.OriginalDuration, Before = Capture(value.Resistance.Before, catalog, issues)
                }
            };
        }

        internal static StatusEffectSnapshot Restore(StatusEffectDto value, ContentCatalog catalog, List<DtoMapIssue> issues, bool before = false)
        {
            if (value == null || value.Modifiers == null || value.Damage == null
                || value.Modifiers.Count > 256 || value.Damage.Count > 256 || (before && value.Resistance != null))
            {
                throw new ArgumentException("状态效果数量或抵抗快照非法");
            }
            DamageSourceSnapshot source = null;
            if (value.DamageSource != null)
            {
                StatusDamageSourceDto dto = value.DamageSource;
                if (dto.Tags == null || dto.Tags.Count != 7) { throw new ArgumentException("伤害来源标签非法"); }
                TagSet[] tags = dto.Tags.Select(Tags).ToArray();
                source = new DamageSourceSnapshot(dto.ActorKey, dto.Team, dto.SkillId, dto.ItemId,
                    new CombatTagContext(tags[0], tags[1], tags[2], tags[3], tags[4], tags[5], tags[6]));
            }
            AilmentResistanceSnapshot resistance = null;
            if (value.Resistance != null)
            {
                StatusResistanceDto dto = value.Resistance;
                if (!Enum.IsDefined(typeof(AilmentKind), dto.Ailment) || dto.Ailment == AilmentKind.None
                    || !StatusRules.IsFinite(dto.Raw) || dto.Raw >= 100 || !StatusRules.IsFinite(dto.OriginalDuration)
                    || dto.OriginalDuration <= 0 || dto.OriginalDuration > StatusRules.MaximumTime)
                {
                    throw new ArgumentException("异常抵抗记录非法");
                }
                resistance = new AilmentResistanceSnapshot(dto.Ailment, dto.Raw, Restore(dto.Before, catalog, issues, true), dto.OriginalDuration);
            }
            if (value.Damage.Any(packet => packet == null)) { throw new ArgumentException("周期伤害记录为空"); }
            return new StatusEffectSnapshot(value.Strength,
                RuntimeStateMapper.RestoreModifiers(value.Modifiers, catalog, "statuses.effects", issues),
                value.Damage.Select(packet => new DamagePacket(packet.Type, packet.Amount, packet.ScalingTypes, Tags(packet.Tags))),
                value.BlockedActions, value.DamageStage, source, resistance);
        }

        static TagSet Tags(List<string> ids)
        {
            if (ids == null || ids.Count > 256 || ids.Any(id => !ContentId.IsValidSegment(id)))
            {
                throw new ArgumentException("状态标签非法");
            }
            return new TagSet(ids);
        }
    }
}
