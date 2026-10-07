using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class InstalledFirewood
    {
        public ItemInstance Item { get; }
        public FirewoodPlacement Placement { get; }
        public InstalledFirewood(ItemInstance item, HexCoordinate anchor, int rotation)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Placement = new FirewoodPlacement(item.InstanceId,
                item.SkillBoardState?.Layout ?? throw new ArgumentException("道具没有柴薪布局"), anchor, rotation);
        }
    }

    public sealed class SkillBoardEmbedding
    {
        public FirewoodPortReference Slot { get; }
        public ItemInstance Item { get; }
        public SkillBoardEmbedding(FirewoodPortReference slot, ItemInstance item)
        {
            Slot = slot;
            Item = item ?? throw new ArgumentNullException(nameof(item));
        }
    }

    public sealed class SkillBoardState
    {
        public IReadOnlyList<InstalledFirewood> Firewoods { get; }
        public IReadOnlyList<FirewoodConnection> Connections { get; }
        public IReadOnlyList<SkillBoardEmbedding> Embeddings { get; }
        public ItemInstance EnergyBottle { get; }
        public IEnumerable<ItemInstance> Items => Firewoods.Select(w => w.Item)
            .Concat(Embeddings.Select(e => e.Item)).Concat(EnergyBottle == null ? Array.Empty<ItemInstance>() : new[] { EnergyBottle });

        public SkillBoardState(IEnumerable<InstalledFirewood> firewoods = null, IEnumerable<FirewoodConnection> connections = null,
            IEnumerable<SkillBoardEmbedding> embeddings = null, ItemInstance energyBottle = null)
        {
            Firewoods = Array.AsReadOnly((firewoods ?? Array.Empty<InstalledFirewood>()).ToArray());
            Connections = Array.AsReadOnly((connections ?? Array.Empty<FirewoodConnection>()).ToArray());
            Embeddings = Array.AsReadOnly((embeddings ?? Array.Empty<SkillBoardEmbedding>()).ToArray());
            EnergyBottle = energyBottle;
        }

        public string Validate()
        {
            if (Firewoods.Any(w => w == null) || Embeddings.Any(e => e == null)) { return "棋盘记录为空"; }
            var ids = new HashSet<ItemInstanceId>();
            foreach (ItemInstance item in Items)
            {
                if (item == null || !item.Id.IsCanonical || !ids.Add(item.Id)
                    || item.SkillBoardState == null || !item.SkillBoardState.IsValidFor(item.BaseDefinition))
                {
                    return "棋盘物品身份重复或实例不合法";
                }
            }
            if (Firewoods.Any(w => w.Item.BaseDefinition.ItemType != ItemType.Firewood)) { return "柴薪角色错误"; }
            if (EnergyBottle != null && EnergyBottle.BaseDefinition.ItemType != ItemType.EnergyBottle) { return "专栏只接受能量瓶"; }
            var placements = Firewoods.Select(w => w.Placement).ToArray();
            SkillBoardPlacementResult geometry = SkillBoardPlacementValidator.Validate(placements, Connections, true);
            if (!geometry.Success) { return geometry.Issues[0].ToString(); }
            var ports = new HashSet<FirewoodPortReference>(Connections.SelectMany(c => new[] { c.First, c.Second }));
            var slots = new HashSet<FirewoodPortReference>();
            foreach (SkillBoardEmbedding embed in Embeddings)
            {
                InstalledFirewood wood = Firewoods.FirstOrDefault(w => w.Placement.Id == embed.Slot.PlacementId);
                if (wood == null || !slots.Add(embed.Slot)) { return "嵌入插槽不存在或重复"; }
                if (embed.Slot.NodeId == SkillBoardGeometry.CenterNodeId)
                {
                    if (wood.Placement.Layout.Size != FirewoodSize.Large || embed.Item.BaseDefinition.ItemType != ItemType.Flame)
                    {
                        return "只有大柴薪中心可以安装火焰";
                    }
                }
                else if (!wood.Placement.Layout.Nodes.Any(n => n.Id == embed.Slot.NodeId && n.Kind == FirewoodNodeKind.Crack)
                    || embed.Item.BaseDefinition.ItemType != ItemType.Ember || ports.Contains(embed.Slot))
                {
                    return "余烬只能安装在未连接的裂缝";
                }
            }
            return SkillBoardRootConnectivity.IsRooted(placements, Connections) ? null
                : "存在未连接初始空洞的柴薪；拆卸需要保证剩余网络仍连根";
        }
    }

    public sealed class SkillBoardModel : AbstractModel
    {
        public string PlayerId { get; private set; } = string.Empty;
        public SkillBoardState State { get; private set; } = new SkillBoardState();
        public int Revision { get; private set; }
        protected override void OnInit() { }
        internal void Replace(string actorId, SkillBoardState state)
        {
            PlayerId = actorId;
            State = state;
            Revision++;
        }
        public void RestoreState(string actorId, SkillBoardState state)
        {
            if (!ContentId.IsValidSegment(actorId) || state == null || state.Validate() != null)
            {
                throw new ArgumentException("棋盘恢复状态不合法");
            }
            Replace(actorId, state);
        }
    }

    public readonly struct SkillBoardChangedEvent
    {
        public string ActorId { get; }
        public int Revision { get; }
        public SkillBoardChangedEvent(string actorId, int revision) { ActorId = actorId; Revision = revision; }
    }
}
