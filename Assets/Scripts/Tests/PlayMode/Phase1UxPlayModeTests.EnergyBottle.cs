using System.Collections;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public partial class Phase1UxPlayModeTests
    {
        [UnityTest]
        public IEnumerator EnergyBottle_InventoryPointerSwapAndExactReturnKeepOwnership()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            _architecture = menu.GetArchitecture();
            InventoryModel inventory = _architecture.GetModel<InventoryModel>();
            ItemInstance first = CreateBottle("energy_bottle_small", 123);
            ItemInstance second = CreateBottle("energy_bottle_large", 456);
            Assert.That(inventory.TryAddItem(first), Is.True);
            var secondOrigin = new Vector2Int(4, 2);
            Assert.That(inventory.TryAddItemAt(second, secondOrigin), Is.True);
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
            InventoryPanelController panel = menu.GetComponent<InventoryPanelController>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            int casts = 0;
            IUnRegister registration = _architecture.RegisterEvent<PlayerSkillCastEvent>(_ => casts++);
            try
            {
                menu.OpenPage(GameMenuPage.Inventory);
                yield return null; yield return null;
                Assert.That(menu.Board.IsVisible, Is.False);
                Button slot = root.Q<Button>("inventory-bottle-slot");
                Assert.That(GameMenuController.IsNavigable(slot), Is.True);
                yield return DragPointer(mouse, root, FindItemButton(root, first, "inventory-item").worldBound.center, slot.worldBound.center, panel);
                Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.SameAs(first));
                Assert.That(root.focusController.focusedElement, Is.SameAs(slot));
                Assert.That(slot.userData, Is.SameAs(first));
                Assert.That(ItemVisualPresenter.GetSprite(first.BaseDefinition.Icon.AssetGUID), Is.Not.Null);
                Assert.That(root.Q("inventory-bottle-icon").resolvedStyle.backgroundImage.sprite,
                    Is.SameAs(ItemVisualPresenter.GetSprite(first.BaseDefinition.Icon.AssetGUID)));
                Assert.That(slot.Q<Label>(className: "inventory-bottle-title").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "装备后空栏标题不能遮住实际瓶子图标");
                Assert.That(root.Q<Label>("inventory-bottle-energy").text, Does.Contain("123"));
                yield return DragPointer(mouse, root, FindItemButton(root, second, "inventory-item").worldBound.center, slot.worldBound.center, panel);
                Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.SameAs(second));
                Assert.That(inventory.Grid.Placements[first].position, Is.EqualTo(secondOrigin));
                Assert.That(first.SkillBoardState.Energy, Is.EqualTo(123));
                VisualElement grid = root.Q("inventory-grid");
                Vector2 Cell(Vector2Int origin) { return grid.LocalToWorld((Vector2)origin * 52 + new Vector2(24, 24)); }
                yield return DragPointer(mouse, root, slot.worldBound.center, Cell(secondOrigin), panel);
                Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.SameAs(second), "被占位置不可卸下，也不可自动换格");
                var destination = new Vector2Int(7, 3);
                yield return DragPointer(mouse, root, slot.worldBound.center, Cell(destination), panel);
                Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.Null);
                Assert.That(inventory.Grid.Placements[second].position, Is.EqualTo(destination));
                Assert.That(second.SkillBoardState.Energy, Is.EqualTo(456));
                yield return RightClickPointer(mouse, root, FindItemButton(root, first, "inventory-item").worldBound.center);
                Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.SameAs(first));
                yield return RightClickPointer(mouse, root, slot.worldBound.center);
                Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.Null);
                Assert.That(inventory.Grid.Placements.ContainsKey(first), Is.True);
                Assert.That(menu.Board.IsVisible, Is.False, "换瓶不应打开天赋或转移焦点到隐藏控件");
                Assert.That(_architecture.SendCommand(new OpenGameMenuCommand(FindTarget(GameMenuPage.Shop))), Is.True);
                yield return null; yield return null;
                yield return RightClickPointer(mouse, root, FindItemButton(root, first, "inventory-item").worldBound.center);
                Assert.That(inventory.Grid.Placements.ContainsKey(first), Is.False, "商店上下文默认出售，不能被装备动作抢占");
                Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.Null);
                menu.ClosePage(GameMenuPage.Shop);
                menu.ClosePage(GameMenuPage.Inventory);
                yield return new WaitForSeconds(.75f);
                Assert.That(casts, Is.Zero, "物品操作不能触发普攻或留下连放意图");
            }
            finally { registration.UnRegister(); }
        }

        [UnityTest]
        public IEnumerator EnergyBottle_GamepadActionsCarryAndEnergyChangesCancelStaleSource()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            _architecture = menu.GetArchitecture();
            InventoryModel inventory = _architecture.GetModel<InventoryModel>();
            ItemInstance bottle = CreateBottle("energy_bottle_small", 100);
            Assert.That(inventory.TryAddItem(bottle), Is.True);
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
            menu.OpenPage(GameMenuPage.Inventory);
            yield return null; yield return null;
            FindItemButton(root, bottle, "inventory-item").Focus(); yield return null;
            _inputFixture.PressAndRelease(pad.buttonSouth); yield return null; yield return null;
            Assert.That(menu.Workspace.Interactions.IsMenuOpen, Is.True);
            Button equip = root.Q<Button>("item-action-equipbottle");
            Assert.That(equip.enabledInHierarchy, Is.True);
            equip.Focus(); yield return null;
            _inputFixture.PressAndRelease(pad.buttonSouth); yield return null; yield return null;
            Button slot = root.Q<Button>("inventory-bottle-slot");
            Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.SameAs(bottle));
            Assert.That(root.focusController.focusedElement, Is.SameAs(slot));
            ItemInstance previous = bottle;
            bottle = CreateBottle("energy_bottle_large", 100);
            Assert.That(inventory.TryAddItem(bottle), Is.True);
            Vector2Int sourceOrigin = inventory.Grid.Placements[bottle].position;
            FindItemButton(root, bottle, "inventory-item").Focus(); yield return null;
            _inputFixture.PressAndRelease(pad.buttonSouth); yield return null; yield return null;
            root.Q<Button>("item-action-equipbottle").Focus(); yield return null;
            _inputFixture.PressAndRelease(pad.buttonSouth); yield return null; yield return null;
            Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.SameAs(bottle));
            Assert.That(inventory.Grid.Placements[previous].position, Is.EqualTo(sourceOrigin));
            Assert.That(previous.SkillBoardState.Energy, Is.EqualTo(100));
            _inputFixture.PressAndRelease(pad.buttonWest); yield return null; yield return null;
            Assert.That(menu.Workspace.IsDragging, Is.True);
            int collected = bottle.CollectEnergy(7);
            _architecture.SendEvent(new EnergyCollectedEvent("local_player", bottle, default, 7, collected));
            yield return null;
            Assert.That(menu.Workspace.IsDragging, Is.False, "能量变化后旧瓶来源应取消");
            Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.SameAs(bottle));
            Assert.That(root.Q<Label>("inventory-bottle-energy").text, Does.Contain("107"));
            slot.Focus(); yield return null;
            _inputFixture.PressAndRelease(pad.buttonWest); yield return null; yield return null;
            for (int row = 0; row < inventory.Grid.Height
                && root.Q<VisualElement>(className: "inventory-cell--drop-valid") == null; row++)
            {
                _inputFixture.Set(pad.dpad, Vector2.down); yield return null; yield return null;
                _inputFixture.Set(pad.dpad, Vector2.zero); yield return null;
            }
            Assert.That(root.Q<VisualElement>(className: "inventory-cell--drop-valid"), Is.Not.Null,
                "手柄必须能移至可放置的背包格，不能在旧瓶占格上提交");
            _inputFixture.PressAndRelease(pad.buttonWest); yield return null; yield return null;
            Assert.That(_architecture.SendQuery(new GetSkillBoardQuery()).EnergyBottle, Is.Null);
            Assert.That(inventory.Grid.Placements.ContainsKey(bottle), Is.True, "手柄可从专栏放回背包");
            FindItemButton(root, bottle, "inventory-item").Focus(); yield return null;
            _inputFixture.PressAndRelease(pad.buttonWest); yield return null; yield return null;
            Assert.That(menu.Workspace.IsDragging, Is.True);
            InputSystem.RemoveDevice(pad); yield return null; yield return null;
            Assert.That(menu.Workspace.IsDragging, Is.False);
            Assert.That(inventory.Grid.Placements.ContainsKey(bottle), Is.True);
        }

        ItemInstance CreateBottle(string id, int energy)
        {
            ItemBaseDefinition definition = ApplicationHost.Current.ContentCatalog.Resolve<ItemBaseDefinition>(ContentId.Parse("item:" + id)).Value;
            ItemInstance bottle = definition.CreateInstance(_architecture.GetUtility<IItemInstanceIdGenerator>().Next(), 1, 37, ItemRarity.Normal);
            bottle.CollectEnergy(energy);
            return bottle;
        }
    }
}
