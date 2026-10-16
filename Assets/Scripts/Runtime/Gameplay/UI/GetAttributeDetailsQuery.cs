using System;
using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class FlameSkillDetail
    {
        public string InstanceId { get; }
        public string SkillId { get; }
        public IReadOnlyList<ModifierInstance> Modifiers { get; }
        public FlameSkillDetail(string id, string skill, IReadOnlyList<ModifierInstance> modifiers)
        { InstanceId = id; SkillId = skill; Modifiers = modifiers; }
    }
    public readonly struct AttributeDetail
    {
        public string Id { get; }
        public float BaseValue { get; }
        public float RawValue { get; }
        public float EffectiveValue { get; }
        public IReadOnlyList<StatCalculationStep> Steps { get; }

        public AttributeDetail(string id, float baseValue, float rawValue, float effectiveValue, List<StatCalculationStep> steps)
        {
            Id = id;
            BaseValue = baseValue;
            RawValue = rawValue;
            EffectiveValue = effectiveValue;
            Steps = steps.AsReadOnly();
        }
    }

    public sealed class AttributeDetailsSnapshot
    {
        public TagSet ActorTags { get; }
        public int StatsRevision { get; }
        public bool HasPlayer { get; }
        public IReadOnlyList<AttributeDetail> Attributes { get; }
        public IReadOnlyList<ModifierInstance> Modifiers { get; }
        public IReadOnlyList<DamageDetailSnapshot> BaseDamages { get; }
        public LocalizedMessage DamageSourceName { get; }
        public string SkillId { get; }
        public float Interval { get; }
        public float ManaCost { get; }
        public IReadOnlyList<FlameSkillDetail> Flames { get; }

        public AttributeDetailsSnapshot(CombatActor actor, EquipmentModel equipment, PlayerSkillState skillState,
            SkillBoardState board = null, PlayerGrowthState growth = null)
        {
            HasPlayer = actor != null;
            ActorTags = actor != null ? actor.Tags : TagSet.Empty;
            var attributes = new List<AttributeDetail>();
            var modifiers = new List<ModifierInstance>();
            var damages = new List<DamageDetailSnapshot>();
            Attributes = attributes.AsReadOnly();
            Modifiers = modifiers.AsReadOnly();
            BaseDamages = damages.AsReadOnly();
            var flames = new List<FlameSkillDetail>();
            Flames = flames.AsReadOnly();
            if (actor == null) { return; }
            if (board != null && growth != null)
            {
                SkillBoardEffectProjection projection = SkillBoardEffects.Resolve(board, growth);
                foreach (SkillBoardEmbedding embedding in board.Embeddings)
                {
                    if (projection.Flames.TryGetValue(embedding.Item.InstanceId, out IReadOnlyList<ModifierInstance> effects))
                    { flames.Add(new FlameSkillDetail(embedding.Item.InstanceId, embedding.Item.SkillBoardState.FlameSkill.Id, effects)); }
                }
            }
            StatsRevision = actor.StatsRevision;
            StatBlock baseStats = actor.CaptureBaseStats();
            var steps = new List<StatCalculationStep>();
            CombatStatResolver.Build(baseStats, actor.Modifiers, steps, actor.Tags);
            foreach (string id in StatIds.All)
            {
                var selected = steps.FindAll(step => step.StatId == id);
                float effective = id == StatIds.MaxHealth ? actor.MaxHealth
                    : id == StatIds.Mana ? actor.MaxMana : CombatStatValues.Effective(actor.Stats, id);
                attributes.Add(new AttributeDetail(id, baseStats.GetValue(id), actor.Stats.GetValue(id), effective, selected));
            }
            modifiers.AddRange(actor.Modifiers);
            ItemInstance weapon = equipment?.GetWeapon(actor);
            ProjectileSkillDefinition skill = skillState.Skill;
            SkillId = skill?.Id;
            Interval = skill != null ? skill.Cooldown : 0f;
            ManaCost = skill != null ? skill.ManaCost : 0f;
            if (skill == null) { return; }
            IReadOnlyList<DamageRollDefinition> source;
            if (skill.DamageSource == ProjectileDamageSource.EquippedWeapon)
            {
                DamageSourceName = weapon?.BaseDefinition?.LocalizedName?.Message ?? default;
                source = weapon?.BaseDefinition?.BaseDamages;
                modifiers.AddRange(EquipmentEffectResolver.CollectLocalWeaponModifiers(weapon));
            }
            else
            {
                DamageSourceName = LocalizedMessage.Ui("attributes.skill." + skill.Id);
                source = skill.BaseDamages;
            }
            if (source == null) { return; }
            foreach (DamageRollDefinition damage in source)
            {
                damages.Add(new DamageDetailSnapshot(damage.DamageType, damage.AmountRange.x, damage.AmountRange.y,
                    Array.Empty<LocalizedMessage>()));
            }
        }
    }

    public readonly struct AttributeRuntimeSnapshot
    {
        public int StatsRevision { get; }
        public bool HasPlayer { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public float Mana { get; }
        public float MaxMana { get; }
        public float HealthRegeneration { get; }
        public float ManaRegeneration { get; }
        public float RemainingSeconds { get; }
        public string SkillId { get; }
        public float Interval { get; }
        public float ManaCost { get; }
        public float CooldownNormalized => Interval > 0f ? Math.Clamp(RemainingSeconds / Interval, 0f, 1f) : 0f;
        public string ResourceState { get; }
        public string SkillState { get; }

        public AttributeRuntimeSnapshot(CombatActor actor, EquipmentModel equipment, PlayerSkillState skill, bool paused, bool inputEnabled = true)
        {
            HasPlayer = actor != null;
            Health = actor != null ? actor.CurrentHealth : 0f;
            StatsRevision = actor != null ? actor.StatsRevision : 0;
            MaxHealth = actor != null ? actor.MaxHealth : 0f;
            Mana = actor != null ? actor.CurrentMana : 0f;
            MaxMana = actor != null ? actor.MaxMana : 0f;
            HealthRegeneration = actor != null ? Math.Max(0f, actor.Stats.GetValue(StatIds.HealthRegeneration)) : 0f;
            ManaRegeneration = actor != null ? ResourceRegenerationSystem.CalculateManaRegenerationPerSecond(MaxMana, actor.Stats.GetValue(StatIds.ManaRegeneration)) : 0f;
            RemainingSeconds = skill.RemainingSeconds;
            SkillId = skill.Skill?.Id;
            Interval = skill.Skill != null ? skill.Skill.Cooldown : 0f;
            ManaCost = skill.Skill != null ? skill.Skill.ManaCost : 0f;
            ResourceState = actor == null ? "waiting" : !actor.isActiveAndEnabled ? "inactive" : !actor.IsAlive ? "dead"
                : paused ? "paused" : !inputEnabled ? "inactive" : Health >= MaxHealth && Mana >= MaxMana ? "full" : "recovering";
            SkillState = actor == null ? "waiting" : !actor.isActiveAndEnabled ? "inactive" : !actor.IsAlive ? "dead"
                : !AttackSnapshotFactory.CanCreateProjectile(actor, skill.Skill, equipment) ? "no_source"
                : (actor.BlockedActions & StatusActionBlock.Cast) != 0
                    || skill.Skill.DamageSource == ProjectileDamageSource.EquippedWeapon && (actor.BlockedActions & StatusActionBlock.Attack) != 0 ? "inactive"
                : !actor.CanSpendMana(skill.Skill.ManaCost) ? "no_mana" : paused ? "paused"
                : !inputEnabled ? "inactive" : skill.RemainingSeconds > 0f ? "cooldown" : "ready";
        }
    }

    public class GetAttributeDetailsQuery : AbstractQuery<AttributeDetailsSnapshot>
    {
        protected override AttributeDetailsSnapshot OnDo()
        {
            CombatActor actor = FindPlayer(this.GetModel<CombatModel>());
            PlayerSkillState skill = this.GetSystem<PlayerSkillStateRegistry>()?.Capture(actor) ?? default;
            return new AttributeDetailsSnapshot(actor, this.GetModel<EquipmentModel>(), skill,
                this.GetModel<SkillBoardModel>().State, this.GetModel<PlayerGrowthModel>().State);
        }

        internal static CombatActor FindPlayer(CombatModel model)
        {
            foreach (CombatActor actor in model.GetActorsByTeam(ActorTeam.Player))
            {
                if (actor != null) { return actor; }
            }
            return null;
        }
    }

    public class GetAttributeRuntimeQuery : AbstractQuery<AttributeRuntimeSnapshot>
    {
        protected override AttributeRuntimeSnapshot OnDo()
        {
            CombatActor actor = GetAttributeDetailsQuery.FindPlayer(this.GetModel<CombatModel>());
            return new AttributeRuntimeSnapshot(actor, this.GetModel<EquipmentModel>(),
                this.GetSystem<PlayerSkillStateRegistry>()?.Capture(actor) ?? default,
                this.GetSystem<GameplayPauseSystem>().IsPaused, this.GetSystem<PlayerSkillStateRegistry>().IsInputEnabled);
        }
    }
}
