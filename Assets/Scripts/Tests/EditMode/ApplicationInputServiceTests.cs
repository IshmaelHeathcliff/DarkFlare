using System;
using System.Collections;
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
