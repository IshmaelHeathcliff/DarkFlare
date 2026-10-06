using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DarkFlare
{
    public readonly struct FirewoodSymmetryScore : IComparable<FirewoodSymmetryScore>
    {
        public int Rotations { get; }
        public int Reflections { get; }
        public int MinimumDistance { get; }

        public FirewoodSymmetryScore(int rotations, int reflections, int minimumDistance)
        {
            Rotations = rotations;
            Reflections = reflections;
            MinimumDistance = minimumDistance;
        }

        public int CompareTo(FirewoodSymmetryScore other)
        {
            int result = Rotations.CompareTo(other.Rotations);
            if (result != 0) { return result; }
            result = Reflections.CompareTo(other.Reflections);
            return result != 0 ? result : MinimumDistance.CompareTo(other.MinimumDistance);
        }

        public override string ToString()
        {
            return $"旋转 {Rotations} / 反射 {Reflections} / 最小间距 {MinimumDistance}";
        }
    }

    public sealed class FirewoodGenerationResult
    {
        public FirewoodLayout Layout { get; }
        public IReadOnlyList<SkillBoardIssue> Issues { get; }
        public int CandidateCount { get; }
        public int BestCandidateCount { get; }
        public int RotationClassCount { get; }
        public FirewoodSymmetryScore Symmetry { get; }
        public bool Success => Layout != null && Issues.Count == 0;

        internal FirewoodGenerationResult(FirewoodLayout layout, IReadOnlyList<SkillBoardIssue> issues,
            int candidates = 0, int bestCandidates = 0, int classes = 0, FirewoodSymmetryScore symmetry = default)
        {
            Layout = layout;
            Issues = issues;
            CandidateCount = candidates;
            BestCandidateCount = bestCandidates;
            RotationClassCount = classes;
            Symmetry = symmetry;
        }
    }

    public static class FirewoodGenerator
    {
        public const int ContractVersion = 1;

        public static bool IsLegalCounts(FirewoodSize size, int nodes, int cracks, int inner)
        {
            switch (size)
            {
                case FirewoodSize.Large:
                    return nodes >= 9 && nodes <= 15 && cracks >= 1 && cracks <= 6 && cracks <= nodes / 2
                        && inner >= 1 && inner <= 3 && nodes - inner <= 12 && cracks <= nodes - inner;
                case FirewoodSize.Medium:
                    return nodes >= 4 && nodes <= 6 && cracks >= 1 && cracks <= 3 && cracks <= nodes / 2 && inner == 0;
                case FirewoodSize.Small:
                    return nodes >= 2 && nodes <= 3 && cracks >= 1 && cracks <= 2 && inner == 0;
                default: return false;
            }
        }

        public static FirewoodGenerationResult Generate(FirewoodSize size, int seed,
            int nodeCount = 0, int crackCount = 0, int innerCount = 0,
            IReadOnlyList<HexDirectionMask> smallDirections = null)
        {
            List<SkillBoardIssue> issues = new List<SkillBoardIssue>();
            if (!Enum.IsDefined(typeof(FirewoodSize), size))
            {
                issues.Add(new SkillBoardIssue("size", "未知柴薪尺寸"));
                return new FirewoodGenerationResult(null, issues.AsReadOnly());
            }
            IReadOnlyList<HexDirectionMask> allowed = smallDirections ?? SkillBoardGeometry.SmallOutwardDirections;
            if (size == FirewoodSize.Small)
            {
                if (allowed.Count != 3 || Enumerable.Range(0, 3).Any(i => allowed[i] == HexDirectionMask.None
                    || ((int)allowed[i] & ~(int)SkillBoardGeometry.SmallOutwardDirections[i]) != 0))
                {
                    issues.Add(new SkillBoardIssue("small_template", "小柴薪三个顶点都需要合法向外方向", "_smallPortDirections"));
                    return new FirewoodGenerationResult(null, issues.AsReadOnly());
                }
            }
            int minimum = size == FirewoodSize.Large ? 9 : size == FirewoodSize.Medium ? 4 : 2;
            int maximum = size == FirewoodSize.Large ? 15 : size == FirewoodSize.Medium ? 6 : 3;
            int maximumCracks = size == FirewoodSize.Large ? 6 : size == FirewoodSize.Medium ? 3 : 2;
            List<(int N, int C, int L)> combinations = new List<(int, int, int)>();
            for (int n = minimum; n <= maximum; n++)
            {
                for (int c = 1; c <= maximumCracks; c++)
                {
                    for (int l = size == FirewoodSize.Large ? 1 : 0; l <= (size == FirewoodSize.Large ? 3 : 0); l++)
                    {
                        if (IsLegalCounts(size, n, c, l) && (nodeCount == 0 || n == nodeCount)
                            && (crackCount == 0 || c == crackCount) && (innerCount == 0 || l == innerCount))
                        {
                            combinations.Add((n, c, l));
                        }
                    }
                }
            }
            if (combinations.Count == 0)
            {
                issues.Add(new SkillBoardIssue("counts", $"无法生成指定数量：{nodeCount} / {crackCount} / {innerCount}"));
                return new FirewoodGenerationResult(null, issues.AsReadOnly());
            }
            int selectedN = Choose(combinations.Select(p => p.N).Distinct().ToList(), seed, 1);
            int selectedC = Choose(combinations.Where(p => p.N == selectedN).Select(p => p.C).Distinct().ToList(), seed, 2);
            int selectedL = Choose(combinations.Where(p => p.N == selectedN && p.C == selectedC)
                .Select(p => p.L).ToList(), seed, 3);
            List<FirewoodNode[]> candidates = EnumeratePorts(size, selectedC, allowed);
            FirewoodSymmetryScore best = default;
            List<FirewoodNode[]> optimal = new List<FirewoodNode[]>();
            foreach (FirewoodNode[] candidate in candidates)
            {
                FirewoodSymmetryScore score = Score(size, candidate);
                int comparison = score.CompareTo(best);
                if (optimal.Count == 0 || comparison > 0) { best = score; optimal.Clear(); }
                if (score.CompareTo(best) == 0) { optimal.Add(candidate); }
            }
            var classes = new SortedDictionary<string, List<FirewoodNode[]>>(StringComparer.Ordinal);
            foreach (FirewoodNode[] candidate in optimal)
            {
                string key = CanonicalKey(size, candidate);
                if (!classes.TryGetValue(key, out List<FirewoodNode[]> group))
                {
                    group = new List<FirewoodNode[]>();
                    classes.Add(key, group);
                }
                group.Add(candidate);
            }
            if (classes.Count == 0)
            {
                issues.Add(new SkillBoardIssue("no_layout", "没有可行的裂缝布局"));
                return new FirewoodGenerationResult(null, issues.AsReadOnly());
            }
            List<FirewoodNode[]> chosenClass = Choose(classes.Values.ToList(), seed, 4);
            FirewoodNode[] ports = Choose(chosenClass.OrderBy(Key, StringComparer.Ordinal).ToList(), seed, 5);
            List<FirewoodNode> nodes = new List<FirewoodNode>(ports);
            IReadOnlyList<HexCoordinate> ring = SkillBoardGeometry.Ring(size);
            List<int> free = Enumerable.Range(0, ring.Count).Where(i => !ports.Any(p => p.Position.Equals(ring[i]))).ToList();
            Shuffle(free, new Random(Derive(seed, 6)));
            for (int i = 0; i < selectedN - selectedL - selectedC; i++)
            {
                int index = free[i];
                nodes.Add(new FirewoodNode(NodeId("outer", index), ring[index], FirewoodNodeKind.Talent));
            }
            List<int> innerSlots = Enumerable.Range(0, 6).ToList();
            Shuffle(innerSlots, new Random(Derive(seed, 7)));
            for (int i = 0; i < selectedL; i++)
            {
                int index = innerSlots[i];
                nodes.Add(new FirewoodNode(NodeId("inner", index), SkillBoardGeometry.NeighborDirections[index], FirewoodNodeKind.Talent));
            }
            List<FirewoodEdge> edges = BuildEdges(size, nodes);
            var layout = new FirewoodLayout(size, seed, ContractVersion,
                nodes.OrderBy(n => n.Id, StringComparer.Ordinal), edges);
            IReadOnlyList<SkillBoardIssue> validation = layout.Validate();
            return new FirewoodGenerationResult(validation.Count == 0 ? layout : null, validation,
                candidates.Count, optimal.Count, classes.Count, best);
        }

        public static FirewoodSymmetryScore Score(FirewoodSize size, IReadOnlyList<FirewoodNode> cracks)
        {
            int rotations = 0;
            int reflections = 0;
            int order = size == FirewoodSize.Small ? 3 : 6;
            string original = Key(cracks);
            for (int i = 0; i < order; i++)
            {
                if (i != 0 && Key(Transform(size, cracks, i, false)) == original) { rotations++; }
                if (Key(Transform(size, cracks, i, true)) == original) { reflections++; }
            }
            int distance = 0;
            if (cracks.Count > 1)
            {
                distance = int.MaxValue;
                for (int i = 0; i < cracks.Count; i++)
                {
                    for (int j = i + 1; j < cracks.Count; j++)
                    {
                        distance = Math.Min(distance, cracks[i].Position.DistanceTo(cracks[j].Position));
                    }
                }
            }
            return new FirewoodSymmetryScore(rotations, reflections, distance);
        }

        static List<FirewoodNode[]> EnumeratePorts(FirewoodSize size, int count, IReadOnlyList<HexDirectionMask> allowed)
        {
            List<FirewoodNode[]> result = new List<FirewoodNode[]>();
            int slots = size == FirewoodSize.Small ? 3 : 6;
            for (int mask = 1; mask < (1 << slots); mask++)
            {
                List<int> selected = Enumerable.Range(0, slots).Where(i => (mask & (1 << i)) != 0).ToList();
                if (selected.Count != count) { continue; }
                AddDirections(0, new List<FirewoodNode>());

                void AddDirections(int index, List<FirewoodNode> nodes)
                {
                    if (index == selected.Count) { result.Add(nodes.ToArray()); return; }
                    int slot = selected[index];
                    int ringIndex = size == FirewoodSize.Large ? slot * 2 : slot;
                    HexCoordinate position = SkillBoardGeometry.Ring(size)[ringIndex];
                    for (int direction = 0; direction < 6; direction++)
                    {
                        if (size == FirewoodSize.Small ? ((int)allowed[slot] & (1 << direction)) == 0 : direction != slot) { continue; }
                        nodes.Add(new FirewoodNode(NodeId("outer", ringIndex), position, FirewoodNodeKind.Crack,
                            SkillBoardGeometry.NeighborDirections[direction]));
                        AddDirections(index + 1, nodes);
                        nodes.RemoveAt(nodes.Count - 1);
                    }
                }
            }
            return result;
        }

        static List<FirewoodEdge> BuildEdges(FirewoodSize size, List<FirewoodNode> nodes)
        {
            IReadOnlyList<HexCoordinate> ring = SkillBoardGeometry.Ring(size);
            List<FirewoodNode> outer = nodes.Where(n => ring.Contains(n.Position))
                .OrderBy(n => ring.ToList().IndexOf(n.Position)).ToList();
            List<FirewoodEdge> edges = new List<FirewoodEdge>();
            for (int i = 0; i < outer.Count - 1; i++) { edges.Add(new FirewoodEdge(outer[i].Id, outer[i + 1].Id)); }
            if (outer.Count > 2) { edges.Add(new FirewoodEdge(outer[outer.Count - 1].Id, outer[0].Id)); }
            if (size == FirewoodSize.Large)
            {
                foreach (FirewoodNode inner in nodes.Where(n => n.Position.DistanceTo(default) == 1).OrderBy(n => n.Id, StringComparer.Ordinal))
                {
                    FirewoodNode target = outer.OrderBy(n => n.Position.DistanceTo(inner.Position))
                        .ThenBy(n => n.Id, StringComparer.Ordinal).First();
                    edges.Add(new FirewoodEdge(SkillBoardGeometry.CenterNodeId, inner.Id));
                    edges.Add(new FirewoodEdge(inner.Id, target.Id));
                }
            }
            return edges;
        }

        static FirewoodNode[] Transform(FirewoodSize size, IReadOnlyList<FirewoodNode> nodes, int rotations, bool reflection)
        {
            FirewoodNode[] result = new FirewoodNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                HexCoordinate p = nodes[i].Position;
                HexCoordinate d = nodes[i].Outward ?? throw new ArgumentException("对称评分仅接受裂缝端口");
                if (reflection) { p = new HexCoordinate(p.R, p.Q); d = new HexCoordinate(d.R, d.Q); }
                if (size == FirewoodSize.Small)
                {
                    for (int step = 0; step < rotations; step++)
                    {
                        p = new HexCoordinate(1 - p.Q - p.R, p.Q);
                        d = new HexCoordinate(-d.Q - d.R, d.Q);
                    }
                }
                else { p = p.Rotate(rotations); d = d.Rotate(rotations); }
                result[i] = new FirewoodNode(nodes[i].Id, p, FirewoodNodeKind.Crack, d);
            }
            return result;
        }

        static string CanonicalKey(FirewoodSize size, IReadOnlyList<FirewoodNode> nodes)
        {
            return Enumerable.Range(0, size == FirewoodSize.Small ? 3 : 6)
                .Select(i => Key(Transform(size, nodes, i, false))).OrderBy(key => key, StringComparer.Ordinal).First();
        }

        static string Key(IReadOnlyList<FirewoodNode> nodes)
        {
            return string.Join(";", nodes.OrderBy(n => n.Position).Select(n => string.Format(CultureInfo.InvariantCulture,
                "{0},{1}:{2},{3}", n.Position.Q, n.Position.R, n.Outward.Value.Q, n.Outward.Value.R)));
        }

        static string NodeId(string prefix, int index)
        {
            return prefix + "_" + index.ToString(CultureInfo.InvariantCulture);
        }

        static T Choose<T>(IReadOnlyList<T> values, int seed, int stream)
        {
            return values[new Random(Derive(seed, stream)).Next(values.Count)];
        }

        static void Shuffle<T>(IList<T> values, Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int index = random.Next(i + 1);
                (values[i], values[index]) = (values[index], values[i]);
            }
        }

        static int Derive(int seed, int stream)
        {
            unchecked
            {
                uint value = (uint)seed ^ (0x9E3779B9u + (uint)stream * 0x85EBCA6Bu + ContractVersion);
                value = (value ^ value >> 16) * 0x7FEB352Du;
                value = (value ^ value >> 15) * 0x846CA68Bu;
                return (int)(value ^ value >> 16);
            }
        }
    }
}
