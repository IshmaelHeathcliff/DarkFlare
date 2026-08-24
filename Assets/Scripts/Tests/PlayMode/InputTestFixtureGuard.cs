using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DarkFlare.Tests
{
    internal static class InputTestFixtureGuard
    {
        static InputTestFixture s_activeFixture;

        public static IEnumerator Setup(InputTestFixture fixture)
        {
            bool replacesRuntime = s_activeFixture == null;
            bool restartApplication = false;

            if (replacesRuntime
                && ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                UnityEngine.Object.DestroyImmediate(host.gameObject);
                restartApplication = true;
            }

            RunWithUiInputDisabled(() =>
            {
                if (replacesRuntime)
                {
                    fixture.Setup();
                    s_activeFixture = fixture;
                    return;
                }

                RemoveTestDevices();
            });

            if (restartApplication)
            {
                GameObject hostObject = new GameObject("[ApplicationHost]");
                hostObject.AddComponent<ApplicationHost>();
                yield return null;
                SceneFlowPlayModeFixture sceneFlowFixture =
                    new SceneFlowPlayModeFixture();
                yield return sceneFlowFixture.EnterFrontEnd();
            }
        }

        public static void TearDown(InputTestFixture fixture)
        {
            RunWithUiInputDisabled(RemoveTestDevices);
        }

        static void RemoveTestDevices()
        {
            for (int i = InputSystem.devices.Count - 1; i >= 0; i--)
            {
                InputSystem.RemoveDevice(InputSystem.devices[i]);
            }
        }

        static void RunWithUiInputDisabled(Action action)
        {
            InputSystemUIInputModule[] modules =
                UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            List<InputSystemUIInputModule> enabledModules =
                new List<InputSystemUIInputModule>(modules.Length);

            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null && modules[i].enabled)
                {
                    modules[i].enabled = false;
                    enabledModules.Add(modules[i]);
                }
            }

            InputSystem.DisableAllEnabledActions();

            try
            {
                action();
            }
            finally
            {
                for (int i = 0; i < enabledModules.Count; i++)
                {
                    if (enabledModules[i] != null)
                    {
                        enabledModules[i].enabled = true;
                    }
                }
            }
        }
    }
}
