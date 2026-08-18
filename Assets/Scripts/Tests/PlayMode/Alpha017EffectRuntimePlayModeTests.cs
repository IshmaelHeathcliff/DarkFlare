using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha017EffectRuntimePlayModeTests
    {
        GameObject _effectPrefab;
        readonly GameArchitectureTestFixture _fixture = new GameArchitectureTestFixture();
        IArchitecture _architecture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return _fixture.Restart();
            _architecture = _fixture.Architecture;
            _effectPrefab = new GameObject("Alpha017PoolTestEffect");
            _effectPrefab.SetActive(false);
            _effectPrefab.AddComponent<SpriteRenderer>();
            _effectPrefab.AddComponent<Animator>();
            _effectPrefab.AddComponent<PooledSpriteEffect>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _architecture = null;

            if (_effectPrefab != null)
            {
                Object.DestroyImmediate(_effectPrefab);
            }

            yield return _fixture.Restart();
        }

        [UnityTest]
        public IEnumerator Pool_IsPrewarmedBoundedReusableAndReleasedWithArchitecture()
        {
            VisualEffectPool pool = _architecture.GetUtility<VisualEffectPool>();
            pool.Prewarm(_effectPrefab);
            VisualEffectPoolStats initial = pool.GetStats(_effectPrefab);
            Assert.AreEqual(VisualEffectPool.DefaultPrewarmCount, initial.Total);
            Assert.AreEqual(0, initial.Active);
            Assert.AreEqual(VisualEffectPool.DefaultPrewarmCount, initial.Available);

            for (int i = 0; i < VisualEffectPool.MaxInstancesPerPrefab; i++)
            {
                Assert.IsTrue(pool.TryPlay(_effectPrefab, new Vector3(i, 0f, 0f), Color.white), i.ToString());
            }

            Assert.IsFalse(pool.TryPlay(_effectPrefab, Vector3.zero, Color.white));
            VisualEffectPoolStats saturated = pool.GetStats(_effectPrefab);
            Assert.AreEqual(VisualEffectPool.MaxInstancesPerPrefab, saturated.Total);
            Assert.AreEqual(VisualEffectPool.MaxInstancesPerPrefab, saturated.Active);
            Assert.AreEqual(0, saturated.Available);

            yield return new WaitForSeconds(0.3f);

            VisualEffectPoolStats returned = pool.GetStats(_effectPrefab);
            Assert.AreEqual(VisualEffectPool.MaxInstancesPerPrefab, returned.Total);
            Assert.AreEqual(0, returned.Active);
            Assert.AreEqual(VisualEffectPool.MaxInstancesPerPrefab, returned.Available);
            Assert.IsTrue(pool.TryPlay(_effectPrefab, Vector3.zero, Color.red));
            Assert.AreEqual(1, pool.GetStats(_effectPrefab).Active);

            FieldInfo rootField = typeof(VisualEffectPool).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(rootField);
            Transform poolRoot = (Transform)rootField.GetValue(pool);
            Assert.IsNotNull(poolRoot);
            yield return _fixture.StopCurrent();
            _architecture = null;
            Assert.AreEqual(0, pool.GetStats(_effectPrefab).Total);
            float destroyTimeout = Time.realtimeSinceStartup + 1f;

            while (poolRoot != null && Time.realtimeSinceStartup < destroyTimeout)
            {
                yield return null;
            }

            Assert.IsTrue(poolRoot == null, "本测试创建的特效池根未在帧末销毁");
        }
    }
}
