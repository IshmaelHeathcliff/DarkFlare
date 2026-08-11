using System.Collections.Generic;

namespace DarkFlare
{
    public class ApplyDamageCommand : AbstractCommand<DamageResult>
    {
        readonly CombatActor _defender;
        readonly AttackSnapshot _attack;

        public ApplyDamageCommand(AttackSnapshot attack, CombatActor defender)
        {
            _attack = attack;
            _defender = defender;
        }

        public ApplyDamageCommand(
            CombatActor attacker,
            CombatActor defender,
            string skillId,
            IEnumerable<DamagePacket> baseDamages,
            int randomSeed)
        {
            _defender = defender;
            _attack = AttackSnapshotFactory.CreateImmediate(
                attacker,
                skillId,
                string.Empty,
                baseDamages,
                randomSeed);
        }

        public ApplyDamageCommand(
            CombatActor attacker,
            CombatActor defender,
            string skillId,
            IEnumerable<DamagePacket> baseDamages,
            TagSet contextTags,
            int randomSeed)
        {
            _defender = defender;
            _attack = AttackSnapshotFactory.CreateImmediate(
                attacker,
                skillId,
                string.Empty,
                baseDamages,
                contextTags,
                randomSeed);
        }

        protected override DamageResult OnExecute()
        {
            return this.GetSystem<CombatSystem>().ApplyDamage(_attack, _defender);
        }
    }
}
