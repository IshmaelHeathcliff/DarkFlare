using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class TagSet
    {
        readonly HashSet<string> _ids;

        public static TagSet Empty { get; } = new TagSet();

        public bool IsEmpty => _ids.Count == 0;

        public IReadOnlyCollection<string> Ids => _ids;

        public TagSet()
        {
            _ids = new HashSet<string>();
        }

        public TagSet(IEnumerable<string> ids)
        {
            _ids = new HashSet<string>();

            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _ids.Add(id);
                }
            }
        }

        public static TagSet FromDefinitions(IEnumerable<TagDefinition> definitions)
        {
            List<string> ids = new List<string>();

            foreach (TagDefinition definition in definitions)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id))
                {
                    ids.Add(definition.Id);
                }
            }

            return new TagSet(ids);
        }

        public bool Contains(string id) => _ids.Contains(id);

        public bool ContainsAll(TagSet other)
        {
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

            foreach (string id in other._ids)
            {
                result.Add(id);
            }

            return new TagSet(result);
        }
    }
}

