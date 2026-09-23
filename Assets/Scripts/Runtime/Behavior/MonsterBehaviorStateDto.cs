using System;
using Newtonsoft.Json;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum MonsterBehaviorMode
    {
        [LabelText("休眠")] Sleeping,
        [LabelText("闲逛")] Wandering,
        [LabelText("追逐")] Chasing,
        [LabelText("攻击")] Attacking,
        [LabelText("受控")] Controlled,
        [LabelText("死亡")] Dead
    }

    public enum MonsterAttackPhase
    {
        [LabelText("无动作")] None,
        [LabelText("前摇")] Windup,
        [LabelText("后摇")] Recovery
    }

    public sealed class MonsterBehaviorStateDto : IPersistenceDto
    {
        public int Version { get; set; } = 1;
        public MonsterBehaviorMode Mode { get; set; }
        public Vector3Dto Anchor { get; set; } = new();
        public Vector3Dto Facing { get; set; } = new() { X = 1 };
        public Vector3Dto LastSeen { get; set; } = new();
        public float MemoryRemaining { get; set; }
        public Vector3Dto WanderGoal { get; set; } = new();
        public bool HasWanderGoal { get; set; }
        public float WaitRemaining { get; set; }
        public int RandomSeed { get; set; }
        public long RandomSequence { get; set; }
        public long ActionId { get; set; }
        public MonsterAttackPhase AttackPhase { get; set; }
        public float PhaseRemaining { get; set; }
        public bool HitResolved { get; set; }
        public string TargetId { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsValid => Version == 1 && Enum.IsDefined(typeof(MonsterBehaviorMode), Mode)
            && Enum.IsDefined(typeof(MonsterAttackPhase), AttackPhase)
            && VectorValid(Anchor) && VectorValid(Facing) && VectorValid(LastSeen) && VectorValid(WanderGoal)
            && Facing.X * Facing.X + Facing.Y * Facing.Y > .001f
            && NonNegative(MemoryRemaining) && NonNegative(WaitRemaining) && NonNegative(PhaseRemaining)
            && RandomSequence >= 0 && RandomSequence < long.MaxValue && ActionId >= 0 && ActionId < long.MaxValue
            && TargetId != null && TargetId.Length <= 160
            && (AttackPhase == MonsterAttackPhase.None ? PhaseRemaining == 0 && !HitResolved : Mode == MonsterBehaviorMode.Attacking && ActionId > 0 && TargetId.Length > 0)
            && (AttackPhase != MonsterAttackPhase.Windup || !HitResolved)
            && (AttackPhase != MonsterAttackPhase.Recovery || HitResolved);

        public MonsterBehaviorStateDto Copy()
        {
            var copy = (MonsterBehaviorStateDto)MemberwiseClone();
            copy.Anchor = CopyVector(Anchor); copy.Facing = CopyVector(Facing);
            copy.LastSeen = CopyVector(LastSeen); copy.WanderGoal = CopyVector(WanderGoal);
            return copy;
        }

        public static MonsterBehaviorStateDto Create(Vector3Dto position, int seed)
        {
            return new MonsterBehaviorStateDto { Anchor = CopyVector(position), RandomSeed = seed };
        }

        static Vector3Dto CopyVector(Vector3Dto value)
        {
            return new Vector3Dto { X = value.X, Y = value.Y, Z = value.Z };
        }

        static bool NonNegative(float value) { return float.IsFinite(value) && value >= 0; }
        static bool VectorValid(Vector3Dto value)
        {
            return value != null && float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }
    }
}
