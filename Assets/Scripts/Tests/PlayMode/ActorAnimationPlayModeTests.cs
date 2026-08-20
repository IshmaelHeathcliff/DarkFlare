using System.Collections;
using System.Collections.Generic;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public class ActorAnimationPlayModeTests
    {
        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator MainScene_ActorEventsSwitchAttackHitDeathAndRevive()
        {
            yield return _fixture.EnterMain();

            PlayerController player = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while (player == null && Time.realtimeSinceStartup < timeout)
            {
                player = Object.FindAnyObjectByType<PlayerController>();
                yield return null;
            }

            Assert.IsNotNull(player, "Main 场景未在 15 秒内生成玩家");
            player.enabled = false;

            MonsterController[] monsters = Object.FindObjectsByType<MonsterController>();

            for (int i = 0; i < monsters.Length; i++)
            {
                monsters[i].enabled = false;
            }

            CombatActor actor = player.Actor;
            Animator animator = player.GetComponent<Animator>();
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
            Collider2D collider = player.GetComponent<Collider2D>();
            IArchitecture architecture = GameArchitectureProvider.RequireCurrent();

            Assert.IsNotNull(animator);
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(collider);
            AssertState(animator, "Idle");

            architecture.SendEvent(new ActorAttackedEvent { Actor = actor });
            yield return new WaitForSeconds(0.08f);
            AssertState(animator, "Attack");

            DamageResult hit = CreateDamageResult(5f);
            architecture.SendEvent(new ActorDamagedEvent { Actor = actor, Result = hit });
            yield return new WaitForSeconds(0.08f);
            AssertState(animator, "Hit");

            DamageResult lethalDamage = CreateDamageResult(actor.MaxHealth + 1f);
            Assert.IsTrue(actor.ReceiveDamage(lethalDamage));
            architecture.SendEvent(new ActorDiedEvent { Actor = actor });
            yield return new WaitForSeconds(0.08f);

            AssertState(animator, "Death");
            Assert.IsTrue(renderer.enabled, "死亡动画期间必须保留 SpriteRenderer");
            Assert.IsFalse(collider.enabled, "死亡后必须关闭碰撞，避免继续阻挡或受击");

            actor.Revive(Vector3.zero);
            architecture.SendEvent(new ActorRevivedEvent { Actor = actor });
            yield return null;

            AssertState(animator, "Idle");
            Assert.IsTrue(renderer.enabled);
            Assert.IsTrue(collider.enabled);
            Debug.Log("[Phase0AnimationPlayMode] Attack、Hit、Death、Revive 状态切换通过");
        }

        static DamageResult CreateDamageResult(float damage)
        {
            Dictionary<DamageType, float> values = new Dictionary<DamageType, float>
            {
                { DamageType.Physical, damage },
            };
            return new DamageResult(
                true,
                false,
                new Dictionary<DamageType, float>(values),
                new Dictionary<DamageType, float>(values));
        }

        static void AssertState(Animator animator, string stateName)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            bool matchesCurrent = IsState(current, stateName);
            bool matchesNext = animator.IsInTransition(0) && IsState(next, stateName);
            Assert.IsTrue(
                matchesCurrent || matchesNext,
                $"预期动画状态 {stateName}，当前 shortNameHash={current.shortNameHash}，下一状态 shortNameHash={next.shortNameHash}");
        }

        static bool IsState(AnimatorStateInfo state, string stateName)
        {
            return state.IsName(stateName) || state.IsName($"Base Layer.{stateName}");
        }
    }
}
