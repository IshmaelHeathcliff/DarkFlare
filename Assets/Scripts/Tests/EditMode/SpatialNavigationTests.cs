using DarkFlare;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class SpatialNavigationTests
    {
        [Test]
        public void Neighbor_UsesVisualDirectionAndProjectionBeforeListOrder()
        {
            var source = new Rect(100, 100, 50, 100);
            Rect[] targets = { new Rect(170, 220, 50, 50), new Rect(250, 120, 50, 50), new Rect(10, 120, 50, 50) };
            Assert.AreEqual(1, SpatialNavigation.FindNeighbor(source, targets, Vector2.right));
            Assert.AreEqual(2, SpatialNavigation.FindNeighbor(source, targets, Vector2.left));
            Assert.AreEqual(0, SpatialNavigation.FindNeighbor(source, targets, Vector2.down));
            Assert.AreEqual(-1, SpatialNavigation.FindNeighbor(source, targets, Vector2.up));
        }

        [Test]
        public void Neighbor_SkipsInvalidGeometryAndKeepsStableTiesWithoutWrapping()
        {
            var source = new Rect(0, 0, 50, 50);
            Rect[] targets = { new Rect(60, 0, 0, 50), new Rect(70, 0, 50, 50), new Rect(70, 0, 50, 50) };
            Assert.AreEqual(1, SpatialNavigation.FindNeighbor(source, targets, Vector2.right));
            Assert.AreEqual(-1, SpatialNavigation.FindNeighbor(source, targets, Vector2.left));
            Assert.AreEqual(-1, SpatialNavigation.FindNeighbor(source, targets, Vector2.zero));
        }
    }
}
