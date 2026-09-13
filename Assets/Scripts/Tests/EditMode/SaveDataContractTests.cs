using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class SaveDataContractTests
    {
        const string RunValue = "0123456789abcdef0123456789abcdef";
        const string OtherRunValue = "fedcba9876543210fedcba9876543210";

        [Test]
        public void RunScopedIds_ParseStrictlyAndGeneratorResumesAtNextSequence()
        {
            RunId runId = RunId.Parse(RunValue);
            RunInstanceIdGenerator generator = new RunInstanceIdGenerator(runId);
            Assert.AreEqual($"{RunValue}:monster:1", generator.NextMonsterId().Value);
            Assert.AreEqual($"{RunValue}:monster:2", generator.NextMonsterId().Value);
            Assert.AreEqual($"{RunValue}:drop:1", generator.NextWorldDropId().Value);
            RunInstanceIdState captured = generator.CaptureState();

            Assert.AreEqual(3, captured.NextMonsterSequence);
            Assert.AreEqual(2, captured.NextWorldDropSequence);
            MonsterInstanceId parsedMonster = MonsterInstanceId.Parse($"{RunValue}:monster:2");
            WorldDropId parsedDrop = WorldDropId.Parse($"{RunValue}:drop:1");
            Assert.AreEqual(runId, parsedMonster.RunId);
            Assert.AreEqual(2, parsedMonster.Sequence);
            Assert.AreEqual(runId, parsedDrop.RunId);
            Assert.AreEqual(1, parsedDrop.Sequence);

            RunInstanceIdGenerator restored = new RunInstanceIdGenerator(captured);
            Assert.AreEqual($"{RunValue}:monster:3", restored.NextMonsterId().Value);
            Assert.AreEqual($"{RunValue}:drop:2", restored.NextWorldDropId().Value);
            Assert.IsFalse(MonsterInstanceId.TryParse($"{RunValue}:monster:02", out _));
            Assert.IsFalse(MonsterInstanceId.TryParse($"{RunValue}:drop:2", out _));
            Assert.IsFalse(WorldDropId.TryParse($"{RunValue}:drop:0", out _));
        }

        [Test]
        public void GameplayRandomState_RestoresEveryChannelAtExactContinuation()
        {
            GameplayRandomSystem source = new GameplayRandomSystem();
            source.Configure(true, 24680);
            source.NextSeed(GameplayRandomChannel.SpawnPosition);
            source.NextSeed(GameplayRandomChannel.MonsterInstance);
            source.NextSeed(GameplayRandomChannel.MonsterInstance);
            source.NextSeed(GameplayRandomChannel.Crafting);
            GameplayRandomState captured = source.CaptureState();
            Dictionary<GameplayRandomChannel, int[]> expected = new Dictionary<GameplayRandomChannel, int[]>();

            foreach (GameplayRandomChannel channel in Enum.GetValues(typeof(GameplayRandomChannel)))
            {
                expected.Add(channel, new[]
                {
                    source.NextSeed(channel),
                    source.NextSeed(channel),
                    source.NextSeed(channel),
                });
            }

            GameplayRandomSystem restored = new GameplayRandomSystem();
            restored.RestoreState(captured);

            foreach (GameplayRandomChannel channel in Enum.GetValues(typeof(GameplayRandomChannel)))
            {
                CollectionAssert.AreEqual(expected[channel], new[]
                {
                    restored.NextSeed(channel),
                    restored.NextSeed(channel),
                    restored.NextSeed(channel),
                });
            }
        }

        [Test]
        public void SaveDataValidator_AcceptsCompleteV1OwnershipAndRunState()
        {
            SaveDocumentDto document = CreateValidDocument();

            SaveDataValidationResult result = SaveDataValidator.ValidateDocument(document);

            Assert.IsTrue(result.Succeeded, Describe(result.Issues));
            Assert.AreEqual(SaveSchemaVersion.Current.Value, new SaveHeaderDto().SaveSchemaVersion);
        }

        [Test]
        public void SaveDataValidator_RejectsDuplicateOwnershipDanglingRandomAndSequenceConflicts()
        {
            SaveDocumentDto document = CreateValidDocument();
            document.Payload.Run.Merchant.OrderedItemInstanceIds.Add(
                document.Payload.Profile.Inventory.Placements[0].ItemInstanceId);
            document.Payload.Run.WorldDrops[0].ItemInstanceId =
                "ffffffffffffffffffffffffffffffff";
            document.Payload.Run.Random.Channels.RemoveAt(document.Payload.Run.Random.Channels.Count - 1);
            document.Payload.Run.Random.Channels.Add(new GameplayRandomChannelStateDto
            {
                Channel = document.Payload.Run.Random.Channels[0].Channel,
                NextSequence = 3,
            });
            document.Payload.Run.Monsters[0].InstanceId = $"{OtherRunValue}:monster:2";
            document.Payload.Run.InstanceIds.NextMonsterSequence = 1;
            document.Payload.Run.Player.Position.X = float.NaN;

            SaveDataValidationResult result = SaveDataValidator.ValidateDocument(document);
            SaveDataIssueCode[] codes = result.Issues.Select(issue => issue.Code).ToArray();

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    SaveDataIssueCode.DuplicateOwnership,
                    SaveDataIssueCode.DanglingReference,
                    SaveDataIssueCode.InvalidOwnership,
                    SaveDataIssueCode.DuplicateRandomChannel,
                    SaveDataIssueCode.MissingRandomChannel,
                    SaveDataIssueCode.InvalidSequence,
                    SaveDataIssueCode.InconsistentState,
                    SaveDataIssueCode.NonFiniteNumber,
                },
                codes);
        }

        [Test]
        public void SaveDataValidator_RejectsHeaderSummaryAndDerivedSeedMismatch()
        {
            SaveDocumentDto document = CreateValidDocument();
            document.Header.PayloadSha256 = "ABC";
            document.Header.Summary.Gold++;
            document.Payload.Run.Monsters[0].RandomSeeds.HealthSeed++;

            SaveDataValidationResult result = SaveDataValidator.ValidateDocument(document);

            Assert.IsFalse(result.Succeeded);
            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Has.Some.EqualTo(SaveDataIssueCode.InvalidValue));
            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Has.Some.EqualTo(SaveDataIssueCode.InconsistentState));
        }

        internal static SaveDocumentDto CreateValidDocument()
        {
            string inventoryItem = "11111111111111111111111111111111";
            string equipmentItem = "22222222222222222222222222222222";
            string merchantItem = "33333333333333333333333333333333";
            string dropItem = "44444444444444444444444444444444";
            MonsterInstanceRandomSeeds monsterSeeds = new MonsterInstanceRandomSeeds(9876);
            SavePayloadDto payload = new SavePayloadDto
            {
                Items = new List<ItemInstanceDto>
                {
                    CreateItem(inventoryItem, "item:leather_armor"),
                    CreateItem(equipmentItem, "item:great_sword"),
                    CreateItem(merchantItem, "item:iron_ring"),
                    CreateItem(dropItem, "item:war_axe"),
                },
                Profile = new ProfileSaveData
                {
                    ProfileId = "local_default",
                    PlayerId = PlayerId.LocalPlayer.Value,
                    Gold = 250,
                    Inventory = new InventoryLayoutDto
                    {
                        Width = 10,
                        Height = 6,
                        Placements = new List<InventoryPlacementDto>
                        {
                            new InventoryPlacementDto
                            {
                                ItemInstanceId = inventoryItem,
                                X = 0,
                                Y = 0,
                                Width = 2,
                                Height = 3,
                            },
                        },
                    },
                    Equipment = new List<EquipmentLoadoutDto>
                    {
                        new EquipmentLoadoutDto
                        {
                            ActorInstanceId = PlayerId.LocalPlayer.Value,
                            Entries = new List<EquipmentEntryDto>
                            {
                                new EquipmentEntryDto
                                {
                                    Slot = EquipmentSlot.Weapon,
                                    ItemInstanceId = equipmentItem,
                                },
                            },
                        },
                    },
                },
                Run = new RunSaveData
                {
                    InstanceIds = new RunInstanceIdStateDto
                    {
                        RunId = RunValue,
                        NextMonsterSequence = 3,
                        NextWorldDropSequence = 5,
                    },
                    Random = CreateRandomState(),
                    Player = new PlayerRunStateDto
                    {
                        PlayerId = PlayerId.LocalPlayer.Value,
                        DefinitionContentId = "actor:player",
                        SkillContentId = "skill:basic_projectile",
                        Position = new Vector3Dto { X = 1f, Y = 2f, Z = 0f },
                        Resources = new CombatResourceStateDto
                        {
                            CurrentHealth = 75f,
                            MaxHealth = 100f,
                            CurrentMana = 20f,
                            MaxMana = 200f,
                            IsAlive = true,
                        },
                    },
                    Monsters = new List<MonsterRunStateDto>
                    {
                        new MonsterRunStateDto
                        {
                            InstanceId = $"{RunValue}:monster:2",
                            DefinitionContentId = "monster:razor_hound",
                            RandomSeeds = new MonsterRandomSeedsDto
                            {
                                RootSeed = monsterSeeds.RootSeed,
                                HealthSeed = monsterSeeds.HealthSeed,
                                AffixCountSeed = monsterSeeds.AffixCountSeed,
                                AffixSelectionSeed = monsterSeeds.AffixSelectionSeed,
                            },
                            BaseMaxHealth = 30f,
                            BaseStats = new List<StatValueDto>
                            {
                                new StatValueDto
                                {
                                    StatContentId = "stat:max_health",
                                    Value = 30f,
                                },
                            },
                            EffectiveStats = new List<StatValueDto>
                            {
                                new StatValueDto
                                {
                                    StatContentId = "stat:max_health",
                                    Value = 30f,
                                },
                            },
                            Position = new Vector3Dto { X = 5f, Y = -1f, Z = 0f },
                            Resources = new CombatResourceStateDto
                            {
                                CurrentHealth = 12f,
                                MaxHealth = 30f,
                                IsAlive = true,
                            },
                        },
                    },
                    WorldDrops = new List<WorldDropStateDto>
                    {
                        new WorldDropStateDto
                        {
                            InstanceId = $"{RunValue}:drop:4",
                            ItemInstanceId = dropItem,
                            Position = new Vector3Dto { X = -2f, Y = 3f, Z = 0f },
                        },
                    },
                    Merchant = new MerchantInventoryDto
                    {
                        TraderContentId = "trader:basic_trader",
                        OrderedItemInstanceIds = new List<string> { merchantItem },
                        BuyMultiplier = 1.5f,
                        SellMultiplier = 0.4f,
                    },
                    Spawner = new MonsterSpawnerStateDto
                    {
                        SpawnContentId = "spawn:main",
                        IsRunning = true,
                        NextSpawnRemainingSeconds = 0.75f,
                    },
                },
            };

            return new SaveDocumentDto
            {
                Header = new SaveHeaderDto
                {
                    FormatId = LocalSaveFormat.FormatId,
                    FormatVersion = LocalSaveFormat.FormatVersion,
                    SaveSchemaVersion = SaveSchemaVersion.Current.Value,
                    GameVersion = "0.2.2-alpha",
                    CatalogId = "core",
                    ContentVersion = 1,
                    SlotId = "auto",
                    CommitSequence = 1,
                    CreatedUtc = "2026-08-19T00:00:00.0000000+00:00",
                    UpdatedUtc = "2026-08-19T00:00:00.0000000+00:00",
                    PayloadSha256 = new string('a', 64),
                    Summary = new SaveSummaryDto
                    {
                        RunId = RunValue,
                        Gold = payload.Profile.Gold,
                        ItemCount = payload.Items.Count,
                        CurrentHealth = payload.Run.Player.Resources.CurrentHealth,
                        MaxHealth = payload.Run.Player.Resources.MaxHealth,
                    },
                },
                Payload = payload,
            };
        }

        static GameplayRandomStateDto CreateRandomState()
        {
            GameplayRandomStateDto state = new GameplayRandomStateDto
            {
                RootSeed = 12345,
            };

            foreach (GameplayRandomChannel channel in Enum.GetValues(typeof(GameplayRandomChannel)))
            {
                state.Channels.Add(new GameplayRandomChannelStateDto
                {
                    Channel = channel,
                    NextSequence = (int)channel + 1,
                });
            }

            return state;
        }

        static ItemInstanceDto CreateItem(string id, string contentId)
        {
            return new ItemInstanceDto
            {
                InstanceId = id,
                BaseContentId = contentId,
                Rarity = ItemRarity.Normal,
                ItemLevel = 1,
                Seed = 42,
            };
        }

        static string Describe(IReadOnlyList<SaveDataIssue> issues)
        {
            return string.Join(
                Environment.NewLine,
                issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }
    }
}
