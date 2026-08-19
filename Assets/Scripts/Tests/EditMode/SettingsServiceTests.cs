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
    }
}
