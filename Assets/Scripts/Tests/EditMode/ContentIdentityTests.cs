using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class ContentIdentityTests
    {
        readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdAssets.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void ContentId_ParsesCanonicalValueAndUsesOrdinalIdentity()
        {
            ContentId contentId = ContentId.Parse("item:great_sword");
            ContentId same = new ContentId(ContentNamespaces.Item, "great_sword");

            Assert.AreEqual(ContentNamespaces.Item, contentId.Namespace);
            Assert.AreEqual("great_sword", contentId.LocalId);
            Assert.AreEqual("item:great_sword", contentId.ToString());
            Assert.AreEqual(contentId, same);
            Assert.AreEqual(contentId.GetHashCode(), same.GetHashCode());
            Assert.AreNotEqual(contentId, new ContentId(ContentNamespaces.Item, "war_axe"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("item")]
        [TestCase("item:")]
        [TestCase(":great_sword")]
        [TestCase("Item:great_sword")]
        [TestCase("item:GreatSword")]
        [TestCase("item:great-sword")]
        [TestCase("item:great__sword")]
        [TestCase("item:great_sword:")]
        public void ContentId_RejectsNonCanonicalValues(string value)
        {
            Assert.IsFalse(ContentId.TryParse(value, out _));
            Assert.Throws<FormatException>(() => ContentId.Parse(value));
        }

        [Test]
        public void ContentDefinitionRegistry_ContainsEveryOfficialNamespaceExactlyOnce()
        {
            string[] expectedNamespaces =
            {
                ContentNamespaces.Tag,
                ContentNamespaces.Stat,
                ContentNamespaces.Affix,
                ContentNamespaces.Item,
                ContentNamespaces.Actor,
                ContentNamespaces.Skill,
                ContentNamespaces.Monster,
                ContentNamespaces.MonsterAffix,
                ContentNamespaces.Loot,
                ContentNamespaces.Spawn,
                ContentNamespaces.Trader,
                ContentNamespaces.Crafting,
            };

            CollectionAssert.AreEquivalent(
                expectedNamespaces,
                ContentDefinitionRegistry.All.Select(metadata => metadata.Namespace).ToArray());
            Assert.AreEqual(
                ContentDefinitionRegistry.All.Count,
                ContentDefinitionRegistry.All.Select(metadata => metadata.DefinitionType).Distinct().Count());
        }

        [Test]
        public void EveryDarkFlareDataConfiguration_IsExplicitlyRegistered()
        {
            Type[] runtimeTypes = typeof(GameArchitecture).Assembly.GetTypes();
            List<string> missing = new List<string>();

            for (int i = 0; i < runtimeTypes.Length; i++)
            {
                Type type = runtimeTypes[i];
                CreateAssetMenuAttribute createAssetMenu = type.GetCustomAttribute<CreateAssetMenuAttribute>();

                if (type.IsAbstract
                    || !typeof(ScriptableObject).IsAssignableFrom(type)
                    || createAssetMenu == null
                    || string.IsNullOrEmpty(createAssetMenu.menuName)
                    || !createAssetMenu.menuName.StartsWith("DarkFlare/Data/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!typeof(IContentDefinition).IsAssignableFrom(type)
                    || !ContentDefinitionRegistry.TryGet(type, out _))
                {
                    missing.Add(type.FullName);
                }
            }

            Assert.IsEmpty(missing, string.Join(Environment.NewLine, missing));
        }

        [Test]
        public void ContentCatalog_BuildsImmutableTypedLookup()
        {
            ItemBaseDefinition item = CreateDefinition<ItemBaseDefinition>("great_sword");
            MonsterDefinition monster = CreateDefinition<MonsterDefinition>("wasteland_wraith");
            ContentCatalogDefinition definition = CreateCatalog(item, monster);

            ContentCatalogBuildResult build = ContentCatalog.Build(definition);

            Assert.IsTrue(build.Succeeded, Describe(build.Issues));
            Assert.AreEqual(2, build.Catalog.Count);
            Assert.AreEqual("core", build.Catalog.CatalogId);
            Assert.AreEqual(1, build.Catalog.ContentVersion);
            Assert.IsTrue(
                build.Catalog.TryResolve(
                    new ContentId(ContentNamespaces.Item, "great_sword"),
                    out ItemBaseDefinition resolvedItem));
            Assert.AreSame(item, resolvedItem);
            Assert.AreEqual(1, build.Catalog.GetAll<ItemBaseDefinition>().Count);
            Assert.IsTrue(build.Catalog.TryGetContentId(item, out ContentId reverseId));
            Assert.AreEqual(new ContentId(ContentNamespaces.Item, "great_sword"), reverseId);

            ContentResolveResult<MonsterDefinition> mismatch = build.Catalog.Resolve<MonsterDefinition>(
                new ContentId(ContentNamespaces.Item, "great_sword"));
            Assert.AreEqual(ContentResolveCode.TypeMismatch, mismatch.Code);
            Assert.AreEqual(typeof(ItemBaseDefinition), mismatch.ActualType);
            Assert.AreEqual(MissingContentPolicy.BlockLoad, mismatch.MissingPolicy);
        }

        [Test]
        public void ContentCatalog_RejectsDuplicateAndInvalidEntriesWithoutPartialCatalog()
        {
            ItemBaseDefinition first = CreateDefinition<ItemBaseDefinition>("great_sword");
            ItemBaseDefinition duplicate = CreateDefinition<ItemBaseDefinition>("great_sword");
            MonsterDefinition invalid = CreateDefinition<MonsterDefinition>("Invalid Monster");
            ContentCatalogDefinition definition = CreateCatalog(first, duplicate, invalid, null);

            ContentCatalogBuildResult build = ContentCatalog.Build(definition);

            Assert.IsFalse(build.Succeeded);
            Assert.IsNull(build.Catalog);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    ContentCatalogIssueCode.ContentIdDuplicate,
                    ContentCatalogIssueCode.ContentIdInvalid,
                    ContentCatalogIssueCode.EntryMissing,
                },
                build.Issues.Select(issue => issue.Code).ToArray());
        }

        [Test]
        public void OfficialContentCatalog_ContainsEveryRegisteredPresetDefinition()
        {
            const string CatalogPath = "Assets/Data/Preset/Content/正式内容目录.asset";
            ContentCatalogDefinition definition =
                AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(CatalogPath);

            Assert.IsNotNull(definition, $"缺少正式内容目录：{CatalogPath}");
            ContentCatalogBuildResult build = ContentCatalog.Build(definition);
            Assert.IsTrue(build.Succeeded, Describe(build.Issues));

            string[] guids = AssetDatabase.FindAssets(
                "t:ScriptableObject",
                new[] { "Assets/Data/Preset" });
            List<ScriptableObject> registeredAssets = new List<ScriptableObject>();

            for (int i = 0; i < guids.Length; i++)
            {
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));

                if (asset is IContentDefinition)
                {
                    registeredAssets.Add(asset);
                }
            }

            Assert.AreEqual(registeredAssets.Count, build.Catalog.Count);

            for (int i = 0; i < registeredAssets.Count; i++)
            {
                Assert.IsTrue(
                    build.Catalog.Contains(registeredAssets[i]),
                    $"目录缺少 {AssetDatabase.GetAssetPath(registeredAssets[i])}");
            }

            Assert.AreEqual(
                "wasteland_wraith",
                AssetDatabase.LoadAssetAtPath<LootTableDefinition>(
                    "Assets/Data/Preset/Loot/基础怪物掉落表.asset").Id);
            Assert.AreEqual(
                "main",
                AssetDatabase.LoadAssetAtPath<MonsterSpawnDefinition>(
                    "Assets/Data/Preset/Monsters/基础刷怪表.asset").Id);
            Assert.AreEqual(
                "default",
                AssetDatabase.LoadAssetAtPath<CraftingDefinition>(
                    "Assets/Data/Preset/Crafting/基础打造配置.asset").Id);
        }

        T CreateDefinition<T>(string id) where T : ScriptableObject
        {
            T definition = ScriptableObject.CreateInstance<T>();
            SetField(definition, "_id", id);
            _createdAssets.Add(definition);
            return definition;
        }

        ContentCatalogDefinition CreateCatalog(params ScriptableObject[] entries)
        {
            ContentCatalogDefinition definition = ScriptableObject.CreateInstance<ContentCatalogDefinition>();
            SetField(definition, "_catalogId", "core");
            SetField(definition, "_contentVersion", 1);
            SetField(definition, "_entries", new List<ScriptableObject>(entries));
            _createdAssets.Add(definition);
            return definition;
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 不存在");
            field.SetValue(target, value);
        }

        static string Describe(IReadOnlyList<ContentCatalogIssue> issues)
        {
            return string.Join(
                Environment.NewLine,
                issues.Select(issue => $"{issue.Code}: {issue.Message}"));
        }
    }
}
