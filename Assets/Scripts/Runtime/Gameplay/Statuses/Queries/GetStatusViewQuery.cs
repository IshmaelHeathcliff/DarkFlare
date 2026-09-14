using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public readonly struct StatusActorChoice
    {
        public string Key { get; }
        public LocalizedMessage Name { get; }
        public StatusActorChoice(string key, LocalizedMessage name) { Key = key; Name = name; }
    }

    public sealed class StatusViewSnapshot
    {
        public IReadOnlyList<StatusActorChoice> Actors { get; }
        public StatusTargetSnapshot Player { get; }
        public StatusTargetSnapshot Selected { get; }
        public StatusViewSnapshot(IReadOnlyList<StatusActorChoice> actors, StatusTargetSnapshot player, StatusTargetSnapshot selected)
        {
            Actors = actors; Player = player; Selected = selected;
        }
    }

    public sealed class GetStatusViewQuery : AbstractQuery<StatusViewSnapshot>
    {
        readonly string _selected;
        readonly bool _includeMonsters;
        public GetStatusViewQuery(string selected, bool includeMonsters) { _selected = selected; _includeMonsters = includeMonsters; }
        protected override StatusViewSnapshot OnDo()
        {
            StatusSystem statuses = this.GetSystem<StatusSystem>();
            CombatModel combat = this.GetModel<CombatModel>();
            var choices = new List<StatusActorChoice>();
            StatusTargetId playerTarget = default;
            StatusTargetId selectedTarget = default;
            foreach (CombatActor actor in combat.GetActorsByTeam(ActorTeam.Player)
                .Concat(_includeMonsters ? combat.GetActorsByTeam(ActorTeam.Monster) : System.Array.Empty<CombatActor>()))
            {
                StatusTargetId target = statuses.GetTarget(actor);
                if (!target.IsValid) { continue; }
                PlayerController player = actor.GetComponent<PlayerController>();
                MonsterController monster = actor.GetComponent<MonsterController>();
                LocalizedMessage name = player != null ? player.Definition.LocalizedName.Message
                    : monster != null ? monster.Definition.LocalizedName.Message : new LocalizedMessage("ui", "status.actor");
                choices.Add(new StatusActorChoice(target.ActorKey, name));
                if (player != null) { playerTarget = target; }
                if (target.ActorKey == _selected) { selectedTarget = target; }
            }
            choices = choices.OrderBy(choice => choice.Key == playerTarget.ActorKey ? 0 : 1)
                .ThenBy(choice => choice.Key, System.StringComparer.Ordinal).ToList();
            return new StatusViewSnapshot(choices.AsReadOnly(), statuses.GetStatusSnapshot(playerTarget),
                statuses.GetStatusSnapshot(selectedTarget.IsValid ? selectedTarget : playerTarget));
        }
    }
}
