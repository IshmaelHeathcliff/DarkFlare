using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class FirewoodNodeDto : IPersistenceDto
    {
        public string Id { get; set; } = string.Empty;
        public int Q { get; set; }
        public int R { get; set; }
        public FirewoodNodeKind Kind { get; set; }
        public int? OutwardQ { get; set; }
        public int? OutwardR { get; set; }
    }
    public sealed class FirewoodEdgeDto : IPersistenceDto
    {
        public string First { get; set; } = string.Empty;
        public string Second { get; set; } = string.Empty;
    }
    public sealed class FirewoodLayoutDto : IPersistenceDto
    {
        public FirewoodSize Size { get; set; }
        public int Seed { get; set; }
        public int GenerationVersion { get; set; }
        public List<FirewoodNodeDto> Nodes { get; set; } = new List<FirewoodNodeDto>();
        public List<FirewoodEdgeDto> Edges { get; set; } = new List<FirewoodEdgeDto>();
    }
    public sealed class SkillBoardTalentDto : IPersistenceDto
    {
        public string NodeId { get; set; } = string.Empty;
        public int Radius { get; set; }
        public List<ModifierInstanceDto> Modifiers { get; set; } = new List<ModifierInstanceDto>();
    }
    public sealed class SkillBoardItemDto : IPersistenceDto
    {
        public FirewoodLayoutDto Layout { get; set; }
        public List<SkillBoardTalentDto> Talents { get; set; } = new List<SkillBoardTalentDto>();
        public string FlameSkillContentId { get; set; } = string.Empty;
        public int EmberRadius { get; set; }
        public int Energy { get; set; }
        [Newtonsoft.Json.JsonProperty(Required = Newtonsoft.Json.Required.Always)]
        public float CooldownRemainingSeconds { get; set; }
    }
    public sealed class InstalledFirewoodDto : IPersistenceDto
    {
        public string ItemInstanceId { get; set; } = string.Empty;
        public int Q { get; set; }
        public int R { get; set; }
        public int Rotation { get; set; }
    }
    public sealed class SkillBoardEmbeddingDto : IPersistenceDto
    {
        public string FirewoodInstanceId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string ItemInstanceId { get; set; } = string.Empty;
    }
    public sealed class FirewoodConnectionDto : IPersistenceDto
    {
        public string FirstFirewoodId { get; set; } = string.Empty;
        public string FirstNodeId { get; set; } = string.Empty;
        public string SecondFirewoodId { get; set; } = string.Empty;
        public string SecondNodeId { get; set; } = string.Empty;
    }
    public sealed class SkillBoardSaveDto : IPersistenceDto
    {
        public string ActorInstanceId { get; set; } = string.Empty;
        public List<InstalledFirewoodDto> Firewoods { get; set; } = new List<InstalledFirewoodDto>();
        public List<SkillBoardEmbeddingDto> Embeddings { get; set; } = new List<SkillBoardEmbeddingDto>();
        public List<FirewoodConnectionDto> Connections { get; set; } = new List<FirewoodConnectionDto>();
        public string EnergyBottleInstanceId { get; set; } = string.Empty;
    }
}
