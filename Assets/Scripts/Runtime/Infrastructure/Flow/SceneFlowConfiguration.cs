using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Serializable]
    public sealed class SceneFlowRegistration
    {
        [SerializeField]
        [LabelText("场景标识")]
        string _sceneId;

        [SerializeField]
        [LabelText("Build 场景路径")]
        string _scenePath;

        public string SceneIdValue => _sceneId ?? string.Empty;

        public string ScenePath => _scenePath ?? string.Empty;

        public SceneFlowRegistration(string sceneId, string scenePath)
        {
            _sceneId = sceneId;
            _scenePath = scenePath;
        }
    }

    [CreateAssetMenu(
        fileName = "SceneFlowConfiguration",
        menuName = "DarkFlare/基础设施/场景流配置")]
    public sealed class SceneFlowConfiguration : ScriptableObject
    {
        [SerializeField]
        [LabelText("场景注册")]
        List<SceneFlowRegistration> _scenes = new List<SceneFlowRegistration>();

        [SerializeField]
        [LabelText("单次事务超时秒数")]
        [Min(1f)]
        float _transitionTimeoutSeconds = 30f;

        public IReadOnlyList<SceneFlowRegistration> Scenes => _scenes;

        public TimeSpan TransitionTimeout => TimeSpan.FromSeconds(
            Mathf.Max(1f, _transitionTimeoutSeconds));

        public bool TryResolve(SceneId sceneId, out string scenePath)
        {
            for (int i = 0; i < _scenes.Count; i++)
            {
                SceneFlowRegistration registration = _scenes[i];

                if (registration != null
                    && string.Equals(
                        registration.SceneIdValue,
                        sceneId.Value,
                        StringComparison.Ordinal))
                {
                    scenePath = registration.ScenePath;
                    return !string.IsNullOrWhiteSpace(scenePath);
                }
            }

            scenePath = string.Empty;
            return false;
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            List<string> errors = new List<string>();
            HashSet<SceneId> ids = new HashSet<SceneId>();
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _scenes.Count; i++)
            {
                SceneFlowRegistration registration = _scenes[i];

                if (registration == null)
                {
                    errors.Add($"场景注册 {i} 为空");
                    continue;
                }

                if (!SceneId.TryCreate(registration.SceneIdValue, out SceneId sceneId))
                {
                    errors.Add($"场景注册 {i} 的 SceneId 无效");
                }
                else if (!ids.Add(sceneId))
                {
                    errors.Add($"SceneId 重复：{sceneId}");
                }

                if (string.IsNullOrWhiteSpace(registration.ScenePath))
                {
                    errors.Add($"场景 {registration.SceneIdValue} 的路径为空");
                }
                else if (!paths.Add(registration.ScenePath))
                {
                    errors.Add($"场景路径重复：{registration.ScenePath}");
                }
            }

            if (!ids.Contains(SceneId.Bootstrap))
            {
                errors.Add("缺少 bootstrap 场景注册");
            }

            if (!ids.Contains(SceneId.Main))
            {
                errors.Add("缺少 main 场景注册");
            }

            return errors;
        }
    }
}
