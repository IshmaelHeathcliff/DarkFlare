using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum MonsterRarity
    {
        [LabelText("普通")] Normal,
        [LabelText("魔法")] Magic,
        [LabelText("稀有")] Rare,
        [LabelText("独特")] Unique
    }

    public static class MonsterEnergyReward
    {
        public static int Calculate(int baseEnergy, MonsterRarity rarity, int perAffix, int perAffixRarity,
            IEnumerable<MonsterRarity> affixes)
        {
            if (baseEnergy < 1 || perAffix < 1 || perAffixRarity < 1 || !Enum.IsDefined(typeof(MonsterRarity), rarity)
                || affixes == null) { throw new ArgumentException("击杀能量配置不合法"); }
            int result = checked(baseEnergy * (1 + (int)rarity));
            foreach (MonsterRarity affix in affixes)
            {
                if (!Enum.IsDefined(typeof(MonsterRarity), affix)) { throw new ArgumentException("词条稀有度不合法"); }
                result = checked(result + perAffix + (int)affix * perAffixRarity);
            }
            return result;
        }
        public static int Calculate(MonsterDefinition definition, MonsterInstanceData instance)
        {
            if (definition == null || instance == null || instance.Affixes.Any(a => a?.Definition == null))
            { throw new ArgumentException("怪物定义或实际词条缺失"); }
            return Calculate(definition.BaseEnergy, definition.Rarity, definition.EnergyPerAffix,
                definition.EnergyPerAffixRarity, instance.Affixes.Select(a => a.Definition.Rarity));
        }
    }

    public readonly struct EnergyCollectedEvent
    {
        public string ActorId { get; }
        public ItemInstance Bottle { get; }
        public MonsterInstanceId MonsterId { get; }
        public int Reward { get; }
        public int Collected { get; }
        public int Discarded => Reward - Collected;
        public EnergyCollectedEvent(string actorId, ItemInstance bottle, MonsterInstanceId monsterId, int reward, int collected)
        { ActorId = actorId; Bottle = bottle; MonsterId = monsterId; Reward = reward; Collected = collected; }
    }

    public sealed class EnergyBottleSystem : AbstractSystem
    {
        readonly HashSet<MonsterInstanceId> _settled = new HashSet<MonsterInstanceId>();
        RunId _runId;
        IUnRegister _death;
        protected override void OnInit() { _death = this.RegisterEvent<ActorDiedEvent>(OnActorDied); }
        protected override void OnDeinit() { _death?.UnRegister(); _settled.Clear(); }
        void OnActorDied(ActorDiedEvent e)
        {
            CombatModel combat = this.GetModel<CombatModel>();
            RunId runId = this.GetUtility<IRunInstanceIdGenerator>().RunId;
            if (_runId != runId) { _settled.Clear(); _runId = runId; }
            if (e.Actor == null || e.Actor.IsAlive || e.Actor.Team != ActorTeam.Monster
                || !combat.GetActorsByTeam(ActorTeam.Monster).Contains(e.Actor)) { return; }
            MonsterController controller = e.Actor.GetComponent<MonsterController>();
            if (controller?.Instance == null || controller.Instance.Id.RunId != runId) { return; }
            if (!_settled.Add(controller.Instance.Id)) { return; }
            if (e.Source == null || e.Source.Team != ActorTeam.Player) { return; }
            IReadOnlyList<CombatActor> players = combat.GetActorsByTeam(ActorTeam.Player);
            if (players.Count != 1 || (players[0].ActorId != e.Source.ActorKey && players[0].CombatIdentity != e.Source.ActorKey)) { return; }
            // 无瓶或满瓶时也结算本次死亡；之后换瓶不能补收已经舍弃的收益。
            SkillBoardModel board = this.GetModel<SkillBoardModel>();
            if (board.PlayerId != SkillBoardPlayerIdentity.GetProfileId(players[0]) || board.State.EnergyBottle == null) { return; }
            int reward;
            try { reward = MonsterEnergyReward.Calculate(controller.Definition, controller.Instance); }
            catch (Exception error) when (error is ArgumentException || error is OverflowException)
            {
                ApplicationLog.Warning(LogEventIds.DataValidation, "怪物击杀能量配置无效：" + error.Message);
                return;
            }
            ItemInstance bottle = board.State.EnergyBottle;
            int collected = bottle.CollectEnergy(reward);
            if (collected > 0)
            {
                this.SendEvent(new EnergyCollectedEvent(board.PlayerId, bottle, controller.Instance.Id, reward, collected));
            }
        }
    }
}
