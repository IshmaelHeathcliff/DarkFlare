using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class ApplicationInputServiceTests : InputTestFixture
    {
        ApplicationInputService _service;

        public override void Setup()
        {
            base.Setup();
            _service = new ApplicationInputService();
        }

        public override void TearDown()
        {
            _service?.Dispose();
            _service = null;
            base.TearDown();
        }

        [Test]
        public void SessionWindowActions_WorkInUiAndYieldToUpperOwners()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int inventory = 0;
            int pause = 0;
            int cycle = 0;
            _service.ToggleMenuPerformed += () => inventory++;
            _service.PausePerformed += () => pause++;
            _service.CycleWindowPerformed += step => cycle += step;
            _service.SetSessionMenuActive(true);
            PressAndRelease(keyboard.tabKey);
            PressAndRelease(keyboard.pKey);
            PressAndRelease(keyboard.rightBracketKey);
            Assert.AreEqual(1, inventory);
            Assert.AreEqual(1, pause);
            Assert.AreEqual(1, cycle);
            Assert.IsFalse(_service.IsGameplayEnabled);
            Assert.IsFalse(_service.ActionAsset.FindAction("Player/Interact").enabled);
            using (_service.AcquireUiContext("settings"))
            {
                PressAndRelease(keyboard.tabKey);
                PressAndRelease(keyboard.pKey);
                PressAndRelease(keyboard.rightBracketKey);
            }
            Assert.AreEqual(1, inventory);
            Assert.AreEqual(1, pause);
            Assert.AreEqual(1, cycle);
            PressAndRelease(keyboard.tabKey);
            Assert.AreEqual(2, inventory);
            _service.SetSessionMenuActive(false);
            PressAndRelease(keyboard.tabKey);
            Assert.AreEqual(2, inventory);
        }

        [Test]
        public void SevenSkillActions_AreDistinctAndGameplayOnlyForKeyboardAndGamepad()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            int[] counts = new int[7];
            _service.SkillRequested += request => counts[request.Slot + 1]++;
            _service.SwitchContext(InputContext.Gameplay);
            foreach (var key in new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key,
                keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key }) { PressAndRelease(key); }
            PressAndRelease(gamepad.buttonWest);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1, 1, 1 }, counts);
            foreach (var button in new[] { gamepad.buttonEast, gamepad.buttonSouth, gamepad.leftShoulder,
                gamepad.rightShoulder, gamepad.leftTrigger, gamepad.rightTrigger }) { PressAndRelease(button); }
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 2, 2, 2, 2 }, counts);
            _service.SwitchContext(InputContext.UI);
            PressAndRelease(keyboard.digit1Key); PressAndRelease(gamepad.buttonWest);
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 2, 2, 2, 2 }, counts);
        }

        [Test]
        public void HeldSkillAcrossUiOrSuspension_RequiresReleaseAndNewPress()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int casts = 0;
            _service.SkillRequested += _ => casts++;
            _service.SwitchContext(InputContext.Gameplay);
            Press(keyboard.digit1Key);
            Assert.That(casts, Is.EqualTo(1));
            _service.SwitchContext(InputContext.UI);
            _service.SwitchContext(InputContext.Gameplay);
            InputSystem.Update();
            Assert.That(casts, Is.EqualTo(1));
            Release(keyboard.digit1Key);
            PressAndRelease(keyboard.digit1Key);
            Assert.That(casts, Is.EqualTo(2));
            using (_service.AcquireSuspension(InputSuspensionReason.FocusLost)) { Press(keyboard.digit2Key); }
            InputSystem.Update();
            Assert.That(casts, Is.EqualTo(2));
            Release(keyboard.digit2Key); PressAndRelease(keyboard.digit2Key);
            Assert.That(casts, Is.EqualTo(3));
        }

        [Test]
        public void HeldSkillSnapshot_TracksReleaseNewPressAndLatestAim()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            PlayerSkillInput press = default;
            _service.SkillRequested += value => press = value;
            _service.SwitchContext(InputContext.Gameplay);
            Press(pad.buttonWest);
            PlayerSkillInput first = press;
            Set(pad.rightStick, Vector2.up);
            Assert.IsTrue(_service.TryReadSkillInput(first, true, out PlayerSkillInput latest));
            Assert.That(Vector2.Distance(latest.Look, Vector2.up), Is.LessThan(.001f));
            Release(pad.buttonWest);
            Assert.IsFalse(_service.TryReadSkillInput(first, true, out _));
            Assert.IsTrue(_service.TryReadSkillInput(first, false, out _), "短按首发仍可在帧末提交");
            Press(pad.buttonWest);
            Assert.IsFalse(_service.IsSkillPressCurrent(first), "旧按下不能认领新保持状态");
            Assert.IsTrue(_service.TryReadSkillInput(press, true, out _));
            using (_service.AcquireSuspension(InputSuspensionReason.FocusLost)) { }
            Assert.IsFalse(_service.TryReadSkillInput(press, true, out _));
            Release(pad.buttonWest);
            Press(pad.buttonWest);
            Assert.IsTrue(_service.TryReadSkillInput(press, true, out _));
            InputSystem.RemoveDevice(pad);
            Assert.IsFalse(_service.TryReadSkillInput(press, true, out _));
        }

        [Test]
        public void HeldSkillBindings_KeepTwoActionsIndependent()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var presses = new System.Collections.Generic.List<PlayerSkillInput>();
            _service.SkillRequested += presses.Add;
            _service.SwitchContext(InputContext.Gameplay);
            Press(keyboard.digit1Key);
            Press(keyboard.digit6Key);
            Assert.AreEqual(2, presses.Count);
            Assert.IsTrue(_service.TryReadSkillInput(presses[0], true, out _));
            Assert.IsTrue(_service.TryReadSkillInput(presses[1], true, out _));
            Release(keyboard.digit1Key);
            Assert.IsFalse(_service.TryReadSkillInput(presses[0], true, out _));
            Assert.IsTrue(_service.TryReadSkillInput(presses[1], true, out _));
        }

        [Test]
        public void HeldMouseSkill_ReadsCurrentPointerWithoutCreatingMorePresses()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            int requests = 0;
            PlayerSkillInput press = default;
            _service.SkillRequested += value => { press = value; requests++; };
            _service.SwitchContext(InputContext.Gameplay);
            Set(mouse.position, new Vector2(10, 20));
            Press(mouse.leftButton);
            Set(mouse.position, new Vector2(150, 250));
            Assert.IsTrue(_service.TryReadSkillInput(press, true, out PlayerSkillInput latest));
            Assert.AreEqual(new Vector2(150, 250), latest.Pointer);
            Assert.AreEqual(1, requests);
        }

        [Test]
        public void SkillBindings_AcceptMouseButtonsAndRejectConflictsWithNormalAttack()
        {
            InputSystem.AddDevice<Mouse>();
            var target = new InputBindingTarget(RebindableInputAction.PlayerSkill1, InputBindingPart.Primary, InputDeviceFamily.KeyboardMouse);
            InputRebindResult conflict = _service.ApplyBindingOverrideAsync(target, "<Mouse>/leftButton").GetAwaiter().GetResult();
            Assert.That(conflict.Code, Is.EqualTo(InputRebindResultCode.Conflict));
            InputRebindResult applied = _service.ApplyBindingOverrideAsync(target, "<Mouse>/rightButton").GetAwaiter().GetResult();
            Assert.That(applied.Code, Is.EqualTo(InputRebindResultCode.Success));
            Assert.That(_service.GetBindingControlPath(target), Is.EqualTo("<Mouse>/rightButton"));
        }

        [Test]
        public void GamepadAim_PreservesLastDirectionAndNewSessionResetsIt()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            PlayerSkillInput last = default;
            _service.SkillRequested += request => last = request;
            _service.SwitchContext(InputContext.Gameplay);
            Set(pad.rightStick, Vector2.up);
            Set(pad.rightStick, Vector2.zero);
            PressAndRelease(pad.buttonWest);
            Assert.That(Vector2.Distance(last.Look, Vector2.up), Is.LessThan(0.001f));
            Assert.That(last.Family, Is.EqualTo(InputDeviceFamily.Gamepad));
            _service.ResetAim();
            PressAndRelease(pad.buttonWest);
            Assert.That(last.Look, Is.EqualTo(Vector2.right));
        }

        [UnityTest]
        public IEnumerator ExistingBindingOverrides_KeepTheirKeysAndNewSkillDefaultsAvoidCollision()
        {
            return VerifyLegacyBindingsAsync().ToCoroutine();
        }

        async UniTask VerifyLegacyBindingsAsync()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputAction interact = _service.ActionAsset.FindAction("Player/Interact", true);
            int index = interact.bindings.ToList().FindIndex(b => b.path == "<Keyboard>/e");
            interact.ApplyBindingOverride(index, "<Keyboard>/1");
            string historical = _service.ActionAsset.SaveBindingOverridesAsJson();
            _service.Dispose();
            var settings = new SettingsService(new InMemorySettingsStorage());
            Assert.That(settings.Initialize().Succeeded, Is.True);
            SettingsOperationResult updated = await settings.UpdateAsync(settings.Current.WithInput(historical, InputGlyphPreference.Auto));
            Assert.That(updated.Succeeded, Is.True);
            _service = new ApplicationInputService(settings);
            var skill = new InputBindingTarget(RebindableInputAction.PlayerSkill1, InputBindingPart.Primary, InputDeviceFamily.KeyboardMouse);
            var old = new InputBindingTarget(RebindableInputAction.PlayerInteract, InputBindingPart.Primary, InputDeviceFamily.KeyboardMouse);
            Assert.That(_service.GetBindingControlPath(old), Is.EqualTo("<Keyboard>/1"));
            Assert.That(_service.GetBindingControlPath(skill), Is.EqualTo("<Keyboard>/f1"));
            int skillRequests = 0; int interactions = 0;
            _service.SkillRequested += _ => skillRequests++;
            _service.InteractPerformed += () => interactions++;
            _service.SwitchContext(InputContext.Gameplay);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1)); InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Assert.That(interactions, Is.EqualTo(1)); Assert.That(skillRequests, Is.Zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1)); InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Assert.That(skillRequests, Is.EqualTo(1));
            Assert.That(settings.Current.BindingOverridesJson, Is.EqualTo(historical), "不在读入设置时覆盖历史偏好");
        }

        [Test]
        public void Constructor_EnablesUiContextOnly()
        {
            Assert.AreEqual(InputContext.UI, _service.CurrentContext);
            Assert.IsNotNull(_service.ActionAsset);
            Assert.IsFalse(_service.IsGameplayEnabled);
            Assert.IsTrue(_service.IsUiEnabled);
            Assert.IsFalse(_service.IsSuspended);
        }

        [Test]
        public void SwitchContext_ChangesMapsAndPublishesOnce()
        {
            int changeCount = 0;
            _service.ContextChanged += _ => changeCount++;

            _service.SwitchContext(InputContext.Gameplay);
            _service.SwitchContext(InputContext.Gameplay);

            Assert.AreEqual(InputContext.Gameplay, _service.CurrentContext);
            Assert.IsTrue(_service.IsGameplayEnabled);
            Assert.IsFalse(_service.IsUiEnabled);
            Assert.IsTrue(_service.ActionAsset.FindAction("UI/Point").enabled);
            Assert.IsTrue(_service.ActionAsset.FindAction("UI/Click").enabled);
            Assert.IsFalse(_service.ActionAsset.FindAction("UI/Navigate").enabled);
            Assert.IsFalse(_service.ActionAsset.FindAction("UI/Submit").enabled);
            Assert.AreEqual(1, changeCount);
        }

        [Test]
        public void Suspensions_ReferenceCountAndOverlapBeforeRestoringContext()
        {
            _service.SwitchContext(InputContext.Gameplay);
            IDisposable firstFocus = _service.AcquireSuspension(
                InputSuspensionReason.FocusLost);
            IDisposable secondFocus = _service.AcquireSuspension(
                InputSuspensionReason.FocusLost);
            IDisposable platform = _service.AcquireSuspension(
                InputSuspensionReason.PlatformSuspended);

            Assert.AreEqual(
                InputSuspensionReason.FocusLost | InputSuspensionReason.PlatformSuspended,
                _service.SuspensionReasons);
            Assert.IsFalse(_service.IsGameplayEnabled);
            Assert.IsFalse(_service.IsUiEnabled);

            Assert.IsFalse(_service.ActionAsset.FindAction("UI/Point").enabled);
            Assert.IsFalse(_service.ActionAsset.FindAction("UI/Click").enabled);

            firstFocus.Dispose();
            platform.Dispose();

            Assert.AreEqual(InputSuspensionReason.FocusLost, _service.SuspensionReasons);
            Assert.IsFalse(_service.IsGameplayEnabled);

            secondFocus.Dispose();

            Assert.AreEqual(InputSuspensionReason.None, _service.SuspensionReasons);
            Assert.IsTrue(_service.IsGameplayEnabled);
            Assert.IsFalse(_service.IsUiEnabled);
        }

        [Test]
        public void UiContextOwners_PreserveLatestRequestAndRespectSuspension()
        {
            _service.SwitchContext(InputContext.Gameplay);
            IDisposable settings = _service.AcquireUiContext("settings");
            IDisposable modal = _service.AcquireUiContext("modal");
            _service.SwitchContext(InputContext.UI);
            _service.SwitchContext(InputContext.Gameplay);
            settings.Dispose();
            settings.Dispose();
            Assert.AreEqual(InputContext.Gameplay, _service.RequestedContext);
            Assert.AreEqual(InputContext.UI, _service.CurrentContext);
            Assert.IsTrue(_service.IsUiEnabled);
            Assert.IsFalse(_service.IsGameplayEnabled);

            using (IDisposable suspension = _service.AcquireSuspension(InputSuspensionReason.FocusLost))
            {
                modal.Dispose();
                Assert.IsFalse(_service.IsUiEnabled);
                Assert.IsFalse(_service.IsGameplayEnabled);
            }

            Assert.IsTrue(_service.IsGameplayEnabled);
            Assert.IsFalse(_service.HasUiContextOverride);
            IDisposable stale = _service.AcquireUiContext("ending-session");
            _service.Dispose();
            Assert.DoesNotThrow(() => stale.Dispose());
        }

        [Test]
        public void Cancel_StopsAfterFirstOwnerConsumesInput()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int shellCount = 0;
            int lowerCount = 0;
            _service.CancelRequested += () =>
            {
                shellCount++;
                return true;
            };
            _service.CancelRequested += () =>
            {
                lowerCount++;
                return true;
            };
            _service.CancelPerformed += () => lowerCount++;
            PressAndRelease(keyboard.escapeKey);
            Assert.AreEqual(1, shellCount);
            Assert.AreEqual(0, lowerCount);
        }

        [Test]
        public void SuspensionLease_ReleaseIsIdempotent()
        {
            IDisposable lease = _service.AcquireSuspension(
                InputSuspensionReason.SceneTransition);

            lease.Dispose();
            lease.Dispose();

            Assert.AreEqual(InputSuspensionReason.None, _service.SuspensionReasons);
            Assert.IsTrue(_service.IsUiEnabled);
        }

        [Test]
        public void AcquireSuspension_RejectsNoneAndCombinedReasons()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _service.AcquireSuspension(InputSuspensionReason.None));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _service.AcquireSuspension(
                    InputSuspensionReason.FocusLost
                    | InputSuspensionReason.PlatformSuspended));
        }

        [Test]
        public void Dispose_DisablesActionsAndLeavesOldLeaseSafe()
        {
            IDisposable lease = _service.AcquireSuspension(
                InputSuspensionReason.Shutdown);
            InputActionAsset actionAsset = _service.ActionAsset;
            int closingCount = 0;
            _service.Closing += () =>
            {
                closingCount++;
                Assert.IsFalse(_service.IsClosed);
                Assert.AreSame(actionAsset, _service.ActionAsset);
            };

            _service.Dispose();
            _service.Dispose();
            lease.Dispose();

            Assert.AreEqual(1, closingCount);
            Assert.IsTrue(_service.IsClosed);
            Assert.IsNull(_service.ActionAsset);
            Assert.IsFalse(_service.IsGameplayEnabled);
            Assert.IsFalse(_service.IsUiEnabled);
            Assert.AreEqual(Vector2.zero, _service.Move);
            Assert.Throws<ObjectDisposedException>(
                () => _service.SwitchContext(InputContext.Gameplay));
        }

        [Test]
        public void ArchitectureProvider_RejectsMissingInputOwner()
        {
            LifecycleScope ownerScope = LifecycleScope.CreateRoot(
                "ApplicationInputServiceTests-MissingOwner");

            try
            {
                Assert.Throws<ArgumentNullException>(
                    () => GameArchitectureProvider.StartSession(ownerScope, null));
                Assert.IsFalse(GameArchitectureProvider.HasCurrent);
            }
            finally
            {
                ownerScope.StopAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void SettingsSnapshot_WithDomainValuesPreservesOtherDomains()
        {
            UserSettingsSnapshot source = UserSettingsSnapshot.Default;

            UserSettingsSnapshot input = source.WithInput(
                "{\"overrides\":[]}",
                InputGlyphPreference.Gamepad);
            UserSettingsSnapshot audio = input.WithAudio(0.8f, 0.7f, 0.6f, 0.5f, true);
            UserSettingsSnapshot accessibility = audio.WithReduceMotion(true);

            Assert.AreEqual("{\"overrides\":[]}", accessibility.BindingOverridesJson);
            Assert.AreEqual(InputGlyphPreference.Gamepad, accessibility.GlyphPreference);
            Assert.AreEqual(0.8f, accessibility.MasterVolume);
            Assert.AreEqual(0.7f, accessibility.MusicVolume);
            Assert.AreEqual(0.6f, accessibility.SoundEffectsVolume);
            Assert.AreEqual(0.5f, accessibility.UiVolume);
            Assert.IsTrue(accessibility.Muted);
            Assert.IsTrue(accessibility.ReduceMotion);
            Assert.AreEqual(source.Language, accessibility.Language);
            Assert.AreEqual(source.DisplayWidth, accessibility.DisplayWidth);
        }

        [Test]
        public void DeviceActivity_ChangesAutoGlyphFamilyAndExplicitPreferenceOverridesDisplay()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            int changeCount = 0;
            _service.DeviceFamilyChanged += _ => changeCount++;

            Press(keyboard.enterKey);
            Assert.AreEqual(InputDeviceFamily.KeyboardMouse, _service.ActiveDeviceFamily);
            Assert.AreEqual(InputDeviceFamily.KeyboardMouse, _service.DisplayDeviceFamily);

            Press(gamepad.buttonSouth);
            Assert.AreEqual(InputDeviceFamily.Gamepad, _service.ActiveDeviceFamily);
            Assert.AreEqual(InputDeviceFamily.Gamepad, _service.DisplayDeviceFamily);

            _service.SetGlyphPreferenceForTests(InputGlyphPreference.KeyboardMouse);

            Assert.AreEqual(InputDeviceFamily.Gamepad, _service.ActiveDeviceFamily);
            Assert.AreEqual(InputDeviceFamily.KeyboardMouse, _service.DisplayDeviceFamily);
            Assert.AreEqual(2, changeCount);
            Release(keyboard.enterKey);
            Release(gamepad.buttonSouth);
            _service.SetGlyphPreferenceForTests(InputGlyphPreference.Auto);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(300f, 200f));
            Assert.AreEqual(InputDeviceFamily.KeyboardMouse, _service.DisplayDeviceFamily);
            PressAndRelease(gamepad.buttonEast);
            _service.SwitchContext(InputContext.Gameplay);
            InputSystem.Update();
            Assert.AreEqual(InputDeviceFamily.Gamepad, _service.DisplayDeviceFamily,
                "Context 切换回放未移动的鼠标位置不能覆盖手柄提示");
            Set(mouse.position, new Vector2(320f, 200f));
            Assert.AreEqual(InputDeviceFamily.KeyboardMouse, _service.DisplayDeviceFamily,
                "真实鼠标移动仍应更新提示设备");
        }

        [UnityTest]
        public IEnumerator ApplyBindingOverride_PersistsAndConflictLeavesStableState()
        {
            return VerifyBindingTransactionsAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator InteractiveRebind_EscapeCancelsAndRestoresUiContext()
        {
            return VerifyInteractiveEscapeCancellationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator InteractiveRebind_DeviceRemovalRollsBackWithoutOverride()
        {
            return VerifyInteractiveDeviceRemovalAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator InteractiveRebind_TimeoutRollsBackAndReleasesSuspension()
        {
            return VerifyInteractiveTimeoutAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator SettingsCommitFailure_RollsBackRuntimeOverride()
        {
            return VerifyBindingRollbackAsync().ToCoroutine();
        }

        [Test]
        public void GlyphResolver_KnownAndUnknownControlsNeverExposeRawPath()
        {
            InputGlyphResolver resolver = new InputGlyphResolver();

            InputGlyphToken known = resolver.Resolve(
                "<Gamepad>/buttonSouth",
                InputDeviceFamily.Gamepad,
                "A");
            InputGlyphToken unknown = resolver.Resolve(
                "<Gamepad>/vendorSpecificControl",
                InputDeviceFamily.Gamepad,
                "Special");

            Assert.AreEqual("gamepad.button_south", known.GlyphId);
            Assert.AreEqual("A", known.FallbackText);
            Assert.IsTrue(unknown.UsesTextFallback);
            Assert.AreEqual("Special", unknown.FallbackText);
            StringAssert.DoesNotContain("<Gamepad>", unknown.FallbackText);
        }

        async UniTask VerifyBindingTransactionsAsync()
        {
            _service.Dispose();
            InMemorySettingsStorage storage = new InMemorySettingsStorage();
            SettingsService settings = new SettingsService(storage);
            Assert.IsTrue(settings.Initialize().Succeeded);
            _service = new ApplicationInputService(settings);
            InputBindingTarget interact = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            InputBindingTarget submit = new InputBindingTarget(
                RebindableInputAction.UiSubmit,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);

            InputRebindResult applied = await _service.ApplyBindingOverrideAsync(
                interact,
                "<Keyboard>/f");
            string committedJson = settings.Current.BindingOverridesJson;
            InputRebindResult conflict = await _service.ApplyBindingOverrideAsync(
                interact,
                "<Keyboard>/tab");
            InputRebindResult crossMap = await _service.ApplyBindingOverrideAsync(
                submit,
                "<Keyboard>/f");

            Assert.IsTrue(applied.Succeeded);
            Assert.AreEqual("<Keyboard>/f", _service.GetBindingControlPath(interact));
            Assert.IsFalse(string.IsNullOrWhiteSpace(committedJson));
            Assert.AreEqual(InputRebindResultCode.Conflict, conflict.Code);
            Assert.AreEqual(RebindableInputAction.PlayerToggleMenu, conflict.Conflict.ConflictingTarget.Action);
            Assert.IsTrue(crossMap.Succeeded);
            Assert.AreEqual("<Keyboard>/f", _service.GetBindingControlPath(submit));
            InputRebindResult windowConflict = await _service.ApplyBindingOverrideAsync(submit, "<Keyboard>/tab");
            Assert.AreEqual(InputRebindResultCode.Conflict, windowConflict.Code,
                "背包开关与 UI 同时启用后必须检测跨 Map 冲突");
            Assert.AreEqual("<Keyboard>/f", _service.GetBindingControlPath(submit));
        }

        async UniTask VerifyBindingRollbackAsync()
        {
            _service.Dispose();
            InMemorySettingsStorage storage = new InMemorySettingsStorage();
            SettingsService settings = new SettingsService(storage);
            Assert.IsTrue(settings.Initialize().Succeeded);
            _service = new ApplicationInputService(settings);
            InputBindingTarget target = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            string originalPath = _service.GetBindingControlPath(target);
            storage.FailCommit = true;

            InputRebindResult result = await _service.ApplyBindingOverrideAsync(
                target,
                "<Keyboard>/f");

            Assert.AreEqual(InputRebindResultCode.SettingsCommitFailed, result.Code);
            Assert.AreEqual(originalPath, _service.GetBindingControlPath(target));
            Assert.IsTrue(string.IsNullOrWhiteSpace(settings.Current.BindingOverridesJson));
        }

        async UniTask VerifyInteractiveEscapeCancellationAsync()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputBindingTarget target = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            string original = _service.GetBindingControlPath(target);
            UniTask<InputRebindResult> pending = _service.StartInteractiveRebindAsync(
                target,
                2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            InputSystem.Update();

            InputRebindResult result = await pending;

            Assert.AreEqual(InputRebindResultCode.Cancelled, result.Code);
            Assert.AreEqual(original, _service.GetBindingControlPath(target));
            Assert.IsFalse(_service.IsSuspended);
            Assert.IsTrue(_service.IsUiEnabled);
        }

        async UniTask VerifyInteractiveDeviceRemovalAsync()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            InputBindingTarget target = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.Gamepad);
            string original = _service.GetBindingControlPath(target);
            UniTask<InputRebindResult> pending = _service.StartInteractiveRebindAsync(
                target,
                2f);
            InputSystem.RemoveDevice(gamepad);

            InputRebindResult result = await pending;

            Assert.AreEqual(InputRebindResultCode.DeviceUnavailable, result.Code);
            Assert.AreEqual(original, _service.GetBindingControlPath(target));
            Assert.IsFalse(_service.IsSuspended);
            Assert.IsTrue(_service.IsUiEnabled);
        }

        async UniTask VerifyInteractiveTimeoutAsync()
        {
            InputSystem.AddDevice<Keyboard>();
            InputBindingTarget target = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            string original = _service.GetBindingControlPath(target);
            UniTask<InputRebindResult> pending = _service.StartInteractiveRebindAsync(
                target,
                0.03f);

            while (pending.Status == UniTaskStatus.Pending)
            {
                await UniTask.Delay(10, DelayType.Realtime);
                InputSystem.Update();
            }

            InputRebindResult result = await pending;

            Assert.AreEqual(InputRebindResultCode.TimedOut, result.Code);
            Assert.AreEqual(original, _service.GetBindingControlPath(target));
            Assert.IsFalse(_service.IsSuspended);
            Assert.IsTrue(_service.IsUiEnabled);
        }

        sealed class InMemorySettingsStorage : ILocalSettingsStorage
        {
            UserSettingsDocumentDto _document;

            public bool FailCommit { get; set; }

            public LocalSettingsCommitResult Commit(
                UserSettingsDocumentDto document,
                CancellationToken cancellationToken = default)
            {
                if (FailCommit)
                {
                    return new LocalSettingsCommitResult(
                        LocalSettingsStorageCode.IoFailure,
                        null,
                        SettingsSerializationCode.Success,
                        new InvalidOperationException("injected settings failure"));
                }

                _document = document;
                return new LocalSettingsCommitResult(
                    LocalSettingsStorageCode.Success,
                    document,
                    SettingsSerializationCode.Success,
                    null);
            }

            public LocalSettingsLoadResult Load(CancellationToken cancellationToken = default)
            {
                return _document == null
                    ? new LocalSettingsLoadResult(
                        LocalSettingsStorageCode.NotFound,
                        null,
                        SettingsRecoverySource.DefaultMissing,
                        SettingsSerializationCode.Success,
                        null)
                    : new LocalSettingsLoadResult(
                        LocalSettingsStorageCode.Success,
                        _document,
                        SettingsRecoverySource.Current,
                        SettingsSerializationCode.Success,
                        null);
            }
        }
    }
}
