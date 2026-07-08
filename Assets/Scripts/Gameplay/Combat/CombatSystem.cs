using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public class CombatSystem : AbstractSystem
    {
        static readonly List<ModifierInstance> EmptyModifiers = new List<ModifierInstance>();

        protected override void OnInit()
        {
        }

        public void RegisterActor(CombatActor actor)
        {
            this.GetModel<CombatModel>().RegisterActor(actor);
        }

        public void UnregisterActor(CombatActor actor)
        {
            this.GetModel<CombatModel>().UnregisterActor(actor);
        }

        public DamageResult ApplyDamage(
            CombatActor attacker,
            CombatActor defender,
            string skillId,
            IEnumerable<DamagePacket> baseDamages,
            TagSet contextTags,
            int randomSeed)
        {
            if (defender == null || !defender.IsAlive)
            {
                return new DamageResult(false, false, new Dictionary<DamageType, float>(), new Dictionary<DamageType, float>());
            }

            StatBlock attackerStats = attacker != null ? attacker.Stats : new StatBlock();
            IEnumerable<ModifierInstance> attackerModifiers = attacker != null ? attacker.Modifiers : EmptyModifiers;
            DamageContext context = new DamageContext(
                attacker != null ? attacker.ActorId : "environment",
                defender.ActorId,
                skillId,
                string.Empty,
                randomSeed,
                baseDamages,
                contextTags ?? TagSet.Empty,
                attackerStats,
                defender.Stats,
                attackerModifiers,
                defender.Modifiers);

            DamageResult result = DamageCalculator.Calculate(context);
            bool justDied = defender.ReceiveDamage(result);

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
