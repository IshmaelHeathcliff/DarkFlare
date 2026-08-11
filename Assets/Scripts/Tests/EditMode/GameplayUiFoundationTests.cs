using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;

public class GameplayUiFoundationTests
{
    readonly List<Object> _objects = new List<Object>();

    IArchitecture _architecture;
    float _originalTimeScale;

    [SetUp]
    public void SetUp()
    {
        _originalTimeScale = Time.timeScale;
        GameArchitecture.Interface.Deinit();
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
        Time.timeScale = _originalTimeScale;
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
    public void HudSnapshot_ReflectsPlayerGoldAndEffectiveAttributes()
    {
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        inventory.AddGold(25);
        CreatePlayer();
        HudSnapshot snapshot = _architecture.SendQuery(new GetHudSnapshotQuery());

        Assert.IsTrue(snapshot.HasPlayer);
        Assert.AreEqual(100f, snapshot.CurrentHealth);
        Assert.AreEqual(100f, snapshot.MaxHealth);
        Assert.AreEqual(1f, snapshot.HealthNormalized);
        Assert.AreEqual(100f, snapshot.CurrentMana);
        Assert.AreEqual(100f, snapshot.MaxMana);
        Assert.AreEqual(1f, snapshot.ManaNormalized);
        Assert.AreEqual(25, snapshot.Gold);
        Assert.AreEqual(42f, snapshot.Attributes.Armor);
        Assert.AreEqual(18f, snapshot.Attributes.Evasion);
        Assert.AreEqual(5f, snapshot.Attributes.MoveSpeed);
        Assert.AreEqual(7.5f, snapshot.Attributes.CriticalChance);
        Assert.AreEqual(75f, snapshot.Attributes.FireResistance, "HUD 应显示伤害管线实际使用的抗性上限");
        Assert.AreEqual(12f, snapshot.Attributes.ColdResistance);
        Assert.AreEqual(-15f, snapshot.Attributes.LightningResistance);
        Assert.AreEqual(-100f, snapshot.Attributes.ChaosResistance, "HUD 应显示伤害管线实际使用的抗性下限");
        Assert.AreEqual(StatIds.All.Count, snapshot.Attributes.Values.Count, "属性面板快照必须覆盖全部已登记属性");

        for (int i = 0; i < StatIds.All.Count; i++)
        {
            Assert.AreEqual(StatIds.All[i], snapshot.Attributes.Values[i].StatId);
        }
    }

    [Test]
    public void AttributesMoveFromHudToInventoryContext()
    {
        string hudPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Assets/UI/Hud.uxml"));
        string inventoryPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Assets/UI/Inventory.uxml"));
        string hud = File.ReadAllText(hudPath);
        string inventory = File.ReadAllText(inventoryPath);

        StringAssert.DoesNotContain("name=\"attribute-card\"", hud);
        StringAssert.DoesNotContain("name=\"attribute-armor\"", hud);
        StringAssert.Contains("name=\"inventory-attribute-card\"", inventory);
        StringAssert.Contains("name=\"inventory-attribute-grid\"", inventory);
        StringAssert.DoesNotContain("weapon-card", hud);
        StringAssert.DoesNotContain("weapon-label", hud);
        StringAssert.DoesNotContain("weapon-icon", hud);
    }

    [Test]
    public void CombatFloatingText_DistinguishesTeamAndHealthChangeKind()
    {
        Assert.AreEqual("-12.5", DamageNumberVisual.FormatText(12.5f, CombatTextKind.Damage));
        Assert.AreEqual("+12.5", DamageNumberVisual.FormatText(12.5f, CombatTextKind.Healing));
        Color playerDamage = DamageNumberVisual.GetColor(ActorTeam.Player, CombatTextKind.Damage);
        Color monsterDamage = DamageNumberVisual.GetColor(ActorTeam.Monster, CombatTextKind.Damage);
        Color playerHealing = DamageNumberVisual.GetColor(ActorTeam.Player, CombatTextKind.Healing);
        Color monsterHealing = DamageNumberVisual.GetColor(ActorTeam.Monster, CombatTextKind.Healing);

        Assert.AreNotEqual(playerDamage, monsterDamage);
        Assert.AreNotEqual(playerDamage, playerHealing);
        Assert.AreNotEqual(monsterDamage, monsterHealing);
        Assert.AreNotEqual(playerHealing, monsterHealing);
    }

    [Test]
    public void CombatHealing_ClampsToMissingHealthAndPublishesActualAmount()
    {
        CombatActor actor = CreatePlayer();
        Dictionary<DamageType, float> damage = new Dictionary<DamageType, float>
        {
            { DamageType.Physical, 35f },
        };
        actor.ReceiveDamage(new DamageResult(true, false, damage, damage));
        List<ActorHealedEvent> events = new List<ActorHealedEvent>();
        _architecture.RegisterEvent<ActorHealedEvent>(events.Add);

        float healedAmount = _architecture.GetSystem<CombatSystem>().ApplyHealing(actor, 100f);

        Assert.AreEqual(35f, healedAmount);
        Assert.AreEqual(actor.MaxHealth, actor.CurrentHealth);
        Assert.AreEqual(1, events.Count);
        Assert.AreSame(actor, events[0].Actor);
        Assert.AreEqual(35f, events[0].Amount);
        Assert.AreEqual(0f, _architecture.GetSystem<CombatSystem>().ApplyHealing(actor, 10f));
        Assert.AreEqual(1, events.Count, "满生命时不应发送伪治疗事件");
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
        Assert.IsTrue(snapshot.Items[1].CanEquip);
        Assert.AreEqual(4, snapshot.EquipmentSlots.Count);
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
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        Assert.IsTrue(inventory.TryAddItem(item));
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

    [Test]
    public void CraftingSystem_RejectsItemOutsideInventory_WithoutCostOrEvent()
    {
        CraftingDefinition craftingDefinition = CreateScriptableObject<CraftingDefinition>();
        AffixDefinition affixDefinition = CreateScriptableObject<AffixDefinition>();
        SetField(craftingDefinition, "_affixPool", new List<AffixDefinition> { affixDefinition });
        SetField(craftingDefinition, "_addAffixCost", 20);
        ItemInstance item = CreateItem("detached_craft_item", "背包外物品");
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        inventory.AddGold(100);
        int eventCount = 0;
        _architecture.RegisterEvent<ItemCraftedEvent>(_ => eventCount++);
        _architecture.GetSystem<CraftingSystem>().Setup(craftingDefinition);

        bool crafted = _architecture.SendCommand(new CraftItemCommand(CraftOperation.AddAffix, item));

        Assert.IsFalse(crafted);
        Assert.AreEqual(100, inventory.Gold);
        Assert.AreEqual(0, item.Prefixes.Count + item.Suffixes.Count);
        Assert.AreEqual(0, eventCount);
    }

    [Test]
    public void CraftingSnapshot_ContainsCostsItemsAndAffixDetails()
    {
        CraftingDefinition craftingDefinition = CreateScriptableObject<CraftingDefinition>();
        SetField(craftingDefinition, "_addAffixCost", 20);
        SetField(craftingDefinition, "_rerollAllCost", 40);
        SetField(craftingDefinition, "_removeRerollCost", 30);
        SetField(craftingDefinition, "_upgradeCost", 25);
        AffixDefinition affixDefinition = CreateAffixDefinition("测试增伤", 20f, 20f);
        ItemInstance item = CreateItem("craft_snapshot_item", "快照大剑", baseValue: 10);
        Assert.IsTrue(item.TryAddAffix(affixDefinition.CreateInstance(new System.Random(1))));
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        inventory.AddGold(100);
        Assert.IsTrue(inventory.TryAddItem(item));
        _architecture.GetSystem<CraftingSystem>().Setup(craftingDefinition);

        CraftingSnapshot snapshot = _architecture.SendQuery(new GetCraftingSnapshotQuery());

        Assert.IsTrue(snapshot.IsConfigured);
        Assert.AreEqual(100, snapshot.Gold);
        Assert.AreEqual(20, snapshot.AddAffixCost);
        Assert.AreEqual(40, snapshot.RerollAllCost);
        Assert.AreEqual(30, snapshot.RemoveRerollCost);
        Assert.AreEqual(25, snapshot.UpgradeAffixCost);
        Assert.AreEqual(1, snapshot.Items.Count);
        Assert.AreSame(item, snapshot.Items[0].Item);
        Assert.AreEqual("快照大剑", snapshot.Items[0].DisplayName);
        Assert.AreEqual(12, snapshot.Items[0].Value);
        Assert.AreEqual(4, snapshot.Items[0].SellPrice);
        Assert.AreEqual(1, snapshot.Items[0].Affixes.Count);
        Assert.AreEqual("测试增伤", snapshot.Items[0].Affixes[0].DisplayName);
        StringAssert.Contains("伤害", snapshot.Items[0].Affixes[0].ModifierSummary);
        StringAssert.DoesNotContain("damage", snapshot.Items[0].Affixes[0].ModifierSummary);
        StringAssert.Contains("20", snapshot.Items[0].Affixes[0].ModifierSummary);
        Assert.AreEqual(20f, snapshot.Items[0].Affixes[0].TotalValue);
    }

    [Test]
    public void CraftingSystem_DoesNotCharge_WhenUpgradeCannotImprove()
    {
        CraftingDefinition craftingDefinition = CreateScriptableObject<CraftingDefinition>();
        SetField(craftingDefinition, "_upgradeCost", 25);
        AffixDefinition affixDefinition = CreateAffixDefinition("固定增伤", 20f, 20f);
        ItemInstance item = CreateItem("fixed_upgrade_item", "固定数值物品");
        AffixInstance affix = affixDefinition.CreateInstance(new System.Random(1));
        Assert.IsTrue(item.TryAddAffix(affix));
        InventoryModel inventory = _architecture.GetModel<InventoryModel>();
        inventory.AddGold(100);
        Assert.IsTrue(inventory.TryAddItem(item));
        int eventCount = 0;
        _architecture.RegisterEvent<ItemCraftedEvent>(_ => eventCount++);
        _architecture.GetSystem<CraftingSystem>().Setup(craftingDefinition);

        bool crafted = _architecture.SendCommand(new CraftItemCommand(CraftOperation.UpgradeAffix, item, affix));

        Assert.IsFalse(crafted);
        Assert.AreEqual(100, inventory.Gold);
        Assert.AreSame(affix, item.Prefixes[0]);
        Assert.AreEqual(0, eventCount);
    }

    [Test]
    public void InteractionCommands_PublishFocusAndContextualMenuAccess()
    {
        GameObject targetObject = new GameObject("TestMerchant");
        targetObject.SetActive(false);
        WorldInteractionTarget target = targetObject.AddComponent<WorldInteractionTarget>();
        SetField(target, "_displayName", "测试商人");
        SetField(target, "_menuPage", GameMenuPage.Shop);
        _objects.Add(targetObject);
        targetObject.SetActive(true);
        InteractionFocusChangedEvent focusEvent = default;
        GameMenuOpenRequestedEvent menuEvent = default;
        int focusEventCount = 0;
        int menuEventCount = 0;
        _architecture.RegisterEvent<InteractionFocusChangedEvent>(e =>
        {
            focusEvent = e;
            focusEventCount++;
        });
        _architecture.RegisterEvent<GameMenuOpenRequestedEvent>(e =>
        {
            menuEvent = e;
            menuEventCount++;
        });

        _architecture.SendCommand(new SetInteractionFocusCommand(target));
        bool opened = _architecture.SendCommand(new OpenGameMenuCommand(target));

        Assert.IsTrue(opened);
        Assert.AreEqual(1, focusEventCount);
        Assert.AreSame(target, focusEvent.Target);
        Assert.AreEqual(1, menuEventCount);
        Assert.AreEqual(GameMenuPage.Shop, menuEvent.Page);
        Assert.IsTrue(menuEvent.AvailablePages.Contains(GameMenuPage.Inventory));
        Assert.IsTrue(menuEvent.AvailablePages.Contains(GameMenuPage.Shop));
        Assert.IsFalse(menuEvent.AvailablePages.Contains(GameMenuPage.Crafting));
    }

    [Test]
    public void GameplayPauseCommand_IsIdempotentAndRestoresPreviousTimeScale()
    {
        GameplayPauseSystem pauseSystem = _architecture.GetSystem<GameplayPauseSystem>();
        List<GameplayPauseChangedEvent> events = new List<GameplayPauseChangedEvent>();
        _architecture.RegisterEvent<GameplayPauseChangedEvent>(events.Add);
        Time.timeScale = 0.75f;

        _architecture.SendCommand(new SetGameplayPausedCommand(true));
        _architecture.SendCommand(new SetGameplayPausedCommand(true));

        Assert.IsTrue(pauseSystem.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.AreEqual(1, events.Count);
        Assert.IsTrue(events[0].IsPaused);

        _architecture.SendCommand(new SetGameplayPausedCommand(false));

        Assert.IsFalse(pauseSystem.IsPaused);
        Assert.AreEqual(0.75f, Time.timeScale);
        Assert.AreEqual(2, events.Count);
        Assert.IsFalse(events[1].IsPaused);
    }

    CombatActor CreatePlayer()
    {
        GameObject playerObject = new GameObject("HudTestPlayer");
        playerObject.SetActive(false);
        CombatActor actor = playerObject.AddComponent<CombatActor>();
        StatBlock stats = new StatBlock();
        stats.SetValue(StatIds.Armor, 42f);
        stats.SetValue(StatIds.Mana, 100f);
        stats.SetValue(StatIds.HealthRegeneration, 1f);
        stats.SetValue(StatIds.ManaRegeneration, 5f);
        stats.SetValue(StatIds.Evasion, 18f);
        stats.SetValue(StatIds.MoveSpeed, 5f);
        stats.SetValue(StatIds.CriticalChance, 7.5f);
        stats.SetValue(StatIds.FireResistance, 90f);
        stats.SetValue(StatIds.ColdResistance, 12f);
        stats.SetValue(StatIds.LightningResistance, -15f);
        stats.SetValue(StatIds.ChaosResistance, -120f);
        actor.Configure("hud_test_player", ActorTeam.Player, 100f, stats, TagSet.Empty);
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
        SetField(definition, "_id", instanceId);
        SetField(definition, "_displayName", displayName);
        SetField(definition, "_itemType", itemType);
        SetField(definition, "_allowedEquipmentSlots", GetAllowedSlots(itemType));
        SetField(definition, "_gridSize", gridSize ?? Vector2Int.one);
        SetField(definition, "_baseValue", baseValue);
        return definition.CreateInstance(instanceId, 1, 1, ItemRarity.Normal);
    }

    static EquipmentSlotMask GetAllowedSlots(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Weapon => EquipmentSlotMask.Weapon,
            ItemType.Armor => EquipmentSlotMask.Armor,
            ItemType.Accessory => EquipmentSlotMask.Rings,
            _ => EquipmentSlotMask.None,
        };
    }

    AffixDefinition CreateAffixDefinition(string displayName, float minimumValue, float maximumValue)
    {
        StatDefinition stat = CreateScriptableObject<StatDefinition>();
        SetField(stat, "_id", "damage");
        SetField(stat, "_displayName", "伤害");
        StatModifierDefinition modifier = new StatModifierDefinition();
        SetField(modifier, "_stat", stat);
        SetField(modifier, "_operation", ModifierOperation.Increase);
        SetField(modifier, "_valueRange", new Vector2(minimumValue, maximumValue));
        AffixDefinition affix = CreateScriptableObject<AffixDefinition>();
        SetField(affix, "_displayName", displayName);
        SetField(affix, "_weight", 100);
        SetField(affix, "_modifiers", new List<StatModifierDefinition> { modifier });
        return affix;
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
