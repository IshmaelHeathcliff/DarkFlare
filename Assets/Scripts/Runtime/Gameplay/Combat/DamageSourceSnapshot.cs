using System;

namespace DarkFlare
{
    public enum DamageForm { Hit, Periodic }

    public sealed class DamageSourceSnapshot
    {
        public string ActorKey { get; }
        public ActorTeam Team { get; }
        public string SkillId { get; }
        public string ItemId { get; }
        public CombatTagContext Tags { get; }

        public DamageSourceSnapshot(string actorKey, ActorTeam team, string skillId = null,
            string itemId = null, CombatTagContext tags = null)
        {
            if (string.IsNullOrWhiteSpace(actorKey) || !Enum.IsDefined(typeof(ActorTeam), team))
            {
                throw new ArgumentException("伤害来源身份或阵营非法");
            }
            ActorKey = actorKey; Team = team; SkillId = skillId ?? string.Empty;
            ItemId = itemId ?? string.Empty; Tags = tags ?? CombatTagContext.Empty;
        }
    }
}
