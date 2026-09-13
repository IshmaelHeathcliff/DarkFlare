using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace DarkFlare.Tests
{
    public sealed class SaveRestorePreparerTests
    {
        ContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            ContentCatalogDefinition definition = AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset");
            ContentCatalogBuildResult build = ContentCatalog.Build(definition);
            Assert.IsTrue(
                build.Succeeded,
                string.Join("\n", build.Issues.Select(issue => issue.Message)));
            _catalog = build.Catalog;
        }

        [Test]
        public void Prepare_ResolvesClosedGraphAndPreservesExactContinuationState()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();

            PreparedRestoreResult result = SaveRestorePreparer.Prepare(document, _catalog);

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.AreEqual("player", result.Value.PlayerDefinition.Id);
            Assert.AreEqual("basic_projectile", result.Value.PlayerSkill.Id);
            Assert.AreEqual("main", result.Value.SpawnDefinition.Id);
            ItemInstance[] coins = result.Value.RuntimeState.Inventory.Placements.Keys.Where(item => item.BaseDefinition.ItemType == ItemType.Currency).ToArray();
            Assert.AreEqual(document.Payload.Items.Count + coins.Length, result.Value.RuntimeState.Items.Count);
            Assert.AreEqual(document.Payload.Profile.Gold, coins.Sum(item => item.Quantity));
            Assert.AreEqual(document.Payload.Profile.Inventory.Placements.Count + coins.Length, result.Value.RuntimeState.Inventory.Placements.Count);
            Assert.AreEqual(
                "22222222222222222222222222222222",
                result.Value.PlayerEquipment.Get(EquipmentSlot.Weapon).Id.Value);
            Assert.AreEqual(
                "33333333333333333333333333333333",
                result.Value.RuntimeState.MerchantStock[0].Id.Value);
            Assert.AreSame(
                result.Value.RuntimeState.Items[ItemInstanceId.Parse(
                    "44444444444444444444444444444444")],
                result.Value.WorldDrops[0].Item);
            Assert.AreEqual(3, result.Value.InstanceIdState.NextMonsterSequence);
            Assert.AreEqual(5, result.Value.InstanceIdState.NextWorldDropSequence);
            Assert.AreEqual(
                document.Payload.Run.Random.Channels.Count,
                result.Value.RandomState.NextSequences.Count);
            Assert.AreEqual(
                document.Payload.Run.Monsters[0].RandomSeeds.RootSeed,
                result.Value.Monsters[0].Instance.RandomSeeds.RootSeed);
            Assert.AreEqual(
                30f,
                result.Value.Monsters[0].Instance.EffectiveStats.GetValue(StatIds.MaxHealth));
        }

        [Test]
        public void Prepare_AdditiveAilmentContentPreservesLegacyValuesAndSourceDocument()
        {
            SaveDocumentDto document = SaveRestorePreparer.Prepare(SaveDataContractTests.CreateValidDocument(), _catalog).Value.Document;
            document.Header.ContentVersion = 3;
            string before = JObject.FromObject(document).ToString();
            PreparedRestoreResult result = SaveRestorePreparer.Prepare(document, _catalog);
            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(result.Value.Document.Header.ContentVersion, Is.EqualTo(_catalog.ContentVersion));
            Assert.That(JObject.FromObject(document).ToString(), Is.EqualTo(before));
            Assert.That(JToken.DeepEquals(JToken.FromObject(document.Payload), JToken.FromObject(result.Value.Document.Payload)), Is.True);
            Assert.That(result.Value.Monsters[0].Instance.EffectiveStats.GetValue(StatIds.BurningResistance), Is.Zero);
        }

        [Test]
        public void Prepare_MigratesGoldIntoFullLegacyInventoryWithoutMovingExistingItems()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            InventoryPlacementDto existing = document.Payload.Profile.Inventory.Placements[0];
            document.Payload.Profile.Inventory.Width = existing.Width;
            document.Payload.Profile.Inventory.Height = existing.Height;
            JObject original = JObject.FromObject(document);
            PreparedRestoreResult result = SaveRestorePreparer.Prepare(document, _catalog);
            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.IsTrue(JToken.DeepEquals(original, JObject.FromObject(document)));
            ItemInstance originalItem = result.Value.RuntimeState.Items[ItemInstanceId.Parse(existing.ItemInstanceId)];
            UnityEngine.RectInt placement = result.Value.RuntimeState.Inventory.Placements[originalItem];
            Assert.AreEqual(existing.X, placement.x);
            Assert.AreEqual(existing.Y, placement.y);
            ItemInstance[] stacks = result.Value.RuntimeState.Inventory.Placements.Keys.Where(item => item.BaseDefinition.ItemType == ItemType.Currency).ToArray();
            Assert.AreEqual(existing.Height + (int)Math.Ceiling(stacks.Length / (double)existing.Width), result.Value.RuntimeState.Inventory.Height);
            Assert.AreEqual(document.Payload.Profile.Gold, stacks.Sum(item => item.Quantity));
            ItemInstance coins = stacks[0];
            DtoMapResult<ItemInstanceDto> saved = RuntimeStateMapper.ToDto(coins, _catalog);
            Assert.IsTrue(saved.Succeeded);
            Assert.AreEqual(coins.Quantity, saved.Value.Quantity);
            Assert.AreEqual(coins.Quantity, RuntimeStateMapper.FromDto(saved.Value, _catalog).Value.Quantity);
            saved.Value.Quantity = 0;
            Assert.IsFalse(RuntimeStateMapper.FromDto(saved.Value, _catalog).Succeeded);
        }

        [Test]
        public void Prepare_UpgradesOnlyKnownContentWithoutMutatingSourceOrRerollingItems()
        {
            ContentCatalogDefinition source = AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset");
            ContentCatalogDefinition target = UnityEngine.Object.Instantiate(source);
            try
            {
                SerializedObject serialized = new SerializedObject(target);
                serialized.FindProperty("_contentVersion").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ContentCatalogBuildResult build = ContentCatalog.Build(target);
                Assert.IsTrue(build.Succeeded);
                byte[] historicalBytes = System.IO.File.ReadAllBytes(
                    "Assets/Scripts/Tests/Fixtures/Migration/save-v1-core-v1-four-slots.json");
                SaveDeserializationResult historical = new NewtonsoftSaveSerializer().Deserialize(historicalBytes);
                Assert.IsTrue(historical.Succeeded);
                SaveDocumentDto document = historical.Document;
                document.Header.ContentVersion = 1;
                JObject original = JObject.FromObject(document);

                PreparedRestoreResult result = SaveRestorePreparer.Prepare(document, build.Catalog);

                Assert.IsTrue(result.Succeeded, Describe(result));
                Assert.AreEqual(2, result.Value.Document.Header.ContentVersion);
                Assert.IsTrue(JToken.DeepEquals(original, JObject.FromObject(document)), "预检不能修改调用方的旧档");
                Assert.IsTrue(JToken.DeepEquals(original["Payload"], JObject.FromObject(result.Value.Document.Payload)),
                    "迁移必须保留物品、已掷值、位置、金币和商人库存");
                Assert.IsNull(result.Value.PlayerEquipment.Get(EquipmentSlot.Head));
                Assert.IsNull(result.Value.PlayerEquipment.Get(EquipmentSlot.OffHand));
                foreach (EquipmentEntryDto entry in document.Payload.Profile.Equipment[0].Entries)
                {
                    Assert.AreEqual(entry.ItemInstanceId, result.Value.PlayerEquipment.Get(entry.Slot).Id.Value);
                }

                document.Header.ContentVersion = 3;
                Assert.IsFalse(SaveRestorePreparer.Prepare(document, build.Catalog).Succeeded);
                document.Header.ContentVersion = 1;
                document.Header.CatalogId = "other";
                Assert.IsFalse(SaveRestorePreparer.Prepare(document, build.Catalog).Succeeded);
                document.Header.CatalogId = "core";
                document.Payload.Profile.Equipment[0].Entries[0].Slot = EquipmentSlot.OffHand;
                Assert.IsFalse(SaveRestorePreparer.Prepare(document, build.Catalog).Succeeded,
                    "声明旧内容版本的文件不能携带新增槽位");
                Assert.AreEqual(1, document.Header.ContentVersion);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Prepare_RejectsCatalogMismatchAndDerivedMonsterStateMismatchBeforeCommit()
        {
            SaveDocumentDto catalogMismatch = SaveDataContractTests.CreateValidDocument();
            catalogMismatch.Header.ContentVersion = _catalog.ContentVersion + 1;

            PreparedRestoreResult incompatible = SaveRestorePreparer.Prepare(
                catalogMismatch,
                _catalog);

            Assert.IsFalse(incompatible.Succeeded);
            Assert.That(
                incompatible.Issues.Select(issue => issue.Code),
                Has.Some.EqualTo(DtoMapIssueCode.MissingContent));

            SaveDocumentDto statMismatch = SaveDataContractTests.CreateValidDocument();
            statMismatch.Payload.Run.Monsters[0].EffectiveStats[0].Value = 31f;

            PreparedRestoreResult invalid = SaveRestorePreparer.Prepare(statMismatch, _catalog);

            Assert.IsFalse(invalid.Succeeded);
            Assert.IsTrue(invalid.Issues.Any(issue =>
                issue.Code == DtoMapIssueCode.InvalidValue
                && issue.Path.EndsWith("effectiveStats", StringComparison.Ordinal)));
        }

        [Test]
        public void Prepare_RoundTripsAllEquipmentSlotsAndRejectsDuplicateOwnership()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            document = SaveRestorePreparer.Prepare(document, _catalog).Value.Document;
            document.Payload.Items.RemoveAll(item => item.InstanceId == "22222222222222222222222222222222");
            document.Payload.Profile.Equipment[0].Entries.Clear();
            ItemBaseDefinition[] definitions = AssetDatabase.FindAssets("t:ItemBaseDefinition", new[] { "Assets/Data/Preset/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemBaseDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            foreach (EquipmentSlot slot in EquipmentSlots.All)
            {
                ItemBaseDefinition definition = definitions.First(item => item.CanEquipTo(slot));
                ItemInstance item = definition.CreateInstance(Guid.NewGuid().ToString("N"), 1, 701 + (int)slot);
                DtoMapResult<ItemInstanceDto> dto = RuntimeStateMapper.ToDto(item, _catalog);
                Assert.IsTrue(dto.Succeeded);
                document.Payload.Items.Add(dto.Value);
                document.Payload.Profile.Equipment[0].Entries.Add(new EquipmentEntryDto { Slot = slot, ItemInstanceId = item.Id.Value });
            }
            document.Header.Summary.ItemCount = document.Payload.Items.Count;
            var serializer = new NewtonsoftSaveSerializer();
            SaveSerializationResult saved = serializer.Serialize(document);
            Assert.IsTrue(saved.Succeeded);
            SaveDeserializationResult loaded = serializer.Deserialize(saved.Bytes);
            Assert.IsTrue(loaded.Succeeded);
            PreparedRestoreResult restored = SaveRestorePreparer.Prepare(loaded.Document, _catalog);
            Assert.IsTrue(restored.Succeeded, Describe(restored));
            foreach (EquipmentEntryDto entry in document.Payload.Profile.Equipment[0].Entries)
            {
                ItemInstance actual = restored.Value.PlayerEquipment.Get(entry.Slot);
                Assert.AreEqual(entry.ItemInstanceId, actual.Id.Value);
                Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(document.Payload.Items.First(item => item.InstanceId == entry.ItemInstanceId)),
                    JObject.FromObject(RuntimeStateMapper.ToDto(actual, _catalog).Value)), "装备已掷数值必须精确保留");
            }
            document.Payload.Profile.Equipment[0].Entries[1].ItemInstanceId = document.Payload.Profile.Equipment[0].Entries[0].ItemInstanceId;
            JObject invalidSource = JObject.FromObject(document);
            Assert.IsFalse(SaveRestorePreparer.Prepare(document, _catalog).Succeeded);
            Assert.IsTrue(JToken.DeepEquals(invalidSource, JObject.FromObject(document)), "失败不能修改来源 DTO");
        }

        static string Describe(PreparedRestoreResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }
    }
}
