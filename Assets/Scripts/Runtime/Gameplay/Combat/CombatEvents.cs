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

    public struct ActorDiedEvent
    {
        public CombatActor Actor;
    }

    public struct ActorRevivedEvent
    {
        public CombatActor Actor;
    }
}
