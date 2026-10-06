using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class SkillBoardGeometryTests
    {
        [Test]
        public void Coordinates_PreserveDistanceUnderRotationAndTranslation()
        {
            HexCoordinate first = new HexCoordinate(-7, 11);
            HexCoordinate second = new HexCoordinate(3, -5);
            HexCoordinate offset = new HexCoordinate(-40, 17);
            for (int step = -6; step <= 6; step++)
            {
                Assert.AreEqual(first.DistanceTo(second), first.Rotate(step).Add(offset).DistanceTo(second.Rotate(step).Add(offset)));
            }
            Assert.AreEqual(first, first.Rotate(6));
            Assert.AreEqual(first, first.Rotate(-6));
            Assert.AreEqual(1, new HexCoordinate(0, 1).DistanceTo(new HexCoordinate(1, 0)));
        }

        [Test]
        public void Coordinates_RejectOverflowInsteadOfWrappingIntoLegalSpace()
        {
            Assert.Throws<OverflowException>(() => new HexCoordinate(int.MaxValue, int.MaxValue));
            Assert.Throws<OverflowException>(() => new HexCoordinate(int.MaxValue, 0).Add(new HexCoordinate(1, 0)));
            Assert.Throws<OverflowException>(() => new HexCoordinate(int.MinValue, 1).Rotate(2));
            Assert.Throws<OverflowException>(() => new HexCoordinate(int.MaxValue, 0).DistanceTo(new HexCoordinate(-1, 0)));
        }

        [Test]
        public void FixedShapes_ReserveEmptyCellsAndExposeSixIndependentRoots()
        {
            Assert.AreEqual(19, SkillBoardGeometry.Body(FirewoodSize.Large).Count);
            Assert.AreEqual(7, SkillBoardGeometry.Body(FirewoodSize.Medium).Count);
            Assert.AreEqual(7, SkillBoardGeometry.CenterReservation.Count);
            Assert.IsTrue(SkillBoardGeometry.Body(FirewoodSize.Medium).Contains(default));
            IReadOnlyList<HexCoordinate> small = SkillBoardGeometry.Body(FirewoodSize.Small);
            Assert.AreEqual(3, small.Count);
            for (int i = 0; i < 3; i++)
            {
                for (int j = i + 1; j < 3; j++) { Assert.AreEqual(1, small[i].DistanceTo(small[j])); }
            }
            Assert.AreEqual(6, SkillBoardGeometry.Roots.Distinct().Count());
            Assert.IsTrue(SkillBoardGeometry.Roots.All(p => p.DistanceTo(default) == 16));
        }

        [Test]
        public void Generator_CoversEveryLegalCountCombinationWithoutClamping()
        {
            foreach (FirewoodSize size in Enum.GetValues(typeof(FirewoodSize)))
            {
                int minimum = size == FirewoodSize.Large ? 9 : size == FirewoodSize.Medium ? 4 : 2;
                int maximum = size == FirewoodSize.Large ? 15 : size == FirewoodSize.Medium ? 6 : 3;
                for (int n = minimum; n <= maximum; n++)
                {
                    for (int c = 1; c <= 6; c++)
                    {
                        for (int l = size == FirewoodSize.Large ? 1 : 0; l <= (size == FirewoodSize.Large ? 3 : 0); l++)
                        {
                            if (!FirewoodGenerator.IsLegalCounts(size, n, c, l)) { continue; }
                            FirewoodGenerationResult result = FirewoodGenerator.Generate(size, -71, n, c, l);
                            Assert.IsTrue(result.Success, $"{size}/{n}/{c}/{l}: {string.Join(";", result.Issues)}");
                            Assert.AreEqual(n, result.Layout.Nodes.Count);
                            Assert.AreEqual(c, result.Layout.Nodes.Count(p => p.Kind == FirewoodNodeKind.Crack));
                            if (size == FirewoodSize.Large)
                            {
                                Assert.AreEqual(l, result.Layout.Nodes.Count(p => p.Position.DistanceTo(default) == 1));
                            }
                        }
                    }
                }
            }
            FirewoodLayout twoCracks = FirewoodGenerator.Generate(FirewoodSize.Small, 15, 2, 2).Layout;
            Assert.IsTrue(twoCracks.Nodes.All(n => n.Kind == FirewoodNodeKind.Crack));
        }

        [TestCase(FirewoodSize.Large, 9, 5, 1)]
        [TestCase(FirewoodSize.Large, 15, 6, 1)]
        [TestCase(FirewoodSize.Medium, 4, 3, 0)]
        [TestCase(FirewoodSize.Small, 3, 3, 0)]
        [TestCase(FirewoodSize.Small, -1, 2, 0)]
        public void Generator_RejectsIllegalRequests(FirewoodSize size, int n, int c, int l)
        {
            FirewoodGenerationResult result = FirewoodGenerator.Generate(size, 0, n, c, l);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Layout);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "counts"));
        }

        [Test]
        public void Generator_UsesExplicitSmallTemplatesAndRejectsInwardDirections()
        {
            HexDirectionMask[] directions = { HexDirectionMask.West, HexDirectionMask.NorthEast, HexDirectionMask.SouthEast };
            FirewoodGenerationResult valid = FirewoodGenerator.Generate(FirewoodSize.Small, 0, 2, 2, smallDirections: directions);
            Assert.IsTrue(valid.Success);
            Assert.IsTrue(valid.Layout.Nodes.All(n => !valid.Layout.Body.Contains(n.Position.Add(n.Outward.Value))));
            directions[0] = HexDirectionMask.East;
            Assert.IsFalse(FirewoodGenerator.Generate(FirewoodSize.Small, 0, smallDirections: directions).Success);
            Assert.IsFalse(FirewoodGenerator.Generate(FirewoodSize.Small, 0, smallDirections: Array.Empty<HexDirectionMask>()).Success);
        }

        [TestCase(2, 1, 2, 4)]
        [TestCase(3, 2, 3, 4)]
        [TestCase(4, 1, 2, 2)]
        public void Symmetry_PrefersOppositeAndAlternatingCorners(int count, int rotations, int reflections, int distance)
        {
            FirewoodGenerationResult result = FirewoodGenerator.Generate(FirewoodSize.Large, 77, 12, count);
            Assert.AreEqual(rotations, result.Symmetry.Rotations);
            Assert.AreEqual(reflections, result.Symmetry.Reflections);
            Assert.AreEqual(distance, result.Symmetry.MinimumDistance);
            Assert.Greater(result.CandidateCount, result.BestCandidateCount);
            Assert.AreEqual(1, result.RotationClassCount);
        }

        [Test]
        public void Symmetry_AgreesWithIndependentCornerEnumeration()
        {
            for (int count = 1; count <= 6; count++)
            {
                int bestRotations = -1;
                int bestReflections = -1;
                for (int mask = 1; mask < 64; mask++)
                {
                    List<int> corners = Enumerable.Range(0, 6).Where(i => (mask & (1 << i)) != 0).ToList();
                    if (corners.Count != count) { continue; }
                    int rotations = Enumerable.Range(1, 5).Count(step => corners.All(i => corners.Contains((i + step) % 6)));
                    int reflections = Enumerable.Range(0, 6).Count(axis => corners.All(i => corners.Contains((axis - i + 6) % 6)));
                    if (rotations > bestRotations || (rotations == bestRotations && reflections > bestReflections))
                    {
                        bestRotations = rotations;
                        bestReflections = reflections;
                    }
                }
                FirewoodSymmetryScore score = FirewoodGenerator.Generate(FirewoodSize.Large, 99, 15, count, 3).Symmetry;
                Assert.AreEqual(bestRotations, score.Rotations);
                Assert.AreEqual(bestReflections, score.Reflections);
            }
        }

        [Test]
        public void GenerationAndPlacement_AreDeterministicAndDoNotAdvanceGameplayRandom()
        {
            UnityEngine.Random.State original = UnityEngine.Random.state;
            CultureInfo culture = CultureInfo.CurrentCulture;
            try
            {
                var gameplay = new GameplayRandomSystem();
                gameplay.Configure(true, 321);
                GameplayRandomState before = gameplay.CaptureState();
                UnityEngine.Random.InitState(98);
                float expected = UnityEngine.Random.value;
                UnityEngine.Random.InitState(98);
                FirewoodLayout first = FirewoodGenerator.Generate(FirewoodSize.Large, int.MinValue).Layout;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                FirewoodLayout second = FirewoodGenerator.Generate(FirewoodSize.Large, int.MinValue).Layout;
                Assert.AreEqual(Signature(first), Signature(second));
                foreach (int rotation in Enumerable.Range(0, 6))
                {
                    var placement = new FirewoodPlacement("a", first, new HexCoordinate(16, 0), rotation);
                    SkillBoardPlacementValidator.Validate(new[] { placement }, Array.Empty<FirewoodConnection>());
                }
                Assert.AreEqual(expected, UnityEngine.Random.value);
                CollectionAssert.AreEqual(before.NextSequences, gameplay.CaptureState().NextSequences);
                Assert.AreEqual(Signature(first), Signature(second));
            }
            finally { UnityEngine.Random.state = original; CultureInfo.CurrentCulture = culture; }
        }

        [Test]
        public void Graph_CrossesEmptyRingCellsWithoutCreatingNodes()
        {
            FirewoodLayout layout = FirewoodGenerator.Generate(FirewoodSize.Large, 82, 9, 1, 1).Layout;
            var nodes = layout.Nodes.ToDictionary(n => n.Id);
            Assert.IsTrue(layout.Edges.Any(e => nodes.ContainsKey(e.First) && nodes.ContainsKey(e.Second)
                && nodes[e.First].Position.DistanceTo(nodes[e.Second].Position) > 1));
            Assert.IsEmpty(layout.Validate());
            Assert.AreEqual(9, layout.Nodes.Count);
            Assert.AreEqual(19, layout.Body.Count);
        }

        [Test]
        public void LayoutValidation_RejectsIdentityPortAndGraphCorruption()
        {
            FirewoodLayout original = FirewoodGenerator.Generate(FirewoodSize.Large, 13, 9, 1, 1).Layout;
            FirewoodNode crack = original.Nodes.Single(n => n.Kind == FirewoodNodeKind.Crack);
            FirewoodNode[] corrupt = original.Nodes.Select(n => n.Id == crack.Id
                ? new FirewoodNode(n.Id, new HexCoordinate(1, 1), n.Kind, n.Outward) : n).ToArray();
            var invalid = new FirewoodLayout(original.Size, original.Seed, original.GenerationVersion, corrupt,
                original.Edges.Concat(new[] { new FirewoodEdge("missing", crack.Id) }));
            Assert.IsTrue(invalid.Validate().Any(i => i.Code == "port_direction"));
            Assert.IsTrue(invalid.Validate().Any(i => i.Code == "edge_endpoint"));
            var duplicate = new FirewoodLayout(original.Size, 0, original.GenerationVersion,
                original.Nodes.Concat(new[] { original.Nodes[0] }), original.Edges);
            Assert.IsTrue(duplicate.Validate().Any(i => i.Code == "node_id"));
            var directCenter = new FirewoodLayout(original.Size, 0, original.GenerationVersion, original.Nodes,
                original.Edges.Concat(new[] { new FirewoodEdge(SkillBoardGeometry.CenterNodeId, crack.Id) }));
            Assert.IsTrue(directCenter.Validate().Any(i => i.Code == "center_edge"));
            var outside = new FirewoodLayout(FirewoodSize.Large, 0, original.GenerationVersion,
                original.Nodes.Select(n => n.Id == crack.Id
                    ? new FirewoodNode(n.Id, new HexCoordinate(int.MinValue, 1), n.Kind, n.Outward) : n), original.Edges);
            Assert.IsTrue(outside.Validate().Any(i => i.Code == "node_position"));
        }

        [Test]
        public void Layout_CopiesCallerArraysAndRetainsIdentityAcrossSixPoses()
        {
            FirewoodLayout original = FirewoodGenerator.Generate(FirewoodSize.Medium, 7).Layout;
            FirewoodNode[] nodes = original.Nodes.ToArray();
            FirewoodEdge[] edges = original.Edges.ToArray();
            var copied = new FirewoodLayout(original.Size, original.Seed, original.GenerationVersion, nodes, edges);
            nodes[0] = default;
            edges[0] = default;
            Assert.AreEqual(Signature(original), Signature(copied));
            var start = new FirewoodPlacement("same", copied, new HexCoordinate(-16, 0));
            var restored = new FirewoodPlacement("same", copied, start.Anchor, 6);
            CollectionAssert.AreEqual(copied.Nodes.Select(n => start.ToWorld(n.Position)), copied.Nodes.Select(n => restored.ToWorld(n.Position)));
            Assert.AreSame(copied, restored.Layout);
        }

        [Test]
        public void DirectConnection_MergesIntoCracksAndDisconnectCannotLeaveOverlap()
        {
            FirewoodPlacement first = Facing("a", FirewoodSize.Large, new HexCoordinate(16, 0), 0);
            FirewoodPlacement second = Facing("b", FirewoodSize.Medium, new HexCoordinate(20, 0), 3);
            FirewoodPlacement[] placements = { first, second };
            IReadOnlyList<FirewoodConnection> connections = SkillBoardPlacementValidator.FindConnections(placements);
            Assert.AreEqual(1, connections.Count);
            SkillBoardPlacementResult connected = SkillBoardPlacementValidator.Validate(placements, connections, true);
            Assert.IsTrue(connected.Success, string.Join(";", connected.Issues));
            Assert.AreEqual(26, connected.OccupiedCells.Count);
            Assert.IsTrue(connected.OccupiedCells.Contains(new HexCoordinate(18, 0)));
            Assert.IsTrue(connected.OccupiedCells.Contains(new HexCoordinate(19, 0)));
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(placements, Array.Empty<FirewoodConnection>()).Issues.Any(i => i.Code == "extension_body"));
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(placements, new[] { connections[0], connections[0] }).Issues.Any(i => i.Code == "port_in_use"));
        }

        [Test]
        public void OldSharedMiddleCell_AndUnpairedSingleDirection_AreRejected()
        {
            FirewoodPlacement first = Facing("a", FirewoodSize.Large, new HexCoordinate(16, 0), 0);
            FirewoodPlacement second = Facing("b", FirewoodSize.Medium, new HexCoordinate(21, 0), 3);
            FirewoodPlacement[] placements = { first, second };
            Assert.IsEmpty(SkillBoardPlacementValidator.FindConnections(placements));
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(placements, Array.Empty<FirewoodConnection>()).Issues.Any(i => i.Code == "extension_overlap"));
            FirewoodPortReference firstPort = Port(first);
            FirewoodPortReference secondPort = Port(second);
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(placements,
                new[] { new FirewoodConnection(firstPort, secondPort) }).Issues.Any(i => i.Code == "connection_direction"));
            FirewoodPlacement wrongDirection = Facing("b", FirewoodSize.Medium, new HexCoordinate(20, 0), 0);
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(new[] { first, wrongDirection },
                new[] { new FirewoodConnection(firstPort, Port(wrongDirection)) }).Issues.Any(i => i.Code == "connection_direction"));
        }

        [Test]
        public void Placement_ReservesCenterBlankBodiesAndUnconnectedExtensions()
        {
            FirewoodPlacement center = Facing("a", FirewoodSize.Medium, default, 0);
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(new[] { center }, Array.Empty<FirewoodConnection>()).Issues.Any(i => i.Code == "center_reserved"));
            FirewoodPlacement large = Facing("a", FirewoodSize.Large, new HexCoordinate(16, 0), 0);
            FirewoodPlacement inBlank = Facing("b", FirewoodSize.Medium, new HexCoordinate(16, 0), 0);
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(new[] { large, inBlank }, Array.Empty<FirewoodConnection>()).Issues.Any(i => i.Code == "body_overlap"));
            FirewoodPlacement nearCenter = Facing("a", FirewoodSize.Medium, new HexCoordinate(3, 0), 3);
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(new[] { nearCenter }, Array.Empty<FirewoodConnection>()).Issues.Any(i => i.Code == "center_reserved"));
            FirewoodPlacement far = Facing("a", FirewoodSize.Large, new HexCoordinate(40, 0), 0);
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(new[] { far }, Array.Empty<FirewoodConnection>()).Success);
            Assert.IsFalse(SkillBoardPlacementValidator.Validate(new[] { far }, Array.Empty<FirewoodConnection>(), true).Success);
        }

        [Test]
        public void SmallToSmall_CanConnectWhileSameLargerSizesCannot()
        {
            FirewoodLayout layout = FirewoodGenerator.Generate(FirewoodSize.Small, 24, 2, 1).Layout;
            var first = new FirewoodPlacement("a", layout, new HexCoordinate(32, 0));
            FirewoodNode node = layout.Nodes.Single(n => n.Kind == FirewoodNodeKind.Crack);
            var second = new FirewoodPlacement("b", layout,
                first.ToWorld(node.Position.Add(node.Outward.Value)).Subtract(node.Position.Rotate(3)), 3);
            FirewoodPlacement[] placements = { first, second };
            SkillBoardPlacementResult result = SkillBoardPlacementValidator.Validate(placements, SkillBoardPlacementValidator.FindConnections(placements));
            Assert.IsTrue(result.Success, string.Join(";", result.Issues));
            FirewoodPlacement mediumA = Facing("a", FirewoodSize.Medium, new HexCoordinate(16, 0), 0);
            FirewoodPlacement mediumB = Facing("b", FirewoodSize.Medium, new HexCoordinate(19, 0), 3);
            FirewoodPlacement[] mediums = { mediumA, mediumB };
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(mediums, SkillBoardPlacementValidator.FindConnections(mediums)).Issues.Any(i => i.Code == "connection_size"));
        }

        [Test]
        public void ClosedNetwork_AcceptsSixPiecesWithoutChoosingAParentTree()
        {
            var layout = new FirewoodLayout(FirewoodSize.Small, 0, FirewoodGenerator.ContractVersion,
                new[]
                {
                    new FirewoodNode("a", new HexCoordinate(0, 0), FirewoodNodeKind.Crack, new HexCoordinate(-1, 0)),
                    new FirewoodNode("b", new HexCoordinate(1, 0), FirewoodNodeKind.Crack, new HexCoordinate(1, -1)),
                    new FirewoodNode("c", new HexCoordinate(0, 1), FirewoodNodeKind.Talent),
                },
                new[] { new FirewoodEdge("a", "b"), new FirewoodEdge("b", "c"), new FirewoodEdge("c", "a") });
            Assert.IsEmpty(layout.Validate());
            FirewoodPlacement[] placements = Enumerable.Range(0, 6).Select(i => new FirewoodPlacement(
                "wood_" + i, layout, new HexCoordinate(1, 1).Rotate(i).Add(new HexCoordinate(40, 0)), 5 + i)).ToArray();
            IReadOnlyList<FirewoodConnection> connections = SkillBoardPlacementValidator.FindConnections(placements);
            Assert.AreEqual(6, connections.Count);
            SkillBoardPlacementResult result = SkillBoardPlacementValidator.Validate(placements, connections);
            Assert.IsTrue(result.Success, string.Join(";", result.Issues));
            Assert.AreEqual(18, result.OccupiedCells.Count);
            foreach (FirewoodPlacement placement in placements)
            {
                Assert.AreEqual(2, connections.Count(c => c.First.PlacementId == placement.Id || c.Second.PlacementId == placement.Id));
            }
        }

        [Test]
        public void Configuration_GeneratesIndependentSnapshotsAndReportsInvalidTemplates()
        {
            var definition = ScriptableObject.CreateInstance<FirewoodGenerationDefinition>();
            try
            {
                var serialized = new UnityEditor.SerializedObject(definition);
                serialized.FindProperty("_size").enumValueIndex = (int)FirewoodSize.Small;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                string configuration = JsonUtility.ToJson(definition);
                FirewoodLayout first = definition.Generate(123, 2, 2).Layout;
                FirewoodLayout second = definition.Generate(123, 2, 2).Layout;
                Assert.AreEqual(Signature(first), Signature(second));
                Assert.AreNotSame(first, second);
                Assert.AreEqual(configuration, JsonUtility.ToJson(definition));
                serialized.FindProperty("_smallPortDirections").GetArrayElementAtIndex(0).intValue = (int)HexDirectionMask.East;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(definition.ValidateConfiguration().Any(i => i.Subject == "_smallPortDirections[0]"));
                Assert.IsFalse(definition.Generate(123).Success);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void PlacementFailures_DoNotMutateInputsAndReportOverflowOrIdentity()
        {
            FirewoodPlacement placement = Facing("a", FirewoodSize.Large, new HexCoordinate(16, 0), 0);
            string before = Signature(placement.Layout);
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(new[] { placement, placement }, Array.Empty<FirewoodConnection>()).Issues.Any(i => i.Code == "placement_id"));
            var overflow = new FirewoodPlacement("edge", placement.Layout, new HexCoordinate(int.MaxValue, 0));
            Assert.IsTrue(SkillBoardPlacementValidator.Validate(new[] { overflow }, Array.Empty<FirewoodConnection>()).Issues.Any(i => i.Code == "coordinate_overflow"));
            Assert.AreEqual(before, Signature(placement.Layout));
            Assert.AreEqual(new HexCoordinate(16, 0), placement.Anchor);
        }

        static FirewoodPlacement Facing(string id, FirewoodSize size, HexCoordinate anchor, int direction)
        {
            FirewoodLayout layout = FirewoodGenerator.Generate(size, 41, crackCount: 1).Layout;
            FirewoodNode crack = layout.Nodes.Single(n => n.Kind == FirewoodNodeKind.Crack);
            int rotation = Enumerable.Range(0, 6).Single(r => crack.Outward.Value.Rotate(r).Equals(SkillBoardGeometry.NeighborDirections[direction]));
            return new FirewoodPlacement(id, layout, anchor, rotation);
        }

        static FirewoodPortReference Port(FirewoodPlacement placement)
        {
            return new FirewoodPortReference(placement.Id, placement.Layout.Nodes.Single(n => n.Kind == FirewoodNodeKind.Crack).Id);
        }

        static string Signature(FirewoodLayout layout)
        {
            return string.Join(";", layout.Nodes.Select(n => $"{n.Id}:{n.Position}:{n.Kind}:{n.Outward}"))
                + "|" + string.Join(";", layout.Edges.Select(e => $"{e.First}:{e.Second}"));
        }
    }
}
