using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class StatusCombatPlayModeTests
    {
        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        [UnitySetUp]
        public IEnumerator SetUp() { yield return _fixture.Restart(); }

        [UnityTearDown]
        public IEnumerator TearDown() { yield return _fixture.Restart(); }

        [UnityTest]
        public IEnumerator ConfiguredActors_BlockMovementAndRebindAfterDisableAndDeath()
        {
            yield return _fixture.EnterMain();
            PlayerController player = null;
            MonsterController monster = null;
            float timeout = Time.realtimeSinceStartup + 20;
            while ((player == null || monster == null) && Time.realtimeSinceStartup < timeout)
            {
                player = Object.FindAnyObjectByType<PlayerController>();
                monster = Object.FindAnyObjectByType<MonsterController>();
                yield return null;
            }
            Assert.That(player, Is.Not.Null);
            Assert.That(monster, Is.Not.Null);
            foreach (MonsterSpawner spawner in Object.FindObjectsByType<MonsterSpawner>()) { spawner.enabled = false; }
            foreach (MonsterController other in Object.FindObjectsByType<MonsterController>()) { if (other != monster) { other.enabled = false; } }
            player.enabled = false;
            IArchitecture architecture = GameArchitectureProvider.RequireCurrent();
            StatusSystem statuses = architecture.GetSystem<StatusSystem>();
            CombatActor actor = monster.Actor;
            StatusTargetId original = statuses.GetTarget(actor);
            Assert.That(original.ActorKey, Is.EqualTo(monster.Instance.Id.Value));
            actor.transform.position = player.transform.position + Vector3.right * 5;
            yield return new WaitForFixedUpdate();
            Assert.That(actor.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude, Is.GreaterThan(0));
            var source = new StatusSource(StatusSourceKind.Mechanism, "playmode");
            StatusDefinition stun = ApplicationHost.Current.ContentCatalog.GetAll<StatusDefinition>().Single(definition => definition.Id == "stun");
            actor.SetModifierSource("test_resistance", new[] { new ModifierInstance(StatIds.StunResistance,
                ModifierOperation.Flat, ModifierScope.GlobalActor, 50, default, default, TagQuery.Empty) });
            Assert.That(statuses.ApplyStatus(original, StatusMutation.Apply(stun.CreateRules(), source,
                stun.CreateEffects(new System.Random(0)))).Result.Succeeded, Is.True);
            Assert.That(statuses.GetStatusSnapshot(original).Layers.Single().RemainingSeconds, Is.EqualTo(1));
            yield return new WaitForFixedUpdate();
            Assert.That(actor.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(architecture.SendQuery(new GetActorActionsQuery(actor)).CanUseItem, Is.True);
            actor.transform.position = player.transform.position;
            GameplayRandomSystem random = architecture.GetSystem<GameplayRandomSystem>();
            GameplayRandomState before = random.CaptureState();
            float cooldown = monster.ContactDamageCooldownRemainingSeconds;
            int attacks = 0;
            IUnRegister attackObserver = architecture.RegisterEvent<ActorAttackedEvent>(_ => attacks++);
            Assert.That(architecture.SendCommand(new ContactAttackCommand(monster, player.Actor)), Is.False);
            Assert.That(monster.ContactDamageCooldownRemainingSeconds, Is.EqualTo(cooldown));
            Assert.That(attacks, Is.Zero);
            CollectionAssert.AreEquivalent(before.NextSequences, random.CaptureState().NextSequences);
            attackObserver.UnRegister();
            actor.transform.position = player.transform.position + Vector3.right * 5;
            statuses.Advance(1);
            yield return new WaitForFixedUpdate();
            Assert.That(actor.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude, Is.GreaterThan(0));
            actor.SetModifierSource("test_resistance", null);
            actor.enabled = false;
            Assert.That(statuses.GetTarget(actor).IsValid, Is.False);
            Assert.That(actor.BlockedActions, Is.EqualTo(StatusActionBlock.None));
            actor.enabled = true;
            Assert.That(statuses.GetTarget(actor), Is.Not.EqualTo(original));
            Assert.That(statuses.GetStatusSnapshot(statuses.GetTarget(actor)).Layers, Is.Empty);

            CombatActor hero = player.Actor;
            StatusTargetId playerTarget = statuses.GetTarget(hero);
            statuses.ApplyStatus(playerTarget, StatusMutation.Apply(new StatusRules("control"), source,
                new StatusEffectSnapshot(blockedActions: StatusActionBlock.Cast)));
            DamageResult damage = architecture.GetSystem<CombatSystem>().ApplyPeriodicDamage(
                new DamageSourceSnapshot(original.ActorKey, ActorTeam.Monster), hero,
                new[] { new DamagePacket(DamageType.Physical, hero.MaxHealth * 2, TagSet.Empty) });
            Assert.That(damage.DidDealDamage, Is.True);
            Assert.That(statuses.GetTarget(hero).IsValid, Is.False);
            architecture.GetSystem<CombatSystem>().Revive(hero, hero.transform.position);
            Assert.That(statuses.GetTarget(hero), Is.Not.EqualTo(playerTarget));
            Assert.That(statuses.GetTarget(hero).IsValid, Is.True);
            Assert.That(architecture.SendQuery(new GetActorActionsQuery(hero)).CanCast, Is.True);
        }
    }
}
