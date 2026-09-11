using System;
using System.IO;
using UnityEngine;

namespace DarkFlare.Tests
{
    internal static class Alpha027PlayModeDataEnvironment
    {
        static IDisposable s_installation;

        internal static string RootPath { get; private set; }

        internal static string SettingsRootPath { get; private set; }

        internal static string SaveRootPath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            ReleaseAndDelete();
            RootPath = Path.Combine(
                Application.temporaryCachePath,
                "DarkFlare",
                "PlayModeTests",
                Guid.NewGuid().ToString("N"));
            SettingsRootPath = Path.Combine(RootPath, "Settings");
            SaveRootPath = Path.Combine(RootPath, "Saves");
            s_installation = ApplicationDataPathProviderFactory.InstallForTests(
                () => new FixedSettingsPathProvider(SettingsRootPath),
                () => new FixedSavePathProvider(SaveRootPath));
        }

        internal static void ReinstallForTestRun()
        {
            Install();
        }

        internal static void ReleaseAndDelete()
        {
            s_installation?.Dispose();
            s_installation = null;

            string rootPath = RootPath;
            RootPath = null;
            SettingsRootPath = null;
            SaveRootPath = null;

            if (string.IsNullOrWhiteSpace(rootPath)
                || !Directory.Exists(rootPath))
            {
                return;
            }

            string allowedParent = Path.GetFullPath(Path.Combine(
                Application.temporaryCachePath,
                "DarkFlare",
                "PlayModeTests"));
            DirectoryInfo rootDirectory = new DirectoryInfo(Path.GetFullPath(rootPath));

            if (rootDirectory.Parent == null
                || !string.Equals(
                    rootDirectory.Parent.FullName,
                    allowedParent,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"拒绝清理非测试目录：{rootPath}");
            }

            rootDirectory.Delete(true);
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
