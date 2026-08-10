using System.Collections.Generic;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class MerchantGridLayoutTests
{
    [Test]
    public void Pack_IsDeterministicAndKeepsItemsInsideGrid()
    {
        List<Vector2Int> sizes = new List<Vector2Int>
        {
            new Vector2Int(2, 2),
            new Vector2Int(1, 2),
            new Vector2Int(2, 1),
            Vector2Int.one,
        };

        IReadOnlyList<RectInt> first = MerchantGridLayout.Pack(5, sizes, out int firstHeight);
        IReadOnlyList<RectInt> second = MerchantGridLayout.Pack(5, sizes, out int secondHeight);

        Assert.AreEqual(firstHeight, secondHeight);
        Assert.AreEqual(sizes.Count, first.Count);

        for (int i = 0; i < first.Count; i++)
        {
            Assert.AreEqual(first[i], second[i]);
            Assert.GreaterOrEqual(first[i].xMin, 0);
            Assert.LessOrEqual(first[i].xMax, 5);
            Assert.Greater(first[i].height, 0);
        }
    }

    [Test]
    public void Pack_DoesNotOverlapAndClampsOversizedWidth()
    {
        List<Vector2Int> sizes = new List<Vector2Int>
        {
            new Vector2Int(8, 1),
            new Vector2Int(2, 2),
            new Vector2Int(3, 1),
            new Vector2Int(1, 3),
        };

        IReadOnlyList<RectInt> placements = MerchantGridLayout.Pack(5, sizes, out int height);

        Assert.AreEqual(5, placements[0].width);
        Assert.GreaterOrEqual(height, 1);

        for (int i = 0; i < placements.Count; i++)
        {
            for (int j = i + 1; j < placements.Count; j++)
            {
                Assert.IsFalse(placements[i].Overlaps(placements[j]));
            }
        }
    }
}
