using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DarkFlare
{
    public enum MissingContentPolicy
    {
        BlockLoad,
        SkipOptionalEntry,
        UseExplicitPlaceholder,
    }

    public static class ContentNamespaces
    {
        public const string Tag = "tag";
        public const string Stat = "stat";
        public const string Affix = "affix";
        public const string Item = "item";
        public const string Actor = "actor";
        public const string Skill = "skill";
        public const string Monster = "monster";
        public const string MonsterAffix = "monster_affix";
        public const string Loot = "loot";
        public const string Spawn = "spawn";
        public const string Trader = "trader";
        public const string Crafting = "crafting";
    }

    public interface IContentDefinition
    {
        string Id { get; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ContentDefinitionAttribute : Attribute
    {
        public string Namespace { get; }

        public MissingContentPolicy MissingPolicy { get; }

        public ContentDefinitionAttribute(
            string contentNamespace,
            MissingContentPolicy missingPolicy = MissingContentPolicy.BlockLoad)
        {
            if (!ContentId.IsValidSegment(contentNamespace))
            {
                throw new ArgumentException("内容命名空间必须使用小写 snake_case", nameof(contentNamespace));
            }

            Namespace = contentNamespace;
            MissingPolicy = missingPolicy;
        }
    }

    public sealed class ContentDefinitionMetadata
    {
        public Type DefinitionType { get; }

        public string Namespace { get; }

        public MissingContentPolicy MissingPolicy { get; }

        internal ContentDefinitionMetadata(
            Type definitionType,
            string contentNamespace,
            MissingContentPolicy missingPolicy)
        {
            DefinitionType = definitionType;
            Namespace = contentNamespace;
            MissingPolicy = missingPolicy;
        }
    }

    public static class ContentDefinitionRegistry
    {
        static readonly object SyncRoot = new object();

        static IReadOnlyList<ContentDefinitionMetadata> s_all;
        static IReadOnlyDictionary<Type, ContentDefinitionMetadata> s_byType;
        static IReadOnlyDictionary<string, ContentDefinitionMetadata> s_byNamespace;

        public static IReadOnlyList<ContentDefinitionMetadata> All
        {
            get
            {
                EnsureBuilt();
                return s_all;
            }
        }

        public static bool TryGet(Type definitionType, out ContentDefinitionMetadata metadata)
        {
            EnsureBuilt();

            if (definitionType == null)
            {
                metadata = null;
                return false;
            }

            return s_byType.TryGetValue(definitionType, out metadata);
        }

        public static bool TryGet(string contentNamespace, out ContentDefinitionMetadata metadata)
        {
            EnsureBuilt();

            if (contentNamespace == null)
            {
                metadata = null;
                return false;
            }

            return s_byNamespace.TryGetValue(contentNamespace, out metadata);
        }

        static void EnsureBuilt()
        {
            if (s_all != null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (s_all != null)
                {
                    return;
                }

                Build();
            }
        }

        static void Build()
        {
            Type contentInterface = typeof(IContentDefinition);
            Type scriptableObjectType = typeof(ScriptableObject);
            Type[] types = typeof(ContentDefinitionRegistry).Assembly.GetTypes();
            Dictionary<Type, ContentDefinitionMetadata> byType =
                new Dictionary<Type, ContentDefinitionMetadata>();
            Dictionary<string, ContentDefinitionMetadata> byNamespace =
                new Dictionary<string, ContentDefinitionMetadata>(StringComparer.Ordinal);

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];

                if (type.IsAbstract || !contentInterface.IsAssignableFrom(type))
                {
                    continue;
                }

                if (!scriptableObjectType.IsAssignableFrom(type))
                {
                    throw new InvalidOperationException($"内容定义必须继承 ScriptableObject：{type.FullName}");
                }

                ContentDefinitionAttribute attribute = type.GetCustomAttribute<ContentDefinitionAttribute>();

                if (attribute == null)
                {
                    throw new InvalidOperationException($"内容定义缺少 ContentDefinitionAttribute：{type.FullName}");
                }

                ContentDefinitionMetadata metadata = new ContentDefinitionMetadata(
                    type,
                    attribute.Namespace,
                    attribute.MissingPolicy);

                if (byNamespace.TryGetValue(attribute.Namespace, out ContentDefinitionMetadata duplicate))
                {
                    throw new InvalidOperationException(
                        $"内容命名空间 {attribute.Namespace} 同时登记到 {duplicate.DefinitionType.FullName} 和 {type.FullName}");
                }

                byType.Add(type, metadata);
                byNamespace.Add(attribute.Namespace, metadata);
            }

            ContentDefinitionMetadata[] ordered = byType.Values
                .OrderBy(metadata => metadata.Namespace, StringComparer.Ordinal)
                .ToArray();
            s_all = Array.AsReadOnly(ordered);
            s_byType = byType;
            s_byNamespace = byNamespace;
        }
    }
}
