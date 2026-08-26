using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    [SetUpFixture]
    public sealed class Alpha027PlayModeDataEnvironmentFixture
    {
        [OneTimeSetUp]
        public void InstallDataEnvironment()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                Object.DestroyImmediate(host.gameObject);
            }

            ApplicationShellBootstrap[] shells =
                Object.FindObjectsByType<ApplicationShellBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < shells.Length; i++)
            {
                Object.DestroyImmediate(shells[i].gameObject);
            }

            Alpha027PlayModeDataEnvironment.ReinstallForTestRun();
            GameObject hostObject = new GameObject("[ApplicationHost]");
            hostObject.AddComponent<ApplicationHost>();
        }

        [OneTimeTearDown]
        public void ReleaseDataEnvironment()
        {
            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                Object.DestroyImmediate(host.gameObject);
            }

            Alpha027PlayModeDataEnvironment.ReleaseAndDelete();
        }
    }
}
