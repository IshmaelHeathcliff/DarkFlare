using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace DarkFlare.Tests
{
    public class GroundTilemapTests
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string SpriteRoot = "Assets/Art/Sprites/Environment/GroundTiles";
        const string TileRoot = "Assets/Art/Tiles/Environment/Ground";

        static readonly string[] SpriteNames =
        {
            "Base/ground_base_stone_00",
            "Base/ground_base_stone_01",
            "Base/ground_base_stone_02",
            "Details/ground_detail_cracks_00",
            "Details/ground_detail_rubble_00",
            "Details/ground_detail_ash_00",
        };

        [Test]
        public void GroundSpritesAndTiles_UseFrozenSingleSpriteContract()
        {
            for (int i = 0; i < SpriteNames.Length; i++)
            {
                string spritePath = $"{SpriteRoot}/{SpriteNames[i]}.png";
                string tilePath = $"{TileRoot}/{Path.GetFileName(SpriteNames[i])}.asset";
                TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                TextureImporterSettings settings = new TextureImporterSettings();

                Assert.IsNotNull(importer, spritePath);
                importer.ReadTextureSettings(settings);
                Assert.IsNotNull(sprite, spritePath);
                Assert.IsNotNull(tile, tilePath);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, spritePath);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, spritePath);
                Assert.AreEqual(64f, importer.spritePixelsPerUnit, 0.001f, spritePath);
                Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType, spritePath);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, spritePath);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, spritePath);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, spritePath);
                Assert.IsFalse(importer.mipmapEnabled, spritePath);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), sprite.pivot / sprite.rect.size, spritePath);
                Assert.AreSame(sprite, tile.sprite, tilePath);
                Assert.AreEqual(Tile.ColliderType.None, tile.colliderType, tilePath);
            }
        }

        [Test]
        public void MainScene_UsesFilledDoubleGroundTilemap()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                GameObject gridObject = Array.Find(scene.GetRootGameObjects(), root => root.name == "GroundGrid");
                GameObject legacy = Array.Find(scene.GetRootGameObjects(), root => root.name == "VisualSliceGround");

                Assert.IsNotNull(gridObject);
                Assert.IsNull(legacy);
                Assert.AreEqual(new Vector3(-20f, -20f, 0f), gridObject.transform.position);
                Assert.AreEqual(Vector3.one, gridObject.transform.localScale);

                Grid grid = gridObject.GetComponent<Grid>();
                Assert.IsNotNull(grid);
                Assert.AreEqual(new Vector3(8f, 8f, 0f), grid.cellSize);

                Tilemap baseTilemap = gridObject.transform.Find("GroundBaseTilemap")?.GetComponent<Tilemap>();
                Tilemap detailTilemap = gridObject.transform.Find("GroundDetailTilemap")?.GetComponent<Tilemap>();
                Assert.IsNotNull(baseTilemap);
                Assert.IsNotNull(detailTilemap);
                Assert.AreEqual(25, CountTiles(baseTilemap));
                Assert.AreEqual(11, CountTiles(detailTilemap));
                Assert.AreEqual(3, baseTilemap.GetUsedTilesCount());
                Assert.AreEqual(3, detailTilemap.GetUsedTilesCount());
                Assert.AreEqual(new Vector3Int(0, 0, 0), baseTilemap.cellBounds.position);
                Assert.AreEqual(new Vector3Int(5, 5, 1), baseTilemap.cellBounds.size);
                Assert.IsNull(baseTilemap.GetComponent<TilemapCollider2D>());
                Assert.IsNull(detailTilemap.GetComponent<TilemapCollider2D>());
                TilemapRenderer baseRenderer = baseTilemap.GetComponent<TilemapRenderer>();
                TilemapRenderer detailRenderer = detailTilemap.GetComponent<TilemapRenderer>();
                Assert.IsNotNull(baseRenderer);
                Assert.IsNotNull(detailRenderer);
                Assert.AreEqual(-100, baseRenderer.sortingOrder);
                Assert.AreEqual(-99, detailRenderer.sortingOrder);
            }
            finally
            {
                if (setup.Length > 0 && Array.Exists(setup, item => item.isLoaded && item.isActive))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
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
    }
}
