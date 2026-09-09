using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class Alpha014ResourceSystemPlayModeTests
    {
        const float SetupTimeoutSeconds = 20f;

        static readonly FieldInfo PlayerSkillField = typeof(CombatPrototypeBootstrap).GetField(
            "_playerSkill",
            BindingFlags.Instance | BindingFlags.NonPublic);

        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameArchitectureProvider.TryGetCurrent(out IArchitecture architecture))
            {
                GameplayPauseSystem pauseSystem = architecture.GetSystem<GameplayPauseSystem>();

                if (pauseSystem.IsPaused)
                {
                    pauseSystem.SetPaused(false);
                }
            }

            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator MainScene_CastsAtomicallyRegeneratesAndUpdatesManaHud()
        {
            yield return _fixture.EnterMain();
            PlayerController player = null;
            MonsterSpawner spawner = null;
            CombatPrototypeBootstrap bootstrap = null;
            HudController hud = null;
            float timeout = Time.realtimeSinceStartup + SetupTimeoutSeconds;

            while ((player == null
                    || spawner == null
                    || bootstrap == null
                    || hud == null
                    || !spawner.gameObject.activeInHierarchy)
                   && Time.realtimeSinceStartup < timeout)
            {
                player = Object.FindAnyObjectByType<PlayerController>();
                spawner = Object.FindAnyObjectByType<MonsterSpawner>();
                bootstrap = Object.FindAnyObjectByType<CombatPrototypeBootstrap>();
                hud = Object.FindAnyObjectByType<HudController>();
                yield return null;
            }

            Assert.IsNotNull(player, "Main 场景未生成玩家");
            Assert.IsNotNull(spawner, "Main 场景未启用刷怪器");
            Assert.IsNotNull(bootstrap, "Main 场景缺少启动器");
            Assert.IsNotNull(hud, "Main 场景缺少 HUD");
            Assert.IsNotNull(PlayerSkillField, "未找到玩家技能字段");
            ProjectileSkillDefinition skill = PlayerSkillField.GetValue(bootstrap) as ProjectileSkillDefinition;
            Assert.IsNotNull(skill);
            Assert.AreEqual(8f, skill.ManaCost, 0.001f);

            IArchitecture architecture = GameArchitectureProvider.RequireCurrent();
            CombatActor actor = player.Actor;
            CombatSystem combat = architecture.GetSystem<CombatSystem>();
            ResourceRegenerationSystem regeneration = architecture.GetSystem<ResourceRegenerationSystem>();
            GameplayPauseSystem pause = architecture.GetSystem<GameplayPauseSystem>();
            player.enabled = false;
            architecture.GetSystem<PlayerSkillStateRegistry>().Register(actor, () => new PlayerSkillState(skill, 0f));
            spawner.enabled = false;
            pause.SetPaused(true);
            Assert.AreEqual(200f, actor.CurrentMana, 0.001f);

            SkillCastResult firstCast = architecture.SendCommand(new FireProjectileCommand(
                actor,
                skill,
                actor.transform.position,
                Vector2.right));

            Assert.IsTrue(firstCast.IsSuccess);
            Assert.IsNotNull(firstCast.Projectile);
            Assert.AreEqual(192f, actor.CurrentMana, 0.001f);
            Assert.AreEqual(192f, hud.LastSnapshot.CurrentMana, 0.001f);
            Object.Destroy(firstCast.Projectile.gameObject);
            UIDocument document = hud.GetComponent<UIDocument>();
            ProgressBar manaBar = document.rootVisualElement.Q<ProgressBar>("mana-bar");
            Label skillStatus = document.rootVisualElement.Q<Label>("skill-status-label");
            Assert.IsNotNull(manaBar);
            Assert.IsNotNull(skillStatus);
            Assert.AreEqual("192 / 200", manaBar.title);

            Assert.IsTrue(combat.TrySpendMana(actor, 191f));
            SkillCastResult rejectedCast = architecture.SendCommand(new FireProjectileCommand(
                actor,
                skill,
                actor.transform.position,
                Vector2.right));
            Assert.AreEqual(SkillCastStatus.InsufficientMana, rejectedCast.Status);
            Assert.AreEqual(1f, actor.CurrentMana, 0.001f);
            Assert.AreEqual(DisplayStyle.Flex, skillStatus.style.display.value);
            Assert.IsTrue(ApplicationHost.TryGetCurrent(out ApplicationHost host));
            Assert.AreEqual(
                host.Localization.GetString(
                    "ui",
                    "attributes.state.no_mana"),
                skillStatus.text);

            regeneration.AdvanceRegeneration(2f);
            Assert.AreEqual(1f, actor.CurrentMana, 0.001f, "暂停期间不应恢复法力");
            pause.SetPaused(false);
            regeneration.AdvanceRegeneration(0.7f);
            Assert.AreEqual(8f, actor.CurrentMana, 0.001f);
            Assert.AreEqual("ready", hud.RuntimeSnapshot.SkillState, "恢复至可施放时应立即清除不足状态，无需先攻击");

            SkillCastResult recoveredCast = architecture.SendCommand(new FireProjectileCommand(
                actor,
                skill,
                actor.transform.position,
                Vector2.right));
            Assert.IsTrue(recoveredCast.IsSuccess);
            Assert.AreEqual(0f, actor.CurrentMana, 0.001f);
            Assert.AreEqual("no_mana", hud.RuntimeSnapshot.SkillState, "成功施放后按当前资源显示状态");
            Object.Destroy(recoveredCast.Projectile.gameObject);

            actor.ReceiveDamage(CreateDamageResult(10f));
            float damagedHealth = actor.CurrentHealth;
            regeneration.AdvanceRegeneration(2f);
            Assert.AreEqual(damagedHealth + 2f, actor.CurrentHealth, 0.001f);

            combat.Revive(actor, actor.transform.position);
            Assert.AreEqual(actor.MaxHealth, actor.CurrentHealth, 0.001f);
            Assert.AreEqual(actor.MaxMana, actor.CurrentMana, 0.001f);
        }

        static DamageResult CreateDamageResult(float amount)
        {
            System.Collections.Generic.Dictionary<DamageType, float> damage =
                new System.Collections.Generic.Dictionary<DamageType, float>
                {
                    { DamageType.Physical, amount },
                };
            return new DamageResult(true, false, damage, damage);
        }
    }
}
