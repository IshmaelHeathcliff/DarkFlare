using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace DarkFlare.Editor
{
    public static class GroundTilemapSetup
    {
        const string MenuRoot = "DarkFlare/地表/";
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string TileAssetRoot = "Assets/Art/Tiles/Environment/Ground";
        const string SpriteLitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        const int MapSize = 5;
        const float CellWorldSize = 8f;

        static readonly string[] BaseSpritePaths =
        {
            "Assets/Art/Sprites/Environment/GroundTiles/Base/ground_base_stone_00.png",
            "Assets/Art/Sprites/Environment/GroundTiles/Base/ground_base_stone_01.png",
            "Assets/Art/Sprites/Environment/GroundTiles/Base/ground_base_stone_02.png",
        };

        static readonly string[] DetailSpritePaths =
        {
            "Assets/Art/Sprites/Environment/GroundTiles/Details/ground_detail_cracks_00.png",
            "Assets/Art/Sprites/Environment/GroundTiles/Details/ground_detail_rubble_00.png",
            "Assets/Art/Sprites/Environment/GroundTiles/Details/ground_detail_ash_00.png",
        };

        static readonly int[,] BasePattern =
        {
            { 0, 1, 0, 2, 0 },
            { 2, 0, 1, 0, 2 },
            { 0, 2, 0, 1, 0 },
            { 1, 0, 2, 0, 1 },
            { 0, 1, 0, 2, 0 },
        };

        static readonly DetailPlacement[] DetailPattern =
        {
            new DetailPlacement(0, 0, 1, 0f, 0.62f),
            new DetailPlacement(0, 3, 2, 90f, 0.42f),
            new DetailPlacement(1, 1, 0, 180f, 0.42f),
            new DetailPlacement(1, 4, 1, 270f, 0.58f),
            new DetailPlacement(2, 0, 2, 180f, 0.38f),
            new DetailPlacement(2, 3, 0, 90f, 0.38f),
            new DetailPlacement(3, 1, 1, 180f, 0.58f),
            new DetailPlacement(3, 4, 2, 0f, 0.4f),
            new DetailPlacement(4, 0, 0, 270f, 0.4f),
            new DetailPlacement(4, 2, 1, 90f, 0.62f),
            new DetailPlacement(4, 4, 0, 0f, 0.38f),
        };

        [MenuItem(MenuRoot + "重建双层 Tilemap", false, 100)]
        public static void RebuildGroundTilemaps()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("播放模式中不能重建地表 Tilemap");
            }

            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                throw new InvalidOperationException($"请先打开 {ScenePath}，当前场景为 {scene.path}");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporters(BaseSpritePaths, false);
            ConfigureImporters(DetailSpritePaths, true);
            EnsureAssetFolder(TileAssetRoot);

            Tile[] baseTiles = CreateTileAssets(BaseSpritePaths);
            Tile[] detailTiles = CreateTileAssets(DetailSpritePaths);
            GameObject staging = FindRoot(scene, "GroundGrid_Staging");

            if (staging != null)
            {
                Object.DestroyImmediate(staging);
            }

            staging = new GameObject("GroundGrid_Staging", typeof(Grid));

            try
            {
                BuildHierarchy(staging, baseTiles, detailTiles);
                ValidateStaging(staging);

                GameObject existingGrid = FindRoot(scene, "GroundGrid");
                GameObject legacyGround = FindRoot(scene, "VisualSliceGround");
                int siblingIndex = legacyGround != null ? legacyGround.transform.GetSiblingIndex() : 0;

                if (existingGrid != null)
                {
                    Object.DestroyImmediate(existingGrid);
                }

                if (legacyGround != null)
                {
                    Object.DestroyImmediate(legacyGround);
                }

                staging.name = "GroundGrid";
                staging.transform.SetSiblingIndex(siblingIndex);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("Main 场景保存失败");
                }

                Debug.Log("[GroundTilemapSetup] 已创建 5x5 基础地表与 11 格细节地表，覆盖范围保持 -20..20。");
            }
            catch
            {
                if (staging != null && staging.name == "GroundGrid_Staging")
                {
                    Object.DestroyImmediate(staging);
                }

                throw;
            }
        }

        static void ConfigureImporters(IReadOnlyList<string> paths, bool alphaIsTransparency)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];

                if (!File.Exists(GetAbsoluteProjectPath(path)))
                {
                    throw new FileNotFoundException($"找不到地表 Tile：{path}");
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                {
                    throw new InvalidOperationException($"无法读取 TextureImporter：{path}");
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = alphaIsTransparency;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 512;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        static Tile[] CreateTileAssets(IReadOnlyList<string> spritePaths)
        {
            Tile[] tiles = new Tile[spritePaths.Count];

            for (int i = 0; i < spritePaths.Count; i++)
            {
                string spritePath = spritePaths[i];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                if (sprite == null)
                {
                    throw new InvalidOperationException($"无法加载地表 Sprite：{spritePath}");
                }

                string tilePath = $"{TileAssetRoot}/{Path.GetFileNameWithoutExtension(spritePath)}.asset";
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);

                if (tile == null)
                {
                    Object existing = AssetDatabase.LoadMainAssetAtPath(tilePath);

                    if (existing != null)
                    {
                        throw new InvalidOperationException($"Tile 目标路径已有其他类型资产：{tilePath}");
                    }

                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }

                tile.sprite = sprite;
                tile.color = Color.white;
                tile.transform = Matrix4x4.identity;
                tile.gameObject = null;
                tile.flags = TileFlags.None;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tiles[i] = tile;
            }

            return tiles;
        }

        static void BuildHierarchy(GameObject root, IReadOnlyList<Tile> baseTiles, IReadOnlyList<Tile> detailTiles)
        {
            root.transform.position = new Vector3(-20f, -20f, 0f);
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Grid grid = root.GetComponent<Grid>();
            grid.cellSize = new Vector3(CellWorldSize, CellWorldSize, 0f);
            grid.cellGap = Vector3.zero;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            Tilemap baseTilemap = CreateLayer(root.transform, "GroundBaseTilemap", -100);
            Tilemap detailTilemap = CreateLayer(root.transform, "GroundDetailTilemap", -99);

            for (int y = 0; y < MapSize; y++)
            {
                for (int x = 0; x < MapSize; x++)
                {
                    baseTilemap.SetTile(new Vector3Int(x, y, 0), baseTiles[BasePattern[y, x]]);
                }
            }

            for (int i = 0; i < DetailPattern.Length; i++)
            {
                DetailPlacement placement = DetailPattern[i];
                Vector3Int cell = new Vector3Int(placement.X, placement.Y, 0);
                detailTilemap.SetTile(cell, detailTiles[placement.TileIndex]);
                detailTilemap.SetTransformMatrix(
                    cell,
                    Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, placement.Rotation), Vector3.one));
                detailTilemap.SetColor(cell, new Color(1f, 1f, 1f, placement.Alpha));
            }

            baseTilemap.CompressBounds();
            detailTilemap.CompressBounds();
            baseTilemap.RefreshAllTiles();
            detailTilemap.RefreshAllTiles();
        }

        static Tilemap CreateLayer(Transform parent, string name, int sortingOrder)
        {
            GameObject layer = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(parent, false);
            Tilemap tilemap = layer.GetComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            tilemap.orientation = Tilemap.Orientation.XY;

            TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            renderer.detectChunkCullingBounds = TilemapRenderer.DetectChunkCullingBounds.Auto;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitMaterialPath);

            if (renderer.sharedMaterial == null)
            {
                throw new InvalidOperationException($"找不到 Sprite Lit 材质：{SpriteLitMaterialPath}");
            }

            return tilemap;
        }

        static void ValidateStaging(GameObject root)
        {
            Tilemap baseTilemap = root.transform.Find("GroundBaseTilemap")?.GetComponent<Tilemap>();
            Tilemap detailTilemap = root.transform.Find("GroundDetailTilemap")?.GetComponent<Tilemap>();

            if (baseTilemap == null || detailTilemap == null)
            {
                throw new InvalidOperationException("双层 Tilemap 层级创建不完整");
            }

            int baseCount = CountTiles(baseTilemap);
            int detailCount = CountTiles(detailTilemap);

            if (baseCount != MapSize * MapSize || detailCount != DetailPattern.Length)
            {
                throw new InvalidOperationException($"Tile 填充数量不正确：基础 {baseCount}，细节 {detailCount}");
            }
        }

        static int CountTiles(Tilemap tilemap)
        {
            int count = 0;

            foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(position))
                {
                    count++;
                }
            }

            return count;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        static void EnsureAssetFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        static string GetAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        readonly struct DetailPlacement
        {
            public int X { get; }
            public int Y { get; }
            public int TileIndex { get; }
            public float Rotation { get; }
            public float Alpha { get; }

            public DetailPlacement(int x, int y, int tileIndex, float rotation, float alpha)
            {
                X = x;
                Y = y;
                TileIndex = tileIndex;
                Rotation = rotation;
                Alpha = alpha;
            }
        }
    }
}
