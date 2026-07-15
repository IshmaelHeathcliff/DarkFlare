using System.Collections.Generic;

namespace DarkFlare
{
    public class ApplyDamageCommand : AbstractCommand<DamageResult>
    {
        readonly CombatActor _attacker;
        readonly CombatActor _defender;
        readonly string _skillId;
        readonly IEnumerable<DamagePacket> _baseDamages;
        readonly TagSet _contextTags;
        readonly int _randomSeed;

        public ApplyDamageCommand(
            CombatActor attacker,
            CombatActor defender,
            string skillId,
            IEnumerable<DamagePacket> baseDamages,
            TagSet contextTags,
            int randomSeed)
        {
            _attacker = attacker;
            _defender = defender;
            _skillId = skillId;
            _baseDamages = baseDamages;
            _contextTags = contextTags;
            _randomSeed = randomSeed;
        }

        protected override DamageResult OnExecute()
        {
            return this.GetSystem<CombatSystem>().ApplyDamage(_attacker, _defender, _skillId, _baseDamages, _contextTags, _randomSeed);
        }
    }
}
