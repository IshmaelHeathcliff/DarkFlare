using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public partial class CombatActor
    {
        readonly SortedDictionary<string, ModifierInstance[]> _modifierSources = new SortedDictionary<string, ModifierInstance[]>(StringComparer.Ordinal);
        TagSet _staticTags = TagSet.Empty;
        TagSet _statusTags = TagSet.Empty;
        public StatusActionBlock BlockedActions { get; internal set; }

        public void SetModifierSource(string source, IEnumerable<ModifierInstance> modifiers)
        {
            var replacements = new Dictionary<string, IEnumerable<ModifierInstance>> { [source] = modifiers };
            PrepareEffects(replacements, _statusTags, BlockedActions).Apply();
        }

        internal ActorEffectPreparation PrepareEffects(IDictionary<string, IEnumerable<ModifierInstance>> replacements,
            TagSet statusTags, StatusActionBlock blockedActions)
        {
            var sources = new SortedDictionary<string, ModifierInstance[]>(_modifierSources, StringComparer.Ordinal);
            foreach (KeyValuePair<string, IEnumerable<ModifierInstance>> pair in replacements)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) { throw new ArgumentException("修改器来源不能为空"); }
                ModifierInstance[] values = pair.Value?.ToArray() ?? Array.Empty<ModifierInstance>();
                if (values.Length == 0) { sources.Remove(pair.Key); }
                else { sources[pair.Key] = values; }
            }
            ModifierInstance[] modifiers = sources.Values.SelectMany(value => value).ToArray();
            foreach (ModifierInstance modifier in modifiers)
            {
                if (modifier == null || !IsFinite(modifier.Value)) { throw new ArgumentException("修改器数值非法"); }
            }
            TagSet tags = _staticTags.Union(statusTags);
            bool changed = !SameModifiers(modifiers, _modifiers) || blockedActions != BlockedActions
                || !tags.ContainsAll(_tags) || !_tags.ContainsAll(tags);
            StatBlock stats = changed ? CombatStatResolver.Build(_baseStats, modifiers, contextTags: tags) : _stats;
            foreach (float value in stats.Values.Values)
            {
                if (!IsFinite(value)) { throw new ArgumentException("属性计算溢出"); }
            }
            return new ActorEffectPreparation(this, sources, modifiers, stats, statusTags, tags, blockedActions, changed);
        }

        static bool SameModifiers(IReadOnlyList<ModifierInstance> first, IReadOnlyList<ModifierInstance> second)
        {
            if (first.Count != second.Count) { return false; }
            for (int i = 0; i < first.Count; i++)
            {
                ModifierInstance a = first[i];
                ModifierInstance b = second[i];
                if (a.StatId != b.StatId || a.Operation != b.Operation || a.Scope != b.Scope || a.Value != b.Value
                    || a.FromDamageType != b.FromDamageType || a.ToDamageType != b.ToDamageType
                    || !a.Origin.Equals(b.Origin) || a.UsesLegacyTagMatching != b.UsesLegacyTagMatching
                    || a.Query.ScopeMask != b.Query.ScopeMask
                    || !SameTags(a.RequiredTags, b.RequiredTags) || !SameTags(a.RequiredAnyTags, b.RequiredAnyTags)
                    || !SameTags(a.BlockedTags, b.BlockedTags)) { return false; }
            }
            return true;
        }

        static bool SameTags(TagSet first, TagSet second)
        {
            return first.ContainsAll(second) && second.ContainsAll(first);
        }

        internal sealed class ActorEffectPreparation
        {
            readonly CombatActor _actor;
            readonly SortedDictionary<string, ModifierInstance[]> _sources;
            readonly ModifierInstance[] _modifiers;
            readonly StatBlock _stats;
            readonly TagSet _statusTags;
            readonly TagSet _tags;
            readonly StatusActionBlock _blocks;
            readonly bool _changed;
            readonly int _revision;
            readonly bool _alive;
            public CombatResourceSnapshot Before { get; }

            internal ActorEffectPreparation(CombatActor actor, SortedDictionary<string, ModifierInstance[]> sources,
                ModifierInstance[] modifiers, StatBlock stats, TagSet statusTags, TagSet tags, StatusActionBlock blocks, bool changed)
            {
                _actor = actor; _sources = sources; _modifiers = modifiers; _stats = stats;
                _statusTags = statusTags; _tags = tags; _blocks = blocks; _changed = changed;
                _revision = actor.StatsRevision; _alive = actor.IsAlive; Before = actor.Resources;
            }

            public bool IsCurrent => _actor != null && _actor.StatsRevision == _revision
                && _actor.IsAlive == _alive && _actor.Resources.Equals(Before);

            public void Apply()
            {
                if (!IsCurrent) { throw new InvalidOperationException("角色属性或资源已变化"); }
                _actor._modifierSources.Clear();
                foreach (var pair in _sources) { _actor._modifierSources.Add(pair.Key, pair.Value); }
                _actor._statusTags = _statusTags;
                if (!_changed) { return; }
                _actor._modifiers.Clear();
                _actor._modifiers.AddRange(_modifiers);
                _actor._stats = _stats;
                _actor._tags = _tags;
                _actor.BlockedActions = _blocks;
                _actor._currentHealth = _actor.MaxHealth * (Before.MaxHealth > 0 ? Before.CurrentHealth / Before.MaxHealth : 1);
                _actor._currentMana = _actor.MaxMana * (Before.MaxMana > 0 ? Before.CurrentMana / Before.MaxMana : 1);
                _actor.StatsRevision++;
            }
        }
    }
}
