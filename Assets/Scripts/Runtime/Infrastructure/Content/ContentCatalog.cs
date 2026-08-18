using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace DarkFlare
{
    public enum ContentCatalogIssueCode
    {
        DefinitionMissing,
        CatalogIdInvalid,
        ContentVersionInvalid,
        EntryMissing,
        DefinitionContractMissing,
        DefinitionTypeUnregistered,
        ContentIdInvalid,
        ContentIdDuplicate,
    }

    public sealed class ContentCatalogIssue
    {
        public ContentCatalogIssueCode Code { get; }

        public string Message { get; }

        public UnityEngine.Object Context { get; }

        public ContentCatalogIssue(
            ContentCatalogIssueCode code,
            string message,
            UnityEngine.Object context = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Context = context;
        }
    }

    public sealed class ContentCatalogBuildResult
    {
        public ContentCatalog Catalog { get; }

        public IReadOnlyList<ContentCatalogIssue> Issues { get; }

        public bool Succeeded => Catalog != null && Issues.Count == 0;

        internal ContentCatalogBuildResult(
            ContentCatalog catalog,
            IReadOnlyList<ContentCatalogIssue> issues)
        {
            Catalog = catalog;
            Issues = issues;
        }
    }

    public enum ContentResolveCode
    {
        Success,
        InvalidId,
        Missing,
        TypeMismatch,
    }

    public readonly struct ContentResolveResult<T> where T : ScriptableObject, IContentDefinition
    {
        public ContentResolveCode Code { get; }

        public ContentId ContentId { get; }

        public T Value { get; }

        public Type ActualType { get; }

        public MissingContentPolicy MissingPolicy { get; }

        public bool Succeeded => Code == ContentResolveCode.Success;

        internal ContentResolveResult(
            ContentResolveCode code,
            ContentId contentId,
            T value,
            Type actualType,
            MissingContentPolicy missingPolicy)
        {
            Code = code;
            ContentId = contentId;
            Value = value;
            ActualType = actualType;
            MissingPolicy = missingPolicy;
        }
    }

    public sealed class ContentCatalog
    {
        readonly IReadOnlyDictionary<ContentId, ScriptableObject> _entries;
        readonly IReadOnlyDictionary<Type, IReadOnlyList<ScriptableObject>> _entriesByType;
        readonly IReadOnlyDictionary<ScriptableObject, ContentId> _idsByEntry;

        public string CatalogId { get; }

        public int ContentVersion { get; }

        public ContentVersion Version => new ContentVersion(ContentVersion);

        public int Count => _entries.Count;

        ContentCatalog(
            string catalogId,
            int contentVersion,
            IReadOnlyDictionary<ContentId, ScriptableObject> entries,
            IReadOnlyDictionary<Type, IReadOnlyList<ScriptableObject>> entriesByType,
            IReadOnlyDictionary<ScriptableObject, ContentId> idsByEntry)
        {
            CatalogId = catalogId;
            ContentVersion = contentVersion;
            _entries = entries;
            _entriesByType = entriesByType;
            _idsByEntry = idsByEntry;
        }

        public static ContentCatalogBuildResult Build(ContentCatalogDefinition definition)
        {
            List<ContentCatalogIssue> issues = new List<ContentCatalogIssue>();

            if (definition == null)
            {
                issues.Add(new ContentCatalogIssue(
                    ContentCatalogIssueCode.DefinitionMissing,
                    "内容目录定义为空"));
                return Failed(issues);
            }

            if (!ContentId.IsValidSegment(definition.CatalogId))
            {
                issues.Add(new ContentCatalogIssue(
                    ContentCatalogIssueCode.CatalogIdInvalid,
                    $"内容目录 ID 非法：{definition.CatalogId}",
                    definition));
            }

            if (definition.ContentVersion <= 0)
            {
                issues.Add(new ContentCatalogIssue(
                    ContentCatalogIssueCode.ContentVersionInvalid,
                    $"内容版本必须大于 0：{definition.ContentVersion}",
                    definition));
            }

            Dictionary<ContentId, ScriptableObject> entries = new Dictionary<ContentId, ScriptableObject>();
            Dictionary<Type, List<ScriptableObject>> mutableByType = new Dictionary<Type, List<ScriptableObject>>();
            Dictionary<ScriptableObject, ContentId> idsByEntry = new Dictionary<ScriptableObject, ContentId>();

            for (int i = 0; i < definition.Entries.Count; i++)
            {
                ScriptableObject entry = definition.Entries[i];

                if (entry == null)
                {
                    issues.Add(new ContentCatalogIssue(
                        ContentCatalogIssueCode.EntryMissing,
                        $"内容目录第 {i} 项为空",
                        definition));
                    continue;
                }

                if (entry is not IContentDefinition contentDefinition)
                {
                    issues.Add(new ContentCatalogIssue(
                        ContentCatalogIssueCode.DefinitionContractMissing,
                        $"内容资产未实现 IContentDefinition：{entry.GetType().FullName}",
                        entry));
                    continue;
                }

                Type entryType = entry.GetType();

                if (!ContentDefinitionRegistry.TryGet(entryType, out ContentDefinitionMetadata metadata))
                {
                    issues.Add(new ContentCatalogIssue(
                        ContentCatalogIssueCode.DefinitionTypeUnregistered,
                        $"内容类型未登记：{entryType.FullName}",
                        entry));
                    continue;
                }

                if (!ContentId.TryCreate(metadata.Namespace, contentDefinition.Id, out ContentId contentId))
                {
                    issues.Add(new ContentCatalogIssue(
                        ContentCatalogIssueCode.ContentIdInvalid,
                        $"内容 ID 非法：{metadata.Namespace}:{contentDefinition.Id}",
                        entry));
                    continue;
                }

                if (entries.TryGetValue(contentId, out ScriptableObject duplicate))
                {
                    issues.Add(new ContentCatalogIssue(
                        ContentCatalogIssueCode.ContentIdDuplicate,
                        $"内容 ID 重复：{contentId}，首次出现于 {duplicate.name}",
                        entry));
                    continue;
                }

                entries.Add(contentId, entry);
                idsByEntry.Add(entry, contentId);

                if (!mutableByType.TryGetValue(entryType, out List<ScriptableObject> typedEntries))
                {
                    typedEntries = new List<ScriptableObject>();
                    mutableByType.Add(entryType, typedEntries);
                }

                typedEntries.Add(entry);
            }

            if (issues.Count > 0)
            {
                return Failed(issues);
            }

            Dictionary<Type, IReadOnlyList<ScriptableObject>> entriesByType =
                new Dictionary<Type, IReadOnlyList<ScriptableObject>>();

            foreach (KeyValuePair<Type, List<ScriptableObject>> pair in mutableByType)
            {
                entriesByType.Add(pair.Key, pair.Value.AsReadOnly());
            }

            ContentCatalog catalog = new ContentCatalog(
                definition.CatalogId,
                definition.ContentVersion,
                new ReadOnlyDictionary<ContentId, ScriptableObject>(entries),
                new ReadOnlyDictionary<Type, IReadOnlyList<ScriptableObject>>(entriesByType),
                new ReadOnlyDictionary<ScriptableObject, ContentId>(idsByEntry));
            return new ContentCatalogBuildResult(catalog, Array.Empty<ContentCatalogIssue>());
        }

        public bool Contains(ScriptableObject entry)
        {
            return entry != null && _idsByEntry.ContainsKey(entry);
        }

        public bool TryGetContentId(ScriptableObject entry, out ContentId contentId)
        {
            if (entry != null && _idsByEntry.TryGetValue(entry, out contentId))
            {
                return true;
            }

            contentId = default;
            return false;
        }

        public ContentResolveResult<T> Resolve<T>(ContentId contentId)
            where T : ScriptableObject, IContentDefinition
        {
            ContentDefinitionRegistry.TryGet(typeof(T), out ContentDefinitionMetadata expectedMetadata);
            MissingContentPolicy missingPolicy = expectedMetadata != null
                ? expectedMetadata.MissingPolicy
                : MissingContentPolicy.BlockLoad;

            if (!contentId.IsValid)
            {
                return new ContentResolveResult<T>(
                    ContentResolveCode.InvalidId,
                    contentId,
                    null,
                    null,
                    missingPolicy);
            }

            if (!_entries.TryGetValue(contentId, out ScriptableObject entry))
            {
                return new ContentResolveResult<T>(
                    ContentResolveCode.Missing,
                    contentId,
                    null,
                    null,
                    missingPolicy);
            }

            if (entry is not T typedEntry)
            {
                return new ContentResolveResult<T>(
                    ContentResolveCode.TypeMismatch,
                    contentId,
                    null,
                    entry.GetType(),
                    missingPolicy);
            }

            return new ContentResolveResult<T>(
                ContentResolveCode.Success,
                contentId,
                typedEntry,
                entry.GetType(),
                missingPolicy);
        }

        public bool TryResolve<T>(ContentId contentId, out T value)
            where T : ScriptableObject, IContentDefinition
        {
            ContentResolveResult<T> result = Resolve<T>(contentId);
            value = result.Value;
            return result.Succeeded;
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IContentDefinition
        {
            if (!_entriesByType.TryGetValue(typeof(T), out IReadOnlyList<ScriptableObject> entries))
            {
                return Array.Empty<T>();
            }

            T[] result = new T[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                result[i] = (T)entries[i];
            }

            return Array.AsReadOnly(result);
        }

        static ContentCatalogBuildResult Failed(List<ContentCatalogIssue> issues)
        {
            return new ContentCatalogBuildResult(null, issues.AsReadOnly());
        }
    }
}
