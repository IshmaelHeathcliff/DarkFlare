using System.Collections.Generic;

namespace DarkFlare
{
    public class CombatModel : AbstractModel
    {
        readonly Dictionary<ActorTeam, List<CombatActor>> _actorsByTeam = new Dictionary<ActorTeam, List<CombatActor>>();

        protected override void OnInit()
        {
        }

        public void RegisterActor(CombatActor actor)
        {
            List<CombatActor> actors = GetOrCreateTeamList(actor.Team);

            if (!actors.Contains(actor))
            {
                actors.Add(actor);
            }
        }

        public void UnregisterActor(CombatActor actor)
        {
            List<CombatActor> actors = GetOrCreateTeamList(actor.Team);
            actors.Remove(actor);
        }

        public IReadOnlyList<CombatActor> GetActorsByTeam(ActorTeam team)
        {
            return _actorsByTeam.TryGetValue(team, out List<CombatActor> actors)
                ? actors : System.Array.Empty<CombatActor>();
        }

        List<CombatActor> GetOrCreateTeamList(ActorTeam team)
        {
            if (!_actorsByTeam.TryGetValue(team, out List<CombatActor> actors))
            {
                actors = new List<CombatActor>();
                _actorsByTeam.Add(team, actors);
            }

            return actors;
        }
    }
}
