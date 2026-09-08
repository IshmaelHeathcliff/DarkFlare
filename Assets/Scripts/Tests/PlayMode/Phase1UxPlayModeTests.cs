using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public class Phase1UxPlayModeTests
    {
        readonly List<Object> _objects = new List<Object>();
        readonly InputTestFixture _inputFixture = new InputTestFixture();
        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();

        IArchitecture _architecture;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return _fixture.StopCurrent();
            yield return null;
            yield return InputTestFixtureGuard.Setup(_inputFixture);
            yield return _fixture.Restart();
            _architecture = _fixture.Architecture;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            Time.timeScale = 1f;
            _architecture = null;
            yield return _fixture.StopCurrent();
            yield return null;
            InputTestFixtureGuard.TearDown(_inputFixture);
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator ItemActions_EquipmentMenuDiscardAndDeviceLossKeepOwnership()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            IArchitecture architecture = menu.GetArchitecture();
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            inventory.AddGold(10000);
            EconomyModel economy = architecture.GetModel<EconomyModel>();
            ItemInstance weapon = economy.MerchantStock.First(item => item.BaseDefinition.CanEquipTo(EquipmentSlot.Weapon));
            ItemInstance ring = economy.MerchantStock.First(item => item.BaseDefinition.CanEquipTo(EquipmentSlot.RingLeft));
            Assert.IsTrue(architecture.SendCommand(new BuyItemCommand(weapon)));
            Assert.IsTrue(architecture.SendCommand(new BuyItemCommand(ring)));
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            VisualElement root = menu.GetComponent<UIDocument>().rootVisualElement;
            CombatActor player = architecture.SendQuery(new GetInventorySnapshotQuery()).Player;
            EquipmentModel equipment = architecture.GetModel<EquipmentModel>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            yield return RightClickPointer(mouse, root, FindItemButton(root, weapon, "inventory-item").worldBound.center);
            Assert.AreSame(weapon, equipment.GetItem(player, EquipmentSlot.Weapon), "普通背包右键没有装备");
            yield return RightClickPointer(mouse, root, root.Q("inventory-slot-weapon").worldBound.center);
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(weapon), "装备右键没有卸下");
            ItemInstance previousLeft = equipment.GetItem(player, EquipmentSlot.RingLeft);
            yield return RightClickPointer(mouse, root, FindItemButton(root, ring, "inventory-item").worldBound.center);
            Assert.IsTrue(menu.Workspace.Interactions.IsMenuOpen, "戒指需要明确选择左右槽");
            Assert.AreEqual("item-action-equip-RingLeft", ((VisualElement)root.focusController.focusedElement).name);
            _inputFixture.Press(pad.dpad.down);
            yield return null;
            _inputFixture.Release(pad.dpad.down);
            yield return null;
            Assert.AreEqual("item-action-equip-RingRight", ((VisualElement)root.focusController.focusedElement).name);
            _inputFixture.PressAndRelease(pad.buttonSouth);
            yield return null;
            yield return null;
            Assert.AreSame(ring, equipment.GetItem(player, EquipmentSlot.RingRight));
            Assert.AreSame(previousLeft, equipment.GetItem(player, EquipmentSlot.RingLeft));

            Button button = FindItemButton(root, weapon, "inventory-item");
            button.Focus();
            yield return null;
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            Assert.IsTrue(menu.Workspace.IsDragging);
            InputSystem.RemoveDevice(pad);
            yield return null;
            Assert.IsFalse(menu.Workspace.IsDragging, "手柄断开没有取消拿起");
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(weapon));
            yield return DragPointer(mouse, root, button.worldBound.position + new Vector2(24f, 24f),
                root.Q("item-discard-zone").worldBound.center, menu.GetComponent<InventoryPanelController>());
            Assert.IsFalse(inventory.Grid.Placements.ContainsKey(weapon), "明确丢弃区未转移物品");
            LootPickupController drop = Object.FindObjectsByType<LootPickupController>(FindObjectsSortMode.None)
                .Single(pickup => pickup.Item == weapon);
            Assert.IsNotNull(drop);
            Assert.IsFalse(architecture.SendCommand(new DiscardItemCommand(weapon, player)), "重复丢弃必须失败");
            Assert.LessOrEqual(CountItemSelectionHighlights(root), 1);
            InputSystem.RemoveDevice(mouse);
        }

        [UnityTest]
        public IEnumerator ItemActions_TradeByPointerAndGamepadAndReturnCraftingToExactCell()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            IArchitecture architecture = menu.GetArchitecture();
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            inventory.AddGold(10000);
            VisualElement root = menu.GetComponent<UIDocument>().rootVisualElement;
            InventoryPanelController panel = menu.GetComponent<InventoryPanelController>();
            CraftingPanelController crafting = menu.GetComponent<CraftingPanelController>();
            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(FindTarget(GameMenuPage.Shop))));
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Button stock = root.Q("shop-merchant-list").Query<Button>().First();
            ItemInstance first = (ItemInstance)stock.userData;
            int before = inventory.Gold;
            VisualElement grid = root.Q("inventory-grid");
            Vector2Int exact = new Vector2Int(3, 1);
            yield return DragPointer(mouse, root, stock.worldBound.position + new Vector2(24f, 24f),
                grid.worldBound.position + new Vector2(exact.x * 52f + 24f, exact.y * 52f + 24f), panel);
            Assert.AreEqual(exact, inventory.Grid.Placements[first].position, "购买没有保留鼠标指定格");
            Assert.Less(inventory.Gold, before);
            Assert.IsFalse(architecture.GetModel<EconomyModel>().HasStock(first));
            yield return DragPointer(mouse, root, FindItemButton(root, first, "inventory-item").worldBound.center,
                root.Q("shop-sell-zone").worldBound.center, panel);
            Assert.IsFalse(inventory.Grid.Placements.ContainsKey(first), "明确出售落点没有成交");

            menu.ClosePage(GameMenuPage.Inventory);
            yield return null;
            stock = root.Q("shop-merchant-list").Query<Button>().First();
            ItemInstance second = (ItemInstance)stock.userData;
            yield return RightClickPointer(mouse, root, stock.worldBound.center);
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(second), "单窗商人物品右键不能买入");
            Button playerSource = root.Q("shop-player-list").Query<Button>().ToList().First(button => button.userData == second);
            yield return RightClickPointer(mouse, root, playerSource.worldBound.center);
            Assert.IsFalse(inventory.Grid.Placements.ContainsKey(second), "单窗玩家来源右键不能卖出");

            stock = root.Q("shop-merchant-list").Query<Button>().First();
            ItemInstance third = (ItemInstance)stock.userData;
            stock.Focus();
            yield return null;
            _inputFixture.PressAndRelease(pad.buttonSouth);
            yield return null;
            yield return null;
            Assert.IsTrue(menu.Workspace.Interactions.IsMenuOpen, "手柄提交没有打开物品动作菜单");
            Assert.AreEqual("item-action-buy", ((VisualElement)root.focusController.focusedElement).name);
            yield return RightClickPointer(mouse, root, stock.worldBound.position + new Vector2(16f, 16f));
            Assert.IsFalse(menu.Workspace.Interactions.IsMenuOpen);
            Assert.IsFalse(inventory.Grid.Placements.ContainsKey(third), "菜单外右键只能关闭菜单，不能穿透购买");
            stock.Focus();
            _inputFixture.PressAndRelease(pad.buttonSouth);
            yield return null;
            yield return null;
            Assert.IsTrue(menu.Workspace.Interactions.IsMenuOpen);
            _inputFixture.PressAndRelease(pad.buttonSouth);
            yield return null;
            yield return null;
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(third), "手柄动作菜单没有买入");
            Assert.IsFalse(menu.Workspace.Interactions.IsMenuOpen);

            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            stock = root.Q("shop-merchant-list").Query<Button>().First();
            ItemInstance fourth = (ItemInstance)stock.userData;
            stock.Focus();
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            Assert.IsTrue(menu.Workspace.IsDragging);
            _inputFixture.Press(pad.dpad.left);
            yield return null;
            _inputFixture.Release(pad.dpad.left);
            yield return null;
            Assert.IsNotEmpty(grid.Query<VisualElement>(className: "inventory-cell--drop-valid").ToList(),
                "商人拿起后向左应进入实际左侧背包格");
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(fourth), "手柄跨窗指定格购买未提交");
            Assert.AreEqual(inventory.Grid.Width - fourth.BaseDefinition.GridSize.x,
                inventory.Grid.Placements[fourth].x, "从右侧商店进入背包应保留最近列");
            menu.ClosePage(GameMenuPage.Inventory);

            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(FindTarget(GameMenuPage.Crafting))));
            yield return null;
            yield return null;
            Button candidate = root.Q("crafting-candidates").Query<Button>().ToList().First(button => button.userData == third);
            RectInt original = inventory.Grid.Placements[third];
            yield return RightClickPointer(mouse, root, candidate.worldBound.center);
            Assert.AreSame(third, crafting.SlottedItem, "单窗候选右键没有放入打造槽");
            yield return RightClickPointer(mouse, root, root.Q("crafting-input-slot").worldBound.center);
            Assert.IsNull(crafting.SlottedItem);
            Assert.AreEqual(original, inventory.Grid.Placements[third]);
            candidate = root.Q("crafting-candidates").Query<Button>().ToList().First(button => button.userData == third);
            yield return DragPointer(mouse, root, candidate.worldBound.center, root.Q("crafting-input-slot").worldBound.center, panel);
            Assert.AreSame(third, crafting.SlottedItem, "单窗候选不能拖入打造槽");
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            yield return DragPointer(mouse, root, root.Q("crafting-input-slot").worldBound.center,
                grid.worldBound.position + new Vector2(9f * 52f + 24f, 5f * 52f + 24f), panel);
            Assert.AreSame(third, crafting.SlottedItem, "非法取回落点不应释放打造目标");
            Assert.AreEqual(original, inventory.Grid.Placements[third]);
            exact = new Vector2Int(5, 0);
            yield return DragPointer(mouse, root, root.Q("crafting-input-slot").worldBound.center,
                grid.worldBound.position + new Vector2(exact.x * 52f + 24f, exact.y * 52f + 24f), panel);
            Assert.IsNull(crafting.SlottedItem);
            Assert.AreEqual(exact, inventory.Grid.Placements[third].position, "打造取回没有使用指定格");
            Assert.LessOrEqual(CountItemSelectionHighlights(root), 1);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(pad);
        }

        [UnityTest]
        public IEnumerator GamepadNavigation_MovesHeldGridItemsAndCrossesCraftingBoundary()
        {
            yield return _fixture.EnterMain();
            yield return null;
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            IArchitecture architecture = menu.GetArchitecture();
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            ItemInstance item = CreateItemDefinition("navigation_item", "导航物品")
                .CreateInstance("navigation_instance", 1, 42);
            Assert.IsTrue(inventory.TryAddItem(item));
            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(FindTarget(GameMenuPage.Crafting))));
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            VisualElement root = menu.GetComponent<UIDocument>().rootVisualElement;
            Button itemButton = FindItemButton(root, item, "inventory-item");
            itemButton.Focus();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Vector2Int initial = inventory.Grid.Placements[item].position;
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            Assert.IsTrue(menu.Workspace.IsDragging);
            _inputFixture.Press(pad.dpad.right);
            yield return null;
            _inputFixture.Release(pad.dpad.right);
            yield return null;
            Assert.AreEqual(initial, inventory.Grid.Placements[item].position, "拿起时不能提前提交移动");
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            Assert.AreEqual(initial + Vector2Int.right, inventory.Grid.Placements[item].position,
                "一次方向输入必须只移动一格");
            Assert.IsFalse(menu.Workspace.IsDragging);

            Assert.IsTrue(architecture.SendCommand(new MoveInventoryItemCommand(item, new Vector2Int(9, 0))));
            yield return null;
            itemButton = FindItemButton(root, item, "inventory-item");
            itemButton.Focus();
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            _inputFixture.Press(pad.dpad.right);
            yield return null;
            _inputFixture.Release(pad.dpad.right);
            yield return null;
            Button slot = root.Q<Button>("crafting-input-slot");
            Assert.IsTrue(slot.ClassListContains("inventory-external-drop--valid"), "背包右边缘未进入实际右侧打造落点");
            _inputFixture.Press(pad.dpad.left);
            yield return null;
            _inputFixture.Release(pad.dpad.left);
            yield return null;
            Assert.IsFalse(slot.ClassListContains("inventory-external-drop--valid"), "打造落点不能返回背包");
            _inputFixture.Set(pad.leftStick, Vector2.right);
            yield return null;
            _inputFixture.Set(pad.leftStick, Vector2.zero);
            yield return null;
            Assert.IsTrue(slot.ClassListContains("inventory-external-drop--valid"), "摇杆与 D-pad 导航规则不一致");
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            Assert.AreSame(item, menu.GetComponent<CraftingPanelController>().SlottedItem);
            Assert.AreEqual(new Vector2Int(9, 0), inventory.Grid.Placements[item].position);
            slot.Focus();
            _inputFixture.Press(pad.dpad.left);
            yield return null;
            _inputFixture.Release(pad.dpad.left);
            yield return null;
            Assert.AreEqual(GameMenuPage.Inventory, menu.CurrentPage, "普通浏览不能从打造跨回背包");
            Assert.Less(((VisualElement)root.focusController.focusedElement).worldBound.center.x, slot.worldBound.center.x);
            slot.Focus();
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            _inputFixture.Press(pad.dpad.left);
            yield return null;
            _inputFixture.Release(pad.dpad.left);
            yield return null;
            Assert.IsNotEmpty(root.Query<VisualElement>(className: "inventory-cell--drop-valid").ToList(),
                "打造槽拿起后向左应允许取回背包");
            _inputFixture.PressAndRelease(pad.buttonWest);
            yield return null;
            Assert.IsNull(menu.GetComponent<CraftingPanelController>().SlottedItem);
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(item));
            Assert.LessOrEqual(CountItemSelectionHighlights(root), 1);
            InputSystem.RemoveDevice(pad);
        }

        [UnityTest]
        public IEnumerator IndependentWindows_TradeAndCraftWithoutInventoryAndRevokeInvalidTargets()
        {
            yield return _fixture.EnterMain();
            yield return null;
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            IArchitecture architecture = menu.GetArchitecture();
            VisualElement root = menu.GetComponent<UIDocument>().rootVisualElement;
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            inventory.AddGold(10000);
            WorldInteractionTarget merchant = FindTarget(GameMenuPage.Shop);
            WorldInteractionTarget craftingTarget = FindTarget(GameMenuPage.Crafting);
            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(merchant)));
            yield return null;
            yield return null;
            Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Inventory));
            ShopPanelController shop = menu.GetComponent<ShopPanelController>();
            Button stock = root.Q("shop-merchant-list").Query<Button>().First();
            ItemInstance purchased = (ItemInstance)stock.userData;
            InvokeButton(stock);
            InvokeButton(root.Q<Button>("shop-buy"));
            yield return null;
            yield return null;
            Assert.IsTrue(inventory.Grid.Placements.ContainsKey(purchased));
            Button playerSource = root.Q("shop-player-list").Query<Button>().ToList()
                .First(button => button.userData == purchased);
            InvokeButton(playerSource);
            Assert.AreEqual(ShopItemSource.Player, shop.SelectedSource);
            InvokeButton(root.Q<Button>("shop-sell"));
            yield return null;
            Assert.IsFalse(inventory.Grid.Placements.ContainsKey(purchased));
            Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Inventory));

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            _inputFixture.Press(keyboard.tabKey);
            yield return null;
            _inputFixture.Release(keyboard.tabKey);
            yield return null;
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Inventory));
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Shop));
            menu.ClosePage(GameMenuPage.Inventory);
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Shop));

            ItemBaseDefinition definition = CreateItemDefinition("window_craft", "窗口打造物品");
            ItemInstance item = definition.CreateInstance("window_craft_item", 1, 42, ItemRarity.Normal);
            Assert.IsTrue(inventory.TryAddItem(item));
            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(craftingTarget)));
            yield return null;
            yield return null;
            Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Shop));
            Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Inventory));
            CraftingPanelController crafting = menu.GetComponent<CraftingPanelController>();
            Button candidate = root.Q("crafting-candidates").Query<Button>().ToList()
                .First(button => button.userData == item);
            InvokeButton(candidate);
            InvokeButton(root.Q<Button>("crafting-slot-place"));
            yield return null;
            Assert.AreSame(item, crafting.SlottedItem);
            InvokeButton(root.Q<Button>("crafting-upgrade-rarity"));
            yield return null;
            Assert.AreNotEqual(ItemRarity.Normal, item.Rarity);
            RectInt original = inventory.Grid.Placements[item];
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            menu.ClosePage(GameMenuPage.Inventory);
            Assert.AreSame(item, crafting.SlottedItem);

            _inputFixture.Press(pad.selectButton);
            yield return null;
            _inputFixture.Release(pad.selectButton);
            yield return null;
            Assert.IsTrue(menu.IsPauseOpen);
            Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Crafting));
            Assert.AreSame(item, crafting.SlottedItem);
            _inputFixture.Press(pad.buttonEast);
            yield return null;
            _inputFixture.Release(pad.buttonEast);
            yield return null;
            Assert.IsFalse(menu.IsPauseOpen);
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Crafting));
            Assert.AreSame(item, crafting.SlottedItem);
            menu.OpenPage(GameMenuPage.Inventory);
            craftingTarget.enabled = false;
            yield return null;
            Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Crafting));
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Inventory));
            Assert.IsNull(crafting.SlottedItem);
            Assert.AreEqual(original, inventory.Grid.Placements[item]);
            menu.OpenPage(GameMenuPage.Crafting);
            Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Crafting), "失效目标不能沿用旧授权重开");
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(pad);
        }

        [UnityTest]
        public IEnumerator ShopPurchase_UsesGridAndRestoresNeighborFocusFeedbackAndTooltip()
        {
            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            yield return _fixture.EnterMain();
            GameMenuController menu = null;
            UIDocument document = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((menu == null || document == null || document.rootVisualElement.panel == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                menu = Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<UIDocument>() : null;
                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未初始化 GameMenuController");
            Assert.IsNotNull(document, "UIRoot 缺少 UIDocument");
            IArchitecture architecture = menu.GetArchitecture();
            Assert.IsTrue(ApplicationHost.TryGetCurrent(out ApplicationHost host));
            Assert.IsNotNull(host.Localization);
            ShopSnapshot setupShop = default;
            InventorySnapshot setupInventory = default;
            timeout = Time.realtimeSinceStartup + 15f;

            while ((setupShop.MerchantItems == null
                    || setupShop.MerchantItems.Count == 0
                    || !setupInventory.HasPlayer)
                   && Time.realtimeSinceStartup < timeout)
            {
                setupShop = architecture.SendQuery(new GetShopSnapshotQuery());
                setupInventory = architecture.SendQuery(new GetInventorySnapshotQuery());
                yield return null;
            }

            Assert.IsNotEmpty(setupShop.MerchantItems, "Main 场景未完成商店初始化");
            Assert.IsTrue(setupInventory.HasPlayer, "Main 场景未完成玩家初始化");
            EconomyModel economy = architecture.GetModel<EconomyModel>();
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            inventory.AddGold(10000);

            for (int i = 0; i < 30; i++)
            {
                ItemBaseDefinition definition = CreateItemDefinition($"phase1_shop_{i}", $"状态测试装备 {i}");
                economy.AddStock(definition.CreateInstance($"phase1_shop_item_{i}", 1, 100 + i));
            }

            WorldInteractionTarget merchant = FindTarget(GameMenuPage.Shop);
            Assert.IsNotNull(merchant, "Main 场景缺少商人交互目标");
            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(merchant)), "无法打开商店");
            yield return null;
            yield return null;

            ShopPanelController shop = menu.GetComponent<ShopPanelController>();
            VisualElement root = document.rootVisualElement;
            VisualElement merchantList = root.Q<VisualElement>("shop-merchant-list");
            VisualElement merchantFrame = root.Q<VisualElement>("shop-merchant-frame");
            Assert.IsNotNull(merchantFrame, "商店缺少无滚动背包框架");
            Assert.IsNull(root.Q<ScrollView>("shop-merchant-scroll"), "商人背包不应继续使用 ScrollView");
            Button selectedButton = FindButton(merchantList, "状态测试装备 3");
            Assert.IsNotNull(selectedButton, "未生成可用于滚动验收的商店按钮");
            ItemInstance purchasedItem = selectedButton.userData as ItemInstance;
            Assert.IsNotNull(purchasedItem, "商店按钮缺少物品实例");
            string purchasedDisplayName = host.Localization.GetString(
                ItemDetailSnapshotFactory.Create(purchasedItem).Name);
            yield return FocusAfterScheduledRestore(root, selectedButton);
            InvokeButton(selectedButton);
            yield return null;
            Assert.AreEqual("phase1_shop_item_3", shop.SelectedItem.InstanceId, "Submit 未固定当前选择");

            int selectedIndex = FindSnapshotIndex(shop.LastSnapshot.MerchantItems, shop.SelectedItem.InstanceId);
            Assert.GreaterOrEqual(selectedIndex, 0);
            string expectedNeighborId = selectedIndex + 1 < shop.LastSnapshot.MerchantItems.Count
                ? shop.LastSnapshot.MerchantItems[selectedIndex + 1].Item.InstanceId
                : shop.LastSnapshot.MerchantItems[selectedIndex - 1].Item.InstanceId;
            Button buyButton = root.Q<Button>("shop-buy");
            buyButton.Focus();
            yield return null;
            InvokeButton(buyButton);
            yield return null;
            yield return null;

            Assert.AreEqual(ShopItemSource.Merchant, shop.SelectedSource, "购买后离开了原来源列");
            Assert.AreEqual(expectedNeighborId, shop.SelectedItem.InstanceId, "购买后未选择原索引邻近项");
            Label feedback = root.Q<Label>("shop-feedback");
            StringAssert.Contains(
                purchasedDisplayName,
                feedback.text,
                "交易反馈在刷新帧内被覆盖");
            VisualElement detailRoot = root.Q<VisualElement>("item-tooltip");
            Label detailName = detailRoot.Q<Label>("item-detail-name");
            string selectedDisplayName = host.Localization.GetString(
                ItemDetailSnapshotFactory.Create(shop.SelectedItem).Name);
            Assert.AreEqual(selectedDisplayName, detailName.text, "详情未跟随邻近选择");
            AssertFocusedSelectedItem(root, shop.SelectedItem.BaseDefinition.DisplayName);

            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            List<Button> playerButtons = root.Query<Button>(className: "inventory-item").ToList();
            Assert.IsNotEmpty(playerButtons, "购买后玩家背包没有可用于跨栏导航的物品");
            Button rightmostPlayerButton = playerButtons[0];

            for (int i = 1; i < playerButtons.Count; i++)
            {
                if (playerButtons[i].worldBound.center.x > rightmostPlayerButton.worldBound.center.x)
                {
                    rightmostPlayerButton = playerButtons[i];
                }
            }

            rightmostPlayerButton.Focus();
            yield return null;
            Assert.AreEqual(1, CountItemSelectionHighlights(root), "玩家背包取得焦点后出现多个物品高亮");
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            _inputFixture.Press(keyboard.rightArrowKey);
            yield return null;
            _inputFixture.Release(keyboard.rightArrowKey);
            yield return null;
            yield return null;
            Assert.IsInstanceOf<Button>(root.focusController.focusedElement, "背包向右导航后焦点丢失");
            Button navigatedButton = (Button)root.focusController.focusedElement;
            Assert.IsTrue(navigatedButton.ClassListContains("shop-item"), "背包最右侧向右未进入商人背包");
            Assert.AreEqual(ShopItemSource.Merchant, shop.SelectedSource, "跨栏导航后未切换到商人物品选择");
            Assert.AreEqual(1, CountItemSelectionHighlights(root), "跨栏导航后玩家和商人物品同时高亮");
            expectedNeighborId = shop.SelectedItem.InstanceId;

            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            menu.OpenPage(GameMenuPage.Shop);
            yield return null;
            yield return null;
            Assert.AreEqual(expectedNeighborId, shop.SelectedItem.InstanceId, "页签往返后选择丢失");

            architecture.GetUtility<GameInput>().SwitchToGameplay();
            yield return null;
            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(merchant)), "无法重新打开同一商店");
            yield return null;
            yield return null;
            Assert.AreEqual(expectedNeighborId, shop.SelectedItem.InstanceId, "关闭重开后选择丢失");

            Vector2Int[] resolutions =
            {
                new Vector2Int(1920, 1080),
            };
            WorldInteractionTarget crafting = FindTarget(GameMenuPage.Crafting);
            Assert.IsNotNull(crafting, "Main 场景缺少打造台交互目标");

            try
            {
                string[] localeModes =
                {
                    "zh-Hans",
                    "en",
                    "qps-ploc",
                };

                for (int localeIndex = 0; localeIndex < localeModes.Length; localeIndex++)
                {
                    string localeMode = localeModes[localeIndex];
                    UserLanguagePreference preference = localeIndex == 0
                        ? UserLanguagePreference.SimplifiedChinese
                        : UserLanguagePreference.English;
                    yield return ChangeLanguage(host.Localization, preference);

                    for (int i = 0; i < resolutions.Length; i++)
                    {
                        Vector2Int resolution = resolutions[i];
                        SetGameViewResolution(resolution);
                        yield return WaitForResolution(resolution, 5f);
                        Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(merchant)), "无法打开商店布局验收");
                        menu.OpenPage(GameMenuPage.Inventory);
                        yield return null;
                        yield return null;
                        List<Button> merchantPreviewButtons = root.Query<Button>(className: "shop-item").ToList();
                        Assert.IsNotEmpty(merchantPreviewButtons, "商人背包缺少可悬停物品");
                        merchantPreviewButtons[0].Focus();
                        yield return null;
                        yield return null;

                        if (localeMode == "qps-ploc")
                        {
                            Assert.Greater(ApplyPseudoLocalization(root), 0, "没有可用于伪本地化的 UI 文本");
                            yield return null;
                            yield return null;
                        }

                        AssertLayoutInsideRoot(root, new[]
                        {
                            "game-menu-panel",
                            "shop-page",
                            "shop-merchant-frame",
                            "shop-merchant-list",
                            "inventory-grid",
                            "item-tooltip",
                            "shop-buy",
                            "shop-sell",
                            "game-menu-close",
                        });
                        AssertElementsInsideContainer(root, "shop-page", new[]
                        {
                            "shop-actions",
                            "shop-buy",
                            "shop-sell",
                            "shop-buy-zone",
                            "shop-sell-zone",
                        });
                        Assert.LessOrEqual(root.Q("shop-actions").worldBound.yMax,
                            root.Q("shop-buy-zone").parent.worldBound.yMin + 1f,
                            "买卖按钮与拖放操作区重叠");
                        AssertWorkbenchShare(root);
                        AssertMerchantBackpack(root);
                        AssertVisibleTextFits(root, localeMode, resolution);

                        if (resolution.x >= 1920)
                        {
                            AssertElementsDoNotOverlap(root, "item-tooltip", "game-menu-panel");
                            AssertTooltipClearOfWindows(root);
                        }

                        Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(crafting)), "无法打开打造页布局验收");
                        yield return null;
                        yield return null;
                        List<Button> playerPreviewButtons = root.Query<Button>(className: "inventory-item").ToList();
                        Assert.IsNotEmpty(playerPreviewButtons, "玩家背包缺少可悬停物品");
                        playerPreviewButtons[0].Focus();
                        yield return null;
                        yield return null;

                        if (localeMode == "qps-ploc")
                        {
                            ApplyPseudoLocalization(root);
                            yield return null;
                            yield return null;
                        }

                        AssertLayoutInsideRoot(root, new[]
                        {
                            "game-menu-panel",
                            "crafting-page",
                            "crafting-input-slot",
                            "crafting-slot-place",
                            "crafting-slot-remove",
                            "crafting-selected-rarity",
                            "inventory-grid",
                            "item-tooltip",
                            "crafting-add-affix",
                            "crafting-remove-affix",
                            "game-menu-close",
                        });
                        AssertElementsInsideContainer(root, "crafting-page", new[]
                        {
                            "crafting-actions",
                            "crafting-input-slot",
                            "crafting-slot-place",
                            "crafting-slot-remove",
                            "crafting-add-affix",
                            "crafting-reroll-affixes",
                            "crafting-remove-affix",
                            "crafting-upgrade-rarity",
                            "crafting-reroll-values",
                        });
                        AssertWorkbenchShare(root);
                        AssertVisibleTextFits(root, localeMode, resolution);

                        if (resolution.x >= 1920)
                        {
                            AssertTooltipClearOfWindows(root);
                        }
                        menu.ClosePage(GameMenuPage.Crafting);
                        yield return null;
                        yield return null;
                        if (localeMode == "qps-ploc") { ApplyPseudoLocalization(root); }
                        yield return null;
                        yield return null;
                        AssertLayoutInsideRoot(root, new[] { "inventory-window", "item-operation-bar" });
                        AssertVisibleTextFits(root, localeMode, resolution);
                    }
                }

                yield return ChangeLanguage(
                    host.Localization,
                    UserLanguagePreference.SimplifiedChinese);

                Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(merchant)), "无法打开商店右键出售验收");
                yield return null;
                yield return null;
                VisualElement tooltip = root.Q<VisualElement>("item-tooltip");
                VisualElement tooltipDetail = root.Q<VisualElement>("item-tooltip-detail");
                ItemDetailView denseDetailView = new ItemDetailView(tooltipDetail);
                denseDetailView.Show(CreateDenseDetail(purchasedItem), null);
                tooltip.style.display = DisplayStyle.Flex;
                tooltip.style.visibility = Visibility.Visible;
                yield return null;
                yield return null;
                VisualElement suffixList = root.Q<VisualElement>("item-detail-suffix-list");
                VisualElement lastAffix = suffixList[suffixList.childCount - 1];
                Assert.IsTrue(tooltipDetail.ClassListContains("item-detail--dense"), "六词缀详情未进入紧凑排版");
                Assert.LessOrEqual(
                    lastAffix.worldBound.yMax,
                    tooltip.worldBound.yMax - 8f,
                    "六词缀详情内容仍然溢出物品信息栏");
                Button purchasedInventoryButton = FindItemButton(root, purchasedItem, "inventory-item");
                Assert.IsNotNull(purchasedInventoryButton, "购买后玩家背包未生成对应物品按钮");
                int goldBeforeSale = architecture.SendQuery(new GetShopSnapshotQuery()).Gold;
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                yield return RightClickPointer(mouse, root, purchasedInventoryButton.worldBound.center);
                InventorySnapshot afterContextSale = architecture.SendQuery(new GetInventorySnapshotQuery());
                Assert.IsFalse(ContainsItem(afterContextSale, purchasedItem), "商店界面右键点击未直接卖出背包物品");
                Assert.Greater(
                    architecture.SendQuery(new GetShopSnapshotQuery()).Gold,
                    goldBeforeSale,
                    "右键出售后金币没有增加");
                StringAssert.Contains(
                    host.Localization.GetString(
                        ItemDetailSnapshotFactory.Create(purchasedItem).Name),
                    feedback.text,
                    "右键出售没有显示交易反馈");
                Assert.AreEqual(ShopItemSource.Merchant, shop.SelectedSource, "右键出售后没有恢复到商人库存选择");
                Assert.AreEqual(expectedNeighborId, shop.SelectedItem.InstanceId, "右键出售后商人库存邻近选择丢失");
            }
            finally
            {
                SetGameViewResolution(new Vector2Int(originalWidth, originalHeight));
                architecture.GetUtility<GameInput>().SwitchToGameplay();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Crafting_OnlyUsesItemsPlacedInTheInputSlot()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = null;
            UIDocument document = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((menu == null || document == null || document.rootVisualElement.panel == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                menu = Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<UIDocument>() : null;
                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未初始化 GameMenuController");
            Assert.IsNotNull(document, "UIRoot 缺少 UIDocument");
            IArchitecture architecture = menu.GetArchitecture();
            InventorySnapshot setupInventory = default;
            timeout = Time.realtimeSinceStartup + 15f;

            while (!setupInventory.HasPlayer && Time.realtimeSinceStartup < timeout)
            {
                setupInventory = architecture.SendQuery(new GetInventorySnapshotQuery());
                yield return null;
            }

            Assert.IsTrue(setupInventory.HasPlayer, "Main 场景未完成玩家初始化");
            ItemBaseDefinition definition = CreateItemDefinition(
                "phase1_crafting_slot",
                "打造槽测试武器");
            ItemInstance item = definition.CreateInstance(
                "phase1_crafting_slot_item",
                1,
                101,
                ItemRarity.Normal);
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            inventory.AddGold(10000);
            Assert.IsTrue(inventory.TryAddItem(item), "无法添加打造槽验收物品");
            WorldInteractionTarget craftingTarget = FindTarget(GameMenuPage.Crafting);
            Assert.IsNotNull(craftingTarget, "Main 场景缺少打造台交互目标");
            Assert.IsTrue(
                architecture.SendCommand(new OpenGameMenuCommand(craftingTarget)),
                "无法打开打造页");
            yield return null;
            yield return null;

            VisualElement root = document.rootVisualElement;
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;
            InventoryPanelController inventoryPanel = menu.GetComponent<InventoryPanelController>();
            CraftingPanelController craftingPanel = menu.GetComponent<CraftingPanelController>();
            Button itemButton = FindItemButton(root, item, "inventory-item");
            Button inputSlot = root.Q<Button>("crafting-input-slot");
            Button placeButton = root.Q<Button>("crafting-slot-place");
            Button removeButton = root.Q<Button>("crafting-slot-remove");
            Button upgradeButton = root.Q<Button>("crafting-upgrade-rarity");
            Assert.IsNotNull(itemButton, "玩家背包未生成打造槽验收物品按钮");
            Assert.IsNotNull(inputSlot, "打造页缺少打造槽");
            Assert.AreEqual(100f, inputSlot.worldBound.width, 1f, "打造槽宽度不是 2 格");
            Assert.AreEqual(152f, inputSlot.worldBound.height, 1f, "打造槽高度不是 3 格");

            InvokeButton(itemButton);
            yield return null;
            Assert.IsNull(craftingPanel.SlottedItem, "点击背包物品不应直接成为打造目标");
            Assert.IsFalse(upgradeButton.enabledSelf, "打造槽为空时不应启用打造操作");

            InvokeButton(placeButton);
            yield return null;
            Assert.AreSame(item, craftingPanel.SlottedItem, "显式放入按钮未锁定打造目标");
            Assert.IsTrue(upgradeButton.enabledSelf, "普通物品放入槽位后应允许提升稀有度");
            Assert.IsNull(
                FindItemButton(root, item, "inventory-item"),
                "放入打造槽后物品仍重复显示在玩家背包");

            InvokeButton(removeButton);
            yield return null;
            Assert.IsNull(craftingPanel.SlottedItem, "取回按钮未清空打造槽");
            Assert.IsFalse(upgradeButton.enabledSelf, "取回物品后仍错误启用打造操作");
            itemButton = FindItemButton(root, item, "inventory-item");
            Assert.IsNotNull(itemButton, "取回打造物品后没有恢复背包显示");

            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return DragPointer(
                mouse,
                root,
                itemButton.worldBound.center,
                inputSlot.worldBound.center,
                inventoryPanel);
            Assert.AreSame(item, craftingPanel.SlottedItem, "从背包拖入打造槽未锁定打造目标");

            Assert.IsTrue(inventory.RemoveItem(item), "无法移除打造槽验收物品");
            yield return null;
            yield return null;
            Assert.IsNull(craftingPanel.SlottedItem, "物品离开背包后打造槽未自动失效");
            Assert.IsFalse(upgradeButton.enabledSelf, "失效打造目标仍允许执行打造");
            architecture.GetUtility<GameInput>().SwitchToGameplay();
            yield return null;
        }

        static IEnumerator FocusAfterScheduledRestore(VisualElement root, Button button)
        {
            for (int i = 0; i < 5; i++)
            {
                button.Focus();
                yield return null;

                if (root.focusController.focusedElement == button)
                {
                    yield return null;

                    if (root.focusController.focusedElement == button)
                    {
                        yield break;
                    }
                }
            }

            Assert.AreSame(button, root.focusController.focusedElement, "商店延迟焦点恢复未稳定");
        }

        ItemBaseDefinition CreateItemDefinition(string id, string displayName)
        {
            ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
            _objects.Add(definition);
            SetField(definition, "_id", id);
            SetField(definition, "_displayName", displayName);
            SetField(
                definition,
                "_localizedName",
                new LocalizedContentReference("items", "item.great_sword.name"));
            SetField(definition, "_itemType", ItemType.Weapon);
            SetField(definition, "_allowedEquipmentSlots", EquipmentSlotMask.Weapon);
            SetField(definition, "_gridSize", Vector2Int.one);
            SetField(definition, "_baseValue", 100);
            return definition;
        }

        static WorldInteractionTarget FindTarget(GameMenuPage page)
        {
            WorldInteractionTarget[] targets = Object.FindObjectsByType<WorldInteractionTarget>();

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].MenuPage == page)
                {
                    return targets[i];
                }
            }

            return null;
        }

        static Button FindButton(VisualElement list, string text)
        {
            for (int i = 0; i < list.childCount; i++)
            {
                if (list[i] is Button button
                    && button.userData is ItemInstance item
                    && item.BaseDefinition != null
                    && item.BaseDefinition.DisplayName.Contains(text))
                {
                    return button;
                }
            }

            return null;
        }

        IEnumerator DragPointer(
            Mouse mouse,
            VisualElement root,
            Vector2 source,
            Vector2 destination,
            InventoryPanelController panel)
        {
            Vector2 sourceScreen = PanelToScreen(root, source);
            Vector2 destinationScreen = PanelToScreen(root, destination);
            _inputFixture.Set(mouse.position, sourceScreen);
            yield return null;
            _inputFixture.Press(mouse.leftButton);
            yield return null;
            Assert.IsTrue(panel.IsPointerPending, "PointerDown 未建立拖拽候选");
            _inputFixture.Set(mouse.position, Vector2.Lerp(sourceScreen, destinationScreen, 0.35f));
            yield return null;
            Assert.IsTrue(panel.IsDragging, "Pointer 移动超过阈值后未进入拖拽状态");
            _inputFixture.Set(mouse.position, destinationScreen);
            yield return null;
            _inputFixture.Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsFalse(panel.IsDragging, "Pointer 抬起后拖拽状态未结束");
        }

        IEnumerator RightClickPointer(Mouse mouse, VisualElement root, Vector2 panelPosition)
        {
            _inputFixture.Set(mouse.position, PanelToScreen(root, panelPosition));
            yield return null;
            _inputFixture.Press(mouse.rightButton);
            yield return null;
            _inputFixture.Release(mouse.rightButton);
            yield return null;
            yield return null;
        }

        static Vector2 PanelToScreen(VisualElement root, Vector2 point)
        {
            Rect bounds = root.worldBound;
            float normalizedX = (point.x - bounds.xMin) / bounds.width;
            float normalizedY = (point.y - bounds.yMin) / bounds.height;
            return new Vector2(normalizedX * Screen.width, (1f - normalizedY) * Screen.height);
        }

        static Button FindItemButton(VisualElement root, ItemInstance item, string className)
        {
            List<Button> buttons = root.Query<Button>(className: className).ToList();

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].userData == item)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        static bool ContainsItem(InventorySnapshot snapshot, ItemInstance item)
        {
            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                if (snapshot.Items[i].Item == item)
                {
                    return true;
                }
            }

            return false;
        }

        static int CountItemSelectionHighlights(VisualElement root)
        {
            return root.Query<VisualElement>(className: "inventory-item--selected").ToList().Count
                + root.Query<VisualElement>(className: "inventory-equipment-slot--selected").ToList().Count
                + root.Query<VisualElement>(className: "item-source-row--selected").ToList().Count
                + root.Query<VisualElement>(className: "shop-item--selected").ToList().Count;
        }

        static ItemDetailSnapshot CreateDenseDetail(ItemInstance item)
        {
            List<AffixDetailSnapshot> prefixes = new List<AffixDetailSnapshot>();
            List<AffixDetailSnapshot> suffixes = new List<AffixDetailSnapshot>();

            for (int i = 0; i < 3; i++)
            {
                List<ModifierDetailSnapshot> prefixModifiers = new List<ModifierDetailSnapshot>
                {
                    new ModifierDetailSnapshot(
                        new ModifierInstance(
                            StatIds.Armor,
                            ModifierOperation.Increase,
                            ModifierScope.GlobalActor,
                            20 + i,
                            DamageType.Physical,
                            DamageType.Physical,
                            TagSet.Empty,
                            TagSet.Empty),
                        StatIds.Armor,
                        false),
                };
                List<ModifierDetailSnapshot> suffixModifiers = new List<ModifierDetailSnapshot>
                {
                    new ModifierDetailSnapshot(
                        new ModifierInstance(
                            string.Empty,
                            ModifierOperation.GainAsExtra,
                            ModifierScope.GlobalActor,
                            10 + i,
                            DamageType.Physical,
                            DamageType.Fire,
                            TagSet.Empty,
                            TagSet.Empty),
                        string.Empty,
                        false),
                };
                prefixes.Add(new AffixDetailSnapshot(
                    null,
                    AffixType.Prefix,
                    new LocalizedMessage("affixes", $"test.prefix.{i + 1}"),
                    prefixModifiers,
                    0f));
                suffixes.Add(new AffixDetailSnapshot(
                    null,
                    AffixType.Suffix,
                    new LocalizedMessage("affixes", $"test.suffix.{i + 1}"),
                    suffixModifiers,
                    0f));
            }

            return new ItemDetailSnapshot(
                item,
                item.InstanceId,
                item.BaseDefinition.LocalizedName.Message,
                string.Empty,
                item.BaseDefinition.ItemType,
                ItemRarity.Unique,
                item.ItemLevel,
                item.BaseDefinition.GridSize,
                item.BaseDefinition.Weight,
                item.BaseDefinition.BaseValue,
                item.BaseDefinition.BaseValue,
                new List<DamageDetailSnapshot>(),
                new List<ModifierDetailSnapshot>(),
                prefixes,
                suffixes);
        }

        static void InvokeButton(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new System.MissingMethodException(typeof(Clickable).FullName, "Invoke");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        static int FindSnapshotIndex(IReadOnlyList<ShopItemSnapshot> items, string instanceId)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item.InstanceId == instanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        static void AssertFocusedSelectedItem(VisualElement root, string displayName)
        {
            Focusable focused = root.focusController.focusedElement;
            Assert.IsInstanceOf<Button>(focused, "交易后焦点未恢复到物品按钮");
            StringAssert.Contains(
                displayName,
                GetButtonContentText((Button)focused),
                "焦点与恢复后的选择不一致");
        }

        static string GetButtonContentText(Button button)
        {
            string content = button.text;

            if (button.userData is ItemInstance item && item.BaseDefinition != null)
            {
                content += $" {item.BaseDefinition.DisplayName}";
            }
            List<Label> labels = button.Query<Label>().ToList();

            for (int i = 0; i < labels.Count; i++)
            {
                content += $" {labels[i].text}";
            }

            return content;
        }

        static IEnumerator WaitForResolution(Vector2Int resolution, float timeoutSeconds)
        {
            float timeout = Time.realtimeSinceStartup + timeoutSeconds;

            while ((Screen.width != resolution.x || Screen.height != resolution.y)
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.AreEqual(resolution.x, Screen.width, $"Game View 宽度未切换为 {resolution.x}");
            Assert.AreEqual(resolution.y, Screen.height, $"Game View 高度未切换为 {resolution.y}");
        }

        static void SetGameViewResolution(Vector2Int resolution)
        {
            const string UtilityTypeName = "DarkFlare.Editor.Phase0GameViewResolutionUtility, DarkFlare.Editor";
            System.Type utilityType = System.Type.GetType(UtilityTypeName, true);
            MethodInfo method = utilityType.GetMethod("SetResolution", BindingFlags.Static | BindingFlags.Public)
                ?? throw new System.MissingMethodException(UtilityTypeName, "SetResolution");
            method.Invoke(null, new object[] { resolution.x, resolution.y });
        }

        static IEnumerator ChangeLanguage(
            LocalizationService service,
            UserLanguagePreference preference)
        {
            Cysharp.Threading.Tasks.UniTask<LocalizationOperationResult> operation =
                service.ChangeLanguageAsync(preference);

            while (operation.Status == Cysharp.Threading.Tasks.UniTaskStatus.Pending)
            {
                yield return null;
            }

            LocalizationOperationResult result = operation.GetAwaiter().GetResult();
            Assert.IsTrue(
                result.Succeeded,
                $"切换语言 {preference} 失败：{result.Code} {result.Exception}");
            yield return null;
            yield return null;
        }

        static int ApplyPseudoLocalization(VisualElement root)
        {
            const string UtilityTypeName = "DarkFlare.Editor.PseudoLocalizationUtility, DarkFlare.Editor";
            System.Type utilityType = System.Type.GetType(UtilityTypeName, true);
            MethodInfo method = utilityType.GetMethod("Localize", BindingFlags.Static | BindingFlags.Public)
                ?? throw new System.MissingMethodException(UtilityTypeName, "Localize");
            List<TextElement> elements = root.Query<TextElement>().ToList();
            int transformed = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                TextElement element = elements[i];

                if (string.IsNullOrWhiteSpace(element.text)
                    || !element.text.Any(char.IsLetter)
                    || (element.text[0] == '[' && element.text[^1] == ']')
                    || element.worldBound.width <= 0f
                    || element.worldBound.height <= 0f)
                {
                    continue;
                }

                string pseudo = (string)method.Invoke(null, new object[] { element.text });

                if (!string.Equals(pseudo, element.text, System.StringComparison.Ordinal))
                {
                    element.text = pseudo;
                    transformed++;
                }
            }

            return transformed;
        }

        static void AssertVisibleTextFits(
            VisualElement root,
            string localeMode,
            Vector2Int resolution)
        {
            List<TextElement> elements = root.Query<TextElement>().ToList();

            for (int i = 0; i < elements.Count; i++)
            {
                TextElement element = elements[i];
                Rect content = element.contentRect;

                if (string.IsNullOrWhiteSpace(element.text)
                    || element.resolvedStyle.display == DisplayStyle.None
                    || element.worldBound.width <= 1f
                    || element.worldBound.height <= 1f
                    || content.width <= 1f
                    || content.height <= 1f)
                {
                    continue;
                }

                bool wraps = element.resolvedStyle.whiteSpace == WhiteSpace.Normal;
                Vector2 measured = element.MeasureTextSize(
                    element.text,
                    wraps ? content.width : 0f,
                    wraps ? VisualElement.MeasureMode.AtMost : VisualElement.MeasureMode.Undefined,
                    0f,
                    VisualElement.MeasureMode.Undefined);
                string identity = string.IsNullOrWhiteSpace(element.name)
                    ? $"{element.GetType().Name} '{element.text}'"
                    : element.name;
                string context = $"{localeMode} {resolution.x}×{resolution.y} {identity}";

                if (wraps)
                {
                    Assert.LessOrEqual(
                        measured.y,
                        content.height + 2f,
                        $"{context}: 换行文本被纵向裁切");
                }
                else
                {
                    Assert.LessOrEqual(
                        measured.x,
                        content.width + 2f,
                        $"{context}: 单行文本被横向裁切");
                }
            }
        }

        static void AssertLayoutInsideRoot(VisualElement root, IReadOnlyList<string> names)
        {
            Rect rootBounds = root.worldBound;
            if (GameMenuController.IsNavigable(root.Q("item-operation-bar")))
            {
                AssertElementsInsideContainer(root, "game-menu-panel", new[] { "item-operation-bar" });
                AssertElementsInsideContainer(root, "item-operation-bar", new[]
                {
                    "item-operation-status", "item-operation-actions", "item-discard-zone",
                });
            }

            foreach (VisualElement header in root.Query<VisualElement>(className: "item-window-header").ToList())
            {
                if (!GameMenuController.IsNavigable(header))
                {
                    continue;
                }

                VisualElement title = header.Q<VisualElement>(className: "item-window-title");
                VisualElement close = header.Q<VisualElement>(className: "item-window-close");
                Assert.LessOrEqual(title.worldBound.xMax, close.worldBound.xMin + 1f, "长标题遮挡关闭按钮");
                Assert.LessOrEqual(close.worldBound.xMax, header.worldBound.xMax + 1f, "关闭按钮超出窗口标题区");
            }

            for (int i = 0; i < names.Count; i++)
            {
                VisualElement element = root.Q<VisualElement>(names[i]);
                Assert.IsNotNull(element, $"缺少 UI 元素 {names[i]}");
                Rect bounds = element.worldBound;
                Assert.Greater(bounds.width, 0f, $"{names[i]} 宽度无效");
                Assert.Greater(bounds.height, 0f, $"{names[i]} 高度无效");
                Assert.GreaterOrEqual(bounds.xMin, rootBounds.xMin - 1f, $"{names[i]} 超出左边界");
                Assert.GreaterOrEqual(bounds.yMin, rootBounds.yMin - 1f, $"{names[i]} 超出上边界");
                Assert.LessOrEqual(bounds.xMax, rootBounds.xMax + 1f, $"{names[i]} 超出右边界");
                Assert.LessOrEqual(bounds.yMax, rootBounds.yMax + 1f, $"{names[i]} 超出下边界");
            }


            Rect gridFrame = root.Q<VisualElement>("inventory-grid-frame").worldBound;
            Rect grid = root.Q<VisualElement>("inventory-grid").worldBound;
            Assert.GreaterOrEqual(grid.xMin, gridFrame.xMin - 1f, "背包格超出左边界");
            Assert.GreaterOrEqual(grid.yMin, gridFrame.yMin - 1f, "背包格超出上边界");
            Assert.LessOrEqual(grid.xMax, gridFrame.xMax + 1f, "背包格超出右边界");
            Assert.LessOrEqual(grid.yMax, gridFrame.yMax + 1f, "背包格超出下边界");
        }

        static void AssertElementsDoNotOverlap(VisualElement root, string firstName, string secondName)
        {
            VisualElement first = root.Q<VisualElement>(firstName);
            VisualElement second = root.Q<VisualElement>(secondName);
            Assert.IsNotNull(first, $"缺少 UI 元素 {firstName}");
            Assert.IsNotNull(second, $"缺少 UI 元素 {secondName}");
            Assert.IsFalse(first.worldBound.Overlaps(second.worldBound), $"{firstName} 遮挡了 {secondName}");
        }

        static void AssertTooltipClearOfWindows(VisualElement root)
        {
            Rect tooltip = root.Q("item-tooltip").worldBound;
            foreach (VisualElement window in root.Query<VisualElement>(className: "item-window").ToList())
            {
                if (!GameMenuController.IsNavigable(window)) { continue; }
                Assert.IsFalse(tooltip.Overlaps(window.worldBound), $"详情遮挡 {window.name}");
                Assert.AreEqual(window.worldBound.yMin, tooltip.yMin, 1f, "详情与窗口顶部没有对齐");
            }
        }

        static void AssertWorkbenchShare(VisualElement root)
        {
            VisualElement inventory = root.Q("inventory-window");
            Rect equipment = root.Q("inventory-equipment").worldBound;
            Rect grid = root.Q("inventory-grid-frame").worldBound;
            Assert.LessOrEqual(equipment.yMax, grid.yMin + 1f, "装备区没有位于背包上方");
            foreach (VisualElement window in root.Query<VisualElement>(className: "item-window").ToList())
            {
                if (window == inventory || !GameMenuController.IsNavigable(window)) { continue; }
                Assert.IsFalse(inventory.worldBound.Overlaps(window.worldBound), "独立窗口互相遮挡");
                Assert.AreEqual(inventory.worldBound.yMin, window.worldBound.yMin, 1f, "窗口顶部没有对齐");
                Assert.AreEqual(inventory.worldBound.yMax, window.worldBound.yMax, 1f, "窗口底部没有对齐");
            }
        }

        static void AssertMerchantBackpack(VisualElement root)
        {
            VisualElement frame = root.Q<VisualElement>("shop-merchant-frame");
            VisualElement merchantGrid = root.Q<VisualElement>("shop-merchant-list");
            VisualElement playerGrid = root.Q<VisualElement>("inventory-grid");
            Assert.IsNull(root.Q<ScrollView>("shop-merchant-scroll"), "商人背包不应出现滚动容器");
            Assert.AreEqual(playerGrid.worldBound.width, merchantGrid.worldBound.width, 1f, "商人背包应与玩家背包使用相同列宽");
            Assert.GreaterOrEqual(merchantGrid.worldBound.height, playerGrid.worldBound.height - 1f, "商人背包不应小于玩家背包");
            Assert.GreaterOrEqual(merchantGrid.worldBound.xMin, frame.worldBound.xMin - 1f, "商人网格超出框架左边界");
            Assert.GreaterOrEqual(merchantGrid.worldBound.yMin, frame.worldBound.yMin - 1f, "商人网格超出框架上边界");
            Assert.LessOrEqual(merchantGrid.worldBound.xMax, frame.worldBound.xMax + 1f, "商人网格超出框架右边界");
            Assert.LessOrEqual(merchantGrid.worldBound.yMax, frame.worldBound.yMax + 1f, "商人网格超出框架下边界");

            List<Button> buttons = merchantGrid.Query<Button>(className: "shop-item").ToList();

            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                VisualElement icon = button.Q<VisualElement>(className: "shop-item-icon");
                Assert.IsNotNull(icon, "商人物品格缺少图标");
                Assert.IsNull(button.Q<Label>(), "商人物品格不应在图标旁显示名称或价格");
                Assert.AreEqual(button.worldBound.center.x, icon.worldBound.center.x, 1f, "商人物品图标没有水平居中");
                Assert.AreEqual(button.worldBound.center.y, icon.worldBound.center.y, 1f, "商人物品图标没有垂直居中");
            }
        }

        static void AssertElementsInsideContainer(
            VisualElement root,
            string containerName,
            IReadOnlyList<string> names)
        {
            VisualElement container = root.Q<VisualElement>(containerName);
            Assert.IsNotNull(container, $"缺少 UI 容器 {containerName}");
            Rect containerBounds = container.worldBound;

            for (int i = 0; i < names.Count; i++)
            {
                VisualElement element = root.Q<VisualElement>(names[i]);
                Assert.IsNotNull(element, $"缺少 UI 元素 {names[i]}");
                Rect bounds = element.worldBound;
                Assert.GreaterOrEqual(bounds.xMin, containerBounds.xMin - 1f, $"{names[i]} 超出 {containerName} 左边界");
                Assert.GreaterOrEqual(bounds.yMin, containerBounds.yMin - 1f, $"{names[i]} 超出 {containerName} 上边界");
                Assert.LessOrEqual(bounds.xMax, containerBounds.xMax + 1f, $"{names[i]} 超出 {containerName} 右边界");
                Assert.LessOrEqual(bounds.yMax, containerBounds.yMax + 1f, $"{names[i]} 超出 {containerName} 下边界");
            }
        }

        static void AssertButtonRow(VisualElement root, string leftName, string rightName)
        {
            Rect left = root.Q<VisualElement>(leftName).worldBound;
            Rect right = root.Q<VisualElement>(rightName).worldBound;
            Assert.AreEqual(left.yMin, right.yMin, 1f, $"{leftName} 与 {rightName} 未处于同一行");
            Assert.AreEqual(left.width, right.width, 2f, $"{leftName} 与 {rightName} 宽度不一致");
            Assert.Less(left.xMax, right.xMin, $"{leftName} 与 {rightName} 发生重叠");
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }
    }
}
