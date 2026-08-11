using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Flags]
    public enum CombatTagScope
    {
        [LabelText("无")]
        None = 0,

        [LabelText("来源角色")]
        SourceActor = 1 << 0,

        [LabelText("目标角色")]
        TargetActor = 1 << 1,

        [LabelText("技能")]
        Skill = 1 << 2,

        [LabelText("来源物品")]
        SourceItem = 1 << 3,

        [LabelText("本次攻击")]
        Attack = 1 << 4,

        [LabelText("伤害")]
        Damage = 1 << 5,

        [LabelText("全部战斗作用域")]
        All = SourceActor | TargetActor | Skill | SourceItem | Attack | Damage
    }

    [Serializable]
    public sealed class TagQueryDefinition
    {
        [SerializeField]
        [EnumToggleButtons]
        [LabelText("查询作用域")]
        CombatTagScope _scopeMask;

        [SerializeField]
        [LabelText("必须全部包含")]
        List<TagDefinition> _requiredAll = new List<TagDefinition>();

        [SerializeField]
        [LabelText("必须包含任一")]
        List<TagDefinition> _requiredAny = new List<TagDefinition>();

        [SerializeField]
        [LabelText("禁止包含任一")]
        List<TagDefinition> _blockedAny = new List<TagDefinition>();

        public CombatTagScope ScopeMask => _scopeMask;

        public IReadOnlyList<TagDefinition> RequiredAll => _requiredAll;

        public IReadOnlyList<TagDefinition> RequiredAny => _requiredAny;

        public IReadOnlyList<TagDefinition> BlockedAny => _blockedAny;

        public bool HasConditions => Count(_requiredAll) > 0 || Count(_requiredAny) > 0 || Count(_blockedAny) > 0;

        public TagQuery CreateQuery()
        {
            return new TagQuery(
                _scopeMask,
                TagSet.FromDefinitions(_requiredAll),
                TagSet.FromDefinitions(_requiredAny),
                TagSet.FromDefinitions(_blockedAny));
        }

        static int Count<T>(IReadOnlyCollection<T> values)
        {
            return values != null ? values.Count : 0;
        }
    }

    public sealed class TagQuery
    {
        public static TagQuery Empty { get; } = new TagQuery(
            CombatTagScope.None,
            TagSet.Empty,
            TagSet.Empty,
            TagSet.Empty);

        public CombatTagScope ScopeMask { get; }

        public TagSet RequiredAll { get; }

        public TagSet RequiredAny { get; }

        public TagSet BlockedAny { get; }

        public bool HasConditions => !RequiredAll.IsEmpty || !RequiredAny.IsEmpty || !BlockedAny.IsEmpty;

        public TagQuery(
            CombatTagScope scopeMask,
            TagSet requiredAll,
            TagSet requiredAny,
            TagSet blockedAny)
        {
            ScopeMask = scopeMask;
            RequiredAll = Clone(requiredAll);
            RequiredAny = Clone(requiredAny);
            BlockedAny = Clone(blockedAny);
        }

        public bool Matches(CombatTagContext context)
        {
            return TryMatch(context, out _);
        }

        public bool TryMatch(CombatTagContext context, out string failureReason)
        {
            CombatTagContext safeContext = context ?? CombatTagContext.Empty;
            string querySummary = ToDebugString();
            string contextSummary = safeContext.ToDebugString(ScopeMask);

            foreach (string id in RequiredAll.Ids)
            {
                if (!safeContext.Contains(ScopeMask, id))
                {
                    failureReason = $"{querySummary}; RequiredAll 缺少 '{id}'; Context={contextSummary}";
                    return false;
                }
            }

            if (!RequiredAny.IsEmpty)
            {
                bool found = false;

                foreach (string id in RequiredAny.Ids)
                {
                    if (safeContext.Contains(ScopeMask, id))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    failureReason = $"{querySummary}; RequiredAny 未命中; Context={contextSummary}";
                    return false;
                }
            }

            foreach (string id in BlockedAny.Ids)
            {
                if (safeContext.Contains(ScopeMask, id))
                {
                    failureReason = $"{querySummary}; BlockedAny 命中 '{id}'; Context={contextSummary}";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        public bool Matches(TagSet flatTags)
        {
            TagSet safeTags = flatTags ?? TagSet.Empty;

            if (!RequiredAll.IsEmpty && !safeTags.ContainsAll(RequiredAll))
            {
                return false;
            }

            if (!RequiredAny.IsEmpty && !safeTags.ContainsAny(RequiredAny))
            {
                return false;
            }

            return BlockedAny.IsEmpty || !safeTags.ContainsAny(BlockedAny);
        }

        public string ToDebugString()
        {
            return $"Scope={ScopeMask}; RequiredAll={RequiredAll.ToDebugString()}; "
                + $"RequiredAny={RequiredAny.ToDebugString()}; BlockedAny={BlockedAny.ToDebugString()}";
        }

        static TagSet Clone(TagSet tags)
        {
            return tags != null ? new TagSet(tags.Ids) : TagSet.Empty;
        }
    }

    public static class CombatTagScopeRules
    {
        public static bool SupportsDomain(CombatTagScope scopeMask, CombatTagDomain domain)
        {
            if (domain == CombatTagDomain.Actor)
            {
                return (scopeMask & (CombatTagScope.SourceActor | CombatTagScope.TargetActor)) != 0;
            }

            if (domain == CombatTagDomain.Skill)
            {
                return (scopeMask & CombatTagScope.Skill) != 0;
            }

            if (domain == CombatTagDomain.ItemSpawn)
            {
                return (scopeMask & CombatTagScope.SourceItem) != 0;
            }

            if (domain == CombatTagDomain.Attack)
            {
                return (scopeMask & CombatTagScope.Attack) != 0;
            }

            return domain == CombatTagDomain.Damage && (scopeMask & CombatTagScope.Damage) != 0;
        }
    }
}
