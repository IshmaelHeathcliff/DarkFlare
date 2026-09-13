namespace DarkFlare
{
    public enum ActorResourceType
    {
        Health,
        Mana
    }

    public enum ActorResourceChangeReason
    {
        Configure,
        Damage,
        Healing,
        Regeneration,
        SkillCost,
        Restore,
        MaximumChanged,
        Revive
    }

    public readonly struct ActorResourceChangedEvent
    {
        public CombatActor Actor { get; }

        public ActorResourceType ResourceType { get; }

        public float PreviousValue { get; }

        public float CurrentValue { get; }

        public float PreviousMaximum { get; }

        public float CurrentMaximum { get; }

        public ActorResourceChangeReason Reason { get; }

        public ActorResourceChangedEvent(
            CombatActor actor,
            ActorResourceType resourceType,
            float previousValue,
            float currentValue,
            float previousMaximum,
            float currentMaximum,
            ActorResourceChangeReason reason)
        {
            Actor = actor;
            ResourceType = resourceType;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            PreviousMaximum = previousMaximum;
            CurrentMaximum = currentMaximum;
            Reason = reason;
        }
    }

    public enum SkillCastRejectionReason
    {
        InvalidOwner,
        InvalidDamageSource,
        InsufficientMana,
        SpawnFailed,
        ActionBlocked
    }

    public readonly struct SkillCastRejectedEvent
    {
        public CombatActor Actor { get; }

        public ProjectileSkillDefinition Skill { get; }

        public SkillCastRejectionReason Reason { get; }

        public float CurrentMana { get; }

        public float RequiredMana { get; }

        public SkillCastRejectedEvent(
            CombatActor actor,
            ProjectileSkillDefinition skill,
            SkillCastRejectionReason reason,
            float currentMana,
            float requiredMana)
        {
            Actor = actor;
            Skill = skill;
            Reason = reason;
            CurrentMana = currentMana;
            RequiredMana = requiredMana;
        }
    }

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
        public DamageSourceSnapshot Source { get; set; }
        public DamageForm Form { get; set; }
    }

    public struct ActorRevivedEvent
    {
        public CombatActor Actor;
    }
}
