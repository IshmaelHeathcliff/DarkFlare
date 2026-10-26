using System;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
#endif

[assembly: PrebuildSetup(typeof(DarkFlare.Tests.PlayModeDataRunSetup))]
[assembly: PostBuildCleanup(typeof(DarkFlare.Tests.PlayModeDataRunSetup))]

namespace DarkFlare.Tests
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public sealed class PlayModeDataRunSetup : IPrebuildSetup, IPostBuildCleanup
    {
#if UNITY_EDITOR
        const string RunKey = "DarkFlare.Tests.PlayMode.DataRun";
        static readonly RunCallbacks Callbacks = new RunCallbacks();

        static PlayModeDataRunSetup()
        {
            TestRunnerApi.RegisterTestCallback(Callbacks);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static bool IsActive => !string.IsNullOrEmpty(SessionState.GetString(RunKey, string.Empty));
#else
        internal static bool IsActive => true;
#endif

        public void Setup()
        {
#if UNITY_EDITOR
            SessionState.SetString(RunKey, Guid.NewGuid().ToString("N"));
#endif
        }

        public void Cleanup()
        {
            EndRun();
        }

        static void EndRun()
        {
#if UNITY_EDITOR
            SessionState.EraseString(RunKey);
#endif
            Alpha027PlayModeDataEnvironment.ReleaseAndDelete();
        }

#if UNITY_EDITOR
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EndRun();
            }
        }

        sealed class RunCallbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result) { EndRun(); }
            public void OnError(string message) { EndRun(); }
        }
#endif
    }
}
