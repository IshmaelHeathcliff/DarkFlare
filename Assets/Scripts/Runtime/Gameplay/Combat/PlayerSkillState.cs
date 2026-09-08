using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public readonly struct PlayerSkillState
    {
        public ProjectileSkillDefinition Skill { get; }
        public float RemainingSeconds { get; }

        public PlayerSkillState(ProjectileSkillDefinition skill, float remainingSeconds)
        {
            Skill = skill;
            RemainingSeconds = remainingSeconds;
        }
    }

    // Session owns registrations; the adapter never advances the player's clock.
    public sealed class PlayerSkillStateRegistry : AbstractSystem
    {
        readonly Dictionary<CombatActor, Func<PlayerSkillState>> _readers = new Dictionary<CombatActor, Func<PlayerSkillState>>();

        protected override void OnInit() { }

        protected override void OnDeinit() { _readers.Clear(); }

        public void Register(CombatActor actor, Func<PlayerSkillState> reader)
        {
            _readers[actor] = reader;
        }

        public void Unregister(CombatActor actor)
        {
            if (actor != null) { _readers.Remove(actor); }
        }

        public PlayerSkillState Capture(CombatActor actor)
        {
            return actor != null && actor.isActiveAndEnabled && _readers.TryGetValue(actor, out Func<PlayerSkillState> reader)
                ? reader() : default;
        }
    }
}
