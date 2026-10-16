using UnityEngine;

namespace DarkFlare
{
    public sealed class EnergyBottleSelection
    {
        public ItemInstance Item { get; }
        internal SkillBoardSystem Owner { get; }
        internal string PlayerId { get; }
        internal bool Equipped { get; }
        internal InventoryGrid Grid { get; }
        internal long GridRevision { get; }
        internal RectInt Placement { get; }
        internal EnergyBottleSelection(SkillBoardSystem owner, string playerId, ItemInstance item, bool equipped,
            InventoryGrid grid, RectInt placement)
        {
            Owner = owner;
            PlayerId = playerId;
            Item = item;
            Equipped = equipped;
            Grid = grid;
            GridRevision = grid.Revision;
            Placement = placement;
        }
    }

    public sealed partial class SkillBoardSystem
    {
        public EnergyBottleSelection SelectBottle(string playerId, ItemInstance item)
        {
            if (!IsCurrentPlayer(playerId) || item?.BaseDefinition.ItemType != ItemType.EnergyBottle) { return null; }
            SkillBoardState board = this.GetModel<SkillBoardModel>().State;
            InventoryGrid grid = this.GetModel<InventoryModel>().Grid;
            bool equipped = ReferenceEquals(board.EnergyBottle, item);
            bool inBag = grid.Placements.TryGetValue(item, out RectInt placement);
            if (equipped == inBag) { return null; }
            return new EnergyBottleSelection(this, playerId, item, equipped, grid, placement);
        }

        public bool IsSelectionCurrent(EnergyBottleSelection selection)
        {
            if (selection == null || selection.Owner != this || !IsCurrentPlayer(selection.PlayerId)) { return false; }
            SkillBoardState board = this.GetModel<SkillBoardModel>().State;
            InventoryGrid grid = this.GetModel<InventoryModel>().Grid;
            return selection.Equipped
                ? ReferenceEquals(board.EnergyBottle, selection.Item) && !grid.Placements.ContainsKey(selection.Item)
                : ReferenceEquals(grid, selection.Grid) && grid.Revision == selection.GridRevision
                    && !ReferenceEquals(board.EnergyBottle, selection.Item)
                    && grid.Placements.TryGetValue(selection.Item, out RectInt placement) && placement == selection.Placement;
        }

        public SkillBoardPreview PreviewCharge(EnergyBottleSelection selection)
        {
            return IsSelectionCurrent(selection) ? Preview(selection.PlayerId,
                new SkillBoardRequest(SkillBoardOperation.ConsumeBottle, selection.Item))
                : new SkillBoardPreview("中心选择已失效，请重新选择瓶子", SkillBoardFailure.Stale);
        }
    }

    public sealed class SelectEnergyBottleQuery : AbstractQuery<EnergyBottleSelection>
    {
        readonly string _playerId;
        readonly ItemInstance _item;
        public SelectEnergyBottleQuery(string playerId, ItemInstance item)
        {
            _playerId = playerId;
            _item = item;
        }
        protected override EnergyBottleSelection OnDo()
        {
            return this.GetSystem<SkillBoardSystem>().SelectBottle(_playerId, _item);
        }
    }

    public sealed class PreviewEnergyChargeQuery : AbstractQuery<SkillBoardPreview>
    {
        readonly EnergyBottleSelection _selection;
        public PreviewEnergyChargeQuery(EnergyBottleSelection selection)
        {
            _selection = selection;
        }
        protected override SkillBoardPreview OnDo()
        {
            return this.GetSystem<SkillBoardSystem>().PreviewCharge(_selection);
        }
    }

    public sealed class IsEnergyBottleSelectionCurrentQuery : AbstractQuery<bool>
    {
        readonly EnergyBottleSelection _selection;
        public IsEnergyBottleSelectionCurrentQuery(EnergyBottleSelection selection)
        {
            _selection = selection;
        }
        protected override bool OnDo()
        {
            return this.GetSystem<SkillBoardSystem>().IsSelectionCurrent(_selection);
        }
    }
}
