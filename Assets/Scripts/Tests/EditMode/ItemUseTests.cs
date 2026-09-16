using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFlare.Tests
{
    public sealed class ItemUseTests
    {
        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();
        readonly List<Object> _objects = new List<Object>();
        IArchitecture _architecture;
        ItemUseSystem _uses;
        InventoryModel _inventory;
        StatusSystem _statuses;
        CombatActor _actor;
        StatusTargetId _target;

        [SetUp]
        public void SetUp()
        {
            _architecture = _fixture.Start();
            _uses = _architecture.GetSystem<ItemUseSystem>();
            _inventory = _architecture.GetModel<InventoryModel>();
            _statuses = _architecture.GetSystem<StatusSystem>();
            var go = new GameObject("item_user");
            go.SetActive(false);
            _objects.Add(go);
            _actor = go.AddComponent<CombatActor>();
            var stats = new StatBlock();
            stats.SetValue(StatIds.Mana, 100);
            _actor.Configure("item_user", ActorTeam.Player, 100, stats, TagSet.Empty);
            go.SetActive(true);
            _architecture.GetModel<CombatModel>().RegisterActor(_actor);
            _target = _statuses.Bind(_actor, PlayerId.LocalPlayer);
            Resources(40, 20);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _objects.AsEnumerable().Reverse()) { if (item != null) { Object.DestroyImmediate(item); } }
            _objects.Clear();
            _fixture.Stop();
        }

        [Test]
        public void PreviewIsReadOnly_CompositeCommitIsVisibleFromFirstEvent_AndCannotReplay()
        {
            ItemInstance item = Item(3, Heal(20), new ItemUseEffect(ItemUseEffectKind.RestoreMana, 50));
            ItemInstance other = item.BaseDefinition.CreateInstance("other_stack", 1, 4);
            Assert.That(other.TrySetQuantity(4), Is.True);
            Assert.That(_inventory.TryAddItemAtWithoutEvents(other, new Vector2Int(3, 0)), Is.True);
            ItemUseRequest request = Request(item);
            ItemUseResult preview = _architecture.SendQuery(new GetItemUseSnapshotQuery(request));
            Assert.That(preview.Succeeded, Is.True);
            Assert.That(preview.Resources.CurrentHealth, Is.EqualTo(60));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(40));
            Assert.That(item.Quantity, Is.EqualTo(3));
            int seen = 0;
            IUnRegister subscription = _architecture.RegisterEvent<ActorResourceChangedEvent>(evt =>
            {
                Assert.That(_actor.CurrentHealth, Is.EqualTo(60));
                Assert.That(_actor.CurrentMana, Is.EqualTo(70));
                Assert.That(item.Quantity, Is.EqualTo(2));
                seen++;
            });
            try { Assert.That(_architecture.SendCommand(new UseItemCommand(request)).Succeeded, Is.True); }
            finally { subscription.UnRegister(); }
            Assert.That(seen, Is.GreaterThan(0));
            Assert.That(other.Quantity, Is.EqualTo(4));
            Assert.That(_architecture.SendCommand(new UseItemCommand(request)).Code, Is.EqualTo(ItemUseCode.AlreadyUsed));
        }

        [TestCase(false, 100f)]
        [TestCase(true, 70f)]
        public void StatusAndHealingFollowConfiguredOrder(bool statusFirst, float expected)
        {
            Resources(20, 20);
            StatusDefinition status = Buff();
            var apply = new ItemUseEffect(ItemUseEffectKind.ApplyStatus, status: status);
            ItemInstance item = statusFirst ? Item(1, apply, Heal(30)) : Item(1, Heal(30), apply);
            ItemUseRequest request = Request(item);
            ItemUseResult preview = _architecture.SendQuery(new GetItemUseSnapshotQuery(request));
            Assert.That(preview.Succeeded, Is.True);
            Assert.That(preview.Resources.CurrentHealth, Is.EqualTo(expected));
            Assert.That(_architecture.SendCommand(new UseItemCommand(request)).Succeeded, Is.True);
            Assert.That(_actor.CurrentHealth, Is.EqualTo(expected));
            Assert.That(_actor.MaxHealth, Is.EqualTo(200));
            Assert.That(_inventory.Grid.Placements.ContainsKey(item), Is.False);
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers.Count, Is.EqualTo(1));
            _statuses.Advance(5);
            Assert.That(_actor.MaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void PercentageRecoveryUsesCandidateMaximum_AndClamps()
        {
            ItemInstance item = Item(1, new ItemUseEffect(ItemUseEffectKind.ApplyStatus, status: Buff()),
                new ItemUseEffect(ItemUseEffectKind.Heal, 25, ItemRecoveryMode.Percent),
                new ItemUseEffect(ItemUseEffectKind.RestoreMana, 500));
            Assert.That(Use(item).Succeeded, Is.True);
            Assert.That(_actor.CurrentHealth, Is.EqualTo(130));
            Assert.That(_actor.CurrentMana, Is.EqualTo(100));
        }

        [Test]
        public void NoChangeDoesNotConsume_ButPartialChangeDoes()
        {
            Resources(100, 100);
            ItemInstance item = Item(2, Heal(10), new ItemUseEffect(ItemUseEffectKind.RestoreMana, 15));
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.NoChange));
            Assert.That(item.Quantity, Is.EqualTo(2));
            Resources(100, 80);
            Assert.That(Use(item).Succeeded, Is.True);
            Assert.That(_actor.CurrentMana, Is.EqualTo(95));
            Assert.That(item.Quantity, Is.EqualTo(1));
        }

        [Test]
        public void InvalidLateEffectDoesNotLeakEarlierRecovery()
        {
            ItemInstance item = Item(2, Heal(30), new ItemUseEffect(ItemUseEffectKind.ApplyStatus));
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.InvalidConfiguration));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(40));
            Assert.That(item.Quantity, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void StalePreparationKeepsExternalChanges(int change)
        {
            ItemInstance item = Item(3, Heal(20), new ItemUseEffect(ItemUseEffectKind.ApplyStatus, status: Buff()));
            RectInt origin = _inventory.Grid.Placements[item];
            using (ItemUsePreparation preparation = _uses.Prepare(Request(item)))
            {
                Assert.That(preparation.Preview.Succeeded, Is.True);
                if (change == 0) { _actor.ReceiveHealing(5); }
                if (change == 1) { item.TrySetQuantity(2); }
                if (change == 2)
                {
                    _statuses.ApplyStatus(_target, StatusMutation.Apply(new StatusRules("other"), new StatusSource(StatusSourceKind.Mechanism, "test")));
                }
                Assert.That(_uses.Commit(preparation).Code, Is.EqualTo(ItemUseCode.StaleVersion));
            }
            Assert.That(_actor.CurrentHealth, Is.EqualTo(change == 0 ? 45 : 40));
            Assert.That(_actor.MaxHealth, Is.EqualTo(100));
            Assert.That(item.Quantity, Is.EqualTo(change == 1 ? 2 : 3));
            Assert.That(_inventory.Grid.Placements[item], Is.EqualTo(origin));
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers.Count, Is.EqualTo(change == 2 ? 1 : 0));
        }

        [Test]
        public void DisposedPreparationDoesNotApplyOrConsume()
        {
            ItemInstance item = Item(2, Heal(20));
            ItemUsePreparation preparation = _uses.Prepare(Request(item));
            preparation.Dispose();
            Assert.That(_uses.Commit(preparation).Code, Is.EqualTo(ItemUseCode.AlreadyUsed));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(40));
            Assert.That(item.Quantity, Is.EqualTo(2));
        }

        [Test]
        public void DispelProtectsBuffsAndProtectedAilments_AndStunDoesNotBlockUse()
        {
            var source = new StatusSource(StatusSourceKind.Mechanism, "test");
            foreach (StatusRules rule in new[] { new StatusRules("benefit"),
                new StatusRules("protected", category: StatusCategory.Ailment, canDispel: false),
                new StatusRules("stunned", category: StatusCategory.Ailment) })
            {
                _statuses.ApplyStatus(_target, StatusMutation.Apply(rule, source,
                    new StatusEffectSnapshot(blockedActions: rule.Id.LocalId == "stunned" ? StatusActionBlock.Cast : StatusActionBlock.None)));
            }
            ItemInstance item = Item(1, new ItemUseEffect(ItemUseEffectKind.Dispel, count: 4096));
            Assert.That(Use(item).Succeeded, Is.True);
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers.Select(layer => layer.Rules.Id.LocalId), Is.EquivalentTo(new[] { "benefit", "protected" }));
            Assert.That(_architecture.SendQuery(new GetActorActionsQuery(_actor)).CanCast, Is.True);
        }

        [Test]
        public void ExplicitUseBlockAndDeadTargetAreRejected()
        {
            _statuses.ApplyStatus(_target, StatusMutation.Apply(new StatusRules("locked"), new StatusSource(StatusSourceKind.Mechanism, "test"),
                new StatusEffectSnapshot(blockedActions: StatusActionBlock.UseItem)));
            ItemInstance item = Item(2, Heal(20));
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.ActionBlocked));
            _statuses.DispelStatuses(_target, new StatusFilter());
            _actor.RestoreResources(new CombatResourceSnapshot(0, 100, 0, 100), false);
            Assert.That(Use(item).Succeeded, Is.False);
            Assert.That(item.Quantity, Is.EqualTo(2));
        }

        [Test]
        public void ConsumableFlagAndUseCapabilityAreIndependent()
        {
            ItemInstance item = Item(1, Heal(10));
            ItemUseDefinition use = item.BaseDefinition.UseDefinition;
            Set(item.BaseDefinition, "_useDefinition", null);
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.NotUsable));
            Set(item.BaseDefinition, "_useDefinition", use);
            Set(item.BaseDefinition, "_consumable", false);
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.InvalidConfiguration));
            Set(use, "_consumeCount", 0);
            Assert.That(Use(item).Succeeded, Is.True);
            Assert.That(_inventory.Grid.Placements.ContainsKey(item), Is.True);
        }

        [Test]
        public void OwnershipQuantityAndMovedPreparedItemAreChecked()
        {
            ItemInstance item = Item(1, Heal(10));
            Set(item.BaseDefinition.UseDefinition, "_consumeCount", 2);
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.InsufficientQuantity));
            Set(item.BaseDefinition.UseDefinition, "_consumeCount", 1);
            using (ItemUsePreparation preparation = _uses.Prepare(Request(item)))
            {
                _inventory.RemoveItemWithoutEvents(item);
                _inventory.TryAddItemAtWithoutEvents(item, new Vector2Int(5, 0));
                Assert.That(_uses.Commit(preparation).Code, Is.EqualTo(ItemUseCode.StaleVersion));
            }
            _inventory.RemoveItemWithoutEvents(item);
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.NotOwned));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(40));
        }

        [Test]
        public void NotificationReentryCannotConsumeTwice_AndPauseAllowsUse()
        {
            ItemInstance item = Item(3, Heal(10));
            ItemUseRequest request = Request(item);
            ItemUseCode reentry = ItemUseCode.Success;
            int count = 0;
            IUnRegister subscription = _architecture.RegisterEvent<ItemUsedEvent>(evt =>
            {
                count++;
                reentry = _architecture.SendCommand(new UseItemCommand(request)).Code;
            });
            try
            {
                using (GamePauseLease pause = GameTimeService.Shared.AcquirePause("item_test"))
                {
                    Assert.That(_architecture.SendCommand(new UseItemCommand(request)).Succeeded, Is.True);
                }
            }
            finally { subscription.UnRegister(); }
            Assert.That(reentry, Is.EqualTo(ItemUseCode.Busy));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(item.Quantity, Is.EqualTo(2));
        }

        [Test]
        public void SessionStopRejectsPreparedAndOldRequests()
        {
            ItemInstance item = Item(2, Heal(10));
            ItemUseRequest request = Request(item);
            using (ItemUsePreparation preparation = _uses.Prepare(request))
            {
                GameArchitectureProvider.TryGetOwnerScope(out LifecycleScope scope);
                scope.BeginStop();
                Assert.That(_uses.Commit(preparation).Code, Is.EqualTo(ItemUseCode.Unavailable));
                Assert.That(item.Quantity, Is.EqualTo(2));
            }
        }

        [Test]
        public void LateRuleConflictRollsBackRecoveryAndEarlierStatus()
        {
            StatusDefinition buff = Buff();
            _statuses.ApplyStatus(_target, StatusMutation.Apply(new StatusRules("potion_buff", maxStacks: 2), new StatusSource(StatusSourceKind.Mechanism, "other")));
            ItemInstance item = Item(2, Heal(20), new ItemUseEffect(ItemUseEffectKind.ApplyStatus, status: buff));
            ItemUseResult result = Use(item);
            Assert.That(result.Code, Is.EqualTo(ItemUseCode.StatusRejected));
            Assert.That(result.StatusCode, Is.EqualTo(StatusResultCode.RuleConflict));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(40));
            Assert.That(_actor.MaxHealth, Is.EqualTo(100));
            Assert.That(item.Quantity, Is.EqualTo(2));
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers.Single().Rules.MaxStacks, Is.EqualTo(2));
        }

        [Test]
        public void FullyResistedStatusIsNoChange_AndOtherEffectsMayStillSucceed()
        {
            StatusDefinition weakness = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusDefinition>("Assets/Data/Preset/Statuses/weakness.asset");
            Assert.That(weakness, Is.Not.Null);
            _actor.SetModifierSource("resistance", new[] { new ModifierInstance(AilmentResistanceResolver.StatId(AilmentKind.Weakness),
                ModifierOperation.Flat, ModifierScope.GlobalActor, 100, default, default, TagQuery.Empty) });
            ItemInstance item = Item(2, new ItemUseEffect(ItemUseEffectKind.ApplyStatus, status: weakness));
            Assert.That(Use(item).Code, Is.EqualTo(ItemUseCode.NoChange));
            Set(item.BaseDefinition.UseDefinition, "_effects", new List<ItemUseEffect>
                { Heal(10), new ItemUseEffect(ItemUseEffectKind.ApplyStatus, status: weakness) });
            Assert.That(Use(item).Succeeded, Is.True);
            Assert.That(_actor.CurrentHealth, Is.EqualTo(50));
            Assert.That(item.Quantity, Is.EqualTo(1));
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers, Is.Empty);
        }

        [Test]
        public void NotificationExceptionLeavesCommittedResultAndPreventsReplay()
        {
            ItemInstance item = Item(2, Heal(10));
            ItemUseRequest request = Request(item);
            int used = 0;
            IUnRegister observed = _architecture.RegisterEvent<ItemUsedEvent>(evt => used++);
            IUnRegister throwing = _architecture.RegisterEvent<ActorResourceChangedEvent>(evt => throw new InvalidOperationException("subscriber"));
            try { Assert.Throws<InvalidOperationException>(() => _architecture.SendCommand(new UseItemCommand(request))); }
            finally { throwing.UnRegister(); observed.UnRegister(); }
            Assert.That(used, Is.EqualTo(1));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(50));
            Assert.That(item.Quantity, Is.EqualTo(1));
            Assert.That(_architecture.SendCommand(new UseItemCommand(request)).Code, Is.EqualTo(ItemUseCode.AlreadyUsed));
            Assert.That(_statuses.Store.IsQuiescent, Is.True);
        }

        [Test]
        public void NonStackableConsumableAndMonotonicRequestWatermark()
        {
            ItemInstance item = Item(1, Heal(10));
            Set(item.BaseDefinition, "_stackable", false);
            ItemUseRequest oldRequest = Request(item);
            ItemUseRequest latest = Request(item);
            Assert.That(_architecture.SendCommand(new UseItemCommand(latest)).Succeeded, Is.True);
            Assert.That(_architecture.SendCommand(new UseItemCommand(oldRequest)).Code, Is.EqualTo(ItemUseCode.AlreadyUsed));
            Assert.That(_inventory.Grid.Placements.ContainsKey(item), Is.False);
        }

        [Test]
        public void UnrelatedGoldChangeDoesNotProduceDuplicateBalanceNotification()
        {
            _inventory.ConfigureCurrency(UnityEditor.AssetDatabase.LoadAssetAtPath<ItemBaseDefinition>("Assets/Data/Preset/Items/金币.asset"));
            ItemInstance item = Item(2, Heal(10));
            using (ItemUsePreparation preparation = _uses.Prepare(Request(item)))
            {
                _inventory.AddGold(5);
                int notifications = 0;
                IUnRegister subscription = _architecture.RegisterEvent<GoldChangedEvent>(evt => notifications++);
                try
                {
                    Assert.That(_uses.Commit(preparation).Succeeded, Is.True);
                    Assert.That(_inventory.Gold, Is.EqualTo(5));
                    Assert.That(notifications, Is.Zero);
                }
                finally { subscription.UnRegister(); }
            }
        }

        [Test]
        public void ForeignPlayerCannotUseLocalInventory()
        {
            var go = new GameObject("foreign"); _objects.Add(go);
            CombatActor foreign = go.AddComponent<CombatActor>();
            foreign.Configure("foreign", ActorTeam.Player, 100, new StatBlock(), TagSet.Empty);
            _architecture.GetModel<CombatModel>().RegisterActor(foreign);
            _statuses.Bind(foreign, new PlayerId("foreign"));
            ItemInstance item = Item(2, Heal(10));
            ItemUseRequest request = _architecture.SendCommand(new CreateItemUseRequestCommand(foreign, item));
            Assert.That(_architecture.SendCommand(new UseItemCommand(request)).Code, Is.EqualTo(ItemUseCode.ActionBlocked));
            Assert.That(item.Quantity, Is.EqualTo(2));
        }

        ItemUseRequest Request(ItemInstance item) { return _architecture.SendCommand(new CreateItemUseRequestCommand(_actor, item)); }
        ItemUseResult Use(ItemInstance item) { return _architecture.SendCommand(new UseItemCommand(Request(item))); }
        static ItemUseEffect Heal(float amount) { return new ItemUseEffect(ItemUseEffectKind.Heal, amount); }
        void Resources(float health, float mana) { _actor.RestoreResources(new CombatResourceSnapshot(health, 100, mana, 100), true); }
        T Asset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>(); _objects.Add(asset); return asset;
        }
        ItemInstance Item(int quantity, params ItemUseEffect[] effects)
        {
            ItemBaseDefinition definition = Asset<ItemBaseDefinition>();
            Set(definition, "_id", "test_potion"); Set(definition, "_itemType", ItemType.Consumable);
            Set(definition, "_stackable", true); Set(definition, "_maxStackSize", 10); Set(definition, "_consumable", true);
            ItemUseDefinition use = Asset<ItemUseDefinition>(); Set(use, "_id", "test_use");
            Set(use, "_effects", effects.ToList()); Set(definition, "_useDefinition", use);
            ItemInstance item = definition.CreateInstance("potion_" + _objects.Count, 1, 0);
            Assert.That(item.TrySetQuantity(quantity), Is.True);
            Assert.That(_inventory.TryAddItemAtWithoutEvents(item, Vector2Int.zero), Is.True);
            return item;
        }
        StatusDefinition Buff()
        {
            StatusDefinition definition = Asset<StatusDefinition>(); Set(definition, "_id", "potion_buff");
            StatDefinition stat = Asset<StatDefinition>(); Set(stat, "_id", StatIds.MaxHealth);
            var modifier = new StatModifierDefinition(); Set(modifier, "_stat", stat); Set(modifier, "_valueRange", new Vector2(100, 100));
            Set(definition, "_modifiers", new List<StatModifierDefinition> { modifier });
            return definition;
        }
        static void Set(object owner, string field, object value)
        {
            owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        }
    }
}
