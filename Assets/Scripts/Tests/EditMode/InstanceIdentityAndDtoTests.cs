using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class InstanceIdentityAndDtoTests
    {
        sealed class ForbiddenUnityReferenceDto : IPersistenceDto
        {
            public GameObject RuntimeObject { get; set; }
        }

        sealed class ForbiddenAssetLocatorDto : IPersistenceDto
        {
            public string AssetGuid { get; set; }
            public string ResourcePath { get; set; }
        }

        ContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            ContentCatalogDefinition definition = AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset");
            ContentCatalogBuildResult build = ContentCatalog.Build(definition);
            Assert.IsTrue(build.Succeeded, string.Join("\n", build.Issues.Select(issue => issue.Message)));
            _catalog = build.Catalog;
        }

        [Test]
        public void InstanceIdGenerators_ProduceTypedUniqueAndDeterministicIdentities()
        {
            DeterministicItemInstanceIdGenerator first = new DeterministicItemInstanceIdGenerator(42);
            DeterministicItemInstanceIdGenerator replay = new DeterministicItemInstanceIdGenerator(42);
            ItemInstanceId firstId = first.Next();
            ItemInstanceId secondId = first.Next();

            Assert.IsTrue(firstId.IsCanonical);
            Assert.AreNotEqual(firstId, secondId);
            Assert.AreEqual(firstId, replay.Next());
            Assert.AreEqual(secondId, replay.Next());
            Assert.AreEqual(PlayerId.LocalPlayer, new PlayerId("local_player"));
            Assert.Throws<ArgumentException>(() => new SaveSlotId("../slot"));

            RunId runId = new RunId("0123456789abcdef0123456789abcdef");
            RunInstanceIdGenerator runIds = new RunInstanceIdGenerator(runId);
            MonsterInstanceId monsterOne = runIds.NextMonsterId();
            MonsterInstanceId monsterTwo = runIds.NextMonsterId();
            WorldDropId dropOne = runIds.NextWorldDropId();

            Assert.AreEqual("0123456789abcdef0123456789abcdef:monster:1", monsterOne.Value);
            Assert.AreEqual("0123456789abcdef0123456789abcdef:monster:2", monsterTwo.Value);
            Assert.AreEqual("0123456789abcdef0123456789abcdef:drop:1", dropOne.Value);
        }

        [Test]
        public void PersistenceDtos_ContainOnlyPureSerializableTypes()
        {
            Type[] dtoTypes = typeof(IPersistenceDto).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(IPersistenceDto).IsAssignableFrom(type))
                .ToArray();
            List<string> violations = new List<string>();

            for (int i = 0; i < dtoTypes.Length; i++)
            {
                CollectForbiddenTypes(dtoTypes[i], dtoTypes[i].Name, new HashSet<Type>(), violations);
            }

            Assert.IsEmpty(violations, string.Join(Environment.NewLine, violations));
        }

        [Test]
        public void PersistenceDtoRule_RejectsNestedUnityReference()
        {
            List<string> violations = new List<string>();
            CollectForbiddenTypes(
                typeof(ForbiddenUnityReferenceDto),
                nameof(ForbiddenUnityReferenceDto),
                new HashSet<Type>(),
                violations);

            Assert.That(violations, Has.Some.Contains(nameof(GameObject)));
        }

        [Test]
        public void PersistenceDtoRule_RejectsAssetGuidAndResourcePathFields()
        {
            List<string> violations = new List<string>();
            CollectForbiddenTypes(
                typeof(ForbiddenAssetLocatorDto),
                nameof(ForbiddenAssetLocatorDto),
                new HashSet<Type>(),
                violations);

            Assert.That(violations, Has.Some.Contains(nameof(ForbiddenAssetLocatorDto.AssetGuid)));
            Assert.That(violations, Has.Some.Contains(nameof(ForbiddenAssetLocatorDto.ResourcePath)));
        }

        [Test]
        public void ItemMapper_RoundTripsRolledAffixesAndExactModifierValues()
        {
            ItemBaseDefinition itemDefinition = Resolve<ItemBaseDefinition>(ContentNamespaces.Item, "great_sword");
            ItemInstanceId id = ItemInstanceId.Parse("11111111111111111111111111111111");
            ItemInstance source = ItemGenerator.Generate(
                itemDefinition,
                _catalog.GetAll<AffixDefinition>(),
                new ItemGenerationOptions(id, 10, 123456, ItemRarity.Magic, 1, 1));
            source.Quality = 17;
            source.Durability = 0.625f;

            DtoMapResult<ItemInstanceDto> serialized = RuntimeStateMapper.ToDto(source, _catalog);
            Assert.IsTrue(serialized.Succeeded, Describe(serialized.Issues));
            DtoMapResult<ItemInstance> restored = RuntimeStateMapper.FromDto(serialized.Value, _catalog);
            Assert.IsTrue(restored.Succeeded, Describe(restored.Issues));

            Assert.AreEqual(source.Id, restored.Value.Id);
            Assert.AreSame(source.BaseDefinition, restored.Value.BaseDefinition);
            Assert.AreEqual(source.Rarity, restored.Value.Rarity);
            Assert.AreEqual(source.ItemLevel, restored.Value.ItemLevel);
            Assert.AreEqual(source.Seed, restored.Value.Seed);
            Assert.AreEqual(source.Quality, restored.Value.Quality);
            Assert.AreEqual(source.Durability, restored.Value.Durability);
            AssertAffixesEqual(source.Prefixes, restored.Value.Prefixes);
            AssertAffixesEqual(source.Suffixes, restored.Value.Suffixes);
            AssertModifiersEqual(source.ImplicitModifiers, restored.Value.ImplicitModifiers);
        }

        [Test]
        public void RuntimeStateMapper_RestoresClosedDetachedObjectGraphWithoutChangingIdentity()
        {
            ItemInstance weapon = CreateNormalItem("22222222222222222222222222222222", "great_sword", 101);
            ItemInstance armor = CreateNormalItem("33333333333333333333333333333333", "leather_armor", 102);
            ItemInstance merchantItem = CreateNormalItem("44444444444444444444444444444444", "iron_ring", 103);
            RuntimeStateDto dto = CreateState(weapon, armor, merchantItem);

            DtoMapResult<DetachedRuntimeState> result = RuntimeStateMapper.Restore(dto, _catalog);

            Assert.IsTrue(result.Succeeded, Describe(result.Issues));
            Assert.AreEqual(3, result.Value.Items.Count);
            Assert.AreSame(
                result.Value.Items[armor.Id],
                result.Value.Inventory.Placements.Keys.Single());
            Assert.AreSame(
                result.Value.Items[weapon.Id],
                result.Value.Equipment[0].Loadout.Get(EquipmentSlot.Weapon));
            Assert.AreSame(result.Value.Items[merchantItem.Id], result.Value.MerchantStock[0]);
        }

        [Test]
        public void RuntimeStateMapper_RejectsDuplicateDanglingAndIllegalSlotBeforeCommit()
        {
            ItemInstance weapon = CreateNormalItem("55555555555555555555555555555555", "great_sword", 201);
            ItemInstance armor = CreateNormalItem("66666666666666666666666666666666", "leather_armor", 202);
            ItemInstance merchantItem = CreateNormalItem("77777777777777777777777777777777", "iron_ring", 203);
            RuntimeStateDto dto = CreateState(weapon, armor, merchantItem);
            dto.Items.Add(dto.Items[0]);
            dto.Inventory.Placements[0].ItemInstanceId = "88888888888888888888888888888888";
            dto.Equipment[0].Entries[0].Slot = EquipmentSlot.Armor;

            DtoMapResult<DetachedRuntimeState> result = RuntimeStateMapper.Restore(dto, _catalog);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    DtoMapIssueCode.DuplicateInstanceId,
                    DtoMapIssueCode.DanglingReference,
                    DtoMapIssueCode.InvalidEquipmentSlot,
                },
                result.Issues.Select(issue => issue.Code).ToArray());
            Assert.IsNull(result.Value);
        }

        RuntimeStateDto CreateState(
            ItemInstance weapon,
            ItemInstance armor,
            ItemInstance merchantItem)
        {
            RuntimeStateDto dto = new RuntimeStateDto();
            dto.Items.Add(RuntimeStateMapper.ToDto(weapon, _catalog).Value);
            dto.Items.Add(RuntimeStateMapper.ToDto(armor, _catalog).Value);
            dto.Items.Add(RuntimeStateMapper.ToDto(merchantItem, _catalog).Value);
            dto.Inventory = new InventoryLayoutDto
            {
                Width = 10,
                Height = 6,
                Placements = new List<InventoryPlacementDto>
                {
                    new InventoryPlacementDto
                    {
                        ItemInstanceId = armor.Id.Value,
                        X = 0,
                        Y = 0,
                        Width = armor.BaseDefinition.GridSize.x,
                        Height = armor.BaseDefinition.GridSize.y,
                    },
                },
            };
            dto.Actors.Add(new ActorIdentityDto
            {
                Kind = ActorIdentityKind.Player,
                InstanceId = PlayerId.LocalPlayer.Value,
                DefinitionContentId = "actor:player",
            });
            dto.Equipment.Add(new EquipmentLoadoutDto
            {
                ActorInstanceId = PlayerId.LocalPlayer.Value,
                Entries = new List<EquipmentEntryDto>
                {
                    new EquipmentEntryDto
                    {
                        Slot = EquipmentSlot.Weapon,
                        ItemInstanceId = weapon.Id.Value,
                    },
                },
            });
            dto.Merchant = new MerchantInventoryDto
            {
                TraderContentId = "trader:basic_trader",
                OrderedItemInstanceIds = new List<string> { merchantItem.Id.Value },
            };
            return dto;
        }

        ItemInstance CreateNormalItem(string id, string localContentId, int seed)
        {
            ItemBaseDefinition definition = Resolve<ItemBaseDefinition>(ContentNamespaces.Item, localContentId);
            return definition.CreateInstance(
                ItemInstanceId.Parse(id),
                1,
                seed,
                ItemRarity.Normal);
        }

        T Resolve<T>(string contentNamespace, string localId)
            where T : ScriptableObject, IContentDefinition
        {
            ContentResolveResult<T> result = _catalog.Resolve<T>(new ContentId(contentNamespace, localId));
            Assert.IsTrue(result.Succeeded, $"无法解析 {contentNamespace}:{localId}");
            return result.Value;
        }

        static void CollectForbiddenTypes(
            Type type,
            string path,
            ISet<Type> visiting,
            List<string> violations)
        {
            if (IsAllowedLeaf(type))
            {
                return;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type)
                || type.Namespace != null && type.Namespace.StartsWith("Cysharp.Threading.Tasks", StringComparison.Ordinal)
                || typeof(Delegate).IsAssignableFrom(type))
            {
                violations.Add($"{path}: {type.FullName}");
                return;
            }

            if (type.IsArray)
            {
                CollectForbiddenTypes(type.GetElementType(), $"{path}[]", visiting, violations);
                return;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                CollectForbiddenTypes(type.GetGenericArguments()[0], $"{path}[]", visiting, violations);
                return;
            }

            if (!typeof(IPersistenceDto).IsAssignableFrom(type))
            {
                violations.Add($"{path}: {type.FullName}");
                return;
            }

            if (!visiting.Add(type))
            {
                return;
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            for (int i = 0; i < properties.Length; i++)
            {
                if (IsForbiddenAssetLocatorName(properties[i].Name))
                {
                    violations.Add($"{path}.{properties[i].Name}: DTO 不得保存资源 GUID、地址或路径");
                    continue;
                }

                CollectForbiddenTypes(
                    properties[i].PropertyType,
                    $"{path}.{properties[i].Name}",
                    visiting,
                    violations);
            }

            visiting.Remove(type);
        }

        static bool IsForbiddenAssetLocatorName(string name)
        {
            return name.IndexOf("Guid", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Address", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("AssetReference", StringComparison.OrdinalIgnoreCase) >= 0
                || name.EndsWith("Path", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsAllowedLeaf(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal);
        }

        static void AssertAffixesEqual(
            IReadOnlyList<AffixInstance> expected,
            IReadOnlyList<AffixInstance> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreSame(expected[i].Definition, actual[i].Definition);
                AssertModifiersEqual(expected[i].Modifiers, actual[i].Modifiers);
            }
        }

        static void AssertModifiersEqual(
            IReadOnlyList<ModifierInstance> expected,
            IReadOnlyList<ModifierInstance> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].StatId, actual[i].StatId);
                Assert.AreEqual(expected[i].Operation, actual[i].Operation);
                Assert.AreEqual(expected[i].Scope, actual[i].Scope);
                Assert.AreEqual(expected[i].Value, actual[i].Value);
                Assert.AreEqual(expected[i].FromDamageType, actual[i].FromDamageType);
                Assert.AreEqual(expected[i].ToDamageType, actual[i].ToDamageType);
                Assert.AreEqual(expected[i].Query.ScopeMask, actual[i].Query.ScopeMask);
                CollectionAssert.AreEquivalent(expected[i].Query.RequiredAll.Ids, actual[i].Query.RequiredAll.Ids);
                CollectionAssert.AreEquivalent(expected[i].Query.RequiredAny.Ids, actual[i].Query.RequiredAny.Ids);
                CollectionAssert.AreEquivalent(expected[i].Query.BlockedAny.Ids, actual[i].Query.BlockedAny.Ids);
            }
        }

        static string Describe(IReadOnlyList<DtoMapIssue> issues)
        {
            return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }
    }
}
