using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFlare.Editor
{
    public static class GameplayPhysicsConfigurationValidator
    {
        const string MainScenePath = "Assets/Scenes/Main.unity";

        static readonly string[] PlayerPrefabPaths =
        {
            "Assets/Prefabs/Combat/Player.prefab",
        };

        static readonly string[] MonsterPrefabPaths =
        {
            "Assets/Prefabs/Combat/Monster_Basic.prefab",
            "Assets/Prefabs/Combat/Monster_Swift.prefab",
            "Assets/Prefabs/Combat/Monster_Heavy.prefab",
        };

        public static List<string> Validate()
        {
            List<string> issues = new List<string>();
            int playerLayer = RequireLayer(GameplayPhysicsLayers.PlayerActor, issues);
            int monsterLayer = RequireLayer(GameplayPhysicsLayers.MonsterActor, issues);
            int worldLayer = RequireLayer(GameplayPhysicsLayers.WorldObstacle, issues);

            if (playerLayer < 0 || monsterLayer < 0 || worldLayer < 0)
            {
                return issues;
            }

            ValidatePair(playerLayer, monsterLayer, true, issues);
            ValidatePair(monsterLayer, monsterLayer, true, issues);
            ValidatePair(playerLayer, playerLayer, true, issues);
            ValidatePair(playerLayer, worldLayer, false, issues);
            ValidatePair(monsterLayer, worldLayer, false, issues);
            ValidatePair(playerLayer, 0, false, issues);
            ValidatePair(monsterLayer, 0, false, issues);
            ValidatePrefabLayers(PlayerPrefabPaths, playerLayer, issues);
            ValidatePrefabLayers(MonsterPrefabPaths, monsterLayer, issues);
            ValidateWorldBounds(worldLayer, issues);
            return issues;
        }

        static int RequireLayer(string layerName, List<string> issues)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer < 0)
            {
                issues.Add($"缺少物理层：{layerName}");
            }

            return layer;
        }

        static void ValidatePair(int firstLayer, int secondLayer, bool shouldIgnore, List<string> issues)
        {
            bool ignored = Physics2D.GetIgnoreLayerCollision(firstLayer, secondLayer);

            if (ignored != shouldIgnore)
            {
                string firstName = LayerMask.LayerToName(firstLayer);
                string secondName = LayerMask.LayerToName(secondLayer);
                issues.Add($"物理层关系错误：{firstName} ↔ {secondName} 应为 {(shouldIgnore ? "忽略" : "接触")}");
            }
        }

        static void ValidatePrefabLayers(
            IReadOnlyList<string> prefabPaths,
            int expectedLayer,
            List<string> issues)
        {
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    issues.Add($"缺少角色 Prefab：{path}");
                    continue;
                }

                Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);

                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    GameObject target = transforms[transformIndex].gameObject;

                    if (target.layer != expectedLayer)
                    {
                        issues.Add($"Prefab Layer 错误：{path}/{GetRelativePath(prefab.transform, target.transform)}");
                    }
                }
            }
        }

        static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return root.name;
            }

            List<string> parts = new List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return $"{root.name}/{string.Join("/", parts)}";
        }

        static void ValidateWorldBounds(int expectedLayer, List<string> issues)
        {
            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool closeAfterValidation = !scene.IsValid() || !scene.isLoaded;

            try
            {
                if (closeAfterValidation)
                {
                    scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
                }

                GameObject worldBounds = FindRoot(scene, "WorldBounds");

                if (worldBounds == null)
                {
                    issues.Add($"{MainScenePath} 缺少 WorldBounds");
                    return;
                }

                if (worldBounds.layer != expectedLayer)
                {
                    issues.Add($"{MainScenePath}/WorldBounds 未使用 {GameplayPhysicsLayers.WorldObstacle} Layer");
                }

                EdgeCollider2D collider = worldBounds.GetComponent<EdgeCollider2D>();

                if (collider == null || collider.isTrigger)
                {
                    issues.Add($"{MainScenePath}/WorldBounds 必须使用非 Trigger EdgeCollider2D");
                }
            }
            catch (Exception exception)
            {
                issues.Add($"无法验证 {MainScenePath}/WorldBounds：{exception.Message}");
            }
            finally
            {
                if (closeAfterValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
