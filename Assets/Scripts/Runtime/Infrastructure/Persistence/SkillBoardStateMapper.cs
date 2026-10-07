using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static partial class RuntimeStateMapper
    {
        static SkillBoardItemDto MapSkillBoardItem(ItemInstance item, ContentCatalog catalog, List<DtoMapIssue> issues)
        {
            SkillBoardItemState state = item.SkillBoardState;
            if (!item.BaseDefinition.IsSkillBoardItem)
            {
                if (state != null) { Add(issues, DtoMapIssueCode.InvalidValue, "item.skillBoard", "普通道具含棋盘状态"); }
                return null;
            }
            if (state == null || !state.IsValidFor(item.BaseDefinition))
            { Add(issues, DtoMapIssueCode.InvalidValue, "item.skillBoard", "缺少合法实际变体"); return null; }
            var dto = new SkillBoardItemDto
            {
                EmberRadius = state.EmberRadius,
                Energy = state.Energy,
                FlameSkillContentId = state.FlameSkill == null ? string.Empty
                    : GetContentId(state.FlameSkill, catalog, "item.skillBoard.flameSkill", issues)
            };
            if (state.Layout != null)
            {
                dto.Layout = new FirewoodLayoutDto
                {
                    Size = state.Layout.Size, Seed = state.Layout.Seed, GenerationVersion = state.Layout.GenerationVersion,
                    Nodes = state.Layout.Nodes.Select(n => new FirewoodNodeDto
                    { Id = n.Id, Q = n.Position.Q, R = n.Position.R, Kind = n.Kind, OutwardQ = n.Outward?.Q, OutwardR = n.Outward?.R }).ToList(),
                    Edges = state.Layout.Edges.Select(e => new FirewoodEdgeDto { First = e.First, Second = e.Second }).ToList()
                };
            }
            foreach (SkillBoardTalentState talent in state.Talents)
            {
                dto.Talents.Add(new SkillBoardTalentDto { NodeId = talent.NodeId, Radius = talent.Radius,
                    Modifiers = MapModifiersToDto(talent.Modifiers, catalog, "item.skillBoard.talents", issues) });
            }
            return dto;
        }

        static void RestoreSkillBoardItem(ItemInstance item, SkillBoardItemDto dto, ContentCatalog catalog,
            string path, List<DtoMapIssue> issues)
        {
            if (!item.BaseDefinition.IsSkillBoardItem)
            {
                if (dto != null) { Add(issues, DtoMapIssueCode.InvalidValue, path, "普通道具含棋盘状态"); }
                return;
            }
            if (dto == null || dto.Talents == null || dto.Talents.Count > 15 || dto.Talents.Any(t => t == null))
            { Add(issues, DtoMapIssueCode.InvalidValue, path, "缺少实际棋盘道具状态或节点非法"); return; }
            FirewoodLayout layout = null;
            if (dto.Layout != null)
            {
                FirewoodLayoutDto l = dto.Layout;
                if (l.Nodes == null || l.Edges == null || l.Nodes.Count > 15 || l.Edges.Count > 120
                    || l.Nodes.Any(n => n == null || n.OutwardQ.HasValue != n.OutwardR.HasValue) || l.Edges.Any(e => e == null))
                { Add(issues, DtoMapIssueCode.InvalidValue, path, "布局节点或连线非法"); return; }
                try
                {
                    layout = new FirewoodLayout(l.Size, l.Seed, l.GenerationVersion,
                        l.Nodes.Select(n => new FirewoodNode(n.Id, new HexCoordinate(n.Q, n.R), n.Kind,
                            n.OutwardQ.HasValue ? new HexCoordinate(n.OutwardQ.Value, n.OutwardR.Value) : (HexCoordinate?)null)),
                        l.Edges.Select(e => new FirewoodEdge(e.First, e.Second)));
                }
                catch (OverflowException)
                { Add(issues, DtoMapIssueCode.InvalidValue, path, "布局坐标超出表示范围"); return; }
            }
            ProjectileSkillDefinition skill = string.IsNullOrEmpty(dto.FlameSkillContentId) ? null
                : Resolve<ProjectileSkillDefinition>(dto.FlameSkillContentId, catalog, path + ".flameSkill", issues);
            var talents = dto.Talents.Select(t => new SkillBoardTalentState(t.NodeId, t.Radius,
                RestoreModifiers(t.Modifiers, catalog, path + ".talents", issues))).ToArray();
            var state = new SkillBoardItemState(layout, talents, skill, dto.EmberRadius, dto.Energy);
            if (!state.IsValidFor(item.BaseDefinition))
            { Add(issues, DtoMapIssueCode.InvalidValue, path, "实际布局、角色、技能、范围或瓶内能量非法"); return; }
            item.InitializeSkillBoardState(state);
        }

        public static SkillBoardSaveDto ToDto(string actorId, SkillBoardState state)
        {
            return new SkillBoardSaveDto
            {
                ActorInstanceId = actorId,
                Firewoods = state.Firewoods.Select(w => new InstalledFirewoodDto
                { ItemInstanceId = w.Item.InstanceId, Q = w.Placement.Anchor.Q, R = w.Placement.Anchor.R, Rotation = w.Placement.Rotation }).ToList(),
                Embeddings = state.Embeddings.Select(e => new SkillBoardEmbeddingDto
                { FirewoodInstanceId = e.Slot.PlacementId, NodeId = e.Slot.NodeId, ItemInstanceId = e.Item.InstanceId }).ToList(),
                Connections = state.Connections.Select(c => new FirewoodConnectionDto
                { FirstFirewoodId = c.First.PlacementId, FirstNodeId = c.First.NodeId,
                    SecondFirewoodId = c.Second.PlacementId, SecondNodeId = c.Second.NodeId }).ToList(),
                EnergyBottleInstanceId = state.EnergyBottle?.InstanceId ?? string.Empty
            };
        }

        static SkillBoardState RestoreSkillBoard(SkillBoardSaveDto dto, IReadOnlyDictionary<ItemInstanceId, ItemInstance> items,
            HashSet<string> players, ISet<ItemInstanceId> owned, List<DtoMapIssue> issues)
        {
            if (dto == null || dto.Firewoods == null || dto.Embeddings == null || dto.Connections == null
                || dto.Firewoods.Any(w => w == null) || dto.Embeddings.Any(e => e == null) || dto.Connections.Any(c => c == null))
            { Add(issues, DtoMapIssueCode.InvalidValue, "skillBoard", "棋盘记录缺失"); return new SkillBoardState(); }
            if (!players.Contains(dto.ActorInstanceId) && (!string.IsNullOrEmpty(dto.ActorInstanceId)
                || dto.Firewoods.Count > 0 || dto.Embeddings.Count > 0 || dto.Connections.Count > 0 || !string.IsNullOrEmpty(dto.EnergyBottleInstanceId)))
            { Add(issues, DtoMapIssueCode.DanglingReference, "skillBoard.actor", "棋盘玩家不存在"); }
            var woods = new List<InstalledFirewood>();
            foreach (InstalledFirewoodDto w in dto.Firewoods)
            {
                ItemInstance item = ClaimBoardItem(w.ItemInstanceId, items, owned, issues);
                if (item?.BaseDefinition.ItemType != ItemType.Firewood || item.SkillBoardState?.Layout == null
                    || w.Rotation < 0 || w.Rotation > 5)
                { Add(issues, DtoMapIssueCode.InvalidValue, "skillBoard.firewoods", "柴薪角色、布局或旋转非法"); continue; }
                try { woods.Add(new InstalledFirewood(item, new HexCoordinate(w.Q, w.R), w.Rotation)); }
                catch (OverflowException) { Add(issues, DtoMapIssueCode.InvalidValue, "skillBoard.firewoods", "柴薪锚点超出表示范围"); }
            }
            var embeddings = new List<SkillBoardEmbedding>();
            foreach (SkillBoardEmbeddingDto e in dto.Embeddings)
            {
                ItemInstance item = ClaimBoardItem(e.ItemInstanceId, items, owned, issues);
                if (item != null) { embeddings.Add(new SkillBoardEmbedding(new FirewoodPortReference(e.FirewoodInstanceId, e.NodeId), item)); }
            }
            ItemInstance bottle = string.IsNullOrEmpty(dto.EnergyBottleInstanceId) ? null
                : ClaimBoardItem(dto.EnergyBottleInstanceId, items, owned, issues);
            var state = new SkillBoardState(woods, dto.Connections.Select(c => new FirewoodConnection(
                new FirewoodPortReference(c.FirstFirewoodId, c.FirstNodeId), new FirewoodPortReference(c.SecondFirewoodId, c.SecondNodeId))), embeddings, bottle);
            string error = state.Validate();
            if (error != null) { Add(issues, DtoMapIssueCode.InvalidValue, "skillBoard", error); }
            return state;
        }

        static ItemInstance ClaimBoardItem(string reference, IReadOnlyDictionary<ItemInstanceId, ItemInstance> items,
            ISet<ItemInstanceId> owned, List<DtoMapIssue> issues)
        {
            if (!ItemInstanceId.TryParse(reference, out ItemInstanceId id) || !items.TryGetValue(id, out ItemInstance item))
            { Add(issues, DtoMapIssueCode.DanglingReference, "skillBoard.item", "道具不存在：" + reference); return null; }
            if (!owned.Add(id)) { Add(issues, DtoMapIssueCode.OwnershipConflict, "skillBoard.item", "重复所有者：" + reference); }
            return item;
        }
    }
}
