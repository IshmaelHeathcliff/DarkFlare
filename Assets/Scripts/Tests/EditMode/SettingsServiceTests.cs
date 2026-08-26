using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class SettingsServiceTests
    {
        string _rootPath;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(
                Path.GetTempPath(),
                "DarkFlareSettingsServiceTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            string fullRoot = Path.GetFullPath(_rootPath);
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "DarkFlareSettingsServiceTests"));
            StringAssert.StartsWith(
                expectedParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                fullRoot);

            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }

        [Test]
        public void Initialize_MissingFileUsesValidatedDefaults()
        {
            SettingsService service = CreateService();

            SettingsOperationResult result = service.Initialize();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SettingsOperationCode.DefaultLoaded, result.Code);
            Assert.AreEqual(SettingsRecoverySource.DefaultMissing, result.RecoverySource);
            Assert.AreEqual(UserLanguagePreference.Auto, service.Current.Language);
            Assert.IsTrue(SettingsDataValidator.ValidateSnapshot(service.Current).Succeeded);
        }

        [UnityTest]
        public IEnumerator Update_PersistsThenReloadsAndPublishesCommittedSnapshot()
        {
            return VerifyUpdateAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator InvalidUpdate_DoesNotChangeMemoryOrCreateFile()
        {
            return VerifyInvalidUpdateAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator ResetToDefaults_PersistsCompleteDefaultSnapshot()
        {
            return VerifyResetToDefaultsAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator ResetToDefaults_StorageFailurePreservesPreviousSnapshot()
        {
            return VerifyResetFailureAsync().ToCoroutine();
        }

        async UniTask VerifyUpdateAsync()
        {
            SettingsService service = CreateService();
            Assert.IsTrue(service.Initialize().Succeeded);
            int changedCount = 0;
            service.Changed += settings =>
            {
                changedCount++;
                Assert.AreEqual(UserLanguagePreference.English, settings.Language);
            };

            SettingsOperationResult updated = await service.UpdateAsync(
                service.Current.WithLanguage(UserLanguagePreference.English));

            Assert.IsTrue(updated.Succeeded, updated.Exception?.ToString());
            Assert.AreEqual(1, changedCount);
            Assert.AreEqual(UserLanguagePreference.English, service.Current.Language);
            SettingsService reloaded = CreateService();
            SettingsOperationResult reloadResult = reloaded.Initialize();
            Assert.IsTrue(reloadResult.Succeeded, reloadResult.Exception?.ToString());
            Assert.AreEqual(UserLanguagePreference.English, reloaded.Current.Language);
        }

        async UniTask VerifyInvalidUpdateAsync()
        {
            SettingsService service = CreateService();
            Assert.IsTrue(service.Initialize().Succeeded);
            UserSettingsSnapshot invalid = new UserSettingsSnapshot(
                UserLanguagePreference.English,
                2f,
                1f,
                1f,
                1f,
                false,
                string.Empty,
                InputGlyphPreference.Auto,
                1920,
                1080,
                PreferredDisplayMode.FullscreenWindow,
                1f,
                1f,
                false,
                1f,
                false);

            SettingsOperationResult result = await service.UpdateAsync(invalid);

            Assert.AreEqual(SettingsOperationCode.InvalidSettings, result.Code);
            Assert.AreEqual(UserLanguagePreference.Auto, service.Current.Language);
            Assert.IsFalse(File.Exists(Path.Combine(_rootPath, "settings.json")));
        }

        async UniTask VerifyResetToDefaultsAsync()
        {
            SettingsService service = CreateService();
            Assert.IsTrue(service.Initialize().Succeeded);
            UserSettingsSnapshot customized = service.Current
                .WithLanguage(UserLanguagePreference.English)
                .WithAudio(0.2f, 0.3f, 0.4f, 0.5f, true)
                .WithInput("{\"bindings\":[]}", InputGlyphPreference.Gamepad)
                .WithReduceMotion(true);
            Assert.IsTrue((await service.UpdateAsync(customized)).Succeeded);

            SettingsOperationResult result = await service.ResetToDefaultsAsync();

            Assert.IsTrue(result.Succeeded, result.Exception?.ToString());
            AssertDefaults(service.Current);
            SettingsService reloaded = CreateService();
            Assert.IsTrue(reloaded.Initialize().Succeeded);
            AssertDefaults(reloaded.Current);
        }

        async UniTask VerifyResetFailureAsync()
        {
            SwitchableSettingsStorage storage = new SwitchableSettingsStorage(
                new LocalSettingsStorage(
                    new TestSettingsPathProvider(_rootPath),
                    new NewtonsoftSettingsSerializer()));
            SettingsService service = new SettingsService(storage);
            Assert.IsTrue(service.Initialize().Succeeded);
            UserSettingsSnapshot customized = service.Current
                .WithLanguage(UserLanguagePreference.English)
                .WithReduceMotion(true);
            Assert.IsTrue((await service.UpdateAsync(customized)).Succeeded);
            storage.FailCommit = true;

            SettingsOperationResult result = await service.ResetToDefaultsAsync();

            Assert.AreEqual(SettingsOperationCode.StorageFailure, result.Code);
            Assert.AreEqual(UserLanguagePreference.English, service.Current.Language);
            Assert.IsTrue(service.Current.ReduceMotion);
        }

        static void AssertDefaults(UserSettingsSnapshot settings)
        {
            UserSettingsSnapshot defaults = UserSettingsSnapshot.Default;
            Assert.AreEqual(defaults.Language, settings.Language);
            Assert.AreEqual(defaults.MasterVolume, settings.MasterVolume);
            Assert.AreEqual(defaults.MusicVolume, settings.MusicVolume);
            Assert.AreEqual(defaults.SoundEffectsVolume, settings.SoundEffectsVolume);
            Assert.AreEqual(defaults.UiVolume, settings.UiVolume);
            Assert.AreEqual(defaults.Muted, settings.Muted);
            Assert.AreEqual(defaults.BindingOverridesJson, settings.BindingOverridesJson);
            Assert.AreEqual(defaults.GlyphPreference, settings.GlyphPreference);
            Assert.AreEqual(defaults.ReduceMotion, settings.ReduceMotion);
        }

        SettingsService CreateService()
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

        sealed class SwitchableSettingsStorage : ILocalSettingsStorage
        {
            readonly ILocalSettingsStorage _inner;

            public SwitchableSettingsStorage(ILocalSettingsStorage inner)
            {
                _inner = inner;
            }

            public bool FailCommit { get; set; }

            public LocalSettingsCommitResult Commit(
                UserSettingsDocumentDto document,
                System.Threading.CancellationToken cancellationToken = default)
            {
                return FailCommit
                    ? new LocalSettingsCommitResult(
                        LocalSettingsStorageCode.IoFailure,
                        null,
                        SettingsSerializationCode.Success,
                        new IOException("injected settings failure"))
                    : _inner.Commit(document, cancellationToken);
            }

            public LocalSettingsLoadResult Load(
                System.Threading.CancellationToken cancellationToken = default)
            {
                return _inner.Load(cancellationToken);
            }
        }
    }
}
