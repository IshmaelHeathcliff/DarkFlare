using UnityEngine;

namespace DarkFlare
{
    internal sealed class ResolveBossHitCommand : AbstractCommand<bool>
    {
        readonly MonsterController _boss;
        readonly CombatActor _target;
        readonly BehaviorHandle _handle;
        readonly long _actionId;
        readonly BossAttackKind _kind;
        readonly Vector2 _from;
        readonly Vector2 _to;
        readonly Vector2 _facing;

        internal ResolveBossHitCommand(MonsterController boss, CombatActor target, BehaviorHandle handle, long actionId,
            BossAttackKind kind, Vector2 from, Vector2 to, Vector2 facing)
        {
            _boss = boss; _target = target; _handle = handle; _actionId = actionId; _kind = kind;
            _from = from; _to = to; _facing = facing;
        }

        protected override bool OnExecute()
        {
            BossBehaviorRuntime runtime = _boss?.BossRuntime;
            BossBehaviorDefinition config = _boss?.Definition?.BossBehavior;
            if (runtime == null || config == null || !runtime.IsAuthorized || !runtime.Handle.Equals(_handle)
                || _target == null || !_target.IsAlive || !_target.isActiveAndEnabled || _target.Team != ActorTeam.Player
                || _boss.Actor == null || !_boss.Actor.IsAlive || !_boss.Actor.isActiveAndEnabled
                || (_boss.Actor.BlockedActions & StatusActionBlock.Attack) != 0
                || _boss.Definition.ContactDamages.Count == 0) { return false; }
            Vector2 point = _target.transform.position;
            bool contact;
            if (_kind == BossAttackKind.Swipe)
            {
                Vector2 offset = point - _from;
                contact = offset.sqrMagnitude <= config.SwipeRadius * config.SwipeRadius
                    && (offset.sqrMagnitude < .0001f || Vector2.Dot(_facing, offset.normalized) >= config.SwipeCosine);
            }
            else
            {
                Vector2 segment = _to - _from;
                float ratio = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(point - _from, segment) / segment.sqrMagnitude) : 0;
                contact = Vector2.Distance(point, _from + segment * ratio) <= config.ChargeHitRadius;
            }
            NavigationMap navigation = this.GetSystem<NavigationSystem>().Active;
            if (!contact || navigation != null && !navigation.HasLineOfSight(_boss.transform.position, _target.transform.position)
                || !runtime.TryConsumeHit(_actionId)) { return false; }
            int seed = this.GetSystem<GameplayRandomSystem>().NextSeed(GameplayRandomChannel.MonsterAttack);
            AttackRandomRolls rolls = AttackRandomRolls.FromRootSeed(seed);
            AttackSnapshot attack = AttackSnapshotFactory.CreateImmediate(_boss.Actor,
                _kind == BossAttackKind.Swipe ? "boss_swipe" : "boss_charge", string.Empty,
                _boss.Definition.CreateContactDamagePackets(rolls.BaseDamageSeed), TagSet.Empty, seed);
            this.SendEvent(new ActorAttackedEvent { Actor = _boss.Actor });
            this.GetSystem<CombatSystem>().ApplyDamage(attack, _target);
            return true;
        }
    }
}
