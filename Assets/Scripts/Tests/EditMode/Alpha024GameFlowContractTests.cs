using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class Alpha024GameFlowContractTests
    {
        [Test]
        public void GameFlowStates_UseTheFrozenInteractiveTransactionAndTerminalRoles()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(GameFlowState.Boot),
                    nameof(GameFlowState.FrontEnd),
                    nameof(GameFlowState.Loading),
                    nameof(GameFlowState.InGame),
                    nameof(GameFlowState.Paused),
                    nameof(GameFlowState.Recovering),
                    nameof(GameFlowState.FatalError),
                },
                Enum.GetNames(typeof(GameFlowState)));

            Assert.IsTrue(GameFlowTransitionRules.IsInteractiveStable(GameFlowState.FrontEnd));
            Assert.IsTrue(GameFlowTransitionRules.IsInteractiveStable(GameFlowState.InGame));
            Assert.IsTrue(GameFlowTransitionRules.IsInteractiveStable(GameFlowState.Paused));
            Assert.IsTrue(GameFlowTransitionRules.IsTransactionState(GameFlowState.Loading));
            Assert.IsTrue(GameFlowTransitionRules.IsTransactionState(GameFlowState.Recovering));
            Assert.IsTrue(GameFlowTransitionRules.IsTerminal(GameFlowState.FatalError));
            Assert.IsFalse(GameFlowTransitionRules.IsInteractiveStable(GameFlowState.Boot));
            Assert.IsFalse(GameFlowTransitionRules.IsTransactionState(GameFlowState.Boot));
        }

        [Test]
        public void GameFlowTransitionRules_AllowExactlyTheFrozenGraph()
        {
            int allowedCount = 0;

            foreach (GameFlowState fromState in Enum.GetValues(typeof(GameFlowState)))
            {
                foreach (GameFlowState toState in Enum.GetValues(typeof(GameFlowState)))
                {
                    bool expected = IsExpectedTransition(fromState, toState);
                    bool actual = GameFlowTransitionRules.CanTransition(fromState, toState);

                    Assert.AreEqual(
                        expected,
                        actual,
                        $"未冻结的状态转换：{fromState} → {toState}");

                    if (actual)
                    {
                        allowedCount++;
                    }
                }
            }

            Assert.AreEqual(15, allowedCount);
        }

        [Test]
        public void SceneId_IsStableCaseSensitiveSnakeCaseValue()
        {
            Assert.AreEqual("bootstrap", SceneId.Bootstrap.Value);
            Assert.AreEqual("main", SceneId.Main.Value);
            Assert.AreEqual(new SceneId("main"), SceneId.Main);
            Assert.AreNotEqual(SceneId.Bootstrap, SceneId.Main);
            Assert.IsFalse(default(SceneId).IsValid);
            Assert.IsTrue(SceneId.TryCreate("test_scene_2", out SceneId sceneId));
            Assert.AreEqual("test_scene_2", sceneId.ToString());

            string[] invalidValues =
            {
                null,
                string.Empty,
                "Main",
                "main-scene",
                "2main",
                "main__scene",
                "main_",
                " main",
            };

            for (int i = 0; i < invalidValues.Length; i++)
            {
                Assert.IsFalse(
                    SceneId.TryCreate(invalidValues[i], out SceneId _),
                    invalidValues[i] ?? "<null>");
            }
        }

        [Test]
        public void SceneFlowRequests_MapIntentToSemanticSceneWithoutPaths()
        {
            SceneFlowRequest enterFrontEnd = SceneFlowRequest.EnterFrontEnd();
            SceneFlowRequest newGame = SceneFlowRequest.StartGame(GameStartIntent.NewGame);
            SceneFlowRequest continueGame = SceneFlowRequest.StartGame(GameStartIntent.Continue);
            SceneFlowRequest returnToFrontEnd = SceneFlowRequest.ReturnToFrontEnd();

            AssertRequest(
                enterFrontEnd,
                SceneFlowOperation.EnterFrontEnd,
                GameStartIntent.None,
                SceneId.Bootstrap);
            AssertRequest(
                newGame,
                SceneFlowOperation.StartGame,
                GameStartIntent.NewGame,
                SceneId.Main);
            AssertRequest(
                continueGame,
                SceneFlowOperation.StartGame,
                GameStartIntent.Continue,
                SceneId.Main);
            AssertRequest(
                returnToFrontEnd,
                SceneFlowOperation.ReturnToFrontEnd,
                GameStartIntent.None,
                SceneId.Bootstrap);
            Assert.IsFalse(default(SceneFlowRequest).IsValid);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SceneFlowRequest.StartGame(GameStartIntent.None));
        }

        [Test]
        public void SceneFlowProgress_DistinguishesMeasuredAndIndeterminatePhases()
        {
            SceneFlowProgress indeterminate = SceneFlowProgress.Indeterminate(
                SceneFlowPhase.Preparing,
                SceneId.Main,
                true);
            SceneFlowProgress measured = SceneFlowProgress.Measured(
                SceneFlowPhase.LoadingScene,
                SceneId.Main,
                0.65f,
                true);

            Assert.IsFalse(indeterminate.HasMeasuredProgress);
            Assert.IsNull(indeterminate.Progress01);
            Assert.IsTrue(indeterminate.CanCancel);
            Assert.IsTrue(measured.HasMeasuredProgress);
            Assert.AreEqual(0.65f, measured.Progress01.Value, 0.0001f);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SceneFlowProgress.Measured(
                    SceneFlowPhase.LoadingScene,
                    SceneId.Main,
                    -0.01f,
                    true));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SceneFlowProgress.Measured(
                    SceneFlowPhase.LoadingScene,
                    SceneId.Main,
                    1.01f,
                    true));
            Assert.Throws<ArgumentException>(
                () => SceneFlowProgress.Indeterminate(
                    SceneFlowPhase.Preparing,
                    default,
                    false));
        }

        [Test]
        public void SceneFlowResult_SeparatesSuccessFailurePlayerMessageAndInternalException()
        {
            SceneFlowResult success = SceneFlowResult.Success(
                SceneFlowOperation.StartGame,
                GameFlowState.FrontEnd,
                GameFlowState.InGame,
                SceneId.Main);
            InvalidOperationException expectedException = new InvalidOperationException(
                "expected internal failure");
            LocalizedMessage message = new LocalizedMessage(
                "system",
                "scene_flow.load_failed");
            SceneFlowResult failure = SceneFlowResult.Failure(
                SceneFlowOperation.StartGame,
                SceneFlowErrorCode.LoadFailed,
                GameFlowState.FrontEnd,
                GameFlowState.InGame,
                SceneFlowPhase.LoadingScene,
                SceneId.Main,
                SceneFlowRecoveryAction.Retry | SceneFlowRecoveryAction.ReturnToFrontEnd,
                message,
                expectedException);

            Assert.IsTrue(success.Succeeded);
            Assert.AreEqual(SceneFlowPhase.Completed, success.Phase);
            Assert.AreEqual(SceneFlowRecoveryAction.None, success.RecoveryActions);
            Assert.IsFalse(success.CanRetry);
            Assert.IsFalse(failure.Succeeded);
            Assert.IsTrue(failure.CanRetry);
            Assert.IsTrue(failure.HasRecoveryAction(SceneFlowRecoveryAction.Retry));
            Assert.IsTrue(failure.HasRecoveryAction(SceneFlowRecoveryAction.ReturnToFrontEnd));
            Assert.AreEqual("system", failure.PlayerMessage.TableName);
            Assert.AreEqual("scene_flow.load_failed", failure.PlayerMessage.EntryKey);
            Assert.AreSame(expectedException, failure.Exception);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SceneFlowResult.Failure(
                    SceneFlowOperation.StartGame,
                    SceneFlowErrorCode.None,
                    GameFlowState.FrontEnd,
                    GameFlowState.InGame,
                    SceneFlowPhase.LoadingScene,
                    SceneId.Main,
                    SceneFlowRecoveryAction.None,
                    default));
        }

        [Test]
        public void SceneFlowEnums_ContainTheFrozenPhasesErrorsAndRecoveryActions()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(SceneFlowOperation.None),
                    nameof(SceneFlowOperation.EnterFrontEnd),
                    nameof(SceneFlowOperation.StartGame),
                    nameof(SceneFlowOperation.ReturnToFrontEnd),
                },
                Enum.GetNames(typeof(SceneFlowOperation)));
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(GameStartIntent.None),
                    nameof(GameStartIntent.NewGame),
                    nameof(GameStartIntent.Continue),
                },
                Enum.GetNames(typeof(GameStartIntent)));
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(SceneFlowPhase.None),
                    nameof(SceneFlowPhase.Preparing),
                    nameof(SceneFlowPhase.PreparingSave),
                    nameof(SceneFlowPhase.LoadingScene),
                    nameof(SceneFlowPhase.ActivatingScene),
                    nameof(SceneFlowPhase.StartingSession),
                    nameof(SceneFlowPhase.SavingBeforeExit),
                    nameof(SceneFlowPhase.StoppingSession),
                    nameof(SceneFlowPhase.UnloadingScene),
                    nameof(SceneFlowPhase.Recovering),
                    nameof(SceneFlowPhase.Completed),
                },
                Enum.GetNames(typeof(SceneFlowPhase)));
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(SceneFlowErrorCode.None),
                    nameof(SceneFlowErrorCode.InvalidState),
                    nameof(SceneFlowErrorCode.Cancelled),
                    nameof(SceneFlowErrorCode.Superseded),
                    nameof(SceneFlowErrorCode.ConfigurationInvalid),
                    nameof(SceneFlowErrorCode.SceneNotInBuild),
                    nameof(SceneFlowErrorCode.LoadFailed),
                    nameof(SceneFlowErrorCode.ActivationFailed),
                    nameof(SceneFlowErrorCode.EntryMissing),
                    nameof(SceneFlowErrorCode.EntryDuplicate),
                    nameof(SceneFlowErrorCode.SavePrepareFailed),
                    nameof(SceneFlowErrorCode.SaveBeforeExitFailed),
                    nameof(SceneFlowErrorCode.SessionStartFailed),
                    nameof(SceneFlowErrorCode.SessionStopFailed),
                    nameof(SceneFlowErrorCode.UnloadFailed),
                    nameof(SceneFlowErrorCode.Timeout),
                    nameof(SceneFlowErrorCode.ApplicationUnavailable),
                    nameof(SceneFlowErrorCode.FatalCleanupFailed),
                },
                Enum.GetNames(typeof(SceneFlowErrorCode)));
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(SceneFlowRecoveryAction.None),
                    nameof(SceneFlowRecoveryAction.Retry),
                    nameof(SceneFlowRecoveryAction.CancelTransition),
                    nameof(SceneFlowRecoveryAction.ReturnToFrontEnd),
                    nameof(SceneFlowRecoveryAction.RetrySafeBoot),
                    nameof(SceneFlowRecoveryAction.Quit),
                },
                Enum.GetNames(typeof(SceneFlowRecoveryAction)));
        }

        [Test]
        public void Alpha024Topology_UsesBootstrapAndSingleCrossSessionEntryPoint()
        {
            List<string> enabledScenes = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    enabledScenes.Add(scenes[i].path);
                }
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "Assets/Scenes/Bootstrap.unity",
                    "Assets/Scenes/Main.unity",
                },
                enabledScenes);
            Assert.IsNotNull(FindMethod(typeof(ApplicationHost), "BeginSceneSessionInitialization"));
            Assert.IsNotNull(FindMethod(typeof(SessionSaveFacade), "SaveAutoAsync"));
            Assert.IsNull(FindMethod(typeof(SessionSaveFacade), "ContinueAutoAsync"));
            Assert.IsNull(FindMethod(typeof(SessionSaveFacade), "StartNewGameAsync"));
            Assert.IsNull(FindMethod(typeof(CombatPrototypeBootstrap), "Start"));
            Assert.IsNotNull(FindMethod(typeof(GameplayPauseSystem), "SetPaused"));
            Assert.IsNotNull(typeof(GameMenuController).GetProperty("IsSaveOperationBusy"));
        }

        [Test]
        public void Alpha024Assets_HaveFrozenSceneUiAndConfigurationTopology()
        {
            SceneFlowConfiguration configuration =
                AssetDatabase.LoadAssetAtPath<SceneFlowConfiguration>(
                    "Assets/Settings/Scenes/SceneFlowConfiguration.asset");
            Assert.IsNotNull(configuration);
            Assert.IsEmpty(configuration.ValidateConfiguration());
            Assert.IsTrue(configuration.TryResolve(SceneId.Bootstrap, out string bootstrapPath));
            Assert.AreEqual("Assets/Scenes/Bootstrap.unity", bootstrapPath);
            Assert.IsTrue(configuration.TryResolve(SceneId.Main, out string mainPath));
            Assert.AreEqual("Assets/Scenes/Main.unity", mainPath);

            VisualTreeAsset shellTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/ApplicationShell.uxml");
            Assert.IsNotNull(shellTree);
            TemplateContainer shell = shellTree.CloneTree();
            Assert.IsNotNull(shell.Q<VisualElement>("application-front-end"));
            Assert.IsNotNull(shell.Q<VisualElement>("application-toast"));
            Assert.IsNotNull(shell.Q<VisualElement>("application-modal"));
            Assert.IsNotNull(shell.Q<VisualElement>("application-busy"));
            Assert.IsNotNull(shell.Q<VisualElement>("application-fatal"));
            Assert.IsNotNull(shell.Q<Button>("front-end-new-game"));
            Assert.IsNotNull(shell.Q<Button>("front-end-continue"));
            Assert.IsNotNull(shell.Q<DropdownField>("front-end-language"));
            Assert.IsNotNull(shell.Q<Button>("front-end-quit"));

            VisualTreeAsset gameTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/GameRoot.uxml");
            Assert.IsNotNull(gameTree);
            TemplateContainer gameRoot = gameTree.CloneTree();
            Assert.IsNotNull(gameRoot.Q<Button>("game-menu-save"));
            Assert.IsNotNull(gameRoot.Q<Button>("game-menu-return-front-end"));
            Assert.IsNull(gameRoot.Q<Button>("game-menu-continue"));
            Assert.IsNull(gameRoot.Q<Button>("game-menu-new-game"));
            Assert.IsNull(gameRoot.Q<DropdownField>("game-menu-language-dropdown"));

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene bootstrapScene = EditorSceneManager.OpenScene(
                    bootstrapPath,
                    OpenSceneMode.Single);
                Assert.AreEqual(1, FindComponentsInScene<ApplicationShellBootstrap>(bootstrapScene).Count);
                Assert.AreEqual(1, FindComponentsInScene<EventSystem>(bootstrapScene).Count);
                Assert.AreEqual(0, FindComponentsInScene<CombatPrototypeBootstrap>(bootstrapScene).Count);
                UIDocument document = FindComponentsInScene<UIDocument>(bootstrapScene)[0];
                Assert.AreSame(shellTree, document.visualTreeAsset);
                Assert.IsNotNull(document.panelSettings);

                Scene mainScene = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Additive);
                Assert.AreEqual(0, FindComponentsInScene<EventSystem>(mainScene).Count);
                Assert.AreEqual(1, FindComponentsInScene<CombatPrototypeBootstrap>(mainScene).Count);
            }
            finally
            {
                if (Array.Exists(setup, scene => scene.isLoaded && scene.isActive))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        static void AssertRequest(
            SceneFlowRequest request,
            SceneFlowOperation operation,
            GameStartIntent startIntent,
            SceneId sceneId)
        {
            Assert.IsTrue(request.IsValid);
            Assert.AreEqual(operation, request.Operation);
            Assert.AreEqual(startIntent, request.StartIntent);
            Assert.AreEqual(sceneId, request.TargetScene);
        }

        static bool IsExpectedTransition(GameFlowState fromState, GameFlowState toState)
        {
            switch (fromState)
            {
                case GameFlowState.Boot:
                    return toState == GameFlowState.FrontEnd
                        || toState == GameFlowState.FatalError;
                case GameFlowState.FrontEnd:
                    return toState == GameFlowState.Loading;
                case GameFlowState.Loading:
                    return toState == GameFlowState.InGame
                        || toState == GameFlowState.FrontEnd
                        || toState == GameFlowState.Recovering
                        || toState == GameFlowState.FatalError;
                case GameFlowState.InGame:
                    return toState == GameFlowState.Paused
                        || toState == GameFlowState.Loading;
                case GameFlowState.Paused:
                    return toState == GameFlowState.InGame
                        || toState == GameFlowState.Loading;
                case GameFlowState.Recovering:
                    return toState == GameFlowState.FrontEnd
                        || toState == GameFlowState.InGame
                        || toState == GameFlowState.Paused
                        || toState == GameFlowState.FatalError;
                default:
                    return false;
            }
        }

        static MethodInfo FindMethod(Type type, string methodName)
        {
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic);

            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == methodName)
                {
                    return methods[i];
                }
            }

            return null;
        }

        static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> components = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                components.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            return components;
        }
    }
}
