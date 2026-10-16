using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class PlayerGrowthPreview
    {
        public bool Success => Error == null;
        public string Error { get; }
        public SkillBoardFailure Failure { get; }
        public PlayerGrowthState Result { get; }
        internal PlayerGrowthSystem Owner { get; }
        internal string PlayerId { get; }
        internal PlayerGrowthState Before { get; }
        internal SkillBoardState Board { get; }
        internal CombatActor.ActorEffectPreparation Effects { get; }
        internal CombatActor Actor { get; }
        internal bool Consumed { get; set; }

        internal PlayerGrowthPreview(string error, SkillBoardFailure failure = SkillBoardFailure.InvalidPath)
        {
            Error = error;
            Failure = failure;
        }
        internal PlayerGrowthPreview(PlayerGrowthSystem owner, string player, PlayerGrowthState before,
            PlayerGrowthState result, SkillBoardState board, CombatActor actor, CombatActor.ActorEffectPreparation effects)
        {
            Owner = owner;
            PlayerId = player;
            Before = before;
            Result = result;
            Board = board;
            Actor = actor;
            Effects = effects;
        }
    }

    public sealed class PlayerGrowthSystem : AbstractSystem
    {
        public const string AttributeSource = "player_growth";
        IUnRegister _removed;
        bool _disposed;

        protected override void OnInit()
        {
            _removed = this.RegisterEvent<ActorUnregisteredEvent>(e =>
            {
                PlayerGrowthModel model = this.GetModel<PlayerGrowthModel>();
                if (SkillBoardPlayerIdentity.GetProfileId(e.Actor) != model.PlayerId) { return; }
                model.PlayerId = string.Empty;
                model.State = new PlayerGrowthState();
                if (e.Actor != null) { e.Actor.SetModifierSource(AttributeSource, Array.Empty<ModifierInstance>()); }
            });
        }
        protected override void OnDeinit()
        {
            _disposed = true;
            _removed?.UnRegister();
        }

        internal static GrowthRules ConfiguredRules(CombatActor actor)
        {
            PlayerController player = actor != null ? actor.GetComponent<PlayerController>() : null;
            return player?.Definition?.Growth != null ? player.Definition.Growth.Rules : GrowthRules.Default;
        }

        internal bool RulesAreCurrent(PlayerGrowthState state)
        {
            CombatActor actor = GetAttributeDetailsQuery.FindPlayer(this.GetModel<CombatModel>());
            return !_disposed && state.Rules.Equals(ConfiguredRules(actor));
        }

        internal void Configure(CombatActor actor, PlayerGrowthConfiguration configuration)
        {
            if (actor == null || actor.Team != ActorTeam.Player) { throw new ArgumentException("成长配置需要玩家"); }
            PlayerGrowthModel model = this.GetModel<PlayerGrowthModel>();
            string playerId = SkillBoardPlayerIdentity.GetProfileId(actor);
            if (!string.IsNullOrEmpty(model.PlayerId) && model.PlayerId != playerId) { throw new ArgumentException("成长属于其他玩家"); }
            GrowthRules rules = configuration != null ? configuration.Rules : GrowthRules.Default;
            if (!rules.IsValid) { throw new ArgumentException("成长配置非法"); }
            if (string.IsNullOrEmpty(model.PlayerId)) { model.State = new PlayerGrowthState(rules); }
            if (!model.State.Rules.Equals(rules)) { throw new ArgumentException("成长成本合同已改变，需要迁移"); }
            model.PlayerId = playerId;
            SkillBoardEffects.Prepare(actor, this.GetModel<SkillBoardModel>().State, model.State).Apply();
        }

        public void Restore(string playerId, PlayerGrowthState state, CombatActor actor)
        {
            string error = PlayerGrowthGraph.Validate(state, this.GetModel<SkillBoardModel>().State);
            if (error != null || !state.Rules.Equals(ConfiguredRules(actor))
                || SkillBoardPlayerIdentity.GetProfileId(actor) != playerId)
            {
                throw new ArgumentException(error ?? "成长配置或玩家身份不匹配");
            }
            CombatActor.ActorEffectPreparation effects = SkillBoardEffects.Prepare(actor, this.GetModel<SkillBoardModel>().State, state);
            effects.Apply();
            PlayerGrowthModel model = this.GetModel<PlayerGrowthModel>();
            model.PlayerId = playerId;
            model.State = state;
        }

        public PlayerGrowthPreview Preview(string playerId, string statId, int count, string woodId, string nodeId)
        {
            PlayerGrowthModel model = this.GetModel<PlayerGrowthModel>();
            if (_disposed || !this.GetSystem<SkillBoardSystem>().IsCurrentPlayer(playerId)
                || model.PlayerId.Length > 0 && model.PlayerId != playerId || !RulesAreCurrent(model.State))
            {
                return new PlayerGrowthPreview("成长玩家、Session 或成本配置无效", SkillBoardFailure.InvalidOwner);
            }
            if (statId != null && (count <= 0 || model.State.AttributePoints < count)
                || statId == null && model.State.TalentPoints < (string.IsNullOrEmpty(nodeId) ? model.State.Rules.FirewoodCost : model.State.Rules.TalentCost))
            { return new PlayerGrowthPreview("可用点数不足", SkillBoardFailure.InsufficientPoints); }
            if (statId == null && model.State.IsActive(woodId, nodeId))
            { return new PlayerGrowthPreview("已经点亮", SkillBoardFailure.AlreadyActive); }
            SkillBoardState board = this.GetModel<SkillBoardModel>().State;
            CombatActor actor = GetAttributeDetailsQuery.FindPlayer(this.GetModel<CombatModel>());
            try
            {
                PlayerGrowthState result = statId != null ? model.State.Allocate(statId, count)
                    : model.State.Activate(woodId, nodeId);
                string error = PlayerGrowthGraph.Validate(result, board);
                if (error != null) { return new PlayerGrowthPreview(error); }
                CombatActor.ActorEffectPreparation effects = SkillBoardEffects.Prepare(actor, board, result);
                return new PlayerGrowthPreview(this, playerId, model.State, result, board, actor, effects);
            }
            catch (ArgumentException error) { return new PlayerGrowthPreview(error.Message); }
            catch (OverflowException) { return new PlayerGrowthPreview("成长数值超出范围", SkillBoardFailure.Overflow); }
        }

        public bool Commit(PlayerGrowthPreview preview, out string error)
        {
            error = "成长预览已失效，请重新预检";
            PlayerGrowthModel model = this.GetModel<PlayerGrowthModel>();
            if (_disposed || preview == null || !preview.Success || preview.Owner != this || preview.Consumed
                || !this.GetSystem<SkillBoardSystem>().IsCurrentPlayer(preview.PlayerId)
                || !ReferenceEquals(model.State, preview.Before)
                || !ReferenceEquals(this.GetModel<SkillBoardModel>().State, preview.Board)
                || !RulesAreCurrent(preview.Before) || preview.Effects != null && !preview.Effects.IsCurrent)
            {
                return false;
            }
            preview.Consumed = true;
            preview.Effects?.Apply();
            model.PlayerId = preview.PlayerId;
            model.State = preview.Result;
            try
            {
                if (preview.Effects != null)
                {
                    this.GetSystem<CombatSystem>().PublishResourceChanges(preview.Actor, preview.Effects.Before, ActorResourceChangeReason.Configure);
                }
            }
            finally { this.SendEvent(new PlayerGrowthChangedEvent(model.PlayerId)); }
            error = null;
            return true;
        }
    }

    public sealed class ConfigurePlayerGrowthCommand : AbstractCommand
    {
        readonly CombatActor _actor;
        readonly PlayerGrowthConfiguration _configuration;
        public ConfigurePlayerGrowthCommand(CombatActor actor, PlayerGrowthConfiguration configuration)
        {
            _actor = actor;
            _configuration = configuration;
        }
        protected override void OnExecute()
        {
            this.GetSystem<PlayerGrowthSystem>().Configure(_actor, _configuration);
        }
    }

    public sealed class PreviewPlayerGrowthQuery : AbstractQuery<PlayerGrowthPreview>
    {
        readonly string _playerId;
        readonly string _statId;
        readonly int _count;
        readonly string _woodId;
        readonly string _nodeId;
        public PreviewPlayerGrowthQuery(string playerId, string statId = null, int count = 1, string woodId = null, string nodeId = "")
        {
            _playerId = playerId;
            _statId = statId;
            _count = count;
            _woodId = woodId;
            _nodeId = nodeId;
        }
        protected override PlayerGrowthPreview OnDo()
        {
            return this.GetSystem<PlayerGrowthSystem>().Preview(_playerId, _statId, _count, _woodId, _nodeId);
        }
    }

    public sealed class CommitPlayerGrowthCommand : AbstractCommand<bool>
    {
        readonly PlayerGrowthPreview _preview;
        public string Error { get; private set; }
        public CommitPlayerGrowthCommand(PlayerGrowthPreview preview)
        {
            _preview = preview;
        }
        protected override bool OnExecute()
        {
            bool success = this.GetSystem<PlayerGrowthSystem>().Commit(_preview, out string error);
            Error = error;
            return success;
        }
    }

    public sealed class GetPlayerGrowthQuery : AbstractQuery<PlayerGrowthState>
    {
        protected override PlayerGrowthState OnDo()
        {
            return this.GetModel<PlayerGrowthModel>().State;
        }
    }
}
