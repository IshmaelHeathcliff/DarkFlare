using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class LocalizationServiceTests
    {
        string _rootPath;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(
                Path.GetTempPath(),
                "DarkFlareLocalizationServiceTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            string fullRoot = Path.GetFullPath(_rootPath);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "DarkFlareLocalizationServiceTests"));
            StringAssert.StartsWith(
                expectedParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                fullRoot);

            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator Initialize_ExplicitPreferenceOverridesAutomaticLocaleAndPreloadsTables()
        {
            return VerifyExplicitInitializationAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator RapidSwitch_LatestRequestWinsAndOnlyFinalPreferenceRemains()
        {
            return VerifyLatestWinsAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator MissingCurrentTranslation_FallsBackToSimplifiedChineseThenPlaceholder()
        {
            return VerifyFallbackAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator PreloadedString_SynchronousLookupUsesSameFallbackContract()
        {
            return VerifySynchronousFallbackAsync().ToCoroutine();
        }

        async UniTask VerifyExplicitInitializationAsync()
        {
            SettingsService settings = CreateSettingsService();
            Assert.IsTrue(settings.Initialize().Succeeded);
            Assert.IsTrue((await settings.UpdateAsync(
                settings.Current.WithLanguage(UserLanguagePreference.English))).Succeeded);
            FakeLocalizationRuntime runtime = new FakeLocalizationRuntime
            {
                AutomaticLocaleCode = LocalizationService.SimplifiedChineseLocaleCode,
            };
            LocalizationService service = new LocalizationService(settings, runtime);

            LocalizationOperationResult result = await service.InitializeAsync(CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Exception?.ToString());
            Assert.AreEqual(LocalizationService.EnglishLocaleCode, service.CurrentLocaleCode);
            Assert.AreEqual(LocalizationService.EnglishLocaleCode, runtime.SelectedLocaleCode);
            CollectionAssert.AreEquivalent(new[] { "ui", "system" }, runtime.LastPreloadTables);
        }

        async UniTask VerifyLatestWinsAsync()
        {
            SettingsService settings = CreateSettingsService();
            Assert.IsTrue(settings.Initialize().Succeeded);
            FakeLocalizationRuntime runtime = new FakeLocalizationRuntime
            {
                AutomaticLocaleCode = LocalizationService.SimplifiedChineseLocaleCode,
            };
            LocalizationService service = new LocalizationService(settings, runtime);
            Assert.IsTrue((await service.InitializeAsync(CancellationToken.None)).Succeeded);
            runtime.BlockLocale(LocalizationService.EnglishLocaleCode);

            UniTask<LocalizationOperationResult> first = service.ChangeLanguageAsync(
                UserLanguagePreference.English).Preserve();
            await UniTask.WaitUntil(() => runtime.EnglishApplyStarted);
            UniTask<LocalizationOperationResult> latest = service.ChangeLanguageAsync(
                UserLanguagePreference.SimplifiedChinese).Preserve();
            LocalizationOperationResult latestResult = await latest;
            LocalizationOperationResult firstResult = await first;

            Assert.AreEqual(LocalizationOperationCode.Cancelled, firstResult.Code);
            Assert.IsTrue(latestResult.Succeeded, latestResult.Exception?.ToString());
            Assert.AreEqual(
                LocalizationService.SimplifiedChineseLocaleCode,
                service.CurrentLocaleCode);
            Assert.AreEqual(
                UserLanguagePreference.SimplifiedChinese,
                settings.Current.Language);
            Assert.AreEqual(
                LocalizationService.SimplifiedChineseLocaleCode,
                runtime.SelectedLocaleCode);
        }

        async UniTask VerifyFallbackAsync()
        {
            SettingsService settings = CreateSettingsService();
            Assert.IsTrue(settings.Initialize().Succeeded);
            Assert.IsTrue((await settings.UpdateAsync(
                settings.Current.WithLanguage(UserLanguagePreference.English))).Succeeded);
            FakeLocalizationRuntime runtime = new FakeLocalizationRuntime
            {
                AutomaticLocaleCode = LocalizationService.EnglishLocaleCode,
            };
            runtime.SetString("ui", "known", LocalizationService.SimplifiedChineseLocaleCode, "已知");
            LocalizationService service = new LocalizationService(settings, runtime);
            Assert.IsTrue((await service.InitializeAsync(CancellationToken.None)).Succeeded);

            string fallback = await service.GetStringAsync("ui", "known");
            string placeholder = await service.GetStringAsync("ui", "missing");

            Assert.AreEqual("已知", fallback);
            Assert.AreEqual("[ui.missing]", placeholder);
        }

        async UniTask VerifySynchronousFallbackAsync()
        {
            SettingsService settings = CreateSettingsService();
            Assert.IsTrue(settings.Initialize().Succeeded);
            Assert.IsTrue((await settings.UpdateAsync(
                settings.Current.WithLanguage(UserLanguagePreference.English))).Succeeded);
            FakeLocalizationRuntime runtime = new FakeLocalizationRuntime
            {
                AutomaticLocaleCode = LocalizationService.EnglishLocaleCode,
            };
            runtime.SetString("ui", "known", LocalizationService.SimplifiedChineseLocaleCode, "已知");
            LocalizationService service = new LocalizationService(settings, runtime);
            Assert.IsTrue((await service.InitializeAsync(CancellationToken.None)).Succeeded);

            Assert.AreEqual("已知", service.GetString("ui", "known"));
            Assert.AreEqual("[ui.missing]", service.GetString("ui", "missing"));
        }

        SettingsService CreateSettingsService()
        {
            return new SettingsService(new LocalSettingsStorage(
                new TestSettingsPathProvider(_rootPath),
                new NewtonsoftSettingsSerializer()));
        }

        sealed class TestSettingsPathProvider : ISettingsPathProvider
        {
            public string RootPath { get; }

            public TestSettingsPathProvider(string rootPath)
            {
                RootPath = rootPath;
            }
        }

        sealed class FakeLocalizationRuntime : ILocalizationRuntime
        {
            readonly HashSet<string> _available = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                LocalizationService.SimplifiedChineseLocaleCode,
                LocalizationService.EnglishLocaleCode,
            };
            readonly Dictionary<string, UniTaskCompletionSource> _gates =
                new Dictionary<string, UniTaskCompletionSource>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, string> _strings = new Dictionary<string, string>(
                StringComparer.Ordinal);

            public string AutomaticLocaleCode { get; set; }

            public string SelectedLocaleCode { get; private set; } = string.Empty;

            public IReadOnlyList<string> LastPreloadTables { get; private set; } =
                Array.Empty<string>();

            public bool EnglishApplyStarted { get; private set; }

            public bool IsLocaleAvailable(string localeCode)
            {
                return _available.Contains(localeCode);
            }

            public UniTask InitializeAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return UniTask.CompletedTask;
            }

            public async UniTask ApplyLocaleAsync(
                string localeCode,
                IReadOnlyList<string> preloadTables,
                CancellationToken cancellationToken)
            {
                if (string.Equals(
                        localeCode,
                        LocalizationService.EnglishLocaleCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    EnglishApplyStarted = true;
                }

                if (_gates.TryGetValue(localeCode, out UniTaskCompletionSource gate))
                {
                    await gate.Task.AttachExternalCancellation(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                SelectedLocaleCode = localeCode;
                LastPreloadTables = new List<string>(preloadTables).AsReadOnly();
            }

            public UniTask<string> GetStringAsync(
                string tableName,
                string entryKey,
                string localeCode,
                IList<object> arguments,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _strings.TryGetValue(Key(tableName, entryKey, localeCode), out string value);
                return UniTask.FromResult(value ?? string.Empty);
            }

            public string GetString(
                string tableName,
                string entryKey,
                string localeCode,
                IList<object> arguments)
            {
                _strings.TryGetValue(Key(tableName, entryKey, localeCode), out string value);
                return value ?? string.Empty;
            }

            public void BlockLocale(string localeCode)
            {
                _gates[localeCode] = new UniTaskCompletionSource();
            }

            public void SetString(
                string tableName,
                string entryKey,
                string localeCode,
                string value)
            {
                _strings[Key(tableName, entryKey, localeCode)] = value;
            }

            static string Key(string tableName, string entryKey, string localeCode)
            {
                return tableName + "|" + entryKey + "|" + localeCode;
            }
        }
    }
}
