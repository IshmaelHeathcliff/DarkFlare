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
            this.SendEvent(new ActorRegisteredEvent(actor));
        }

        public void UnregisterActor(CombatActor actor)
        {
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
            if (attack == null || defender == null || !defender.IsAlive)
            {
                return new DamageResult(false, false, new Dictionary<DamageType, float>(), new Dictionary<DamageType, float>());
            }

            DamageContext context = new DamageContext(
                attack.AttackerId,
                defender.ActorId,
                attack.SkillId,
                attack.SourceItemId,
                attack.RandomSeed,
                attack.BaseDamages,
                attack.ContextTags,
                attack.AttackerStats,
                defender.Stats,
                attack.AttackerModifiers,
                defender.Modifiers,
                attack.IsHit,
                attack.IsCritical);

            DamageResult result = DamageCalculator.Calculate(context);
            bool justDied = defender.ReceiveDamage(result);

            if (result.IsHit)
            {
                Debug.Log($"[CombatSystem] {context.AttackerId} 对 {defender.ActorId} 造成 {result.TotalDamage:0.#} 点伤害");
            }

            this.SendEvent(new ActorDamagedEvent { Actor = defender, Result = result });

            if (justDied)
            {
                Debug.Log($"[CombatSystem] {defender.ActorId} 死亡");
                this.SendEvent(new ActorDiedEvent { Actor = defender });
            }

            return result;
        }

        public void Revive(CombatActor actor, Vector3 position)
        {
            actor.Revive(position);
            Debug.Log($"[CombatSystem] {actor.ActorId} 复活");
            this.SendEvent(new ActorRevivedEvent { Actor = actor });
        }

    }
}
