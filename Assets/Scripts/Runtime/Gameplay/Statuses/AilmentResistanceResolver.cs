using System;
using System.Linq;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum AilmentKind
    {
        [LabelText("无")] None,
        [LabelText("虚弱")] Weakness,
        [LabelText("眩晕")] Stun,
        [LabelText("流血")] Bleeding,
        [LabelText("燃烧")] Burning,
        [LabelText("冰缓")] Chill,
        [LabelText("感电")] Shock,
        [LabelText("中毒")] Poison
    }

    public sealed class AilmentResistanceSnapshot
    {
        public AilmentKind Ailment { get; }
        public float RawResistance { get; }
        public float EffectiveResistance { get; }
        public StatusEffectSnapshot Before { get; }
        public double OriginalDuration { get; }

        internal AilmentResistanceSnapshot(AilmentKind ailment, float raw, StatusEffectSnapshot before, double duration)
        {
            Ailment = ailment;
            RawResistance = raw;
            EffectiveResistance = Math.Clamp(raw, 0, 100);
            Before = before;
            OriginalDuration = duration;
        }
    }

    internal sealed class AilmentResistedException : ArgumentException { }

    public static class AilmentResistanceResolver
    {
        public static string StatId(AilmentKind ailment)
        {
            return ailment switch
            {
                AilmentKind.Weakness => StatIds.WeaknessResistance,
                AilmentKind.Stun => StatIds.StunResistance,
                AilmentKind.Bleeding => StatIds.BleedingResistance,
                AilmentKind.Burning => StatIds.BurningResistance,
                AilmentKind.Chill => StatIds.ChillResistance,
                AilmentKind.Shock => StatIds.ShockResistance,
                AilmentKind.Poison => StatIds.PoisonResistance,
                _ => null
            };
        }

        public static void Validate(StatusRules rules, StatusEffectSnapshot effects)
        {
            if (rules.Ailment == AilmentKind.None) { return; }
            bool periodic = rules.Ailment == AilmentKind.Bleeding || rules.Ailment == AilmentKind.Burning || rules.Ailment == AilmentKind.Poison;
            bool stun = rules.Ailment == AilmentKind.Stun;
            if (periodic)
            {
                if (effects.PeriodicDamage.Count == 0 || rules.Interval <= 0 || effects.Modifiers.Count != 0
                    || effects.BlockedActions != StatusActionBlock.None)
                {
                    throw new ArgumentException("伤害异常必须仅包含周期伤害");
                }
            }
            else if (stun)
            {
                if (effects.BlockedActions != (StatusActionBlock.Move | StatusActionBlock.Attack | StatusActionBlock.Cast)
                    || effects.Modifiers.Count != 0 || effects.PeriodicDamage.Count != 0)
                {
                    throw new ArgumentException("眩晕必须仅禁止移动、攻击和施法");
                }
            }
            else
            {
                string stat = rules.Ailment == AilmentKind.Chill ? StatIds.MoveSpeed : StatIds.Damage;
                ModifierScope scope = rules.Ailment == AilmentKind.Shock ? ModifierScope.TargetTaken : ModifierScope.GlobalActor;
                if (effects.Modifiers.Count != 1 || effects.PeriodicDamage.Count != 0 || effects.BlockedActions != StatusActionBlock.None
                    || effects.Modifiers.Any(modifier => modifier.StatId != stat || modifier.Scope != scope
                        || modifier.Operation != ModifierOperation.More
                        || (rules.Ailment == AilmentKind.Shock ? modifier.Value <= 0 : modifier.Value >= 0 || modifier.Value < -100)))
                {
                    throw new ArgumentException("属性异常必须配置对应属性和作用域的单一 More / Less 幅度");
                }
            }
        }

        internal static StatusMutation Normalize(StatusMutation mutation, StatBlock targetStats)
        {
            AilmentKind ailment = mutation.Rules.Ailment;
            if (ailment == AilmentKind.None) { return mutation; }
            StatusEffectSnapshot effects = mutation.Effects;
            double duration = mutation.Duration ?? mutation.Rules.Duration;
            if (effects.Resistance != null)
            {
                if (effects.Resistance.Ailment != ailment) { throw new ArgumentException("异常抵抗快照类型冲突"); }
                // 查询得到的快照再次施加时回到原参数，既不重复缩放，也不绕过新目标抗性。
                duration = effects.Resistance.OriginalDuration;
                effects = effects.Resistance.Before;
            }
            Validate(mutation.Rules, effects);
            float raw = targetStats.GetValue(StatId(ailment));
            if (!StatusRules.IsFinite(raw)) { throw new ArgumentException("异常抗性必须为有限值"); }
            var snapshot = new AilmentResistanceSnapshot(ailment, raw, effects, duration);
            float scale = 1 - snapshot.EffectiveResistance / 100;
            if (scale <= 0) { throw new AilmentResistedException(); }
            var modifiers = effects.Modifiers.Select(modifier => modifier.WithValue(modifier.Value * scale)).ToArray();
            var damage = effects.PeriodicDamage.Select(packet => packet.WithAmount(packet.Amount * scale)).ToArray();
            double strength = damage.Length > 0 ? damage.Sum(packet => (double)packet.Amount) / mutation.Rules.Interval
                : modifiers.Length > 0 ? Math.Abs(modifiers[0].Value) : effects.Strength;
            return mutation.WithResistance(new StatusEffectSnapshot(strength, modifiers, damage, effects.BlockedActions,
                effects.DamageStage, effects.DamageSource, snapshot), ailment == AilmentKind.Stun ? duration * scale : mutation.Duration);
        }
    }
}
