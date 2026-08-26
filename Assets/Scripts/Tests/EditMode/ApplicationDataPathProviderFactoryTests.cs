using System;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class ApplicationDataPathProviderFactoryTests
    {
        [SetUp]
        public void SetUp()
        {
            ApplicationDataPathProviderFactory.ResetStaticState();
        }

        [TearDown]
        public void TearDown()
        {
            ApplicationDataPathProviderFactory.ResetStaticState();
        }

        [Test]
        public void InstallForTests_EnforcesOneOwnerAndRestoresProductionProviders()
        {
            IDisposable installation = ApplicationDataPathProviderFactory.InstallForTests(
                () => new FixedSettingsPathProvider("test-settings"),
                () => new FixedSavePathProvider("test-saves"));

            Assert.AreEqual(
                "test-settings",
                ApplicationDataPathProviderFactory.CreateSettingsPathProvider().RootPath);
            Assert.AreEqual(
                "test-saves",
                ApplicationDataPathProviderFactory.CreateSavePathProvider().RootPath);
            Assert.Throws<InvalidOperationException>(() =>
                ApplicationDataPathProviderFactory.InstallForTests(
                    () => new FixedSettingsPathProvider("other-settings"),
                    () => new FixedSavePathProvider("other-saves")));

            installation.Dispose();

            Assert.IsInstanceOf<PersistentSettingsPathProvider>(
                ApplicationDataPathProviderFactory.CreateSettingsPathProvider());
            Assert.IsInstanceOf<PersistentSavePathProvider>(
                ApplicationDataPathProviderFactory.CreateSavePathProvider());
        }

        [Test]
        public void StaleInstallation_CannotReleaseReplacementOwner()
        {
            IDisposable stale = ApplicationDataPathProviderFactory.InstallForTests(
                () => new FixedSettingsPathProvider("stale-settings"),
                () => new FixedSavePathProvider("stale-saves"));
            ApplicationDataPathProviderFactory.ResetStaticState();
            IDisposable current = ApplicationDataPathProviderFactory.InstallForTests(
                () => new FixedSettingsPathProvider("current-settings"),
                () => new FixedSavePathProvider("current-saves"));

            stale.Dispose();

            Assert.AreEqual(
                "current-settings",
                ApplicationDataPathProviderFactory.CreateSettingsPathProvider().RootPath);
            Assert.AreEqual(
                "current-saves",
                ApplicationDataPathProviderFactory.CreateSavePathProvider().RootPath);
            current.Dispose();
        }

        [Test]
        public void SubsystemReset_PreservesOwnedTestInstallation()
        {
            IDisposable installation = ApplicationDataPathProviderFactory.InstallForTests(
                () => new FixedSettingsPathProvider("settings-test"),
                () => new FixedSavePathProvider("save-test"));

            ApplicationDataPathProviderFactory.ResetForSubsystemRegistration();

            Assert.AreEqual(
                "settings-test",
                ApplicationDataPathProviderFactory.CreateSettingsPathProvider().RootPath);
            Assert.AreEqual(
                "save-test",
                ApplicationDataPathProviderFactory.CreateSavePathProvider().RootPath);

            installation.Dispose();
            Assert.IsInstanceOf<PersistentSettingsPathProvider>(
                ApplicationDataPathProviderFactory.CreateSettingsPathProvider());
            Assert.IsInstanceOf<PersistentSavePathProvider>(
                ApplicationDataPathProviderFactory.CreateSavePathProvider());
        }

        sealed class FixedSettingsPathProvider : ISettingsPathProvider
        {
            public string RootPath { get; }

            public FixedSettingsPathProvider(string rootPath)
            {
                RootPath = rootPath;
            }
        }

        sealed class FixedSavePathProvider : ISavePathProvider
        {
            public string RootPath { get; }

            public FixedSavePathProvider(string rootPath)
            {
                RootPath = rootPath;
            }
        }
    }
}
