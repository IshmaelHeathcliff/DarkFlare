using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkFlare.Tests
{
    public class GameInputTests : InputTestFixture
    {
        GameInput _input;

        public override void Setup()
        {
            base.Setup();
            _input = new GameInput();
        }

        public override void TearDown()
        {
            _input?.Dispose();
            _input = null;
            base.TearDown();
        }

        [Test]
        public void Constructor_EnablesGameplayMapOnly()
        {
            Assert.AreEqual(GameInputMode.Gameplay, _input.CurrentMode);
            Assert.IsTrue(_input.IsGameplayEnabled);
            Assert.IsFalse(_input.IsUiEnabled);
            Assert.AreEqual(Vector2.zero, _input.Move);
        }

        [Test]
        public void Move_ReadsKeyboardAndGamepad()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.wKey);

            Assert.AreEqual(Vector2.up, _input.Move);

            Release(keyboard.wKey);
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Vector2 gamepadMovement = new Vector2(0.25f, -0.5f);
            Set(gamepad.leftStick, gamepadMovement);

            Assert.That(_input.Move.x, Is.EqualTo(gamepadMovement.x).Within(0.02f));
            Assert.That(_input.Move.y, Is.EqualTo(gamepadMovement.y).Within(0.02f));
        }

        [Test]
        public void ToggleMenuAndCancel_SwitchActionMaps()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            PressAndRelease(keyboard.tabKey);

            Assert.AreEqual(GameInputMode.UI, _input.CurrentMode);
            Assert.IsFalse(_input.IsGameplayEnabled);
            Assert.IsTrue(_input.IsUiEnabled);

            PressAndRelease(keyboard.escapeKey);

            Assert.AreEqual(GameInputMode.Gameplay, _input.CurrentMode);

            PressAndRelease(gamepad.startButton);

            Assert.AreEqual(GameInputMode.UI, _input.CurrentMode);

            PressAndRelease(gamepad.buttonEast);

            Assert.AreEqual(GameInputMode.Gameplay, _input.CurrentMode);
        }

        [Test]
        public void SwitchMethods_IgnoreRepeatedMode()
        {
            int changeCount = 0;
            _input.ModeChanged += _ => changeCount++;

            _input.SwitchToGameplay();
            _input.SwitchToUi();
            _input.SwitchToUi();
            _input.SwitchToGameplay();

            Assert.AreEqual(2, changeCount);
        }
    }
}
