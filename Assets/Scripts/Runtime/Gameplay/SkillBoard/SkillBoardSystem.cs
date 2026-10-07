using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkFlare
{
    public enum SkillBoardOperation { Install, Move, Remove, Embed, Extract, EquipBottle, UnequipBottle }

    public sealed class SkillBoardRequest
    {
        public SkillBoardOperation Operation { get; }
        public ItemInstance Item { get; }
        public HexCoordinate Anchor { get; }
        public int Rotation { get; }
        public FirewoodPortReference Slot { get; }
        public IReadOnlyList<FirewoodConnection> Connections { get; }
        public SkillBoardRequest(SkillBoardOperation operation, ItemInstance item = null, HexCoordinate anchor = default,
            int rotation = 0, FirewoodPortReference slot = default, IEnumerable<FirewoodConnection> connections = null)
        {
            Operation = operation;
            Item = item;
            Anchor = anchor;
            Rotation = rotation;
            Slot = slot;
            Connections = connections == null ? null : Array.AsReadOnly(connections.ToArray());
        }
    }

    public sealed class SkillBoardPreview
    {
        public bool Success => Error == null;
        public string Error { get; }
        internal SkillBoardSystem Owner { get; }
        internal string ActorId { get; }
        internal SkillBoardState Before { get; }
        internal SkillBoardState After { get; }
        internal InventoryGrid SourceGrid { get; }
        internal InventoryGrid FinalGrid { get; }
        internal IReadOnlyDictionary<ItemInstance, RectInt> Placements { get; }
        internal IReadOnlyDictionary<ItemInstance, int> Revisions { get; }
        internal IReadOnlyDictionary<ItemInstance, (int Quality, float Durability)> MutableValues { get; }
        internal ItemInstance Incoming { get; }
        internal ItemInstance Outgoing { get; }
        internal bool Consumed { get; set; }

        internal SkillBoardPreview(string error) { Error = error; }
        internal SkillBoardPreview(SkillBoardSystem owner, string actorId, SkillBoardState before, SkillBoardState after,
            InventoryGrid source, InventoryGrid final, ItemInstance incoming, ItemInstance outgoing)
        {
            Owner = owner;
            ActorId = actorId;
            Before = before;
            After = after;
            SourceGrid = source;
            FinalGrid = final;
            Incoming = incoming;
            Outgoing = outgoing;
            Placements = new Dictionary<ItemInstance, RectInt>(source.Placements);
            Revisions = before.Items.Concat(source.Placements.Keys).Distinct().ToDictionary(i => i, i => i.Revision);
            MutableValues = Revisions.Keys.ToDictionary(i => i, i => (i.Quality, i.Durability));
        }
    }

    public sealed class SkillBoardSystem : AbstractSystem
    {
        bool _disposed;
        IUnRegister _actorRemoved;
        protected override void OnInit()
        {
            _actorRemoved = this.RegisterEvent<ActorUnregisteredEvent>(e =>
            {
                SkillBoardModel model = this.GetModel<SkillBoardModel>();
                if (SkillBoardPlayerIdentity.GetProfileId(e.Actor) == model.PlayerId) { model.Replace(string.Empty, new SkillBoardState()); }
            });
        }
        protected override void OnDeinit() { _disposed = true; _actorRemoved?.UnRegister(); }

        bool IsCurrentPlayer(string actorId)
        {
            if (_disposed) { return false; }
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host) && host.CurrentSession != null
                && (host.CurrentSession.Architecture.GetSystem<SkillBoardSystem>() != this
                    || host.CurrentSession.State != GameSessionState.Running
                    || !host.CurrentSession.SceneScope.CanAcceptWork)) { return false; }
            IReadOnlyList<CombatActor> players = this.GetModel<CombatModel>().GetActorsByTeam(ActorTeam.Player);
            return players.Count == 1 && SkillBoardPlayerIdentity.GetProfileId(players[0]) == actorId && ContentId.IsValidSegment(actorId);
        }

        public SkillBoardPreview Preview(string actorId, SkillBoardRequest request)
        {
            if (!IsCurrentPlayer(actorId) || request == null) { return new SkillBoardPreview("当前玩家身份无效"); }
            SkillBoardModel model = this.GetModel<SkillBoardModel>();
            if (model.PlayerId.Length > 0 && model.PlayerId != actorId) { return new SkillBoardPreview("棋盘属于其他玩家"); }
            SkillBoardState before = model.State;
            var woods = before.Firewoods.ToList();
            var embeddings = before.Embeddings.ToList();
            IReadOnlyList<FirewoodConnection> connections = request.Operation == SkillBoardOperation.Install || request.Operation == SkillBoardOperation.Move
                ? request.Connections ?? before.Connections : before.Connections;
            ItemInstance bottle = before.EnergyBottle;
            ItemInstance incoming = null;
            ItemInstance outgoing = null;
            ItemInstance item = request.Item;
            InstalledFirewood wood = woods.FirstOrDefault(w => ReferenceEquals(w.Item, item));
            switch (request.Operation)
            {
                case SkillBoardOperation.Install:
                    if (item?.BaseDefinition?.ItemType != ItemType.Firewood || item.SkillBoardState?.Layout == null)
                    { return new SkillBoardPreview("安装需要柴薪"); }
                    if (request.Rotation < 0 || request.Rotation > 5) { return new SkillBoardPreview("旋转必须为 0–5"); }
                    woods.Add(new InstalledFirewood(item, request.Anchor, request.Rotation));
                    incoming = item;
                    break;
                case SkillBoardOperation.Move:
                    if (wood == null || request.Rotation < 0 || request.Rotation > 5) { return new SkillBoardPreview("柴薪或旋转无效"); }
                    woods.Remove(wood);
                    woods.Add(new InstalledFirewood(item, request.Anchor, request.Rotation));
                    break;
                case SkillBoardOperation.Remove:
                    if (wood == null) { return new SkillBoardPreview("柴薪不在棋盘上"); }
                    if (embeddings.Any(e => e.Slot.PlacementId == item.InstanceId))
                    { return new SkillBoardPreview("请先逐件取出火焰和余烬"); }
                    woods.Remove(wood);
                    // 单件拆卸同时移除它的连接，其余连接保持。
                    connections = before.Connections.Where(c => c.First.PlacementId != item.InstanceId
                        && c.Second.PlacementId != item.InstanceId).ToArray();
                    outgoing = item;
                    break;
                case SkillBoardOperation.Embed:
                    if (item?.BaseDefinition?.ItemType != ItemType.Flame && item?.BaseDefinition?.ItemType != ItemType.Ember)
                    { return new SkillBoardPreview("只能嵌入火焰或余烬"); }
                    embeddings.Add(new SkillBoardEmbedding(request.Slot, item));
                    incoming = item;
                    break;
                case SkillBoardOperation.Extract:
                    SkillBoardEmbedding embedding = embeddings.FirstOrDefault(e => e.Slot.Equals(request.Slot));
                    if (embedding == null) { return new SkillBoardPreview("插槽为空"); }
                    embeddings.Remove(embedding);
                    outgoing = embedding.Item;
                    break;
                case SkillBoardOperation.EquipBottle:
                    if (item?.BaseDefinition?.ItemType != ItemType.EnergyBottle) { return new SkillBoardPreview("专栏只接受能量瓶"); }
                    incoming = item;
                    outgoing = bottle;
                    bottle = item;
                    break;
                case SkillBoardOperation.UnequipBottle:
                    if (bottle == null) { return new SkillBoardPreview("专栏为空"); }
                    outgoing = bottle;
                    bottle = null;
                    break;
                default: return new SkillBoardPreview("未知棋盘操作");
            }
            var after = new SkillBoardState(woods, connections, embeddings, bottle);
            string error = after.Validate();
            if (error != null) { return new SkillBoardPreview(error); }
            InventoryGrid grid = this.GetModel<InventoryModel>().Grid;
            if (incoming != null && (!grid.Placements.ContainsKey(incoming)
                || before.Items.Any(i => i.Id == incoming.Id))) { return new SkillBoardPreview("源道具不属于背包"); }
            var final = new InventoryGrid(grid.Width, grid.Height);
            foreach (KeyValuePair<ItemInstance, RectInt> pair in grid.Placements)
            {
                if (ReferenceEquals(pair.Key, incoming)) { continue; }
                if (after.Items.Any(i => i.Id == pair.Key.Id) || !final.TryAddAt(pair.Key, pair.Value.position))
                { return new SkillBoardPreview("背包所有权或位置不合法"); }
            }
            if (outgoing != null)
            {
                bool added = incoming != null ? final.TryAddAt(outgoing, grid.Placements[incoming].position) : final.TryAdd(outgoing);
                if (!added) { return new SkillBoardPreview("背包没有足够空间取回道具"); }
            }
            return new SkillBoardPreview(this, actorId, before, after, grid, final, incoming, outgoing);
        }

        public bool Commit(SkillBoardPreview preview, out string error)
        {
            error = "预览已失效，请重新预检";
            SkillBoardModel model = this.GetModel<SkillBoardModel>();
            InventoryModel inventory = this.GetModel<InventoryModel>();
            if (preview == null || !preview.Success || preview.Owner != this || preview.Consumed
                || !IsCurrentPlayer(preview.ActorId) || !ReferenceEquals(model.State, preview.Before)
                || !ReferenceEquals(inventory.Grid, preview.SourceGrid)
                || inventory.Grid.Placements.Count != preview.Placements.Count
                || preview.Placements.Any(p => !inventory.Grid.Placements.TryGetValue(p.Key, out RectInt rect) || rect != p.Value)
                || preview.Revisions.Any(p => p.Key.Revision != p.Value)
                || preview.MutableValues.Any(p => p.Key.Quality != p.Value.Quality || p.Key.Durability != p.Value.Durability)) { return false; }
            preview.Consumed = true;
            inventory.ReplaceGridWithoutEvents(preview.FinalGrid);
            model.Replace(preview.ActorId, preview.After);
            // 所有权已一次性提交，事件接收者只会观察到一致最终态。
            if (preview.Incoming != null) { inventory.NotifyItemChanged(preview.Incoming, InventoryChangeType.Removed); }
            if (preview.Outgoing != null) { inventory.NotifyItemChanged(preview.Outgoing, InventoryChangeType.Added); }
            this.SendEvent(new SkillBoardChangedEvent(model.PlayerId, model.Revision));
            error = null;
            return true;
        }
    }

    public sealed class PreviewSkillBoardQuery : AbstractQuery<SkillBoardPreview>
    {
        readonly string _actorId;
        readonly SkillBoardRequest _request;
        public PreviewSkillBoardQuery(string actorId, SkillBoardRequest request) { _actorId = actorId; _request = request; }
        protected override SkillBoardPreview OnDo() { return this.GetSystem<SkillBoardSystem>().Preview(_actorId, _request); }
    }

    public sealed class CommitSkillBoardCommand : AbstractCommand<bool>
    {
        readonly SkillBoardPreview _preview;
        public string Error { get; private set; }
        public CommitSkillBoardCommand(SkillBoardPreview preview) { _preview = preview; }
        protected override bool OnExecute()
        {
            bool success = this.GetSystem<SkillBoardSystem>().Commit(_preview, out string error);
            Error = error;
            return success;
        }
    }

    public sealed class GetSkillBoardQuery : AbstractQuery<SkillBoardState>
    {
        protected override SkillBoardState OnDo() { return this.GetModel<SkillBoardModel>().State; }
    }
}
