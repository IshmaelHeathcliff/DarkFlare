using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DarkFlare.Tests
{
    internal static class InputTestFixtureGuard
    {
        static InputTestFixture s_activeFixture;
        static InputActionAsset s_originalActions;
        static readonly PropertyInfo InputManager = typeof(InputSystem).GetProperty("manager", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly PropertyInfo ManagerActions = InputManager?.PropertyType.GetProperty("actions", BindingFlags.Instance | BindingFlags.Public);
        static readonly Type UiEventProvider = Type.GetType("UnityEngine.InputForUI.EventProvider, UnityEngine.InputForUIModule");
        static readonly MethodInfo SetUiEventProviderEnabled = UiEventProvider?.GetMethod("SetEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        static readonly FieldInfo UiEventProviderEnabled = UiEventProvider?.GetField("m_IsEnabled", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly Type NativeRuntime = typeof(InputSystem).Assembly.GetType("UnityEngine.InputSystem.LowLevel.NativeInputRuntime");
        static readonly PropertyInfo NativeRuntimeInstance = NativeRuntime?.GetProperty("instance", BindingFlags.Static | BindingFlags.Public);
        static readonly PropertyInfo NativeBeforeUpdate = NativeRuntime?.GetProperty("onBeforeUpdate", BindingFlags.Instance | BindingFlags.Public);

        public static IEnumerator Setup(InputTestFixture fixture)
        {
            bool replacesRuntime = s_activeFixture == null;
            bool restartApplication = false;
            if (replacesRuntime)
            {
                s_originalActions = InputSystem.actions;
                if (InputManager == null || ManagerActions == null)
                {
                    throw new InvalidOperationException("无法恢复项目输入资产，请检查当前 Input System API");
                }
            }

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
                    // 先于包内的退出回调登记，确保异常中止也走同一保护恢复路径。
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
                    try
                    {
                        fixture.Setup();
                        s_activeFixture = fixture;
                        if (NativeRuntimeInstance == null || NativeBeforeUpdate == null)
                        {
                            throw new InvalidOperationException("无法隔离原生输入的 beforeUpdate，请检查当前 Input System API");
                        }
                        // 包内夹具只替换原生 onUpdate；模拟运行时已自行执行 beforeUpdate，不能再调用原设备的检查。
                        NativeBeforeUpdate.SetValue(NativeRuntimeInstance.GetValue(null), null);
                    }
                    catch
                    {
                        ReleaseRuntime();
                        throw;
                    }
                    return;
                }

                RemoveTestDevices();
            }, resetProjectActions: replacesRuntime);

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

        public static void ReleaseRuntime()
        {
            InputTestFixture fixture = s_activeFixture;
            s_activeFixture = null;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            if (fixture != null)
            {
                RunWithUiInputDisabled(() =>
                {
                    fixture.TearDown();
                    // 包内 Reset 会清空被保存的 manager.actions；Restore 没有恢复这一引用。
                    ManagerActions.SetValue(InputManager.GetValue(null), s_originalActions);
                    s_originalActions = null;
                }, resetProjectActions: true);
            }
        }

#if UNITY_EDITOR
        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) { ReleaseRuntime(); }
        }
#endif

        static void RemoveTestDevices()
        {
            for (int i = InputSystem.devices.Count - 1; i >= 0; i--)
            {
                InputSystem.RemoveDevice(InputSystem.devices[i]);
            }
        }

        static void RunWithUiInputDisabled(Action action, bool resetProjectActions = false)
        {
            // Unity 6 的原生 UI 输入接收器也订阅项目 Actions；不能跨输入运行时保留这些监听。
            if (SetUiEventProviderEnabled == null || UiEventProviderEnabled == null)
            {
                throw new InvalidOperationException("无法隔离 Unity 原生 UI 输入接收器，请检查当前 Editor 的 EventProvider API");
            }
            bool uiProviderEnabled = (bool)UiEventProviderEnabled.GetValue(null);
            SetUiEventProviderEnabled.Invoke(null, new object[] { false });
            InputSystemUIInputModule[] modules =
                UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(
                    FindObjectsInactive.Include);
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
                if (resetProjectActions)
                {
                    // Disable 不清除已排队的初始状态检查；先在所属运行时消化，避免原生 beforeUpdate 读取旧设备缓冲。
                    InputSystem.Update();
                    // 两个运行时共享同一项目资产；释放设备绑定缓存，下一次 Enable 才能绑定新运行时。
                    if (InputSystem.actions != null)
                    {
                        foreach (InputActionMap map in InputSystem.actions.actionMaps) { map.Dispose(); }
                    }
                }
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
                SetUiEventProviderEnabled.Invoke(null, new object[] { uiProviderEnabled });
            }
        }
    }
}
