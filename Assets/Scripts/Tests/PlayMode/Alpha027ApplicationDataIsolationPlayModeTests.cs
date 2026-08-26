using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha027ApplicationDataIsolationPlayModeTests
    {
        readonly SceneFlowPlayModeFixture _sceneFlow = new SceneFlowPlayModeFixture();

        [UnityTest]
        public IEnumerator ApplicationHost_UsesTemporaryIsolatedDataRoots()
        {
            yield return _sceneFlow.EnterFrontEnd();

            ApplicationHost host = ApplicationHost.Current;
            AssertPathEquals(
                Alpha027PlayModeDataEnvironment.SettingsRootPath,
                host.SettingsRootPath);
            AssertPathEquals(
                Alpha027PlayModeDataEnvironment.SaveRootPath,
                host.SaveRootPath);
            AssertPathIsUnder(
                Alpha027PlayModeDataEnvironment.RootPath,
                host.SettingsRootPath);
            AssertPathIsUnder(
                Alpha027PlayModeDataEnvironment.RootPath,
                host.SaveRootPath);

            string persistentRoot = Path.GetFullPath(Application.persistentDataPath);
            Assert.IsFalse(
                IsPathUnder(persistentRoot, host.SettingsRootPath),
                "PlayMode 设置不得写入 Application.persistentDataPath");
            Assert.IsFalse(
                IsPathUnder(persistentRoot, host.SaveRootPath),
                "PlayMode 存档不得写入 Application.persistentDataPath");
        }

        static void AssertPathEquals(string expected, string actual)
        {
            Assert.AreEqual(
                Path.GetFullPath(expected),
                Path.GetFullPath(actual));
        }

        static void AssertPathIsUnder(string parent, string candidate)
        {
            Assert.IsTrue(
                IsPathUnder(parent, candidate),
                $"{candidate} 不在隔离根目录 {parent} 下");
        }

        static bool IsPathUnder(string parent, string candidate)
        {
            string normalizedParent = Path.GetFullPath(parent)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidate);
            return normalizedCandidate.StartsWith(
                normalizedParent,
                System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
