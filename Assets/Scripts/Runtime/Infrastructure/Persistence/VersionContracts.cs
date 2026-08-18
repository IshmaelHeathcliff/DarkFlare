using System;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct GameVersion : IEquatable<GameVersion>
    {
        public string Value { get; }

        public GameVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("GameVersion 不能为空", nameof(value));
            }

            Value = value;
        }

        public static GameVersion Current => new GameVersion(Application.version);

        public bool Equals(GameVersion other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is GameVersion other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct ContentVersion : IEquatable<ContentVersion>, IComparable<ContentVersion>
    {
        public int Value { get; }

        public ContentVersion(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ContentVersion 必须大于 0");
            }

            Value = value;
        }

        public int CompareTo(ContentVersion other) => Value.CompareTo(other.Value);

        public bool Equals(ContentVersion other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ContentVersion other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public readonly struct SaveSchemaVersion : IEquatable<SaveSchemaVersion>, IComparable<SaveSchemaVersion>
    {
        public int Value { get; }

        public SaveSchemaVersion(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "SaveSchemaVersion 不能小于 0");
            }

            Value = value;
        }

        public int CompareTo(SaveSchemaVersion other) => Value.CompareTo(other.Value);

        public bool Equals(SaveSchemaVersion other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SaveSchemaVersion other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public readonly struct SettingsSchemaVersion : IEquatable<SettingsSchemaVersion>, IComparable<SettingsSchemaVersion>
    {
        public int Value { get; }

        public SettingsSchemaVersion(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "SettingsSchemaVersion 不能小于 0");
            }

            Value = value;
        }

        public int CompareTo(SettingsSchemaVersion other) => Value.CompareTo(other.Value);

        public bool Equals(SettingsSchemaVersion other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SettingsSchemaVersion other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public enum VersionCompatibilityCode
    {
        Compatible,
        MigrationRequired,
        FutureVersionUnsupported,
    }

    public readonly struct VersionCompatibility
    {
        public VersionCompatibilityCode Code { get; }

        public int SourceVersion { get; }

        public int CurrentVersion { get; }

        public VersionCompatibility(
            VersionCompatibilityCode code,
            int sourceVersion,
            int currentVersion)
        {
            Code = code;
            SourceVersion = sourceVersion;
            CurrentVersion = currentVersion;
        }

        public static VersionCompatibility Evaluate(int sourceVersion, int currentVersion)
        {
            if (sourceVersion == currentVersion)
            {
                return new VersionCompatibility(
                    VersionCompatibilityCode.Compatible,
                    sourceVersion,
                    currentVersion);
            }

            return new VersionCompatibility(
                sourceVersion < currentVersion
                    ? VersionCompatibilityCode.MigrationRequired
                    : VersionCompatibilityCode.FutureVersionUnsupported,
                sourceVersion,
                currentVersion);
        }
    }
}
