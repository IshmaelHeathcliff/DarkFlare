using System;
using Newtonsoft.Json;

namespace DarkFlare
{
    public enum BossEncounterStatus { None, Ready, Alive, Defeated, LegacyAbsent }

    public sealed class BossEncounterStateDto : IPersistenceDto
    {
        public BossEncounterStatus Status { get; set; }
        public string SpawnId { get; set; } = string.Empty;
        public string MonsterDefinitionId { get; set; } = string.Empty;
        public string InstanceId { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsValid => Enum.IsDefined(typeof(BossEncounterStatus), Status)
            && SpawnId != null && MonsterDefinitionId != null && InstanceId != null
            && (Status == BossEncounterStatus.None ? SpawnId.Length == 0 && MonsterDefinitionId.Length == 0 && InstanceId.Length == 0
                : Status == BossEncounterStatus.LegacyAbsent ? InstanceId.Length == 0
                : ContentId.IsValidSegment(SpawnId)
                    && ContentId.TryParse(MonsterDefinitionId, out ContentId id) && id.Namespace == ContentNamespaces.Monster
                    && (Status == BossEncounterStatus.Ready ? InstanceId.Length == 0
                        : MonsterInstanceId.TryParse(InstanceId, out _)));

        public BossEncounterStateDto Copy()
        {
            return new BossEncounterStateDto { Status = Status, SpawnId = SpawnId,
                MonsterDefinitionId = MonsterDefinitionId, InstanceId = InstanceId };
        }
    }
}
