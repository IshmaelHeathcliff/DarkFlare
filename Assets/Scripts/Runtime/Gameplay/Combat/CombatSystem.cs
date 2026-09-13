using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public class CombatSystem : AbstractSystem
    {
        protected override void OnInit()
        {
        }

        public void RegisterActor(CombatActor actor)
        {
            this.GetModel<CombatModel>().RegisterActor(actor);
            this.GetSystem<StatusSystem>().BindConfiguredActor(actor);
            this.SendEvent(new ActorRegisteredEvent(actor));
        }

        public void UnregisterActor(CombatActor actor)
        {
            this.GetSystem<StatusSystem>().Unbind(actor);
            this.GetModel<CombatModel>().UnregisterActor(actor);
            this.SendEvent(new ActorUnregisteredEvent(actor));
        }

        public DamageResult ApplyDamage(
            CombatActor attacker,
            CombatActor defender,
            string skillId,
            IEnumerable<DamagePacket> baseDamages,
            TagSet contextTags,
            int randomSeed)
        {
            AttackSnapshot attack = AttackSnapshotFactory.CreateImmediate(
                attacker,
                skillId,
                string.Empty,
                baseDamages,
                contextTags,
                randomSeed);
            return ApplyDamage(attack, defender);
        }

        public DamageResult ApplyDamage(AttackSnapshot attack, CombatActor defender)
        {
            if (attack == null)
            {
                return DamageResult.CreateWithoutDamage(HitResolutionCalculator.Invalid(
                    AttackRandomRolls.FromRootSeed(0)));
            }

            if (defender == null
                || !defender.IsAlive
                || attack.AttackerTeam == defender.Team)
            {
                return DamageResult.CreateWithoutDamage(HitResolutionCalculator.Invalid(attack.RandomRolls));
            }

            HitResolution resolution = HitResolutionCalculator.Resolve(
                attack.AttackerStats,
                defender.Stats,
                attack.RandomRolls);
            DamageContext context = new DamageContext(
                attack.AttackerId,
                defender.ActorId,
                attack.SkillId,
                attack.SourceItemId,
                attack.RandomSeed,
                attack.BaseDamages,
                attack.TagContext.WithTargetActorTags(defender.Tags),
                attack.AttackerStats,
                defender.Stats,
                attack.AttackerModifiers,
                defender.Modifiers,
                resolution);

            var source = new DamageSourceSnapshot(attack.AttackerId, attack.AttackerTeam, attack.SkillId, attack.SourceItemId, attack.TagContext);
            DamageResult result = DamageCalculator.Calculate(context, source);
            CommitDamage(defender, result, source);
            if (result.IsHit && defender != null && defender.IsAlive && attack.OnHitStatus != null)
            {
                StatusSystem statuses = this.GetSystem<StatusSystem>();
                statuses.ApplyStatus(statuses.GetTarget(defender), attack.OnHitStatus.CreateMutation());
            }
            return result;
        }

        public DamageResult ApplyPeriodicDamage(DamageSourceSnapshot source, CombatActor defender, IEnumerable<DamagePacket> damage)
        {
            if (source == null || defender == null || !defender.isActiveAndEnabled || !defender.IsAlive
                || source.Team == defender.Team || !this.GetSystem<StatusSystem>().GetTarget(defender).IsValid)
            {
                return new DamageResult(HitOutcome.InvalidTarget, false, 0, 0, 0, 0, null, DamageForm.Periodic, source);
            }
            DamageResult result = DamageCalculator.CalculatePeriodic(damage, source, defender.Stats, defender.Modifiers, defender.Tags);
            return CommitDamage(defender, result, source);
        }

        DamageResult CommitDamage(CombatActor defender, DamageResult result, DamageSourceSnapshot source)
        {
            CombatResourceSnapshot previousResources = defender.Resources;
            bool justDied = result.DidDealDamage && defender.ReceiveDamage(result);
            if (justDied) { this.GetSystem<StatusSystem>().Unbind(defender); }
            PublishResourceChanges(defender, previousResources, ActorResourceChangeReason.Damage);
            this.SendEvent(new DamageResolvedEvent { Actor = defender, Result = result });

            if (result.DidDealDamage)
            {
                ApplicationLog.Info(LogEventIds.GameplayCombat, $"[CombatSystem] {source.ActorKey} 对 {defender.ActorId} 造成 {result.TotalDamage:0.#} 点伤害 ({result.Form})");
                this.SendEvent(new ActorDamagedEvent { Actor = defender, Result = result });
            }

            if (justDied)
            {
                ApplicationLog.Info(LogEventIds.GameplayCombat, $"[CombatSystem] {defender.ActorId} 死亡");
                this.SendEvent(new ActorDiedEvent { Actor = defender, Source = source, Form = result.Form });
            }

            return result;
        }

        public float ApplyHealing(CombatActor actor, float amount)
        {
            return ApplyHealing(actor, amount, ActorResourceChangeReason.Healing, true);
        }

        public float ApplyHealthRegeneration(CombatActor actor, float amount)
        {
            return ApplyHealing(actor, amount, ActorResourceChangeReason.Regeneration, false);
        }

        public bool TrySpendMana(CombatActor actor, float amount)
        {
            if (actor == null || amount < 0f || !actor.CanSpendMana(amount))
            {
                return false;
            }

            CombatResourceSnapshot previousResources = actor.Resources;

            if (!actor.TrySpendMana(amount))
            {
                return false;
            }

            PublishResourceChanges(actor, previousResources, ActorResourceChangeReason.SkillCost);
            return true;
        }

        public float RestoreMana(
            CombatActor actor,
            float amount,
            ActorResourceChangeReason reason = ActorResourceChangeReason.Restore)
        {
            if (actor == null || !actor.IsAlive || amount <= 0f)
            {
                return 0f;
            }

            CombatResourceSnapshot previousResources = actor.Resources;
            float restoredAmount = actor.ReceiveMana(amount);

            if (restoredAmount > 0f)
            {
                PublishResourceChanges(actor, previousResources, reason);
            }

            return restoredAmount;
        }

        public void PublishResourceChanges(
            CombatActor actor,
            CombatResourceSnapshot previousResources,
            ActorResourceChangeReason reason)
        {
            if (actor == null)
            {
                return;
            }

            CombatResourceSnapshot currentResources = actor.Resources;
            PublishResourceChange(
                actor,
                ActorResourceType.Health,
                previousResources.CurrentHealth,
                currentResources.CurrentHealth,
                previousResources.MaxHealth,
                currentResources.MaxHealth,
                reason);
            PublishResourceChange(
                actor,
                ActorResourceType.Mana,
                previousResources.CurrentMana,
                currentResources.CurrentMana,
                previousResources.MaxMana,
                currentResources.MaxMana,
                reason);
        }

        float ApplyHealing(
            CombatActor actor,
            float amount,
            ActorResourceChangeReason reason,
            bool sendHealingFeedback)
        {
            if (actor == null || !actor.IsAlive || amount <= 0f)
            {
                return 0f;
            }

            CombatResourceSnapshot previousResources = actor.Resources;
            float healedAmount = actor.ReceiveHealing(amount);

            if (healedAmount <= 0f)
            {
                return 0f;
            }

            PublishResourceChanges(actor, previousResources, reason);

            if (sendHealingFeedback)
            {
                ApplicationLog.Info(LogEventIds.GameplayCombat, $"[CombatSystem] {actor.ActorId} 恢复 {healedAmount:0.#} 点生命");
                this.SendEvent(new ActorHealedEvent { Actor = actor, Amount = healedAmount });
            }

            return healedAmount;
        }

        public void Revive(CombatActor actor, Vector3 position)
        {
            CombatResourceSnapshot previousResources = actor.Resources;
            actor.Revive(position);
            this.GetSystem<StatusSystem>().BindConfiguredActor(actor);
            PublishResourceChanges(actor, previousResources, ActorResourceChangeReason.Revive);
            ApplicationLog.Info(LogEventIds.GameplayCombat, $"[CombatSystem] {actor.ActorId} 复活");
            this.SendEvent(new ActorRevivedEvent { Actor = actor });
        }

        void PublishResourceChange(
            CombatActor actor,
            ActorResourceType resourceType,
            float previousValue,
            float currentValue,
            float previousMaximum,
            float currentMaximum,
            ActorResourceChangeReason reason)
        {
            if (Mathf.Approximately(previousValue, currentValue)
                && Mathf.Approximately(previousMaximum, currentMaximum))
            {
                return;
            }

            this.SendEvent(new ActorResourceChangedEvent(
                actor,
                resourceType,
                previousValue,
                currentValue,
                previousMaximum,
                currentMaximum,
                reason));
        }

    }
}
