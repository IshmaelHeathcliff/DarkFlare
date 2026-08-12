using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DarkFlare.Editor
{
    public enum Alpha017MigrationPhase
    {
        Preflight,
        Apply,
        Validate
    }

    public enum Alpha017WorldTargetKind
    {
        Prefab,
        SceneObject
    }

    public sealed class Alpha017EffectFamilyContract
    {
        public string Id { get; }

        public string Directory { get; }

        public string FilePrefix { get; }

        public int FrameCount { get; }

        public int FramesPerSecond { get; }

        public bool Loop { get; }

        public Alpha017EffectFamilyContract(
            string id,
            string directory,
            string filePrefix,
            int frameCount,
            int framesPerSecond,
            bool loop)
        {
            Id = id;
            Directory = directory;
            FilePrefix = filePrefix;
            FrameCount = frameCount;
            FramesPerSecond = framesPerSecond;
            Loop = loop;
        }

        public string GetFramePath(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= FrameCount)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            }

            return $"{Directory}/{FilePrefix}_{frameIndex:00}.png";
        }
    }

    public sealed class Alpha017WorldTargetSnapshot
    {
        public Alpha017WorldTargetKind Kind { get; }

        public string Path { get; }

        public bool RequiresSortingGroup { get; }

        public bool Exists { get; }

        public int RendererCount { get; }

        public int DefaultRendererCount { get; }

        public int SortingGroupCount { get; }

        public Alpha017WorldTargetSnapshot(
            Alpha017WorldTargetKind kind,
            string path,
            bool requiresSortingGroup,
            bool exists,
            int rendererCount,
            int defaultRendererCount,
            int sortingGroupCount)
        {
            Kind = kind;
            Path = path;
            RequiresSortingGroup = requiresSortingGroup;
            Exists = exists;
            RendererCount = rendererCount;
            DefaultRendererCount = defaultRendererCount;
            SortingGroupCount = sortingGroupCount;
        }
    }

    public sealed class Alpha017ProjectileBaselineSnapshot
    {
        public bool Exists { get; }

        public int CanvasWidth { get; }

        public int CanvasHeight { get; }

        public float PixelsPerUnit { get; }

        public SpriteImportMode SpriteMode { get; }

        public SpriteMeshType MeshType { get; }

        public FilterMode FilterMode { get; }

        public TextureImporterCompression Compression { get; }

        public bool MipmapEnabled { get; }

        public TextureWrapMode WrapMode { get; }

        public Vector2 Pivot { get; }

        public Vector3 PrefabRootScale { get; }

        public Vector2 VisibleWorldSize { get; }

        public Alpha017ProjectileBaselineSnapshot(
            bool exists,
            int canvasWidth,
            int canvasHeight,
            float pixelsPerUnit,
            SpriteImportMode spriteMode,
            SpriteMeshType meshType,
            FilterMode filterMode,
            TextureImporterCompression compression,
            bool mipmapEnabled,
            TextureWrapMode wrapMode,
            Vector2 pivot,
            Vector3 prefabRootScale,
            Vector2 visibleWorldSize)
        {
            Exists = exists;
            CanvasWidth = canvasWidth;
            CanvasHeight = canvasHeight;
            PixelsPerUnit = pixelsPerUnit;
            SpriteMode = spriteMode;
            MeshType = meshType;
            FilterMode = filterMode;
            Compression = compression;
            MipmapEnabled = mipmapEnabled;
            WrapMode = wrapMode;
            Pivot = pivot;
            PrefabRootScale = prefabRootScale;
            VisibleWorldSize = visibleWorldSize;
        }
    }

    public sealed class Alpha017MigrationIssue
    {
        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public Alpha017MigrationIssue(string code, string path, string message)
        {
            Code = code;
            Path = path;
            Message = message;
        }
    }

    public sealed class Alpha017VisualBaselineReport
    {
        public Alpha017MigrationPhase Phase { get; }

        public IReadOnlyList<string> SortingLayers { get; }

        public IReadOnlyList<Alpha017WorldTargetSnapshot> WorldTargets { get; }

        public IReadOnlyList<string> MissingEffectFrames { get; }

        public IReadOnlyList<Alpha017MigrationIssue> Issues { get; }

        public Alpha017ProjectileBaselineSnapshot Projectile { get; }

        public Alpha017VisualBaselineReport(
            Alpha017MigrationPhase phase,
            IReadOnlyList<string> sortingLayers,
            IReadOnlyList<Alpha017WorldTargetSnapshot> worldTargets,
            IReadOnlyList<string> missingEffectFrames,
            IReadOnlyList<Alpha017MigrationIssue> issues,
            Alpha017ProjectileBaselineSnapshot projectile)
        {
            Phase = phase;
            SortingLayers = sortingLayers;
            WorldTargets = worldTargets;
            MissingEffectFrames = missingEffectFrames;
            Issues = issues;
            Projectile = projectile;
        }
    }

    public static class Alpha017VisualMigrationPreflight
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string ProjectileSpritePath = "Assets/Art/Sprites/Effects/projectile_arcane.png";
        public const string ProjectilePrefabPath = "Assets/Prefabs/Combat/Projectile_Default.prefab";
        public const int EffectCanvasSize = 96;
        public const int EffectPixelsPerUnit = 64;
        public const int ProjectileVisibleWidthPixels = 56;
        public const int ProjectileVisibleHeightPixels = 22;

        static readonly string[] ExpectedSortingLayerNamesData =
        {
            "Ground",
            "WorldObject",
            "WorldEffect",
            "WorldInfo",
        };

        static readonly Alpha017EffectFamilyContract[] EffectFamiliesData =
        {
            new Alpha017EffectFamilyContract(
                "projectile-arcane-flight",
                "Assets/Art/Sprites/Effects/Projectile/Arcane/Flight",
                "effect_projectile_arcane_flight_e",
                6,
                12,
                true),
            new Alpha017EffectFamilyContract(
                "projectile-arcane-impact",
                "Assets/Art/Sprites/Effects/Projectile/Arcane/Impact",
                "effect_projectile_arcane_impact",
                6,
                24,
                false),
            new Alpha017EffectFamilyContract(
                "actor-hit-default",
                "Assets/Art/Sprites/Effects/Hit",
                "effect_hit_default",
                5,
                20,
                false),
            new Alpha017EffectFamilyContract(
                "actor-hit-critical",
                "Assets/Art/Sprites/Effects/Hit",
                "effect_hit_critical",
                6,
                24,
                false),
        };

        static readonly TargetDefinition[] PrefabTargets =
        {
            new TargetDefinition("Assets/Prefabs/Combat/Player.prefab", true),
            new TargetDefinition("Assets/Prefabs/Combat/Monster_Basic.prefab", true),
            new TargetDefinition("Assets/Prefabs/Combat/Monster_Swift.prefab", true),
            new TargetDefinition("Assets/Prefabs/Combat/Monster_Heavy.prefab", true),
            new TargetDefinition("Assets/Prefabs/World/Merchant.prefab", true),
            new TargetDefinition("Assets/Prefabs/World/CraftingStation.prefab", true),
            new TargetDefinition("Assets/Prefabs/Loot/LootPickup.prefab", true),
            new TargetDefinition(ProjectilePrefabPath, false),
        };

        static readonly TargetDefinition[] SceneTargets =
        {
            new TargetDefinition("GroundGrid/GroundBaseTilemap", false),
            new TargetDefinition("GroundGrid/GroundDetailTilemap", false),
            new TargetDefinition("WorldVisuals/Camp/CampTent", true),
            new TargetDefinition("WorldVisuals/Camp/SupplyCrates", true),
            new TargetDefinition("WorldVisuals/Camp/Brazier", true),
            new TargetDefinition("WorldVisuals/Camp/CampFlag", true),
            new TargetDefinition("WorldVisuals/CombatClusters/WestCluster/DeadTreeLeft", true),
            new TargetDefinition("WorldVisuals/CombatClusters/WestCluster/RockClusterA", true),
            new TargetDefinition("WorldVisuals/CombatClusters/EastCluster/DeadTreeRight", true),
            new TargetDefinition("WorldVisuals/CombatClusters/EastCluster/RockClusterB", true),
        };

        public static IReadOnlyList<string> ExpectedSortingLayerNames => ExpectedSortingLayerNamesData;

        public static IReadOnlyList<Alpha017EffectFamilyContract> EffectFamilies => EffectFamiliesData;

        [MenuItem("DarkFlare/Alpha 0.1.7/阶段 A/运行视觉迁移预检")]
        public static void RunMenuPreflight()
        {
            Alpha017VisualBaselineReport report = Capture(Alpha017MigrationPhase.Preflight);
            Debug.Log(
                $"[Alpha017Preflight] targets={report.WorldTargets.Count}, "
                + $"missingFrames={report.MissingEffectFrames.Count}, issues={report.Issues.Count}");

            for (int i = 0; i < report.Issues.Count; i++)
            {
                Alpha017MigrationIssue issue = report.Issues[i];
                Debug.Log($"[Alpha017Preflight] {issue.Code} | {issue.Path} | {issue.Message}");
            }
        }

        public static Alpha017VisualBaselineReport Capture(Alpha017MigrationPhase phase)
        {
            if (phase == Alpha017MigrationPhase.Apply)
            {
                throw new InvalidOperationException("阶段 A 只允许只读预检；Apply 将在阶段 D 的一次性迁移器中实现。");
            }

            List<string> sortingLayers = CaptureSortingLayers();
            List<Alpha017WorldTargetSnapshot> worldTargets = CapturePrefabTargets();
            worldTargets.AddRange(CaptureSceneTargets());
            List<string> missingEffectFrames = CaptureMissingEffectFrames();
            Alpha017ProjectileBaselineSnapshot projectile = CaptureProjectileBaseline();
            List<Alpha017MigrationIssue> issues = BuildIssues(
                sortingLayers,
                worldTargets,
                missingEffectFrames);

            return new Alpha017VisualBaselineReport(
                phase,
                sortingLayers.AsReadOnly(),
                worldTargets.AsReadOnly(),
                missingEffectFrames.AsReadOnly(),
                issues.AsReadOnly(),
                projectile);
        }

        public static IReadOnlyList<string> GetExpectedEffectFramePaths()
        {
            List<string> result = new List<string>();

            for (int familyIndex = 0; familyIndex < EffectFamiliesData.Length; familyIndex++)
            {
                Alpha017EffectFamilyContract family = EffectFamiliesData[familyIndex];

                for (int frameIndex = 0; frameIndex < family.FrameCount; frameIndex++)
                {
                    result.Add(family.GetFramePath(frameIndex));
                }
            }

            return result.AsReadOnly();
        }

        static List<string> CaptureSortingLayers()
        {
            SortingLayer[] layers = SortingLayer.layers;
            List<string> result = new List<string>(layers.Length);

            for (int i = 0; i < layers.Length; i++)
            {
                result.Add(layers[i].name);
            }

            return result;
        }

        static List<Alpha017WorldTargetSnapshot> CapturePrefabTargets()
        {
            List<Alpha017WorldTargetSnapshot> result = new List<Alpha017WorldTargetSnapshot>();

            for (int i = 0; i < PrefabTargets.Length; i++)
            {
                TargetDefinition target = PrefabTargets[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target.Path);
                result.Add(CaptureWorldTarget(
                    Alpha017WorldTargetKind.Prefab,
                    target.Path,
                    target.RequiresSortingGroup,
                    prefab));
            }

            return result;
        }

        static List<Alpha017WorldTargetSnapshot> CaptureSceneTargets()
        {
            List<Alpha017WorldTargetSnapshot> result = new List<Alpha017WorldTargetSnapshot>();
            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedForScan = !scene.IsValid() || !scene.isLoaded;

            if (openedForScan)
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            }

            try
            {
                for (int i = 0; i < SceneTargets.Length; i++)
                {
                    TargetDefinition target = SceneTargets[i];
                    GameObject sceneObject = FindSceneObject(scene, target.Path);
                    result.Add(CaptureWorldTarget(
                        Alpha017WorldTargetKind.SceneObject,
                        target.Path,
                        target.RequiresSortingGroup,
                        sceneObject));
                }
            }
            finally
            {
                if (openedForScan && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return result;
        }

        static Alpha017WorldTargetSnapshot CaptureWorldTarget(
            Alpha017WorldTargetKind kind,
            string path,
            bool requiresSortingGroup,
            GameObject root)
        {
            if (root == null)
            {
                return new Alpha017WorldTargetSnapshot(kind, path, requiresSortingGroup, false, 0, 0, 0);
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            SortingGroup[] sortingGroups = root.GetComponentsInChildren<SortingGroup>(true);
            int defaultRendererCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (string.Equals(renderers[i].sortingLayerName, "Default", StringComparison.Ordinal))
                {
                    defaultRendererCount++;
                }
            }

            return new Alpha017WorldTargetSnapshot(
                kind,
                path,
                requiresSortingGroup,
                true,
                renderers.Length,
                defaultRendererCount,
                sortingGroups.Length);
        }

        static List<string> CaptureMissingEffectFrames()
        {
            IReadOnlyList<string> expectedPaths = GetExpectedEffectFramePaths();
            List<string> result = new List<string>();

            for (int i = 0; i < expectedPaths.Count; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(expectedPaths[i]) == null)
                {
                    result.Add(expectedPaths[i]);
                }
            }

            return result;
        }

        static Alpha017ProjectileBaselineSnapshot CaptureProjectileBaseline()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectileSpritePath);
            TextureImporter importer = AssetImporter.GetAtPath(ProjectileSpritePath) as TextureImporter;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);

            if (texture == null || importer == null || prefab == null)
            {
                return new Alpha017ProjectileBaselineSnapshot(
                    false,
                    0,
                    0,
                    0f,
                    SpriteImportMode.None,
                    SpriteMeshType.Tight,
                    FilterMode.Bilinear,
                    TextureImporterCompression.Compressed,
                    false,
                    TextureWrapMode.Repeat,
                    Vector2.zero,
                    Vector3.zero,
                    Vector2.zero);
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Vector3 rootScale = prefab.transform.localScale;
            Vector2 visibleWorldSize = new Vector2(
                ProjectileVisibleWidthPixels / importer.spritePixelsPerUnit * Mathf.Abs(rootScale.x),
                ProjectileVisibleHeightPixels / importer.spritePixelsPerUnit * Mathf.Abs(rootScale.y));

            return new Alpha017ProjectileBaselineSnapshot(
                true,
                texture.width,
                texture.height,
                importer.spritePixelsPerUnit,
                importer.spriteImportMode,
                settings.spriteMeshType,
                importer.filterMode,
                importer.textureCompression,
                importer.mipmapEnabled,
                importer.wrapMode,
                importer.spritePivot,
                rootScale,
                visibleWorldSize);
        }

        static List<Alpha017MigrationIssue> BuildIssues(
            IReadOnlyList<string> sortingLayers,
            IReadOnlyList<Alpha017WorldTargetSnapshot> targets,
            IReadOnlyList<string> missingEffectFrames)
        {
            List<Alpha017MigrationIssue> result = new List<Alpha017MigrationIssue>();

            for (int i = 0; i < ExpectedSortingLayerNamesData.Length; i++)
            {
                string expectedLayer = ExpectedSortingLayerNamesData[i];

                if (!ContainsOrdinal(sortingLayers, expectedLayer))
                {
                    result.Add(new Alpha017MigrationIssue(
                        "sorting-layer-missing",
                        expectedLayer,
                        "正式 Sorting Layer 尚未建立"));
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Alpha017WorldTargetSnapshot target = targets[i];

                if (!target.Exists)
                {
                    result.Add(new Alpha017MigrationIssue("target-missing", target.Path, "精准迁移目标不存在"));
                    continue;
                }

                if (target.RequiresSortingGroup && target.SortingGroupCount != 1)
                {
                    result.Add(new Alpha017MigrationIssue(
                        "sorting-group-missing",
                        target.Path,
                        $"要求一个 SortingGroup，当前为 {target.SortingGroupCount}"));
                }

                if (target.DefaultRendererCount > 0)
                {
                    result.Add(new Alpha017MigrationIssue(
                        "default-renderer",
                        target.Path,
                        $"仍有 {target.DefaultRendererCount}/{target.RendererCount} 个 Renderer 使用 Default"));
                }
            }

            for (int i = 0; i < missingEffectFrames.Count; i++)
            {
                result.Add(new Alpha017MigrationIssue(
                    "effect-frame-missing",
                    missingEffectFrames[i],
                    "阶段 B 正式帧尚未生成"));
            }

            return result;
        }

        static bool ContainsOrdinal(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        static GameObject FindSceneObject(Scene scene, string hierarchyPath)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(hierarchyPath))
            {
                return null;
            }

            string[] parts = hierarchyPath.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            Transform current = null;

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (string.Equals(roots[rootIndex].name, parts[0], StringComparison.Ordinal))
                {
                    current = roots[rootIndex].transform;
                    break;
                }
            }

            for (int partIndex = 1; current != null && partIndex < parts.Length; partIndex++)
            {
                current = current.Find(parts[partIndex]);
            }

            return current != null ? current.gameObject : null;
        }

        readonly struct TargetDefinition
        {
            public string Path { get; }

            public bool RequiresSortingGroup { get; }

            public TargetDefinition(string path, bool requiresSortingGroup)
            {
                Path = path;
                RequiresSortingGroup = requiresSortingGroup;
            }
        }
    }
}
