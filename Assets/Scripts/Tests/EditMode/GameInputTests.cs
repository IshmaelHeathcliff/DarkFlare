using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkFlare.Tests
{
    public class GameInputTests : InputTestFixture
    {
        ApplicationInputService _service;
        GameInput _input;

        public override void Setup()
        {
            base.Setup();
            _service = new ApplicationInputService();
            _input = new GameInput(_service);
        }

        public override void TearDown()
        {
            _input?.Dispose();
            _input = null;
            _service?.Dispose();
            _service = null;
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
        public void Interact_UsesKeyboardAndGamepad_OnlyInGameplayMode()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            int interactCount = 0;
            _input.InteractPerformed += () => interactCount++;

            PressAndRelease(keyboard.eKey);
            PressAndRelease(gamepad.buttonNorth);

            Assert.AreEqual(2, interactCount);

            _input.SwitchToUi();
            PressAndRelease(keyboard.eKey);
            PressAndRelease(gamepad.buttonNorth);

            Assert.AreEqual(2, interactCount);
        }

        [Test]
        public void InteractBindingDisplayString_UsesOnlyCurrentDisplayFamily()
        {
            string displayName = _input.GetInteractBindingDisplayString();

            Assert.IsFalse(string.IsNullOrWhiteSpace(displayName));
            StringAssert.DoesNotContain(" / ", displayName);
            StringAssert.DoesNotContain("<Keyboard>", displayName);
            StringAssert.DoesNotContain("<Gamepad>", displayName);
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

        [Test]
        public void UiRearrangeNavigateAndConsumedCancel_AreForwarded()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int rearrangeCount = 0;
            int navigateCount = 0;
            int cancelCount = 0;
            _input.RearrangePerformed += () => rearrangeCount++;
            _input.NavigatePerformed += _ => navigateCount++;
            _input.CancelRequested += () =>
            {
                cancelCount++;
                return true;
            };
            _input.SwitchToUi();

            PressAndRelease(keyboard.spaceKey);
            PressAndRelease(keyboard.rightArrowKey);
            PressAndRelease(keyboard.escapeKey);

            Assert.AreEqual(1, rearrangeCount);
            Assert.GreaterOrEqual(navigateCount, 1);
            Assert.AreEqual(1, cancelCount);
            Assert.AreEqual(GameInputMode.UI, _input.CurrentMode);
        }

        [Test]
        public void Dispose_DoesNotCloseOwnerAndRestoresUiContext()
        {
            _input.Dispose();

            Assert.IsFalse(_service.IsClosed);
            Assert.AreEqual(InputContext.UI, _service.CurrentContext);
            Assert.IsFalse(_service.IsGameplayEnabled);
            Assert.IsTrue(_service.IsUiEnabled);
            Assert.IsNotNull(_service.ActionAsset);
        }
    }
}
