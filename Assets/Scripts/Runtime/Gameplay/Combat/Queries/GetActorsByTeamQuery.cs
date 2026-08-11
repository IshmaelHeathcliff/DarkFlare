using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class GetActorsByTeamQuery : AbstractQuery<IReadOnlyList<CombatActor>>
    {
        readonly ActorTeam _team;

        public GetActorsByTeamQuery(ActorTeam team)
        {
            _team = team;
        }

        protected override IReadOnlyList<CombatActor> OnDo()
        {
            return this.GetModel<CombatModel>().GetActorsByTeam(_team);
        }
    }
}
