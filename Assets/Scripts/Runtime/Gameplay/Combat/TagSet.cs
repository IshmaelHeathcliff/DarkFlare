using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkFlare
{
    public sealed class TagSet
    {
        readonly HashSet<string> _ids;
        readonly ReadOnlyCollection<string> _idsView;

        public static TagSet Empty { get; } = new TagSet();

        public bool IsEmpty => _ids.Count == 0;

        public IReadOnlyCollection<string> Ids => _idsView;

        public TagSet()
        {
            _ids = new HashSet<string>();
            _idsView = new List<string>().AsReadOnly();
        }

        public TagSet(IEnumerable<string> ids)
        {
            _ids = new HashSet<string>();

            if (ids == null)
            {
                _idsView = new List<string>().AsReadOnly();
                return;
            }

            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _ids.Add(id);
                }
            }

            _idsView = new List<string>(_ids).AsReadOnly();
        }

        public static TagSet FromDefinitions(IEnumerable<TagDefinition> definitions)
        {
            List<string> ids = new List<string>();

            if (definitions == null)
            {
                return Empty;
            }

            foreach (TagDefinition definition in definitions)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id))
                {
                    ids.Add(definition.Id);
                }
            }

            return new TagSet(ids);
        }

        public bool Contains(string id) => !string.IsNullOrWhiteSpace(id) && _ids.Contains(id);

        public bool ContainsAll(TagSet other)
        {
            if (other == null || other.IsEmpty)
            {
                return true;
            }

            foreach (string id in other._ids)
            {
                if (!_ids.Contains(id))
                {
                    return false;
                }
            }

            return true;
        }

        public bool ContainsAny(TagSet other)
        {
            if (other == null || other.IsEmpty)
            {
                return false;
            }

            foreach (string id in other._ids)
            {
                if (_ids.Contains(id))
                {
                    return true;
                }
            }

            return false;
        }

        public TagSet Union(TagSet other)
        {
            HashSet<string> result = new HashSet<string>(_ids);

            if (other == null)
            {
                return new TagSet(result);
            }

            foreach (string id in other._ids)
            {
                result.Add(id);
            }

            return new TagSet(result);
        }

        public string ToDebugString()
        {
            List<string> ids = new List<string>(_ids);
            ids.Sort(StringComparer.Ordinal);
            return $"[{string.Join(", ", ids)}]";
        }

        public override string ToString()
        {
            return ToDebugString();
        }
    }
}
