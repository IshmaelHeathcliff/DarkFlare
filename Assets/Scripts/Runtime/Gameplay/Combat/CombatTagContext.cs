using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class CombatTagContext
    {
        public static CombatTagContext Empty { get; } = new CombatTagContext();

        public TagSet SourceActorTags { get; }

        public TagSet TargetActorTags { get; }

        public TagSet SkillTags { get; }

        public TagSet SourceItemTags { get; }

        public TagSet AttackTags { get; }

        public TagSet DamageTags { get; }

        internal TagSet LegacyTags { get; }

        public CombatTagContext(
            TagSet sourceActorTags = null,
            TagSet targetActorTags = null,
            TagSet skillTags = null,
            TagSet sourceItemTags = null,
            TagSet attackTags = null,
            TagSet damageTags = null,
            TagSet legacyTags = null)
        {
            SourceActorTags = Clone(sourceActorTags);
            TargetActorTags = Clone(targetActorTags);
            SkillTags = Clone(skillTags);
            SourceItemTags = Clone(sourceItemTags);
            AttackTags = Clone(attackTags);
            DamageTags = Clone(damageTags);
            LegacyTags = Clone(legacyTags);
        }

        public CombatTagContext WithTargetActorTags(TagSet targetActorTags)
        {
            return new CombatTagContext(
                SourceActorTags,
                targetActorTags,
                SkillTags,
                SourceItemTags,
                AttackTags,
                DamageTags,
                LegacyTags);
        }

        public CombatTagContext WithDamageTags(TagSet damageTags)
        {
            return new CombatTagContext(
                SourceActorTags,
                TargetActorTags,
                SkillTags,
                SourceItemTags,
                AttackTags,
                damageTags,
                LegacyTags);
        }

        internal CombatTagContext WithDamageTags(TagSet damageTags, TagSet legacyDamageTags)
        {
            return new CombatTagContext(
                SourceActorTags,
                TargetActorTags,
                SkillTags,
                SourceItemTags,
                AttackTags,
                damageTags,
                LegacyTags.Union(legacyDamageTags));
        }

        public TagSet Flatten(CombatTagScope scopeMask)
        {
            HashSet<string> ids = new HashSet<string>();
            Add(ids, scopeMask, CombatTagScope.SourceActor, SourceActorTags);
            Add(ids, scopeMask, CombatTagScope.TargetActor, TargetActorTags);
            Add(ids, scopeMask, CombatTagScope.Skill, SkillTags);
            Add(ids, scopeMask, CombatTagScope.SourceItem, SourceItemTags);
            Add(ids, scopeMask, CombatTagScope.Attack, AttackTags);
            Add(ids, scopeMask, CombatTagScope.Damage, DamageTags);
            return ids.Count > 0 ? new TagSet(ids) : TagSet.Empty;
        }

        public string ToDebugString(CombatTagScope scopeMask = CombatTagScope.All)
        {
            List<string> parts = new List<string>();
            AddDebugPart(parts, scopeMask, CombatTagScope.SourceActor, nameof(SourceActorTags), SourceActorTags);
            AddDebugPart(parts, scopeMask, CombatTagScope.TargetActor, nameof(TargetActorTags), TargetActorTags);
            AddDebugPart(parts, scopeMask, CombatTagScope.Skill, nameof(SkillTags), SkillTags);
            AddDebugPart(parts, scopeMask, CombatTagScope.SourceItem, nameof(SourceItemTags), SourceItemTags);
            AddDebugPart(parts, scopeMask, CombatTagScope.Attack, nameof(AttackTags), AttackTags);
            AddDebugPart(parts, scopeMask, CombatTagScope.Damage, nameof(DamageTags), DamageTags);
            return parts.Count > 0 ? string.Join("; ", parts) : "Scope=None";
        }

        internal bool Contains(CombatTagScope scopeMask, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return IsMatch(scopeMask, CombatTagScope.SourceActor, SourceActorTags, id)
                || IsMatch(scopeMask, CombatTagScope.TargetActor, TargetActorTags, id)
                || IsMatch(scopeMask, CombatTagScope.Skill, SkillTags, id)
                || IsMatch(scopeMask, CombatTagScope.SourceItem, SourceItemTags, id)
                || IsMatch(scopeMask, CombatTagScope.Attack, AttackTags, id)
                || IsMatch(scopeMask, CombatTagScope.Damage, DamageTags, id);
        }

        static bool IsMatch(CombatTagScope scopeMask, CombatTagScope scope, TagSet tags, string id)
        {
            return (scopeMask & scope) != 0 && tags.Contains(id);
        }

        static void Add(HashSet<string> destination, CombatTagScope scopeMask, CombatTagScope scope, TagSet tags)
        {
            if ((scopeMask & scope) == 0)
            {
                return;
            }

            foreach (string id in tags.Ids)
            {
                destination.Add(id);
            }
        }

        static void AddDebugPart(
            List<string> destination,
            CombatTagScope scopeMask,
            CombatTagScope scope,
            string name,
            TagSet tags)
        {
            if ((scopeMask & scope) != 0)
            {
                destination.Add($"{name}={tags.ToDebugString()}");
            }
        }

        static TagSet Clone(TagSet tags)
        {
            return tags != null ? new TagSet(tags.Ids) : TagSet.Empty;
        }
    }
}
