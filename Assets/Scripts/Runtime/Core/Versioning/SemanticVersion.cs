using System;

namespace DarkFlare
{
    /// <summary>不可变 SemVer 值；排序忽略构建元数据，完整身份包含构建元数据。</summary>
    public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        readonly string _value;
        readonly string[] _prereleaseIdentifiers;

        // 使用规范十进制字符串，不限制 SemVer 数字段的整数宽度。
        public string Major { get; }
        public string Minor { get; }
        public string Patch { get; }
        public string Prerelease { get; }
        public string BuildMetadata { get; }

        SemanticVersion(string value, string[] core, string prerelease, string[] identifiers, string buildMetadata)
        {
            _value = value;
            _prereleaseIdentifiers = identifiers;
            Major = core[0];
            Minor = core[1];
            Patch = core[2];
            Prerelease = prerelease;
            BuildMetadata = buildMetadata;
        }

        public static SemanticVersion Parse(string value)
        {
            if (!TryParse(value, out SemanticVersion version))
            {
                throw new FormatException("版本必须符合 SemVer 的 major.minor.patch[-prerelease][+build] 格式");
            }

            return version;
        }

        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int buildIndex = value.IndexOf('+');
            string buildMetadata = buildIndex < 0 ? string.Empty : value.Substring(buildIndex + 1);
            if (buildIndex >= 0 && !TryReadIdentifiers(buildMetadata, false, out _))
            {
                return false;
            }

            string precedence = buildIndex < 0 ? value : value.Substring(0, buildIndex);
            int prereleaseIndex = precedence.IndexOf('-');
            string prerelease = prereleaseIndex < 0 ? string.Empty : precedence.Substring(prereleaseIndex + 1);
            string[] identifiers = Array.Empty<string>();
            if (prereleaseIndex >= 0 && !TryReadIdentifiers(prerelease, true, out identifiers))
            {
                return false;
            }

            string coreValue = prereleaseIndex < 0 ? precedence : precedence.Substring(0, prereleaseIndex);
            string[] core = coreValue.Split('.');
            if (core.Length != 3)
            {
                return false;
            }

            foreach (string number in core)
            {
                if (!IsNumeric(number) || HasLeadingZero(number))
                {
                    return false;
                }
            }

            version = new SemanticVersion(value, core, prerelease, identifiers, buildMetadata);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (other is null)
            {
                return 1;
            }

            int result = CompareNumeric(Major, other.Major);
            if (result != 0) { return result; }
            result = CompareNumeric(Minor, other.Minor);
            if (result != 0) { return result; }
            result = CompareNumeric(Patch, other.Patch);
            if (result != 0) { return result; }

            if (_prereleaseIdentifiers.Length == 0)
            {
                return other._prereleaseIdentifiers.Length == 0 ? 0 : 1;
            }
            if (other._prereleaseIdentifiers.Length == 0)
            {
                return -1;
            }

            int count = Math.Min(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
            for (int index = 0; index < count; index++)
            {
                string left = _prereleaseIdentifiers[index];
                string right = other._prereleaseIdentifiers[index];
                bool leftNumeric = IsNumeric(left);
                bool rightNumeric = IsNumeric(right);
                result = leftNumeric && rightNumeric
                    ? CompareNumeric(left, right)
                    : leftNumeric != rightNumeric
                        ? (leftNumeric ? -1 : 1)
                        : string.CompareOrdinal(left, right);
                if (result != 0) { return result; }
            }

            return _prereleaseIdentifiers.Length.CompareTo(other._prereleaseIdentifiers.Length);
        }

        public bool Equals(SemanticVersion other)
        {
            return other is not null && string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SemanticVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(_value);
        }

        public override string ToString()
        {
            return _value;
        }

        public static bool operator ==(SemanticVersion left, SemanticVersion right)
        {
            return ReferenceEquals(left, right) || left is not null && left.Equals(right);
        }

        public static bool operator !=(SemanticVersion left, SemanticVersion right)
        {
            return !(left == right);
        }

        public static bool operator <(SemanticVersion left, SemanticVersion right)
        {
            return Compare(left, right) < 0;
        }

        public static bool operator >(SemanticVersion left, SemanticVersion right)
        {
            return Compare(left, right) > 0;
        }

        public static bool operator <=(SemanticVersion left, SemanticVersion right)
        {
            return Compare(left, right) <= 0;
        }

        public static bool operator >=(SemanticVersion left, SemanticVersion right)
        {
            return Compare(left, right) >= 0;
        }

        static int Compare(SemanticVersion left, SemanticVersion right)
        {
            return left is null ? (right is null ? 0 : -1) : left.CompareTo(right);
        }

        static int CompareNumeric(string left, string right)
        {
            int length = left.Length.CompareTo(right.Length);
            return length != 0 ? length : string.CompareOrdinal(left, right);
        }

        static bool TryReadIdentifiers(string value, bool rejectLeadingZero, out string[] identifiers)
        {
            identifiers = value.Split('.');
            foreach (string identifier in identifiers)
            {
                if (identifier.Length == 0)
                {
                    return false;
                }
                foreach (char character in identifier)
                {
                    if (!(character >= '0' && character <= '9'
                        || character >= 'A' && character <= 'Z'
                        || character >= 'a' && character <= 'z'
                        || character == '-'))
                    {
                        return false;
                    }
                }
                if (rejectLeadingZero && IsNumeric(identifier) && HasLeadingZero(identifier))
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsNumeric(string value)
        {
            if (value.Length == 0) { return false; }
            foreach (char character in value)
            {
                if (character < '0' || character > '9') { return false; }
            }

            return true;
        }

        static bool HasLeadingZero(string value)
        {
            return value.Length > 1 && value[0] == '0';
        }
    }
}

