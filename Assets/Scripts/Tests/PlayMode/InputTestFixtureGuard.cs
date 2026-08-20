using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DarkFlare.Tests
{
    internal static class InputTestFixtureGuard
    {
        static InputTestFixture s_activeFixture;

        public static void Setup(InputTestFixture fixture)
        {
            RunWithUiInputDisabled(() =>
            {
                if (s_activeFixture == null)
                {
                    fixture.Setup();
                    s_activeFixture = fixture;
                    return;
                }

                RemoveTestDevices();
            });
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
                    modules[i].UnassignActions();
                    enabledModules.Add(modules[i]);
                }
            }

            InputSystem.DisableAllEnabledActions();

            try
            {
                action();

                for (int i = 0; i < enabledModules.Count; i++)
                {
                    InputSystemUIInputModule module = enabledModules[i];
                    if (module != null)
                    {
                        module.AssignDefaultActions();
                    }
                }
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
