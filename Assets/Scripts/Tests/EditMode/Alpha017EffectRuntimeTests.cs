using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class Alpha017EffectRuntimeTests
    {
        const string ProjectilePrefabPath = "Assets/Prefabs/Combat/Projectile_Default.prefab";
        const string ImpactPrefabPath = "Assets/Prefabs/Effects/Projectile_Arcane_Impact.prefab";
        const string DefaultHitPrefabPath = "Assets/Prefabs/Effects/Actor_Hit_Default.prefab";
        const string CriticalHitPrefabPath = "Assets/Prefabs/Effects/Actor_Hit_Critical.prefab";

        static readonly string[] ActorPrefabPaths =
        {
            "Assets/Prefabs/Combat/Player.prefab",
            "Assets/Prefabs/Combat/Monster_Basic.prefab",
            "Assets/Prefabs/Combat/Monster_Swift.prefab",
            "Assets/Prefabs/Combat/Monster_Heavy.prefab",
        };

        [Test]
        public void DamageTint_UsesLargestFinalDamageAndStableTypePriority()
        {
            DamageResult equalDamage = CreateResult(
                true,
                new Dictionary<DamageType, float>
                {
                    { DamageType.Physical, 10f },
                    { DamageType.Fire, 10f },
                    { DamageType.Cold, 10f },
                    { DamageType.Lightning, 10f },
                    { DamageType.Chaos, 10f },
                });
            Assert.IsTrue(CombatEffectPalette.TryGetPrimaryDamageType(equalDamage, out DamageType equalType));
            Assert.AreEqual(DamageType.Physical, equalType);

            DamageResult chaosDamage = CreateResult(
                false,
                new Dictionary<DamageType, float>
                {
                    { DamageType.Physical, 5f },
                    { DamageType.Chaos, 12f },
                });
            Assert.IsTrue(CombatEffectPalette.TryGetPrimaryDamageType(chaosDamage, out DamageType chaosType));
            Assert.AreEqual(DamageType.Chaos, chaosType);
            Assert.AreEqual(new Color(0.72f, 0.34f, 1f, 1f), CombatEffectPalette.GetHitTint(chaosDamage));

            Dictionary<DamageType, Color> expectedColors = new Dictionary<DamageType, Color>
            {
                { DamageType.Physical, new Color(1f, 0.9f, 0.76f, 1f) },
                { DamageType.Fire, new Color(1f, 0.36f, 0.18f, 1f) },
                { DamageType.Cold, new Color(0.32f, 0.82f, 1f, 1f) },
                { DamageType.Lightning, new Color(1f, 0.84f, 0.24f, 1f) },
                { DamageType.Chaos, new Color(0.72f, 0.34f, 1f, 1f) },
            };
            HashSet<Color> colors = new HashSet<Color>();

            foreach (KeyValuePair<DamageType, Color> pair in expectedColors)
            {
                Color actual = CombatEffectPalette.GetHitTint(pair.Key);
                Assert.AreEqual(pair.Value, actual, pair.Key.ToString());
                Assert.IsTrue(colors.Add(actual), pair.Key.ToString());
            }

            Assert.AreEqual(new Color(0.35f, 0.95f, 1f, 1f), CombatEffectPalette.ArcaneProjectileTint);
        }

        [Test]
        public void EffectSemantics_SeparateContactFromActualActorDamage()
        {
            DamageResult hit = CreateResult(
                false,
                new Dictionary<DamageType, float> { { DamageType.Physical, 10f } });
            DamageResult noDamage = DamageResult.CreateWithoutDamage(new HitResolution(
                HitOutcome.NoDamage,
                1f,
                0f,
                0f,
                0f,
                false));
            DamageResult missed = DamageResult.CreateWithoutDamage(new HitResolution(
                HitOutcome.Missed,
                0f,
                1f,
                0f,
                0f,
                false));
            DamageResult evaded = DamageResult.CreateWithoutDamage(new HitResolution(
                HitOutcome.Evaded,
                0.5f,
                0.75f,
                0f,
                0f,
                false));

            Assert.IsTrue(CombatEffectPalette.ShouldPlayProjectileImpact(hit));
            Assert.IsTrue(CombatEffectPalette.ShouldPlayActorHit(hit));
            Assert.IsTrue(CombatEffectPalette.ShouldPlayProjectileImpact(noDamage));
            Assert.IsFalse(CombatEffectPalette.ShouldPlayActorHit(noDamage));
            Assert.IsFalse(CombatEffectPalette.ShouldPlayProjectileImpact(missed));
            Assert.IsFalse(CombatEffectPalette.ShouldPlayActorHit(missed));
            Assert.IsFalse(CombatEffectPalette.ShouldPlayProjectileImpact(evaded));
            Assert.IsFalse(CombatEffectPalette.ShouldPlayActorHit(evaded));
            Assert.IsFalse(CombatEffectPalette.ShouldPlayProjectileImpact(null));
            Assert.IsFalse(CombatEffectPalette.ShouldPlayActorHit(null));
        }

        [Test]
        public void ProjectilePrefab_PreservesPhysicsRootAndUsesFlightVisualChild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            Assert.IsNotNull(prefab, ProjectilePrefabPath);
            Assert.AreEqual(new Vector3(0.25f, 0.25f, 1f), prefab.transform.localScale);
            Assert.IsNotNull(prefab.GetComponent<Rigidbody2D>());
            CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
            Assert.IsNotNull(collider);
            Assert.AreEqual(0.16f, collider.radius, 0.0001f);
            Assert.IsNull(prefab.GetComponent<SpriteRenderer>(), "物理根不应继续承载飞行表现");

            Transform visual = prefab.transform.Find("Visual");
            Assert.IsNotNull(visual);
            Assert.AreEqual(Vector3.one, visual.localScale);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            Animator animator = visual.GetComponent<Animator>();
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(animator);
            Assert.AreEqual("effect_projectile_arcane_flight_e_00", renderer.sprite.name);
            Assert.AreEqual(
                "Assets/Art/Animations/Effects/Controllers/Projectile_Arcane_Flight.controller",
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));

            ProjectileController controller = prefab.GetComponent<ProjectileController>();
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath),
                GetField<GameObject>(controller, "_impactEffectPrefab"));
        }

        [Test]
        public void ActorPrefabs_HaveStableHitAnchorAndExplicitEffectDependencies()
        {
            GameObject defaultHitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultHitPrefabPath);
            GameObject criticalHitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CriticalHitPrefabPath);
            Assert.IsNotNull(defaultHitPrefab);
            Assert.IsNotNull(criticalHitPrefab);

            for (int i = 0; i < ActorPrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPrefabPaths[i]);
                Assert.IsNotNull(prefab, ActorPrefabPaths[i]);
                Transform anchor = prefab.transform.Find("HitEffectAnchor");
                Assert.IsNotNull(anchor, ActorPrefabPaths[i]);
                Assert.AreEqual(Vector3.one, anchor.localScale, ActorPrefabPaths[i]);
                Assert.Greater(anchor.localPosition.y, 0f, ActorPrefabPaths[i]);
                ActorVisualFeedbackController controller = prefab.GetComponent<ActorVisualFeedbackController>();
                Assert.IsNotNull(controller, ActorPrefabPaths[i]);
                Assert.AreSame(anchor, GetField<Transform>(controller, "_hitEffectAnchor"));
                Assert.AreSame(defaultHitPrefab, GetField<GameObject>(controller, "_defaultHitEffectPrefab"));
                Assert.AreSame(criticalHitPrefab, GetField<GameObject>(controller, "_criticalHitEffectPrefab"));
                Assert.IsNull(
                    typeof(ActorVisualFeedbackController).GetField(
                        "_flashTween",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    "多帧受击接入后不应保留闪白 Tween 状态");
            }
        }

        [TestCase(ImpactPrefabPath, "effect_projectile_arcane_impact_00", "Projectile_Arcane_Impact.controller")]
        [TestCase(DefaultHitPrefabPath, "effect_hit_default_00", "Actor_Hit_Default.controller")]
        [TestCase(CriticalHitPrefabPath, "effect_hit_critical_00", "Actor_Hit_Critical.controller")]
        public void OneShotEffectPrefabs_AreInactiveUnitScaleAndPoolReady(
            string prefabPath,
            string firstFrameName,
            string controllerName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            Assert.IsFalse(prefab.activeSelf, prefabPath);
            Assert.AreEqual(Vector3.one, prefab.transform.localScale, prefabPath);
            PooledSpriteEffect effect = prefab.GetComponent<PooledSpriteEffect>();
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Animator animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(effect, prefabPath);
            Assert.IsNotNull(renderer, prefabPath);
            Assert.IsNotNull(animator, prefabPath);
            Assert.AreEqual(0.25f, effect.Duration, 0.0001f, prefabPath);
            Assert.AreEqual(firstFrameName, renderer.sprite.name, prefabPath);
            Assert.AreEqual(controllerName, System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)));
        }

        static DamageResult CreateResult(bool isCritical, Dictionary<DamageType, float> finalDamages)
        {
            Dictionary<DamageType, DamageTypeBreakdown> breakdowns = new Dictionary<DamageType, DamageTypeBreakdown>();

            foreach (KeyValuePair<DamageType, float> pair in finalDamages)
            {
                breakdowns[pair.Key] = new DamageTypeBreakdown(
                    pair.Key,
                    pair.Value,
                    pair.Value,
                    0f,
                    0f,
                    0f,
                    pair.Value);
            }

            return new DamageResult(HitOutcome.Hit, isCritical, 1f, 0f, 1f, 0f, breakdowns);
        }

        static T GetField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return field.GetValue(target) as T;
        }
    }
}
