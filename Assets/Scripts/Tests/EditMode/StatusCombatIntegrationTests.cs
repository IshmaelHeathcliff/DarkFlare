using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFlare.Tests
{
    public sealed class StatusCombatIntegrationTests
    {
        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();
        readonly List<Object> _objects = new List<Object>();
        readonly StatusSource _source = new StatusSource(StatusSourceKind.Mechanism, "test");
        IArchitecture _architecture;
        StatusSystem _statuses;
        CombatActor _actor;
        StatusTargetId _target;

        [SetUp]
        public void SetUp()
        {
            _architecture = _fixture.Start();
            _statuses = _architecture.GetSystem<StatusSystem>();
            _actor = Actor(ActorTeam.Player);
            _target = _statuses.Bind(_actor, PlayerId.LocalPlayer);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _objects.AsEnumerable().Reverse()) { if (item != null) { Object.DestroyImmediate(item); } }
            _objects.Clear();
            _fixture.Stop();
        }

        [TestCase(StatusRepeatMode.Uniform, 130f)]
        [TestCase(StatusRepeatMode.Independent, 133.1f)]
        public void EffectiveLayers_PreserveDistinctMoreSemantics(StatusRepeatMode mode, float expected)
        {
            Apply(new StatusRules("more", mode, 3), new StatusEffectSnapshot(modifiers: new[] { Mod(10, ModifierOperation.More) }), 3);
            Assert.That(_actor.MaxHealth, Is.EqualTo(expected).Within(.001));
            _statuses.TryConsumeStatusStacks(_target, "more", 1);
            Assert.That(_actor.MaxHealth, Is.EqualTo(mode == StatusRepeatMode.Uniform ? 120 : 121).Within(.001));
        }

        [Test]
        public void SourcesAndResources_CommitOnceAndSurviveOtherSourceRemoval()
        {
            _actor.ReceiveDamage(Damage(50));
            _actor.TrySpendMana(50);
            int revision = _actor.StatsRevision;
            int notifications = 0;
            _architecture.RegisterEvent<StatusChangedEvent>(e =>
            {
                if (!e.Result.Changes.Any(change => change.Reason == StatusChangeReason.Applied)) { return; }
                notifications++;
                Assert.That(_actor.CurrentHealth, Is.EqualTo(100));
                Assert.That(_actor.MaxHealth, Is.EqualTo(200));
                Assert.That(_statuses.GetStatusSnapshot(_target).Version, Is.EqualTo(e.Result.Snapshot.Version));
            });
            Apply(new StatusRules("health", maxStacks: 2), new StatusEffectSnapshot(modifiers: new[] { Mod(50) }), 2);
            Assert.That(_actor.StatsRevision, Is.EqualTo(revision + 1));
            Assert.That(notifications, Is.EqualTo(1));
            _actor.SetModifierSource("equipment", new[] { Mod(100, stat: StatIds.Mana) });
            Assert.That(_actor.CurrentMana, Is.EqualTo(100));
            _statuses.Advance(5);
            Assert.That(_actor.CurrentHealth, Is.EqualTo(50));
            Assert.That(_actor.MaxHealth, Is.EqualTo(100));
            Assert.That(_actor.MaxMana, Is.EqualTo(200));
            _actor.SetModifierSource("equipment", null);
            Assert.That(_actor.CurrentMana, Is.EqualTo(50));
        }

        [Test]
        public void RefreshAndTick_DoNotRebuildUnchangedEffects()
        {
            var rules = new StatusRules("buff", interval: 1);
            var effects = new StatusEffectSnapshot(modifiers: new[] { Mod(10) });
            Apply(rules, effects);
            int revision = _actor.StatsRevision;
            _statuses.Advance(1);
            Apply(rules, effects);
            Assert.That(_actor.StatsRevision, Is.EqualTo(revision));
        }

        [Test]
        public void DeferredCommit_RollbackAndStaleResourcesNeverLeakEffects()
        {
            StatusMutation mutation = StatusMutation.Apply(new StatusRules("health"), _source, new StatusEffectSnapshot(modifiers: new[] { Mod(100) }));
            int revision = _actor.StatsRevision;
            using (StatusCommit commit = _statuses.Store.Commit(_statuses.Store.Prepare(_target, new[] { mutation })))
            {
                Assert.That(_actor.MaxHealth, Is.EqualTo(100));
                Assert.That(_statuses.GetStatusSnapshot(_target).Layers, Is.Empty);
                commit.Rollback();
            }
            Assert.That(_actor.StatsRevision, Is.EqualTo(revision));
            using (StatusCommit commit = _statuses.Store.Commit(_statuses.Store.Prepare(_target, new[] { mutation })))
            {
                _actor.TrySpendMana(1);
                Assert.That(commit.Publish(), Is.False);
                Assert.That(commit.Result.Code, Is.EqualTo(StatusResultCode.StaleVersion));
            }
            Assert.That(_actor.CurrentMana, Is.EqualTo(99));
            Assert.That(_actor.MaxHealth, Is.EqualTo(100));
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers, Is.Empty);
        }

        [Test]
        public void OverflowPreparation_IsRejectedWithoutChangingStateOrResources()
        {
            StatusTargetSnapshot before = _statuses.GetStatusSnapshot(_target);
            CombatResourceSnapshot resources = _actor.Resources;
            StatusOperation operation = _statuses.ApplyStatus(_target, StatusMutation.Apply(new StatusRules("overflow", maxStacks: 2),
                _source, new StatusEffectSnapshot(modifiers: new[] { Mod(float.MaxValue) }), 2));
            Assert.That(operation.Result.Code, Is.EqualTo(StatusResultCode.InvalidRequest));
            Assert.That(_statuses.GetStatusSnapshot(_target).Version, Is.EqualTo(before.Version));
            Assert.That(_actor.Resources, Is.EqualTo(resources));
        }

        [Test]
        public void Strongest_UsesWholeCandidateAndKeepsTagsFromSuppressedLayers()
        {
            var rules = new StatusRules("strong", StatusRepeatMode.Strongest, 2, tags: new TagSet(new[] { "marked" }));
            Apply(rules, new StatusEffectSnapshot(1, new[] { Mod(10) }, blockedActions: StatusActionBlock.Attack));
            _statuses.ApplyStatus(_target, StatusMutation.Apply(rules, _source,
                new StatusEffectSnapshot(2, new[] { Mod(20) }, blockedActions: StatusActionBlock.Move), duration: 1));
            Assert.That(_actor.MaxHealth, Is.EqualTo(120));
            Assert.That(Actions().CanAttack, Is.True);
            Assert.That(Actions().CanMove, Is.False);
            _statuses.Advance(1);
            Assert.That(_actor.MaxHealth, Is.EqualTo(110));
            Assert.That(Actions().CanAttack, Is.False);
            Assert.That(_actor.Tags.ContainsAll(rules.Tags), Is.True);
        }

        [Test]
        public void SourceOrder_OverridesRemainDeterministic()
        {
            _actor.SetModifierSource("z", new[] { Mod(130, ModifierOperation.Override) });
            _actor.SetModifierSource("a", new[] { Mod(120, ModifierOperation.Override) });
            Assert.That(_actor.MaxHealth, Is.EqualTo(130));
            _actor.SetModifierSource("z", null);
            _actor.SetModifierSource("z", new[] { Mod(130, ModifierOperation.Override) });
            Assert.That(_actor.MaxHealth, Is.EqualTo(130));
        }

        [Test]
        public void ReusedActorWithNewIdentity_DoesNotKeepPreviousTargetOrEffects()
        {
            CombatActor enemy = Enemy(1);
            CombatActor other = Enemy(2);
            StatusTargetId previous = _statuses.GetTarget(enemy);
            _statuses.ApplyStatus(previous, StatusMutation.Apply(new StatusRules("old"), _source,
                new StatusEffectSnapshot(modifiers: new[] { Mod(50) })));
            Assert.That(other.MaxHealth, Is.EqualTo(100));
            Assert.Throws<ArgumentException>(() => _statuses.Bind(enemy,
                MonsterInstanceId.Parse("00000000000000000000000000000001:monster:2")));
            Assert.That(_statuses.GetTarget(enemy), Is.EqualTo(previous));
            Assert.That(enemy.MaxHealth, Is.EqualTo(150));
            StatusTargetId current = _statuses.Bind(enemy, MonsterInstanceId.Parse("00000000000000000000000000000001:monster:3"));
            Assert.That(current.ActorKey, Does.EndWith(":3"));
            Assert.That(_statuses.GetStatusSnapshot(previous).IsRegistered, Is.False);
            Assert.That(_statuses.GetStatusSnapshot(current).Layers, Is.Empty);
            Assert.That(enemy.MaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void PeriodicBase_IsFrozenOnceAndUsesLiveTargetResistance()
        {
            CombatActor enemy = Enemy(1);
            _actor.SetModifierSource("equipment", new[] { Mod(100, ModifierOperation.Increase, StatIds.Damage) });
            var source = new StatusSource(StatusSourceKind.Skill, "burn", _target);
            StatusTargetId enemyTarget = _statuses.GetTarget(enemy);
            var rules = new StatusRules("burn", duration: 4, interval: 1);
            StatusOperation operation = _statuses.ApplyStatus(enemyTarget, StatusMutation.Apply(rules, source,
                new StatusEffectSnapshot(periodicDamage: new[] { new DamagePacket(DamageType.Fire, 10, TagSet.Empty) }, damageStage: StatusDamageStage.Base)));
            Assert.That(operation.Result.Succeeded, Is.True);
            _actor.SetModifierSource("equipment", null);
            _statuses.Advance(1);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(80));
            enemy.SetModifierSource("equipment", new[] { Mod(50, stat: StatIds.FireResistance) });
            _statuses.Advance(1);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(70));
        }

        [Test]
        public void PeriodicPhysical_SkipsArmorHitRandomAndImpact()
        {
            CombatActor enemy = Enemy(1);
            enemy.SetModifierSource("monster", new[] { Mod(99999, stat: StatIds.Armor),
                new ModifierInstance(StatIds.Damage, ModifierOperation.More, ModifierScope.TargetTaken, 100, default, default, TagQuery.Empty) });
            GameplayRandomSystem random = _architecture.GetSystem<GameplayRandomSystem>();
            random.Configure(true, 123);
            GameplayRandomState before = random.CaptureState();
            DamageResult result = _architecture.GetSystem<CombatSystem>().ApplyPeriodicDamage(
                new DamageSourceSnapshot(_target.ActorKey, ActorTeam.Player), enemy, new[] { new DamagePacket(DamageType.Physical, 10, TagSet.Empty) });
            Assert.That(result.TotalDamage, Is.EqualTo(20));
            Assert.That(result.IsHit, Is.False);
            Assert.That(result.IsCritical, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.NotApplicable));
            Assert.That(CombatEffectPalette.ShouldPlayActorHit(result), Is.False);
            Assert.That(CombatEffectPalette.ShouldPlayProjectileImpact(result), Is.False);
            CollectionAssert.AreEquivalent(before.NextSequences, random.CaptureState().NextSequences);
        }

        [Test]
        public void SourceDeath_ReleasesAuraButPreservesFrozenTimedDamageAndAttribution()
        {
            CombatActor enemy = Enemy(1);
            StatusTargetId recipient = _statuses.GetTarget(enemy);
            var source = new StatusSource(StatusSourceKind.Skill, "source", _target);
            _statuses.ApplyStatus(recipient, StatusMutation.Apply(new StatusRules("aura", lifetime: StatusLifetime.SourceOwned), source));
            _statuses.ApplyStatus(recipient, StatusMutation.Apply(new StatusRules("dot", duration: 5, interval: 1), source,
                new StatusEffectSnapshot(periodicDamage: new[] { new DamagePacket(DamageType.Physical, 60, TagSet.Empty) })));
            _actor.ReceiveDamage(Damage(100));
            _statuses.Unbind(_actor);
            Assert.That(_statuses.GetStatusSnapshot(recipient).HasStatus("aura"), Is.False);
            int deaths = 0;
            _architecture.RegisterEvent<ActorDiedEvent>(e => { deaths++; Assert.That(e.Source.ActorKey, Is.EqualTo(_target.ActorKey)); });
            _statuses.Advance(5);
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(_statuses.GetStatusSnapshot(recipient).IsRegistered, Is.False);
        }

        [TestCase(StatusRepeatMode.Uniform)]
        [TestCase(StatusRepeatMode.Independent)]
        public void PeriodicLayers_DoNotMultiplyEachTickByStackCount(StatusRepeatMode mode)
        {
            CombatActor enemy = Enemy(1);
            _statuses.ApplyStatus(_statuses.GetTarget(enemy), StatusMutation.Apply(new StatusRules("dot", mode, 3, 2, 1),
                new StatusSource(StatusSourceKind.Skill, "dot", _target),
                new StatusEffectSnapshot(periodicDamage: new[] { new DamagePacket(DamageType.Physical, 10, TagSet.Empty) }), 3));
            _statuses.Advance(1);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(70));
        }

        [Test]
        public void UnbindAndRebind_InvalidateOldRequestsAndPreserveEquipment()
        {
            _actor.SetModifierSource("equipment", new[] { Mod(20) });
            Apply(new StatusRules("control"), new StatusEffectSnapshot(modifiers: new[] { Mod(50) }, blockedActions: StatusActionBlock.Cast));
            _actor.gameObject.SetActive(false);
            _statuses.Unbind(_actor);
            Assert.That(_actor.MaxHealth, Is.EqualTo(120));
            _actor.gameObject.SetActive(true);
            StatusTargetId rebound = _statuses.Bind(_actor, PlayerId.LocalPlayer);
            Assert.That(rebound, Is.Not.EqualTo(_target));
            Assert.That(_statuses.ApplyStatus(_target, StatusMutation.Apply(new StatusRules("old"), _source)).Result.Code,
                Is.EqualTo(StatusResultCode.InvalidTarget));
            Assert.That(Actions().CanCast, Is.True);
        }

        [Test]
        public void SpellGate_LeavesManaRandomAndAttackEventsUntouched()
        {
            Apply(new StatusRules("control"), new StatusEffectSnapshot(blockedActions: StatusActionBlock.Move | StatusActionBlock.Attack | StatusActionBlock.Cast));
            Assert.That(Actions().CanUseItem, Is.True);
            var skill = ScriptableObject.CreateInstance<ProjectileSkillDefinition>();
            _objects.Add(skill);
            typeof(ProjectileSkillDefinition).GetField("_baseDamages", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(skill, new List<DamageRollDefinition> { new DamageRollDefinition() });
            GameplayRandomSystem random = _architecture.GetSystem<GameplayRandomSystem>();
            random.Configure(true, 123);
            GameplayRandomState before = random.CaptureState();
            int attacks = 0;
            _architecture.RegisterEvent<ActorAttackedEvent>(_ => attacks++);
            SkillCastResult result = _architecture.SendCommand(new FireProjectileCommand(_actor, skill, Vector3.zero, Vector2.right));
            Assert.That(result.Status, Is.EqualTo(SkillCastStatus.ActionBlocked));
            Assert.That(_actor.CurrentMana, Is.EqualTo(100));
            Assert.That(attacks, Is.Zero);
            CollectionAssert.AreEquivalent(before.NextSequences, random.CaptureState().NextSequences);
        }

        CombatActor Actor(ActorTeam team)
        {
            var gameObject = new GameObject("status_actor");
            _objects.Add(gameObject);
            CombatActor actor = gameObject.AddComponent<CombatActor>();
            var stats = new StatBlock();
            stats.SetValue(StatIds.Mana, 100);
            actor.Configure("same_base", team, 100, stats, TagSet.Empty);
            return actor;
        }

        [Test]
        public void AttributeTagsAndOrigins_AreResolvedFromHeldStatuses()
        {
            var tags = new TagSet(new[] { "marked" });
            var conditional = new ModifierInstance(StatIds.MaxHealth, ModifierOperation.More, ModifierScope.GlobalActor,
                10, default, default, new TagQuery(CombatTagScope.SourceActor, tags, TagSet.Empty, TagSet.Empty));
            Apply(new StatusRules("condition", tags: tags), new StatusEffectSnapshot(modifiers: new[] { conditional }));
            Assert.That(_actor.MaxHealth, Is.EqualTo(110).Within(.001));
            var details = new AttributeDetailsSnapshot(_actor, null, default);
            StatCalculationStep step = details.Attributes.Single(attribute => attribute.Id == StatIds.MaxHealth).Steps
                .Single(candidate => candidate.Origin.Kind == ModifierOriginKind.Status);
            Assert.That(step.Origin.Kind, Is.EqualTo(ModifierOriginKind.Status));
            Assert.That(step.Origin.StatusId, Is.EqualTo("condition"));
            Assert.Throws<ArgumentException>(() => new StatusEffectSnapshot(modifiers: new[] {
                new ModifierInstance(StatIds.MaxHealth, ModifierOperation.Flat, ModifierScope.GlobalActor, 10, default, default,
                    new TagQuery(CombatTagScope.Skill, tags, TagSet.Empty, TagSet.Empty)) }));
        }

        [Test]
        public void MissingDamageContextAndFriendlyPeriodicDamage_AreRejected()
        {
            var effects = new StatusEffectSnapshot(periodicDamage: new[] { new DamagePacket(DamageType.Fire, 10, TagSet.Empty) });
            Assert.That(_statuses.ApplyStatus(_target, StatusMutation.Apply(new StatusRules("dot", interval: 1), _source, effects)).Result.Code,
                Is.EqualTo(StatusResultCode.InvalidRequest));
            DamageResult result = _architecture.GetSystem<CombatSystem>().ApplyPeriodicDamage(
                new DamageSourceSnapshot("friendly", ActorTeam.Player), _actor, effects.PeriodicDamage);
            Assert.That(result.Form, Is.EqualTo(DamageForm.Periodic));
            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.InvalidTarget));
            Assert.That(_actor.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void SourceCleanup_OperatesDuringPendingClockAndCancelsPreparedTarget()
        {
            CombatActor enemy = Enemy(1);
            StatusTargetId recipient = _statuses.GetTarget(enemy);
            _statuses.ApplyStatus(recipient, StatusMutation.Apply(new StatusRules("aura", interval: 1, lifetime: StatusLifetime.SourceOwned),
                new StatusSource(StatusSourceKind.Skill, "aura", _target), new StatusEffectSnapshot(modifiers: new[] { Mod(20) })));
            Assert.That(_statuses.Advance(5, 1).HasPending, Is.True);
            _statuses.Unbind(_actor);
            Assert.That(enemy.MaxHealth, Is.EqualTo(100));
            Assert.That(_statuses.GetStatusSnapshot(recipient).Layers, Is.Empty);
            _statuses.Advance(0);
            using (StatusCommit commit = _statuses.Store.Commit(_statuses.Store.Prepare(recipient,
                new[] { StatusMutation.Apply(new StatusRules("prepared"), _source) })))
            {
                _statuses.Unbind(enemy);
                Assert.That(commit.Publish(), Is.False);
                Assert.That(_statuses.GetStatusSnapshot(recipient).IsRegistered, Is.False);
            }
        }

        [Test]
        public void FrozenProjectile_DoesNotRecheckSourceActionOrLife()
        {
            CombatActor enemy = Enemy(1);
            var stats = new StatBlock();
            stats.SetValue(StatIds.Accuracy, 10000);
            _actor.Configure("source", ActorTeam.Player, 100, stats, TagSet.Empty);
            AttackSnapshot attack = AttackSnapshotFactory.CreateImmediate(_actor, "test", "",
                new[] { new DamagePacket(DamageType.Fire, 10, TagSet.Empty) }, TagSet.Empty, 123);
            Apply(new StatusRules("control"), new StatusEffectSnapshot(blockedActions: StatusActionBlock.Attack | StatusActionBlock.Cast));
            _actor.ReceiveDamage(Damage(100));
            _statuses.Unbind(_actor);
            DamageResult result = _architecture.GetSystem<CombatSystem>().ApplyDamage(attack, enemy);
            Assert.That(result.DidDealDamage, Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void PreparedAura_RejectsProviderLossBeforePublication()
        {
            CombatActor enemy = Enemy(1);
            StatusTargetId recipient = _statuses.GetTarget(enemy);
            StatusMutation aura = StatusMutation.Apply(new StatusRules("aura", lifetime: StatusLifetime.SourceOwned),
                new StatusSource(StatusSourceKind.Skill, "aura", _target), new StatusEffectSnapshot(modifiers: new[] { Mod(20) }));
            using (StatusCommit commit = _statuses.Store.Commit(_statuses.Store.Prepare(recipient, new[] { aura })))
            {
                _statuses.Unbind(_actor);
                Assert.That(commit.Publish(), Is.False);
                Assert.That(commit.Result.Code, Is.EqualTo(StatusResultCode.StaleVersion));
            }
            Assert.That(_statuses.GetStatusSnapshot(recipient).Layers, Is.Empty);
            Assert.That(enemy.MaxHealth, Is.EqualTo(100));
        }

        CombatActor Enemy(int id)
        {
            CombatActor actor = Actor(ActorTeam.Monster);
            _statuses.Bind(actor, MonsterInstanceId.Parse("00000000000000000000000000000001:monster:" + id));
            return actor;
        }

        [TestCase("weakness")]
        [TestCase("stun")]
        [TestCase("bleeding")]
        [TestCase("burning")]
        [TestCase("chill")]
        [TestCase("shock")]
        [TestCase("poison")]
        public void AilmentResistance_ScalesOnlyItsEffectAndFreezes(string id)
        {
            StatusDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusDefinition>("Assets/Data/Preset/Statuses/" + id + ".asset");
            Assert.That(definition.ValidateConfiguration(), Is.Empty);
            StatusApplication application = StatusApplication.Capture(definition, _source, new StatBlock(), null,
                new DamageSourceSnapshot("enemy", ActorTeam.Monster), 123);
            _actor.SetModifierSource("resistance", new[] { Mod(50, stat: id + "_resistance") });
            StatusResult result = _statuses.ApplyStatus(_target, application.CreateMutation()).Result;
            Assert.That(result.Code, Is.EqualTo(StatusResultCode.Success));
            StatusLayerSnapshot layer = result.Snapshot.Layers.Single();
            Assert.That(layer.Effects.Resistance.EffectiveResistance, Is.EqualTo(50));
            Assert.That(layer.RemainingSeconds, Is.EqualTo(definition.CreateRules().Duration * (id == "stun" ? .5 : 1)));
            if (layer.Effects.Modifiers.Count > 0)
            {
                Assert.That(layer.Effects.Modifiers[0].Value, Is.EqualTo(application.Effects.Modifiers[0].Value * .5));
            }
            if (layer.Effects.PeriodicDamage.Count > 0)
            {
                Assert.That(layer.Effects.PeriodicDamage[0].Amount, Is.EqualTo(application.Effects.PeriodicDamage[0].Amount * .5));
            }
            _actor.SetModifierSource("resistance", new[] { Mod(100, stat: id + "_resistance") });
            Assert.That(_statuses.ApplyStatus(_target, application.CreateMutation()).Result.Code, Is.EqualTo(StatusResultCode.Resisted));
            StatusLayerSnapshot unchanged = _statuses.GetStatusSnapshot(_target).Layers.Single();
            Assert.That(unchanged.InstanceId, Is.EqualTo(layer.InstanceId));
            Assert.That(unchanged.ExpiresAt, Is.EqualTo(layer.ExpiresAt));
            Assert.That(unchanged.Effects, Is.SameAs(layer.Effects));
            _statuses.DispelStatuses(_target, new StatusFilter(category: StatusCategory.Ailment));
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers, Is.Empty);
        }

        [Test]
        public void ResistedBatch_DoesNotConsumeMarkerOrPublish()
        {
            Apply(new StatusRules("charge", maxStacks: 3), StatusEffectSnapshot.Marker, 2);
            _actor.SetModifierSource("resistance", new[] { Mod(100, stat: StatIds.StunResistance) });
            StatusDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusDefinition>("Assets/Data/Preset/Statuses/stun.asset");
            StatusApplication effect = StatusApplication.Capture(definition, _source, new StatBlock(), null,
                new DamageSourceSnapshot("enemy", ActorTeam.Monster), 0);
            long version = _statuses.GetStatusSnapshot(_target).Version;
            var command = new ConsumeStatusForEffectCommand(_target, "charge", 1, effect, version);
            _architecture.SendCommand(command);
            Assert.That(command.Operation.Result.Code, Is.EqualTo(StatusResultCode.Resisted));
            Assert.That(_statuses.GetStatusSnapshot(_target).GetStacks(), Is.EqualTo(2));
            Assert.That(_statuses.GetStatusSnapshot(_target).Version, Is.EqualTo(version));
        }

        [TestCase(-20f, 0f, StatusResultCode.Success)]
        [TestCase(0f, 0f, StatusResultCode.Success)]
        [TestCase(150f, 100f, StatusResultCode.Resisted)]
        public void AilmentResistance_ClampsAtApplicationBoundary(float raw, float effective, StatusResultCode code)
        {
            _actor.SetModifierSource("resistance", new[] { Mod(raw, stat: StatIds.ChillResistance) });
            StatusDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusDefinition>("Assets/Data/Preset/Statuses/chill.asset");
            StatusResult result = _statuses.ApplyStatus(_target,
                StatusMutation.Apply(definition.CreateRules(), _source, definition.CreateEffects(new System.Random(0)))).Result;
            Assert.That(result.Code, Is.EqualTo(code));
            Assert.That(CombatStatValues.Effective(_actor.Stats, StatIds.ChillResistance), Is.EqualTo(effective));
            if (result.Succeeded) { Assert.That(result.Snapshot.Layers.Single().Effects.Resistance.EffectiveResistance, Is.EqualTo(effective)); }
            else { Assert.That(result.Snapshot.Layers, Is.Empty); }
        }

        [Test]
        public void ConsumeMarker_GrantsEffectAtomicallyAndDoesNotRepeatAtExpiry()
        {
            Apply(new StatusRules("charge", maxStacks: 3), StatusEffectSnapshot.Marker, 2);
            var effect = new StatusApplication(new StatusRules("empowered", duration: 2),
                new StatusEffectSnapshot(modifiers: new[] { Mod(20) }), new StatusSource(StatusSourceKind.Talent, "node"));
            long version = _statuses.GetStatusSnapshot(_target).Version;
            var command = new ConsumeStatusForEffectCommand(_target, "charge", 2, effect, version);
            _architecture.SendCommand(command);
            Assert.That(command.Operation.Result.Code, Is.EqualTo(StatusResultCode.Success));
            Assert.That(command.Operation.Result.Changes.Count(change => change.Reason == StatusChangeReason.Consumed), Is.EqualTo(2));
            Assert.That(_actor.MaxHealth, Is.EqualTo(120));
            _statuses.Advance(2);
            Assert.That(_actor.MaxHealth, Is.EqualTo(100));
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers, Is.Empty);
            _architecture.SendCommand(new ConsumeStatusForEffectCommand(_target, "charge", 2, effect, version));
            Assert.That(_actor.MaxHealth, Is.EqualTo(100));
        }

        [TestCase(StatusSourceKind.Skill)]
        [TestCase(StatusSourceKind.Equipment)]
        [TestCase(StatusSourceKind.Talent)]
        [TestCase(StatusSourceKind.Consumable)]
        [TestCase(StatusSourceKind.Mechanism)]
        public void SharedAilmentEntry_ReapplyingSnapshotUsesOriginalMagnitudeAndNewResistance(StatusSourceKind kind)
        {
            StatusDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusDefinition>("Assets/Data/Preset/Statuses/chill.asset");
            var source = new StatusSource(kind, "provider");
            _actor.SetModifierSource("resistance", new[] { Mod(50, stat: StatIds.ChillResistance) });
            StatusEffectSnapshot original = _statuses.ApplyStatus(_target, StatusMutation.Apply(definition.CreateRules(), source,
                definition.CreateEffects(new System.Random(0)))).Result.Snapshot.Layers.Single().Effects;
            _actor.SetModifierSource("resistance", new[] { Mod(75, stat: StatIds.ChillResistance) });
            StatusResult applied = _statuses.ApplyStatus(_target, StatusMutation.Apply(definition.CreateRules(), source, original)).Result;
            Assert.That(applied.Code, Is.EqualTo(StatusResultCode.Success));
            Assert.That(applied.Snapshot.Layers.Last().Effects.Modifiers.Single().Value, Is.EqualTo(-5));
            Assert.That(applied.Snapshot.Layers.First().Effects.Modifiers.Single().Value, Is.EqualTo(-10));
            Assert.That(applied.Snapshot.Layers.First().IsActive, Is.True);
            _statuses.ReleaseStatusSource(_target, source);
            Assert.That(_statuses.GetStatusSnapshot(_target).GetStacks(), Is.EqualTo(2), "限时状态不依赖来源继续存在");
            _statuses.TryConsumeStatusStacks(_target, "chill", 1);
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers.Single().IsActive, Is.True);
            _statuses.DispelStatuses(_target, new StatusFilter(category: StatusCategory.Ailment));
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers, Is.Empty);
        }

        [Test]
        public void ProjectileAilment_UsesFrozenSourceAndCurrentTargetResistanceAfterSourceLeaves()
        {
            CombatActor enemy = Enemy(7);
            _actor.SetModifierSource("accuracy", new[] { Mod(100000, stat: StatIds.Accuracy) });
            ItemBaseDefinition weapon = UnityEditor.AssetDatabase.FindAssets("t:ItemBaseDefinition", new[] { "Assets/Data/Preset" })
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<ItemBaseDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .First(item => item.CanEquipTo(EquipmentSlot.Weapon));
            Assert.That(_architecture.GetSystem<EquipmentSystem>().GrantAndEquipStartingWeapon(_actor, weapon), Is.True);
            ProjectileSkillDefinition skill = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectileSkillDefinition>("Assets/Data/Preset/Skills/status_burning_example.asset");
            AttackSnapshot attack = AttackSnapshotFactory.CreateProjectile(_actor, skill, _architecture.GetModel<EquipmentModel>(), 123);
            Assert.That(attack.OnHitStatus, Is.Not.Null);
            enemy.SetModifierSource("resistance", new[] { Mod(50, stat: StatIds.BurningResistance) });
            _statuses.Unbind(_actor);
            DamageResult result = _architecture.GetSystem<CombatSystem>().ApplyDamage(attack, enemy);
            Assert.That(result.IsHit, Is.True);
            StatusLayerSnapshot layer = _statuses.GetStatusSnapshot(_statuses.GetTarget(enemy)).Layers.Single();
            Assert.That(layer.Effects.Resistance.EffectiveResistance, Is.EqualTo(50));
            float health = enemy.CurrentHealth;
            _statuses.Advance(1);
            Assert.That(enemy.CurrentHealth, Is.LessThan(health));
            Assert.That(_statuses.GetStatusSnapshot(_statuses.GetTarget(enemy)).GetStacks(), Is.EqualTo(1), "周期不递归施加");
            var dispel = new UseStatusSkillCommand(enemy, enemy, dispel: true);
            _architecture.SendCommand(dispel);
            Assert.That(dispel.Operation.Result.Succeeded, Is.True);
            Assert.That(_statuses.GetStatusSnapshot(_statuses.GetTarget(enemy)).Layers, Is.Empty);
        }

        [Test]
        public void EquipmentStatus_FailedReplacementPreservesLoadoutInventoryAndLayers()
        {
            ItemBaseDefinition original = UnityEditor.AssetDatabase.FindAssets("t:ItemBaseDefinition", new[] { "Assets/Data/Preset" })
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<ItemBaseDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .First(item => item.CanEquipTo(EquipmentSlot.Weapon));
            ItemBaseDefinition definition = Object.Instantiate(original);
            _objects.Add(definition);
            StatusDefinition aura = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusDefinition>("Assets/Data/Preset/Statuses/equipment_guard.asset");
            typeof(ItemBaseDefinition).GetField("_providedStatus", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, aura);
            ItemInstance item = definition.CreateInstance("00000000000000000000000000000001:item:999", 1, 0);
            InventoryModel inventory = _architecture.GetModel<InventoryModel>();
            Assert.That(inventory.TryAddItemWithoutEvents(item), Is.True);
            EquipmentSystem equipment = _architecture.GetSystem<EquipmentSystem>();
            Assert.That(equipment.Equip(_actor, item, EquipmentSlot.Weapon), Is.True);
            Assert.That(_statuses.GetStatusSnapshot(_target).GetStacks(new StatusFilter("equipment_guard")), Is.EqualTo(1));
            EquipmentLoadout loadout = _architecture.GetModel<EquipmentModel>().GetLoadout(_actor);
            equipment.RestoreLoadout(_actor, loadout);
            Assert.That(_statuses.GetStatusSnapshot(_target).GetStacks(new StatusFilter("equipment_guard")), Is.EqualTo(1));
            StatusDefinition invalid = Object.Instantiate(aura);
            _objects.Add(invalid);
            typeof(StatusDefinition).GetField("_lifetime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(invalid, StatusLifetime.Timed);
            typeof(ItemBaseDefinition).GetField("_providedStatus", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, invalid);
            ItemInstance replacement = definition.CreateInstance("00000000000000000000000000000001:item:998", 1, 0);
            var originalOrigin = new Vector2Int(5, 0);
            Assert.That(inventory.TryAddItemAtWithoutEvents(replacement, originalOrigin), Is.True);
            Assert.That(equipment.Equip(_actor, replacement, EquipmentSlot.Weapon), Is.False);
            Assert.That(loadout.Get(EquipmentSlot.Weapon), Is.SameAs(item));
            Assert.That(inventory.Grid.Placements.ContainsKey(replacement), Is.True);
            Assert.That(inventory.Grid.Placements[replacement].position, Is.EqualTo(originalOrigin));
            Assert.That(inventory.Grid.Placements.ContainsKey(item), Is.False);
            Assert.That(_statuses.GetStatusSnapshot(_target).GetStacks(new StatusFilter("equipment_guard")), Is.EqualTo(1));
            Assert.That(equipment.Unequip(_actor, EquipmentSlot.Weapon), Is.True);
            Assert.That(_statuses.GetStatusSnapshot(_target).Layers, Is.Empty);
        }

        void Apply(StatusRules rules, StatusEffectSnapshot effects, int count = 1)
        {
            Assert.That(_statuses.ApplyStatus(_target, StatusMutation.Apply(rules, _source, effects, count)).Result.Succeeded, Is.True);
        }

        ActorActionPermission Actions() { return _architecture.SendQuery(new GetActorActionsQuery(_actor)); }
        static ModifierInstance Mod(float value, ModifierOperation operation = ModifierOperation.Flat, string stat = StatIds.MaxHealth)
        {
            return new ModifierInstance(stat, operation, ModifierScope.GlobalActor, value, default, default, TagQuery.Empty);
        }
        static DamageResult Damage(float value)
        {
            return new DamageResult(true, false, new Dictionary<DamageType, float> { [DamageType.Physical] = value },
                new Dictionary<DamageType, float> { [DamageType.Physical] = value });
        }
    }
}
