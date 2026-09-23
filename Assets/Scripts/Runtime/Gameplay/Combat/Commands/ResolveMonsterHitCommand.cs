using UnityEngine;

namespace DarkFlare
{
    internal static class MonsterAttackRules
    {
        internal static bool CanHit(IArchitecture architecture, MonsterController monster, CombatActor target)
        {
            if (monster == null || monster.Definition == null || monster.Instance == null || target == null
                || !target.IsAlive || !target.isActiveAndEnabled || monster.Actor == null
                || target.Team == monster.Actor.Team || !monster.Actor.IsAlive || !monster.Actor.IsConfigured
                || !monster.Actor.isActiveAndEnabled || (monster.Actor.BlockedActions & StatusActionBlock.Attack) != 0
                || !architecture.GetSystem<StatusSystem>().GetTarget(monster.Actor).IsValid
                || ((Vector2)monster.transform.position - (Vector2)target.transform.position).sqrMagnitude > monster.Definition.ContactDamageRadius * monster.Definition.ContactDamageRadius
                || monster.Definition.ContactDamages.Count == 0) { return false; }
            NavigationMap navigation = architecture.GetSystem<NavigationSystem>().Active;
            return navigation == null || navigation.HasLineOfSight(monster.transform.position, target.transform.position);
        }
    }

    internal sealed class ResolveMonsterHitCommand : AbstractCommand<bool>
    {
        readonly MonsterController _monster;
        readonly CombatActor _target;
        readonly BehaviorHandle _handle;
        readonly long _action;
        internal ResolveMonsterHitCommand(MonsterController monster, CombatActor target, BehaviorHandle handle, long action)
        { _monster = monster; _target = target; _handle = handle; _action = action; }
        protected override bool OnExecute()
        {
            MonsterBehaviorRuntime runtime = _monster.BehaviorRuntime;
            if (runtime == null || !runtime.IsAuthorized || !runtime.Handle.Equals(_handle)
                || !runtime.TryConsumeHit(_action) || !MonsterAttackRules.CanHit(_monster.GetArchitecture(), _monster, _target)) { return false; }
            int seed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.MonsterAttack);
            AttackRandomRolls rolls = AttackRandomRolls.FromRootSeed(seed);
            AttackSnapshot attack = AttackSnapshotFactory.CreateImmediate(_monster.Actor, "monster_contact", string.Empty,
                _monster.Definition.CreateContactDamagePackets(rolls.BaseDamageSeed), TagSet.Empty, seed);
            this.SendEvent(new ActorAttackedEvent { Actor = _monster.Actor });
            this.GetSystem<CombatSystem>().ApplyDamage(attack, _target); return true;
        }
    }
}
