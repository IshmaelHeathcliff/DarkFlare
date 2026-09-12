using System;
using System.Collections;
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
    public class Phase0ExperiencePlayModeTests
    {
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
            Time.timeScale = 1f;
            _architecture = null;
            yield return _fixture.StopCurrent();
            yield return null;
            InputTestFixtureGuard.TearDown(_inputFixture);
            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator MainScene_InventorySupportsKeyboardGamepadAndStandardResolution()
        {
            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            yield return _fixture.EnterMain();

            GameMenuController menu = null;
            RuntimePanelView document = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((menu == null
                    || menu.SessionBindCount == 0
                    || document == null
                    || document.Root?.panel == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<RuntimePanelView>() : null;
                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未在 15 秒内初始化 GameMenuController");
            Assert.IsNotNull(document, "UIRoot 缺少 RuntimePanelView");
            IArchitecture architecture = menu.GetArchitecture();
            ShopSnapshot shop = default;
            InventorySnapshot inventory = default;
            timeout = Time.realtimeSinceStartup + 15f;

            while ((shop.MerchantItems == null
                    || shop.MerchantItems.Count == 0
                    || !inventory.HasPlayer)
                   && Time.realtimeSinceStartup < timeout)
            {
                shop = architecture.SendQuery(new GetShopSnapshotQuery());
                inventory = architecture.SendQuery(new GetInventorySnapshotQuery());
                yield return null;
            }

            Assert.IsNotNull(shop.MerchantItems, "Main 场景未初始化商人库存");
            Assert.IsNotEmpty(shop.MerchantItems, "Main 场景商人库存为空");
            Assert.IsTrue(inventory.HasPlayer, "Main 场景未生成玩家");
            ItemInstance acceptanceItem = shop.MerchantItems[0].Item;
            Assert.IsTrue(architecture.SendCommand(new BuyItemCommand(acceptanceItem)), "无法购买验收装备");
            yield return null;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            _inputFixture.PressAndRelease(keyboard.tabKey);
            yield return null;
            AssertMenuOpen(menu, document, "键盘 Tab");

            _inputFixture.PressAndRelease(keyboard.escapeKey);
            yield return null;
            AssertMenuClosed(menu, document, "键盘 Esc");

            _inputFixture.PressAndRelease(gamepad.startButton);
            yield return null;
            AssertMenuOpen(menu, document, "手柄 Start");

            _inputFixture.PressAndRelease(gamepad.buttonEast);
            yield return null;
            AssertMenuClosed(menu, document, "手柄 B");

            try
            {
                Vector2Int resolution = new Vector2Int(1920, 1080);
                SetGameViewResolution(resolution);
                yield return WaitForResolution(resolution, 5f);
                menu.OpenPage(GameMenuPage.Inventory);
                yield return null;

                Assert.AreEqual(resolution.x, Screen.width, $"Game View 宽度未切换为 {resolution.x}");
                Assert.AreEqual(resolution.y, Screen.height, $"Game View 高度未切换为 {resolution.y}");
                AssertMenuOpen(menu, document, "1920×1080");
                AssertLayoutInsideRoot(document.Root);
                menu.GetArchitecture().GetUtility<GameInput>().SwitchToGameplay();
                yield return null;

                Assert.IsTrue(
                    architecture.SendCommand(new EquipItemCommand(inventory.Player, acceptanceItem)),
                    "无法装备背包中的验收装备");
                yield return null;
                InventorySnapshot equipped = architecture.SendQuery(new GetInventorySnapshotQuery());
                Assert.AreSame(acceptanceItem, equipped.CurrentWeapon, "装备命令未更新当前武器");
            }
            finally
            {
                SetGameViewResolution(new Vector2Int(originalWidth, originalHeight));
                menu.GetArchitecture().GetUtility<GameInput>().SwitchToGameplay();
                Time.timeScale = 1f;
            }

            Debug.Log("[Phase0ExperiencePlayMode] 键鼠、手柄、背包焦点与 1920×1080 验收通过");
        }

        [UnityTest]
        public IEnumerator MainScene_PointerDragMovesInventoryItemAndEquipsIt()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = null;
            RuntimePanelView document = null;
            InventorySnapshot inventory = default;
            ShopSnapshot shop = default;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((menu == null
                    || menu.SessionBindCount == 0
                    || document == null
                    || document.Root?.panel == null
                    || !inventory.HasPlayer
                    || shop.MerchantItems == null
                    || shop.MerchantItems.Count == 0)
                   && Time.realtimeSinceStartup < timeout)
            {
                menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<RuntimePanelView>() : null;

                if (menu != null && menu.SessionBindCount > 0)
                {
                    IArchitecture current = menu.GetArchitecture();
                    inventory = current.SendQuery(new GetInventorySnapshotQuery());
                    shop = current.SendQuery(new GetShopSnapshotQuery());
                }

                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未初始化菜单");
            Assert.IsNotNull(document, "UIRoot 缺少 RuntimePanelView");
            Assert.IsTrue(inventory.HasPlayer, "Main 场景未生成玩家");
            IArchitecture architecture = menu.GetArchitecture();
            ItemInstance weapon = null;

            for (int i = 0; i < shop.MerchantItems.Count; i++)
            {
                ItemInstance candidate = shop.MerchantItems[i].Item;

                if (candidate?.BaseDefinition != null
                    && candidate.BaseDefinition.CanEquipTo(EquipmentSlot.Weapon))
                {
                    weapon = candidate;
                    break;
                }
            }

            Assert.IsNotNull(weapon, "商人库存缺少可用于拖拽验收的武器");
            Assert.IsTrue(architecture.SendCommand(new BuyItemCommand(weapon)), "无法购买拖拽验收武器");
            yield return null;
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null;
            yield return null;

            InventoryPanelController panel = menu.GetComponent<InventoryPanelController>();
            Assert.IsNotNull(panel, "UIRoot 缺少 InventoryPanelController");
            VisualElement root = document.Root;
            InventoryItemSnapshot before = FindInventoryItem(architecture, weapon);
            Vector2Int targetOrigin = FindMoveTarget(architecture, weapon, before.Placement);
            Button itemButton = FindInventoryButton(root, weapon.BaseDefinition.DisplayName);
            VisualElement grid = root.Q<VisualElement>("inventory-grid");
            float step = 52f;
            Vector2 targetPoint = new Vector2(
                grid.worldBound.xMin + targetOrigin.x * step + 24f,
                grid.worldBound.yMin + targetOrigin.y * step + 24f);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Vector2 itemScreenPosition = PanelToScreen(root, itemButton.worldBound.center);
            _inputFixture.Set(mouse.position, itemScreenPosition);
            yield return null;
            _inputFixture.Press(mouse.leftButton);
            yield return null;
            Assert.IsTrue(panel.IsPointerPending, "鼠标按下后未进入拖拽候选状态");
            _inputFixture.Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsFalse(panel.IsPointerPending, "鼠标单击松开后仍残留拖拽候选状态");
            Assert.IsFalse(panel.IsDragging, "鼠标单击不应拿起物品");
            Assert.AreEqual(
                before.Placement.position,
                FindInventoryItem(architecture, weapon).Placement.position,
                "鼠标单击不应移动物品");
            Assert.IsNotNull(
                root.Q<VisualElement>(className: "inventory-item--selected"),
                "鼠标单击应只选择背包物品");
            Assert.IsNull(
                root.Q<VisualElement>(className: "inventory-equipment-slot--selected"),
                "选择背包物品时不应同时高亮装备槽");
            yield return DragPointer(mouse, root, itemButton.worldBound.center, targetPoint, panel);

            InventoryItemSnapshot moved = FindInventoryItem(architecture, weapon);
            Assert.AreNotEqual(before.Placement.position, moved.Placement.position, "Pointer 拖拽未更新背包位置");
            itemButton = FindInventoryButton(root, weapon.BaseDefinition.DisplayName);
            Button weaponSlot = root.Q<Button>("inventory-slot-weapon");
            yield return DragPointer(mouse, root, itemButton.worldBound.center, weaponSlot.worldBound.center, panel);

            InventorySnapshot equipped = architecture.SendQuery(new GetInventorySnapshotQuery());
            Assert.AreSame(weapon, equipped.CurrentWeapon, "Pointer 拖拽未将武器装备到目标槽");
            Assert.IsFalse(ContainsItem(equipped, weapon), "装备成功后武器仍留在背包");
            Vector2Int unequipOrigin = FindUnequipTarget(architecture, equipped.Player, EquipmentSlot.Weapon);
            targetPoint = new Vector2(
                grid.worldBound.xMin + unequipOrigin.x * step + 24f,
                grid.worldBound.yMin + unequipOrigin.y * step + 24f);
            yield return DragPointer(mouse, root, weaponSlot.worldBound.center, targetPoint, panel);

            InventorySnapshot unequipped = architecture.SendQuery(new GetInventorySnapshotQuery());
            Assert.IsNull(unequipped.CurrentWeapon, "从装备槽拖回背包后武器仍处于装备状态");
            Assert.IsTrue(ContainsItem(unequipped, weapon), "从装备槽拖回背包后武器未回到背包");
            itemButton = FindInventoryButton(root, weapon.BaseDefinition.DisplayName);
            _inputFixture.Set(mouse.position, PanelToScreen(root, root.worldBound.max - new Vector2(12f, 12f)));
            yield return null;
            Assert.AreEqual(
                DisplayStyle.None,
                root.Q<VisualElement>("item-tooltip").resolvedStyle.display,
                "鼠标离开所有物品后提示窗仍然可见");
            _inputFixture.Set(mouse.position, PanelToScreen(root, itemButton.worldBound.center));
            yield return null;
            yield return null;
            VisualElement tooltip = root.Q<VisualElement>("item-tooltip");

            for (int i = 0; i < 10 && tooltip.resolvedStyle.visibility != Visibility.Visible; i++)
            {
                yield return null;
            }

            Assert.AreEqual(DisplayStyle.Flex, tooltip.resolvedStyle.display, "拖回背包后首次悬停未显示物品信息");
            Assert.AreEqual(Visibility.Visible, tooltip.resolvedStyle.visibility, "物品信息完成定位后仍不可见");
            Assert.AreEqual(
                root.Q<VisualElement>("game-menu-content").worldBound.yMin,
                tooltip.worldBound.yMin,
                1f,
                "拖回背包后物品信息顶部位置错误");
            architecture.GetUtility<GameInput>().SwitchToGameplay();
            yield return null;
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
            VisualElement picked = root.panel.Pick(source);
            Assert.IsTrue(
                panel.IsPointerPending,
                $"PointerDown 未进入待拖拽状态；picked={picked?.name ?? "null"}，mouse={mouse.position.ReadValue()}，source={sourceScreen}");
            _inputFixture.Set(mouse.position, Vector2.Lerp(sourceScreen, destinationScreen, 0.35f));
            yield return null;
            Assert.IsTrue(
                panel.IsDragging,
                $"Pointer 移动超过阈值后未进入拖拽状态；pending={panel.IsPointerPending}");
            _inputFixture.Set(mouse.position, destinationScreen);
            yield return null;
            _inputFixture.Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsFalse(panel.IsDragging, "Pointer 抬起后拖拽状态未结束");
            Assert.AreEqual(
                DisplayStyle.None,
                root.Q<VisualElement>("item-tooltip").resolvedStyle.display,
                "Pointer 放下物品后提示窗不应立即遮挡背包");
        }

        static Vector2 PanelToScreen(VisualElement root, Vector2 point)
        {
            Rect bounds = root.worldBound;
            float normalizedX = (point.x - bounds.xMin) / bounds.width;
            float normalizedY = (point.y - bounds.yMin) / bounds.height;
            return new Vector2(normalizedX * Screen.width, (1f - normalizedY) * Screen.height);
        }

        static InventoryItemSnapshot FindInventoryItem(IArchitecture architecture, ItemInstance item)
        {
            InventorySnapshot snapshot = architecture.SendQuery(new GetInventorySnapshotQuery());

            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                if (snapshot.Items[i].Item == item)
                {
                    return snapshot.Items[i];
                }
            }

            Assert.Fail("背包快照中缺少验收物品");
            return default;
        }

        static Vector2Int FindMoveTarget(
            IArchitecture architecture,
            ItemInstance item,
            RectInt currentPlacement)
        {
            for (int y = 5; y >= 0; y--)
            {
                for (int x = 9; x >= 0; x--)
                {
                    Vector2Int origin = new Vector2Int(x, y);

                    if (origin != currentPlacement.position
                        && architecture.SendQuery(new CanMoveInventoryItemQuery(item, origin)))
                    {
                        return origin;
                    }
                }
            }

            Assert.Fail("背包中没有可用于 Pointer 拖拽验收的位置");
            return default;
        }

        static Vector2Int FindUnequipTarget(
            IArchitecture architecture,
            CombatActor player,
            EquipmentSlot slot)
        {
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    Vector2Int origin = new Vector2Int(x, y);

                    if (architecture.SendQuery(new CanUnequipItemToGridQuery(player, slot, origin)))
                    {
                        return origin;
                    }
                }
            }

            Assert.Fail("背包中没有可用于装备拖回验收的位置");
            return default;
        }

        static Button FindInventoryButton(VisualElement root, string displayName)
        {
            System.Collections.Generic.List<Button> buttons = root.Query<Button>(className: "inventory-item").ToList();

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].userData is ItemInstance item
                    && item.BaseDefinition != null
                    && item.BaseDefinition.DisplayName == displayName)
                {
                    return buttons[i];
                }
            }

            Assert.Fail($"背包 UI 中缺少物品按钮：{displayName}");
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

        static IEnumerator WaitForResolution(Vector2Int resolution, float timeoutSeconds)
        {
            float timeout = Time.realtimeSinceStartup + timeoutSeconds;

            while ((Screen.width != resolution.x || Screen.height != resolution.y)
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        static void SetGameViewResolution(Vector2Int resolution)
        {
            const string UtilityTypeName = "DarkFlare.Editor.Phase0GameViewResolutionUtility, DarkFlare.Editor";
            System.Type utilityType = System.Type.GetType(UtilityTypeName, true);
            MethodInfo method = utilityType.GetMethod("SetResolution", BindingFlags.Static | BindingFlags.Public)
                ?? throw new MissingMethodException(UtilityTypeName, "SetResolution");
            method.Invoke(null, new object[] { resolution.x, resolution.y });
        }

        static void AssertMenuOpen(
            GameMenuController menu,
            RuntimePanelView document,
            string inputPath)
        {
            VisualElement overlay = document.Root.Q<VisualElement>("game-menu-overlay");
            Focusable focusedElement = document.Root.focusController.focusedElement;
            VisualElement detailRoot = document.Root.Q<VisualElement>("item-tooltip");
            Assert.IsTrue(menu.IsOpen, $"{inputPath} 未打开菜单");
            Assert.AreEqual(DisplayStyle.Flex, overlay.resolvedStyle.display, $"{inputPath} 菜单遮罩不可见");
            Assert.AreEqual(0f, Time.timeScale, $"{inputPath} 打开菜单后未暂停玩法");
            Assert.IsNotNull(focusedElement, $"{inputPath} 打开菜单后没有默认焦点");
            Assert.IsNull(
                document.Root.Q<VisualElement>(className: "inventory-item--selected"),
                $"{inputPath} 在没有操作物品时错误创建了默认选择");
            Assert.AreEqual(
                DisplayStyle.None,
                detailRoot.resolvedStyle.display,
                $"{inputPath} 在没有悬停物品时错误显示了默认物品信息");
        }

        static void AssertMenuClosed(GameMenuController menu, RuntimePanelView document, string inputPath)
        {
            VisualElement overlay = document.Root.Q<VisualElement>("game-menu-overlay");
            Assert.IsFalse(menu.IsOpen, $"{inputPath} 未关闭菜单");
            Assert.AreEqual(DisplayStyle.None, overlay.resolvedStyle.display, $"{inputPath} 关闭后菜单遮罩仍可见");
            Assert.AreEqual(1f, Time.timeScale, $"{inputPath} 关闭菜单后未恢复玩法");
        }

        static void AssertLayoutInsideRoot(VisualElement root)
        {
            string[] names =
            {
                "game-menu-panel",
                "inventory-page",
                "inventory-grid",
                "item-workbench-shared",
                "inventory-equip",
                "game-menu-close",
            };
            Rect rootBounds = root.worldBound;

            for (int i = 0; i < names.Length; i++)
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

            AssertInventoryGridInsideFrame(root);
            Rect equipment = root.Q<VisualElement>("inventory-equipment").worldBound;
            Rect inventory = root.Q<VisualElement>("inventory-grid-frame").worldBound;
            Assert.LessOrEqual(equipment.yMax, inventory.yMin + 1f, "装备区没有位于背包上方");
            Rect cell = root.Q<VisualElement>(className: "inventory-cell").worldBound;
            Rect weapon = root.Q<VisualElement>("inventory-slot-weapon").worldBound;
            Rect armor = root.Q<VisualElement>("inventory-slot-armor").worldBound;
            Rect ringLeft = root.Q<VisualElement>("inventory-slot-ring-left").worldBound;
            Rect ringRight = root.Q<VisualElement>("inventory-slot-ring-right").worldBound;
            Assert.GreaterOrEqual(weapon.width, cell.width * 2f + 4f, "武器槽宽度小于背包 2 格");
            Assert.GreaterOrEqual(weapon.height, cell.height * 3f + 8f, "武器槽高度小于背包 3 格");
            Assert.GreaterOrEqual(armor.width, cell.width * 2f + 4f, "护甲槽宽度小于背包 2 格");
            Assert.GreaterOrEqual(armor.height, cell.height * 3f + 8f, "护甲槽高度小于背包 3 格");
            Assert.GreaterOrEqual(ringLeft.width, cell.width, "左戒指槽小于背包 1 格");
            Assert.GreaterOrEqual(ringLeft.height, cell.height, "左戒指槽高度不足一个物品格");
            Assert.GreaterOrEqual(ringRight.width, cell.width, "右戒指槽小于背包 1 格");
            Assert.GreaterOrEqual(ringRight.height, cell.height, "右戒指槽高度不足一个物品格");
        }

        static void AssertInventoryGridInsideFrame(VisualElement root)
        {
            Rect frame = root.Q<VisualElement>("inventory-grid-frame").worldBound;
            Rect grid = root.Q<VisualElement>("inventory-grid").worldBound;
            Assert.GreaterOrEqual(grid.xMin, frame.xMin - 1f, "背包格超出左边界");
            Assert.GreaterOrEqual(grid.yMin, frame.yMin - 1f, "背包格超出上边界");
            Assert.LessOrEqual(grid.xMax, frame.xMax + 1f, "背包格超出右边界");
            Assert.LessOrEqual(grid.yMax, frame.yMax + 1f, "背包格超出下边界");
        }
    }
}
