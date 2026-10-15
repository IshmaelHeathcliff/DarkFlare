using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class PlayerGrowthTests
    {
        const string Player = "growth_player";
        GameArchitectureTestFixture _session;
        IArchitecture _architecture;
        GameObject _playerObject;
        CombatActor _actor;
        ContentCatalog _catalog;
        PlayerGrowthModel Growth => _architecture.GetModel<PlayerGrowthModel>();
        InventoryModel Inventory => _architecture.GetModel<InventoryModel>();
        SkillBoardSystem BoardSystem => _architecture.GetSystem<SkillBoardSystem>();
        SkillBoardState Board => _architecture.GetModel<SkillBoardModel>().State;
        PlayerGrowthSystem System => _architecture.GetSystem<PlayerGrowthSystem>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            _session = new GameArchitectureTestFixture();
            _architecture = _session.Start();
            _playerObject = new GameObject("Growth Player");
            _actor = _playerObject.AddComponent<CombatActor>();
            var stats = new StatBlock();
            stats.SetValue(StatIds.Mana, 200);
            _actor.Configure(Player, ActorTeam.Player, 100, stats, TagSet.Empty);
            _architecture.GetSystem<CombatSystem>().RegisterActor(_actor);
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_playerObject);
            _session.Stop();
        }

        [TestCase(99, 0, 99)]
        [TestCase(100, 1, 0)]
        [TestCase(224, 1, 124)]
        [TestCase(225, 2, 0)]
        [TestCase(1000, 6, 25)]
        public void Conversion_UsesIncreasingThresholdsAndPreservesRemainder(int energy, int points, int remainder)
        {
            PlayerGrowthState state = new PlayerGrowthState().Charge(energy);
            Assert.That(state.EarnedPoints, Is.EqualTo(points));
            Assert.That(state.TalentPoints, Is.EqualTo(points));
            Assert.That(state.AttributePoints, Is.EqualTo(points));
            Assert.That(state.Energy, Is.EqualTo(remainder));
            Assert.That(state.Rules.TotalCost(points) + state.Energy, Is.EqualTo(energy));
            Assert.That(state.Validate(), Is.Null);
        }

        [Test]
        public void SplitBottlesAndSpentPoints_DoNotChangeConversionSequence()
        {
            PlayerGrowthState split = new PlayerGrowthState().Charge(90).Charge(135).Allocate(StatIds.Strength, 1).Charge(200);
            PlayerGrowthState combined = new PlayerGrowthState().Charge(425).Allocate(StatIds.Strength, 1);
            Assert.That(JToken.DeepEquals(JObject.FromObject(RuntimeStateMapper.ToDto(Player, split)),
                JObject.FromObject(RuntimeStateMapper.ToDto(Player, combined))), Is.True);
            Assert.That(split.Rules.NextCost(split.EarnedPoints), Is.EqualTo(175));
        }

        [Test]
        public void LargeIntegerCurveAndInvalidInputs_AreBoundedAndRejectBrokenLedgers()
        {
            var rules = new GrowthRules(1, int.MaxValue, 1, 1, 1);
            PlayerGrowthState large = new PlayerGrowthState(rules).Charge(int.MaxValue);
            Assert.That(large.EarnedPoints, Is.EqualTo(1));
            Assert.That(large.Energy, Is.EqualTo(int.MaxValue - 1L));
            Assert.Throws<ArgumentException>(() => new PlayerGrowthState().Charge(0));
            Assert.Throws<ArgumentException>(() => new PlayerGrowthState(new GrowthRules()).Charge(1));
            Assert.That(new PlayerGrowthState(earnedPoints: 1).Validate(), Is.Not.Null);
            Assert.That(new PlayerGrowthState(energy: long.MaxValue).Validate(), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => new PlayerGrowthState().Charge(100).Allocate(StatIds.MaxHealth, 1));
        }

        [Test]
        public void ChargingBackpackAndEquippedBottle_PublishesOnlyConsistentFinalState()
        {
            ItemInstance first = Item("energy_bottle_small");
            first.CollectEnergy(90);
            Inventory.TryAddItem(first);
            SkillBoardPreview preview = BoardSystem.Preview(Player, new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, first));
            Assert.That(preview.Success, Is.True, preview.Error);
            Assert.That(Inventory.Grid.Placements.ContainsKey(first), Is.True, "选择 / 预检不提前消费");
            Assert.That(Growth.State.Energy, Is.Zero);
            int notifications = 0;
            IUnRegister registration = _architecture.RegisterEvent<PlayerGrowthChangedEvent>(_ =>
            {
                notifications++;
                Assert.That(Inventory.Grid.Placements.ContainsKey(first), Is.False);
                Assert.That(Growth.State.Energy, Is.EqualTo(90));
            });
            Assert.That(BoardSystem.Commit(preview, out _), Is.True);
            registration.UnRegister();
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(BoardSystem.Commit(preview, out _), Is.False);
            Assert.That(BoardSystem.Preview(Player, new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, first)).Success, Is.False);
            ItemInstance second = Item("energy_bottle_small");
            second.CollectEnergy(135);
            Inventory.TryAddItem(second);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.EquipBottle, second));
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, second));
            Assert.That(Board.EnergyBottle, Is.Null);
            Assert.That(Growth.State.EarnedPoints, Is.EqualTo(2));
            Assert.That(Growth.State.Energy, Is.Zero);
            Assert.That(Inventory.Grid.Placements.ContainsKey(second), Is.False);
        }

        [Test]
        public void EmptyBottleAndStaleCharge_RetainBottleAndExternalEnergy()
        {
            ItemInstance bottle = Item("energy_bottle_small");
            Inventory.TryAddItem(bottle);
            Assert.That(BoardSystem.Preview(Player, new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, bottle)).Success, Is.False);
            Assert.That(Inventory.Grid.Placements.ContainsKey(bottle), Is.True);
            bottle.CollectEnergy(100);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.EquipBottle, bottle));
            SkillBoardPreview preview = BoardSystem.Preview(Player, new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, bottle));
            bottle.CollectEnergy(1);
            Assert.That(BoardSystem.Commit(preview, out _), Is.False);
            Assert.That(Board.EnergyBottle, Is.SameAs(bottle));
            Assert.That(bottle.SkillBoardState.Energy, Is.EqualTo(101));
            Assert.That(Growth.State.EarnedPoints, Is.Zero);
        }

        [Test]
        public void CenterSelection_TracksOriginalOwnerAndInvalidatesMovedSourceWithoutConsuming()
        {
            ItemInstance bottle = Item("energy_bottle_small");
            bottle.CollectEnergy(100);
            Inventory.TryAddItem(bottle);
            EnergyBottleSelection selection = _architecture.SendQuery(new SelectEnergyBottleQuery(Player, bottle));
            RectInt originalPlacement = Inventory.Grid.Placements[bottle];
            Assert.That(selection, Is.Not.Null);
            Assert.That(BoardSystem.IsSelectionCurrent(selection), Is.True);
            Assert.That(Inventory.Grid.Placements.ContainsKey(bottle), Is.True);
            SkillBoardPreview charge = BoardSystem.PreviewCharge(selection);
            bottle.CollectEnergy(1);
            Assert.That(BoardSystem.IsSelectionCurrent(selection), Is.True, "积蓄改变预览，但不转移选择的所有权");
            Assert.That(BoardSystem.Commit(charge, out _), Is.False);
            Assert.That(Inventory.TryMoveItem(bottle, new Vector2Int(5, 3)), Is.True);
            Assert.That(BoardSystem.IsSelectionCurrent(selection), Is.False);
            Assert.That(BoardSystem.PreviewCharge(selection).Success, Is.False);
            Assert.That(Inventory.TryMoveItem(bottle, originalPlacement.position), Is.True);
            Assert.That(BoardSystem.IsSelectionCurrent(selection), Is.False, "移动后再放回原位也不能复用旧选择");
            selection = BoardSystem.SelectBottle(Player, bottle);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.EquipBottle, bottle));
            Assert.That(BoardSystem.IsSelectionCurrent(selection), Is.False);
            EnergyBottleSelection equipped = BoardSystem.SelectBottle(Player, bottle);
            Assert.That(equipped, Is.Not.Null);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.UnequipBottle));
            Assert.That(BoardSystem.IsSelectionCurrent(equipped), Is.False);
            Assert.That(Growth.State.EarnedPoints, Is.Zero);
            Assert.That(bottle.SkillBoardState.Energy, Is.EqualTo(101));
        }

        [Test]
        public void FailedReturnAndThrowingObserver_CannotLoseOrDuplicateSpentPoints()
        {
            Charge(1000);
            ItemInstance wood = Item("firewood_large");
            Inventory.TryAddItem(wood);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            GrowthCommit(System.Preview(Player, null, 1, wood.InstanceId, ""));
            while (Inventory.TryAddItem(Item("ember_power"))) { }
            PlayerGrowthState before = Growth.State;
            Assert.That(BoardSystem.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Remove, wood)).Success, Is.False);
            Assert.That(Growth.State, Is.SameAs(before));
            ItemInstance filler = Inventory.Grid.Placements.Keys.First();
            Assert.That(Inventory.RemoveItem(filler), Is.True);
            SkillBoardPreview preview = BoardSystem.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Remove, wood));
            Assert.That(preview.Success, Is.True, preview.Error);
            IUnRegister listener = _architecture.RegisterEvent<SkillBoardChangedEvent>(_ => throw new InvalidOperationException("observer"));
            try { Assert.Throws<InvalidOperationException>(() => BoardSystem.Commit(preview, out _)); }
            finally { listener.UnRegister(); }
            Assert.That(Growth.State.TalentPoints, Is.EqualTo(6));
            Assert.That(Growth.State.Activations, Is.Empty);
            Assert.That(Inventory.Grid.Placements.ContainsKey(wood), Is.True);
            Assert.That(BoardSystem.Commit(preview, out _), Is.False, "通知异常不能把已提交操作变回可重试");
        }

        [Test]
        public void Allocation_RebuildsOneSourceAndPreservesResourcesAndActorConfiguration()
        {
            Charge(1000);
            _actor.RestoreResources(new CombatResourceSnapshot(50, 100, 50, 200), true);
            GrowthCommit(System.Preview(Player, StatIds.Strength, 2, null, ""));
            GrowthCommit(System.Preview(Player, StatIds.Intelligence, 1, null, ""));
            Assert.That(_actor.MaxHealth, Is.EqualTo(104));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(52));
            Assert.That(_actor.MaxMana, Is.EqualTo(202));
            Assert.That(_actor.CurrentMana, Is.EqualTo(50.5f));
            Assert.That(_actor.CaptureBaseStats().GetValue(StatIds.Strength), Is.Zero);
            _actor.SetModifierSource("other", new[]
            {
                new ModifierInstance(StatIds.Strength, ModifierOperation.Increase, ModifierScope.GlobalActor,
                    100, default, default, TagQuery.Empty)
            });
            Assert.That(_actor.Stats.GetValue(StatIds.Strength), Is.EqualTo(4));
            _architecture.SendCommand(new ConfigurePlayerGrowthCommand(_actor, null));
            Assert.That(_actor.Stats.GetValue(StatIds.Strength), Is.EqualTo(4), "替换来源不能重复累计");
            AttributeDetailsSnapshot detail = _architecture.SendQuery(new GetAttributeDetailsQuery());
            Assert.That(detail.Modifiers.Count(m => m.Origin.Kind == ModifierOriginKind.Growth), Is.EqualTo(2));
            Assert.That(Growth.State.Strength, Is.EqualTo(2));
            Assert.That(Growth.State.AttributePoints, Is.EqualTo(3));
            _actor.RestoreResources(new CombatResourceSnapshot(0, _actor.MaxHealth, 0, _actor.MaxMana), false);
            _actor.Revive(Vector3.zero);
            Assert.That(_actor.MaxHealth, Is.EqualTo(108));
            Assert.That(Growth.State.Strength, Is.EqualTo(2));
        }

        [Test]
        public void GrowthAndBoardPreviews_RejectChangedPointsOwnershipAndResources()
        {
            Charge(1000);
            ItemInstance wood = Item("firewood_large");
            Inventory.TryAddItem(wood);
            SkillBoardPreview install = BoardSystem.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            PlayerGrowthPreview allocation = System.Preview(Player, StatIds.Strength, 1, null, "");
            _actor.RestoreResources(new CombatResourceSnapshot(90, 100, 200, 200), true);
            Assert.That(System.Commit(allocation, out _), Is.False, "资源投影变化使预览失效");
            GrowthCommit(System.Preview(Player, StatIds.Strength, 1, null, ""));
            Assert.That(BoardSystem.Commit(install, out _), Is.False, "点数变化使旧棋盘预检失效");
            PlayerGrowthPreview older = System.Preview(Player, StatIds.Dexterity, 1, null, "");
            Charge(10);
            Assert.That(System.Commit(older, out _), Is.False);
            Assert.That(System.Preview("other_player", StatIds.Strength, 1, null, "").Success, Is.False);
            Assert.That(Growth.State.Dexterity, Is.Zero);
        }

        [Test]
        public void TalentPathAndReturn_RequireSeparatePointsAndRefundOnlyCurrentWood()
        {
            Charge(1000);
            ItemInstance wood = Item("firewood_large");
            Inventory.TryAddItem(wood);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.Install, wood, new HexCoordinate(16, 0)));
            FirewoodLayout layout = wood.SkillBoardState.Layout;
            string firstTalent = layout.Edges.Where(e => e.First == "center" || e.Second == "center")
                .Select(e => e.First == "center" ? e.Second : e.First).First();
            Assert.That(System.Preview(Player, null, 1, wood.InstanceId, firstTalent).Success, Is.False);
            GrowthCommit(System.Preview(Player, null, 1, wood.InstanceId, ""));
            Assert.That(Growth.State.Activations.Count, Is.EqualTo(1), "柴薪点亮不批量激活天赋");
            string crack = layout.Nodes.First(n => n.Kind == FirewoodNodeKind.Crack).Id;
            Assert.That(System.Preview(Player, null, 1, wood.InstanceId, crack).Success, Is.False);
            GrowthCommit(System.Preview(Player, null, 1, wood.InstanceId, firstTalent));
            Assert.That(System.Preview(Player, null, 1, wood.InstanceId, firstTalent).Success, Is.False);
            GrowthCommit(System.Preview(Player, StatIds.Dexterity, 1, null, ""));
            long next = Growth.State.Rules.NextCost(Growth.State.EarnedPoints);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.Remove, wood));
            Assert.That(Growth.State.Activations, Is.Empty);
            Assert.That(Growth.State.TalentPoints, Is.EqualTo(Growth.State.EarnedPoints));
            Assert.That(Growth.State.Dexterity, Is.EqualTo(1));
            Assert.That(Growth.State.AttributePoints, Is.EqualTo(5));
            Assert.That(Growth.State.Rules.NextCost(Growth.State.EarnedPoints), Is.EqualTo(next));
            Assert.That(Inventory.Grid.Placements.ContainsKey(wood), Is.True);
        }

        [Test]
        public void DtoRoundTripAndCorruption_RejectLostBalancesUnknownContractsAndDuplicateActivation()
        {
            Charge(1000);
            PlayerGrowthDto dto = RuntimeStateMapper.ToDto(Player, Growth.State.Allocate(StatIds.Strength, 2));
            PlayerGrowthState restored = RuntimeStateMapper.ReadPlayerGrowth(dto, out string error);
            Assert.That(error, Is.Null);
            Assert.That(JToken.DeepEquals(JObject.FromObject(dto), JObject.FromObject(RuntimeStateMapper.ToDto(Player, restored))), Is.True);
            dto.AttributePoints++;
            Assert.That(RuntimeStateMapper.ReadPlayerGrowth(dto, out _), Is.Null);
            dto.AttributePoints--;
            dto.ContractVersion++;
            Assert.That(RuntimeStateMapper.ReadPlayerGrowth(dto, out _), Is.Null);
        }

        [Test]
        public void PureActivationGraph_UsesExplicitEdgesAndActiveRootsThroughCycles()
        {
            string a = Guid.NewGuid().ToString("N");
            string b = Guid.NewGuid().ToString("N");
            string c = Guid.NewGuid().ToString("N");
            var layout = new FirewoodLayout(FirewoodSize.Large, 1, 1,
                new[] { new FirewoodNode("near", new HexCoordinate(1, 0), FirewoodNodeKind.Talent),
                    new FirewoodNode("far", new HexCoordinate(2, 0), FirewoodNodeKind.Talent) },
                new[] { new FirewoodEdge("center", "near"), new FirewoodEdge("near", "far") });
            var root = new FirewoodPlacement(a, layout, SkillBoardGeometry.Roots[0]);
            PlayerGrowthState credits = new PlayerGrowthState().Charge(1000);
            PlayerGrowthState activeRoot = credits.Activate(a, "");
            Assert.That(PlayerGrowthGraph.Validate(activeRoot.Activate(a, "far"), new[] { root }, Array.Empty<FirewoodConnection>()), Is.Not.Null);
            Assert.That(PlayerGrowthGraph.Validate(activeRoot.Activate(a, "near").Activate(a, "far"),
                new[] { root }, Array.Empty<FirewoodConnection>()), Is.Null);
            var small = new FirewoodLayout(FirewoodSize.Small, 1, 1, Array.Empty<FirewoodNode>(), Array.Empty<FirewoodEdge>());
            var placements = new[] { root, new FirewoodPlacement(b, small, new HexCoordinate(21, 0)),
                new FirewoodPlacement(c, layout, SkillBoardGeometry.Roots[1]) };
            FirewoodConnection Link(string first, string second)
            {
                return new FirewoodConnection(new FirewoodPortReference(first, "port"), new FirewoodPortReference(second, "port"));
            }
            FirewoodConnection[] links = { Link(a, b), Link(b, c), Link(c, a) };
            PlayerGrowthState active = activeRoot.Activate(b, "").Activate(c, "");
            Assert.That(PlayerGrowthGraph.Validate(active, placements, links), Is.Null);
            Assert.That(PlayerGrowthGraph.Validate(active, placements, Array.Empty<FirewoodConnection>()), Is.Not.Null);
            PlayerGrowthState refunded = active.ReturnFirewoods(new System.Collections.Generic.HashSet<string> { b, c });
            Assert.That(PlayerGrowthGraph.Validate(refunded, placements.Where(p => p.Id != a), new[] { Link(b, c) }), Is.Null);
        }

        [Test]
        public void OldSystemAfterSessionEnds_CannotCommitCapturedGrowth()
        {
            Charge(1000);
            PlayerGrowthPreview preview = System.Preview(Player, StatIds.Strength, 1, null, "");
            PlayerGrowthSystem previous = System;
            _session.Stop();
            _architecture = _session.Start();
            Assert.That(previous.Commit(preview, out _), Is.False);
            Assert.That(_architecture.GetModel<PlayerGrowthModel>().State.EarnedPoints, Is.Zero);
        }

        ItemInstance Item(string id)
        {
            ItemBaseDefinition definition = _catalog.Resolve<ItemBaseDefinition>(ContentId.Parse("item:" + id)).Value;
            return definition.CreateInstance(_architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 42, ItemRarity.Normal);
        }
        void Charge(int amount)
        {
            ItemInstance bottle = Item("energy_bottle_large");
            bottle.CollectEnergy(amount);
            Assert.That(Inventory.TryAddItem(bottle), Is.True);
            BoardCommit(new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, bottle));
        }
        void BoardCommit(SkillBoardRequest request)
        {
            SkillBoardPreview preview = BoardSystem.Preview(Player, request);
            Assert.That(preview.Success, Is.True, preview.Error);
            Assert.That(BoardSystem.Commit(preview, out string error), Is.True, error);
        }
        void GrowthCommit(PlayerGrowthPreview preview)
        {
            Assert.That(preview.Success, Is.True, preview.Error);
            Assert.That(System.Commit(preview, out string error), Is.True, error);
            Assert.That(System.Commit(preview, out _), Is.False);
        }
    }
}
