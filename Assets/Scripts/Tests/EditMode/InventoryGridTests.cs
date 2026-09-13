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

    [Test]
    public void TryExchange_ReplacesItemInsideFullGrid()
    {
        InventoryGrid grid = new InventoryGrid(2, 1);
        ItemInstance equippedCandidate = CreateItem(1, 1, "candidate");
        ItemInstance blocker = CreateItem(1, 1, "blocker");
        ItemInstance previousWeapon = CreateItem(1, 1, "previous");
        Assert.IsTrue(grid.TryAdd(equippedCandidate));
        Assert.IsTrue(grid.TryAdd(blocker));

        bool exchanged = grid.TryExchange(equippedCandidate, previousWeapon);

        Assert.IsTrue(exchanged);
        Assert.IsFalse(grid.Placements.ContainsKey(equippedCandidate));
        Assert.AreEqual(new RectInt(0, 0, 1, 1), grid.Placements[previousWeapon]);
        Assert.AreEqual(new RectInt(1, 0, 1, 1), grid.Placements[blocker]);
    }

    [Test]
    public void TryExchange_LeavesGridUnchanged_WhenReplacementDoesNotFit()
    {
        InventoryGrid grid = new InventoryGrid(2, 1);
        ItemInstance equippedCandidate = CreateItem(1, 1, "candidate");
        ItemInstance blocker = CreateItem(1, 1, "blocker");
        ItemInstance oversizedPreviousWeapon = CreateItem(2, 1, "previous");
        Assert.IsTrue(grid.TryAdd(equippedCandidate));
        Assert.IsTrue(grid.TryAdd(blocker));
        RectInt originalCandidatePlacement = grid.Placements[equippedCandidate];
        RectInt originalBlockerPlacement = grid.Placements[blocker];

        bool exchanged = grid.TryExchange(equippedCandidate, oversizedPreviousWeapon);

        Assert.IsFalse(exchanged);
        Assert.AreEqual(originalCandidatePlacement, grid.Placements[equippedCandidate]);
        Assert.AreEqual(originalBlockerPlacement, grid.Placements[blocker]);
        Assert.IsFalse(grid.Placements.ContainsKey(oversizedPreviousWeapon));
    }

    [Test]
    public void StackPickup_FillsExistingStackAndPlacesRemainderWithoutLosingQuantity()
    {
        var fixture = new DarkFlare.Tests.GameArchitectureTestFixture();
        try
        {
            InventoryModel inventory = fixture.Start().GetModel<InventoryModel>();
            ItemInstance first = CreateStack(8);
            ItemInstance incoming = first.BaseDefinition.CreateInstance("incoming", 1, 2);
            Assert.IsTrue(incoming.TrySetQuantity(5));
            Assert.IsTrue(inventory.TryAddItem(first));
            Assert.IsTrue(inventory.TryAddItem(incoming));
            Assert.AreEqual(10, first.Quantity);
            Assert.AreEqual(3, incoming.Quantity);
            Assert.AreEqual(13, inventory.CountItems(first.BaseDefinition));
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(incoming));
        }
        finally { fixture.Stop(); }
    }

    [Test]
    public void StackPickup_WhenRemainderCannotFit_PreservesBothStacks()
    {
        var fixture = new DarkFlare.Tests.GameArchitectureTestFixture();
        try
        {
            InventoryModel inventory = fixture.Start().GetModel<InventoryModel>();
            inventory.RestoreState(new InventoryGrid(1, 1), 0);
            ItemInstance first = CreateStack(8);
            ItemInstance incoming = first.BaseDefinition.CreateInstance("incoming", 1, 2);
            incoming.TrySetQuantity(5);
            Assert.IsTrue(inventory.TryAddItem(first));
            Assert.IsFalse(inventory.TryAddItem(incoming));
            Assert.AreEqual(8, first.Quantity);
            Assert.AreEqual(5, incoming.Quantity);
            incoming.TrySetQuantity(2);
            Assert.IsTrue(inventory.TryAddItem(incoming));
            Assert.AreEqual(10, first.Quantity);
            Assert.IsFalse(inventory.Grid.Placements.ContainsKey(incoming));
        }
        finally { fixture.Stop(); }
    }

    [Test]
    public void StackDrag_MergesAndRemovesEmptySource_EquipmentRemainsSingle()
    {
        var fixture = new DarkFlare.Tests.GameArchitectureTestFixture();
        try
        {
            InventoryModel inventory = fixture.Start().GetModel<InventoryModel>();
            ItemInstance first = CreateStack(3);
            ItemInstance second = first.BaseDefinition.CreateInstance("second", 1, 2);
            second.TrySetQuantity(4);
            inventory.Grid.TryAddAt(first, Vector2Int.zero);
            inventory.Grid.TryAddAt(second, new Vector2Int(1, 0));
            Assert.IsTrue(inventory.CanMoveItem(second, Vector2Int.zero));
            Assert.IsTrue(inventory.TryMoveItem(second, Vector2Int.zero));
            Assert.AreEqual(7, first.Quantity);
            Assert.IsFalse(inventory.Grid.Placements.ContainsKey(second));
            Assert.IsFalse(CreateItem(1, 1, "weapon").TrySetQuantity(2));
        }
        finally { fixture.Stop(); }
    }

    ItemInstance CreateStack(int quantity)
    {
        ItemInstance item = CreateItem(1, 1, "material");
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(ItemBaseDefinition).GetField("_itemType", Flags).SetValue(item.BaseDefinition, ItemType.Material);
        typeof(ItemBaseDefinition).GetField("_stackable", Flags).SetValue(item.BaseDefinition, true);
        typeof(ItemBaseDefinition).GetField("_maxStackSize", Flags).SetValue(item.BaseDefinition, 10);
        Assert.IsTrue(item.TrySetQuantity(quantity));
        Assert.IsFalse(item.BaseDefinition.IsConsumable, "堆叠性不隐含可消耗性");
        return item;
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
