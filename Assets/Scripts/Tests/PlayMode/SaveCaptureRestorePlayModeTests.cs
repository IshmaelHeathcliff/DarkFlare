using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class SaveCaptureRestorePlayModeTests
    {
        const float TimeoutSeconds = 20f;

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();
        SaveCoordinator _testCoordinator;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _testCoordinator?.EmergencyClose();
            _testCoordinator = null;
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator MainSession_CapturePrepareRestorePreservesStateWithoutNewGameGrant()
        {
            yield return _fixture.EnterMain();
            yield return WaitForRunningSession();
            ApplicationHost host = ApplicationHost.Current;
            GameSessionHost oldSession = host.CurrentSession;
            int oldGeneration = oldSession.ArchitectureGeneration;
            IArchitecture oldArchitecture = oldSession.Architecture;
            PlayerController oldPlayer = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            CombatPrototypeBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
            Assert.IsNotNull(oldPlayer);
            Assert.IsNotNull(spawner);
            Assert.IsNotNull(bootstrap);
            oldArchitecture.GetModel<InventoryModel>().AddGold(37);
            CombatResourceSnapshot oldResources = oldPlayer.Actor.Resources;
            float expectedHealth = oldResources.MaxHealth - 13f;
            oldPlayer.Actor.RestoreResources(
                new CombatResourceSnapshot(
                    expectedHealth,
                    oldResources.MaxHealth,
                    oldResources.CurrentMana,
                    oldResources.MaxMana),
                true);
            GameplayRandomSystem oldRandom = oldArchitecture.GetSystem<GameplayRandomSystem>();
            oldRandom.NextSeed(GameplayRandomChannel.Crafting);
            oldRandom.NextSeed(GameplayRandomChannel.Crafting);
            SessionSnapshotSource source = new SessionSnapshotSource(
                oldSession,
                host.ContentCatalog,
                spawner);

            SessionSnapshotResult captured = source.Capture();

            Assert.IsTrue(captured.Succeeded, Describe(captured));
            int capturedGold = captured.Payload.Profile.Gold;
            int capturedMerchantCount = captured.Payload.Run.Merchant.OrderedItemInstanceIds.Count;
            GameplayRandomState capturedRandom = oldRandom.CaptureState();
            GameplayRandomSystem randomOracle = new GameplayRandomSystem();
            randomOracle.RestoreState(capturedRandom);
            int expectedNextCraftSeed = randomOracle.NextSeed(GameplayRandomChannel.Crafting);
            RunInstanceIdState capturedIds = oldArchitecture
                .GetUtility<IRunInstanceIdGenerator>()
                .CaptureState();
            RunInstanceIdGenerator idOracle = new RunInstanceIdGenerator(capturedIds);
            string expectedNextMonsterId = idOracle.NextMonsterId().Value;
            SaveDocumentDto document = CreateDocument(captured.Payload, host.ContentCatalog);
            SaveSerializationResult serialized = new NewtonsoftSaveSerializer().Serialize(document);
            Assert.IsTrue(serialized.Succeeded, serialized.Exception?.ToString());
            SaveDeserializationResult deserialized = new NewtonsoftSaveSerializer().Deserialize(
                serialized.Bytes,
                SaveSlotId.Parse("auto"),
                1);
            Assert.IsTrue(deserialized.Succeeded, deserialized.Exception?.ToString());
            PreparedRestoreResult prepared = SaveRestorePreparer.Prepare(
                deserialized.Document,
                host.ContentCatalog);
            Assert.IsTrue(prepared.Succeeded, Describe(prepared));
            bool callbackReceived = false;
            LifecycleResult callbackResult = default;
            LifecycleResult submitted = host.BeginSceneSessionInitialization(
                SceneManager.GetActiveScene(),
                new RestoreGameSessionInitializer(
                    bootstrap.CreateSceneConfiguration(),
                    prepared.Value),
                onCompleted: result =>
                {
                    callbackResult = result;
                    callbackReceived = true;
                });
            Assert.IsTrue(submitted.IsSuccess, submitted.Message);
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while ((!callbackReceived
                    || host.CurrentSession == null
                    || host.CurrentSession.State != GameSessionState.Running)
                && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsTrue(callbackReceived, "Restore 回调超时");
            Assert.IsTrue(callbackResult.IsSuccess, callbackResult.Exception?.ToString());
            Assert.Greater(host.CurrentSession.ArchitectureGeneration, oldGeneration);
            IArchitecture restoredArchitecture = host.CurrentSession.Architecture;
            PrefabAssetLoader prefabLoader = restoredArchitecture.GetUtility<PrefabAssetLoader>();

            foreach (MonsterDefinition monster in prepared.Value.SpawnDefinition.AllMonsters)
            {
                Assert.IsNotNull(
                    prefabLoader.GetPrefab(monster.Prefab),
                    $"Restore 必须预热后续刷怪所需 Prefab: {monster.Id}");
            }

            PlayerController restoredPlayer = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(restoredPlayer);
            Assert.AreEqual(expectedHealth, restoredPlayer.Actor.CurrentHealth, 0.001f);
            Assert.AreEqual(capturedGold, restoredArchitecture.GetModel<InventoryModel>().Gold);
            Assert.AreEqual(
                capturedMerchantCount,
                restoredArchitecture.GetModel<EconomyModel>().MerchantStock.Count);
            Assert.AreEqual(
                expectedNextCraftSeed,
                restoredArchitecture.GetSystem<GameplayRandomSystem>()
                    .NextSeed(GameplayRandomChannel.Crafting));
            Assert.AreEqual(
                expectedNextMonsterId,
                restoredArchitecture.GetUtility<IRunInstanceIdGenerator>()
                    .NextMonsterId()
                    .Value);
            Assert.AreEqual(
                captured.Payload.Profile.Equipment[0].Entries.Single(
                    entry => entry.Slot == EquipmentSlot.Weapon).ItemInstanceId,
                restoredArchitecture.GetModel<EquipmentModel>()
                    .GetWeapon(restoredPlayer.Actor)
                    .Id
                    .Value);
            Assert.AreEqual(
                captured.Payload.Run.Spawner.IsRunning,
                UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>().IsSpawning);
        }

        [UnityTest]
        public IEnumerator SessionFacade_SaveIsSessionScopedAndCrossSessionMethodsAreRemoved()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            GameSessionHost session = host.CurrentSession;
            MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            Assert.IsNotNull(spawner);
            LocalSaveStorage storage = new LocalSaveStorage(
                new MemorySavePathProvider("C:/DarkFlare-FacadePlayModeTests"),
                new NewtonsoftSaveSerializer(),
                new MemorySaveFileOperations());
            _testCoordinator = new SaveCoordinator(
                host.ProfileScope,
                storage,
                host.ContentCatalog,
                "test");
            _testCoordinator.BindSession(
                session,
                new SessionSnapshotSource(session, host.ContentCatalog, spawner));
            SessionSaveFacade facade = new SessionSaveFacade(
                host,
                session,
                _testCoordinator,
                SceneManager.GetActiveScene());
            InventoryModel inventory = session.Architecture.GetModel<InventoryModel>();
            inventory.AddGold(41);
            int savedGold = inventory.Gold;
            SaveOperationResult saveResult = null;
            yield return facade.SaveAutoAsync().ToCoroutine(result => saveResult = result);
            Assert.IsTrue(saveResult.Succeeded, saveResult.Exception?.ToString());
            Assert.AreEqual(savedGold, inventory.Gold);
            Assert.IsTrue(facade.IsAvailable);
            Assert.IsNull(typeof(SessionSaveFacade).GetMethod("ContinueAutoAsync"));
            Assert.IsNull(typeof(SessionSaveFacade).GetMethod("StartNewGameAsync"));
        }

        [UnityTest]
        public IEnumerator GameMenu_SaveActionsArePresentFocusableAndReflectSlotProbe()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
            UIDocument document = menu != null ? menu.GetComponent<UIDocument>() : null;
            Assert.IsNotNull(menu);
            Assert.IsNotNull(document);
            menu.OpenPage(GameMenuPage.Inventory);
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (menu.IsSaveOperationBusy && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsFalse(menu.IsSaveOperationBusy, "自动存档探测超时");
            VisualElement root = document.rootVisualElement;
            Button save = root.Q<Button>("game-menu-save");
            Button returnFrontEnd = root.Q<Button>("game-menu-return-front-end");
            Label status = root.Q<Label>("game-menu-save-status");
            Assert.IsNotNull(save);
            Assert.IsNotNull(returnFrontEnd);
            Assert.IsNotNull(status);
            Assert.IsTrue(save.enabledSelf);
            Assert.IsTrue(returnFrontEnd.enabledSelf);
            Assert.IsNull(root.Q<Button>("game-menu-continue"));
            Assert.IsNull(root.Q<Button>("game-menu-new-game"));
            Assert.IsNull(root.Q<DropdownField>("game-menu-language-dropdown"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(status.text));
            save.Focus();
            yield return null;
            Assert.AreSame(save, root.focusController.focusedElement);
            returnFrontEnd.Focus();
            yield return null;
            Assert.AreSame(returnFrontEnd, root.focusController.focusedElement);
        }

        static SaveDocumentDto CreateDocument(SavePayloadDto payload, ContentCatalog catalog)
        {
            string now = new DateTimeOffset(
                    2026,
                    8,
                    19,
                    0,
                    0,
                    0,
                    TimeSpan.Zero)
                .ToString("O");
            return new SaveDocumentDto
            {
                Header = new SaveHeaderDto
                {
                    FormatId = LocalSaveFormat.FormatId,
                    FormatVersion = LocalSaveFormat.FormatVersion,
                    SaveSchemaVersion = SaveSchemaVersion.Current.Value,
                    GameVersion = "0.2.2-alpha",
                    CatalogId = catalog.CatalogId,
                    ContentVersion = catalog.ContentVersion,
                    SlotId = "auto",
                    CommitSequence = 1,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    PayloadSha256 = new string('0', 64),
                    Summary = new SaveSummaryDto
                    {
                        RunId = payload.Run.InstanceIds.RunId,
                        Gold = payload.Profile.Gold,
                        ItemCount = payload.Items.Count,
                        CurrentHealth = payload.Run.Player.Resources.CurrentHealth,
                        MaxHealth = payload.Run.Player.Resources.MaxHealth,
                    },
                },
                Payload = payload,
            };
        }

        static IEnumerator WaitForRunningSession()
        {
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (Time.realtimeSinceStartup < timeout)
            {
                if (ApplicationHost.TryGetCurrent(out ApplicationHost host)
                    && host.CurrentSession != null
                    && host.CurrentSession.State == GameSessionState.Running)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("等待 Running Session 超时");
        }

        static string Describe(SessionSnapshotResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }

        static string Describe(PreparedRestoreResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}"));
        }

        sealed class MemorySavePathProvider : ISavePathProvider
        {
            public MemorySavePathProvider(string rootPath)
            {
                RootPath = rootPath;
            }

            public string RootPath { get; }
        }

        sealed class MemorySaveFileOperations : ILocalSaveFileOperations
        {
            readonly HashSet<string> _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            public bool DirectoryExists(string path)
            {
                return _directories.Contains(Normalize(path));
            }

            public void CreateDirectory(string path)
            {
                _directories.Add(Normalize(path));
            }

            public IReadOnlyList<string> EnumerateFiles(string path)
            {
                string prefix = Normalize(path).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                return _files.Keys
                    .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            public bool FileExists(string path)
            {
                return _files.ContainsKey(Normalize(path));
            }

            public long GetFileLength(string path)
            {
                return _files[Normalize(path)].LongLength;
            }

            public byte[] ReadAllBytes(string path)
            {
                return _files[Normalize(path)].ToArray();
            }

            public void WriteAllBytesDurable(string path, byte[] bytes)
            {
                _files.Add(Normalize(path), bytes.ToArray());
            }

            public void MoveFileNoOverwrite(string sourcePath, string destinationPath)
            {
                string source = Normalize(sourcePath);
                string destination = Normalize(destinationPath);
                _files.Add(destination, _files[source]);
                _files.Remove(source);
            }

            public void DeleteFile(string path)
            {
                _files.Remove(Normalize(path));
            }

            static string Normalize(string path)
            {
                return Path.GetFullPath(path);
            }
        }
    }
}
