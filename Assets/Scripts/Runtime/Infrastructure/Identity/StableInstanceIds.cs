using System;
using System.Globalization;

namespace DarkFlare
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public static PlayerId LocalPlayer { get; } = new PlayerId("local_player");

        public string Value { get; }

        public PlayerId(string value)
        {
            if (!ContentId.IsValidSegment(value))
            {
                throw new ArgumentException("PlayerId 必须是小写稳定 slug", nameof(value));
            }

            Value = value;
        }

        public bool Equals(PlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);

        public override int GetHashCode() => StableIdentityHash.Compute(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);

        public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);
    }

    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>
    {
        public string Value { get; }

        public bool IsCanonical => IsCanonicalValue(Value);

        ItemInstanceId(string value)
        {
            Value = value;
        }

        public static ItemInstanceId Parse(string value)
        {
            if (!TryParse(value, out ItemInstanceId result))
            {
                throw new FormatException($"非法 ItemInstanceId：{value}");
            }

            return result;
        }

        public static bool TryParse(string value, out ItemInstanceId result)
        {
            if (!IsCanonicalValue(value))
            {
                result = default;
                return false;
            }

            result = new ItemInstanceId(value);
            return true;
        }

        internal static ItemInstanceId FromLegacy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("物品实例 ID 不能为空", nameof(value));
            }

            return new ItemInstanceId(value);
        }

        public bool Equals(ItemInstanceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ItemInstanceId other && Equals(other);

        public override int GetHashCode() => StableIdentityHash.Compute(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(ItemInstanceId left, ItemInstanceId right) => left.Equals(right);

        public static bool operator !=(ItemInstanceId left, ItemInstanceId right) => !left.Equals(right);

        static bool IsCanonicalValue(string value)
        {
            if (value == null || value.Length != 32)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool digit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';

                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly struct RunId : IEquatable<RunId>
    {
        public string Value { get; }

        public RunId(string value)
        {
            if (!ItemInstanceId.TryParse(value, out _))
            {
                throw new ArgumentException("RunId 必须是 32 位小写 UUID", nameof(value));
            }

            Value = value;
        }

        public bool Equals(RunId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is RunId other && Equals(other);

        public override int GetHashCode() => StableIdentityHash.Compute(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(RunId left, RunId right) => left.Equals(right);

        public static bool operator !=(RunId left, RunId right) => !left.Equals(right);
    }

    public readonly struct MonsterInstanceId : IEquatable<MonsterInstanceId>
    {
        public string Value { get; }

        internal MonsterInstanceId(RunId runId, long sequence)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Value = $"{runId.Value}:monster:{sequence.ToString(CultureInfo.InvariantCulture)}";
        }

        internal static MonsterInstanceId FromLegacySeed(int seed)
        {
            return new MonsterInstanceId(
                new RunId($"{unchecked((uint)seed):x8}000000000000000000000000"),
                1);
        }

        public bool Equals(MonsterInstanceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MonsterInstanceId other && Equals(other);

        public override int GetHashCode() => StableIdentityHash.Compute(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(MonsterInstanceId left, MonsterInstanceId right) => left.Equals(right);

        public static bool operator !=(MonsterInstanceId left, MonsterInstanceId right) => !left.Equals(right);
    }

    public readonly struct WorldDropId : IEquatable<WorldDropId>
    {
        public string Value { get; }

        internal WorldDropId(RunId runId, long sequence)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Value = $"{runId.Value}:drop:{sequence.ToString(CultureInfo.InvariantCulture)}";
        }

        internal static WorldDropId FromLegacyItem(ItemInstanceId itemId)
        {
            string seed = StableIdentityHash.Compute(itemId.Value).ToString("x8", CultureInfo.InvariantCulture);
            return new WorldDropId(new RunId($"{seed}000000000000000000000000"), 1);
        }

        public bool Equals(WorldDropId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is WorldDropId other && Equals(other);

        public override int GetHashCode() => StableIdentityHash.Compute(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(WorldDropId left, WorldDropId right) => left.Equals(right);

        public static bool operator !=(WorldDropId left, WorldDropId right) => !left.Equals(right);
    }

    public readonly struct SaveSlotId : IEquatable<SaveSlotId>
    {
        public string Value { get; }

        public SaveSlotId(string value)
        {
            if (!ContentId.IsValidSegment(value))
            {
                throw new ArgumentException("SaveSlotId 必须是小写稳定 slug", nameof(value));
            }

            Value = value;
        }

        public bool Equals(SaveSlotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SaveSlotId other && Equals(other);

        public override int GetHashCode() => StableIdentityHash.Compute(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(SaveSlotId left, SaveSlotId right) => left.Equals(right);

        public static bool operator !=(SaveSlotId left, SaveSlotId right) => !left.Equals(right);
    }

    public interface IItemInstanceIdGenerator : IUtility
    {
        ItemInstanceId Next();
    }

    public sealed class UuidItemInstanceIdGenerator : IItemInstanceIdGenerator
    {
        public ItemInstanceId Next()
        {
            return ItemInstanceId.Parse(Guid.NewGuid().ToString("N"));
        }
    }

    public sealed class DeterministicItemInstanceIdGenerator : IItemInstanceIdGenerator
    {
        readonly uint _seed;
        uint _sequence;

        public DeterministicItemInstanceIdGenerator(int seed)
        {
            _seed = unchecked((uint)seed);
        }

        public ItemInstanceId Next()
        {
            _sequence++;
            uint first = Mix(_seed ^ _sequence);
            uint second = Mix(first ^ 0x9E3779B9u);
            uint third = Mix(second ^ _sequence);
            uint fourth = Mix(third ^ _seed);
            return ItemInstanceId.Parse($"{first:x8}{second:x8}{third:x8}{fourth:x8}");
        }

        static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            return value ^ value >> 16;
        }
    }

    public interface IRunInstanceIdGenerator : IUtility
    {
        RunId RunId { get; }

        MonsterInstanceId NextMonsterId();

        WorldDropId NextWorldDropId();
    }

    public sealed class RunInstanceIdGenerator : IRunInstanceIdGenerator
    {
        long _monsterSequence;
        long _worldDropSequence;

        public RunId RunId { get; }

        public RunInstanceIdGenerator(RunId runId)
        {
            RunId = runId;
        }

        public static RunInstanceIdGenerator Create()
        {
            return new RunInstanceIdGenerator(new RunId(Guid.NewGuid().ToString("N")));
        }

        public MonsterInstanceId NextMonsterId()
        {
            _monsterSequence++;
            return new MonsterInstanceId(RunId, _monsterSequence);
        }

        public WorldDropId NextWorldDropId()
        {
            _worldDropSequence++;
            return new WorldDropId(RunId, _worldDropSequence);
        }
    }

    static class StableIdentityHash
    {
        public static int Compute(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string safeValue = value ?? string.Empty;

                for (int i = 0; i < safeValue.Length; i++)
                {
                    hash ^= safeValue[i];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
