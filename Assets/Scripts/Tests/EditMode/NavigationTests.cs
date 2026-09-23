using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class NavigationTests
    {
        MapTestDefinitions _definitions;
        long _memory;
        [SetUp] public void SetUp() { _definitions = new MapTestDefinitions(); _memory = NavigationMemory.Bytes; }
        [TearDown] public void TearDown() { _definitions.Dispose(); Assert.AreEqual(_memory, NavigationMemory.Bytes); }
        MapGeometrySnapshot Geometry(bool wall = true)
        {
            MapDocument doc = _definitions.Document();
            if (wall)
            {
                MapObjectDefinition definition = _definitions.Create<MapObjectDefinition>("nav_wall");
                MapTestDefinitions.Set(definition, "_capabilities", MapObjectCapabilities.Obstacle);
                MapTestDefinitions.Set(definition, "_footprint", new Vector2(.1f, 2));
                doc.Objects = new[] { new MapObjectRecord { ObjectId = "wall", DefinitionId = "map_object:nav_wall", Position = new MapPoint { X = 2, Y = 2 } } };
            }
            var lookup = new MapDefinitionLookup(_definitions.Assets);
            return MapGeometryCompiler.Compile(MapGeometryInput.Capture(MapDocumentCompiler.Compile(doc, lookup), lookup));
        }
        [TestCase(.1)]
        [TestCase(.36)]
        public void BurstGrid_MatchesGeometryEdgesAndFootprints(double radius)
        {
            MapGeometrySnapshot geometry = Geometry();
            using var grid = new NavigationGrid(geometry, radius);
            grid.Complete(); NavigationGridView view = grid.View;
            for (int i = 0; i < view.Count; i++)
                for (int d = 0; d < 8; d++)
                {
                    int2 offset = NavigationGridView.Offset(d);
                    bool expected = geometry.CheckEdge(i % view.Width, i / view.Width, i % view.Width + offset.x, i / view.Width + offset.y,
                        new MapGroundProfile(radius), out int cost) == MapGeometryResult.Valid;
                    Assert.AreEqual(expected, view.Edge(i, d), $"node {i} edge {d}");
                    if (expected) { Assert.AreEqual((ulong)cost, view.Cost(view.Neighbor(i, d), d)); }
                }
        }
        [Test]
        public void DirectQueries_PreserveManagedGeometryAtWallsAndBoundaries()
        {
            using var grid = new NavigationGrid(Geometry(), .36);
            grid.Complete();
            NavigationGridView view = grid.View;
            float2[] points = { new(-.01f, 2), new(.36f, .36f), new(.3601f, .3601f), new(2, 2), new(1.59f, 2), new(1.6f, 2), new(3.5f, 2), new(4.01f, 2) };
            foreach (float2 from in points)
            {
                Assert.AreEqual(view.LegalNode(from), NavigationQueries.LegalNode(view, from));
                foreach (float2 to in points)
                {
                    Assert.AreEqual(view.Segment(from, to, view.Radius), NavigationQueries.Segment(view, from, to, view.Radius) != 0);
                }
            }
        }

        [Test]
        public void FlowAndAStar_AgreeOnDirectedWeightedCostAndPreservePendingFrontier()
        {
            using var grid = new NavigationGrid(Geometry(), .1);
            grid.Complete(); NavigationGridView view = grid.View;
            // Deliberately asymmetric arrival cost: reverse Dijkstra must use the forward edge's destination.
            for (int i = 0; i < view.Count; i++) { view.Costs[i] = (ushort)(1 + (i * 17 % 13)); }
            int goal = view.LegalNode(new float2(3.5f, 2));
            using var flow = new NavigationSearch(view.Count);
            flow.Begin(0, goal, true, 1); flow.Schedule(view, 1); flow.Complete();
            Assert.AreEqual(0, flow.Status); Assert.AreEqual(1, flow.Expanded);
            Finish(flow, view);
            using var path = new NavigationSearch(view.Count);
            foreach (float2 p in new[] { new float2(.5f, 2), new float2(.5f, .5f), new float2(3.5f, .5f), new float2(3.5f, 2) })
            {
                int start = view.LegalNode(p);
                path.Begin(start, goal, false, 1); Finish(path, view);
                Assert.AreEqual(1, path.Status);
                Assert.AreEqual(flow.Distance[start], path.Distance[goal]);
                int current = goal, count = 0;
                while (current != start)
                {
                    Assert.Less(count++, view.Count); int previous = path.Parent[current]; Assert.GreaterOrEqual(previous, 0);
                    Assert.IsTrue(view.Segment(view.Point(previous), view.Point(current), view.Radius)); current = previous;
                }
            }
        }
        [Test]
        public void CompleteBarrier_IsUnreachableAndMemoryAdmissionIsAtomic()
        {
            MapGeometrySnapshot geometry = Geometry(false);
            using var grid = new NavigationGrid(geometry, .1, new[] { new MapGeometryRect(1.9, 0, 2.1, 4) });
            grid.Complete(); NavigationGridView view = grid.View;
            using var search = new NavigationSearch(view.Count);
            search.Begin(view.LegalNode(new float2(.5f, 2)), view.LegalNode(new float2(3.5f, 2)), false, 1);
            Finish(search, view); Assert.AreEqual(2, search.Status);
            long before = NavigationMemory.Bytes;
            Assert.Throws<MapGeometryCapacityException>(() => new NavigationMemory(NavigationMemory.Limit));
            Assert.AreEqual(before, NavigationMemory.Bytes);
        }
        [TestCase(2)]
        [TestCase(1000)]
        public void DenseHash_IsBoundedFiniteAndTwoCoincidentAgentsSeparateOppositely(int count)
        {
            using var grid = new NavigationGrid(Geometry(false), .1); grid.Complete();
            using var samples = new NativeArray<NavigationSample>(count, Allocator.TempJob);
            NativeArray<NavigationSample> writable = samples;
            using var sorted = new NativeArray<NavigationNeighbor>(count, Allocator.TempJob);
            using var velocities = new NativeArray<float2>(count, Allocator.TempJob);
            using var visits = new NativeArray<int>(count, Allocator.TempJob);
            for (int i = 0; i < count; i++)
            {
                writable[i] = new NavigationSample
                {
                    Position = new float2(2, 2),
                    Radius = .1f,
                    CanMove = true,
                    Speed = 2,
                    SeparationRadius = 1,
                    SeparationWeight = 1,
                    Identity = (ulong)i + 1
                };
            }
            JobHandle hash = new NavigationHashJob { Samples = samples, Sorted = sorted }.Schedule();
            new NavigationSteeringJob
            {
                Geometry = grid.View.Geometry,
                Samples = samples,
                Sorted = sorted,
                Velocities = velocities,
                Visits = visits,
                DeltaTime = .02f
            }.Schedule(count, 32, hash).Complete();
            for (int i = 0; i < count; i++)
            { Assert.LessOrEqual(visits[i], 96); Assert.IsTrue(math.all(math.isfinite(velocities[i]))); Assert.LessOrEqual(math.length(velocities[i]), 2.0001f); }
            if (count == 2) { Assert.Less(math.length(velocities[0] + velocities[1]), .0001f); Assert.Greater(math.length(velocities[0]), .1f); }
        }
        [Test]
        public void Steering_StopsDisabledAgentsAndCannotCrossThinObstacle()
        {
            using var grid = new NavigationGrid(Geometry(), .1); grid.Complete();
            using var samples = new NativeArray<NavigationSample>(2, Allocator.TempJob);
            NativeArray<NavigationSample> writable = samples;
            using var sorted = new NativeArray<NavigationNeighbor>(2, Allocator.TempJob);
            using var velocities = new NativeArray<float2>(2, Allocator.TempJob);
            using var visits = new NativeArray<int>(2, Allocator.TempJob);
            writable[0] = new NavigationSample { Position = new float2(1.8f, 2), Radius = .1f, Desired = new float2(1, 0), CanMove = true, Speed = 20, Identity = 1 };
            writable[1] = new NavigationSample { Position = new float2(1, 2), Radius = .1f, Desired = new float2(1, 0), CanMove = false, Speed = 20, Identity = 2 };
            JobHandle hash = new NavigationHashJob { Samples = samples, Sorted = sorted }.Schedule();
            new NavigationSteeringJob
            {
                Geometry = grid.View.Geometry,
                Samples = samples,
                Sorted = sorted,
                Velocities = velocities,
                Visits = visits,
                DeltaTime = .02f
            }.Schedule(2, 1, hash).Complete();
            Assert.AreEqual(float2.zero, velocities[0]); Assert.AreEqual(float2.zero, velocities[1]);
        }
        [Test]
        public void WallContact_OnlyAllowsVerifiedOutwardAttachmentWithoutTeleport()
        {
            using var grid = new NavigationGrid(Geometry(), .1); grid.Complete();
            NavigationGeometryView geometry = grid.View.Geometry;
            var contact = new float2(1.845f, 2);
            Assert.IsFalse(geometry.Segment(contact, contact, .1125));
            Assert.IsTrue(geometry.Escape(contact, new float2(1.79f, 2), .1, .01));
            Assert.IsFalse(geometry.Escape(contact, new float2(1.86f, 2), .1, .01));
            Assert.IsFalse(geometry.Escape(contact, new float2(2.3f, 2), .1, .01));
        }
        [Test]
        public void IsolatedLegalNode_CanResolveAZeroLengthPath()
        {
            using var grid = new NavigationGrid(Geometry(false), .1, new[] { new MapGeometryRect(0, 0, 2.01, 4),
                new MapGeometryRect(2.24, 0, 4, 4), new MapGeometryRect(2.01, 0, 2.24, 2.01), new MapGeometryRect(2.01, 2.24, 2.24, 4) });
            grid.Complete(); NavigationGridView view = grid.View;
            int point = view.LegalNode(new float2(2.125f, 2.125f));
            Assert.GreaterOrEqual(point, 0);
            for (int direction = 0; direction < 8; direction++) { Assert.IsFalse(view.Edge(point, direction)); }
            using var search = new NavigationSearch(view.Count);
            search.Begin(point, point, false, 1); Finish(search, view);
            Assert.AreEqual(1, search.Status); Assert.AreEqual(0, search.Distance[point]);
        }
        static void Finish(NavigationSearch search, NavigationGridView view)
        {
            for (int i = 0; i <= view.Count && search.Status == 0; i++) { search.Schedule(view, 11); search.Complete(); }
            Assert.AreNotEqual(0, search.Status, "bounded search must eventually terminate");
        }
    }
}
