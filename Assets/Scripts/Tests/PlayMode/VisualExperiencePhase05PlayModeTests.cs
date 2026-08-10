using System.Collections;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public class VisualExperiencePhase05PlayModeTests
    {
        IArchitecture _architecture;

        [SetUp]
        public void SetUp()
        {
            GameArchitecture.Interface.Deinit();
            _architecture = GameArchitecture.Interface;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            _architecture?.Deinit();
            _architecture = null;
        }

        [UnityTest]
        public IEnumerator MainScene_UsesSharedBoundsAndKeepsCameraOnGround()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            PlayerController player = null;
            CameraFollowTarget follow = null;
            MonsterSpawner spawner = null;
            float timeout = Time.realtimeSinceStartup + 15f;

            while ((player == null || follow == null || spawner == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                player = Object.FindAnyObjectByType<PlayerController>();
                follow = Camera.main != null ? Camera.main.GetComponent<CameraFollowTarget>() : null;
                spawner = Object.FindAnyObjectByType<MonsterSpawner>();
                yield return null;
            }

            Assert.IsNotNull(player, "Main 场景未生成玩家");
            Assert.IsNotNull(follow, "Main Camera 缺少 CameraFollowTarget");
            Assert.IsNotNull(spawner, "Main 场景缺少 MonsterSpawner");
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation);
            Assert.IsNotNull(follow.WorldBounds, "相机未绑定 WorldBounds");
            Assert.AreSame(follow.WorldBounds, spawner.WorldBounds, "相机与刷怪器未共用同一边界");

            GameObject ground = GameObject.Find("GroundGrid");
            GameObject boundsObject = GameObject.Find("WorldBounds");
            Assert.IsNotNull(ground);
            Assert.AreEqual(2, ground.transform.childCount);
            Tilemap baseTilemap = ground.transform.Find("GroundBaseTilemap").GetComponent<Tilemap>();
            Tilemap detailTilemap = ground.transform.Find("GroundDetailTilemap").GetComponent<Tilemap>();
            Assert.AreEqual(25, CountTiles(baseTilemap));
            Assert.AreEqual(11, CountTiles(detailTilemap));
            Assert.IsNotNull(boundsObject);
            Assert.AreEqual(follow.WorldBounds, boundsObject.GetComponent<EdgeCollider2D>());

            Bounds groundBounds = GetRendererBounds(ground);
            Camera camera = Camera.main;
            Vector3 clamped = follow.ClampToWorldBounds(new Vector3(100f, 100f, camera.transform.position.z));
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Assert.GreaterOrEqual(clamped.x - halfWidth, groundBounds.min.x);
            Assert.LessOrEqual(clamped.x + halfWidth, groundBounds.max.x);
            Assert.GreaterOrEqual(clamped.y - halfHeight, groundBounds.min.y);
            Assert.LessOrEqual(clamped.y + halfHeight, groundBounds.max.y);
        }

        [UnityTest]
        public IEnumerator LootRarityRing_RotatesAndUsesRuntimeRarityColor()
        {
            GameObject root = new GameObject("LootPickupVisualTest");
            GameObject visualRoot = new GameObject("Visual");
            GameObject halo = new GameObject("Halo");
            visualRoot.transform.SetParent(root.transform, false);
            halo.transform.SetParent(visualRoot.transform, false);
            SpriteRenderer haloRenderer = halo.AddComponent<SpriteRenderer>();
            LootPickupVisual visual = root.AddComponent<LootPickupVisual>();
            ItemBaseDefinition definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
            ItemInstance item = definition.CreateInstance("loot-ring-playmode", 1, 1, ItemRarity.Magic);
            Quaternion initialRotation = halo.transform.localRotation;

            visual.Bind(item);
            yield return new WaitForSeconds(0.25f);

            Assert.Greater(Quaternion.Angle(initialRotation, halo.transform.localRotation), 5f);
            Color expected = LootPickupVisual.GetRarityColor(ItemRarity.Magic);
            Assert.AreEqual(expected.r, haloRenderer.color.r, 0.001f);
            Assert.AreEqual(expected.g, haloRenderer.color.g, 0.001f);
            Assert.AreEqual(expected.b, haloRenderer.color.b, 0.001f);
            Assert.AreEqual(0.82f, haloRenderer.color.a, 0.001f);

            Object.Destroy(root);
            Object.Destroy(definition);
            yield return null;
        }

        static Bounds GetRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers);
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
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
