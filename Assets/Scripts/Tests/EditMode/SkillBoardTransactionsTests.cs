using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class SkillBoardTransactionsTests
    {
        GameArchitectureTestFixture _session;
        IArchitecture _architecture;
        GameObject _playerObject;
        ContentCatalog _catalog;
        InventoryModel Inventory => _architecture.GetModel<InventoryModel>();
        SkillBoardModel Board => _architecture.GetModel<SkillBoardModel>();
        SkillBoardSystem System => _architecture.GetSystem<SkillBoardSystem>();
        const string Player = "local_player";

        [SetUp]
        public void SetUp()
        {
            _catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            _session = new GameArchitectureTestFixture();
            _architecture = _session.Start();
            _playerObject = new GameObject("SkillBoard Player");
            var actor = _playerObject.AddComponent<CombatActor>();
            actor.Configure(Player, ActorTeam.Player, 100, new StatBlock(), TagSet.Empty);
            _architecture.GetSystem<CombatSystem>().RegisterActor(actor);
        }
        [TearDown]
        public void TearDown() { UnityEngine.Object.DestroyImmediate(_playerObject); _session.Stop(); }

        [Test]
        public void Creation_AllRolesHaveUniqueNonStackingPayloadAndCorrectInventoryShape()
        {
            foreach (ItemBaseDefinition definition in _catalog.GetAll<ItemBaseDefinition>().Where(d => d.IsSkillBoardItem))
            {
                ItemInstance first = Create(definition.Id, 71);
                ItemInstance second = Create(definition.Id, 71);
                Assert.That(first.Id, Is.Not.EqualTo(second.Id));
                Assert.That(first.SkillBoardState.IsValidFor(definition), Is.True);
                Assert.That(definition.IsEquipment, Is.False);
                Assert.That(first.CanStackWith(second), Is.False);
                Assert.That(definition.GridSize, Is.EqualTo(definition.ItemType == ItemType.EnergyBottle ? new Vector2Int(2, 1) : Vector2Int.one));
                Assert.That(SkillBoardItemConfiguration.Validate(definition), Is.Empty);
                Assert.That(JToken.DeepEquals(JObject.FromObject(RuntimeStateMapper.ToDto(first, _catalog).Value.SkillBoard),
                    JObject.FromObject(RuntimeStateMapper.ToDto(second, _catalog).Value.SkillBoard)), Is.True);
            }
        }
        [Test]
        public void InstallAndLastRemoval_TransferTheSameInstanceOnce()
        {
            ItemInstance wood = Create("firewood_large");
            Inventory.TryAddItem(wood);
            SkillBoardPreview preview = System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            Assert.That(preview.Success, Is.True, preview.Error);
            Assert.That(Inventory.Grid.Placements.ContainsKey(wood), Is.True, "预览不得转移所有权");
            Assert.That(System.Commit(preview, out _), Is.True);
            Assert.That(System.Commit(preview, out _), Is.False, "一次预览只能提交一次");
            Assert.That(Inventory.Grid.Placements.ContainsKey(wood), Is.False);
            Assert.That(Board.State.Firewoods.Single().Item, Is.SameAs(wood));
            Commit(new SkillBoardRequest(SkillBoardOperation.Remove, wood));
            Assert.That(Inventory.Grid.Placements.ContainsKey(wood), Is.True);
            Assert.That(Board.State.Firewoods, Is.Empty);
        }
        [Test]
        public void EmbeddedItems_RequireManualExtractionBeforeWoodRemoval()
        {
            ItemInstance wood = InstallRoot();
            ItemInstance flame = Create("flame_projectile");
            Inventory.TryAddItem(flame);
            var slot = new FirewoodPortReference(wood.InstanceId, SkillBoardGeometry.CenterNodeId);
            Commit(new SkillBoardRequest(SkillBoardOperation.Embed, flame, slot: slot));
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Remove, wood)).Success, Is.False);
            Commit(new SkillBoardRequest(SkillBoardOperation.Extract, slot: slot));
            Assert.That(Inventory.Grid.Placements.ContainsKey(flame), Is.True);
            Commit(new SkillBoardRequest(SkillBoardOperation.Remove, wood));
        }
        [Test]
        public void FilledInventory_RejectsExtractionAndRemovalWithoutEventsOrTopologyChanges()
        {
            ItemInstance wood = InstallRoot();
            ItemInstance flame = Create("flame_projectile");
            Inventory.TryAddItem(flame);
            var slot = new FirewoodPortReference(wood.InstanceId, SkillBoardGeometry.CenterNodeId);
            Commit(new SkillBoardRequest(SkillBoardOperation.Embed, flame, slot: slot));
            FillInventory();
            SkillBoardState before = Board.State;
            int events = 0;
            IUnRegister registration = _architecture.RegisterEvent<SkillBoardChangedEvent>(_ => events++);
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Extract, slot: slot)).Success, Is.False);
            Assert.That(Board.State, Is.SameAs(before));
            Assert.That(events, Is.Zero);
            registration.UnRegister();
        }
        [Test]
        public void StalePreview_RejectsMovedSourceAndPreservesExternalMove()
        {
            ItemInstance wood = Create("firewood_large"); Inventory.TryAddItem(wood);
            SkillBoardPreview preview = System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            Inventory.TryMoveItem(wood, new Vector2Int(5, 3));
            Assert.That(System.Commit(preview, out _), Is.False);
            Assert.That(Inventory.Grid.Placements[wood].position, Is.EqualTo(new Vector2Int(5, 3)));
            Assert.That(Board.State.Firewoods, Is.Empty);
        }
        [Test]
        public void StalePreview_RejectsChangedVariantAndOtherSession()
        {
            ItemInstance wood = Create("firewood_large"); Inventory.TryAddItem(wood);
            SkillBoardPreview preview = System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            wood.Quality++;
            Assert.That(System.Commit(preview, out _), Is.False);
            SkillBoardPreview fresh = System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            SkillBoardSystem previous = System;
            UnityEngine.Object.DestroyImmediate(_playerObject); _playerObject = null;
            _architecture = _session.Restart();
            Assert.That(previous.Commit(fresh, out _), Is.False);
            Assert.That(System.Commit(fresh, out _), Is.False);
        }
        [Test]
        public void BottleSwap_UsesExactSourceCellsEvenWhenInventoryIsFull()
        {
            ItemInstance first = Create("energy_bottle_small"); Inventory.TryAddItem(first);
            Commit(new SkillBoardRequest(SkillBoardOperation.EquipBottle, first));
            Assert.That(first.CollectEnergy(1100), Is.EqualTo(1000));
            ItemInstance second = Create("energy_bottle_large"); Inventory.TryAddItemAt(second, new Vector2Int(4, 2));
            FillInventory();
            Commit(new SkillBoardRequest(SkillBoardOperation.EquipBottle, second));
            Assert.That(Inventory.Grid.Placements[first].position, Is.EqualTo(new Vector2Int(4, 2)));
            Assert.That(first.SkillBoardState.Energy, Is.EqualTo(1000));
            Assert.That(Board.State.EnergyBottle, Is.SameAs(second));
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.UnequipBottle)).Success, Is.False);
        }
        [Test]
        public void BottleCollection_InvalidatesPendingUnequipPreview()
        {
            ItemInstance bottle = Create("energy_bottle_small"); Inventory.TryAddItem(bottle);
            Commit(new SkillBoardRequest(SkillBoardOperation.EquipBottle, bottle));
            SkillBoardPreview preview = System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.UnequipBottle));
            bottle.CollectEnergy(7);
            Assert.That(System.Commit(preview, out _), Is.False);
            Assert.That(Board.State.EnergyBottle, Is.SameAs(bottle));
            Assert.That(bottle.SkillBoardState.Energy, Is.EqualTo(7));
        }
        [Test]
        public void DirectConnection_RejectsEmberUntilManuallyExtractedAndRejectsBridgeRemoval()
        {
            ItemInstance root = FixedWood(FirewoodSize.Large);
            ItemInstance medium = FixedWood(FirewoodSize.Medium);
            Inventory.TryAddItem(root); Inventory.TryAddItem(medium);
            int rootRotation = FacingRotation(root, 0);
            int mediumRotation = FacingRotation(medium, 3);
            Commit(new SkillBoardRequest(SkillBoardOperation.Install, root, new HexCoordinate(16, 0), rootRotation));
            var slot = new FirewoodPortReference(root.InstanceId, root.SkillBoardState.Layout.Nodes.Single(n => n.Kind == FirewoodNodeKind.Crack).Id);
            ItemInstance ember = Create("ember_power"); Inventory.TryAddItem(ember);
            Commit(new SkillBoardRequest(SkillBoardOperation.Embed, ember, slot: slot));
            var placements = new[] { Board.State.Firewoods[0].Placement,
                new FirewoodPlacement(medium.InstanceId, medium.SkillBoardState.Layout, new HexCoordinate(20, 0), mediumRotation) };
            IReadOnlyList<FirewoodConnection> connections = SkillBoardPlacementValidator.FindConnections(placements);
            var install = new SkillBoardRequest(SkillBoardOperation.Install, medium, new HexCoordinate(20, 0), mediumRotation, connections: connections);
            Assert.That(System.Preview(Player, install).Success, Is.False);
            Commit(new SkillBoardRequest(SkillBoardOperation.Extract, slot: slot));
            Commit(install);
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Embed, ember, slot: slot)).Success, Is.False);
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Remove, root)).Success, Is.False);
            ItemInstance flame = Create("flame_projectile"); Inventory.TryAddItem(flame);
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Embed, flame,
                slot: new FirewoodPortReference(medium.InstanceId, SkillBoardGeometry.CenterNodeId))).Success, Is.False);
            Commit(new SkillBoardRequest(SkillBoardOperation.Remove, medium));
            Assert.That(Board.State.Connections, Is.Empty);
            Commit(new SkillBoardRequest(SkillBoardOperation.Remove, root));
        }
        [Test]
        public void InvalidPlacementOrRoot_DoesNotTakeItemFromInventory()
        {
            ItemInstance large = Create("firewood_large"); Inventory.TryAddItem(large);
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, large, new HexCoordinate(40, 0))).Success, Is.False);
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, large)).Success, Is.False);
            Assert.That(Inventory.Grid.Placements.ContainsKey(large), Is.True);
            ItemInstance small = Create("firewood_small"); Inventory.TryAddItem(small);
            Assert.That(System.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, small, new HexCoordinate(100, 0))).Success, Is.False);
        }
        [Test]
        public void RootReachability_AllowsLoopsAndAnotherRootButRejectsDisconnectedRemainder()
        {
            FirewoodLayout large = FirewoodGenerator.Generate(FirewoodSize.Large, 8).Layout;
            FirewoodLayout small = FirewoodGenerator.Generate(FirewoodSize.Small, 4).Layout;
            var placements = new[] { new FirewoodPlacement("a", large, SkillBoardGeometry.Roots[0]),
                new FirewoodPlacement("b", small, new HexCoordinate(21, 0)), new FirewoodPlacement("c", small, new HexCoordinate(25, 0)),
                new FirewoodPlacement("d", small, new HexCoordinate(25, 4)), new FirewoodPlacement("e", large, SkillBoardGeometry.Roots[1]) };
            FirewoodConnection Link(string a, string b) { return new FirewoodConnection(new FirewoodPortReference(a, "x"), new FirewoodPortReference(b, "y")); }
            FirewoodConnection[] links = { Link("a", "b"), Link("b", "c"), Link("c", "d"), Link("d", "b"), Link("d", "e") };
            Assert.That(SkillBoardRootConnectivity.IsRooted(placements, links), Is.True);
            Assert.That(SkillBoardRootConnectivity.IsRooted(placements.Where(p => p.Id != "a").ToArray(), links.Where(l => l.First.PlacementId != "a").ToArray()), Is.True);
            Assert.That(SkillBoardRootConnectivity.IsRooted(placements.Where(p => p.Id != "a" && p.Id != "e").ToArray(),
                links.Where(l => l.First.PlacementId != "a" && l.Second.PlacementId != "e").ToArray()), Is.False);
        }
        [Test]
        public void ItemMapper_PreservesActualLayoutsEffectsFlameAndPartialEnergyAndRejectsCorruption()
        {
            foreach (ItemBaseDefinition definition in _catalog.GetAll<ItemBaseDefinition>().Where(d => d.IsSkillBoardItem))
            {
                ItemInstance item = Create(definition.Id, 46);
                if (definition.ItemType == ItemType.EnergyBottle) { item.CollectEnergy(17); }
                ItemInstanceDto dto = RuntimeStateMapper.ToDto(item, _catalog).Value;
                DtoMapResult<ItemInstance> restored = RuntimeStateMapper.FromDto(dto, _catalog);
                Assert.That(restored.Succeeded, Is.True, string.Join(";", restored.Issues.Select(i => i.Message)));
                Assert.That(JToken.DeepEquals(JObject.FromObject(dto), JObject.FromObject(RuntimeStateMapper.ToDto(restored.Value, _catalog).Value)), Is.True);
                dto.SkillBoard.Energy = -1;
                Assert.That(RuntimeStateMapper.FromDto(dto, _catalog).Succeeded, Is.False);
            }
            ItemInstanceDto broken = RuntimeStateMapper.ToDto(Create("firewood_large"), _catalog).Value;
            broken.SkillBoard.Layout.Nodes.Add(broken.SkillBoard.Layout.Nodes[0]);
            Assert.That(RuntimeStateMapper.FromDto(broken, _catalog).Succeeded, Is.False);
            ItemInstanceDto bottle = RuntimeStateMapper.ToDto(Create("energy_bottle_small"), _catalog).Value;
            bottle.SkillBoard.Energy = 1001;
            Assert.That(RuntimeStateMapper.FromDto(bottle, _catalog).Succeeded, Is.False);
            ItemInstanceDto invalidCoordinates = RuntimeStateMapper.ToDto(Create("firewood_large"), _catalog).Value;
            invalidCoordinates.SkillBoard.Layout.Nodes[0].Q = int.MaxValue;
            invalidCoordinates.SkillBoard.Layout.Nodes[0].R = int.MaxValue;
            Assert.That(RuntimeStateMapper.FromDto(invalidCoordinates, _catalog).Succeeded, Is.False);
        }
        [Test]
        public void CompleteSaveGraph_RestoresBoardAndBottleAndRejectsDuplicateOwnerBeforeCommit()
        {
            SaveDocumentDto document = SaveRestorePreparer.Prepare(SaveDataContractTests.CreateValidDocument(), _catalog).Value.Document;
            ItemInstance wood = Create("firewood_large");
            ItemInstance flame = Create("flame_projectile");
            ItemInstance bottle = Create("energy_bottle_small"); bottle.CollectEnergy(321);
            var state = new SkillBoardState(new[] { new InstalledFirewood(wood, new HexCoordinate(16, 0), 2) },
                embeddings: new[] { new SkillBoardEmbedding(new FirewoodPortReference(wood.InstanceId, SkillBoardGeometry.CenterNodeId), flame) }, energyBottle: bottle);
            document.Payload.Profile.SkillBoard = RuntimeStateMapper.ToDto(document.Payload.Profile.PlayerId, state);
            foreach (ItemInstance item in state.Items) { document.Payload.Items.Add(RuntimeStateMapper.ToDto(item, _catalog).Value); }
            document.Header.Summary.ItemCount = document.Payload.Items.Count;
            PreparedRestoreResult restored = SaveRestorePreparer.Prepare(document, _catalog);
            Assert.That(restored.Succeeded, Is.True, string.Join(";", restored.Issues.Select(i => i.Message)));
            Assert.That(restored.Value.RuntimeState.SkillBoard.EnergyBottle.SkillBoardState.Energy, Is.EqualTo(321));
            Assert.That(restored.Value.RuntimeState.SkillBoard.Embeddings.Single().Item.InstanceId, Is.EqualTo(flame.InstanceId));
            SkillBoardState current = Board.State;
            document.Payload.Profile.Inventory.Placements.Add(new InventoryPlacementDto { ItemInstanceId = wood.InstanceId, X = 5, Y = 5, Width = 1, Height = 1 });
            Assert.That(SaveRestorePreparer.Prepare(document, _catalog).Succeeded, Is.False);
            Assert.That(Board.State, Is.SameAs(current));
            document.Payload.Profile.Inventory.Placements.RemoveAt(document.Payload.Profile.Inventory.Placements.Count - 1);
            document.Payload.Profile.SkillBoard.Firewoods[0].Q = int.MaxValue;
            document.Payload.Profile.SkillBoard.Firewoods[0].R = int.MaxValue;
            Assert.That(SaveRestorePreparer.Prepare(document, _catalog).Succeeded, Is.False);
        }

        [Test]
        public void BuyingAndSelling_RetainsVariantInstanceAndStoredEnergy()
        {
            Inventory.ConfigureCurrency(_catalog);
            Inventory.AddGold(1000);
            foreach (string id in new[] { "firewood_large", "flame_projectile", "ember_power", "energy_bottle_small" })
            {
                ItemInstance item = Create(id, 93);
                if (item.BaseDefinition.ItemType == ItemType.EnergyBottle) { item.CollectEnergy(321); }
                string variant = JObject.FromObject(RuntimeStateMapper.ToDto(item, _catalog).Value).ToString();
                EconomyModel economy = _architecture.GetModel<EconomyModel>();
                economy.RestoreState(_catalog.GetAll<TraderDefinition>().First(), new[] { item }, 1.5f, .4f);
                Assert.That(_architecture.SendCommand(new BuyItemCommand(item)), Is.True);
                Assert.That(Inventory.Grid.Placements.ContainsKey(item), Is.True);
                Assert.That(economy.MerchantStock.Contains(item), Is.False);
                ItemInstance sold = null;
                IUnRegister registration = _architecture.RegisterEvent<TradeCompletedEvent>(e => sold = e.Item);
                Assert.That(_architecture.SendCommand(new SellItemCommand(item)), Is.True);
                registration.UnRegister();
                Assert.That(sold, Is.SameAs(item));
                Assert.That(Inventory.Grid.Placements.ContainsKey(item), Is.False);
                Assert.That(JObject.FromObject(RuntimeStateMapper.ToDto(item, _catalog).Value).ToString(), Is.EqualTo(variant));
            }
        }

        ItemInstance InstallRoot()
        {
            ItemInstance wood = Create("firewood_large"); Inventory.TryAddItem(wood);
            Commit(new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            return wood;
        }
        ItemInstance Create(string id, int seed = 8)
        {
            ItemBaseDefinition definition = _catalog.Resolve<ItemBaseDefinition>(ContentId.Parse("item:" + id)).Value;
            return definition.CreateInstance(_architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, seed, ItemRarity.Normal);
        }
        ItemInstance FixedWood(FirewoodSize size)
        {
            ItemInstance generated = Create(size == FirewoodSize.Large ? "firewood_large" : "firewood_medium");
            FirewoodLayout layout = FirewoodGenerator.Generate(size, 41, crackCount: 1).Layout;
            var item = new ItemInstance(generated.Id, generated.BaseDefinition, ItemRarity.Normal, 1, 41, Array.Empty<ModifierInstance>());
            ModifierInstance modifier = generated.SkillBoardState.Talents[0].Modifiers[0];
            item.InitializeSkillBoardState(new SkillBoardItemState(layout, layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent)
                .Select(n => new SkillBoardTalentState(n.Id, 0, new[] { modifier }))));
            return item;
        }
        static int FacingRotation(ItemInstance item, int direction)
        {
            FirewoodNode node = item.SkillBoardState.Layout.Nodes.Single(n => n.Kind == FirewoodNodeKind.Crack);
            return Enumerable.Range(0, 6).Single(r => node.Outward.Value.Rotate(r).Equals(SkillBoardGeometry.NeighborDirections[direction]));
        }
        void FillInventory()
        {
            while (Inventory.TryAddItem(Create("ember_power"))) { }
        }
        void Commit(SkillBoardRequest request)
        {
            SkillBoardPreview preview = _architecture.SendQuery(new PreviewSkillBoardQuery(Player, request));
            Assert.That(preview.Success, Is.True, preview.Error);
            var command = new CommitSkillBoardCommand(preview);
            Assert.That(_architecture.SendCommand(command), Is.True, command.Error);
        }
    }
}
