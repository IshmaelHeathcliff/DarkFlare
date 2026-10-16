using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct PlayerSkillState
    {
        public ProjectileSkillDefinition Skill { get; }
        public float RemainingSeconds { get; }
        public string SourceId { get; }
        public string IconGuid { get; }

        public PlayerSkillState(ProjectileSkillDefinition skill, float remainingSeconds, string sourceId = "", string iconGuid = "")
        {
            Skill = skill;
            RemainingSeconds = remainingSeconds;
            SourceId = sourceId;
            IconGuid = iconGuid;
        }
    }

    public sealed class PlayerSkillsModel : AbstractModel
    {
        readonly string[] _slots = Enumerable.Repeat(string.Empty, 6).ToArray();
        public IReadOnlyList<string> Slots => Array.AsReadOnly(_slots);
        internal double NormalCooldownEnd { get; set; }
        public float NormalCooldownRemaining => (float)Math.Max(0, NormalCooldownEnd - Time.timeAsDouble);
        protected override void OnInit() { }
        internal void SetSlots(IReadOnlyList<string> slots)
        {
            for (int i = 0; i < _slots.Length; i++) { _slots[i] = slots[i]; }
        }
    }

    public readonly struct PlayerSkillsChangedEvent { }
    public readonly struct PlayerSkillCastEvent
    {
        public CombatActor Actor { get; }
        public AttackSnapshot Attack { get; }
        public PlayerSkillCastEvent(CombatActor actor, AttackSnapshot attack) { Actor = actor; Attack = attack; }
    }

    public sealed class PlayerSkillStateRegistry : AbstractSystem
    {
        readonly Dictionary<CombatActor, Func<PlayerSkillState>> _readers = new Dictionary<CombatActor, Func<PlayerSkillState>>();
        bool _disposed;
        bool _casting;
        public bool IsInputEnabled => !_disposed && (this.GetUtility<GameInput>()?.IsGameplayEnabled ?? true);

        protected override void OnInit() { }

        protected override void OnDeinit() { _disposed = true; _readers.Clear(); }

        public void Register(CombatActor actor, Func<PlayerSkillState> reader)
        {
            _readers[actor] = reader;
        }

        public void Unregister(CombatActor actor)
        {
            if (actor != null) { _readers.Remove(actor); }
        }

        public PlayerSkillState Capture(CombatActor actor)
        {
            if (actor == null || !actor.isActiveAndEnabled || _disposed) { return default; }
            ItemInstance weapon = this.GetModel<EquipmentModel>().GetWeapon(actor);
            if (actor.GetComponent<PlayerController>() == null && _readers.TryGetValue(actor, out Func<PlayerSkillState> reader)) { return reader(); }
            return new PlayerSkillState(weapon?.BaseDefinition.NormalSkill,
                this.GetModel<PlayerSkillsModel>().NormalCooldownRemaining, weapon?.InstanceId ?? string.Empty);
        }

        public PlayerSkillState CaptureSlot(int slot)
        {
            if (_disposed || slot < 0 || slot >= 6) { return default; }
            string id = this.GetModel<PlayerSkillsModel>().Slots[slot];
            ItemInstance flame = FindFlame(id, this.GetModel<SkillBoardModel>().State, this.GetModel<PlayerGrowthModel>().State);
            return flame == null ? default : new PlayerSkillState(flame.SkillBoardState.FlameSkill,
                flame.SkillBoardState.CooldownRemainingSeconds, flame.InstanceId, flame.BaseDefinition.Icon?.AssetGUID ?? string.Empty);
        }

        internal static ItemInstance FindFlame(string id, SkillBoardState board, PlayerGrowthState growth)
        {
            return board.Embeddings.FirstOrDefault(e => e.Item.InstanceId == id
                && e.Item.BaseDefinition.ItemType == ItemType.Flame && growth.IsActive(e.Slot.PlacementId))?.Item;
        }

        public bool Assign(string playerId, int slot, string flameId)
        {
            if (_disposed || slot < 0 || slot >= 6 || !this.GetSystem<SkillBoardSystem>().IsCurrentPlayer(playerId)) { return false; }
            if (!string.IsNullOrEmpty(flameId) && FindFlame(flameId, this.GetModel<SkillBoardModel>().State,
                this.GetModel<PlayerGrowthModel>().State) == null) { return false; }
            PlayerSkillsModel model = this.GetModel<PlayerSkillsModel>();
            string[] slots = model.Slots.ToArray();
            for (int i = 0; i < slots.Length; i++) { if (slots[i] == flameId) { slots[i] = string.Empty; } }
            slots[slot] = flameId ?? string.Empty;
            model.SetSlots(slots);
            this.SendEvent(new PlayerSkillsChangedEvent());
            return true;
        }

        internal void Reconcile(SkillBoardState board, PlayerGrowthState growth)
        {
            PlayerSkillsModel model = this.GetModel<PlayerSkillsModel>();
            model.SetSlots(model.Slots.Select(id => FindFlame(id, board, growth) != null ? id : string.Empty).ToArray());
        }

        public void Restore(IReadOnlyList<string> slots, float normalRemaining)
        {
            if (_disposed || slots == null || slots.Count != 6 || !FiniteNonnegative(normalRemaining))
            { throw new ArgumentException("技能槽或普通技能冷却非法"); }
            var ids = new HashSet<string>();
            foreach (string id in slots)
            {
                if (id == null || id.Length > 0 && (!ids.Add(id) || FindFlame(id,
                    this.GetModel<SkillBoardModel>().State, this.GetModel<PlayerGrowthModel>().State) == null))
                { throw new ArgumentException("火焰来源失效或重复分配"); }
            }
            PlayerSkillsModel model = this.GetModel<PlayerSkillsModel>();
            model.SetSlots(slots);
            model.NormalCooldownEnd = Time.timeAsDouble + normalRemaining;
        }

        public SkillCastResult Cast(CombatActor actor, int slot, string expectedSourceId, Vector3 position, Vector2 direction)
        {
            if (_disposed || _casting || actor == null || !actor.IsAlive || !actor.isActiveAndEnabled
                || !this.GetSystem<SkillBoardSystem>().IsCurrentPlayer(SkillBoardPlayerIdentity.GetProfileId(actor)))
            { return new SkillCastResult(SkillCastStatus.InvalidOwner); }
            GameInput input = this.GetUtility<GameInput>();
            if (input != null && !input.IsGameplayEnabled || this.GetSystem<GameplayPauseSystem>().IsPaused)
            { return new SkillCastResult(SkillCastStatus.ActionBlocked); }
            if (!Finite(direction.x) || !Finite(direction.y) || direction.sqrMagnitude < 0.0001f
                || !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
            { return new SkillCastResult(SkillCastStatus.InvalidAim); }
            PlayerSkillState state = slot == -1 ? Capture(actor) : CaptureSlot(slot);
            if (state.Skill == null || string.IsNullOrEmpty(expectedSourceId) || state.SourceId != expectedSourceId)
            { return new SkillCastResult(SkillCastStatus.InvalidSource); }
            if (state.RemainingSeconds > 0) { return new SkillCastResult(SkillCastStatus.Cooldown); }
            ItemInstance flame = slot == -1 ? null : FindFlame(state.SourceId,
                this.GetModel<SkillBoardModel>().State, this.GetModel<PlayerGrowthModel>().State);
            IReadOnlyList<ModifierInstance> modifiers = flame == null ? Array.Empty<ModifierInstance>()
                : SkillBoardEffects.Resolve(this.GetModel<SkillBoardModel>().State, this.GetModel<PlayerGrowthModel>().State).Flames[flame.InstanceId];
            _casting = true;
            try
            {
                return FireProjectileCommand.Perform(((IBelongToArchitecture)this).GetArchitecture(), actor, state.Skill, position, direction.normalized,
                    modifiers, flame?.InstanceId ?? string.Empty, () =>
                    {
                        if (flame == null) { this.GetModel<PlayerSkillsModel>().NormalCooldownEnd = Time.timeAsDouble + state.Skill.Cooldown; }
                        else { flame.SkillBoardState.RestoreCooldown(state.Skill.Cooldown); }
                    });
            }
            finally { _casting = false; }
        }

        internal static bool FiniteNonnegative(float value) { return Finite(value) && value >= 0; }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public sealed class RestoreNormalSkillCooldownCommand : AbstractCommand
    {
        readonly float _remaining;
        public RestoreNormalSkillCooldownCommand(float remaining) { _remaining = remaining; }
        protected override void OnExecute()
        {
            this.GetSystem<PlayerSkillStateRegistry>().Restore(this.GetModel<PlayerSkillsModel>().Slots, _remaining);
        }
    }

    public sealed class AssignPlayerSkillCommand : AbstractCommand<bool>
    {
        readonly string _player;
        readonly int _slot;
        readonly string _flame;
        public AssignPlayerSkillCommand(string player, int slot, string flame) { _player = player; _slot = slot; _flame = flame; }
        protected override bool OnExecute() { return this.GetSystem<PlayerSkillStateRegistry>().Assign(_player, _slot, _flame); }
    }

    public sealed class CastPlayerSkillCommand : AbstractCommand<SkillCastResult>
    {
        readonly CombatActor _actor;
        readonly int _slot;
        readonly string _source;
        readonly Vector3 _position;
        readonly Vector2 _direction;
        public CastPlayerSkillCommand(CombatActor actor, int slot, string source, Vector3 position, Vector2 direction)
        { _actor = actor; _slot = slot; _source = source; _position = position; _direction = direction; }
        protected override SkillCastResult OnExecute()
        { return this.GetSystem<PlayerSkillStateRegistry>().Cast(_actor, _slot, _source, _position, _direction); }
    }

    public sealed class GetPlayerSkillsQuery : AbstractQuery<IReadOnlyList<PlayerSkillState>>
    {
        protected override IReadOnlyList<PlayerSkillState> OnDo()
        {
            PlayerSkillStateRegistry skills = this.GetSystem<PlayerSkillStateRegistry>();
            return Array.AsReadOnly(Enumerable.Range(0, 6).Select(skills.CaptureSlot).ToArray());
        }
    }

    public sealed class GetPlayerSkillRuntimeQuery : AbstractQuery<AttributeRuntimeSnapshot>
    {
        readonly int _slot;
        public GetPlayerSkillRuntimeQuery(int slot) { _slot = slot; }
        protected override AttributeRuntimeSnapshot OnDo()
        {
            CombatActor actor = GetAttributeDetailsQuery.FindPlayer(this.GetModel<CombatModel>());
            return new AttributeRuntimeSnapshot(actor, this.GetModel<EquipmentModel>(),
                this.GetSystem<PlayerSkillStateRegistry>().CaptureSlot(_slot), this.GetSystem<GameplayPauseSystem>().IsPaused,
                this.GetSystem<PlayerSkillStateRegistry>().IsInputEnabled);
        }
    }

    public sealed class GetPlayerNormalSkillQuery : AbstractQuery<PlayerSkillState>
    {
        readonly CombatActor _actor;
        public GetPlayerNormalSkillQuery(CombatActor actor) { _actor = actor; }
        protected override PlayerSkillState OnDo() { return this.GetSystem<PlayerSkillStateRegistry>().Capture(_actor); }
    }
}
