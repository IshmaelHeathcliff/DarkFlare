using System;
using System.Collections;
using System.IO;
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
    public class Phase0ExperiencePlayModeTests : InputTestFixture
    {
        IArchitecture _architecture;

        public override void Setup()
        {
            base.Setup();
            GameArchitecture.Interface.Deinit();
            _architecture = GameArchitecture.Interface;
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            _architecture?.Deinit();
            _architecture = null;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator MainScene_InventorySupportsKeyboardGamepadAndTargetResolutions()
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
                menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<UIDocument>() : null;
                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未在 15 秒内初始化 GameMenuController");
            Assert.IsNotNull(document, "UIRoot 缺少 UIDocument");
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

            PressAndRelease(keyboard.tabKey);
            yield return null;
            AssertMenuOpen(menu, document, acceptanceItem, "键盘 Tab");

            PressAndRelease(keyboard.escapeKey);
            yield return null;
            AssertMenuClosed(menu, document, "键盘 Esc");

            PressAndRelease(gamepad.startButton);
            yield return null;
            AssertMenuOpen(menu, document, acceptanceItem, "手柄 Start");

            PressAndRelease(gamepad.buttonEast);
            yield return null;
            AssertMenuClosed(menu, document, "手柄 B");

            try
            {
                Vector2Int[] resolutions =
                {
                    new Vector2Int(1280, 720),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440),
                };

                for (int i = 0; i < resolutions.Length; i++)
                {
                    Vector2Int resolution = resolutions[i];
                    SetGameViewResolution(resolution);
                    yield return WaitForResolution(resolution, 5f);
                    menu.OpenPage(GameMenuPage.Inventory);
                    yield return null;

                    Assert.AreEqual(resolution.x, Screen.width, $"Game View 宽度未切换为 {resolution.x}");
                    Assert.AreEqual(resolution.y, Screen.height, $"Game View 高度未切换为 {resolution.y}");
                    AssertMenuOpen(menu, document, acceptanceItem, $"{resolution.x}×{resolution.y}");
                    AssertLayoutInsideRoot(document.rootVisualElement);
                    CaptureAcceptanceScreenshot(resolution);
                    yield return new WaitForEndOfFrame();
                    menu.GetArchitecture().GetUtility<GameInput>().SwitchToGameplay();
                    yield return null;
                }

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

            Debug.Log("[Phase0ExperiencePlayMode] 键鼠、手柄、背包焦点与三档分辨率验收通过");
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
            UIDocument document,
            ItemInstance selectedItem,
            string inputPath)
        {
            VisualElement overlay = document.rootVisualElement.Q<VisualElement>("game-menu-overlay");
            Focusable focusedElement = document.rootVisualElement.focusController.focusedElement;
            Label selectedName = document.rootVisualElement.Q<Label>("inventory-selected-name");
            Assert.IsTrue(menu.IsOpen, $"{inputPath} 未打开菜单");
            Assert.AreEqual(DisplayStyle.Flex, overlay.resolvedStyle.display, $"{inputPath} 菜单遮罩不可见");
            Assert.AreEqual(0f, Time.timeScale, $"{inputPath} 打开菜单后未暂停玩法");
            Assert.IsNotNull(focusedElement, $"{inputPath} 打开菜单后没有默认焦点");
            Assert.AreEqual(
                selectedItem.BaseDefinition.DisplayName,
                selectedName.text,
                $"{inputPath} 未显示当前选中装备");
        }

        static void AssertMenuClosed(GameMenuController menu, UIDocument document, string inputPath)
        {
            VisualElement overlay = document.rootVisualElement.Q<VisualElement>("game-menu-overlay");
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
                "inventory-selected-name",
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
        }

        static void CaptureAcceptanceScreenshot(Vector2Int resolution)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(projectRoot, "docs", "assets", "visual-style");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(
                folder,
                $"phase-0-acceptance-inventory-{resolution.x}x{resolution.y}.png");
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
