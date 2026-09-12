using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
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
        public IEnumerator PanelReload_RebindsGameplayAndPreservesRendererDisabledContent()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            RuntimePanelView view = menu.GetComponent<RuntimePanelView>();
            HudController hud = menu.GetComponent<HudController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            VisualTreeAsset source = view.Renderer.visualTreeAsset;
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((view.Root?.panel == null || menu.SessionBindCount == 0) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsNotNull(view.Root?.panel);
            try
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    VisualElement entry = view.Root.Q("game-hud-inventory");
                    view.Renderer.enabled = false;
                    yield return null;
                    view.Renderer.enabled = true;
                    yield return null;
                    Assert.AreSame(entry, view.Root.Q("game-hud-inventory"), "禁用 Renderer 必须保留视觉内容");
                    view.gameObject.SetActive(false);
                    yield return null;
                    view.gameObject.SetActive(true);
                    yield return null;
                    yield return null;
                    Assert.AreSame(entry, view.Root.Q("game-hud-inventory"), "禁用宿主后应复用内容并重建 Controller 订阅");
                    menu.OpenPage(GameMenuPage.Inventory);
                    yield return null;
                    int binds = menu.SessionBindCount;
                    VisualTreeAsset replacement = Object.Instantiate(source);
                    _objects.Add(replacement);
                    view.Renderer.visualTreeAsset = replacement;
                    yield return null;
                    yield return null;
                    Assert.AreNotSame(entry, view.Root.Q("game-hud-inventory"), "重载后必须绑定新子树");
                    Assert.AreEqual(binds + 1, menu.SessionBindCount);
                    Assert.IsFalse(menu.IsOpen, "重建视觉树应结束旧窗口与拖放事务");
                    Assert.IsTrue(ApplicationHost.Current.Input.IsGameplayEnabled);
                    Assert.AreEqual(Object.FindAnyObjectByType<PlayerController>().Actor.CurrentHealth, hud.RuntimeSnapshot.Health);
                    _inputFixture.PressAndRelease(keyboard.tabKey);
                    yield return null;
                    yield return null;
                    Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Inventory), "重载后一次输入只能切换一次");
                    _inputFixture.PressAndRelease(pad.buttonEast);
                    yield return null;
                    yield return null;
                    Assert.IsFalse(menu.IsOpen);
                    Assert.IsTrue(ApplicationHost.Current.Input.IsGameplayEnabled);
                }
            }
            finally
            {
                view.gameObject.SetActive(true);
                view.Renderer.enabled = true;
                view.Renderer.visualTreeAsset = source;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hud_CurrentResourcesMotionAndEntriesRespectInputOwnership()
        {
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            yield return host.PlatformLifecycle.HandleFocusChangedAsync(true).ToCoroutine();
            yield return host.Accessibility.SetReduceMotionAsync(false).ToCoroutine();
            yield return host.Input.SetGlyphPreferenceAsync(InputGlyphPreference.Auto).ToCoroutine();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            HudController hud = Object.FindAnyObjectByType<HudController>();
            VisualElement root = hud.GetComponent<RuntimePanelView>().Root;
            IArchitecture architecture = menu.GetArchitecture();
            architecture.GetUtility<GameInput>().SwitchToGameplay();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            CombatActor actor = player.Actor;
            player.enabled = false;
            Object.FindAnyObjectByType<MonsterSpawner>().enabled = false;
            var damage = new Dictionary<DamageType, float> { { DamageType.Physical, actor.CurrentHealth - actor.MaxHealth * 0.2f } };
            CombatResourceSnapshot previousResources = actor.Resources;
            actor.ReceiveDamage(new DamageResult(true, false, damage, damage));
            architecture.GetSystem<CombatSystem>().PublishResourceChanges(actor, previousResources, ActorResourceChangeReason.Damage);
            Assert.AreEqual(0.2f, root.Q<ProgressBar>("health-bar").value, 0.001f);
            Assert.IsTrue(root.Q("health-card").ClassListContains("hud-resource--low"));
            Assert.IsTrue(hud.IsWarningAnimating);
            yield return host.Accessibility.SetReduceMotionAsync(true).ToCoroutine();
            Assert.IsFalse(hud.IsWarningAnimating);
            Assert.AreEqual(0f, root.Q("hud-low-warning").style.opacity.value);
            yield return host.Accessibility.SetReduceMotionAsync(false).ToCoroutine();
            Assert.IsTrue(hud.IsWarningAnimating);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            VisualElement hudEntry = root.Q("game-hud-inventory");
            Rect entryBounds = hudEntry.worldBound;
            float entryBorder = hudEntry.resolvedStyle.borderLeftWidth;
            _inputFixture.Set(mouse.position, PanelToScreen(root, root.Q("game-hud-inventory").worldBound.center));
            yield return null;
            _inputFixture.Press(mouse.leftButton);
            yield return null;
            Assert.AreEqual(entryBounds, hudEntry.worldBound, "按下不能改变入口视觉位置或尺寸");
            Assert.AreEqual(entryBorder, hudEntry.resolvedStyle.borderLeftWidth, "焦点 / 按下不应改变内容边界");
            Assert.AreEqual(Vector3.one, hudEntry.resolvedStyle.scale.value, "按下不得缩放命中区域");
            _inputFixture.Release(mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Inventory));
            Assert.IsFalse(hud.IsWarningAnimating);
            Assert.AreEqual(DisplayStyle.None, root.Q("game-hud-inventory").resolvedStyle.display);
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            _inputFixture.PressAndRelease(pad.buttonEast);
            yield return null;
            Assert.IsFalse(menu.IsOpen);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsTrue(hud.IsWarningAnimating);
            Assert.IsTrue(root.Q("hud-inventory-binding").ClassListContains("input-glyph--gamepad-start"),
                $"Family={host.Input.DisplayDeviceFamily}; token={host.Input.GetGlyphToken(RebindableInputAction.PlayerToggleMenu).GlyphId}; classes={string.Join(",", root.Q("hud-inventory-binding").GetClasses())}");
            Assert.LessOrEqual(root.Q("skill-cooldown").worldBound.yMax, root.Q("hud-entries").worldBound.yMin);
            architecture.GetSystem<CombatSystem>().Revive(actor, actor.transform.position);
            Assert.IsFalse(hud.IsWarningAnimating);
            Assert.IsFalse(root.Q("health-card").ClassListContains("hud-resource--low"));
            hud.enabled = false;
            Assert.IsFalse(hud.IsWarningAnimating);
            Assert.IsFalse(hud.RuntimeSnapshot.HasPlayer);
            hud.enabled = true;
            yield return null;
            Assert.AreEqual(actor.CurrentHealth, hud.RuntimeSnapshot.Health);
        }

        [UnityTest]
        public IEnumerator Attributes_IndependentWindowsRefreshAndNavigateWithoutTakingItemOwnership()
        {
            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            IArchitecture architecture = menu.GetArchitecture();
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
            ApplicationHost host = ApplicationHost.Current;
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                SetGameViewResolution(new Vector2Int(1920, 1080));
                yield return WaitForResolution(new Vector2Int(1920, 1080), 5f);
                menu.OpenPage(GameMenuPage.Inventory);
                yield return null;
                root.Q<Button>("inventory-attributes-open").Focus();
                _inputFixture.PressAndRelease(pad.buttonSouth);
                yield return null;
                yield return null;
                Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Attributes));
                Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Inventory));
                Assert.AreEqual("paused", menu.Attributes.RuntimeSnapshot.ResourceState);
                float remaining = menu.Attributes.RuntimeSnapshot.RemainingSeconds;
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.AreEqual(remaining, menu.Attributes.RuntimeSnapshot.RemainingSeconds, 0.01f);
                ScrollView scroll = root.Q<ScrollView>("attributes-scroll");
                Toggle resources = root.Q<Foldout>("attributes-group-resources").Q<Toggle>();
                resources.Focus();
                _inputFixture.PressAndRelease(pad.buttonSouth);
                yield return null;
                Assert.IsFalse(root.Q<Foldout>("attributes-group-resources").value);
                _inputFixture.Press(pad.dpad.down);
                yield return null;
                _inputFixture.Release(pad.dpad.down);
                yield return null;
                Assert.AreNotSame(resources, root.focusController.focusedElement);
                Assert.IsTrue(root.Q("attributes-window").Contains((VisualElement)root.focusController.focusedElement));
                menu.ClosePage(GameMenuPage.Inventory);
                yield return null;
                Assert.IsTrue(menu.IsOpen);
                Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Attributes));
                Assert.AreEqual(DisplayStyle.None, root.Q("item-operation-bar").resolvedStyle.display);
                menu.TogglePause();
                yield return null;
                host.ApplicationShell.OpenSettings();
                yield return null;
                Assert.IsFalse(menu.IsWindowVisible(GameMenuPage.Attributes));
                _inputFixture.PressAndRelease(pad.buttonEast);
                yield return null;
                yield return null;
                Assert.IsFalse(host.ApplicationShell.BlocksGameplay);
                menu.TogglePause();
                yield return null;
                Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Attributes));
                InventoryModel inventory = architecture.GetModel<InventoryModel>();
                inventory.AddGold(10000);
                ItemInstance armor = architecture.GetModel<EconomyModel>().MerchantStock.First(item => item.BaseDefinition.CanEquipTo(EquipmentSlot.Armor));
                Assert.IsTrue(architecture.SendCommand(new BuyItemCommand(armor)));
                CombatActor player = architecture.SendQuery(new GetInventorySnapshotQuery()).Player;
                Assert.IsTrue(architecture.GetSystem<EquipmentSystem>().Equip(player, armor, EquipmentSlot.Armor));
                yield return null;
                Assert.IsTrue(menu.Attributes.LastSnapshot.Modifiers.Any(modifier => modifier.Origin.ItemId == armor.InstanceId));
                Assert.AreEqual(player.MaxHealth, menu.Attributes.LastSnapshot.Attributes.First(value => value.Id == StatIds.MaxHealth).EffectiveValue);
                foreach (string locale in new[] { "zh-Hans", "en", "qps-ploc" })
                {
                    yield return ChangeLanguage(host.Localization, locale == "zh-Hans" ? UserLanguagePreference.SimplifiedChinese : UserLanguagePreference.English);
                    foreach (GameMenuPage? service in new GameMenuPage?[] { null, GameMenuPage.Shop, GameMenuPage.Crafting })
                    {
                        if (service.HasValue) { Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(FindTarget(service.Value)))); }
                        menu.OpenPage(GameMenuPage.Inventory);
                        menu.OpenPage(GameMenuPage.Attributes);
                        yield return null;
                        yield return null;
                        foreach (Foldout group in scroll.Query<Foldout>().ToList()) { group.value = false; }
                        foreach (Foldout group in scroll.Query<Foldout>().ToList().Where(fold => fold.name.StartsWith("attributes-group-"))) { group.value = true; }
                        scroll.scrollOffset = Vector2.zero;
                        if (locale == "qps-ploc") { ApplyPseudoLocalization(root); }
                        yield return null;
                        yield return null;
                        AssertLayoutInsideRoot(root, new[] { "inventory-window", "attributes-window", "attributes-scroll" });
                        Assert.LessOrEqual(root.Q("inventory-window").worldBound.xMax, root.Q("attributes-window").worldBound.xMin);
                        if (service.HasValue)
                        {
                            VisualElement serviceWindow = root.Q(service == GameMenuPage.Shop ? "shop-window" : "crafting-window");
                            Assert.LessOrEqual(serviceWindow.worldBound.xMax, root.Q("attributes-window").worldBound.xMin);
                        }
                        AssertVisibleTextFits(root, locale, new Vector2Int(1920, 1080));
                        root.Q<Foldout>("attribute-max_health").value = true;
                        yield return null;
                        scroll.ScrollTo(root.Q("attribute-max_health"));
                        yield return null;
                        AssertVisibleTextFits(root, locale, new Vector2Int(1920, 1080));
                        scroll.scrollOffset = new Vector2(0f, scroll.verticalScroller.highValue);
                        yield return null;
                        AssertVisibleTextFits(root, locale, new Vector2Int(1920, 1080));
                        Assert.LessOrEqual(CountItemSelectionHighlights(root), 1);
                        root.Q<Button>("attributes-window-close").Focus();
                        _inputFixture.Press(pad.dpad.left);
                        yield return null;
                        _inputFixture.Release(pad.dpad.left);
                        yield return null;
                        Assert.AreNotEqual(GameMenuPage.Attributes, menu.CurrentPage, "左移必须进入视觉相邻窗口");
                        _inputFixture.Press(pad.dpad.right);
                        yield return null;
                        _inputFixture.Release(pad.dpad.right);
                        yield return null;
                        Assert.AreEqual(GameMenuPage.Attributes, menu.CurrentPage, "右移必须能回到属性窗口");
                        if (service == GameMenuPage.Shop)
                        {
                            Button stock = root.Q("shop-merchant-list").Query<Button>().First();
                            root.Q<ScrollView>("shop-merchant-scroll").ScrollTo(stock);
                            yield return null;
                            stock.Focus();
                            yield return null;
                            yield return null;
                            yield return null;
                            VisualElement tooltip = root.Q("item-tooltip");
                            Assert.AreEqual(Visibility.Visible, tooltip.resolvedStyle.visibility);
                            Assert.GreaterOrEqual(tooltip.worldBound.yMin, root.Q("attributes-window-close").worldBound.yMax);
                            Assert.LessOrEqual(tooltip.worldBound.xMax, root.Q("attributes-window").worldBound.xMax);
                            root.Q<Button>("attributes-window-close").Focus();
                            yield return null;
                            Assert.AreEqual(DisplayStyle.None, tooltip.resolvedStyle.display);
                            Assert.AreEqual(Visibility.Visible, scroll.contentContainer.resolvedStyle.visibility);
                        }
                        if (service.HasValue) { menu.ClosePage(service.Value); }
                    }
                }
                menu.ClosePage(GameMenuPage.Inventory);
                menu.OpenPage(GameMenuPage.Attributes);
                _inputFixture.PressAndRelease(pad.buttonEast);
                yield return null;
                Assert.IsFalse(menu.IsOpen);
            }
            finally
            {
                SetGameViewResolution(new Vector2Int(originalWidth, originalHeight));
                InputSystem.RemoveDevice(pad);
            }
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
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
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
            LootPickupController drop = Object.FindObjectsByType<LootPickupController>()
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
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
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
            menu.ClosePage(GameMenuPage.Inventory);
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
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
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
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
            InventoryModel inventory = architecture.GetModel<InventoryModel>();
            inventory.AddGold(10000);
            WorldInteractionTarget merchant = FindTarget(GameMenuPage.Shop);
            WorldInteractionTarget craftingTarget = FindTarget(GameMenuPage.Crafting);
            Assert.IsTrue(architecture.SendCommand(new OpenGameMenuCommand(merchant)));
            yield return null;
            yield return null;
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Inventory));
            Assert.AreEqual(root.Q("inventory-window").worldBound.width, root.Q("shop-window").worldBound.width);
            menu.ClosePage(GameMenuPage.Inventory);
            yield return null;
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
            Assert.IsTrue(menu.IsWindowVisible(GameMenuPage.Inventory));
            Assert.AreEqual(root.Q("inventory-window").worldBound.width, root.Q("crafting-window").worldBound.width);
            menu.ClosePage(GameMenuPage.Inventory);
            yield return null;
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
            menu.OpenPage(GameMenuPage.Attributes);
            InvokeButton(root.Q<Button>("game-menu-close"));
            yield return null;
            Assert.AreEqual(GameMenuAccess.None, menu.OpenWindows);
            Assert.IsFalse(menu.IsOpen);
            Assert.IsTrue(architecture.GetUtility<GameInput>().IsGameplayEnabled);
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
            RuntimePanelView document = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((menu == null || document == null || document.Root?.panel == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                menu = Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<RuntimePanelView>() : null;
                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未初始化 GameMenuController");
            Assert.IsNotNull(document, "UIRoot 缺少 RuntimePanelView");
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

            for (int i = 0; i < 80; i++)
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
            VisualElement root = document.Root;
            VisualElement merchantList = root.Q<VisualElement>("shop-merchant-list");
            VisualElement merchantFrame = root.Q<VisualElement>("shop-merchant-frame");
            Assert.IsNotNull(merchantFrame, "商店缺少固定视口框架");
            ScrollView merchantScroll = root.Q<ScrollView>("shop-merchant-scroll");
            Assert.IsNotNull(merchantScroll, "商品扩展需要滚动视口");
            Button selectedButton = FindButton(merchantList, "状态测试装备 79");
            Assert.IsNotNull(selectedButton, "未生成可用于滚动验收的商店按钮");
            Assert.IsFalse(GameMenuController.IsInScrollViewport(selectedButton), "首屏不能覆盖整个压力商品池");
            Button above = merchantList.Query<Button>(className: "shop-item").ToList()
                .Where(button => button.worldBound.center.y < selectedButton.worldBound.center.y
                    && button.worldBound.xMin < selectedButton.worldBound.xMax
                    && button.worldBound.xMax > selectedButton.worldBound.xMin)
                .OrderByDescending(button => button.worldBound.center.y).First();
            merchantScroll.ScrollTo(above);
            yield return FocusAfterScheduledRestore(root, above);
            Gamepad navigationPad = InputSystem.AddDevice<Gamepad>();
            yield return null;
            yield return null;
            _inputFixture.Press(navigationPad.dpad.down);
            yield return null;
            _inputFixture.Release(navigationPad.dpad.down);
            yield return null;
            yield return null;
            Assert.AreSame(selectedButton, root.focusController.focusedElement, "向下没有到达视觉同列的末行商品");
            Assert.IsTrue(GameMenuController.IsInScrollViewport(selectedButton), "导航末行商品后没有自动滚动显露");
            ItemInstance purchasedItem = selectedButton.userData as ItemInstance;
            Assert.IsNotNull(purchasedItem, "商店按钮缺少物品实例");
            string purchasedDisplayName = host.Localization.GetString(
                ItemDetailSnapshotFactory.Create(purchasedItem).Name);
            yield return FocusAfterScheduledRestore(root, selectedButton);
            InvokeButton(selectedButton);
            yield return null;
            Assert.AreEqual("phase1_shop_item_79", shop.SelectedItem.InstanceId, "Submit 未固定当前选择");

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
            Assert.IsTrue(GameMenuController.IsInScrollViewport(navigatedButton), "跨窗不能选中屏外商品");
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
                            "shop-merchant-scroll",
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
                        AssertElementsInsideContainer(root, "inventory-window", new[]
                        {
                            "inventory-equip", "inventory-unequip", "inventory-attributes-open", "inventory-attribute-grid",
                        });
                        Assert.LessOrEqual(root.Q("inventory-actions").worldBound.yMax,
                            root.Q("inventory-attribute-grid").worldBound.yMin + 1f,
                            "穿脱操作区不能遮挡属性摘要");
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
                ItemDetailView denseDetailView = new ItemDetailView(tooltipDetail, host.Localization);
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
            RuntimePanelView document = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((menu == null || document == null || document.Root?.panel == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                menu = Object.FindAnyObjectByType<GameMenuController>();
                document = menu != null ? menu.GetComponent<RuntimePanelView>() : null;
                yield return null;
            }

            Assert.IsNotNull(menu, "Main 场景未初始化 GameMenuController");
            Assert.IsNotNull(document, "UIRoot 缺少 RuntimePanelView");
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

            VisualElement root = document.Root;
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
                    new LocalizedMessage("affixes", "item_affix.reinforced.name"),
                    prefixModifiers,
                    0f));
                suffixes.Add(new AffixDetailSnapshot(
                    null,
                    AffixType.Suffix,
                    new LocalizedMessage("affixes", "item_affix.of_power.name"),
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
                    || element.ClassListContains("input-binding-token")
                    || !element.text.Any(char.IsLetter)
                    || !HasVisibleHierarchy(element)
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
                    || !HasVisibleHierarchy(element)
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
                        $"{context}: 换行文本被纵向裁切；元素 {element.worldBound}，容器 {element.parent.worldBound}");
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

        static bool HasVisibleHierarchy(VisualElement element)
        {
            for (VisualElement ancestor = element; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor.resolvedStyle.display == DisplayStyle.None
                    || ancestor.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }
                if (ancestor.ClassListContains("unity-scroll-view__content-viewport")
                    && !ancestor.worldBound.Overlaps(element.worldBound))
                {
                    return false;
                }
            }
            return true;
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
            ScrollView scroll = root.Q<ScrollView>("shop-merchant-scroll");
            Assert.IsNotNull(scroll, "商人背包缺少滚动视口");
            Assert.AreEqual(playerGrid.worldBound.width, merchantGrid.worldBound.width, 1f, "商人背包应与玩家背包使用相同列宽");
            Assert.GreaterOrEqual(merchantGrid.worldBound.height, playerGrid.worldBound.height - 1f, "商人背包不应小于玩家背包");
            Assert.GreaterOrEqual(merchantGrid.worldBound.xMin, frame.worldBound.xMin - 1f, "商人网格超出框架左边界");
            Assert.GreaterOrEqual(scroll.contentViewport.worldBound.yMin, frame.worldBound.yMin - 1f, "商品视口超出框架上边界");
            Assert.LessOrEqual(merchantGrid.worldBound.xMax, frame.worldBound.xMax + 1f, "商人网格超出框架右边界");
            Assert.LessOrEqual(scroll.contentViewport.worldBound.yMax, frame.worldBound.yMax + 1f, "商品视口超出框架下边界");

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
