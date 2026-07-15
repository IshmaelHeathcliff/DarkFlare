using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class InventoryGridTests
{
    readonly List<ItemBaseDefinition> _createdDefinitions = new List<ItemBaseDefinition>();

    [TearDown]
    public void TearDown()
    {
        foreach (ItemBaseDefinition definition in _createdDefinitions)
        {
            Object.DestroyImmediate(definition);
        }

        _createdDefinitions.Clear();
    }

    [Test]
    public void TryAdd_PlacesItemAtOrigin_AndMarksExpectedCells()
    {
        InventoryGrid grid = new InventoryGrid(10, 6);
        ItemInstance sword = CreateItem(2, 3, "sword");

        Assert.IsTrue(grid.TryAdd(sword));
        Assert.AreEqual(new RectInt(0, 0, 2, 3), grid.Placements[sword]);

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                Assert.IsTrue(grid.IsOccupied(x, y));
            }
        }

        Assert.IsFalse(grid.IsOccupied(2, 0));
        Assert.IsFalse(grid.IsOccupied(0, 3));
    }

    [Test]
    public void TryAdd_ReturnsFalse_WhenGridIsFull()
    {
        InventoryGrid grid = new InventoryGrid(2, 2);
        Assert.IsTrue(grid.TryAdd(CreateItem(2, 2, "big")));

        Assert.IsFalse(grid.TryAdd(CreateItem(1, 1, "small")));
    }

    [Test]
    public void TryAdd_Succeeds_AfterRemovingBlockingItem()
    {
        InventoryGrid grid = new InventoryGrid(2, 2);
        ItemInstance blocker = CreateItem(2, 2, "blocker");
        Assert.IsTrue(grid.TryAdd(blocker));

        ItemInstance next = CreateItem(1, 1, "next");
        Assert.IsFalse(grid.TryAdd(next));

        Assert.IsTrue(grid.Remove(blocker));
        Assert.IsTrue(grid.TryAdd(next));
    }

    [Test]
    public void TryAdd_TwoItems_DoNotOverlap()
    {
        InventoryGrid grid = new InventoryGrid(4, 2);
        ItemInstance first = CreateItem(2, 2, "first");
        ItemInstance second = CreateItem(2, 2, "second");

        Assert.IsTrue(grid.TryAdd(first));
        Assert.IsTrue(grid.TryAdd(second));

        RectInt firstRect = grid.Placements[first];
        RectInt secondRect = grid.Placements[second];
        Assert.IsFalse(firstRect.Overlaps(secondRect));
    }

    ItemInstance CreateItem(int width, int height, string id)
    {
        ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        typeof(ItemBaseDefinition).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, id);
        typeof(ItemBaseDefinition).GetField("_gridSize", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(definition, new Vector2Int(width, height));
        _createdDefinitions.Add(definition);

        return new ItemInstance(id, definition, ItemRarity.Normal, 1, 0, new List<ModifierInstance>());
    }
}
