using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class StatusSaveDto : IPersistenceDto
    {
        public double Time { get; set; }
        public List<StatusActorDto> Actors { get; set; } = new List<StatusActorDto>();
    }

    public sealed class StatusActorDto : IPersistenceDto
    {
        public string ActorKey { get; set; }
        public long Order { get; set; }
        public long Registration { get; set; }
        public long NextId { get; set; }
        public List<StatusLayerDto> Layers { get; set; } = new List<StatusLayerDto>();
    }

    public sealed class StatusLayerDto : IPersistenceDto
    {
        public long Id { get; set; }
        public StatusRulesDto Rules { get; set; }
        public StatusEffectDto Effects { get; set; }
        public StatusSourceKind SourceKind { get; set; }
        public string SourceKey { get; set; }
        public string SourceActorKey { get; set; }
        public long SourceRegistration { get; set; }
        public double AppliedAt { get; set; }
        public double? ExpiresAt { get; set; }
        public double? NextTickAt { get; set; }
        public long TickOrdinal { get; set; }
    }

    public sealed class StatusRulesDto : IPersistenceDto
    {
        public string Id { get; set; }
        public StatusRepeatMode Repeat { get; set; }
        public StatusClockMode Clock { get; set; }
        public StatusLifetime Lifetime { get; set; }
        public StatusOverflow Overflow { get; set; }
        public StatusCategory Category { get; set; }
        public AilmentKind Ailment { get; set; }
        public int MaxStacks { get; set; }
        public double Duration { get; set; }
        public double Interval { get; set; }
        public bool CanDispel { get; set; }
        public bool CanConsume { get; set; }
        public bool RefreshExistingLayers { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class StatusEffectDto : IPersistenceDto
    {
        public double Strength { get; set; }
        public List<ModifierInstanceDto> Modifiers { get; set; } = new List<ModifierInstanceDto>();
        public List<StatusDamageDto> Damage { get; set; } = new List<StatusDamageDto>();
        public StatusActionBlock BlockedActions { get; set; }
        public StatusDamageStage DamageStage { get; set; }
        public StatusDamageSourceDto DamageSource { get; set; }
        public StatusResistanceDto Resistance { get; set; }
    }

    public sealed class StatusResistanceDto : IPersistenceDto
    {
        public AilmentKind Ailment { get; set; }
        public float Raw { get; set; }
        public double OriginalDuration { get; set; }
        public StatusEffectDto Before { get; set; }
    }

    public sealed class StatusDamageDto : IPersistenceDto
    {
        public DamageType Type { get; set; }
        public DamageTypeMask ScalingTypes { get; set; }
        public float Amount { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class StatusDamageSourceDto : IPersistenceDto
    {
        public string ActorKey { get; set; }
        public ActorTeam Team { get; set; }
        public string SkillId { get; set; }
        public string ItemId { get; set; }
        public List<List<string>> Tags { get; set; } = new List<List<string>>();
    }
}
