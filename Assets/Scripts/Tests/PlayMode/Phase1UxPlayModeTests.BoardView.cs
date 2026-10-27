using System.Collections;
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
        public IEnumerator Board_MouseHitsAndAttributeConfirmationsKeepTheirIdentity()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            _architecture = menu.GetArchitecture();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            _architecture.GetSystem<PlayerGrowthSystem>().Restore(player.Id.Value, new PlayerGrowthState().Charge(1000), player.Actor);
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            menu.OpenPage(GameMenuPage.SkillBoard);
            yield return null; yield return null;
            SkillBoardViewport canvas = menu.Board.Canvas;
            foreach (HexCoordinate origin in new[] { new HexCoordinate(-30, 19), new HexCoordinate(23, -41) })
            {
                menu.Board.Jump(origin); canvas.Zoom(1.3f);
                yield return null;
                HexCoordinate neighbor = origin.Add(SkillBoardGeometry.NeighborDirections[2]);
                yield return BoardClick(mouse, root, canvas.LocalToWorld(canvas.CellPosition(neighbor)));
                Assert.That(canvas.Cursor, Is.EqualTo(neighbor), "平移 / 缩放后仍命中同一逻辑格");
                yield return BoardKeyBack(keyboard);
            }
            for (int i = 0; i < 3; i++)
            {
                menu.Board.Jump(default); canvas.Zoom(1.15f);
                yield return null;
                HexCoordinate cell = SkillBoardProjection.AttributeCell(i);
                string stat = SkillBoardProjection.AttributeAt(cell);
                yield return BoardClick(mouse, root, canvas.LocalToWorld(canvas.CellPosition(cell)));
                Assert.That(canvas.Cursor, Is.EqualTo(cell));
                Button allocate = root.Q<Button>("board-allocate-" + stat);
                Assert.That(GameMenuController.IsNavigable(allocate), Is.True, stat);
                yield return BoardWaitForFocus(root, allocate.name);
                Assert.That(root.focusController.focusedElement, Is.SameAs(allocate));
                Assert.That(menu.Board.Preview, Is.Null, "点击只进入操作，不直接提交");
                yield return BoardPress(root, allocate.name, keyboard);
                yield return BoardPress(root, "board-confirm", keyboard);
                PlayerGrowthState growth = _architecture.SendQuery(new GetPlayerGrowthQuery());
                Assert.That(growth.Strength, Is.EqualTo(1));
                Assert.That(growth.Dexterity, Is.EqualTo(i >= 1 ? 1 : 0));
                Assert.That(growth.Intelligence, Is.EqualTo(i >= 2 ? 1 : 0));
                yield return BoardKeyBack(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator Board_ViewToolsKeepGamepadFocusNavigationAndLayeredReturn()
        {
            yield return _fixture.EnterMain();
            GameMenuController menu = Object.FindAnyObjectByType<GameMenuController>();
            VisualElement root = menu.GetComponent<RuntimePanelView>().Root;
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            menu.OpenPage(GameMenuPage.Attributes);
            menu.OpenPage(GameMenuPage.SkillBoard);
            yield return null; yield return null;
            Assert.That(root.focusController.focusedElement, Is.SameAs(menu.Board.Canvas));
            menu.Board.Canvas.Zoom(4);
            HexCoordinate panStart = menu.Board.Canvas.Cursor;
            for (int i = 0; i < 12; i++) { yield return BoardPadDirection(pad, Vector2.right); }
            Assert.That(menu.Board.Canvas.Cursor.DistanceTo(panStart), Is.EqualTo(12));
            Assert.That(menu.Board.Canvas.contentRect.Contains(menu.Board.Canvas.CellPosition(menu.Board.Canvas.Cursor)), Is.True,
                "手柄游标离开边缘时必须自动平移，不能丢失当前格");
            menu.Board.Jump(new HexCoordinate(7, -7));
            yield return BoardPadSubmit(pad);
            yield return BoardWaitForFocus(root, "board-tools");
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q("board-tools")));
            yield return BoardPadSubmit(pad);
            yield return BoardWaitForFocus(root, "board-center");
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q("board-center")));
            HexCoordinate cursor = menu.Board.Canvas.Cursor;
            yield return BoardPadDirection(pad, Vector2.right);
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q("board-fit")));
            Assert.That(menu.Board.Canvas.Cursor, Is.EqualTo(cursor), "工具导航不能移动棋盘游标");
            yield return BoardPadSubmit(pad);
            Assert.That(root.focusController.focusedElement, Is.SameAs(menu.Board.Canvas));
            float before = Vector2.Distance(menu.Board.Canvas.CellPosition(default), menu.Board.Canvas.CellPosition(new HexCoordinate(1, 0)));
            yield return BoardPress(root, "board-tools", keyboard);
            yield return BoardPadDirection(pad, Vector2.down);
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q("board-zoom-in")));
            yield return BoardPadSubmit(pad);
            float after = Vector2.Distance(menu.Board.Canvas.CellPosition(default), menu.Board.Canvas.CellPosition(new HexCoordinate(1, 0)));
            Assert.That(after, Is.GreaterThan(before));
            yield return BoardPress(root, "board-tools", keyboard);
            for (int i = 0; i < 4; i++) { yield return BoardPadDirection(pad, Vector2.down); }
            yield return BoardPadDirection(pad, Vector2.right);
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q("board-root-5")));
            yield return BoardPadSubmit(pad);
            Assert.That(menu.Board.Canvas.Cursor, Is.EqualTo(SkillBoardGeometry.Roots[5]));
            yield return BoardPress(root, "board-tools", keyboard);
            _inputFixture.PressAndRelease(pad.buttonEast); yield return null; yield return null;
            Assert.That(GameMenuController.IsNavigable(root.Q("board-center")), Is.False);
            Assert.That(menu.Board.IsVisible, Is.True);
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q("board-tools")));
            yield return BoardPress(root, "board-tools", keyboard);
            _inputFixture.PressAndRelease(pad.rightShoulder); yield return null; yield return null;
            Assert.That(menu.Workspace.ActiveWindow, Is.EqualTo(GameMenuPage.Inventory));
            Assert.That(GameMenuController.IsNavigable(root.Q("board-center")), Is.False, "切换到背包必须释放工具输入所有权");
            _inputFixture.PressAndRelease(pad.leftShoulder); yield return null; yield return null;
            menu.Board.Canvas.Focus();
            _inputFixture.PressAndRelease(pad.buttonEast); yield return null; yield return null;
            Assert.That(root.focusController.focusedElement, Is.SameAs(menu.Board.Canvas));
            Assert.That(menu.Board.IsVisible, Is.True);
            _inputFixture.PressAndRelease(pad.buttonEast); yield return null; yield return null;
            Assert.That(menu.Board.IsVisible, Is.False);
            Assert.That(menu.OpenWindows, Is.EqualTo(GameMenuAccess.Attributes), "恢复进入前窗口组合");
        }

        IEnumerator BoardClick(Mouse mouse, VisualElement root, Vector2 position)
        {
            _inputFixture.Set(mouse.position, PanelToScreen(root, position)); yield return null;
            _inputFixture.Press(mouse.leftButton); yield return null;
            _inputFixture.Release(mouse.leftButton); yield return null; yield return null;
        }

        IEnumerator BoardKeyBack(Keyboard keyboard)
        {
            _inputFixture.PressAndRelease(keyboard.escapeKey); yield return null; yield return null;
        }

        IEnumerator BoardPadSubmit(Gamepad pad)
        {
            _inputFixture.PressAndRelease(pad.buttonSouth); yield return null; yield return null;
        }

        IEnumerator BoardPadDirection(Gamepad pad, Vector2 direction)
        {
            _inputFixture.Set(pad.dpad, direction); yield return null; yield return null;
            _inputFixture.Set(pad.dpad, Vector2.zero); yield return null;
            yield return new WaitForSecondsRealtime(0.04f);
        }

        static IEnumerator BoardWaitForFocus(VisualElement root, string name)
        {
            float deadline = Time.realtimeSinceStartup + 2;
            while (root.focusController.focusedElement != root.Q(name) && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q(name)), name + " 布局完成后应接收焦点");
        }
    }
}
