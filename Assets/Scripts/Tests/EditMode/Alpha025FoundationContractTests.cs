using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkFlare.Tests
{
    public sealed class Alpha025FoundationContractTests
    {
        const string InputAssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [Test]
        public void RebindableCatalog_FreezesSevenConsumedActionsAndMatchesInputAsset()
        {
            RebindableInputAction[] expected =
            {
                RebindableInputAction.PlayerMove,
                RebindableInputAction.PlayerInteract,
                RebindableInputAction.PlayerToggleMenu,
                RebindableInputAction.UiNavigate,
                RebindableInputAction.UiSubmit,
                RebindableInputAction.UiCancel,
                RebindableInputAction.UiRearrange,
            };
            CollectionAssert.AreEqual(expected, RebindableInputCatalog.Actions);
            Assert.AreEqual(
                RebindableInputCatalog.Actions.Count,
                RebindableInputCatalog.Actions.Distinct().Count());

            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            Assert.IsNotNull(asset, $"缺少正式输入资产 {InputAssetPath}");
            Assert.IsTrue(
                asset.controlSchemes.Any(scheme => scheme.name == "Keyboard&Mouse"));
            Assert.IsTrue(asset.controlSchemes.Any(scheme => scheme.name == "Gamepad"));

            for (int i = 0; i < RebindableInputCatalog.Actions.Count; i++)
            {
                RebindableInputAction actionId = RebindableInputCatalog.Actions[i];
                string mapName = RebindableInputCatalog.GetMapName(actionId);
                string actionName = RebindableInputCatalog.GetActionName(actionId);
                InputAction action = asset.FindAction($"{mapName}/{actionName}", true);

                Assert.IsTrue(
                    HasBindingGroup(action, "Keyboard&Mouse"),
                    $"{mapName}/{actionName} 缺少 Keyboard&Mouse 绑定");
                Assert.IsTrue(
                    HasBindingGroup(action, "Gamepad"),
                    $"{mapName}/{actionName} 缺少 Gamepad 绑定");
            }

            Assert.IsFalse(RebindableInputCatalog.Actions.Any(
                action => RebindableInputCatalog.GetActionName(action) == "Attack"));
            Assert.IsFalse(RebindableInputCatalog.Actions.Any(
                action => RebindableInputCatalog.GetActionName(action) == "Look"));
        }

        [Test]
        public void InputSuspensionReasons_AreIndependentFlags()
        {
            InputSuspensionReason[] reasons = Enum.GetValues(typeof(InputSuspensionReason))
                .Cast<InputSuspensionReason>()
                .Where(reason => reason != InputSuspensionReason.None)
                .ToArray();
            InputSuspensionReason combined = InputSuspensionReason.None;

            for (int i = 0; i < reasons.Length; i++)
            {
                int value = (int)reasons[i];
                Assert.AreNotEqual(0, value);
                Assert.AreEqual(0, value & (value - 1), $"{reasons[i]} 不是独立 bit");
                Assert.IsFalse((combined & reasons[i]) != 0, $"{reasons[i]} 与已有原因重叠");
                combined |= reasons[i];
            }

            Assert.IsTrue((combined & InputSuspensionReason.FocusLost) != 0);
            Assert.IsTrue((combined & InputSuspensionReason.PlatformSuspended) != 0);
            Assert.IsTrue((combined & InputSuspensionReason.Rebinding) != 0);
            Assert.IsTrue((combined & InputSuspensionReason.Shutdown) != 0);
        }

        [Test]
        public void BindingTarget_RequiresDeviceFamilyAndHasValueEquality()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.Unknown));

            InputBindingTarget first = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            InputBindingTarget same = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            InputBindingTarget other = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.Gamepad);

            Assert.AreEqual(first, same);
            Assert.AreEqual(first.GetHashCode(), same.GetHashCode());
            Assert.AreNotEqual(first, other);
            Assert.IsTrue(RebindableInputCatalog.UsesDirectionalParts(
                RebindableInputAction.PlayerMove));
            Assert.IsTrue(RebindableInputCatalog.UsesDirectionalParts(
                RebindableInputAction.UiNavigate));
            Assert.IsFalse(RebindableInputCatalog.UsesDirectionalParts(
                RebindableInputAction.PlayerInteract));
        }

        [Test]
        public void RebindResults_SeparateSuccessConflictAndPersistenceFailure()
        {
            InputBindingTarget target = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            InputBindingTarget occupied = new InputBindingTarget(
                RebindableInputAction.PlayerToggleMenu,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            InputBindingConflict conflict = new InputBindingConflict(
                target,
                occupied,
                "<Keyboard>/tab");

            InputRebindResult success = InputRebindResult.Success(target, "<Keyboard>/e");
            InputRebindResult conflictResult = InputRebindResult.ConflictFailure(conflict);
            InvalidOperationException storageException = new InvalidOperationException("commit");
            InputRebindResult failure = InputRebindResult.Failure(
                InputRebindResultCode.SettingsCommitFailed,
                target,
                storageException);

            Assert.IsTrue(success.Succeeded);
            Assert.AreEqual("<Keyboard>/e", success.ControlPath);
            Assert.IsFalse(conflictResult.Succeeded);
            Assert.AreSame(conflict, conflictResult.Conflict);
            Assert.AreEqual(InputRebindResultCode.Conflict, conflictResult.Code);
            Assert.IsFalse(failure.Succeeded);
            Assert.AreSame(storageException, failure.Exception);
            Assert.Throws<ArgumentOutOfRangeException>(() => InputRebindResult.Failure(
                InputRebindResultCode.Success,
                target));
        }

        [Test]
        public void GlyphToken_DistinguishesSpriteAndReadableFallback()
        {
            InputGlyphToken sprite = new InputGlyphToken(
                InputDeviceFamily.Gamepad,
                "<Gamepad>/buttonSouth",
                "gamepad.button-south",
                "A");
            InputGlyphToken fallback = new InputGlyphToken(
                InputDeviceFamily.KeyboardMouse,
                "<Keyboard>/e",
                string.Empty,
                "E");

            Assert.IsFalse(sprite.UsesTextFallback);
            Assert.IsTrue(fallback.UsesTextFallback);
            Assert.AreEqual("E", fallback.FallbackText);
            StringAssert.DoesNotContain("<Keyboard>", fallback.FallbackText);
        }

        [Test]
        public void AudioContracts_FreezeStableCueCategoriesAndResultSemantics()
        {
            Assert.AreEqual("ui.confirm", AudioCueIds.UiConfirm.Value);
            Assert.AreEqual(AudioCueIds.UiConfirm, new AudioCueId("ui.confirm"));
            Assert.IsFalse(default(AudioCueId).IsValid);
            Assert.Throws<ArgumentException>(() => new AudioCueId(" "));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AudioCategory.Master,
                    AudioCategory.Music,
                    AudioCategory.SoundEffects,
                    AudioCategory.UI,
                },
                Enum.GetValues(typeof(AudioCategory)));

            AudioOperationResult success = AudioOperationResult.Success(AudioCueIds.UiConfirm);
            AudioOperationResult rejected = AudioOperationResult.Failure(
                AudioOperationCode.ConcurrencyRejected,
                AudioCueIds.UiConfirm);

            Assert.IsTrue(success.Succeeded);
            Assert.IsFalse(rejected.Succeeded);
            Assert.AreEqual(AudioOperationCode.ConcurrencyRejected, rejected.Code);
            Assert.Throws<ArgumentOutOfRangeException>(() => AudioOperationResult.Failure(
                AudioOperationCode.Success,
                AudioCueIds.UiConfirm));
        }

        [Test]
        public void MotionProfile_FreezesDefaultAndReducedMotionPolicy()
        {
            MotionProfile normal = MotionProfile.FromReduceMotion(false);
            MotionProfile reduced = MotionProfile.FromReduceMotion(true);

            Assert.IsFalse(normal.ReduceMotion);
            Assert.AreEqual(1f, normal.DurationScale);
            Assert.IsTrue(normal.AllowContinuousMotion);
            Assert.IsTrue(reduced.ReduceMotion);
            Assert.That(reduced.DurationScale, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.IsFalse(reduced.AllowContinuousMotion);
            Assert.Throws<ArgumentOutOfRangeException>(() => new MotionProfile(
                true,
                float.NaN,
                false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MotionProfile(
                true,
                1.1f,
                false));
        }

        [Test]
        public void PlatformContracts_KeepOverlappingReasonsAndNoSessionAsSafeResult()
        {
            PlatformSuspensionReason reasons = PlatformSuspensionReason.FocusLost
                | PlatformSuspensionReason.PlatformSuspended;
            PlatformLifecycleResult noSession = PlatformLifecycleResult.SessionUnavailable(
                PlatformLifecycleState.Active,
                PlatformLifecycleState.Suspended,
                reasons,
                true);
            PlatformLifecycleResult timeout = PlatformLifecycleResult.Failure(
                PlatformLifecycleResultCode.TimedOut,
                PlatformLifecycleState.Active,
                PlatformLifecycleState.Suspended,
                reasons,
                true,
                new TimeoutException());

            Assert.IsTrue((reasons & PlatformSuspensionReason.FocusLost) != 0);
            Assert.IsTrue((reasons & PlatformSuspensionReason.PlatformSuspended) != 0);
            Assert.IsTrue(noSession.Succeeded);
            Assert.IsTrue(noSession.CheckpointRequested);
            Assert.IsFalse(timeout.Succeeded);
            Assert.AreEqual(PlatformLifecycleResultCode.TimedOut, timeout.Code);
            Assert.Throws<ArgumentOutOfRangeException>(() => PlatformLifecycleResult.Failure(
                PlatformLifecycleResultCode.SessionUnavailable,
                PlatformLifecycleState.Active,
                PlatformLifecycleState.Suspended,
                reasons,
                true));
        }

        [Test]
        public void SettingsSchemaOne_AlreadyCarriesAllStageDomainsWithoutMigration()
        {
            UserSettingsSnapshot snapshot = UserSettingsSnapshot.Default;
            UserSettingsDataDto dto = snapshot.ToDto();

            Assert.AreEqual(1, SettingsSchemaVersion.Current.Value);
            Assert.IsNotNull(dto.Audio);
            Assert.IsNotNull(dto.Input);
            Assert.IsNotNull(dto.Accessibility);
            Assert.AreEqual(snapshot.MasterVolume, dto.Audio.MasterVolume);
            Assert.AreEqual(snapshot.BindingOverridesJson, dto.Input.BindingOverridesJson);
            Assert.AreEqual(snapshot.ReduceMotion, dto.Accessibility.ReduceMotion);
            Assert.IsTrue(SettingsDataValidator.ValidateSnapshot(snapshot).Succeeded);
        }

        [Test]
        public void FoundationContracts_DoNotOwnUnityObjects()
        {
            Type[] contractTypes =
            {
                typeof(InputBindingTarget),
                typeof(InputBindingConflict),
                typeof(InputRebindResult),
                typeof(InputGlyphToken),
                typeof(AudioCueId),
                typeof(AudioOperationResult),
                typeof(MotionProfile),
                typeof(PlatformLifecycleResult),
            };

            for (int typeIndex = 0; typeIndex < contractTypes.Length; typeIndex++)
            {
                PropertyInfo[] properties = contractTypes[typeIndex].GetProperties(
                    BindingFlags.Instance | BindingFlags.Public);

                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    Assert.IsFalse(
                        typeof(UnityEngine.Object).IsAssignableFrom(properties[propertyIndex].PropertyType),
                        $"{contractTypes[typeIndex].Name}.{properties[propertyIndex].Name} 不得持有 Unity 对象");
                }
            }
        }

        static bool HasBindingGroup(InputAction action, string group)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                string groups = action.bindings[i].groups;

                if (string.IsNullOrWhiteSpace(groups))
                {
                    continue;
                }

                string[] values = groups.Split(';');

                for (int groupIndex = 0; groupIndex < values.Length; groupIndex++)
                {
                    if (string.Equals(values[groupIndex], group, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
