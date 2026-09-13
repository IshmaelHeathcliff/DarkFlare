using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class StatusSystemTests
    {
        readonly StatusSource _source = new StatusSource(StatusSourceKind.Skill, "test_skill");
        StatusStore _store;
        StatusTargetId _target;

        [SetUp]
        public void SetUp()
        {
            _store = new StatusStore(1);
            _target = _store.RegisterTarget(PlayerId.LocalPlayer);
        }

        [TearDown]
        public void TearDown()
        {
            _store.Dispose();
        }

        [TestCase(StatusClockMode.PerLayer)]
        [TestCase(StatusClockMode.Shared)]
        public void Uniform_RefreshesAllParametersAndCapsStacks(StatusClockMode clock)
        {
            var rules = new StatusRules("buff", maxStacks: 3, duration: 5, clock: clock);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(10), 2));
            _store.Advance(2);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(20), 2));
            StatusTargetSnapshot snapshot = _store.Capture(_target);
            Assert.That(snapshot.GetStacks(), Is.EqualTo(3));
            Assert.That(snapshot.Layers.All(layer => layer.Effects.Strength == 20 && layer.ExpiresAt == 7), Is.True);
            _store.Advance(5);
            Assert.That(_store.Capture(_target).Layers, Is.Empty);
        }

        [Test]
        public void Uniform_PerLayerExpiryCanPreserveExistingTimersWhileSharingValues()
        {
            var rules = new StatusRules("buff", maxStacks: 3, duration: 3, refreshExistingLayers: false);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(1)));
            _store.Advance(1);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(2)));
            StatusTargetSnapshot snapshot = _store.Capture(_target);
            CollectionAssert.AreEqual(new[] { 3d, 4d }, snapshot.Layers.Select(layer => layer.ExpiresAt));
            Assert.That(snapshot.Layers.All(layer => layer.Effects.Strength == 2), Is.True);
            _store.Advance(2);
            Assert.That(_store.Capture(_target).Layers.Single().InstanceId, Is.EqualTo(2));
            Assert.That(_store.Capture(_target).Layers.Single().RemainingSeconds, Is.EqualTo(1));
        }

        [Test]
        public void Uniform_PerLayerFullStackStillUsesConfiguredOverflowRefresh()
        {
            var rules = new StatusRules("buff", maxStacks: 2, duration: 3, refreshExistingLayers: false);
            Push(StatusMutation.Apply(rules, _source, count: 2));
            _store.Advance(1);
            Push(StatusMutation.Apply(rules, _source));
            Assert.That(_store.Capture(_target).Layers.All(layer => layer.ExpiresAt == 4), Is.True);
        }

        [Test]
        public void Strongest_ResumesOriginalWeakCandidateWithoutExtendingIt()
        {
            var rules = new StatusRules("burn", StatusRepeatMode.Strongest, 3, 10);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(10)));
            _store.Advance(1);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(20), duration: 2));
            Assert.That(_store.Capture(_target).GetStacks(activeOnly: true), Is.EqualTo(1));
            _store.Advance(2);
            StatusLayerSnapshot layer = _store.Capture(_target).Layers.Single();
            Assert.That(layer.IsActive, Is.True);
            Assert.That(layer.Effects.Strength, Is.EqualTo(10));
            Assert.That(layer.RemainingSeconds, Is.EqualTo(7));
        }

        [Test]
        public void Strongest_AtCapacityKeepsBestAndOldestTies()
        {
            var rules = new StatusRules("burn", StatusRepeatMode.Strongest, 2);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(10)));
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(20)));
            StatusTargetSnapshot before = _store.Capture(_target);
            Assert.That(Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(10))).Code, Is.EqualTo(StatusResultCode.NoChange));
            Assert.That(_store.Capture(_target).Version, Is.EqualTo(before.Version));
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(30)));
            CollectionAssert.AreEqual(new[] { 20d, 30d }, _store.Capture(_target).Layers.Select(layer => layer.Effects.Strength));
            Assert.That(_store.Capture(_target).Layers.Single(layer => layer.IsActive).Effects.Strength, Is.EqualTo(30));
        }

        [Test]
        public void Strongest_ChoosesWholePayload()
        {
            var rules = new StatusRules("control", StatusRepeatMode.Strongest, 2);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(1, blockedActions: StatusActionBlock.Move)));
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(2, blockedActions: StatusActionBlock.Cast)));
            Assert.That(_store.Capture(_target).Layers.Single(layer => layer.IsActive).Effects.BlockedActions, Is.EqualTo(StatusActionBlock.Cast));
        }

        [Test]
        public void Independent_ReplacesEarliestExpiryAndPreservesOtherLayers()
        {
            var rules = new StatusRules("poison", StatusRepeatMode.Independent, 2, 10);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(1)));
            _store.Advance(1);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(2), duration: 2));
            StatusResult replacement = Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(3), duration: 5));
            Assert.That(replacement.Changes.Single(change => change.Reason == StatusChangeReason.Replaced).Layer.Effects.Strength, Is.EqualTo(2));
            Assert.That(_store.Capture(_target).Layers.Single(layer => layer.InstanceId == 1).ExpiresAt, Is.EqualTo(10));
            _store.Advance(5);
            Assert.That(_store.Capture(_target).Layers.Single().Effects.Strength, Is.EqualTo(1));
        }

        [Test]
        public void Independent_EqualExpiryReplacesEarlierInstance()
        {
            var rules = new StatusRules("poison", StatusRepeatMode.Independent, 2);
            Push(StatusMutation.Apply(rules, _source, count: 2));
            StatusResult result = Push(StatusMutation.Apply(rules, _source));
            Assert.That(result.Changes.Single(change => change.Reason == StatusChangeReason.Replaced).Layer.InstanceId, Is.EqualTo(1));
        }

        [TestCase(StatusRepeatMode.Uniform)]
        [TestCase(StatusRepeatMode.Strongest)]
        [TestCase(StatusRepeatMode.Independent)]
        public void RejectPolicy_DoesNotRefreshOrReplace(StatusRepeatMode mode)
        {
            var rules = new StatusRules("mark", mode, overflow: StatusOverflow.Reject);
            Push(StatusMutation.Apply(rules, _source));
            _store.Advance(1);
            StatusTargetSnapshot before = _store.Capture(_target);
            Assert.That(Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(999))).Code, Is.EqualTo(StatusResultCode.Capacity));
            Assert.That(_store.Capture(_target).Version, Is.EqualTo(before.Version));
            Assert.That(_store.Capture(_target).Layers.Single().ExpiresAt, Is.EqualTo(before.Layers.Single().ExpiresAt));
        }

        [Test]
        public void RejectedBatch_RollsBackEarlierAcceptedLayersAndIds()
        {
            var rules = new StatusRules("mark", StatusRepeatMode.Independent, overflow: StatusOverflow.Reject);
            Assert.That(Push(StatusMutation.Apply(rules, _source, count: 2)).Code, Is.EqualTo(StatusResultCode.Capacity));
            Assert.That(_store.Capture(_target).Layers, Is.Empty);
            Push(StatusMutation.Apply(rules, _source));
            Assert.That(_store.Capture(_target).Layers.Single().InstanceId, Is.EqualTo(1));
        }

        [Test]
        public void RefreshTime_PreservesSelectedPayloadIdentitySourceAndPhase()
        {
            var rules = new StatusRules("poison", StatusRepeatMode.Independent, 2, 5, 1, overflow: StatusOverflow.RefreshTime);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(10), count: 2));
            _store.Advance(0.5);
            Push(StatusMutation.Apply(rules, new StatusSource(StatusSourceKind.Consumable, "potion"),
                new StatusEffectSnapshot(999), refreshInstanceId: 2));
            StatusTargetSnapshot snapshot = _store.Capture(_target);
            StatusLayerSnapshot refreshed = snapshot.Layers.Single(layer => layer.InstanceId == 2);
            Assert.That(refreshed.ExpiresAt, Is.EqualTo(5.5));
            Assert.That(refreshed.Effects.Strength, Is.EqualTo(10));
            Assert.That(refreshed.Source, Is.EqualTo(_source));
            Assert.That(refreshed.NextTickAt, Is.EqualTo(1));
            Assert.That(snapshot.Layers.Single(layer => layer.InstanceId == 1).ExpiresAt, Is.EqualTo(5));
            Assert.That(Push(StatusMutation.Apply(rules, _source, refreshInstanceId: 99)).Code, Is.EqualTo(StatusResultCode.StaleVersion));
        }

        [TestCase(3, 3)]
        [TestCase(2.5, 2)]
        public void Clock_ResolvesLastFullTickBeforeExpiryWithoutTailTick(double duration, int count)
        {
            var order = new List<string>();
            _store.Ticked += tick => order.Add("tick");
            _store.Changed += result => { if (result.Changes.Any(change => change.Reason == StatusChangeReason.Expired)) { order.Add("expired"); } };
            Push(StatusMutation.Apply(new StatusRules("dot", duration: duration, interval: 1), _source));
            _store.Advance(5);
            Assert.That(order.Count(item => item == "tick"), Is.EqualTo(count));
            Assert.That(order.Last(), Is.EqualTo("expired"));
        }

        [Test]
        public void Clock_DecimalIntervalsKeepBoundaryTick()
        {
            var ticks = new List<double>();
            _store.Ticked += tick => ticks.Add(tick.Time);
            Push(StatusMutation.Apply(new StatusRules("dot", duration: 0.3, interval: 0.1), _source));
            _store.Advance(0.3);
            Assert.That(ticks.Count, Is.EqualTo(3));
            Assert.That(ticks.Last(), Is.EqualTo(0.3).Within(1e-12));
        }

        [Test]
        public void Clock_RefreshDoesNotResetPhaseAndReplacementDoes()
        {
            var ticks = new List<double>();
            _store.Ticked += tick => ticks.Add(tick.Time);
            var refresh = new StatusRules("refresh", duration: 2, interval: 1);
            Push(StatusMutation.Apply(refresh, _source));
            _store.Advance(0.75);
            Push(StatusMutation.Apply(refresh, _source));
            _store.Advance(0.25);
            CollectionAssert.AreEqual(new[] { 1d }, ticks);
            Push(StatusMutation.Dispel(new StatusFilter()));
            var replace = new StatusRules("replace", StatusRepeatMode.Independent, duration: 2, interval: 1);
            Push(StatusMutation.Apply(replace, _source));
            _store.Advance(0.5);
            Push(StatusMutation.Apply(replace, _source));
            _store.Advance(1);
            CollectionAssert.AreEqual(new[] { 1d, 2.5 }, ticks);
        }

        [Test]
        public void Clock_SuppressedCandidateKeepsPhaseWithoutCatchUp()
        {
            var ticks = new List<(double Time, double Strength)>();
            _store.Ticked += tick => ticks.Add((tick.Time, tick.Layer.Effects.Strength));
            var rules = new StatusRules("burn", StatusRepeatMode.Strongest, 2, 5, 1);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(1)));
            _store.Advance(0.5);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(2), duration: 2));
            _store.Advance(3);
            CollectionAssert.AreEqual(new[] { (1.5, 2d), (2.5, 2d), (3d, 1d) }, ticks);
        }

        [Test]
        public void Clock_BudgetLargeAndSmallStepsProduceSameSequence()
        {
            List<(long, double)> single = RunClock(new[] { 4d }, 256);
            List<(long, double)> divided = RunClock(Enumerable.Repeat(0.125, 32).ToArray(), 256);
            List<(long, double)> budgeted = RunClock(new[] { 4d }, 1);
            CollectionAssert.AreEqual(single, divided);
            CollectionAssert.AreEqual(single, budgeted);
        }

        [Test]
        public void Clock_PendingAtDeadlineMustStillDrainExpiry()
        {
            Push(StatusMutation.Apply(new StatusRules("dot", duration: 1, interval: 1), _source));
            StatusAdvanceResult first = _store.Advance(1, 1);
            Assert.That(first.HasPending, Is.True);
            Assert.That(first.Time, Is.EqualTo(first.PendingUntil));
            Assert.That(Push(StatusMutation.Dispel(new StatusFilter())).Code, Is.EqualTo(StatusResultCode.Busy));
            Assert.That(_store.Advance(1).Code, Is.EqualTo(StatusResultCode.Busy));
            Assert.That(_store.Advance(0).HasPending, Is.False);
            Assert.That(_store.Capture(_target).Layers, Is.Empty);
        }

        [Test]
        public void Clock_CallbackCanRemoveTargetBeforeFutureTicks()
        {
            int ticks = 0;
            StatusOperation removal = null;
            _store.Ticked += tick => { ticks++; removal = _store.UnregisterTarget(_target); Assert.That(removal.IsCompleted, Is.False); };
            Push(StatusMutation.Apply(new StatusRules("dot", duration: 10, interval: 1), _source));
            _store.Advance(10);
            Assert.That(ticks, Is.EqualTo(1));
            Assert.That(removal.Result.Succeeded, Is.True);
            Assert.That(_store.Capture(_target).IsRegistered, Is.False);
        }

        [Test]
        public void Consume_InsufficientIsAtomicAndActiveSelectionReturnsExactLayer()
        {
            var rules = new StatusRules("mark", StatusRepeatMode.Strongest, 3);
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(1)));
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(2)));
            long version = _store.Capture(_target).Version;
            Assert.That(Push(StatusMutation.Consume("mark", 3)).Code, Is.EqualTo(StatusResultCode.InsufficientStacks));
            Assert.That(_store.Capture(_target).Version, Is.EqualTo(version));
            StatusResult result = Push(StatusMutation.Consume("mark", 1, StatusStackSelection.Active));
            Assert.That(result.Changes.Single().Layer.Effects.Strength, Is.EqualTo(2));
            Assert.That(result.Changes.Single().Reason, Is.EqualTo(StatusChangeReason.Consumed));
            Assert.That(result.Snapshot.Layers.Single().IsActive, Is.True);
        }

        [Test]
        public void Consume_SpecifiedStaleOrDuplicateIdsDoNotPartiallyRemove()
        {
            Push(StatusMutation.Apply(new StatusRules("mark", maxStacks: 2), _source, count: 2));
            Assert.That(Push(StatusMutation.Consume("mark", 2, StatusStackSelection.Instances, new long[] { 1, 9 })).Code,
                Is.EqualTo(StatusResultCode.StaleVersion));
            Assert.That(Push(StatusMutation.Consume("mark", 2, StatusStackSelection.Instances, new long[] { 1, 1 })).Code,
                Is.EqualTo(StatusResultCode.InvalidRequest));
            Assert.That(_store.Capture(_target).GetStacks(), Is.EqualTo(2));
            StatusResult result = Push(StatusMutation.Consume("mark", 1, StatusStackSelection.Instances, new long[] { 2 }));
            Assert.That(result.Changes.Single().Layer.InstanceId, Is.EqualTo(2));
        }

        [Test]
        public void Dispel_FiltersCategoryTagsProtectionAndCount()
        {
            Push(StatusMutation.Apply(new StatusRules("poison", category: StatusCategory.Ailment, tags: new TagSet(new[] { "chaos" })), _source));
            Push(StatusMutation.Apply(new StatusRules("curse", category: StatusCategory.Ailment, canDispel: false), _source));
            Push(StatusMutation.Apply(new StatusRules("buff", canConsume: false), _source));
            StatusResult result = Push(StatusMutation.Dispel(new StatusFilter(category: StatusCategory.Ailment, requiredTags: new TagSet(new[] { "chaos" })), 1));
            Assert.That(result.Changes.Single().Layer.Rules.Id.LocalId, Is.EqualTo("poison"));
            Assert.That(result.Changes.Single().Reason, Is.EqualTo(StatusChangeReason.Dispelled));
            Assert.That(Push(StatusMutation.Consume("buff", 1)).Code, Is.EqualTo(StatusResultCode.Protected));
            Push(StatusMutation.Dispel(new StatusFilter()));
            Assert.That(_store.Capture(_target).Layers.Single().Rules.Id.LocalId, Is.EqualTo("curse"));
        }

        [Test]
        public void Source_RebindingIsIdempotentAndReleaseOnlyRemovesOwnedContribution()
        {
            var owned = new StatusRules("aura", StatusRepeatMode.Independent, 3, lifetime: StatusLifetime.SourceOwned);
            var other = new StatusSource(StatusSourceKind.Equipment, "other_item");
            Push(StatusMutation.Apply(owned, _source));
            Assert.That(Push(StatusMutation.Apply(owned, _source)).Code, Is.EqualTo(StatusResultCode.NoChange));
            Push(StatusMutation.Apply(owned, other));
            Push(StatusMutation.Apply(new StatusRules("potion"), _source));
            Push(StatusMutation.UpdateSource(owned, _source, new StatusEffectSnapshot(42)));
            Assert.That(_store.Capture(_target).GetStacks(new StatusFilter("aura")), Is.EqualTo(2));
            Assert.That(_store.Capture(_target).Layers.Single(layer => layer.Effects.Strength == 42).Source, Is.EqualTo(_source));
            Push(StatusMutation.ReleaseSource(_source));
            Assert.That(_store.Capture(_target).GetStacks(), Is.EqualTo(2));
            Assert.That(_store.Capture(_target).HasStatus("potion"), Is.True);
            Assert.That(Push(StatusMutation.ReleaseSource(_source)).Code, Is.EqualTo(StatusResultCode.NoChange));
        }

        [Test]
        public void Source_FullUniformGroupCannotRefreshAnotherOwnersContribution()
        {
            var rules = new StatusRules("aura", lifetime: StatusLifetime.SourceOwned);
            var other = new StatusSource(StatusSourceKind.Equipment, "other_item");
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(1)));
            long version = _store.Capture(_target).Version;
            Assert.That(Push(StatusMutation.Apply(rules, other, new StatusEffectSnapshot(99))).Code, Is.EqualTo(StatusResultCode.Capacity));
            Assert.That(_store.Capture(_target).Version, Is.EqualTo(version));
            Assert.That(_store.Capture(_target).Layers.Single().Effects.Strength, Is.EqualTo(1));
            Assert.That(Push(StatusMutation.ReleaseSource(other)).Code, Is.EqualTo(StatusResultCode.NoChange));
            Push(StatusMutation.UpdateSource(rules, _source, new StatusEffectSnapshot(2)));
            Assert.That(_store.Capture(_target).Layers.Single().Effects.Strength, Is.EqualTo(2));
        }

        [Test]
        public void Prepare_DoesNotMutateAndRollbackRestoresExactState()
        {
            int notifications = 0;
            _store.Changed += result => notifications++;
            Push(StatusMutation.Apply(new StatusRules("dot", duration: 5, interval: 1), _source));
            _store.Advance(0.5);
            StatusTargetSnapshot before = _store.Capture(_target);
            StatusPreparation preparation = _store.Prepare(_target, new[] { StatusMutation.Dispel(new StatusFilter()) });
            Assert.That(_store.Capture(_target).GetStacks(), Is.EqualTo(1));
            using (StatusCommit commit = _store.Commit(preparation))
            {
                Assert.That(commit.Result.Snapshot.GetStacks(), Is.Zero);
                Assert.That(_store.Capture(_target).GetStacks(), Is.EqualTo(1));
                Assert.That(_store.Advance(1).Code, Is.EqualTo(StatusResultCode.Busy));
                Assert.That(commit.Rollback(), Is.True);
                Assert.That(commit.Publish(), Is.False);
            }
            StatusTargetSnapshot after = _store.Capture(_target);
            Assert.That(after.Version, Is.EqualTo(before.Version));
            Assert.That(after.Layers.Single().InstanceId, Is.EqualTo(before.Layers.Single().InstanceId));
            Assert.That(after.Layers.Single().NextTickAt, Is.EqualTo(1));
            Assert.That(after.Layers.Single().RemainingSeconds, Is.EqualTo(4.5));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(_store.Commit(preparation).Result.Code, Is.EqualTo(StatusResultCode.AlreadyCommitted));
        }

        [Test]
        public void Prepare_RejectsVersionClockAndTargetChanges()
        {
            var apply = StatusMutation.Apply(new StatusRules("mark"), _source);
            StatusPreparation first = _store.Prepare(_target, new[] { apply });
            Push(apply);
            Assert.That(_store.Commit(first).Result.Code, Is.EqualTo(StatusResultCode.StaleVersion));
            StatusPreparation timed = _store.Prepare(_target, new[] { apply });
            _store.Advance(0.1);
            Assert.That(_store.Commit(timed).Result.Code, Is.EqualTo(StatusResultCode.StaleVersion));
            StatusPreparation removed = _store.Prepare(_target, new[] { apply });
            _store.UnregisterTarget(_target);
            Assert.That(_store.Commit(removed).Result.Code, Is.EqualTo(StatusResultCode.InvalidTarget));
        }

        [Test]
        public void Prepare_MultipleEffectsFailAsOneTransaction()
        {
            StatusPreparation preparation = _store.Prepare(_target, new[]
            {
                StatusMutation.Apply(new StatusRules("buff"), _source), StatusMutation.Consume("absent", 1)
            });
            Assert.That(preparation.Preview.Code, Is.EqualTo(StatusResultCode.InsufficientStacks));
            Assert.That(preparation.Preview.Changes, Is.Empty);
            Assert.That(_store.Commit(preparation).Result.Succeeded, Is.False);
            Assert.That(_store.Capture(_target).Layers, Is.Empty);
        }

        [Test]
        public void Publish_NotifiesOnceAndQueuedConsumeRechecksVersion()
        {
            var operations = new List<StatusOperation>();
            int notifications = 0;
            _store.Changed += result =>
            {
                notifications++;
                Assert.That(_store.Capture(_target).Version, Is.EqualTo(result.Snapshot.Version));
                if (!result.Snapshot.HasStatus("mark")) { return; }
                for (int i = 0; i < 2; i++)
                {
                    StatusOperation operation = _store.Execute(_target, new[] { StatusMutation.Consume("mark", 1) }, result.Snapshot.Version);
                    Assert.That(operation.IsCompleted, Is.False);
                    operations.Add(operation);
                }
            };
            using (StatusCommit commit = _store.Commit(_store.Prepare(_target, new[] { StatusMutation.Apply(new StatusRules("mark"), _source) })))
            {
                Assert.That(notifications, Is.Zero);
                Assert.That(commit.Publish(), Is.True);
                Assert.That(commit.Publish(), Is.False);
            }
            Assert.That(operations[0].Result.Code, Is.EqualTo(StatusResultCode.Success));
            Assert.That(operations[1].Result.Code, Is.EqualTo(StatusResultCode.StaleVersion));
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void Identity_SameBaseMonstersAndReboundTargetRemainIsolated()
        {
            MonsterInstanceId first = MonsterInstanceId.Parse("00000000000000000000000000000001:monster:1");
            MonsterInstanceId second = MonsterInstanceId.Parse("00000000000000000000000000000001:monster:2");
            StatusTargetId a = _store.RegisterTarget(first);
            StatusTargetId b = _store.RegisterTarget(second);
            _store.Execute(a, new[] { StatusMutation.Apply(new StatusRules("mark"), _source) });
            Assert.That(_store.Capture(b).Layers, Is.Empty);
            _store.UnregisterTarget(a);
            StatusTargetId rebound = _store.RegisterTarget(first);
            Assert.That(rebound, Is.Not.EqualTo(a));
            Assert.That(_store.Capture(rebound).Layers, Is.Empty);
            Assert.That(_store.Execute(a, new[] { StatusMutation.Dispel(new StatusFilter()) }).Result.Code, Is.EqualTo(StatusResultCode.InvalidTarget));
            using (var otherSession = new StatusStore(2))
            {
                otherSession.RegisterTarget(PlayerId.LocalPlayer);
                Assert.That(otherSession.Execute(_target, new[] { StatusMutation.Dispel(new StatusFilter()) }).Result.Code, Is.EqualTo(StatusResultCode.InvalidTarget));
            }
        }

        [Test]
        public void Snapshot_DoesNotExposeMutableListsOrFollowLaterChanges()
        {
            var payload = new List<DamagePacket> { new DamagePacket(DamageType.Fire, 2, TagSet.Empty) };
            var effects = new StatusEffectSnapshot(periodicDamage: payload);
            Push(StatusMutation.Apply(new StatusRules("dot", interval: 1), _source, effects));
            StatusTargetSnapshot snapshot = _store.Capture(_target);
            payload.Clear();
            Assert.That(effects.PeriodicDamage.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<StatusLayerSnapshot>)snapshot.Layers).Clear());
            Push(StatusMutation.Dispel(new StatusFilter()));
            Assert.That(snapshot.Layers.Count, Is.EqualTo(1));
            Assert.That(_store.Capture(_target).Layers, Is.Empty);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1)]
        public void InvalidNumbers_FailWithoutChangingTimeOrLayers(double invalid)
        {
            Assert.Throws<ArgumentException>(() => new StatusRules("mark", duration: invalid));
            Assert.Throws<ArgumentException>(() => new StatusEffectSnapshot(invalid));
            Assert.That(_store.Advance(invalid).Code, Is.EqualTo(StatusResultCode.InvalidRequest));
            Assert.That(Push(StatusMutation.Apply(new StatusRules("mark"), _source, duration: invalid)).Code,
                Is.EqualTo(StatusResultCode.InvalidRequest));
            Assert.That(_store.Time, Is.Zero);
            Assert.That(_store.Capture(_target).Layers, Is.Empty);
        }

        [Test]
        public void Uniform_EquivalentPayloadAtSameTimeIsNoChange()
        {
            var rules = new StatusRules("buff", lifetime: StatusLifetime.Infinite, interval: 1);
            var modifier = new ModifierInstance("movement_speed", ModifierOperation.More, ModifierScope.GlobalActor,
                10, DamageType.Physical, DamageType.Physical, TagQuery.Empty);
            var packet = new DamagePacket(DamageType.Fire, 2, new TagSet(new[] { "fire" }));
            Push(StatusMutation.Apply(rules, _source, new StatusEffectSnapshot(3, new[] { modifier }, new[] { packet })));
            long version = _store.Capture(_target).Version;
            StatusResult duplicate = Push(StatusMutation.Apply(rules, _source,
                new StatusEffectSnapshot(3, new[] { modifier.WithOrigin(default) }, new[] { packet })));
            Assert.That(duplicate.Code, Is.EqualTo(StatusResultCode.NoChange));
            Assert.That(_store.Capture(_target).Version, Is.EqualTo(version));
            Assert.That(Push(StatusMutation.Apply(rules, _source,
                new StatusEffectSnapshot(3, new[] { modifier }, new[] { packet.WithAmount(4) }))).Code,
                Is.EqualTo(StatusResultCode.Success));
        }

        [Test]
        public void Composite_ApplyAndDispelShareCandidateState()
        {
            StatusPreparation preparation = _store.Prepare(_target, new[]
            {
                StatusMutation.Apply(new StatusRules("poison", category: StatusCategory.Ailment), _source),
                StatusMutation.Apply(new StatusRules("buff"), _source),
                StatusMutation.Dispel(new StatusFilter(category: StatusCategory.Ailment))
            });
            Assert.That(preparation.Preview.Succeeded, Is.True);
            using (StatusCommit commit = _store.Commit(preparation)) { commit.Publish(); }
            Assert.That(_store.Capture(_target).Layers.Single().Rules.Id.LocalId, Is.EqualTo("buff"));
        }

        [Test]
        public void Transaction_WorkBudgetFailureLeavesOriginalStateAndIds()
        {
            var rules = new StatusRules("mark", maxStacks: 4096);
            StatusPreparation preparation = _store.Prepare(_target, new[]
            {
                StatusMutation.Apply(rules, _source, count: 3000), StatusMutation.Apply(rules, _source, count: 2000)
            });
            Assert.That(preparation.Preview.Code, Is.EqualTo(StatusResultCode.Capacity));
            Assert.That(_store.Capture(_target).Layers, Is.Empty);
            Push(StatusMutation.Apply(rules, _source));
            Assert.That(_store.Capture(_target).Layers.Single().InstanceId, Is.EqualTo(1));
        }

        [Test]
        public void PreparedTarget_IsNotInvalidatedByOtherTargetsAtSameTime()
        {
            StatusTargetId other = _store.RegisterTarget(new PlayerId("other_player"));
            var mutation = StatusMutation.Apply(new StatusRules("mark"), _source);
            StatusPreparation prepared = _store.Prepare(_target, new[] { mutation });
            _store.Execute(other, new[] { mutation });
            using (StatusCommit commit = _store.Commit(prepared))
            {
                Assert.That(commit.Result.Code, Is.EqualTo(StatusResultCode.Success));
                commit.Publish();
            }
        }

        [Test]
        public void Dispose_RevokesUnpublishedCommitAndQueuedWork()
        {
            StatusCommit commit = _store.Commit(_store.Prepare(_target,
                new[] { StatusMutation.Apply(new StatusRules("mark"), _source) }));
            _store.Dispose();
            Assert.That(commit.Publish(), Is.False);
            Assert.That(commit.Rollback(), Is.False);
            Assert.That(_store.Capture(_target).IsRegistered, Is.False);
        }

        [Test]
        public void InvalidCombinationsAndRuleReplacementAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new StatusRules("mark", StatusRepeatMode.Independent, clock: StatusClockMode.Shared));
            Assert.Throws<ArgumentException>(() => new StatusRules("mark", StatusRepeatMode.Strongest, overflow: StatusOverflow.RefreshTime));
            Assert.Throws<ArgumentException>(() => new StatusRules("mark", maxStacks: 0));
            Assert.Throws<ArgumentException>(() => new StatusRules("mark", interval: 1e-12));
            Assert.Throws<ArgumentException>(() => new StatusRules("mark", clock: StatusClockMode.Shared, refreshExistingLayers: false));
            Assert.Throws<ArgumentException>(() => StatusMutation.Consume(null, 1));
            Assert.Throws<ArgumentException>(() => new StatusRules("mark", tags: new TagSet(new[] { "Bad ID" })));
            Push(StatusMutation.Apply(new StatusRules("mark"), _source));
            Assert.That(Push(StatusMutation.Apply(new StatusRules("mark", duration: 20), _source)).Code, Is.EqualTo(StatusResultCode.RuleConflict));
            Assert.That(Push(StatusMutation.Apply(new StatusRules("mark"), default)).Code, Is.EqualTo(StatusResultCode.InvalidRequest));
            Assert.That(_store.Advance(double.MaxValue).Code, Is.EqualTo(StatusResultCode.InvalidRequest));
        }

        [Test]
        public void Definition_ValidatesAndProducesFrozenRules()
        {
            var definition = ScriptableObject.CreateInstance<StatusDefinition>();
            try
            {
                typeof(StatusDefinition).GetField("_id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(definition, "mark");
                StatusRules rules = definition.CreateRules();
                Assert.That(definition.ValidateConfiguration(), Is.Empty);
                typeof(StatusDefinition).GetField("_duration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(definition, 9d);
                Assert.That(rules.Duration, Is.EqualTo(5));
                Assert.That(definition.CreateRules().Duration, Is.EqualTo(9));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void Architecture_CommandQueryEventsAndRestartUseSessionOwnedState()
        {
            var fixture = new GameArchitectureTestFixture();
            try
            {
                IArchitecture architecture = fixture.Start();
                StatusSystem system = architecture.GetSystem<StatusSystem>();
                StatusTargetId target = system.Store.RegisterTarget(PlayerId.LocalPlayer);
                int changes = 0;
                architecture.RegisterEvent<StatusChangedEvent>(_ => changes++);
                StatusOperation result = architecture.SendCommand(new ChangeStatusesCommand(target,
                    new[] { StatusMutation.Apply(new StatusRules("mark"), _source) }));
                Assert.That(result.Result.Succeeded, Is.True);
                Assert.That(architecture.SendQuery(new GetStatusSnapshotQuery(target)).GetStacks(), Is.EqualTo(1));
                Assert.That(changes, Is.EqualTo(1));
                IArchitecture restarted = fixture.Restart();
                Assert.That(system.Store.Capture(target).IsRegistered, Is.False);
                Assert.That(restarted.GetSystem<StatusSystem>().Store.Generation, Is.Not.EqualTo(target.Generation));
            }
            finally { fixture.Stop(); }
        }

        StatusResult Push(StatusMutation mutation)
        {
            return _store.Execute(_target, new[] { mutation }).Result;
        }

        List<(long, double)> RunClock(double[] steps, int budget)
        {
            using (var store = new StatusStore(10))
            {
                StatusTargetId target = store.RegisterTarget(PlayerId.LocalPlayer);
                var ticks = new List<(long, double)>();
                store.Ticked += tick => ticks.Add((tick.Layer.InstanceId, tick.Time));
                store.Execute(target, new[] { StatusMutation.Apply(new StatusRules("dot", StatusRepeatMode.Independent, 2, 3, 0.5), _source, count: 2) });
                foreach (double step in steps)
                {
                    StatusAdvanceResult result = store.Advance(step, budget);
                    int safety = 0;
                    while (result.HasPending && safety++ < 100) { result = store.Advance(0, budget); }
                    Assert.That(result.HasPending, Is.False);
                }
                Assert.That(store.Capture(target).Layers, Is.Empty);
                Assert.That(store.Time, Is.EqualTo(4));
                return ticks;
            }
        }
    }
}
