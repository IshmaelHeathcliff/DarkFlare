using System;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public readonly struct StatusTargetId : IEquatable<StatusTargetId>
    {
        public int Generation { get; }
        public long Registration { get; }
        public string ActorKey { get; }
        public bool IsValid => Generation > 0 && Registration > 0 && !string.IsNullOrEmpty(ActorKey);

        internal StatusTargetId(int generation, long registration, string actorKey)
        {
            Generation = generation;
            Registration = registration;
            ActorKey = actorKey;
        }

        public bool Equals(StatusTargetId other)
        {
            return Generation == other.Generation && Registration == other.Registration && ActorKey == other.ActorKey;
        }

        public static bool operator ==(StatusTargetId first, StatusTargetId second) { return first.Equals(second); }
        public static bool operator !=(StatusTargetId first, StatusTargetId second) { return !first.Equals(second); }

        public override bool Equals(object obj)
        {
            return obj is StatusTargetId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Generation, Registration, ActorKey);
        }
    }

    public enum StatusSourceKind
    {
        [LabelText("技能")] Skill,
        [LabelText("天赋")] Talent,
        [LabelText("装备")] Equipment,
        [LabelText("消耗物品")] Consumable,
        [LabelText("其他机制")] Mechanism
    }

    public readonly struct StatusSource : IEquatable<StatusSource>
    {
        public StatusSourceKind Kind { get; }
        public string Key { get; }
        public StatusTargetId Actor { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Key) && Enum.IsDefined(typeof(StatusSourceKind), Kind);

        public StatusSource(StatusSourceKind kind, string key, StatusTargetId actor = default)
        {
            if (!Enum.IsDefined(typeof(StatusSourceKind), kind) || string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("状态来源必须具有合法类型和稳定键");
            }
            Kind = kind;
            Key = key;
            Actor = actor;
        }

        public bool Equals(StatusSource other)
        {
            return Kind == other.Kind && Key == other.Key && Actor.Equals(other.Actor);
        }

        public override bool Equals(object obj)
        {
            return obj is StatusSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Key, Actor);
        }
    }
}
