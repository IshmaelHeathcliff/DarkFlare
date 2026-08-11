namespace DarkFlare
{
    public struct ActorAttackedEvent
    {
        public CombatActor Actor;
    }

    public struct ActorDamagedEvent
    {
        public CombatActor Actor;

        public DamageResult Result;
    }

    public struct DamageResolvedEvent
    {
        public CombatActor Actor;

        public DamageResult Result;
    }

    public struct ActorHealedEvent
    {
        public CombatActor Actor;

        public float Amount;
    }

    public struct ActorDiedEvent
    {
        public CombatActor Actor;
    }

    public struct ActorRevivedEvent
    {
        public CombatActor Actor;
    }
}
