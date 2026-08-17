using System.Collections;
using System.Collections.Generic;
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
    public class Phase1UxPlayModeTests : InputTestFixture
    {
        readonly List<Object> _objects = new List<Object>();

        IArchitecture _architecture;

        public override void Setup()
        {
            // InputTestFixture 会替换全局 Input System。旧架构必须先在原管理器中释放，
            // 否则真实设备的状态监视器会残留并在测试结束后触发空引用。
            GameArchitecture.Interface.Deinit();
            base.Setup();
            _architecture = GameArchitecture.Interface;
        }

        public override void TearDown()
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
            _architecture?.Deinit();
            _architecture = null;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ShopPurchase_UsesGridAndRestoresNeighborFocusFeedbackAndTooltip()
        {
            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
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
            StringAssert.Contains("已购买", feedback.text, "交易反馈在刷新帧内被覆盖");
            VisualElement detailRoot = root.Q<VisualElement>("item-tooltip");
            Label detailName = detailRoot.Q<Label>("item-detail-name");
            Assert.AreEqual(shop.SelectedItem.BaseDefinition.DisplayName, detailName.text, "详情未跟随邻近选择");
            AssertFocusedSelectedItem(root, shop.SelectedItem.BaseDefinition.DisplayName);

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
            Press(keyboard.rightArrowKey);
            yield return null;
            Release(keyboard.rightArrowKey);
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
                new Vector2Int(1280, 720),
                new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1440),
            };
            WorldInteractionTarget crafting = FindTarget(GameMenuPage.Crafting);
            Assert.IsNotNull(crafting, "Main 场景缺少打造台交互目标");

            try
            {
                for (int i = 0; i < resolutions.Length; i++)
                {
                    Vector2Int resolution = resolutions[i];
                    SetGameViewResolution(resolution);
                    yield return WaitForResolution(resolution, 5f);
                    Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(merchant)), "无法打开商店布局验收");
                    yield return null;
                    yield return null;
                    List<Button> merchantPreviewButtons = root.Query<Button>(className: "shop-item").ToList();
                    Assert.IsNotEmpty(merchantPreviewButtons, "商人背包缺少可悬停物品");
                    merchantPreviewButtons[0].Focus();
                    yield return null;
                    yield return null;
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
                    });
                    AssertWorkbenchShare(root);
                    AssertMerchantBackpack(root);

                    if (resolution.x >= 1920)
                    {
                        AssertElementsDoNotOverlap(root, "item-tooltip", "game-menu-panel");
                        AssertTooltipSide(root, true);
                    }

                    Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(crafting)), "无法打开打造页布局验收");
                    yield return null;
                    yield return null;
                    List<Button> playerPreviewButtons = root.Query<Button>(className: "inventory-item").ToList();
                    Assert.IsNotEmpty(playerPreviewButtons, "玩家背包缺少可悬停物品");
                    playerPreviewButtons[0].Focus();
                    yield return null;
                    yield return null;
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

                    if (resolution.x >= 1920)
                    {
                        AssertTooltipSide(root, false);
                    }
                }

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
                StringAssert.Contains("已出售", feedback.text, "右键出售没有显示交易反馈");
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
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
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
            Set(mouse.position, sourceScreen);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Assert.IsTrue(panel.IsPointerPending, "PointerDown 未建立拖拽候选");
            Set(mouse.position, Vector2.Lerp(sourceScreen, destinationScreen, 0.35f));
            yield return null;
            Assert.IsTrue(panel.IsDragging, "Pointer 移动超过阈值后未进入拖拽状态");
            Set(mouse.position, destinationScreen);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsFalse(panel.IsDragging, "Pointer 抬起后拖拽状态未结束");
        }

        IEnumerator RightClickPointer(Mouse mouse, VisualElement root, Vector2 panelPosition)
        {
            Set(mouse.position, PanelToScreen(root, panelPosition));
            yield return null;
            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
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
                    new ModifierDetailSnapshot(null, "护甲", $"护甲提高 {20 + i}%"),
                };
                List<ModifierDetailSnapshot> suffixModifiers = new List<ModifierDetailSnapshot>
                {
                    new ModifierDetailSnapshot(null, "额外火焰伤害", $"获得等同于物理伤害 {10 + i}% 的额外火焰伤害"),
                };
                prefixes.Add(new AffixDetailSnapshot(
                    null,
                    AffixType.Prefix,
                    $"测试前缀 {i + 1}",
                    prefixModifiers,
                    prefixModifiers[0].DisplayText,
                    0f));
                suffixes.Add(new AffixDetailSnapshot(
                    null,
                    AffixType.Suffix,
                    $"测试后缀 {i + 1}",
                    suffixModifiers,
                    suffixModifiers[0].DisplayText,
                    0f));
            }

            return new ItemDetailSnapshot(
                item,
                item.InstanceId,
                item.BaseDefinition.DisplayName,
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

        static void AssertLayoutInsideRoot(VisualElement root, IReadOnlyList<string> names)
        {
            Rect rootBounds = root.worldBound;

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

        static void AssertTooltipSide(VisualElement root, bool right)
        {
            Rect tooltip = root.Q<VisualElement>("item-tooltip").worldBound;
            Rect panel = root.Q<VisualElement>("game-menu-panel").worldBound;
            Assert.AreEqual(panel.yMin, tooltip.yMin, 1f, "物品信息栏顶部未与主 UI 对齐");

            if (right)
            {
                Assert.GreaterOrEqual(tooltip.xMin, panel.xMax, "商人物品信息栏未固定在主 UI 右侧");
                return;
            }

            Assert.LessOrEqual(tooltip.xMax, panel.xMin, "玩家物品信息栏未固定在主 UI 左侧");
        }

        static void AssertWorkbenchShare(VisualElement root)
        {
            Rect content = root.Q<VisualElement>("game-menu-content").worldBound;
            Rect workbench = root.Q<VisualElement>("item-workbench-shared").worldBound;
            Rect equipment = root.Q<VisualElement>("inventory-equipment").worldBound;
            Rect inventory = root.Q<VisualElement>("inventory-grid-frame").worldBound;
            Assert.LessOrEqual(
                workbench.width,
                content.width * 0.5f + 1f,
                "商店或打造界面的装备/背包列超过内容区一半");
            Assert.LessOrEqual(equipment.yMax, inventory.yMin + 1f, "装备区没有位于背包上方");

            VisualElement[] pages =
            {
                root.Q<VisualElement>("inventory-page"),
                root.Q<VisualElement>("shop-page"),
                root.Q<VisualElement>("crafting-page"),
            };
            VisualElement activePage = null;

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i].resolvedStyle.display == DisplayStyle.Flex)
                {
                    activePage = pages[i];
                    break;
                }
            }

            Assert.IsNotNull(activePage, "没有找到当前显示的物品上下文页");
            Assert.AreEqual(workbench.yMin, activePage.worldBound.yMin, 1f, "背包与上下文顶部未对齐");
            Assert.AreEqual(workbench.yMax, activePage.worldBound.yMax, 1f, "背包与上下文底部未对齐");
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
