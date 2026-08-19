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

        public static RunId Parse(string value)
        {
            if (!TryParse(value, out RunId result))
            {
                throw new FormatException($"非法 RunId：{value}");
            }

            return result;
        }

        public static bool TryParse(string value, out RunId result)
        {
            if (!ItemInstanceId.TryParse(value, out _))
            {
                result = default;
                return false;
            }

            result = new RunId(value);
            return true;
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

        public RunId RunId { get; }

        public long Sequence { get; }

        internal MonsterInstanceId(RunId runId, long sequence)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            RunId = runId;
            Sequence = sequence;
            Value = $"{runId.Value}:monster:{sequence.ToString(CultureInfo.InvariantCulture)}";
        }

        public static MonsterInstanceId Parse(string value)
        {
            if (!TryParse(value, out MonsterInstanceId result))
            {
                throw new FormatException($"非法 MonsterInstanceId：{value}");
            }

            return result;
        }

        public static bool TryParse(string value, out MonsterInstanceId result)
        {
            if (!RunScopedInstanceIdParser.TryParse(value, "monster", out RunId runId, out long sequence))
            {
                result = default;
                return false;
            }

            result = new MonsterInstanceId(runId, sequence);
            return true;
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

        public RunId RunId { get; }

        public long Sequence { get; }

        internal WorldDropId(RunId runId, long sequence)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            RunId = runId;
            Sequence = sequence;
            Value = $"{runId.Value}:drop:{sequence.ToString(CultureInfo.InvariantCulture)}";
        }

        public static WorldDropId Parse(string value)
        {
            if (!TryParse(value, out WorldDropId result))
            {
                throw new FormatException($"非法 WorldDropId：{value}");
            }

            return result;
        }

        public static bool TryParse(string value, out WorldDropId result)
        {
            if (!RunScopedInstanceIdParser.TryParse(value, "drop", out RunId runId, out long sequence))
            {
                result = default;
                return false;
            }

            result = new WorldDropId(runId, sequence);
            return true;
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

        public static SaveSlotId Parse(string value)
        {
            return new SaveSlotId(value);
        }

        public static bool TryParse(string value, out SaveSlotId result)
        {
            if (!ContentId.IsValidSegment(value))
            {
                result = default;
                return false;
            }

            result = new SaveSlotId(value);
            return true;
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

        RunInstanceIdState CaptureState();

        MonsterInstanceId NextMonsterId();

        WorldDropId NextWorldDropId();
    }

    public readonly struct RunInstanceIdState
    {
        public RunId RunId { get; }

        public long NextMonsterSequence { get; }

        public long NextWorldDropSequence { get; }

        public RunInstanceIdState(
            RunId runId,
            long nextMonsterSequence,
            long nextWorldDropSequence)
        {
            if (!DarkFlare.RunId.TryParse(runId.Value, out _))
            {
                throw new ArgumentException("RunInstanceIdState 必须引用有效 RunId", nameof(runId));
            }

            if (nextMonsterSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextMonsterSequence));
            }

            if (nextWorldDropSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextWorldDropSequence));
            }

            RunId = runId;
            NextMonsterSequence = nextMonsterSequence;
            NextWorldDropSequence = nextWorldDropSequence;
        }
    }

    public sealed class RunInstanceIdGenerator : IRunInstanceIdGenerator
    {
        long _nextMonsterSequence;
        long _nextWorldDropSequence;

        public RunId RunId { get; }

        public RunInstanceIdGenerator(RunId runId)
            : this(new RunInstanceIdState(runId, 1, 1))
        {
        }

        public RunInstanceIdGenerator(RunInstanceIdState state)
        {
            RunId = state.RunId;
            _nextMonsterSequence = state.NextMonsterSequence;
            _nextWorldDropSequence = state.NextWorldDropSequence;
        }

        public static RunInstanceIdGenerator Create()
        {
            return new RunInstanceIdGenerator(new RunId(Guid.NewGuid().ToString("N")));
        }

        public RunInstanceIdState CaptureState()
        {
            return new RunInstanceIdState(
                RunId,
                _nextMonsterSequence,
                _nextWorldDropSequence);
        }

        public MonsterInstanceId NextMonsterId()
        {
            MonsterInstanceId result = new MonsterInstanceId(RunId, _nextMonsterSequence);
            _nextMonsterSequence = checked(_nextMonsterSequence + 1);
            return result;
        }

        public WorldDropId NextWorldDropId()
        {
            WorldDropId result = new WorldDropId(RunId, _nextWorldDropSequence);
            _nextWorldDropSequence = checked(_nextWorldDropSequence + 1);
            return result;
        }
    }

    static class RunScopedInstanceIdParser
    {
        public static bool TryParse(
            string value,
            string expectedKind,
            out RunId runId,
            out long sequence)
        {
            runId = default;
            sequence = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] segments = value.Split(':');

            if (segments.Length != 3
                || !string.Equals(segments[1], expectedKind, StringComparison.Ordinal)
                || !RunId.TryParse(segments[0], out runId)
                || !long.TryParse(
                    segments[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out sequence)
                || sequence <= 0
                || !string.Equals(
                    segments[2],
                    sequence.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                runId = default;
                sequence = 0;
                return false;
            }

            return true;
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
