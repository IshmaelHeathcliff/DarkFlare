using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class Alpha027IntegratedAcceptancePlayModeTests
    {
        const float TimeoutSeconds = 45f;

        readonly SceneFlowPlayModeFixture _sceneFlow = new SceneFlowPlayModeFixture();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _sceneFlow.EnterFrontEnd();
            ApplicationHost host = ApplicationHost.Current;
            yield return WaitUntil(
                () => !host.SaveCoordinator.IsBusy,
                "SetUp 等待 SaveCoordinator 空闲");
            SaveOperationResult deleted = null;
            yield return host.DeleteAutoSaveAsync()
                .ToCoroutine(result => deleted = result);
            Assert.IsTrue(deleted.Succeeded, deleted.Exception?.ToString());
            SettingsOperationResult reset = null;
            yield return host.Settings.ResetToDefaultsAsync()
                .ToCoroutine(result => reset = result);
            Assert.IsTrue(reset.Succeeded, reset.Exception?.ToString());
            yield return WaitUntil(() => !host.Settings.IsBusy, "SetUp 等待 Settings 空闲");
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator FullPlayerPath_DeleteAndResetPersistAcrossHostRestart()
        {
            ApplicationHost host = ApplicationHost.Current;
            string defaultLocale = host.Localization.CurrentLocaleCode;
            InputBindingTarget bindingTarget = new InputBindingTarget(
                RebindableInputAction.PlayerInteract,
                InputBindingPart.Primary,
                InputDeviceFamily.KeyboardMouse);
            string defaultBinding = host.Input.GetBindingControlPath(bindingTarget);
            LocalizationOperationResult language = null;
            yield return host.Localization.ChangeLanguageAsync(
                    UserLanguagePreference.English)
                .ToCoroutine(result => language = result);
            Assert.IsTrue(
                language.Succeeded,
                $"language={language.Code} / {language.Exception}");
            InputRebindResult rebound = null;
            yield return host.Input.ApplyBindingOverrideAsync(
                    bindingTarget,
                    "<Keyboard>/f10")
                .ToCoroutine(result => rebound = result);
            Assert.IsTrue(
                rebound.Succeeded,
                $"rebind={rebound.Code} / {rebound.Exception}");
            SettingsOperationResult customized = null;
            UserSettingsSnapshot candidate = host.Settings.Current
                .WithAudio(0.25f, 0.35f, 0.45f, 0.55f, true)
                .WithReduceMotion(true);
            yield return host.Settings.UpdateAsync(candidate)
                .ToCoroutine(result => customized = result);
            Assert.IsTrue(
                customized.Succeeded,
                $"settings={customized.Code} / {customized.Exception}");

            SceneFlowResult start = null;
            yield return host.SceneFlow.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.NewGame))
                .ToCoroutine(result => start = result);
            Assert.IsTrue(start.Succeeded, $"start={start.ErrorCode} / {start.Exception}");
            InventoryModel inventory = host.CurrentSession.Architecture
                .GetModel<InventoryModel>();
            inventory.AddGold(77);
            int expectedGold = inventory.Gold;
            SaveOperationResult firstSave = null;
            yield return host.SessionSaveFacade.SaveAutoAsync()
                .ToCoroutine(result => firstSave = result);
            Assert.IsTrue(
                firstSave.Succeeded,
                $"first-save={firstSave.ErrorCode} / {firstSave.Exception}");
            SceneFlowResult firstReturn = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ReturnToFrontEnd())
                .ToCoroutine(result => firstReturn = result);
            Assert.IsTrue(
                firstReturn.Succeeded,
                $"first-return={firstReturn.ErrorCode} / {firstReturn.Exception}");
            yield return WaitUntil(
                () => !host.SaveCoordinator.IsBusy,
                "首次返回后等待 SaveCoordinator 空闲");

            SceneFlowResult continued = null;
            yield return host.SceneFlow.RequestAsync(
                    SceneFlowRequest.StartGame(GameStartIntent.Continue))
                .ToCoroutine(result => continued = result);
            Assert.IsTrue(
                continued.Succeeded,
                $"continue={continued.ErrorCode} / {continued.Exception}");
            Assert.AreEqual(
                expectedGold,
                host.CurrentSession.Architecture.GetModel<InventoryModel>().Gold);
            SaveOperationResult secondSave = null;
            yield return host.SessionSaveFacade.SaveAutoAsync()
                .ToCoroutine(result => secondSave = result);
            Assert.IsTrue(
                secondSave.Succeeded,
                $"second-save={secondSave.ErrorCode} / {secondSave.Exception}");
            Assert.Greater(secondSave.CommitSequence, firstSave.CommitSequence);
            SceneFlowResult secondReturn = null;
            yield return host.SceneFlow.RequestAsync(SceneFlowRequest.ReturnToFrontEnd())
                .ToCoroutine(result => secondReturn = result);
            Assert.IsTrue(
                secondReturn.Succeeded,
                $"second-return={secondReturn.ErrorCode} / {secondReturn.Exception}");
            yield return WaitUntil(
                () => !host.SaveCoordinator.IsBusy,
                "第二次返回后等待 SaveCoordinator 空闲");

            Button deleteSave = FindButton("front-end-delete-save");
            Button modalConfirm = FindButton("application-modal-retry");
            VisualElement modal = FindElement("application-modal");
            InvokeButton(deleteSave);
            Assert.AreEqual(DisplayStyle.Flex, modal.resolvedStyle.display);
            SceneFlowContinuePreparation beforeConfirmation = null;
            yield return host.PrepareContinueAsync(default)
                .ToCoroutine(result => beforeConfirmation = result);
            Assert.IsTrue(beforeConfirmation.Succeeded, "确认前存档不应被删除");
            InvokeButton(modalConfirm);
            yield return WaitUntil(
                () => !host.SaveCoordinator.IsBusy,
                "删除后等待 SaveCoordinator 空闲");
            yield return WaitUntil(
                () => !FindButton("front-end-continue").enabledSelf,
                "删除后等待 Continue 禁用");
            SceneFlowContinuePreparation afterDelete = null;
            yield return host.PrepareContinueAsync(default)
                .ToCoroutine(result => afterDelete = result);
            Assert.IsFalse(afterDelete.Succeeded);
            Assert.AreEqual(UserLanguagePreference.English, host.Settings.Current.Language);
            Assert.IsTrue(host.Settings.Current.ReduceMotion);

            Button openSettings = FindButton("front-end-settings");
            InvokeButton(openSettings);
            Button restoreDefaults = FindButton("settings-restore-defaults");
            InvokeButton(restoreDefaults);
            Assert.AreEqual(DisplayStyle.Flex, modal.resolvedStyle.display);
            Assert.AreEqual(UserLanguagePreference.English, host.Settings.Current.Language);
            InvokeButton(modalConfirm);
            yield return WaitUntil(() =>
                !host.Settings.IsBusy
                && host.Settings.Current.Language == UserLanguagePreference.Auto
                && string.Equals(
                    host.Input.GetBindingControlPath(bindingTarget),
                    defaultBinding,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    host.Localization.CurrentLocaleCode,
                    defaultLocale,
                    StringComparison.OrdinalIgnoreCase),
                "等待默认设置消费者收敛",
                () => $"settingsBusy={host.Settings.IsBusy}, "
                    + $"language={host.Settings.Current.Language}, "
                    + $"binding={host.Input.GetBindingControlPath(bindingTarget)}, "
                    + $"expectedBinding={defaultBinding}, "
                    + $"locale={host.Localization.CurrentLocaleCode}, "
                    + $"expectedLocale={defaultLocale}");
            AssertDefaultConsumers(host, bindingTarget, defaultBinding, defaultLocale);
            SceneFlowContinuePreparation afterReset = null;
            yield return host.PrepareContinueAsync(default)
                .ToCoroutine(result => afterReset = result);
            Assert.IsFalse(afterReset.Succeeded, "恢复设置不得重新创建存档");

            yield return RestartApplicationHost();
            host = ApplicationHost.Current;
            AssertDefaultConsumers(host, bindingTarget, defaultBinding, defaultLocale);
            SceneFlowContinuePreparation afterRestart = null;
            yield return host.PrepareContinueAsync(default)
                .ToCoroutine(result => afterRestart = result);
            Assert.IsFalse(afterRestart.Succeeded, "重启后 Continue 必须保持不可用");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ApplicationHost>(
                FindObjectsInactive.Include).Length);
            Assert.AreEqual(0, host.ResourceDiagnostics.InFlightLoads);
        }

        IEnumerator RestartApplicationHost()
        {
            ApplicationHost oldHost = ApplicationHost.Current;
            UnityEngine.Object.DestroyImmediate(oldHost.gameObject);
            GameObject hostObject = new GameObject("[ApplicationHost]");
            hostObject.AddComponent<ApplicationHost>();
            yield return _sceneFlow.ReloadBootstrap();
            yield return _sceneFlow.EnterFrontEnd();
        }

        static void AssertDefaultConsumers(
            ApplicationHost host,
            InputBindingTarget bindingTarget,
            string defaultBinding,
            string defaultLocale)
        {
            UserSettingsSnapshot defaults = UserSettingsSnapshot.Default;
            UserSettingsSnapshot current = host.Settings.Current;
            Assert.AreEqual(defaults.Language, current.Language);
            Assert.AreEqual(defaults.MasterVolume, current.MasterVolume);
            Assert.AreEqual(defaults.MusicVolume, current.MusicVolume);
            Assert.AreEqual(defaults.SoundEffectsVolume, current.SoundEffectsVolume);
            Assert.AreEqual(defaults.UiVolume, current.UiVolume);
            Assert.AreEqual(defaults.Muted, current.Muted);
            Assert.AreEqual(defaults.BindingOverridesJson, current.BindingOverridesJson);
            Assert.AreEqual(defaults.GlyphPreference, host.Input.GlyphPreference);
            Assert.AreEqual(
                defaultBinding,
                host.Input.GetBindingControlPath(bindingTarget));
            Assert.AreEqual(
                MotionProfile.FromReduceMotion(defaults.ReduceMotion),
                host.Accessibility.Profile);
            Assert.AreEqual(defaults.MasterVolume, host.Audio.LastAppliedSettings.MasterVolume);
            Assert.AreEqual(defaultLocale, host.Localization.CurrentLocaleCode);
        }

        static IEnumerator WaitUntil(
            Func<bool> condition,
            string message,
            Func<string> details = null)
        {
            float timeout = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!condition() && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsTrue(
                condition(),
                details == null
                    ? $"{message}超时"
                    : $"{message}超时：{details.Invoke()}");
        }

        static Button FindButton(string name)
        {
            return FindElement<Button>(name);
        }

        static VisualElement FindElement(string name)
        {
            return FindElement<VisualElement>(name);
        }

        static T FindElement<T>(string name) where T : VisualElement
        {
            UIDocument[] documents = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include);

            for (int i = 0; i < documents.Length; i++)
            {
                T element = documents[i].rootVisualElement?.Q<T>(name);

                if (element != null)
                {
                    return element;
                }
            }

            Assert.Fail($"未找到 UI 元素：{name}");
            return null;
        }

        static void InvokeButton(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(Clickable).FullName, "Invoke");
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
