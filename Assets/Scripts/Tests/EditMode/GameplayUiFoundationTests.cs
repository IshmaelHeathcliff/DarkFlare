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
        Assert.IsTrue(inventory.TryAddItem(weapon));
        EquipmentChangedEvent equipmentEvent = default;
        int equipmentEventCount = 0;
        _architecture.RegisterEvent<EquipmentChangedEvent>(e =>
        {
            equipmentEvent = e;
            equipmentEventCount++;
        });

        bool equipped = _architecture.SendCommand(new EquipItemCommand(player, weapon));
        HudSnapshot snapshot = _architecture.SendQuery(new GetHudSnapshotQuery());

        Assert.IsTrue(equipped);
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
    public void EquipItemCommand_SwapsWeapons_AndRejectsInvalidItems()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();
        CombatActor player = CreatePlayer();
        ItemInstance firstWeapon = CreateItem("first_weapon", "第一把武器");
        ItemInstance secondWeapon = CreateItem("second_weapon", "第二把武器");
        ItemInstance armor = CreateItem("armor", "测试护甲", ItemType.Armor);
        int equipmentEventCount = 0;
        _architecture.RegisterEvent<EquipmentChangedEvent>(_ => equipmentEventCount++);
        Assert.IsTrue(inventory.TryAddItem(firstWeapon));

        bool firstEquipped = _architecture.SendCommand(new EquipItemCommand(player, firstWeapon));
        Assert.IsTrue(inventory.TryAddItem(secondWeapon));
        bool secondEquipped = _architecture.SendCommand(new EquipItemCommand(player, secondWeapon));
        Assert.IsTrue(inventory.TryAddItem(armor));
        bool armorEquipped = _architecture.SendCommand(new EquipItemCommand(player, armor));
        ItemInstance detachedWeapon = CreateItem("detached_weapon", "不在背包的武器");
        bool detachedEquipped = _architecture.SendCommand(new EquipItemCommand(player, detachedWeapon));

        Assert.IsTrue(firstEquipped);
        Assert.IsTrue(secondEquipped);
        Assert.IsFalse(armorEquipped);
        Assert.IsFalse(detachedEquipped);
        Assert.AreSame(secondWeapon, equipment.GetWeapon(player));
        Assert.IsTrue(inventory.Grid.Placements.ContainsKey(firstWeapon));
        Assert.IsFalse(inventory.Grid.Placements.ContainsKey(secondWeapon));
        Assert.IsTrue(inventory.Grid.Placements.ContainsKey(armor));
        Assert.AreEqual(2, equipmentEventCount);
    }

    [Test]
    public void EquipItemCommand_RollsBack_WhenPreviousWeaponDoesNotFit()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EquipmentModel equipment = _architecture.GetModel<EquipmentModel>();
        CombatActor player = CreatePlayer();
        ItemInstance previousWeapon = CreateItem(
            "previous_large_weapon",
            "旧大型武器",
            ItemType.Weapon,
            new Vector2Int(2, 1));
        ItemInstance candidate = CreateItem("candidate_weapon", "待装备武器");
        Assert.IsTrue(inventory.TryAddItem(previousWeapon));
        Assert.IsTrue(_architecture.SendCommand(new EquipItemCommand(player, previousWeapon)));
        Assert.IsTrue(inventory.TryAddItem(candidate));

        for (int i = 0; i < 59; i++)
        {
            Assert.IsTrue(inventory.TryAddItem(CreateItem($"blocker_{i}", $"占位物 {i}")));
        }

        RectInt candidatePlacement = inventory.Grid.Placements[candidate];
        int inventoryEventCount = 0;
        int equipmentEventCount = 0;
        _architecture.RegisterEvent<InventoryChangedEvent>(_ => inventoryEventCount++);
        _architecture.RegisterEvent<EquipmentChangedEvent>(_ => equipmentEventCount++);

        bool equipped = _architecture.SendCommand(new EquipItemCommand(player, candidate));

        Assert.IsFalse(equipped);
        Assert.AreSame(previousWeapon, equipment.GetWeapon(player));
        Assert.AreEqual(candidatePlacement, inventory.Grid.Placements[candidate]);
        Assert.IsFalse(inventory.Grid.Placements.ContainsKey(previousWeapon));
        Assert.AreEqual(0, inventoryEventCount);
        Assert.AreEqual(0, equipmentEventCount);
    }

    [Test]
    public void InventorySnapshot_ContainsOrderedPlacementsAndDisplayData()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        CombatActor player = CreatePlayer();
        ItemInstance weapon = CreateItem("snapshot_weapon", "快照武器", ItemType.Weapon, new Vector2Int(2, 3));
        ItemInstance armor = CreateItem("snapshot_armor", "快照护甲", ItemType.Armor);
        Assert.IsTrue(inventory.TryAddItem(weapon));
        Assert.IsTrue(inventory.TryAddItem(armor));

        InventorySnapshot snapshot = _architecture.SendQuery(new GetInventorySnapshotQuery());

        Assert.AreSame(player, snapshot.Player);
        Assert.AreEqual(10, snapshot.Width);
        Assert.AreEqual(6, snapshot.Height);
        Assert.AreEqual(2, snapshot.Items.Count);
        Assert.AreSame(weapon, snapshot.Items[0].Item);
        Assert.AreEqual(new RectInt(0, 0, 2, 3), snapshot.Items[0].Placement);
        Assert.AreEqual("快照武器", snapshot.Items[0].DisplayName);
        Assert.IsTrue(snapshot.Items[0].CanEquip);
        Assert.AreSame(armor, snapshot.Items[1].Item);
        Assert.AreEqual(new RectInt(2, 0, 1, 1), snapshot.Items[1].Placement);
        Assert.IsFalse(snapshot.Items[1].CanEquip);
        Assert.AreEqual("未装备", snapshot.CurrentWeaponSummary);
    }

    [Test]
    public void ShopSnapshot_ContainsMerchantAndPlayerPrices()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EconomyModel economy = _architecture.GetModel<EconomyModel>();
        ItemInstance merchantItem = CreateItem("merchant_item", "商人长剑", baseValue: 10);
        ItemInstance playerItem = CreateItem("player_item", "玩家护甲", ItemType.Armor, baseValue: 20);
        economy.AddStock(merchantItem);
        inventory.AddGold(100);
        Assert.IsTrue(inventory.TryAddItem(playerItem));

        ShopSnapshot snapshot = _architecture.SendQuery(new GetShopSnapshotQuery());

        Assert.AreEqual(100, snapshot.Gold);
        Assert.AreEqual(1, snapshot.MerchantItems.Count);
        Assert.AreSame(merchantItem, snapshot.MerchantItems[0].Item);
        Assert.AreEqual(ShopItemSource.Merchant, snapshot.MerchantItems[0].Source);
        Assert.AreEqual(15, snapshot.MerchantItems[0].Price);
        Assert.AreEqual(1, snapshot.PlayerItems.Count);
        Assert.AreSame(playerItem, snapshot.PlayerItems[0].Item);
        Assert.AreEqual(ShopItemSource.Player, snapshot.PlayerItems[0].Source);
        Assert.AreEqual(8, snapshot.PlayerItems[0].Price);
    }

    [Test]
    public void TradingCommands_MoveItemsAndSendCompletedEvents()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EconomyModel economy = _architecture.GetModel<EconomyModel>();
        ItemInstance item = CreateItem("trade_item", "交易长剑", baseValue: 10);
        List<TradeCompletedEvent> tradeEvents = new List<TradeCompletedEvent>();
        _architecture.RegisterEvent<TradeCompletedEvent>(tradeEvents.Add);
        economy.AddStock(item);
        inventory.AddGold(100);

        bool bought = _architecture.SendCommand(new BuyItemCommand(item));

        Assert.IsTrue(bought);
        Assert.IsFalse(economy.HasStock(item));
        Assert.IsTrue(inventory.Grid.Placements.ContainsKey(item));
        Assert.AreEqual(85, inventory.Gold);
        Assert.AreEqual(1, tradeEvents.Count);
        Assert.AreEqual(TradeOperation.Buy, tradeEvents[0].Operation);
        Assert.AreEqual(15, tradeEvents[0].Price);

        bool sold = _architecture.SendCommand(new SellItemCommand(item));

        Assert.IsTrue(sold);
        Assert.IsFalse(inventory.Grid.Placements.ContainsKey(item));
        Assert.AreEqual(89, inventory.Gold);
        Assert.AreEqual(2, tradeEvents.Count);
        Assert.AreEqual(TradeOperation.Sell, tradeEvents[1].Operation);
        Assert.AreEqual(4, tradeEvents[1].Price);
    }

    [Test]
    public void BuyItemCommand_FailuresLeaveAllStateUnchanged()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        EconomyModel economy = _architecture.GetModel<EconomyModel>();
        ItemInstance absentItem = CreateItem("absent_item", "无库存物品", baseValue: 10);
        ItemInstance expensiveItem = CreateItem("expensive_item", "金币不足物品", baseValue: 10);
        ItemInstance fullBagItem = CreateItem("full_bag_item", "背包已满物品", baseValue: 10);
        int tradeEventCount = 0;
        _architecture.RegisterEvent<TradeCompletedEvent>(_ => tradeEventCount++);
        economy.AddStock(expensiveItem);

        Assert.IsFalse(_architecture.SendCommand(new BuyItemCommand(absentItem)));
        Assert.IsFalse(_architecture.SendCommand(new BuyItemCommand(expensiveItem)));
        Assert.IsTrue(economy.HasStock(expensiveItem));
        Assert.AreEqual(0, inventory.Gold);

        for (int i = 0; i < 60; i++)
        {
            Assert.IsTrue(inventory.TryAddItem(CreateItem($"trade_blocker_{i}", $"交易占位物 {i}")));
        }

        economy.AddStock(fullBagItem);
        inventory.AddGold(100);

        Assert.IsFalse(_architecture.SendCommand(new BuyItemCommand(fullBagItem)));
        Assert.IsTrue(economy.HasStock(fullBagItem));
        Assert.IsFalse(inventory.Grid.Placements.ContainsKey(fullBagItem));
        Assert.AreEqual(100, inventory.Gold);
        Assert.AreEqual(0, tradeEventCount);
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

    ItemInstance CreateItem(
        string instanceId,
        string displayName,
        ItemType itemType = ItemType.Weapon,
        Vector2Int? gridSize = null,
        int baseValue = 0)
    {
        ItemBaseDefinition definition = CreateScriptableObject<ItemBaseDefinition>();
        SetField(definition, "_displayName", displayName);
        SetField(definition, "_itemType", itemType);
        SetField(definition, "_gridSize", gridSize ?? Vector2Int.one);
        SetField(definition, "_baseValue", baseValue);
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
