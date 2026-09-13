using UnityEngine;

namespace DarkFlare
{
    public sealed class ContactAttackCommand : AbstractCommand<bool>
    {
        readonly MonsterController _monster;
        readonly CombatActor _target;
        public ContactAttackCommand(MonsterController monster, CombatActor target)
        {
            _monster = monster; _target = target;
        }

        protected override bool OnExecute()
        {
            if (_monster == null || _monster.Definition == null || _monster.Instance == null
                || _target == null || !_target.IsAlive || !_target.isActiveAndEnabled
                || _target.Team == _monster.Actor.Team
                || !this.SendQuery(new GetActorActionsQuery(_monster.Actor)).CanAttack
                || _monster.ContactDamageCooldownRemainingSeconds > 0
                || Vector2.Distance(_monster.transform.position, _target.transform.position) > _monster.Definition.ContactDamageRadius
                || _monster.Definition.ContactDamages.Count == 0) { return false; }
            int seed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.MonsterAttack);
            AttackRandomRolls rolls = AttackRandomRolls.FromRootSeed(seed);
            AttackSnapshot attack = AttackSnapshotFactory.CreateImmediate(_monster.Actor, "monster_contact", string.Empty,
                _monster.Definition.CreateContactDamagePackets(rolls.BaseDamageSeed), TagSet.Empty, seed);
            _monster.MarkContactAttack();
            this.SendEvent(new ActorAttackedEvent { Actor = _monster.Actor });
            this.GetSystem<CombatSystem>().ApplyDamage(attack, _target);
            return true;
        }
    }
}
