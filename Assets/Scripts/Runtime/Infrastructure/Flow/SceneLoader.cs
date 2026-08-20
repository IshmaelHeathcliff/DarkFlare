using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DarkFlare
{
    public sealed class SceneLoaderResult
    {
        SceneLoaderResult(
            SceneFlowErrorCode errorCode,
            Scene scene,
            Exception exception)
        {
            ErrorCode = errorCode;
            Scene = scene;
            Exception = exception;
        }

        public SceneFlowErrorCode ErrorCode { get; }

        public Scene Scene { get; }

        public Exception Exception { get; }

        public bool Succeeded => ErrorCode == SceneFlowErrorCode.None;

        public static SceneLoaderResult Success(Scene scene)
        {
            return new SceneLoaderResult(SceneFlowErrorCode.None, scene, null);
        }

        public static SceneLoaderResult Failure(
            SceneFlowErrorCode errorCode,
            Scene scene = default,
            Exception exception = null)
        {
            if (errorCode == SceneFlowErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCode));
            }

            return new SceneLoaderResult(errorCode, scene, exception);
        }
    }

    public interface ISceneLoader
    {
        bool IsSceneInBuild(string scenePath);

        bool TryGetLoadedScene(string scenePath, out Scene scene);

        UniTask<SceneLoaderResult> LoadAsync(
            SceneId sceneId,
            string scenePath,
            IProgress<float> progress,
            CancellationToken cancellationToken);

        SceneLoaderResult SetActive(Scene scene);

        UniTask<SceneLoaderResult> UnloadAsync(
            Scene scene,
            CancellationToken cancellationToken);
    }

}
