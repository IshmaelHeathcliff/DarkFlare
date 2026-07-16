using System.Collections.Generic;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class GameplayUiFoundationTests
{
    readonly List<Object> _objects = new List<Object>();

    IArchitecture _architecture;

    [SetUp]
    public void SetUp()
    {
        _architecture = GameArchitecture.Interface;
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
        _architecture.Deinit();
        _architecture = null;
    }

    [Test]
    public void InventoryModel_SendsEventsOnlyAfterSuccessfulChanges()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        List<GoldChangedEvent> goldEvents = new List<GoldChangedEvent>();
        List<InventoryChangedEvent> inventoryEvents = new List<InventoryChangedEvent>();
        _architecture.RegisterEvent<GoldChangedEvent>(goldEvents.Add);
        _architecture.RegisterEvent<InventoryChangedEvent>(inventoryEvents.Add);
        ItemInstance item = CreateItem("inventory_item", "测试物品");

        inventory.AddGold(20);
        Assert.IsTrue(inventory.TrySpendGold(5));
        Assert.IsFalse(inventory.TrySpendGold(99));
        Assert.IsTrue(inventory.TryAddItem(item));
        Assert.IsFalse(inventory.TryAddItem(item));
        Assert.IsTrue(inventory.RemoveItem(item));
        Assert.IsFalse(inventory.RemoveItem(item));

        Assert.AreEqual(2, goldEvents.Count);
        Assert.AreEqual(0, goldEvents[0].PreviousGold);
        Assert.AreEqual(20, goldEvents[0].CurrentGold);
        Assert.AreEqual(15, goldEvents[1].CurrentGold);
        Assert.AreEqual(2, inventoryEvents.Count);
        Assert.AreEqual(InventoryChangeType.Added, inventoryEvents[0].ChangeType);
        Assert.AreEqual(InventoryChangeType.Removed, inventoryEvents[1].ChangeType);
    }

    [Test]
    public void HudSnapshot_ReflectsPlayerGoldAndWeapon()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        inventory.AddGold(25);
        CombatActor player = CreatePlayer();
        ItemInstance weapon = CreateItem("hud_weapon", "测试长剑");
        EquipmentChangedEvent equipmentEvent = default;
        int equipmentEventCount = 0;
        _architecture.RegisterEvent<EquipmentChangedEvent>(e =>
        {
            equipmentEvent = e;
            equipmentEventCount++;
        });

        _architecture.GetSystem<CombatSystem>().EquipWeapon(player, weapon);
        HudSnapshot snapshot = _architecture.SendQuery(new GetHudSnapshotQuery());

        Assert.IsTrue(snapshot.HasPlayer);
        Assert.AreEqual(100f, snapshot.CurrentHealth);
        Assert.AreEqual(100f, snapshot.MaxHealth);
        Assert.AreEqual(1f, snapshot.HealthNormalized);
        Assert.AreEqual(25, snapshot.Gold);
        StringAssert.Contains("测试长剑", snapshot.WeaponSummary);
        Assert.AreEqual(1, equipmentEventCount);
        Assert.AreSame(player, equipmentEvent.Actor);
        Assert.AreSame(weapon, equipmentEvent.CurrentWeapon);
    }

    [Test]
    public void CraftingSystem_SendsItemCraftedEventAfterSuccess()
    {
        CraftingDefinition craftingDefinition = CreateScriptableObject<CraftingDefinition>();
        AffixDefinition affixDefinition = CreateScriptableObject<AffixDefinition>();
        SetField(craftingDefinition, "_affixPool", new List<AffixDefinition> { affixDefinition });
        SetField(craftingDefinition, "_addAffixCost", 0);
        ItemInstance item = CreateItem("craft_item", "打造测试物品");
        ItemCraftedEvent craftedEvent = default;
        int eventCount = 0;
        _architecture.RegisterEvent<ItemCraftedEvent>(e =>
        {
            craftedEvent = e;
            eventCount++;
        });
        CraftingSystem crafting = _architecture.GetSystem<CraftingSystem>();
        crafting.Setup(craftingDefinition);

        bool crafted = crafting.Craft(CraftOperation.AddAffix, item, null);

        Assert.IsTrue(crafted);
        Assert.AreEqual(1, eventCount);
        Assert.AreEqual(CraftOperation.AddAffix, craftedEvent.Operation);
        Assert.AreSame(item, craftedEvent.Item);
    }

    CombatActor CreatePlayer()
    {
        GameObject playerObject = new GameObject("HudTestPlayer");
        playerObject.SetActive(false);
        CombatActor actor = playerObject.AddComponent<CombatActor>();
        actor.Configure("hud_test_player", ActorTeam.Player, 100f, new StatBlock(), TagSet.Empty);
        _objects.Add(playerObject);
        playerObject.SetActive(true);
        _architecture.GetSystem<CombatSystem>().RegisterActor(actor);
        return actor;
    }

    ItemInstance CreateItem(string instanceId, string displayName)
    {
        ItemBaseDefinition definition = CreateScriptableObject<ItemBaseDefinition>();
        SetField(definition, "_displayName", displayName);
        return definition.CreateInstance(instanceId, 1, 1, ItemRarity.Normal);
    }

    T CreateScriptableObject<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        _objects.Add(value);
        return value;
    }

    static void SetField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(target, value);
    }
}
