using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class EnergyBottleTests
    {
        readonly List<GameObject> _objects = new List<GameObject>();
        GameArchitectureTestFixture _session;
        IArchitecture _architecture;
        ContentCatalog _catalog;
        CombatActor _player;
        MonsterDefinition _monsterDefinition;
        [SetUp]
        public void SetUp()
        {
            _catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>("Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            _monsterDefinition = UnityEngine.Object.Instantiate(_catalog.GetAll<MonsterDefinition>().First(d => d.BossBehavior == null));
            typeof(MonsterDefinition).GetField("_lootTable", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_monsterDefinition, null);
            _session = new GameArchitectureTestFixture(); _architecture = _session.Start();
            _player = Actor("local_player", ActorTeam.Player);
        }
        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in _objects) { if (obj != null) { UnityEngine.Object.DestroyImmediate(obj); } }
            _objects.Clear(); _session.Stop(); UnityEngine.Object.DestroyImmediate(_monsterDefinition);
        }
        [Test]
        public void Reward_GrowsWithAffixCountBothRaritiesAndRejectsInvalidOrOverflowingConfiguration()
        {
            int plain = MonsterEnergyReward.Calculate(10, MonsterRarity.Normal, 5, 5, Array.Empty<MonsterRarity>());
            int affixed = MonsterEnergyReward.Calculate(10, MonsterRarity.Normal, 5, 5, new[] { MonsterRarity.Normal });
            Assert.That(affixed, Is.GreaterThan(plain));
            Assert.That(MonsterEnergyReward.Calculate(10, MonsterRarity.Rare, 5, 5, new[] { MonsterRarity.Normal }), Is.GreaterThan(affixed));
            Assert.That(MonsterEnergyReward.Calculate(10, MonsterRarity.Normal, 5, 5, new[] { MonsterRarity.Rare }), Is.GreaterThan(affixed));
            Assert.Throws<OverflowException>(() => MonsterEnergyReward.Calculate(int.MaxValue, MonsterRarity.Unique, 1, 1, Array.Empty<MonsterRarity>()));
            Assert.Throws<ArgumentException>(() => MonsterEnergyReward.Calculate(-1, MonsterRarity.Normal, 1, 1, Array.Empty<MonsterRarity>()));
            Assert.Throws<ArgumentException>(() => MonsterEnergyReward.Calculate(10, MonsterRarity.Normal, 1, 1, new[] { (MonsterRarity)99 }));
        }
        [Test]
        public void EmptySlot_DiscardsEnergyAndEquippingLaterDoesNotReplayDeath()
        {
            CombatActor monster = Monster();
            ActorDiedEvent death = Kill(monster);
            ItemInstance bottle = EquipBottle();
            _architecture.SendEvent(death);
            Assert.That(bottle.SkillBoardState.Energy, Is.Zero);
            Kill(Monster());
            Assert.That(bottle.SkillBoardState.Energy, Is.GreaterThan(0));
        }
        [Test]
        public void FullCapacity_DiscardsOverflowAndRequiresManualSwapBeforeNewKills()
        {
            ItemInstance bottle = EquipBottle();
            bottle.CollectEnergy(bottle.BaseDefinition.SkillBoard.EnergyCapacity - 3);
            EnergyCollectedEvent captured = default;
            int events = 0;
            IUnRegister registration = _architecture.RegisterEvent<EnergyCollectedEvent>(e => { captured = e; events++; });
            ActorDiedEvent first = Kill(Monster());
            Assert.That(captured.Collected, Is.EqualTo(3));
            Assert.That(captured.Discarded, Is.EqualTo(captured.Reward - 3));
            int revision = bottle.Revision;
            _architecture.SendEvent(first); Kill(Monster());
            Assert.That(events, Is.EqualTo(1));
            Assert.That(bottle.Revision, Is.EqualTo(revision));
            ItemInstance next = EquipBottle("energy_bottle_medium");
            _architecture.SendEvent(first);
            Assert.That(next.SkillBoardState.Energy, Is.Zero);
            Kill(Monster());
            Assert.That(next.SkillBoardState.Energy, Is.GreaterThan(0));
            Assert.That(bottle.SkillBoardState.Energy, Is.EqualTo(1000));
            Assert.That(_architecture.GetModel<InventoryModel>().Grid.Placements.ContainsKey(bottle), Is.True);
            registration.UnRegister();
        }
        [Test]
        public void Attribution_RejectsMissingForeignOrNonPlayerSourceAndDoesNotAllowChangedReplay()
        {
            ItemInstance bottle = EquipBottle();
            CombatActor monster = Monster();
            ActorDiedEvent invalid = Kill(monster, "another_player");
            Assert.That(bottle.SkillBoardState.Energy, Is.Zero);
            invalid.Source = Source(_player.ActorId);
            _architecture.SendEvent(invalid);
            Assert.That(bottle.SkillBoardState.Energy, Is.Zero);
            CombatActor other = Monster(); other.ReceiveDamage(Lethal());
            _architecture.SendEvent(new ActorDiedEvent { Actor = other });
            Assert.That(bottle.SkillBoardState.Energy, Is.Zero);
        }
        [Test]
        public void Attribution_RejectsAliveUnregisteredAndForeignRunActors()
        {
            ItemInstance bottle = EquipBottle();
            CombatActor alive = Monster();
            _architecture.SendEvent(new ActorDiedEvent { Actor = alive, Source = Source(_player.ActorId) });
            Assert.That(bottle.SkillBoardState.Energy, Is.Zero);
            CombatActor removed = Monster(); removed.ReceiveDamage(Lethal());
            _architecture.GetSystem<CombatSystem>().UnregisterActor(removed);
            _architecture.SendEvent(new ActorDiedEvent { Actor = removed, Source = Source(_player.ActorId) });
            CombatActor foreign = Monster(new RunInstanceIdGenerator(new RunId("11111111111111111111111111111111")).NextMonsterId());
            Kill(foreign);
            Assert.That(bottle.SkillBoardState.Energy, Is.Zero);
        }
        [Test]
        public void DuplicateDeath_CollectsOnceAndDoesNotConsumeGameplayRandomness()
        {
            ItemInstance bottle = EquipBottle();
            GameplayRandomSystem random = _architecture.GetSystem<GameplayRandomSystem>(); random.Configure(true, 18);
            string before = Newtonsoft.Json.JsonConvert.SerializeObject(random.CaptureState());
            ActorDiedEvent death = Kill(Monster());
            int energy = bottle.SkillBoardState.Energy;
            int revision = bottle.Revision;
            _architecture.SendEvent(death);
            Assert.That(bottle.SkillBoardState.Energy, Is.EqualTo(energy));
            Assert.That(bottle.Revision, Is.EqualTo(revision));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(random.CaptureState()), Is.EqualTo(before));
        }
        [Test]
        public void NewSession_DoesNotCarryBottleOrSettlementAcrossPlayers()
        {
            ItemInstance bottle = EquipBottle(); Kill(Monster());
            int energy = bottle.SkillBoardState.Energy;
            foreach (GameObject obj in _objects) { UnityEngine.Object.DestroyImmediate(obj); }
            _objects.Clear(); _architecture = _session.Restart();
            _player = Actor("second_profile", ActorTeam.Player);
            Assert.That(_architecture.GetModel<SkillBoardModel>().State.EnergyBottle, Is.Null);
            Kill(Monster());
            Assert.That(bottle.SkillBoardState.Energy, Is.EqualTo(energy));
        }

        CombatActor Actor(string id, ActorTeam team)
        {
            var obj = new GameObject(id); obj.SetActive(false); _objects.Add(obj);
            CombatActor actor = obj.AddComponent<CombatActor>();
            actor.Configure(id, team, 20, new StatBlock(), TagSet.Empty);
            obj.SetActive(true);
            _architecture.GetSystem<CombatSystem>().RegisterActor(actor);
            return actor;
        }
        CombatActor Monster(MonsterInstanceId id = default)
        {
            if (string.IsNullOrEmpty(id.Value)) { id = _architecture.GetUtility<IRunInstanceIdGenerator>().NextMonsterId(); }
            MonsterDefinition definition = _monsterDefinition;
            var obj = new GameObject("Energy Monster"); obj.SetActive(false); _objects.Add(obj);
            CombatActor actor = obj.AddComponent<CombatActor>();
            actor.Configure(id.Value, ActorTeam.Monster, 20, new StatBlock(), TagSet.Empty);
            MonsterController controller = obj.AddComponent<MonsterController>();
            typeof(MonsterController).GetField("_definition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, definition);
            typeof(MonsterController).GetField("_instance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller,
                new MonsterInstanceData(id, new MonsterInstanceRandomSeeds(15), 20, new StatBlock(), new StatBlock(), Array.Empty<MonsterAffixInstance>(), Array.Empty<ModifierInstance>()));
            obj.SetActive(true);
            _architecture.GetSystem<CombatSystem>().RegisterActor(actor);
            return actor;
        }
        ItemInstance EquipBottle(string id = "energy_bottle_small")
        {
            ItemInstance bottle = _catalog.Resolve<ItemBaseDefinition>(ContentId.Parse("item:" + id)).Value.CreateInstance(
                _architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 11, ItemRarity.Normal);
            _architecture.GetModel<InventoryModel>().TryAddItem(bottle);
            SkillBoardSystem board = _architecture.GetSystem<SkillBoardSystem>();
            SkillBoardPreview preview = board.Preview(_player.ActorId, new SkillBoardRequest(SkillBoardOperation.EquipBottle, bottle));
            Assert.That(preview.Success, Is.True, preview.Error);
            Assert.That(board.Commit(preview, out _), Is.True);
            return bottle;
        }
        ActorDiedEvent Kill(CombatActor monster, string source = null)
        {
            Assert.That(monster.ReceiveDamage(Lethal()), Is.True);
            var death = new ActorDiedEvent { Actor = monster, Source = Source(source ?? _player.ActorId), Form = DamageForm.Hit };
            _architecture.SendEvent(death);
            return death;
        }
        static DamageSourceSnapshot Source(string id) { return new DamageSourceSnapshot(id, ActorTeam.Player, "test", "", CombatTagContext.Empty); }
        static DamageResult Lethal()
        { return new DamageResult(true, false, new Dictionary<DamageType, float> { { DamageType.Physical, 1000 } }, new Dictionary<DamageType, float> { { DamageType.Physical, 1000 } }); }
    }
}
