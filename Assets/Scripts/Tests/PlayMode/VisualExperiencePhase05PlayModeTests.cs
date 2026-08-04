using System.Collections;
using DarkFlare;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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

            GameObject ground = GameObject.Find("VisualSliceGround");
            GameObject boundsObject = GameObject.Find("WorldBounds");
            Assert.IsNotNull(ground);
            Assert.AreEqual(25, ground.transform.childCount);
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

        static Bounds GetRendererBounds(GameObject root)
        {
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
            Assert.IsNotEmpty(renderers);
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
