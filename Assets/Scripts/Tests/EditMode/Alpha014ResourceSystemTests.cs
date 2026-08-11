using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class Alpha014ResourceSystemTests
    {
        readonly List<Object> _objects = new List<Object>();

        IArchitecture _architecture;
        float _originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
            GameArchitecture.Interface.Deinit();
            _architecture = GameArchitecture.Interface;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            _architecture.Deinit();
            _architecture = null;
            Time.timeScale = _originalTimeScale;
        }

        [Test]
        public void CombatActor_TracksManaAndPreservesBothResourceRatios()
        {
            CombatActor actor = CreateActor("resource_ratio", 100f, 100f);
            actor.ReceiveDamage(CreateDamageResult(25f));
            Assert.IsTrue(actor.TrySpendMana(60f));
            ModifierInstance health = CreateFlatModifier(StatIds.MaxHealth, 100f);
            ModifierInstance mana = CreateFlatModifier(StatIds.Mana, 100f);

            actor.SetModifiers(new[] { health, mana });

            Assert.AreEqual(200f, actor.MaxHealth, 0.001f);
            Assert.AreEqual(150f, actor.CurrentHealth, 0.001f);
            Assert.AreEqual(200f, actor.MaxMana, 0.001f);
            Assert.AreEqual(80f, actor.CurrentMana, 0.001f);

            CombatActor zeroManaActor = CreateActor("zero_mana", 100f, 0f);
            zeroManaActor.SetModifiers(new[] { CreateFlatModifier(StatIds.Mana, 50f) });
            Assert.AreEqual(50f, zeroManaActor.MaxMana, 0.001f);
            Assert.AreEqual(50f, zeroManaActor.CurrentMana, 0.001f);
            zeroManaActor.SetModifiers(null);
            Assert.AreEqual(0f, zeroManaActor.MaxMana, 0.001f);
            Assert.AreEqual(0f, zeroManaActor.CurrentMana, 0.001f);
        }

        [Test]
        public void CombatSystem_PublishesActualResourceChangesWithoutPassiveHealingFeedback()
        {
            CombatActor actor = CreateActor("resource_events", 100f, 100f);
            actor.ReceiveDamage(CreateDamageResult(40f));
            List<ActorResourceChangedEvent> resourceEvents = new List<ActorResourceChangedEvent>();
            List<ActorHealedEvent> healedEvents = new List<ActorHealedEvent>();
            _architecture.RegisterEvent<ActorResourceChangedEvent>(resourceEvents.Add);
            _architecture.RegisterEvent<ActorHealedEvent>(healedEvents.Add);
            CombatSystem combat = _architecture.GetSystem<CombatSystem>();

            Assert.IsTrue(combat.TrySpendMana(actor, 30f));
            Assert.AreEqual(10f, combat.RestoreMana(actor, 10f));
            Assert.AreEqual(20f, combat.ApplyHealing(actor, 20f));
            Assert.AreEqual(5f, combat.ApplyHealthRegeneration(actor, 5f));

            Assert.AreEqual(4, resourceEvents.Count);
            Assert.AreEqual(ActorResourceChangeReason.SkillCost, resourceEvents[0].Reason);
            Assert.AreEqual(ActorResourceType.Mana, resourceEvents[0].ResourceType);
            Assert.AreEqual(ActorResourceChangeReason.Restore, resourceEvents[1].Reason);
            Assert.AreEqual(ActorResourceChangeReason.Healing, resourceEvents[2].Reason);
            Assert.AreEqual(ActorResourceChangeReason.Regeneration, resourceEvents[3].Reason);
            Assert.AreEqual(1, healedEvents.Count, "被动恢复不能生成主动治疗飘字事件");
            Assert.AreEqual(20f, healedEvents[0].Amount, 0.001f);
        }

        [Test]
        public void ResourceRegeneration_UsesElapsedTimeAndStopsWhenPausedOrDead()
        {
            CombatActor actor = CreateActor("regeneration", 100f, 100f, 2f, 5f);
            actor.ReceiveDamage(CreateDamageResult(30f));
            Assert.IsTrue(actor.TrySpendMana(50f));
            ResourceRegenerationSystem regeneration = _architecture.GetSystem<ResourceRegenerationSystem>();

            regeneration.AdvanceRegeneration(2f);

            Assert.AreEqual(74f, actor.CurrentHealth, 0.001f);
            Assert.AreEqual(60f, actor.CurrentMana, 0.001f);

            _architecture.GetSystem<GameplayPauseSystem>().SetPaused(true);
            regeneration.AdvanceRegeneration(10f);
            Assert.AreEqual(74f, actor.CurrentHealth, 0.001f);
            Assert.AreEqual(60f, actor.CurrentMana, 0.001f);
            _architecture.GetSystem<GameplayPauseSystem>().SetPaused(false);

            actor.ReceiveDamage(CreateDamageResult(999f));
            float manaBefore = actor.CurrentMana;
            regeneration.AdvanceRegeneration(10f);
            Assert.AreEqual(0f, actor.CurrentHealth, 0.001f);
            Assert.AreEqual(manaBefore, actor.CurrentMana, 0.001f);
        }

        [Test]
        public void FireProjectileCommand_InsufficientManaRejectsBeforeAttackRandomSeed()
        {
            CombatActor actor = CreateActor("insufficient_mana", 100f, 10f);
            Assert.IsTrue(actor.TrySpendMana(9f));
            ProjectileSkillDefinition skill = CreateSkill(8f);
            GameplayRandomSystem random = _architecture.GetSystem<GameplayRandomSystem>();
            random.Configure(true, 13579);
            int expectedFirstSeed = random.NextSeed(GameplayRandomChannel.PlayerAttack);
            random.Configure(true, 13579);
            List<SkillCastRejectedEvent> rejectedEvents = new List<SkillCastRejectedEvent>();
            int attackEvents = 0;
            _architecture.RegisterEvent<SkillCastRejectedEvent>(rejectedEvents.Add);
            _architecture.RegisterEvent<ActorAttackedEvent>(_ => attackEvents++);

            SkillCastResult result = _architecture.SendCommand(new FireProjectileCommand(
                actor,
                skill,
                Vector3.zero,
                Vector2.right));

            Assert.AreEqual(SkillCastStatus.InsufficientMana, result.Status);
            Assert.AreEqual(1f, actor.CurrentMana, 0.001f);
            Assert.AreEqual(expectedFirstSeed, random.NextSeed(GameplayRandomChannel.PlayerAttack));
            Assert.AreEqual(0, attackEvents);
            Assert.AreEqual(1, rejectedEvents.Count);
            Assert.AreEqual(SkillCastRejectionReason.InsufficientMana, rejectedEvents[0].Reason);
            Assert.AreEqual(8f, rejectedEvents[0].RequiredMana, 0.001f);
        }

        [Test]
        public void ResourceConfiguration_ContainsTwentyThreeDefinitionsAndFormalBaselines()
        {
            string[] statGuids = AssetDatabase.FindAssets(
                "t:StatDefinition",
                new[] { "Assets/Data/Preset/Stats" });
            List<StatDefinition> stats = statGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<StatDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null)
                .ToList();
            CharacterDefinition player = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/Data/Preset/Actors/玩家.asset");
            ProjectileSkillDefinition skill = AssetDatabase.LoadAssetAtPath<ProjectileSkillDefinition>(
                "Assets/Data/Preset/Skills/基础投射物技能.asset");

            Assert.AreEqual(23, StatIds.All.Count);
            Assert.AreEqual(23, stats.Count);
            CollectionAssert.AreEquivalent(StatIds.All, stats.Select(definition => definition.Id));
            Assert.IsEmpty(StatConfigurationValidator.Validate(stats));
            Assert.AreEqual(100f, player.Mana, 0.001f);
            Assert.AreEqual(1f, player.HealthRegeneration, 0.001f);
            Assert.AreEqual(5f, player.ManaRegeneration, 0.001f);
            Assert.AreEqual(8f, skill.ManaCost, 0.001f);
        }

        [Test]
        public void ResourceValidators_RejectNegativeStatsAndManaCost()
        {
            CharacterDefinition character = CreateScriptableObject<CharacterDefinition>();
            SetField(character, "_mana", -1f);
            SetField(character, "_healthRegeneration", -2f);
            SetField(character, "_manaRegeneration", -3f);
            ProjectileSkillDefinition skill = CreateSkill(-4f);

            List<string> characterIssues = RandomizationConfigurationValidator.Validate(character);
            List<string> skillIssues = RandomizationConfigurationValidator.Validate(skill);

            Assert.That(characterIssues, Has.Some.Contains("最大法力不能为负数"));
            Assert.That(characterIssues, Has.Some.Contains("生命恢复不能为负数"));
            Assert.That(characterIssues, Has.Some.Contains("法力恢复不能为负数"));
            Assert.That(skillIssues, Has.Some.Contains("法力消耗不能为负数"));
        }

        CombatActor CreateActor(
            string actorId,
            float maxHealth,
            float maxMana,
            float healthRegeneration = 0f,
            float manaRegeneration = 0f)
        {
            GameObject gameObject = new GameObject(actorId);
            gameObject.SetActive(false);
            CombatActor actor = gameObject.AddComponent<CombatActor>();
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.Mana, maxMana);
            stats.SetValue(StatIds.HealthRegeneration, healthRegeneration);
            stats.SetValue(StatIds.ManaRegeneration, manaRegeneration);
            actor.Configure(actorId, ActorTeam.Player, maxHealth, stats, TagSet.Empty);
            _objects.Add(gameObject);
            gameObject.SetActive(true);
            _architecture.GetSystem<CombatSystem>().RegisterActor(actor);
            return actor;
        }

        ProjectileSkillDefinition CreateSkill(float manaCost)
        {
            ProjectileSkillDefinition skill = CreateScriptableObject<ProjectileSkillDefinition>();
            DamageRollDefinition damage = new DamageRollDefinition();
            SetField(damage, "_amountRange", new Vector2(10f, 10f));
            SetField(skill, "_damageSource", ProjectileDamageSource.Skill);
            SetField(skill, "_baseDamages", new List<DamageRollDefinition> { damage });
            SetField(skill, "_manaCost", manaCost);
            return skill;
        }

        static DamageResult CreateDamageResult(float amount)
        {
            Dictionary<DamageType, float> damage = new Dictionary<DamageType, float>
            {
                { DamageType.Physical, amount },
            };
            return new DamageResult(true, false, damage, damage);
        }

        static ModifierInstance CreateFlatModifier(string statId, float value)
        {
            return new ModifierInstance(
                statId,
                ModifierOperation.Flat,
                ModifierScope.GlobalActor,
                value,
                DamageType.Physical,
                DamageType.Physical,
                TagQuery.Empty);
        }

        T CreateScriptableObject<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _objects.Add(value);
            return value;
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }
    }
}
