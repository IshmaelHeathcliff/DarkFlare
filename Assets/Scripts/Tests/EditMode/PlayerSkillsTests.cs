using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class PlayerSkillsTests
    {
        const string Player = "skill_player";
        GameArchitectureTestFixture _session;
        IArchitecture _architecture;
        GameObject _object;
        CombatActor _actor;
        ContentCatalog _catalog;
        PlayerSkillStateRegistry Skills => _architecture.GetSystem<PlayerSkillStateRegistry>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ContentCatalog.Build(AssetDatabase.LoadAssetAtPath<ContentCatalogDefinition>(
                "Assets/Data/Preset/Content/正式内容目录.asset")).Catalog;
            _session = new GameArchitectureTestFixture();
            _architecture = _session.Start();
            _object = new GameObject("Skills Player");
            _actor = _object.AddComponent<CombatActor>();
            var stats = new StatBlock();
            stats.SetValue(StatIds.Mana, 100);
            stats.SetValue(StatIds.CriticalChance, 10);
            _actor.Configure(Player, ActorTeam.Player, 100, stats, TagSet.Empty);
            _architecture.GetSystem<CombatSystem>().RegisterActor(_actor);
        }

        [TearDown]
        public void TearDown() { UnityEngine.Object.DestroyImmediate(_object); _session.Stop(); }

        [Test]
        public void WeaponSkill_RequiresEquipmentAndKeepsCooldownWhenWeaponChanges()
        {
            Assert.That(Skills.Capture(_actor).Skill, Is.Null);
            ItemInstance sword = Equip("great_sword");
            Assert.That(Skills.Capture(_actor).Skill, Is.SameAs(sword.BaseDefinition.NormalSkill));
            Skills.Restore(EmptySlots(), 10);
            ItemInstance axe = Equip("war_axe");
            Assert.That(Skills.Capture(_actor).Skill, Is.SameAs(axe.BaseDefinition.NormalSkill));
            Assert.That(axe.BaseDefinition.NormalSkill, Is.Not.SameAs(sword.BaseDefinition.NormalSkill));
            Assert.That(Skills.Capture(_actor).RemainingSeconds, Is.GreaterThan(9));
            Assert.That(Skills.Cast(_actor, -1, sword.InstanceId, Vector3.zero, Vector2.right).Status, Is.EqualTo(SkillCastStatus.InvalidSource));
            Assert.That(Skills.Cast(_actor, -1, axe.InstanceId, Vector3.zero, Vector2.right).Status, Is.EqualTo(SkillCastStatus.Cooldown));
            _architecture.GetSystem<EquipmentSystem>().RestoreLoadout(_actor, new EquipmentLoadout());
            Assert.That(Skills.Capture(_actor).Skill, Is.Null);
        }

        [Test]
        public void FormalWeapons_ProvideRegisteredSkillsWithValidDamageSources()
        {
            ItemBaseDefinition[] weapons = _catalog.GetAll<ItemBaseDefinition>().Where(i => i.ItemType == ItemType.Weapon).ToArray();
            Assert.That(weapons, Is.Not.Empty);
            foreach (ItemBaseDefinition weapon in weapons)
            {
                Assert.That(weapon.NormalSkill, Is.Not.Null, weapon.Id);
                Assert.That(_catalog.Contains(weapon.NormalSkill), Is.True);
                Assert.That(RandomizationConfigurationValidator.Validate(weapon.NormalSkill), Is.Empty);
            }
            Assert.That(weapons.Select(w => w.NormalSkill.Id).Distinct().Count(), Is.EqualTo(weapons.Length));
        }

        [Test]
        public void FlameAssignment_MovesUniqueReferenceAndExtractionClearsOnlyAfterCommit()
        {
            (ItemInstance wood, ItemInstance flame) = InstallFlame();
            Assert.That(Skills.Assign(Player, 0, flame.InstanceId), Is.True);
            flame.SkillBoardState.RestoreCooldown(20);
            Assert.That(Skills.Assign(Player, 5, flame.InstanceId), Is.True);
            Assert.That(Skills.CaptureSlot(0).Skill, Is.Null);
            Assert.That(Skills.CaptureSlot(5).SourceId, Is.EqualTo(flame.InstanceId));
            Assert.That(Skills.CaptureSlot(5).RemainingSeconds, Is.GreaterThan(19));
            Assert.That(Skills.Assign("other", 1, flame.InstanceId), Is.False);
            var slot = new FirewoodPortReference(wood.InstanceId, SkillBoardGeometry.CenterNodeId);
            SkillBoardSystem system = _architecture.GetSystem<SkillBoardSystem>();
            SkillBoardPreview preview = system.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Extract, slot: slot));
            Assert.That(Skills.CaptureSlot(5).Skill, Is.Not.Null);
            Assert.That(system.Commit(preview, out string error), Is.True, error);
            Assert.That(Skills.CaptureSlot(5).Skill, Is.Null);
            Assert.That(flame.SkillBoardState.CooldownRemainingSeconds, Is.GreaterThan(19));
            Assert.That(Skills.Assign(Player, 0, flame.InstanceId), Is.False);
        }

        [Test]
        public void Restore_RejectsDuplicateOrInactiveFlamesAndInvalidCooldownWithoutMutation()
        {
            (_, ItemInstance flame) = InstallFlame();
            Assert.That(Skills.Assign(Player, 2, flame.InstanceId), Is.True);
            string[] duplicates = EmptySlots(); duplicates[0] = flame.InstanceId; duplicates[1] = flame.InstanceId;
            Assert.Throws<ArgumentException>(() => Skills.Restore(duplicates, 0));
            Assert.Throws<ArgumentException>(() => Skills.Restore(EmptySlots(), float.NaN));
            Assert.That(Skills.CaptureSlot(2).SourceId, Is.EqualTo(flame.InstanceId));
            string[] unknown = EmptySlots(); unknown[0] = Guid.NewGuid().ToString("N");
            Assert.Throws<ArgumentException>(() => Skills.Restore(unknown, 0));
        }

        [Test]
        public void FailedCast_DoesNotSpendManaOrBeginCooldownAndRejectsInvalidAim()
        {
            Equip("great_sword");
            (_, ItemInstance flame) = InstallFlame();
            Skills.Assign(Player, 0, flame.InstanceId);
            float mana = _actor.CurrentMana;
            Assert.That(Skills.Cast(_actor, 0, flame.InstanceId, Vector3.zero, Vector2.zero).Status, Is.EqualTo(SkillCastStatus.InvalidAim));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("未预热的 Addressable 引用:"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("投射物 Prefab 未加载:"));
            Assert.That(Skills.Cast(_actor, 0, flame.InstanceId, Vector3.zero, Vector2.right).Status, Is.EqualTo(SkillCastStatus.SpawnFailed));
            Assert.That(_actor.CurrentMana, Is.EqualTo(mana));
            Assert.That(flame.SkillBoardState.CooldownRemainingSeconds, Is.Zero);
        }

        [Test]
        public void AttackSnapshot_KeepsLocalCriticalStatsAndFlameIdentityIsolatedFromWeaponAndOtherFlames()
        {
            ItemInstance weapon = Equip("great_sword");
            ProjectileSkillDefinition skill = weapon.BaseDefinition.NormalSkill;
            var modifier = new ModifierInstance(StatIds.CriticalChance, ModifierOperation.Flat, ModifierScope.Skill,
                50, default, default, TagQuery.Empty);
            AttackSnapshot flame = AttackSnapshotFactory.CreateProjectile(_actor, skill, _architecture.GetModel<EquipmentModel>(), 1,
                new[] { modifier }, "flame_a");
            AttackSnapshot other = AttackSnapshotFactory.CreateProjectile(_actor, skill, _architecture.GetModel<EquipmentModel>(), 1);
            Assert.That(flame.AttackerStats.GetValue(StatIds.CriticalChance), Is.EqualTo(60));
            Assert.That(other.AttackerStats.GetValue(StatIds.CriticalChance), Is.EqualTo(10));
            Assert.That(_actor.Stats.GetValue(StatIds.CriticalChance), Is.EqualTo(10));
            Assert.That(flame.FlameInstanceId, Is.EqualTo("flame_a"));
            Assert.That(other.FlameInstanceId, Is.Empty);
            Assert.That(flame.SourceItemId, Is.EqualTo(weapon.InstanceId));
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void Range_UsesShortestLitEdgeCountAndCountsLongEdgesOnce(int radius, bool expected)
        {
            ItemInstance original = Item("firewood_large");
            FirewoodLayout basis = original.SkillBoardState.Layout;
            FirewoodNode first = basis.Nodes.First(n => n.Kind == FirewoodNodeKind.Talent && n.Position.DistanceTo(default) == 2);
            FirewoodNode source = basis.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent && n.Position.DistanceTo(default) == 2 && n.Id != first.Id)
                .OrderByDescending(n => n.Position.DistanceTo(first.Position)).First();
            Assert.That(source.Position.DistanceTo(first.Position), Is.GreaterThan(1));
            FirewoodEdge[] center = basis.Edges.Where(e => e.First == "center" || e.Second == "center").ToArray();
            string inner = center[0].First == "center" ? center[0].Second : center[0].First;
            var edges = new List<FirewoodEdge>(center) { new FirewoodEdge(inner, first.Id), new FirewoodEdge(first.Id, source.Id) };
            foreach (FirewoodNode node in basis.Nodes.Where(n => n.Id != first.Id && n.Id != source.Id && n.Id != inner))
            { edges.Add(new FirewoodEdge(first.Id, node.Id)); }
            var layout = new FirewoodLayout(basis.Size, basis.Seed, basis.GenerationVersion, basis.Nodes, edges.Distinct());
            var wood = new ItemInstance(Guid.NewGuid().ToString("N"), original.BaseDefinition, ItemRarity.Normal, 1, 42, Array.Empty<ModifierInstance>());
            var marker = new ModifierInstance(StatIds.Damage, ModifierOperation.Increase, ModifierScope.Skill, 123, default, default, TagQuery.Empty);
            wood.InitializeSkillBoardState(new SkillBoardItemState(layout, basis.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent)
                .Select(n => new SkillBoardTalentState(n.Id, n.Id == source.Id ? radius : 1, new[] { marker.WithValue(n.Id == source.Id ? 123 : 0) }))));
            ItemInstance flame = Item("flame_projectile");
            var board = new SkillBoardState(new[] { new InstalledFirewood(wood, SkillBoardGeometry.Roots[0], 0) },
                embeddings: new[] { new SkillBoardEmbedding(new FirewoodPortReference(wood.InstanceId, "center"), flame) });
            PlayerGrowthState growth = new PlayerGrowthState().Charge(10000).Activate(wood.InstanceId, "");
            foreach (FirewoodNode node in basis.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent)) { growth = growth.Activate(wood.InstanceId, node.Id); }
            SkillBoardEffectProjection projection = SkillBoardEffects.Resolve(board, growth);
            Assert.That(projection.Flames[flame.InstanceId].Any(m => m.Value == 123), Is.EqualTo(expected));
            Assert.That(projection.FlameSteps[flame.InstanceId][new FirewoodPortReference(wood.InstanceId, source.Id)], Is.EqualTo(3));
            Assert.That(projection.Global, Is.Empty);
        }

        [Test]
        public void OldSessionSkillSystem_CannotMutateNewSession()
        {
            PlayerSkillStateRegistry old = Skills;
            _session.Stop(); _architecture = _session.Start();
            Assert.That(old.Assign(Player, 0, null), Is.False);
            Assert.Throws<ArgumentException>(() => old.Restore(EmptySlots(), 0));
            Assert.That(_architecture.GetModel<PlayerSkillsModel>().Slots.All(string.IsNullOrEmpty), Is.True);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void EmberRange_RequiresLitPathAndKeepsDisconnectedFlamesIsolatedAfterRotation(int rotation)
        {
            ItemInstance original = Item("firewood_large");
            FirewoodLayout basis = original.SkillBoardState.Layout;
            FirewoodNode crack = basis.Nodes.First(n => n.Kind == FirewoodNodeKind.Crack);
            FirewoodNode inner = basis.Nodes.First(n => n.Position.DistanceTo(default) == 1);
            var edges = new List<FirewoodEdge>();
            foreach (FirewoodNode node in basis.Nodes)
            {
                if (node.Position.DistanceTo(default) == 1) { edges.Add(new FirewoodEdge("center", node.Id)); }
                if (node.Id != crack.Id) { edges.Add(new FirewoodEdge(crack.Id, node.Id)); }
            }
            var layout = new FirewoodLayout(basis.Size, basis.Seed, basis.GenerationVersion, basis.Nodes, edges);
            Assert.That(layout.Validate(), Is.Empty);
            var marker = new ModifierInstance(StatIds.Damage, ModifierOperation.Increase, ModifierScope.Skill,
                123, default, default, TagQuery.Empty);
            var wood = new ItemInstance(Guid.NewGuid().ToString("N"), original.BaseDefinition, ItemRarity.Normal, 1, 42, Array.Empty<ModifierInstance>());
            wood.InitializeSkillBoardState(new SkillBoardItemState(layout, basis.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent)
                .Select(n => new SkillBoardTalentState(n.Id, 1, new[] { marker.WithValue(0) }))));
            var ember = new ItemInstance(Guid.NewGuid().ToString("N"), Item("ember_power").BaseDefinition, ItemRarity.Normal, 1, 42, new[] { marker });
            ember.InitializeSkillBoardState(new SkillBoardItemState(emberRadius: 2));
            ItemInstance flame = Item("flame_projectile"); ItemInstance otherFlame = Item("flame_projectile");
            var board = new SkillBoardState(new[] { new InstalledFirewood(wood, SkillBoardGeometry.Roots[0], rotation),
                new InstalledFirewood(original, SkillBoardGeometry.Roots[1], 0) }, embeddings: new[]
            {
                new SkillBoardEmbedding(new FirewoodPortReference(wood.InstanceId, "center"), flame),
                new SkillBoardEmbedding(new FirewoodPortReference(wood.InstanceId, crack.Id), ember),
                new SkillBoardEmbedding(new FirewoodPortReference(original.InstanceId, "center"), otherFlame)
            });
            Assert.That(board.Validate(), Is.Null);
            PlayerGrowthState growth = new PlayerGrowthState().Charge(10000).Activate(original.InstanceId, "");
            Assert.That(SkillBoardEffects.Resolve(board, growth).Flames.ContainsKey(flame.InstanceId), Is.False);
            growth = growth.Activate(wood.InstanceId, "");
            Assert.That(SkillBoardEffects.Resolve(board, growth).Flames[flame.InstanceId].Any(m => m.Origin.Kind == ModifierOriginKind.Ember), Is.False);
            growth = growth.Activate(wood.InstanceId, inner.Id);
            SkillBoardEffectProjection projected = SkillBoardEffects.Resolve(board, growth);
            Assert.That(projected.Flames[flame.InstanceId].Single(m => m.Origin.Kind == ModifierOriginKind.Ember).Origin.StatusId, Is.EqualTo(ember.InstanceId));
            Assert.That(projected.Flames[otherFlame.InstanceId].Any(m => m.Origin.Kind == ModifierOriginKind.Ember), Is.False);
            Assert.That(projected.FlameSteps[flame.InstanceId][new FirewoodPortReference(wood.InstanceId, crack.Id)], Is.EqualTo(2));
            Assert.That(projected.FlameSteps[otherFlame.InstanceId].ContainsKey(new FirewoodPortReference(wood.InstanceId, crack.Id)), Is.False);
            Assert.That(projected.Global, Is.Empty);
        }

        [Test]
        public void GlobalTalent_RebuildsOnceAcrossGrowthRestoreAndEquipmentChanges()
        {
            (ItemInstance wood, _) = InstallFlame();
            string first = wood.SkillBoardState.Layout.Edges.Where(e => e.First == "center" || e.Second == "center")
                .Select(e => e.First == "center" ? e.Second : e.First).First();
            var growth = _architecture.GetSystem<PlayerGrowthSystem>();
            Assert.That(growth.Commit(growth.Preview(Player, null, 1, wood.InstanceId, first), out _), Is.True);
            ModifierInstance[] original = _actor.Modifiers.Where(m => m.Origin.Kind == ModifierOriginKind.Talent).ToArray();
            Assert.That(original.Length, Is.EqualTo(1));
            growth.Restore(Player, _architecture.GetModel<PlayerGrowthModel>().State, _actor);
            Equip("great_sword");
            Assert.That(_actor.Modifiers.Count(m => m.Origin.Kind == ModifierOriginKind.Talent), Is.EqualTo(1));
            Assert.That(_actor.Modifiers.Single(m => m.Origin.Kind == ModifierOriginKind.Talent).Value, Is.EqualTo(original[0].Value));
        }

        (ItemInstance Wood, ItemInstance Flame) InstallFlame()
        {
            ItemInstance wood = Item("firewood_large"); ItemInstance flame = Item("flame_projectile");
            _architecture.GetModel<InventoryModel>().TryAddItem(wood);
            _architecture.GetModel<InventoryModel>().TryAddItem(flame);
            var board = _architecture.GetSystem<SkillBoardSystem>();
            Assert.That(board.Commit(board.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Install, wood, SkillBoardGeometry.Roots[0])), out _), Is.True);
            _architecture.GetModel<PlayerGrowthModel>().State = new PlayerGrowthState().Charge(1000);
            var growth = _architecture.GetSystem<PlayerGrowthSystem>();
            Assert.That(growth.Commit(growth.Preview(Player, null, 1, wood.InstanceId, ""), out _), Is.True);
            Assert.That(board.Commit(board.Preview(Player, new SkillBoardRequest(SkillBoardOperation.Embed, flame,
                slot: new FirewoodPortReference(wood.InstanceId, "center"))), out _), Is.True);
            return (wood, flame);
        }

        ItemInstance Equip(string id)
        {
            ItemInstance item = Item(id); var loadout = new EquipmentLoadout(); loadout.TrySet(EquipmentSlot.Weapon, item);
            _architecture.GetSystem<EquipmentSystem>().RestoreLoadout(_actor, loadout); return item;
        }
        ItemInstance Item(string id) { return _catalog.Resolve<ItemBaseDefinition>(ContentId.Parse("item:" + id)).Value.CreateInstance(Guid.NewGuid().ToString("N"), 1, 42, ItemRarity.Normal); }
        static string[] EmptySlots() { return Enumerable.Repeat(string.Empty, 6).ToArray(); }
    }
}
