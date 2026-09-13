using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public readonly struct ActorActionPermission
    {
        public bool IsReady { get; }
        public StatusActionBlock Blocked { get; }
        public IReadOnlyList<StatusLayerSnapshot> BlockingSources { get; }
        public bool CanMove => IsReady && (Blocked & StatusActionBlock.Move) == 0;
        public bool CanAttack => IsReady && (Blocked & StatusActionBlock.Attack) == 0;
        public bool CanCast => IsReady && (Blocked & StatusActionBlock.Cast) == 0;
        public bool CanUseItem => IsReady && (Blocked & StatusActionBlock.UseItem) == 0;
        public ActorActionPermission(bool ready, StatusActionBlock blocked, IEnumerable<StatusLayerSnapshot> sources)
        {
            IsReady = ready; Blocked = blocked;
            BlockingSources = new List<StatusLayerSnapshot>(sources ?? Array.Empty<StatusLayerSnapshot>()).AsReadOnly();
        }
    }

    public sealed class GetActorActionsQuery : AbstractQuery<ActorActionPermission>
    {
        readonly CombatActor _actor;
        public GetActorActionsQuery(CombatActor actor) { _actor = actor; }
        protected override ActorActionPermission OnDo()
        {
            if (_actor == null || !_actor.isActiveAndEnabled || !_actor.IsAlive || !_actor.IsConfigured)
            {
                return new ActorActionPermission(false, StatusActionBlock.None, null);
            }
            StatusSystem statuses = this.GetSystem<StatusSystem>();
            StatusTargetId target = statuses.GetTarget(_actor);
            bool ready = target.IsValid || (_actor.StatusActorKey == null
                && _actor.GetComponent<PlayerController>() == null && _actor.GetComponent<MonsterController>() == null);
            return new ActorActionPermission(ready, _actor.BlockedActions, statuses.GetStatusSnapshot(target).Layers
                .Where(layer => layer.IsActive && layer.Effects.BlockedActions != StatusActionBlock.None));
        }
    }
}
