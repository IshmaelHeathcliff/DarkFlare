using System;
using Newtonsoft.Json;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum BossActionPhase
    {
        [LabelText("无")] None,
        [LabelText("阶段转换")] PhaseTransition,
        [LabelText("挥击预警")] SwipeWarning,
        [LabelText("挥击恢复")] SwipeRecovery,
        [LabelText("冲锋预警")] ChargeWarning,
        [LabelText("冲锋移动")] ChargeTravel,
        [LabelText("冲锋恢复")] ChargeRecovery
    }

    public sealed class BossBehaviorStateDto : IPersistenceDto
    {
        public int Version { get; set; } = 1;
        public bool Empowered { get; set; }
        public BossActionPhase Action { get; set; }
        public long ActionId { get; set; }
        public float PhaseRemaining { get; set; }
        public float ChargeRemaining { get; set; }
        public float SwipeCooldown { get; set; }
        public float ChargeCooldown { get; set; }
        public bool HitResolved { get; set; }
        public Vector3Dto Facing { get; set; } = new() { X = 1 };
        public Vector3Dto LastSeen { get; set; } = new();
        public float MemoryRemaining { get; set; }
        public string TargetId { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsValid => Version == 1 && Enum.IsDefined(typeof(BossActionPhase), Action)
            && ActionId >= 0 && ActionId < long.MaxValue && TargetId != null && TargetId.Length <= 160
            && Valid(PhaseRemaining) && Valid(ChargeRemaining) && Valid(SwipeCooldown) && Valid(ChargeCooldown)
            && Valid(MemoryRemaining) && Vector(Facing) && Vector(LastSeen)
            && Facing.X * Facing.X + Facing.Y * Facing.Y > .99f
            && Facing.X * Facing.X + Facing.Y * Facing.Y < 1.01f
            && (Action == BossActionPhase.None ? PhaseRemaining == 0 && ChargeRemaining == 0 : ActionId > 0)
            && (Action is BossActionPhase.SwipeWarning or BossActionPhase.SwipeRecovery or BossActionPhase.ChargeWarning
                or BossActionPhase.ChargeTravel or BossActionPhase.ChargeRecovery ? TargetId.Length > 0 : TargetId.Length == 0)
            && (Action is BossActionPhase.ChargeWarning or BossActionPhase.ChargeTravel ? ChargeRemaining > 0 : true)
            && (Action is BossActionPhase.ChargeTravel or BossActionPhase.ChargeWarning || ChargeRemaining == 0)
            && (Action is BossActionPhase.SwipeRecovery or BossActionPhase.ChargeTravel or BossActionPhase.ChargeRecovery || !HitResolved);

        public BossBehaviorStateDto Copy()
        {
            var copy = (BossBehaviorStateDto)MemberwiseClone();
            copy.Facing = new Vector3Dto { X = Facing.X, Y = Facing.Y, Z = Facing.Z };
            copy.LastSeen = new Vector3Dto { X = LastSeen.X, Y = LastSeen.Y, Z = LastSeen.Z };
            return copy;
        }

        static bool Valid(float value) { return float.IsFinite(value) && value >= 0; }
        static bool Vector(Vector3Dto value)
        {
            return value != null && float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }
    }
}
