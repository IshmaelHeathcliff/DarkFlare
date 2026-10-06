using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace DarkFlare
{
    public enum FirewoodNodeKind
    {
        [LabelText("天赋")] Talent,
        [LabelText("裂缝")] Crack,
    }

    public readonly struct FirewoodNode
    {
        public string Id { get; }
        public HexCoordinate Position { get; }
        public FirewoodNodeKind Kind { get; }
        public HexCoordinate? Outward { get; }

        public FirewoodNode(string id, HexCoordinate position, FirewoodNodeKind kind, HexCoordinate? outward = null)
        {
            Id = id;
            Position = position;
            Kind = kind;
            Outward = outward;
        }
    }

    public readonly struct FirewoodEdge
    {
        public string First { get; }
        public string Second { get; }

        public FirewoodEdge(string first, string second)
        {
            First = first;
            Second = second;
        }
    }

    public sealed class SkillBoardIssue
    {
        public string Code { get; }
        public string Message { get; }
        public string Subject { get; }
        public HexCoordinate? Position { get; }

        public SkillBoardIssue(string code, string message, string subject = null, HexCoordinate? position = null)
        {
            Code = code;
            Message = message;
            Subject = subject;
            Position = position;
        }

        public override string ToString()
        {
            return $"[{Code}] {Subject} {Position}：{Message}";
        }
    }

    public sealed class FirewoodLayout
    {
        readonly IReadOnlyList<FirewoodNode> _nodes;
        readonly IReadOnlyList<FirewoodEdge> _edges;
        public FirewoodSize Size { get; }
        public int Seed { get; }
        public int GenerationVersion { get; }
        public IReadOnlyList<FirewoodNode> Nodes => _nodes;
        public IReadOnlyList<FirewoodEdge> Edges => _edges;
        public IReadOnlyList<HexCoordinate> Body => SkillBoardGeometry.Body(Size);

        public FirewoodLayout(FirewoodSize size, int seed, int generationVersion,
            IEnumerable<FirewoodNode> nodes, IEnumerable<FirewoodEdge> edges)
        {
            Size = size;
            Seed = seed;
            GenerationVersion = generationVersion;
            _nodes = Array.AsReadOnly((nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray());
            _edges = Array.AsReadOnly((edges ?? throw new ArgumentNullException(nameof(edges))).ToArray());
        }

        public IReadOnlyList<SkillBoardIssue> Validate()
        {
            List<SkillBoardIssue> issues = new List<SkillBoardIssue>();
            if (!Enum.IsDefined(typeof(FirewoodSize), Size))
            {
                issues.Add(new SkillBoardIssue("size", "未知柴薪尺寸"));
                return issues.AsReadOnly();
            }
            if (GenerationVersion != FirewoodGenerator.ContractVersion)
            {
                issues.Add(new SkillBoardIssue("generation_version", "不支持的生成合同版本"));
            }
            var body = new HashSet<HexCoordinate>(Body);
            var occupied = new HashSet<HexCoordinate>();
            var byId = new Dictionary<string, FirewoodNode>(StringComparer.Ordinal);
            int cracks = 0;
            int inner = 0;
            bool outsideBody = false;
            foreach (FirewoodNode node in Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Id) || node.Id == SkillBoardGeometry.CenterNodeId
                    || !byId.TryAdd(node.Id, node))
                {
                    issues.Add(new SkillBoardIssue("node_id", "节点身份为空、重复或占用中心身份", node.Id));
                }
                if (!body.Contains(node.Position) || !occupied.Add(node.Position)
                    || (Size != FirewoodSize.Small && node.Position.Equals(default)))
                {
                    issues.Add(new SkillBoardIssue("node_position", "节点位置越出主体、重复或占用空中心", node.Id, node.Position));
                }
                outsideBody |= !body.Contains(node.Position);
                if (Size == FirewoodSize.Large && body.Contains(node.Position) && node.Position.DistanceTo(default) == 1)
                {
                    inner++;
                    if (node.Kind != FirewoodNodeKind.Talent)
                    {
                        issues.Add(new SkillBoardIssue("inner_talent", "内圈只接受连接天赋", node.Id));
                    }
                }
                if (node.Kind == FirewoodNodeKind.Crack)
                {
                    cracks++;
                    bool valid = node.Outward.HasValue && SkillBoardGeometry.NeighborDirections.Contains(node.Outward.Value);
                    if (valid && Size == FirewoodSize.Small)
                    {
                        int vertex = SkillBoardGeometry.Ring(Size).ToList().IndexOf(node.Position);
                        int direction = SkillBoardGeometry.NeighborDirections.ToList().IndexOf(node.Outward.Value);
                        valid = vertex >= 0 && (((int)SkillBoardGeometry.SmallOutwardDirections[vertex] & (1 << direction)) != 0);
                    }
                    else if (valid)
                    {
                        int direction = SkillBoardGeometry.OutwardDirection(Size, node.Position);
                        valid = direction >= 0 && SkillBoardGeometry.NeighborDirections[direction].Equals(node.Outward.Value);
                    }
                    if (!valid) { issues.Add(new SkillBoardIssue("port_direction", "裂缝位置或外伸方向不合法", node.Id, node.Position)); }
                }
                else if (node.Kind != FirewoodNodeKind.Talent || node.Outward.HasValue)
                {
                    issues.Add(new SkillBoardIssue("node_kind", "节点类型非法或普通天赋包含外伸端口", node.Id));
                }
            }
            if (!FirewoodGenerator.IsLegalCounts(Size, Nodes.Count, cracks, inner))
            {
                issues.Add(new SkillBoardIssue("counts", $"节点 / 裂缝 / 内圈数量不合法：{Nodes.Count} / {cracks} / {inner}"));
            }
            if (!outsideBody) { ValidateGraph(byId, issues); }
            return issues.AsReadOnly();
        }

        void ValidateGraph(Dictionary<string, FirewoodNode> nodes, List<SkillBoardIssue> issues)
        {
            var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string id in nodes.Keys) { neighbors[id] = new HashSet<string>(StringComparer.Ordinal); }
            if (Size == FirewoodSize.Large) { neighbors[SkillBoardGeometry.CenterNodeId] = new HashSet<string>(StringComparer.Ordinal); }
            foreach (FirewoodEdge edge in Edges)
            {
                if (edge.First == null || edge.Second == null || edge.First == edge.Second
                    || !neighbors.ContainsKey(edge.First) || !neighbors.ContainsKey(edge.Second))
                {
                    issues.Add(new SkillBoardIssue("edge_endpoint", "连线端点缺失、重复或非法", $"{edge.First}/{edge.Second}"));
                    continue;
                }
                if (!neighbors[edge.First].Add(edge.Second))
                {
                    issues.Add(new SkillBoardIssue("duplicate_edge", "连线重复", $"{edge.First}/{edge.Second}"));
                }
                neighbors[edge.Second].Add(edge.First);
            }
            if (Size == FirewoodSize.Large)
            {
                foreach (string id in neighbors[SkillBoardGeometry.CenterNodeId])
                {
                    if (nodes[id].Kind != FirewoodNodeKind.Talent || nodes[id].Position.DistanceTo(default) != 1)
                    {
                        issues.Add(new SkillBoardIssue("center_edge", "中心只能经过内圈天赋连接外圈", id));
                    }
                }
                foreach (FirewoodNode node in nodes.Values.Where(n => n.Position.DistanceTo(default) == 1))
                {
                    if (!neighbors[node.Id].Contains(SkillBoardGeometry.CenterNodeId)
                        || !neighbors[node.Id].Any(id => nodes.TryGetValue(id, out FirewoodNode other) && other.Position.DistanceTo(default) == 2))
                    {
                        issues.Add(new SkillBoardIssue("inner_edge", "内圈天赋必须连接中心与外圈", node.Id));
                    }
                }
            }
            if (neighbors.Count == 0) { return; }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            pending.Enqueue(neighbors.Keys.OrderBy(id => id, StringComparer.Ordinal).First());
            while (pending.Count > 0)
            {
                string id = pending.Dequeue();
                if (!visited.Add(id)) { continue; }
                foreach (string neighbor in neighbors[id]) { pending.Enqueue(neighbor); }
            }
            if (visited.Count != neighbors.Count) { issues.Add(new SkillBoardIssue("disconnected_graph", "节点连线未连通")); }
        }
    }
}
