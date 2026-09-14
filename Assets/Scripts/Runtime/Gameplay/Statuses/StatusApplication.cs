using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class GetStatusClockDebugQuery : AbstractQuery<string>
    {
        protected override string OnDo()
        {
            StatusSystem system = this.GetSystem<StatusSystem>();
            string mode = system.IsClockRunning ? (GameTimeService.Shared.IsPaused ? "自动（暂停，可手动）" : "自动") : "手动";
            return $"{mode} / 时间 {system.Store.Time:0.###} / 积压 {system.Store.HasPendingTime} / 待提交 {system.PendingElapsed:0.###} 秒";
        }
    }

    public sealed class GetActorStatusQuery : AbstractQuery<StatusTargetSnapshot>
    {
        readonly CombatActor _actor;
        public GetActorStatusQuery(CombatActor actor) { _actor = actor; }
        protected override StatusTargetSnapshot OnDo()
        {
            StatusSystem system = this.GetSystem<StatusSystem>();
            return system.GetStatusSnapshot(system.GetTarget(_actor));
        }
    }

    public sealed class AdvanceStatusesCommand : AbstractCommand<StatusAdvanceResult>
    {
        readonly double _seconds;
        public AdvanceStatusesCommand(double seconds) { _seconds = seconds; }
        protected override StatusAdvanceResult OnExecute() { return this.GetSystem<StatusSystem>().AdvanceManually(_seconds); }
    }

    // 发出攻击时冻结规则和来源效果；目标抗性留到实际施加时解析。
    public sealed class StatusApplication
    {
        public StatusRules Rules { get; }
        public StatusEffectSnapshot Effects { get; }
        public StatusSource Source { get; }

        public StatusApplication(StatusRules rules, StatusEffectSnapshot effects, StatusSource source)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            Source = source;
            AilmentResistanceResolver.Validate(rules, effects);
        }

        public static StatusApplication Capture(StatusDefinition definition, StatusSource source, StatBlock stats,
            IEnumerable<ModifierInstance> modifiers, DamageSourceSnapshot damageSource, int seed)
        {
            StatusRules rules = definition.CreateRules();
            StatusEffectSnapshot effects = definition.CreateEffects(new Random(seed));
            IReadOnlyList<DamagePacket> damage = effects.PeriodicDamage.Count > 0
                ? DamageCalculator.ResolveSource(effects.PeriodicDamage, stats ?? new StatBlock(),
                    modifiers ?? Array.Empty<ModifierInstance>(), damageSource.Tags) : effects.PeriodicDamage;
            return new StatusApplication(rules, new StatusEffectSnapshot(effects.Strength, effects.Modifiers, damage,
                effects.BlockedActions, StatusDamageStage.SourceResolved, damageSource), source);
        }

        public StatusMutation CreateMutation()
        {
            return StatusMutation.Apply(Rules, Source, Effects);
        }
    }

    public sealed class UseStatusSkillCommand : AbstractCommand
    {
        readonly CombatActor _source;
        readonly CombatActor _target;
        readonly StatusApplication _application;
        readonly bool _dispel;
        public StatusOperation Operation { get; private set; }

        public UseStatusSkillCommand(CombatActor source, CombatActor target, StatusApplication application = null, bool dispel = false)
        {
            _source = source; _target = target; _application = application; _dispel = dispel;
        }

        protected override void OnExecute()
        {
            StatusSystem statuses = this.GetSystem<StatusSystem>();
            if (!this.SendQuery(new GetActorActionsQuery(_source)).CanCast || !statuses.GetTarget(_target).IsValid)
            {
                Operation = new StatusOperation { Result = new StatusResult(statuses.GetTarget(_target).IsValid
                    ? StatusResultCode.Protected : StatusResultCode.InvalidTarget, statuses.GetStatusSnapshot(statuses.GetTarget(_target))) };
                return;
            }
            if (_dispel) { Operation = statuses.DispelStatuses(statuses.GetTarget(_target), new StatusFilter(category: StatusCategory.Ailment)); }
            else if (_application != null)
            {
                Operation = statuses.Store.Execute(statuses.GetTarget(_target), new[] { _application.CreateMutation() });
            }
            else { Operation = new StatusOperation { Result = new StatusResult(StatusResultCode.InvalidRequest, statuses.GetStatusSnapshot(statuses.GetTarget(_target))) }; }
        }
    }

    public sealed class ConsumeStatusForEffectCommand : AbstractCommand
    {
        readonly StatusTargetId _target;
        readonly string _marker;
        readonly int _count;
        readonly StatusApplication _effect;
        readonly long _version;
        public StatusOperation Operation { get; private set; }

        public ConsumeStatusForEffectCommand(StatusTargetId target, string marker, int count, StatusApplication effect, long version)
        {
            _target = target; _marker = marker; _count = count; _effect = effect; _version = version;
        }

        protected override void OnExecute()
        {
            Operation = this.GetSystem<StatusSystem>().Store.Execute(_target,
                new[] { StatusMutation.Consume(_marker, _count), _effect.CreateMutation() }, _version);
        }
    }
}
