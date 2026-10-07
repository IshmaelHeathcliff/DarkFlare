using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public static class SkillBoardRootConnectivity
    {
        // 输入先经过几何 / 端口校验；此处只判断成环或跨根网络的根可达性。
        public static bool IsRooted(IReadOnlyList<FirewoodPlacement> placements, IReadOnlyList<FirewoodConnection> connections)
        {
            var reachable = new HashSet<string>(placements.Where(w => w.Layout.Size == FirewoodSize.Large
                && SkillBoardGeometry.Roots.Contains(w.Anchor)).Select(w => w.Id), StringComparer.Ordinal);
            var neighbors = placements.ToDictionary(w => w.Id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (FirewoodConnection connection in connections)
            {
                if (!neighbors.ContainsKey(connection.First.PlacementId) || !neighbors.ContainsKey(connection.Second.PlacementId)) { return false; }
                neighbors[connection.First.PlacementId].Add(connection.Second.PlacementId);
                neighbors[connection.Second.PlacementId].Add(connection.First.PlacementId);
            }
            var pending = new Queue<string>(reachable);
            while (pending.Count > 0)
            {
                foreach (string other in neighbors[pending.Dequeue()])
                { if (reachable.Add(other)) { pending.Enqueue(other); } }
            }
            return reachable.Count == placements.Count;
        }
    }
}
