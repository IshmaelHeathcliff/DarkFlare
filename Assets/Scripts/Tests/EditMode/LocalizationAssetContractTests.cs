using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class LocalizationAssetContractTests
    {
        static readonly string[] RequiredTables =
        {
            "ui",
            "system",
            "items",
            "stats",
            "affixes",
            "monsters",
        };

        string _rootPath;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(
                Path.GetTempPath(),
                "DarkFlareLocalizationAssetTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            string fullRoot = Path.GetFullPath(_rootPath);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "DarkFlareLocalizationAssetTests"));
            StringAssert.StartsWith(
                expectedParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                fullRoot);

            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }

        [Test]
        public void Assets_ContainFrozenLocalesPseudoLocaleAndResponsibilityTables()
        {
            string[] localeCodes = LocalizationEditorSettings.GetLocales()
                .Select(locale => locale.Identifier.Code)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
            string[] tableNames = LocalizationEditorSettings.GetStringTableCollections()
                .Select(collection => collection.TableCollectionName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            IReadOnlyList<PseudoLocale> pseudoLocales =
                LocalizationEditorSettings.GetPseudoLocales();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    LocalizationService.EnglishLocaleCode,
                    LocalizationService.SimplifiedChineseLocaleCode,
                },
                localeCodes);
            CollectionAssert.AreEquivalent(RequiredTables, tableNames);
            Assert.AreEqual(1, pseudoLocales.Count);
            Assert.AreEqual("qps-ploc", pseudoLocales[0].Identifier.Code);
            Assert.AreEqual(
                LocalizationService.SimplifiedChineseLocaleCode,
                LocalizationSettings.ProjectLocale.Identifier.Code);
        }

        [Test]
        public void GameRoot_ContainsFocusableLanguageSelectorAndLocalizedLabelBinding()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/GameRoot.uxml");
            Assert.NotNull(tree);
            TemplateContainer root = tree.CloneTree();
            DropdownField dropdown = root.Q<DropdownField>("game-menu-language-dropdown");
            Label label = root.Q<Label>("game-menu-language-label");

            Assert.NotNull(dropdown);
            Assert.IsTrue(dropdown.focusable);
            Assert.NotNull(label);
            Assert.IsEmpty(label.text);
            Assert.NotNull(label.GetBinding("text"));
        }

        [UnityTest]
        public IEnumerator Runtime_LoadsRealTablesAndPersistsExplicitLanguageInTemporarySettings()
        {
            return VerifyRuntimeIntegrationAsync().ToCoroutine();
        }

        async UniTask VerifyRuntimeIntegrationAsync()
        {
            string originalLocaleCode = LocalizationSettings.SelectedLocale?.Identifier.Code
                ?? LocalizationService.SimplifiedChineseLocaleCode;
            SettingsService settings = new SettingsService(new LocalSettingsStorage(
                new TestSettingsPathProvider(_rootPath),
                new NewtonsoftSettingsSerializer()));
            UnityLocalizationRuntime runtime = new UnityLocalizationRuntime();
            LocalizationService service = new LocalizationService(settings, runtime);

            try
            {
                Assert.IsTrue(settings.Initialize().Succeeded);
                Assert.IsTrue((await settings.UpdateAsync(
                    settings.Current.WithLanguage(UserLanguagePreference.English))).Succeeded);
                LocalizationOperationResult initialized = await service.InitializeAsync(
                    CancellationToken.None);
                Assert.IsTrue(initialized.Succeeded, initialized.Exception?.ToString());
                Assert.AreEqual("Language", await service.GetStringAsync(
                    "ui",
                    "settings.language.label"));

                LocalizationOperationResult changed = await service.ChangeLanguageAsync(
                    UserLanguagePreference.SimplifiedChinese);

                Assert.IsTrue(changed.Succeeded, changed.Exception?.ToString());
                Assert.AreEqual("语言", await service.GetStringAsync(
                    "ui",
                    "settings.language.label"));
                Assert.AreEqual(
                    UserLanguagePreference.SimplifiedChinese,
                    settings.Current.Language);
                Assert.IsTrue(File.Exists(Path.Combine(_rootPath, "settings.json")));
            }
            finally
            {
                service.Close();

                if (runtime.IsLocaleAvailable(originalLocaleCode))
                {
                    await runtime.ApplyLocaleAsync(
                        originalLocaleCode,
                        Array.Empty<string>(),
                        CancellationToken.None);
                }
            }
        }

        sealed class TestSettingsPathProvider : ISettingsPathProvider
        {
            public string RootPath { get; }

            public TestSettingsPathProvider(string rootPath)
            {
                RootPath = rootPath;
            }
        }
    }
}
