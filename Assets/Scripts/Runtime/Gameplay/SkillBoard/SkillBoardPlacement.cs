using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class FirewoodPlacement
    {
        public string Id { get; }
        public FirewoodLayout Layout { get; }
        public HexCoordinate Anchor { get; }
        public int Rotation { get; }

        public FirewoodPlacement(string id, FirewoodLayout layout, HexCoordinate anchor, int rotation = 0)
        {
            Id = id;
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Anchor = anchor;
            Rotation = (rotation % 6 + 6) % 6;
        }

        public HexCoordinate ToWorld(HexCoordinate local)
        {
            return local.Rotate(Rotation).Add(Anchor);
        }
    }

    public readonly struct FirewoodPortReference : IEquatable<FirewoodPortReference>
    {
        public string PlacementId { get; }
        public string NodeId { get; }

        public FirewoodPortReference(string placementId, string nodeId)
        {
            PlacementId = placementId;
            NodeId = nodeId;
        }

        public bool Equals(FirewoodPortReference other)
        {
            return PlacementId == other.PlacementId && NodeId == other.NodeId;
        }

        public override bool Equals(object obj)
        {
            return obj is FirewoodPortReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlacementId, NodeId);
        }

        public override string ToString()
        {
            return $"{PlacementId}/{NodeId}";
        }
    }

    public readonly struct FirewoodConnection
    {
        public FirewoodPortReference First { get; }
        public FirewoodPortReference Second { get; }

        public FirewoodConnection(FirewoodPortReference first, FirewoodPortReference second)
        {
            First = first;
            Second = second;
        }
    }

    public sealed class SkillBoardPlacementResult
    {
        public IReadOnlyList<SkillBoardIssue> Issues { get; }
        public IReadOnlyList<HexCoordinate> OccupiedCells { get; }
        public bool Success => Issues.Count == 0;

        internal SkillBoardPlacementResult(List<SkillBoardIssue> issues, IEnumerable<HexCoordinate> occupied)
        {
            Issues = issues.AsReadOnly();
            OccupiedCells = Array.AsReadOnly(occupied.Distinct().OrderBy(p => p).ToArray());
        }
    }

    public static class SkillBoardPlacementValidator
    {
        readonly struct WorldPort
        {
            public FirewoodPortReference Reference { get; }
            public HexCoordinate Position { get; }
            public HexCoordinate Target { get; }
            public FirewoodSize Size { get; }

            public WorldPort(FirewoodPlacement placement, FirewoodNode node)
            {
                Reference = new FirewoodPortReference(placement.Id, node.Id);
                Position = placement.ToWorld(node.Position);
                Target = placement.ToWorld(node.Position.Add(node.Outward.Value));
                Size = placement.Layout.Size;
            }
        }

        public static IReadOnlyList<FirewoodConnection> FindConnections(IReadOnlyList<FirewoodPlacement> placements)
        {
            if (placements == null) { throw new ArgumentNullException(nameof(placements)); }
            if (placements.Any(p => p == null || p.Layout.Validate().Count != 0)) { return Array.Empty<FirewoodConnection>(); }
            List<WorldPort> ports = placements.OrderBy(p => p.Id, StringComparer.Ordinal)
                .SelectMany(p => p.Layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Crack)
                    .OrderBy(n => n.Id, StringComparer.Ordinal).Select(n => new WorldPort(p, n))).ToList();
            List<FirewoodConnection> result = new List<FirewoodConnection>();
            for (int i = 0; i < ports.Count; i++)
            {
                for (int j = i + 1; j < ports.Count; j++)
                {
                    WorldPort first = ports[i];
                    WorldPort second = ports[j];
                    if (first.Reference.PlacementId != second.Reference.PlacementId
                        && first.Target.Equals(second.Position) && second.Target.Equals(first.Position))
                    {
                        result.Add(new FirewoodConnection(first.Reference, second.Reference));
                    }
                }
            }
            return result.AsReadOnly();
        }

        public static SkillBoardPlacementResult Validate(IReadOnlyList<FirewoodPlacement> placements,
            IReadOnlyList<FirewoodConnection> connections, bool requireLargeRoots = false)
        {
            if (placements == null) { throw new ArgumentNullException(nameof(placements)); }
            if (connections == null) { throw new ArgumentNullException(nameof(connections)); }
            List<SkillBoardIssue> issues = new List<SkillBoardIssue>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (FirewoodPlacement placement in placements)
            {
                if (placement == null || string.IsNullOrWhiteSpace(placement.Id) || !ids.Add(placement.Id))
                {
                    issues.Add(new SkillBoardIssue("placement_id", "柴薪身份为空或重复", placement?.Id));
                    continue;
                }
                foreach (SkillBoardIssue issue in placement.Layout.Validate())
                {
                    issues.Add(new SkillBoardIssue(issue.Code, issue.Message, $"{placement.Id}/{issue.Subject}", issue.Position));
                }
                if (requireLargeRoots && placement.Layout.Size == FirewoodSize.Large && !SkillBoardGeometry.Roots.Contains(placement.Anchor))
                {
                    issues.Add(new SkillBoardIssue("root", "大柴薪必须安装在初始空洞", placement.Id, placement.Anchor));
                }
            }
            if (issues.Count > 0) { return new SkillBoardPlacementResult(issues, Array.Empty<HexCoordinate>()); }
            var body = new Dictionary<HexCoordinate, string>();
            var ports = new Dictionary<FirewoodPortReference, WorldPort>();
            var reserved = new HashSet<HexCoordinate>(SkillBoardGeometry.CenterReservation);
            try
            {
                foreach (FirewoodPlacement placement in placements)
                {
                    foreach (HexCoordinate local in placement.Layout.Body)
                    {
                        HexCoordinate position = placement.ToWorld(local);
                        if (!body.TryAdd(position, placement.Id))
                        {
                            issues.Add(new SkillBoardIssue("body_overlap", $"主体与 {body[position]} 重叠，包括内部空格", placement.Id, position));
                        }
                        if (reserved.Contains(position)) { issues.Add(new SkillBoardIssue("center_reserved", "覆盖中心保留区", placement.Id, position)); }
                    }
                    foreach (FirewoodNode node in placement.Layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Crack))
                    {
                        var port = new WorldPort(placement, node);
                        ports.Add(port.Reference, port);
                    }
                }
            }
            catch (OverflowException)
            {
                issues.Add(new SkillBoardIssue("coordinate_overflow", "旋转或平移超出坐标表示范围"));
                return new SkillBoardPlacementResult(issues, body.Keys);
            }
            var connected = new HashSet<FirewoodPortReference>();
            foreach (FirewoodConnection connection in connections)
            {
                if (!ports.TryGetValue(connection.First, out WorldPort first) || !ports.TryGetValue(connection.Second, out WorldPort second))
                {
                    issues.Add(new SkillBoardIssue("connection_endpoint", "连接必须引用有效裂缝", $"{connection.First} ↔ {connection.Second}"));
                    continue;
                }
                if (first.Reference.PlacementId == second.Reference.PlacementId || !SkillBoardGeometry.CanConnect(first.Size, second.Size))
                {
                    issues.Add(new SkillBoardIssue("connection_size", "尺寸不支持连接或连接指向自身", first.Reference.ToString(), first.Position));
                    continue;
                }
                if (!first.Target.Equals(second.Position) || !second.Target.Equals(first.Position))
                {
                    issues.Add(new SkillBoardIssue("connection_direction", "裂缝必须相邻且外伸方向互相指向", first.Reference.ToString(), first.Position));
                    continue;
                }
                if (connected.Contains(first.Reference) || connected.Contains(second.Reference))
                {
                    issues.Add(new SkillBoardIssue("port_in_use", "每个裂缝最多连接一件柴薪", first.Reference.ToString(), first.Position));
                    continue;
                }
                connected.Add(first.Reference);
                connected.Add(second.Reference);
            }
            var extensions = new Dictionary<HexCoordinate, FirewoodPortReference>();
            foreach (WorldPort port in ports.Values)
            {
                if (connected.Contains(port.Reference)) { continue; }
                if (body.ContainsKey(port.Target))
                {
                    issues.Add(new SkillBoardIssue("extension_body", "未连接端点进入柴薪主体或非配对裂缝", port.Reference.ToString(), port.Target));
                }
                if (!extensions.TryAdd(port.Target, port.Reference))
                {
                    issues.Add(new SkillBoardIssue("extension_overlap", "未连接外伸格不能互相重叠", port.Reference.ToString(), port.Target));
                }
                if (reserved.Contains(port.Target))
                {
                    issues.Add(new SkillBoardIssue("center_reserved", "外伸格覆盖中心保留区", port.Reference.ToString(), port.Target));
                }
            }
            return new SkillBoardPlacementResult(issues, body.Keys.Concat(extensions.Keys));
        }
    }
}
