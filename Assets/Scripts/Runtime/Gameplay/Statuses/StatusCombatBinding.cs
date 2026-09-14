using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed partial class StatusSystem
    {
        readonly Dictionary<CombatActor, StatusTargetId> _actorTargets = new Dictionary<CombatActor, StatusTargetId>();
        readonly Dictionary<StatusTargetId, CombatActor> _targetActors = new Dictionary<StatusTargetId, CombatActor>();

        public StatusTargetId GetTarget(CombatActor actor)
        {
            return actor != null && _actorTargets.TryGetValue(actor, out StatusTargetId target) && _store.IsRegistered(target) ? target : default;
        }

        public StatusTargetId Bind(CombatActor actor, PlayerId id)
        {
            if (!ContentId.IsValidSegment(id.Value)) { throw new ArgumentException("玩家身份非法", nameof(id)); }
            return Attach(actor, "player:" + id.Value, () => _store.RegisterTarget(id));
        }

        public StatusTargetId Bind(CombatActor actor, MonsterInstanceId id)
        {
            if (!MonsterInstanceId.TryParse(id.Value, out _)) { throw new ArgumentException("怪物身份非法", nameof(id)); }
            return Attach(actor, id.Value, () => _store.RegisterTarget(id));
        }

        StatusTargetId Attach(CombatActor actor, string actorKey, Func<StatusTargetId> register)
        {
            if (actor == null || !actor.isActiveAndEnabled || !actor.IsAlive) { return default; }
            if (_targetActors.Any(pair => pair.Key.ActorKey == actorKey && pair.Value != actor))
            {
                throw new ArgumentException("状态身份已绑定其他角色");
            }
            if (_actorTargets.TryGetValue(actor, out StatusTargetId existing))
            {
                if (existing.ActorKey == actorKey && _store.IsRegistered(existing)) { return existing; }
                Unbind(actor);
            }
            StatusTargetId target = register();
            if (_targetActors.ContainsKey(target)) { throw new ArgumentException("状态身份已绑定其他角色"); }
            _actorTargets.Add(actor, target);
            actor.StatusActorKey = target.ActorKey;
            _targetActors.Add(target, actor);
            _store.Attach(target, new Binding(this, actor, target));
            EquipmentLoadout loadout = this.GetModel<EquipmentModel>().GetLoadout(actor);
            if (loadout != null && !TryRebuildEquipment(actor, loadout)) { throw new ArgumentException("装备状态恢复失败"); }
            return target;
        }

        internal bool TryRebuildEquipment(CombatActor actor, EquipmentLoadout loadout)
        {
            try
            {
                StatusTargetId target = GetTarget(actor);
                var equipment = EquipmentEffectResolver.CollectActorModifiers(loadout);
                if (!target.IsValid)
                {
                    if (actor.IsAlive && loadout.Slots.Any(pair => pair.Value?.BaseDefinition?.ProvidedStatus != null)) { return false; }
                    CombatResourceSnapshot before = actor.Resources;
                    actor.SetModifierSource("equipment", equipment);
                    this.GetSystem<CombatSystem>().PublishResourceChanges(actor, before, ActorResourceChangeReason.MaximumChanged);
                    return true;
                }
                var desired = new Dictionary<StatusSource, StatusMutation>();
                foreach (var pair in loadout.Slots)
                {
                    ItemInstance item = pair.Value;
                    StatusDefinition definition = item?.BaseDefinition?.ProvidedStatus;
                    if (definition == null) { continue; }
                    StatusRules rules = definition.CreateRules();
                    if (rules.Lifetime != StatusLifetime.SourceOwned || rules.Category == StatusCategory.Ailment || rules.Interval != 0)
                    {
                        throw new ArgumentException("装备提供状态必须由来源维持且不是异常");
                    }
                    var source = new StatusSource(StatusSourceKind.Equipment, item.InstanceId + ":" + definition.Id, target);
                    desired.Add(source, StatusMutation.Apply(rules, source, definition.CreateEffects(new Random(item.Seed))));
                }
                var changes = new List<StatusMutation>();
                foreach (StatusSource source in _store.Capture(target).Layers
                    .Where(layer => layer.Source.Kind == StatusSourceKind.Equipment && layer.Source.Actor == target
                        && layer.Rules.Lifetime == StatusLifetime.SourceOwned).Select(layer => layer.Source).Distinct())
                {
                    if (!desired.ContainsKey(source)) { changes.Add(StatusMutation.ReleaseSource(source)); }
                }
                changes.AddRange(desired.OrderBy(pair => pair.Key.Key, StringComparer.Ordinal).Select(pair => pair.Value));
                StatusPreparation preparation = _store.Prepare(target, changes);
                if (!preparation.Preview.Succeeded) { return false; }
                IStatusProjection projection = new Binding(this, actor, target).Prepare(preparation.Preview.Snapshot, equipment);
                preparation.Projection = projection;
                using (StatusCommit commit = _store.Commit(preparation))
                {
                    if (!commit.Result.Succeeded) { return false; }
                    if (commit.Result.Code == StatusResultCode.NoChange)
                    {
                        if (!projection.IsCurrent) { return false; }
                        projection.Apply();
                        projection.Publish();
                        return true;
                    }
                    return commit.Publish();
                }
            }
            catch (ArgumentException) { return false; }
        }

        public void BindConfiguredActor(CombatActor actor)
        {
            if (actor == null) { return; }
            PlayerController player = actor.GetComponent<PlayerController>();
            MonsterController monster = actor.GetComponent<MonsterController>();
            if (player != null && player.Definition != null) { Bind(actor, player.Id); }
            else if (monster != null && monster.Instance != null) { Bind(actor, monster.Instance.Id); }
        }

        public void Unbind(CombatActor actor)
        {
            if (ReferenceEquals(actor, null) || !_actorTargets.TryGetValue(actor, out StatusTargetId target)) { return; }
            _actorTargets.Remove(actor);
            _targetActors.Remove(target);
            _store.UnregisterBoundTarget(target);
            foreach (StatusTargetId recipient in _targetActors.Keys.ToArray())
            {
                StatusSource[] sources = _store.Capture(recipient).Layers
                    .Where(layer => layer.Rules.Lifetime == StatusLifetime.SourceOwned && layer.Source.Actor == target)
                    .Select(layer => layer.Source).Distinct().ToArray();
                foreach (StatusSource source in sources) { _store.ReleaseBoundSource(recipient, source); }
            }
        }

        public StatusAdvanceResult Advance(double elapsed, int eventBudget = 256)
        {
            return _store.Advance(elapsed, eventBudget);
        }

        sealed class Binding : IStatusParticipant
        {
            readonly StatusSystem _system;
            readonly CombatActor _actor;
            readonly StatusTargetId _target;
            internal Binding(StatusSystem system, CombatActor actor, StatusTargetId target)
            {
                _system = system; _actor = actor; _target = target;
            }

            public StatusMutation Normalize(StatusTargetId target, StatusMutation mutation)
            {
                if (mutation?.Effects == null) { return mutation; }
                bool frozenSource = mutation.Rules.Lifetime == StatusLifetime.Timed && mutation.Effects.DamageSource != null
                    && mutation.Effects.DamageStage == StatusDamageStage.SourceResolved;
                if (!frozenSource && mutation.Source.Actor.IsValid && (!_system._targetActors.TryGetValue(mutation.Source.Actor, out CombatActor provider)
                    || provider == null || !provider.IsAlive)) { throw new ArgumentException("状态施加来源角色已失效"); }
                StatusEffectSnapshot effects = mutation.Effects;
                if (effects.PeriodicDamage.Count == 0) { return AilmentResistanceResolver.Normalize(mutation, _actor.Stats); }
                _system._targetActors.TryGetValue(mutation.Source.Actor, out CombatActor sourceActor);
                DamageSourceSnapshot source = effects.DamageSource;
                if (source == null && sourceActor != null && sourceActor.IsAlive)
                {
                    source = new DamageSourceSnapshot(mutation.Source.Actor.ActorKey, sourceActor.Team,
                        mutation.Source.Kind == StatusSourceKind.Skill ? mutation.Source.Key : null,
                        mutation.Source.Kind == StatusSourceKind.Consumable || mutation.Source.Kind == StatusSourceKind.Equipment ? mutation.Source.Key : null,
                        new CombatTagContext(sourceActorTags: sourceActor.Tags, targetActorTags: _actor.Tags, legacyTags: sourceActor.Tags));
                }
                if (source == null) { throw new ArgumentException("周期伤害缺少显式来源阵营和身份"); }
                IReadOnlyList<DamagePacket> damage = effects.PeriodicDamage;
                if (effects.DamageStage == StatusDamageStage.Base)
                {
                    if (sourceActor == null || !sourceActor.IsAlive) { throw new ArgumentException("基础周期伤害需要有效来源角色"); }
                    damage = DamageCalculator.ResolveSource(damage, sourceActor.Stats, sourceActor.Modifiers,
                        source.Tags.WithTargetActorTags(_actor.Tags));
                }
                return AilmentResistanceResolver.Normalize(mutation.WithEffects(new StatusEffectSnapshot(effects.Strength, effects.Modifiers, damage,
                    effects.BlockedActions, StatusDamageStage.SourceResolved, source, effects.Resistance)), _actor.Stats);
            }

            public IStatusProjection Prepare(StatusTargetSnapshot snapshot)
            {
                return Prepare(snapshot, null);
            }

            internal IStatusProjection Prepare(StatusTargetSnapshot snapshot, IEnumerable<ModifierInstance> equipment)
            {
                if (_actor == null) { return null; }
                var modifiers = new List<ModifierInstance>();
                TagSet tags = TagSet.Empty;
                StatusActionBlock blocks = StatusActionBlock.None;
                foreach (StatusLayerSnapshot layer in snapshot.Layers) { tags = tags.Union(layer.Rules.Tags); }
                foreach (var group in snapshot.Layers.Where(layer => layer.IsActive).GroupBy(layer => layer.Rules.Id)
                    .OrderBy(group => group.Key.LocalId, StringComparer.Ordinal))
                {
                    StatusLayerSnapshot first = group.First();
                    bool uniform = first.Rules.Repeat == StatusRepeatMode.Uniform;
                    int count = uniform ? group.Count() : 1;
                    foreach (StatusLayerSnapshot layer in uniform ? group.Take(1) : group)
                    {
                        blocks |= layer.Effects.BlockedActions;
                        foreach (ModifierInstance modifier in layer.Effects.Modifiers)
                        {
                            float value = modifier.Operation == ModifierOperation.Override ? modifier.Value : modifier.Value * count;
                            modifiers.Add(modifier.WithValue(value).WithOrigin(new ModifierOrigin(ModifierOriginKind.Status,
                                layer.Rules.Id.LocalId, layer.InstanceId, count)));
                        }
                    }
                }
                var replacements = new Dictionary<string, IEnumerable<ModifierInstance>> { ["status"] = modifiers };
                if (equipment != null) { replacements["equipment"] = equipment; }
                CombatActor.ActorEffectPreparation preparation = _actor.PrepareEffects(replacements, tags, blocks);
                return new Projection(_system, _actor, _target, snapshot.IsRegistered, preparation,
                    snapshot.Layers.Where(layer => layer.Rules.Lifetime == StatusLifetime.SourceOwned && layer.Source.Actor.IsValid)
                        .Select(layer => layer.Source.Actor).Distinct().ToArray());
            }

            public void Tick(StatusTick tick)
            {
                if (_system.GetTarget(_actor) != _target || !_actor.IsAlive) { return; }
                StatusEffectSnapshot effects = tick.Layer.Effects;
                if (effects.PeriodicDamage.Count > 0)
                {
                    _system.GetSystem<CombatSystem>().ApplyPeriodicDamage(effects.DamageSource, _actor, effects.PeriodicDamage);
                }
            }
        }

        sealed class Projection : IStatusProjection
        {
            readonly StatusSystem _system;
            readonly CombatActor _actor;
            readonly StatusTargetId _target;
            readonly bool _registered;
            readonly CombatActor.ActorEffectPreparation _preparation;
            readonly StatusTargetId[] _owners;
            internal Projection(StatusSystem system, CombatActor actor, StatusTargetId target, bool registered,
                CombatActor.ActorEffectPreparation preparation, StatusTargetId[] owners)
            {
                _system = system; _actor = actor; _target = target; _registered = registered; _preparation = preparation;
                _owners = owners;
            }
            public bool IsCurrent => _preparation.IsCurrent && (!_registered || _system.GetTarget(_actor) == _target && _actor.IsAlive)
                && _owners.All(owner => _system._targetActors.TryGetValue(owner, out CombatActor provider) && provider != null && provider.IsAlive);
            public void Apply() { _preparation.Apply(); }
            public void Publish()
            {
                _system.GetSystem<CombatSystem>().PublishResourceChanges(_actor, _preparation.Before, ActorResourceChangeReason.MaximumChanged);
            }
        }
    }

    public sealed class BindActorStatusesCommand : AbstractCommand
    {
        readonly CombatActor _actor;
        public BindActorStatusesCommand(CombatActor actor) { _actor = actor; }
        protected override void OnExecute() { this.GetSystem<StatusSystem>().BindConfiguredActor(_actor); }
    }
}
