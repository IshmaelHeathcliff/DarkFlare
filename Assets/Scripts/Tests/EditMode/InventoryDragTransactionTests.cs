using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using DarkFlare.Tests;
using NUnit.Framework;
using UnityEngine;

public class InventoryDragTransactionTests
{
    readonly List<Object> _objects = new List<Object>();
    readonly GameArchitectureTestFixture _architectureFixture = new GameArchitectureTestFixture();

    IArchitecture _architecture;

    [SetUp]
    public void SetUp()
    {
        _architecture = _architectureFixture.Start();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
            {
                Object.DestroyImmediate(_objects[i]);
            }
        }

        _objects.Clear();
        _architectureFixture.Stop();
        _architecture = null;
    }

    [Test]
    public void InventoryGrid_MoveAndSwap_UsesExactPlacements()
    {
        InventoryGrid grid = new InventoryGrid(5, 3);
        ItemInstance wide = CreateItem("wide", new Vector2Int(2, 1));
        ItemInstance small = CreateItem("small", Vector2Int.one);
        Assert.IsTrue(grid.TryAddAt(wide, Vector2Int.zero));
        Assert.IsTrue(grid.TryAddAt(small, new Vector2Int(3, 0)));

        Assert.IsTrue(grid.TryMove(wide, new Vector2Int(1, 1), out ItemInstance firstExchange));
        Assert.IsNull(firstExchange);
        Assert.AreEqual(new RectInt(1, 1, 2, 1), grid.Placements[wide]);

        Assert.IsTrue(grid.TryMove(wide, new Vector2Int(3, 0), out ItemInstance secondExchange));
        Assert.AreSame(small, secondExchange);
        Assert.AreEqual(new RectInt(3, 0, 2, 1), grid.Placements[wide]);
        Assert.AreEqual(new RectInt(1, 1, 1, 1), grid.Placements[small]);
    }

    [Test]
    public void InventoryGrid_MoveAcrossMultipleItems_LeavesStateUnchanged()
    {
        InventoryGrid grid = new InventoryGrid(4, 2);
        ItemInstance mover = CreateItem("mover", new Vector2Int(2, 1));
        ItemInstance firstBlocker = CreateItem("first_blocker", Vector2Int.one);
        ItemInstance secondBlocker = CreateItem("second_blocker", Vector2Int.one);
        Assert.IsTrue(grid.TryAddAt(mover, Vector2Int.zero));
        Assert.IsTrue(grid.TryAddAt(firstBlocker, new Vector2Int(2, 0)));
        Assert.IsTrue(grid.TryAddAt(secondBlocker, new Vector2Int(3, 0)));

        bool moved = grid.TryMove(mover, new Vector2Int(2, 0), out ItemInstance exchangedItem);

        Assert.IsFalse(moved);
        Assert.IsNull(exchangedItem);
        Assert.AreEqual(new RectInt(0, 0, 2, 1), grid.Placements[mover]);
        Assert.AreEqual(new RectInt(2, 0, 1, 1), grid.Placements[firstBlocker]);
        Assert.AreEqual(new RectInt(3, 0, 1, 1), grid.Placements[secondBlocker]);
    }

    [Test]
    public void MoveInventoryItemCommand_SendsFinalPlacementEvent()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        ItemInstance item = CreateItem("move_event", new Vector2Int(2, 1));
        Assert.IsTrue(inventory.TryAddItemAt(item, new Vector2Int(2, 2)));
        InventoryChangedEvent? received = null;
        _architecture.RegisterEvent<InventoryChangedEvent>(e => received = e);

        bool moved = _architecture.SendCommand(
            new MoveInventoryItemCommand(item, new Vector2Int(5, 3)));

        Assert.IsTrue(moved);
        Assert.IsTrue(received.HasValue);
        Assert.AreEqual(InventoryChangeType.Moved, received.Value.ChangeType);
        Assert.AreEqual(new RectInt(2, 2, 2, 1), received.Value.PreviousPlacement);
        Assert.AreEqual(new RectInt(5, 3, 2, 1), received.Value.CurrentPlacement);
        Assert.AreEqual(received.Value.CurrentPlacement, inventory.Grid.Placements[item]);
    }

    [Test]
    public void EquipItemFromGridCommand_RequiresPreviousItemToFitSourcePlacement()
    {
        CombatActor actor = CreateActor("precise_equip_actor");
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();
        ItemInstance previousWeapon = CreateItem(
            "previous_weapon",
            new Vector2Int(2, 1),
            ItemType.Weapon,
            EquipmentSlotMask.Weapon);
        ItemInstance candidate = CreateItem(
            "candidate_weapon",
            Vector2Int.one,
            ItemType.Weapon,
            EquipmentSlotMask.Weapon);
        ItemInstance blocker = CreateItem("precise_blocker", Vector2Int.one);
        Assert.IsTrue(inventory.TryAddItemAt(previousWeapon, Vector2Int.zero));
        Assert.IsTrue(_architecture.SendCommand(
            new EquipItemCommand(actor, previousWeapon, EquipmentSlot.Weapon)));
        Assert.IsTrue(inventory.TryAddItemAt(candidate, Vector2Int.zero));
        Assert.IsTrue(inventory.TryAddItemAt(blocker, new Vector2Int(1, 0)));

        Assert.IsFalse(_architecture.SendCommand(
            new EquipItemFromGridCommand(actor, candidate, EquipmentSlot.Weapon)));
        Assert.AreSame(previousWeapon, equipment.GetItem(actor, EquipmentSlot.Weapon));
        Assert.AreEqual(new RectInt(0, 0, 1, 1), inventory.Grid.Placements[candidate]);

        Assert.IsTrue(_architecture.SendCommand(
            new MoveInventoryItemCommand(blocker, new Vector2Int(4, 4))));
        Assert.IsTrue(_architecture.SendCommand(
            new EquipItemFromGridCommand(actor, candidate, EquipmentSlot.Weapon)));
        Assert.AreSame(candidate, equipment.GetItem(actor, EquipmentSlot.Weapon));
        Assert.AreEqual(new RectInt(0, 0, 2, 1), inventory.Grid.Placements[previousWeapon]);
    }

    [Test]
    public void UnequipAndRingMove_UseExplicitTargetsAtomically()
    {
        CombatActor actor = CreateActor("drag_equipment_actor");
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();
        ItemInstance leftRing = CreateItem(
            "left_ring",
            Vector2Int.one,
            ItemType.Accessory,
            EquipmentSlotMask.Rings);
        ItemInstance rightRing = CreateItem(
            "right_ring",
            Vector2Int.one,
            ItemType.Accessory,
            EquipmentSlotMask.Rings);
        Assert.IsTrue(inventory.TryAddItem(leftRing));
        Assert.IsTrue(inventory.TryAddItem(rightRing));
        Assert.IsTrue(_architecture.SendCommand(
            new EquipItemCommand(actor, leftRing, EquipmentSlot.RingLeft)));
        Assert.IsTrue(_architecture.SendCommand(
            new EquipItemCommand(actor, rightRing, EquipmentSlot.RingRight)));
        int equipmentEvents = 0;
        _architecture.RegisterEvent<EquipmentChangedEvent>(_ => equipmentEvents++);

        Assert.IsTrue(_architecture.SendCommand(
            new MoveEquippedItemCommand(actor, EquipmentSlot.RingLeft, EquipmentSlot.RingRight)));
        Assert.AreSame(rightRing, equipment.GetItem(actor, EquipmentSlot.RingLeft));
        Assert.AreSame(leftRing, equipment.GetItem(actor, EquipmentSlot.RingRight));
        Assert.AreEqual(2, equipmentEvents);

        Assert.IsTrue(_architecture.SendCommand(
            new UnequipItemToGridCommand(actor, EquipmentSlot.RingRight, new Vector2Int(7, 4))));
        Assert.IsNull(equipment.GetItem(actor, EquipmentSlot.RingRight));
        Assert.AreEqual(new RectInt(7, 4, 1, 1), inventory.Grid.Placements[leftRing]);
    }

    CombatActor CreateActor(string id)
    {
        GameObject actorObject = new GameObject(id);
        _objects.Add(actorObject);
        CombatActor actor = actorObject.AddComponent<CombatActor>();
        actor.Configure(id, ActorTeam.Player, 100f, new StatBlock(), TagSet.Empty);
        return actor;
    }

    ItemInstance CreateItem(
        string id,
        Vector2Int size,
        ItemType type = ItemType.Material,
        EquipmentSlotMask slots = EquipmentSlotMask.None)
    {
        ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
        _objects.Add(definition);
        SetField(definition, "_id", id);
        SetField(definition, "_displayName", id);
        SetField(definition, "_gridSize", size);
        SetField(definition, "_itemType", type);
        SetField(definition, "_allowedEquipmentSlots", slots);
        return definition.CreateInstance(id, 1, 1);
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"缺少字段 {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
    }
}
