using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static class PlayerGrowthGraph
    {
        public static string Validate(PlayerGrowthState state, IEnumerable<FirewoodPlacement> placements,
            IReadOnlyList<FirewoodConnection> connections)
        {
            string error = state.Validate();
            if (error != null) { return error; }
            FirewoodPlacement[] all = placements.ToArray();
            var woods = all.ToDictionary(w => w.Id, StringComparer.Ordinal);
            var active = new HashSet<string>(state.Activations.Where(a => a.IsFirewood).Select(a => a.FirewoodId), StringComparer.Ordinal);
            foreach (GrowthActivation record in state.Activations)
            {
                if (!woods.TryGetValue(record.FirewoodId, out FirewoodPlacement wood) || !active.Contains(wood.Id))
                {
                    return "激活记录不属于已安装且点亮的柴薪";
                }
                if (!record.IsFirewood && !wood.Layout.Nodes.Any(n => n.Id == record.NodeId && n.Kind == FirewoodNodeKind.Talent))
                {
                    return "点亮记录不是普通天赋";
                }
            }
            var rooted = new HashSet<string>(all.Where(w => active.Contains(w.Id) && w.Layout.Size == FirewoodSize.Large
                && SkillBoardGeometry.Roots.Contains(w.Anchor)).Select(w => w.Id), StringComparer.Ordinal);
            var queue = new Queue<string>(rooted);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                foreach (FirewoodConnection connection in connections)
                {
                    string next = connection.First.PlacementId == id ? connection.Second.PlacementId
                        : connection.Second.PlacementId == id ? connection.First.PlacementId : null;
                    if (next != null && active.Contains(next) && rooted.Add(next)) { queue.Enqueue(next); }
                }
            }
            if (!rooted.SetEquals(active)) { return "已点亮柴薪失去通往初始空洞的激活路径"; }
            foreach (string id in active)
            {
                FirewoodPlacement wood = woods[id];
                var allowed = new HashSet<string>(wood.Layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Crack).Select(n => n.Id));
                string[] talents = state.Activations.Where(a => a.FirewoodId == id && !a.IsFirewood).Select(a => a.NodeId).ToArray();
                allowed.UnionWith(talents);
                var reached = new HashSet<string>();
                if (wood.Layout.Size == FirewoodSize.Large)
                {
                    allowed.Add(SkillBoardGeometry.CenterNodeId);
                    reached.Add(SkillBoardGeometry.CenterNodeId);
                }
                else
                {
                    foreach (FirewoodConnection connection in connections)
                    {
                        if (connection.First.PlacementId == id && rooted.Contains(connection.Second.PlacementId)) { reached.Add(connection.First.NodeId); }
                        if (connection.Second.PlacementId == id && rooted.Contains(connection.First.PlacementId)) { reached.Add(connection.Second.NodeId); }
                    }
                }
                var nodes = new Queue<string>(reached);
                while (nodes.Count > 0)
                {
                    string node = nodes.Dequeue();
                    foreach (FirewoodEdge edge in wood.Layout.Edges)
                    {
                        string next = edge.First == node ? edge.Second : edge.Second == node ? edge.First : null;
                        if (next != null && allowed.Contains(next) && reached.Add(next)) { nodes.Enqueue(next); }
                    }
                }
                if (talents.Any(t => !reached.Contains(t))) { return "普通天赋没有经过已点亮节点的有效连线路径"; }
            }
            return null;
        }

        public static string Validate(PlayerGrowthState state, SkillBoardState board)
        {
            return Validate(state, board.Firewoods.Select(w => w.Placement), board.Connections);
        }
    }
}
