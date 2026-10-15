using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class GrowthActivationDto : IPersistenceDto
    {
        public string FirewoodId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public int Cost { get; set; }
    }

    public sealed class PlayerGrowthDto : IPersistenceDto
    {
        public string ActorInstanceId { get; set; } = string.Empty;
        public int ContractVersion { get; set; } = GrowthRules.ContractVersion;
        public int FirstCost { get; set; } = 100;
        public int CostIncrement { get; set; } = 25;
        public int FirewoodCost { get; set; } = 1;
        public int TalentCost { get; set; } = 1;
        public int AttributeValue { get; set; } = 1;
        public long Energy { get; set; }
        public int EarnedPoints { get; set; }
        public int TalentPoints { get; set; }
        public int AttributePoints { get; set; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
        public List<GrowthActivationDto> Activations { get; set; } = new List<GrowthActivationDto>();
    }

    public static partial class RuntimeStateMapper
    {
        public static PlayerGrowthDto ToDto(string playerId, PlayerGrowthState state)
        {
            return new PlayerGrowthDto
            {
                ActorInstanceId = playerId,
                FirstCost = state.Rules.FirstCost,
                CostIncrement = state.Rules.CostIncrement,
                FirewoodCost = state.Rules.FirewoodCost,
                TalentCost = state.Rules.TalentCost,
                AttributeValue = state.Rules.AttributeValue,
                Energy = state.Energy,
                EarnedPoints = state.EarnedPoints,
                TalentPoints = state.TalentPoints,
                AttributePoints = state.AttributePoints,
                Strength = state.Strength,
                Dexterity = state.Dexterity,
                Intelligence = state.Intelligence,
                Activations = state.Activations.Select(a => new GrowthActivationDto
                {
                    FirewoodId = a.FirewoodId, NodeId = a.NodeId, Cost = a.Cost
                }).ToList()
            };
        }

        public static PlayerGrowthState ReadPlayerGrowth(PlayerGrowthDto dto, out string error)
        {
            error = "成长合同、集合缺失或超限";
            if (dto == null || dto.ContractVersion != GrowthRules.ContractVersion || dto.Activations == null
                || dto.Activations.Count > LocalSaveFormat.MaximumItems * 16 || dto.Activations.Any(a => a == null))
            {
                return null;
            }
            var rules = new GrowthRules(dto.FirstCost, dto.CostIncrement, dto.FirewoodCost, dto.TalentCost, dto.AttributeValue);
            var state = new PlayerGrowthState(rules, dto.Energy, dto.EarnedPoints, dto.TalentPoints, dto.AttributePoints,
                dto.Strength, dto.Dexterity, dto.Intelligence,
                dto.Activations.Select(a => new GrowthActivation(a.FirewoodId, a.NodeId, a.Cost)));
            error = state.Validate();
            return error == null ? state : null;
        }

        internal static bool GrowthOwnerMatches(PlayerGrowthDto dto, PlayerGrowthState state, string playerId)
        {
            return dto.ActorInstanceId == playerId || string.IsNullOrEmpty(dto.ActorInstanceId)
                && state.EarnedPoints == 0 && state.Energy == 0;
        }
    }
}
