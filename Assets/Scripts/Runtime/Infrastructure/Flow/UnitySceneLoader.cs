using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public bool IsSceneInBuild(string scenePath)
        {
            return !string.IsNullOrWhiteSpace(scenePath)
                && Application.CanStreamedLevelBeLoaded(scenePath);
        }

        public bool TryGetLoadedScene(string scenePath, out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(scenePath);
            return scene.IsValid() && scene.isLoaded;
        }

        public async UniTask<SceneLoaderResult> LoadAsync(
            SceneId sceneId,
            string scenePath,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (TryGetLoadedScene(scenePath, out Scene loadedScene))
            {
                progress?.Report(1f);
                return SceneLoaderResult.Success(loadedScene);
            }

            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Additive);

                if (operation == null)
                {
                    return SceneLoaderResult.Failure(SceneFlowErrorCode.LoadFailed);
                }

                while (!operation.isDone)
                {
                    progress?.Report(Mathf.Clamp01(operation.progress / 0.9f));
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                progress?.Report(1f);
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                return scene.IsValid() && scene.isLoaded
                    ? SceneLoaderResult.Success(scene)
                    : SceneLoaderResult.Failure(SceneFlowErrorCode.LoadFailed, scene);
            }
            catch (Exception exception)
            {
                return SceneLoaderResult.Failure(
                    SceneFlowErrorCode.LoadFailed,
                    exception: exception);
            }
        }

        public SceneLoaderResult SetActive(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return SceneLoaderResult.Failure(SceneFlowErrorCode.ActivationFailed, scene);
            }

            if (SceneManager.GetActiveScene() == scene)
            {
                return SceneLoaderResult.Success(scene);
            }

            try
            {
                return SceneManager.SetActiveScene(scene)
                    ? SceneLoaderResult.Success(scene)
                    : SceneLoaderResult.Failure(SceneFlowErrorCode.ActivationFailed, scene);
            }
            catch (Exception exception)
            {
                return SceneLoaderResult.Failure(
                    SceneFlowErrorCode.ActivationFailed,
                    scene,
                    exception);
            }
        }

        public async UniTask<SceneLoaderResult> UnloadAsync(
            Scene scene,
            CancellationToken cancellationToken)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return SceneLoaderResult.Success(scene);
            }

            try
            {
                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);

                if (operation == null)
                {
                    return SceneLoaderResult.Failure(SceneFlowErrorCode.UnloadFailed, scene);
                }

                while (!operation.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                return SceneLoaderResult.Success(scene);
            }
            catch (Exception exception)
            {
                return SceneLoaderResult.Failure(
                    SceneFlowErrorCode.UnloadFailed,
                    scene,
                    exception);
            }
        }
    }
}
