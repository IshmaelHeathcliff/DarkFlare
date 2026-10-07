using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static partial class SaveDataValidator
    {
        static void ValidateSkillBoardItem(SkillBoardItemDto dto, string path, List<SaveDataIssue> issues)
        {
            if (dto == null) { return; }
            if (dto.Energy < 0 || dto.EmberRadius < 0)
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "能量或范围不能为负"); }
            if (!string.IsNullOrEmpty(dto.FlameSkillContentId))
            { ValidateContentId(dto.FlameSkillContentId, ContentNamespaces.Skill, path + ".flameSkill", issues); }
            FirewoodLayout layout = ReadSavedLayout(dto.Layout, path + ".layout", issues);
            if (dto.Talents == null || dto.Talents.Count > 15)
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "天赋集合非法"); return; }
            var ids = new HashSet<string>();
            foreach (SkillBoardTalentDto talent in dto.Talents)
            {
                if (talent == null || !ids.Add(talent.NodeId) || talent.Radius < 0
                    || talent.Modifiers == null || talent.Modifiers.Count == 0)
                { Add(issues, SaveDataIssueCode.InvalidValue, path, "天赋身份、范围或效果非法"); continue; }
                ValidateModifiers(talent.Modifiers, path + ".talents", issues);
            }
            if (layout != null && !ids.SetEquals(layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent).Select(n => n.Id)))
            { Add(issues, SaveDataIssueCode.InconsistentState, path, "天赋内容与布局节点不一致"); }
            if (dto.Layout == null && dto.Talents.Count > 0)
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "无布局道具含天赋节点"); }
        }

        static FirewoodLayout ReadSavedLayout(FirewoodLayoutDto dto, string path, List<SaveDataIssue> issues)
        {
            if (dto == null) { return null; }
            if (dto.Nodes == null || dto.Edges == null || dto.Nodes.Count > 15 || dto.Edges.Count > 120
                || dto.Nodes.Any(n => n == null || n.OutwardQ.HasValue != n.OutwardR.HasValue) || dto.Edges.Any(e => e == null))
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "布局节点或连线非法"); return null; }
            try
            {
                var layout = new FirewoodLayout(dto.Size, dto.Seed, dto.GenerationVersion,
                    dto.Nodes.Select(n => new FirewoodNode(n.Id, new HexCoordinate(n.Q, n.R), n.Kind,
                        n.OutwardQ.HasValue ? new HexCoordinate(n.OutwardQ.Value, n.OutwardR.Value) : (HexCoordinate?)null)),
                    dto.Edges.Select(e => new FirewoodEdge(e.First, e.Second)));
                IReadOnlyList<SkillBoardIssue> invalid = layout.Validate();
                foreach (SkillBoardIssue issue in invalid)
                { Add(issues, SaveDataIssueCode.InvalidValue, path, issue.ToString()); }
                return invalid.Count == 0 ? layout : null;
            }
            catch (OverflowException)
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "布局坐标超出表示范围"); return null; }
        }

        static void ValidateSkillBoard(SkillBoardSaveDto dto, string playerId,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items, IDictionary<ItemInstanceId, string> ownership,
            string path, List<SaveDataIssue> issues)
        {
            if (dto == null || dto.Firewoods == null || dto.Embeddings == null || dto.Connections == null
                || dto.Firewoods.Count > LocalSaveFormat.MaximumItems || dto.Embeddings.Count > LocalSaveFormat.MaximumItems
                || dto.Connections.Count > LocalSaveFormat.MaximumItems * 3)
            { Add(issues, SaveDataIssueCode.MissingValue, path, "棋盘集合缺失或超限"); return; }
            bool empty = dto.Firewoods.Count == 0 && dto.Embeddings.Count == 0 && dto.Connections.Count == 0
                && string.IsNullOrEmpty(dto.EnergyBottleInstanceId);
            if (dto.ActorInstanceId != playerId && !(empty && string.IsNullOrEmpty(dto.ActorInstanceId)))
            { Add(issues, SaveDataIssueCode.InvalidOwnership, path, "棋盘玩家与 Profile 不一致"); }
            var placements = new List<FirewoodPlacement>();
            var byId = new Dictionary<string, FirewoodLayout>();
            foreach (InstalledFirewoodDto w in dto.Firewoods)
            {
                if (w == null) { Add(issues, SaveDataIssueCode.MissingValue, path, "柴薪为空"); continue; }
                ValidateItemReference(w.ItemInstanceId, items, ownership, path + ".firewoods", issues);
                if (!ItemInstanceId.TryParse(w.ItemInstanceId, out ItemInstanceId id) || !items.TryGetValue(id, out ItemInstanceDto item)) { continue; }
                FirewoodLayout layout = ReadSavedLayout(item.SkillBoard?.Layout, path + ".firewoods.layout", issues);
                if (layout == null || w.Rotation < 0 || w.Rotation > 5 || !byId.TryAdd(w.ItemInstanceId, layout))
                { Add(issues, SaveDataIssueCode.InvalidValue, path, "柴薪布局、旋转或身份非法"); continue; }
                try { placements.Add(new FirewoodPlacement(w.ItemInstanceId, layout, new HexCoordinate(w.Q, w.R), w.Rotation)); }
                catch (OverflowException) { Add(issues, SaveDataIssueCode.InvalidValue, path, "柴薪锚点超出表示范围"); }
            }
            var connections = new List<FirewoodConnection>();
            foreach (FirewoodConnectionDto c in dto.Connections)
            {
                if (c == null) { Add(issues, SaveDataIssueCode.MissingValue, path, "连接为空"); continue; }
                connections.Add(new FirewoodConnection(new FirewoodPortReference(c.FirstFirewoodId, c.FirstNodeId),
                    new FirewoodPortReference(c.SecondFirewoodId, c.SecondNodeId)));
            }
            SkillBoardPlacementResult geometry = SkillBoardPlacementValidator.Validate(placements, connections, true);
            foreach (SkillBoardIssue issue in geometry.Issues) { Add(issues, SaveDataIssueCode.InvalidValue, path, issue.ToString()); }
            if (geometry.Success && !SkillBoardRootConnectivity.IsRooted(placements, connections))
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "剩余柴薪未连根"); }
            var slots = new HashSet<FirewoodPortReference>();
            var connected = new HashSet<FirewoodPortReference>(connections.SelectMany(c => new[] { c.First, c.Second }));
            foreach (SkillBoardEmbeddingDto e in dto.Embeddings)
            {
                if (e == null) { Add(issues, SaveDataIssueCode.MissingValue, path, "嵌入为空"); continue; }
                ValidateItemReference(e.ItemInstanceId, items, ownership, path + ".embeddings", issues);
                var slot = new FirewoodPortReference(e.FirewoodInstanceId, e.NodeId);
                if (!slots.Add(slot) || !byId.TryGetValue(e.FirewoodInstanceId, out FirewoodLayout layout)
                    || !ItemInstanceId.TryParse(e.ItemInstanceId, out ItemInstanceId id) || !items.TryGetValue(id, out ItemInstanceDto item))
                { Add(issues, SaveDataIssueCode.InvalidValue, path, "嵌入目标非法或重复"); continue; }
                bool valid = e.NodeId == SkillBoardGeometry.CenterNodeId
                    ? layout.Size == FirewoodSize.Large && !string.IsNullOrEmpty(item.SkillBoard?.FlameSkillContentId)
                    : layout.Nodes.Any(n => n.Id == e.NodeId && n.Kind == FirewoodNodeKind.Crack)
                        && item.SkillBoard?.EmberRadius > 0 && !connected.Contains(slot);
                if (!valid) { Add(issues, SaveDataIssueCode.InvalidValue, path, "嵌入角色或连接互斥不合法"); }
            }
            if (!string.IsNullOrEmpty(dto.EnergyBottleInstanceId))
            { ValidateItemReference(dto.EnergyBottleInstanceId, items, ownership, path + ".bottle", issues); }
        }
    }
}
