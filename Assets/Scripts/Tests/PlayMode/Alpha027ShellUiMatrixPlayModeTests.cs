using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class Alpha027ShellUiMatrixPlayModeTests
    {
        readonly SceneFlowPlayModeFixture _fixture = new SceneFlowPlayModeFixture();

        [UnityTest]
        public IEnumerator GameplayMenu_SettingsAndConfirmationOwnInputUntilDismissed()
        {
            var input = new InputTestFixture();
            yield return InputTestFixtureGuard.Setup(input);
            yield return _fixture.EnterMain();
            ApplicationHost host = ApplicationHost.Current;
            GameInput gameInput = host.CurrentSession.Architecture.GetUtility<GameInput>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();

            try
            {
                gameInput.SwitchToUi();
                yield return null;
                yield return ClickPointer(input, mouse, FindButton("game-menu-settings"));
                Assert.IsTrue(host.ApplicationShell.IsSettingsOpen);
                Assert.AreEqual(DisplayStyle.None, FindElement("game-menu-overlay").resolvedStyle.display,
                    "设置接管后背包必须隐藏");

                gameInput.SwitchToGameplay();
                yield return null;
                Assert.IsTrue(host.Input.IsUiEnabled, "底层关闭不能禁用设置输入");
                Assert.IsFalse(host.Input.IsGameplayEnabled);
                Assert.IsTrue(host.GameTime.IsPaused, "上层设置必须持有自己的暂停");
                yield return ClickPointer(input, mouse, FindButton("settings-back"));
                Assert.IsFalse(host.ApplicationShell.IsSettingsOpen);
                Assert.IsTrue(host.Input.IsGameplayEnabled);
                Assert.IsFalse(host.GameTime.IsPaused);

                gameInput.SwitchToUi();
                yield return null;
                yield return ClickPointer(input, mouse, FindButton("game-menu-settings"));
                gameInput.SwitchToGameplay();
                input.Press(keyboard.escapeKey);
                yield return null;
                input.Release(keyboard.escapeKey);
                yield return null;
                Assert.IsFalse(host.ApplicationShell.IsSettingsOpen);
                Assert.IsTrue(host.Input.IsGameplayEnabled,
                    "底层先关闭后，键盘返回设置也必须安全恢复 Gameplay");
                Assert.IsFalse(host.GameTime.IsPaused);

                gameInput.SwitchToUi();
                yield return null;
                yield return ClickPointer(input, mouse, FindButton("game-menu-settings"));
                ((ScrollView)FindElement("settings-scroll")).ScrollTo(FindButton("settings-restore-defaults"));
                yield return null;
                yield return null;
                yield return ClickPointer(input, mouse, FindButton("settings-restore-defaults"));
                input.Press(gamepad.buttonEast);
                yield return null;
                input.Release(gamepad.buttonEast);
                yield return null;
                Assert.AreEqual(DisplayStyle.None, FindElement("application-modal").resolvedStyle.display);
                Assert.IsTrue(host.ApplicationShell.IsSettingsOpen,
                    "一次返回关闭确认框后必须保留下面的设置页");
                Assert.AreEqual(DisplayStyle.None, FindElement("game-menu-overlay").resolvedStyle.display);
                input.Press(keyboard.escapeKey);
                yield return null;
                input.Release(keyboard.escapeKey);
                yield return null;
                Assert.IsFalse(host.ApplicationShell.IsSettingsOpen);
                Assert.AreEqual(DisplayStyle.Flex, FindElement("game-menu-overlay").resolvedStyle.display,
                    "一次返回只关闭设置并恢复背包");
                Assert.AreEqual(GameInputMode.UI, gameInput.CurrentMode);

                yield return ClickPointer(input, mouse, FindButton("game-menu-return-front-end"));
                Assert.AreEqual(DisplayStyle.None, FindElement("game-menu-overlay").resolvedStyle.display);
                input.Press(gamepad.buttonEast);
                yield return null;
                input.Release(gamepad.buttonEast);
                yield return null;
                Assert.AreEqual(DisplayStyle.None, FindElement("application-modal").resolvedStyle.display);
                Assert.AreEqual(DisplayStyle.Flex, FindElement("game-menu-overlay").resolvedStyle.display);
                Assert.AreEqual(GameInputMode.UI, gameInput.CurrentMode);

                yield return ClickPointer(input, mouse, FindButton("game-menu-return-front-end"));
                gameInput.SwitchToGameplay();
                yield return null;
                Assert.IsTrue(host.Input.IsUiEnabled);
                yield return ClickPointer(input, mouse, FindButton("application-modal-cancel"));
                Assert.AreEqual(DisplayStyle.None, FindElement("application-modal").resolvedStyle.display);
                Assert.IsTrue(host.Input.IsGameplayEnabled);
                Assert.IsFalse(host.GameTime.IsPaused);

                gameInput.SwitchToUi();
                yield return null;
                yield return ClickPointer(input, mouse, FindButton("game-menu-return-front-end"));
                yield return ClickPointer(input, mouse, FindButton("application-modal-retry"));
                float timeout = Time.realtimeSinceStartup + 20f;

                while ((host.SceneFlow.IsBusy || host.SceneFlow.State != GameFlowState.FrontEnd)
                       && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }

                Assert.AreEqual(GameFlowState.FrontEnd, host.SceneFlow.State);
                Assert.IsNull(host.CurrentSession);
                Assert.IsFalse(host.GameTime.IsPaused);
                Assert.IsFalse(host.Input.HasUiContextOverride);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
                InputTestFixtureGuard.TearDown(input);
            }
        }

        static IEnumerator ClickPointer(InputTestFixture input, Mouse mouse, Button button)
        {
            bool clicked = false;
            Action observeClick = () => clicked = true;
            button.clicked += observeClick;
            VisualElement root = button.panel.visualTree;
            Rect bounds = root.worldBound;
            Vector2 point = button.worldBound.center;
            input.Set(mouse.position, new Vector2(
                (point.x - bounds.xMin) / bounds.width * Screen.width,
                (1f - (point.y - bounds.yMin) / bounds.height) * Screen.height));
            yield return null;
            input.Press(mouse.leftButton);
            yield return null;
            input.Release(mouse.leftButton);
            yield return null;
            yield return null;
            button.clicked -= observeClick;
            Assert.IsTrue(clicked,
                $"鼠标未点击到 {button.name}：bounds={button.worldBound}, root={bounds}, "
                + $"point={point}, picked={button.panel?.Pick(point)}, "
                + $"context={ApplicationHost.Current.Input.CurrentContext}, "
                + $"suspension={ApplicationHost.Current.Input.SuspensionReasons}");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.EnterFrontEnd();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator FrontEndSettingsAndErrorModal_FitThreeLocalesAtStandardResolution()
        {
            ApplicationHost host = ApplicationHost.Current;
            ApplicationShellController shell = host.ApplicationShell;
            Assert.IsNotNull(shell);
            Vector2Int originalResolution = new Vector2Int(Screen.width, Screen.height);
            Vector2Int[] resolutions =
            {
                new Vector2Int(1920, 1080),
            };
            string[] localeModes = { "zh-Hans", "en", "qps-ploc" };

            try
            {
                for (int localeIndex = 0; localeIndex < localeModes.Length; localeIndex++)
                {
                    string localeMode = localeModes[localeIndex];

                    for (int resolutionIndex = 0;
                         resolutionIndex < resolutions.Length;
                         resolutionIndex++)
                    {
                        Vector2Int resolution = resolutions[resolutionIndex];
                        yield return PrepareLocale(host.Localization, localeMode);
                        SetGameViewResolution(resolution);
                        yield return WaitForResolution(resolution);
                        yield return null;
                        VisualElement root = FindElement("application-shell");

                        if (localeMode == "qps-ploc")
                        {
                            Assert.Greater(ApplyPseudoLocalization(root), 0);
                            yield return null;
                        }

                        AssertInsideRoot(root, new[]
                        {
                            "application-front-end",
                            "front-end-continue",
                            "front-end-new-game",
                            "front-end-settings",
                            "front-end-delete-save",
                            "front-end-language",
                            "front-end-quit",
                            "front-end-version",
                        }, localeMode, resolution);
                        AssertVisibleTextFits(root, localeMode, resolution);

                        shell.OpenSettings();
                        yield return null;

                        if (localeMode == "qps-ploc")
                        {
                            ApplyPseudoLocalization(root);
                            yield return null;
                        }

                        AssertInsideRoot(root, new[]
                        {
                            "application-settings",
                            "settings-scroll",
                            "settings-back",
                            "settings-status",
                        }, localeMode, resolution);
                        AssertVisibleTextFits(root, localeMode, resolution);
                        InvokeButton(FindButton("settings-back"));
                        yield return null;

                        shell.ShowFailure(CreateRecoverableFailure());
                        yield return null;

                        if (localeMode == "qps-ploc")
                        {
                            ApplyPseudoLocalization(root);
                            yield return null;
                        }

                        AssertInsideRoot(root, new[]
                        {
                            "application-modal",
                            "application-modal-title",
                            "application-modal-message",
                            "application-modal-retry",
                            "application-modal-cancel",
                        }, localeMode, resolution);
                        AssertVisibleTextFits(root, localeMode, resolution);
                        InvokeButton(FindButton("application-modal-cancel"));
                        yield return null;
                    }
                }
            }
            finally
            {
                SetGameViewResolution(originalResolution);
            }
        }

        static SceneFlowResult CreateRecoverableFailure()
        {
            return SceneFlowResult.Failure(
                SceneFlowOperation.StartGame,
                SceneFlowErrorCode.LoadFailed,
                GameFlowState.FrontEnd,
                GameFlowState.InGame,
                SceneFlowPhase.LoadingScene,
                SceneId.Main,
                SceneFlowRecoveryAction.Retry | SceneFlowRecoveryAction.ReturnToFrontEnd,
                new LocalizedMessage("system", "scene_flow.load_failed"),
                new InvalidOperationException("UI matrix fixture"));
        }

        static IEnumerator PrepareLocale(
            LocalizationService service,
            string localeMode)
        {
            if (localeMode == "qps-ploc")
            {
                yield return ChangeLanguage(
                    service,
                    UserLanguagePreference.SimplifiedChinese);
                yield return ChangeLanguage(service, UserLanguagePreference.English);
                yield break;
            }

            yield return ChangeLanguage(
                service,
                localeMode == "zh-Hans"
                    ? UserLanguagePreference.SimplifiedChinese
                    : UserLanguagePreference.English);
        }

        static IEnumerator ChangeLanguage(
            LocalizationService service,
            UserLanguagePreference preference)
        {
            LocalizationOperationResult result = null;
            yield return service.ChangeLanguageAsync(preference)
                .ToCoroutine(value => result = value);
            Assert.IsTrue(
                result.Succeeded,
                $"切换语言 {preference} 失败：{result.Code} {result.Exception}");
            yield return null;
            yield return null;
        }

        static IEnumerator WaitForResolution(Vector2Int resolution)
        {
            float timeout = Time.realtimeSinceStartup + 5f;

            while ((Screen.width != resolution.x || Screen.height != resolution.y)
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.AreEqual(resolution.x, Screen.width);
            Assert.AreEqual(resolution.y, Screen.height);
        }

        static void SetGameViewResolution(Vector2Int resolution)
        {
            const string UtilityTypeName =
                "DarkFlare.Editor.Phase0GameViewResolutionUtility, DarkFlare.Editor";
            Type utilityType = Type.GetType(UtilityTypeName, true);
            MethodInfo method = utilityType.GetMethod(
                "SetResolution",
                BindingFlags.Static | BindingFlags.Public)
                ?? throw new MissingMethodException(UtilityTypeName, "SetResolution");
            method.Invoke(null, new object[] { resolution.x, resolution.y });
        }

        static int ApplyPseudoLocalization(VisualElement root)
        {
            const string UtilityTypeName =
                "DarkFlare.Editor.PseudoLocalizationUtility, DarkFlare.Editor";
            Type utilityType = Type.GetType(UtilityTypeName, true);
            MethodInfo method = utilityType.GetMethod(
                "Localize",
                BindingFlags.Static | BindingFlags.Public)
                ?? throw new MissingMethodException(UtilityTypeName, "Localize");
            List<TextElement> elements = root.Query<TextElement>().ToList();
            int transformed = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                TextElement element = elements[i];

                if (string.IsNullOrWhiteSpace(element.text)
                    || !element.text.Any(char.IsLetter)
                    || (element.text[0] == '[' && element.text[^1] == ']')
                    || element.worldBound.width <= 0f
                    || element.worldBound.height <= 0f)
                {
                    continue;
                }

                string pseudo = (string)method.Invoke(null, new object[] { element.text });

                if (!string.Equals(pseudo, element.text, StringComparison.Ordinal))
                {
                    element.text = pseudo;
                    transformed++;
                }
            }

            return transformed;
        }

        static void AssertInsideRoot(
            VisualElement root,
            IReadOnlyList<string> names,
            string localeMode,
            Vector2Int resolution)
        {
            Rect rootBounds = root.worldBound;

            for (int i = 0; i < names.Count; i++)
            {
                VisualElement element = root.Q<VisualElement>(names[i]);
                string context = $"{localeMode} {resolution.x}×{resolution.y} {names[i]}";
                Assert.IsNotNull(element, context);
                Rect bounds = element.worldBound;
                Assert.Greater(bounds.width, 0f, context);
                Assert.Greater(bounds.height, 0f, context);
                Assert.GreaterOrEqual(bounds.xMin, rootBounds.xMin - 1f, context);
                Assert.GreaterOrEqual(bounds.yMin, rootBounds.yMin - 1f, context);
                Assert.LessOrEqual(bounds.xMax, rootBounds.xMax + 1f, context);
                Assert.LessOrEqual(bounds.yMax, rootBounds.yMax + 1f, context);
            }
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
                    || element.ClassListContains("application-settings-binding-glyph-text")
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
                string context =
                    $"{localeMode} {resolution.x}×{resolution.y} {identity}";

                if (wraps)
                {
                    Assert.LessOrEqual(measured.y, content.height + 2f, context);
                }
                else
                {
                    Assert.LessOrEqual(measured.x, content.width + 2f, context);
                }
            }
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
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

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
