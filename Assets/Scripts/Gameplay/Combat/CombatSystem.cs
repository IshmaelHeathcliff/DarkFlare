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

        public void EquipWeapon(CombatActor actor, ItemInstance weapon)
        {
            if (actor == null)
            {
                return;
            }

            this.GetModel<EquipmentModel>().SetWeapon(actor, weapon);

            if (weapon != null)
            {
                // 若武器来自背包，穿戴时从背包移出，保持"已穿戴的物品不占背包格子"不变量
                this.GetModel<InventoryModel>().RemoveItem(weapon);
            }

            actor.SetModifiers(weapon != null ? weapon.CollectModifiers() : EmptyModifiers);
            Debug.Log($"[CombatSystem] {actor.ActorId} 装备了 {(weapon != null ? weapon.BaseDefinition.DisplayName : "无")}");
        }
    }
}
