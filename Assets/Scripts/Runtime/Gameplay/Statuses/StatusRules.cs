using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum StatusCategory
    {
        [LabelText("光环")] Aura,
        [LabelText("特殊技能")] Skill,
        [LabelText("异常")] Ailment
    }

    public enum StatusRepeatMode
    {
        [LabelText("统一叠层并覆盖刷新")] Uniform,
        [LabelText("共存，仅最强生效")] Strongest,
        [LabelText("共存，各层独立生效")] Independent
    }

    public enum StatusClockMode
    {
        [LabelText("逐层计时")] PerLayer,
        [LabelText("共享计时")] Shared
    }

    public enum StatusLifetime
    {
        [LabelText("限时")] Timed,
        [LabelText("来源维持")] SourceOwned,
        [LabelText("无限")] Infinite
    }

    public enum StatusOverflow
    {
        [LabelText("按重复模型默认处理")] Default,
        [LabelText("拒绝新增或刷新")] Reject,
        [LabelText("仅刷新时间")] RefreshTime
    }

    [Flags]
    public enum StatusActionBlock
    {
        [LabelText("无")] None = 0,
        [LabelText("移动")] Move = 1,
        [LabelText("攻击")] Attack = 2,
        [LabelText("施法")] Cast = 4,
        [LabelText("使用物品")] UseItem = 8
    }

    public sealed class StatusRules
    {
        public const double MaximumTime = 1000000000;
        public const double MinimumInterval = 0.000001;
        public ContentId Id { get; }
        public StatusRepeatMode Repeat { get; }
        public StatusClockMode Clock { get; }
        public StatusLifetime Lifetime { get; }
        public StatusOverflow Overflow { get; }
        public StatusCategory Category { get; }
        public int MaxStacks { get; }
        public double Duration { get; }
        public double Interval { get; }
        public bool CanDispel { get; }
        public bool CanConsume { get; }
        public bool RefreshExistingLayers { get; }
        public TagSet Tags { get; }
        public AilmentKind Ailment { get; }

        public StatusRules(string id, StatusRepeatMode repeat = StatusRepeatMode.Uniform,
            int maxStacks = 1, double duration = 5, double interval = 0,
            StatusLifetime lifetime = StatusLifetime.Timed, StatusClockMode clock = StatusClockMode.PerLayer,
            StatusOverflow overflow = StatusOverflow.Default, StatusCategory category = StatusCategory.Skill,
            bool canDispel = true, bool canConsume = true, TagSet tags = null, bool refreshExistingLayers = true,
            AilmentKind ailment = AilmentKind.None)
        {
            Id = new ContentId(ContentNamespaces.Status, id);
            if (!Enum.IsDefined(typeof(AilmentKind), ailment)
                || (ailment != AilmentKind.None && (category != StatusCategory.Ailment || lifetime != StatusLifetime.Timed)))
            {
                throw new ArgumentException("异常必须使用异常分类和限时生命周期");
            }
            Ailment = ailment;
            if (!Enum.IsDefined(typeof(StatusRepeatMode), repeat)
                || !Enum.IsDefined(typeof(StatusClockMode), clock)
                || !Enum.IsDefined(typeof(StatusLifetime), lifetime)
                || !Enum.IsDefined(typeof(StatusOverflow), overflow)
                || !Enum.IsDefined(typeof(StatusCategory), category)
                || maxStacks <= 0 || maxStacks > 4096
                || !IsFinite(duration) || duration < 0 || duration > MaximumTime || (lifetime == StatusLifetime.Timed && duration <= 0)
                || !IsFinite(interval) || interval < 0 || interval > MaximumTime || (interval > 0 && interval < MinimumInterval)
                || (clock == StatusClockMode.Shared && repeat != StatusRepeatMode.Uniform)
                || (!refreshExistingLayers && (repeat != StatusRepeatMode.Uniform || clock != StatusClockMode.PerLayer))
                || (repeat == StatusRepeatMode.Strongest && overflow == StatusOverflow.RefreshTime))
            {
                throw new ArgumentException("非法状态规则：检查枚举、层数、时长、周期及组合");
            }
            Repeat = repeat;
            MaxStacks = maxStacks;
            Duration = duration;
            Interval = interval;
            Lifetime = lifetime;
            Clock = clock;
            Overflow = overflow;
            Category = category;
            CanDispel = canDispel;
            CanConsume = canConsume;
            RefreshExistingLayers = refreshExistingLayers;
            Tags = tags ?? TagSet.Empty;
            foreach (string tag in Tags.Ids)
            {
                if (!ContentId.IsValidSegment(tag)) { throw new ArgumentException("状态标签 ID 非法"); }
            }
        }

        internal bool Matches(StatusRules other)
        {
            return other != null && Id == other.Id && Ailment == other.Ailment && Repeat == other.Repeat && Clock == other.Clock
                && Lifetime == other.Lifetime && Overflow == other.Overflow && Category == other.Category
                && MaxStacks == other.MaxStacks && Duration == other.Duration && Interval == other.Interval
                && CanDispel == other.CanDispel && CanConsume == other.CanConsume && RefreshExistingLayers == other.RefreshExistingLayers
                && Tags.ContainsAll(other.Tags) && other.Tags.ContainsAll(Tags);
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public enum StatusDamageStage { Base, SourceResolved }

    // 显式区分基础值与已结算来源增伤的每跳数值；两者均未经过目标防御。
    public sealed class StatusEffectSnapshot
    {
        public static StatusEffectSnapshot Marker { get; } = new StatusEffectSnapshot();
        public double Strength { get; }
        public IReadOnlyList<ModifierInstance> Modifiers { get; }
        public IReadOnlyList<DamagePacket> PeriodicDamage { get; }
        public StatusActionBlock BlockedActions { get; }
        public StatusDamageStage DamageStage { get; }
        public DamageSourceSnapshot DamageSource { get; }
        public AilmentResistanceSnapshot Resistance { get; }

        public StatusEffectSnapshot(double strength = 0, IEnumerable<ModifierInstance> modifiers = null,
            IEnumerable<DamagePacket> periodicDamage = null, StatusActionBlock blockedActions = StatusActionBlock.None,
            StatusDamageStage damageStage = StatusDamageStage.SourceResolved, DamageSourceSnapshot damageSource = null,
            AilmentResistanceSnapshot resistance = null)
        {
            if (!StatusRules.IsFinite(strength) || strength < 0 || ((int)blockedActions & ~15) != 0
                || !Enum.IsDefined(typeof(StatusDamageStage), damageStage))
            {
                throw new ArgumentException("状态强度或行动限制非法");
            }
            var modifierList = new List<ModifierInstance>(modifiers ?? Array.Empty<ModifierInstance>());
            foreach (ModifierInstance modifier in modifierList)
            {
                if (modifier == null
                    || (modifier.Operation != ModifierOperation.Conversion && modifier.Operation != ModifierOperation.GainAsExtra
                        && string.IsNullOrWhiteSpace(modifier.StatId))
                    || !StatusRules.IsFinite(modifier.Value)
                    || (modifier.Scope != ModifierScope.GlobalActor && modifier.Scope != ModifierScope.Skill
                        && modifier.Scope != ModifierScope.TargetTaken)
                    || !Enum.IsDefined(typeof(ModifierOperation), modifier.Operation)
                    || modifier.Operation == ModifierOperation.Chance || modifier.Operation == ModifierOperation.Trigger
                    || modifier.Operation == ModifierOperation.Limit
                    || !Enum.IsDefined(typeof(DamageType), modifier.FromDamageType)
                    || !Enum.IsDefined(typeof(DamageType), modifier.ToDamageType))
                {
                    throw new ArgumentException("状态修改器为空、数值非法或作用域不受支持");
                }
                if (CombatStatResolver.CanAggregate(modifier) && modifier.Query.HasConditions
                    && !modifier.UsesLegacyTagMatching && modifier.Query.ScopeMask != CombatTagScope.SourceActor)
                {
                    throw new ArgumentException("角色属性状态条件仅支持来源角色标签；技能、目标和伤害条件应配置到伤害作用域");
                }
            }
            var packets = new List<DamagePacket>(periodicDamage ?? Array.Empty<DamagePacket>());
            foreach (DamagePacket packet in packets)
            {
                if (!StatusRules.IsFinite(packet.Amount) || packet.Amount < 0
                    || !Enum.IsDefined(typeof(DamageType), packet.CurrentType)
                    || ((int)packet.ScalingTypes & ~31) != 0)
                {
                    throw new ArgumentException("状态周期伤害非法");
                }
            }
            Strength = strength;
            Modifiers = modifierList.AsReadOnly();
            PeriodicDamage = packets.AsReadOnly();
            BlockedActions = blockedActions;
            DamageStage = damageStage;
            DamageSource = damageSource;
            Resistance = resistance;
        }

        internal bool Matches(StatusEffectSnapshot other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            if (other == null || Strength != other.Strength || BlockedActions != other.BlockedActions
                || DamageStage != other.DamageStage || DamageSource != other.DamageSource || Modifiers.Count != other.Modifiers.Count
                || PeriodicDamage.Count != other.PeriodicDamage.Count) { return false; }
            for (int i = 0; i < Modifiers.Count; i++)
            {
                ModifierInstance first = Modifiers[i];
                ModifierInstance second = other.Modifiers[i];
                if (first.StatId != second.StatId || first.Operation != second.Operation || first.Scope != second.Scope
                    || first.Value != second.Value || first.FromDamageType != second.FromDamageType
                    || first.ToDamageType != second.ToDamageType || first.UsesLegacyTagMatching != second.UsesLegacyTagMatching
                    || !first.Origin.Equals(second.Origin) || first.Query.ScopeMask != second.Query.ScopeMask
                    || !SameTags(first.Query.RequiredAll, second.Query.RequiredAll)
                    || !SameTags(first.Query.RequiredAny, second.Query.RequiredAny)
                    || !SameTags(first.Query.BlockedAny, second.Query.BlockedAny)) { return false; }
            }
            for (int i = 0; i < PeriodicDamage.Count; i++)
            {
                DamagePacket first = PeriodicDamage[i];
                DamagePacket second = other.PeriodicDamage[i];
                if (first.CurrentType != second.CurrentType || first.Amount != second.Amount
                    || first.ScalingTypes != second.ScalingTypes || !SameTags(first.CustomTags, second.CustomTags)) { return false; }
            }
            return true;
        }

        static bool SameTags(TagSet first, TagSet second)
        {
            first ??= TagSet.Empty;
            second ??= TagSet.Empty;
            return first.ContainsAll(second) && second.ContainsAll(first);
        }
    }
}
