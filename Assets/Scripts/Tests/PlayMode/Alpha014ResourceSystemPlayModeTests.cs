using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed partial class Alpha014ResourceSystemPlayModeTests
    {
        const float SetupTimeoutSeconds = 20f;

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
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            HudController hud = Object.FindAnyObjectByType<HudController>();
            ApplicationHost host = ApplicationHost.Current;
            IArchitecture architecture = host.CurrentSession.Architecture;
            CombatActor actor = player.Actor;
            CombatSystem combat = architecture.GetSystem<CombatSystem>();
            ResourceRegenerationSystem regeneration = architecture.GetSystem<ResourceRegenerationSystem>();
            GameplayPauseSystem pause = architecture.GetSystem<GameplayPauseSystem>();
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            ItemInstance wood = Create("firewood_large");
            ItemInstance flame = Create("flame_projectile");
            Assert.IsTrue(inventory.TryAddItem(wood)); Assert.IsTrue(inventory.TryAddItem(flame));
            Board(new SkillBoardRequest(SkillBoardOperation.Install, wood, SkillBoardGeometry.Roots[0]));
            Board(new SkillBoardRequest(SkillBoardOperation.Embed, flame, slot: new FirewoodPortReference(wood.InstanceId, "center")));
            PlayerGrowthSystem growth = architecture.GetSystem<PlayerGrowthSystem>();
            growth.Restore(player.Id.Value, new PlayerGrowthState().Charge(1000), actor);
            Assert.That(growth.Commit(growth.Preview(player.Id.Value, null, 1, wood.InstanceId, ""), out _), Is.True);
            Assert.That(architecture.SendCommand(new AssignPlayerSkillCommand(player.Id.Value, 0, flame.InstanceId)), Is.True);
            ProjectileSkillDefinition skill = flame.SkillBoardState.FlameSkill;
            Assert.Greater(skill.ManaCost, 0);
            float fullMana = actor.CurrentMana;
            pause.SetPaused(true);
            Assert.That(Cast().Status, Is.EqualTo(SkillCastStatus.ActionBlocked));
            Assert.That(actor.CurrentMana, Is.EqualTo(fullMana));
            pause.SetPaused(false);
            SkillCastResult first = Cast();
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(actor.CurrentMana, Is.EqualTo(fullMana - skill.ManaCost).Within(.001f));
            Assert.That(hud.LastSnapshot.CurrentMana, Is.EqualTo(actor.CurrentMana));
            Object.Destroy(first.Projectile.gameObject);
            pause.SetPaused(true);
            float remaining = flame.SkillBoardState.CooldownRemainingSeconds;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(flame.SkillBoardState.CooldownRemainingSeconds, Is.EqualTo(remaining).Within(.001f));
            float pausedMana = actor.CurrentMana;
            regeneration.AdvanceRegeneration(2f);
            Assert.That(actor.CurrentMana, Is.EqualTo(pausedMana));
            pause.SetPaused(false);
            yield return new WaitForSeconds(skill.Cooldown + .1f);
            Assert.IsTrue(combat.TrySpendMana(actor, actor.CurrentMana - 1));
            SkillCastResult insufficient = Cast();
            Assert.That(insufficient.Status, Is.EqualTo(SkillCastStatus.InsufficientMana));
            Assert.That(actor.CurrentMana, Is.EqualTo(1).Within(.001f));
            Assert.That(architecture.SendQuery(new GetPlayerSkillRuntimeQuery(0)).SkillState, Is.EqualTo("no_mana"));
            Label slotState = hud.GetComponent<RuntimePanelView>().Root.Q<Label>("hud-flame-state-0");
            StringAssert.Contains(host.Localization.GetString("ui", "attributes.state.no_mana"), slotState.text);
            regeneration.AdvanceRegeneration((skill.ManaCost - 1) / (actor.MaxMana * .05f));
            Assert.That(architecture.SendQuery(new GetPlayerSkillRuntimeQuery(0)).SkillState, Is.EqualTo("ready"));
            SkillCastResult recovered = Cast();
            Assert.That(recovered.IsSuccess, Is.True);
            Assert.That(actor.CurrentMana, Is.EqualTo(0).Within(.001f));
            Object.Destroy(recovered.Projectile.gameObject);
            actor.ReceiveDamage(CreateDamageResult(10));
            float damaged = actor.CurrentHealth;
            regeneration.AdvanceRegeneration(2f);
            Assert.That(actor.CurrentHealth, Is.EqualTo(damaged + 2).Within(.001f));
            combat.Revive(actor, actor.transform.position);
            Assert.That(actor.CurrentMana, Is.EqualTo(actor.MaxMana));

            ItemInstance Create(string id)
            {
                return host.ContentCatalog.GetAll<ItemBaseDefinition>().Single(d => d.Id == id)
                    .CreateInstance(architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 42, ItemRarity.Normal);
            }
            void Board(SkillBoardRequest request)
            {
                SkillBoardPreview preview = architecture.SendQuery(new PreviewSkillBoardQuery(player.Id.Value, request));
                Assert.That(preview.Success, Is.True, preview.Error);
                Assert.That(architecture.SendCommand(new CommitSkillBoardCommand(preview)), Is.True);
            }
            SkillCastResult Cast()
            {
                return architecture.SendCommand(new CastPlayerSkillCommand(actor, 0, flame.InstanceId, actor.transform.position, Vector2.right));
            }
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
