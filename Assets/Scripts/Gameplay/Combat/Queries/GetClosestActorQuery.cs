using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public class GetClosestActorQuery : AbstractQuery<CombatActor>
    {
        readonly ActorTeam _team;
        readonly Vector3 _position;
        readonly float _range;

        public GetClosestActorQuery(ActorTeam team, Vector3 position, float range)
        {
            _team = team;
            _position = position;
            _range = range;
        }

        protected override CombatActor OnDo()
        {
            IReadOnlyList<CombatActor> actors = this.GetModel<CombatModel>().GetActorsByTeam(_team);
            CombatActor closest = null;
            float closestSqrDistance = _range * _range;

            for (int i = 0; i < actors.Count; i++)
            {
                CombatActor actor = actors[i];

                if (actor == null || !actor.IsAlive)
                {
                    continue;
                }

                float sqrDistance = (actor.transform.position - _position).sqrMagnitude;

                if (sqrDistance <= closestSqrDistance)
                {
                    closest = actor;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closest;
        }
    }
}
