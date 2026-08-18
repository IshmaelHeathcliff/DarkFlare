using System;

namespace DarkFlare
{
    public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
    {
        const char Separator = ':';

        public string Namespace { get; }

        public string LocalId { get; }

        public bool IsValid => IsValidSegment(Namespace) && IsValidSegment(LocalId);

        public ContentId(string contentNamespace, string localId)
        {
            if (!IsValidSegment(contentNamespace))
            {
                throw new ArgumentException("内容命名空间必须使用小写 snake_case", nameof(contentNamespace));
            }

            if (!IsValidSegment(localId))
            {
                throw new ArgumentException("内容本地 ID 必须使用小写 snake_case", nameof(localId));
            }

            Namespace = contentNamespace;
            LocalId = localId;
        }

        public static bool TryCreate(string contentNamespace, string localId, out ContentId contentId)
        {
            if (!IsValidSegment(contentNamespace) || !IsValidSegment(localId))
            {
                contentId = default;
                return false;
            }

            contentId = new ContentId(contentNamespace, localId);
            return true;
        }

        public static ContentId Parse(string value)
        {
            if (!TryParse(value, out ContentId contentId))
            {
                throw new FormatException($"ContentId 格式非法：{value ?? "<null>"}");
            }

            return contentId;
        }

        public static bool TryParse(string value, out ContentId contentId)
        {
            contentId = default;

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int separatorIndex = value.IndexOf(Separator);

            if (separatorIndex <= 0
                || separatorIndex != value.LastIndexOf(Separator)
                || separatorIndex >= value.Length - 1)
            {
                return false;
            }

            return TryCreate(
                value.Substring(0, separatorIndex),
                value.Substring(separatorIndex + 1),
                out contentId);
        }

        public bool Equals(ContentId other)
        {
            return string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
                && string.Equals(LocalId, other.LocalId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ContentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 2166136261u;
                AddToHash(ref hash, Namespace);
                hash ^= Separator;
                hash *= 16777619u;
                AddToHash(ref hash, LocalId);
                return (int)hash;
            }
        }

        public int CompareTo(ContentId other)
        {
            int namespaceComparison = string.Compare(Namespace, other.Namespace, StringComparison.Ordinal);
            return namespaceComparison != 0
                ? namespaceComparison
                : string.Compare(LocalId, other.LocalId, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return IsValid ? $"{Namespace}{Separator}{LocalId}" : string.Empty;
        }

        public static bool operator ==(ContentId left, ContentId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContentId left, ContentId right)
        {
            return !left.Equals(right);
        }

        internal static bool IsValidSegment(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsLowerAsciiLetter(value[0]))
            {
                return false;
            }

            bool previousUnderscore = false;

            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];

                if (character == '_')
                {
                    if (previousUnderscore || i == value.Length - 1)
                    {
                        return false;
                    }

                    previousUnderscore = true;
                    continue;
                }

                if (!IsLowerAsciiLetter(character) && (character < '0' || character > '9'))
                {
                    return false;
                }

                previousUnderscore = false;
            }

            return true;
        }

        static bool IsLowerAsciiLetter(char character)
        {
            return character >= 'a' && character <= 'z';
        }

        static void AddToHash(ref uint hash, string value)
        {
            if (value == null)
            {
                return;
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
        }
    }
}
