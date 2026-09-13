using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DarkFlare.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class Alpha017BaselineTests
    {
        const string ManifestRoot = "Docs/docs/assets/visual-assets/alpha-0.1.7/manifests";

        // 代表直接嵌套、列表元素和递归条件；允许后续模块增加自己的数据结构。
        static readonly string[] RequiredNestedTypes =
        {
            "DarkFlare.DamageRollDefinition",
            "DarkFlare.LocalizedContentReference",
            "DarkFlare.LootTableEntry",
            "DarkFlare.MonsterSpawnRule",
            "DarkFlare.StatModifierDefinition",
            "DarkFlare.TagQueryDefinition",
            "DarkFlare.TraderStockEntry",
        };

        readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        readonly GameArchitectureTestFixture _architectureFixture = new GameArchitectureTestFixture();

        IArchitecture _architecture;

        [SetUp]
        public void SetUp()
        {
            _architecture = _architectureFixture.Start();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            _architectureFixture.Stop();
            _architecture = null;
        }

        [Test]
        public void ConfigurationDiscovery_FindsRegisteredContentAndReachableNestedStructures()
        {
            IReadOnlyList<Type> topLevelTypes = ConfigurationTypeDiscovery.FindTopLevelTypes();
            IReadOnlyList<Type> nestedTypes = ConfigurationTypeDiscovery.FindNestedSerializedTypes(topLevelTypes);

            CollectionAssert.IsSubsetOf(
                ContentDefinitionRegistry.All.Select(metadata => metadata.DefinitionType).ToArray(),
                topLevelTypes.ToArray());
            CollectionAssert.IsSubsetOf(
                RequiredNestedTypes,
                nestedTypes.Select(type => type.FullName).ToArray());
            Assert.That(nestedTypes.Any(type => typeof(UnityEngine.Object).IsAssignableFrom(type)), Is.False,
                "嵌套发现不能把资产引用当作内联数据递归");
            Assert.That(nestedTypes.Contains(typeof(ModifierInstance)), Is.False,
                "运行时只读属性不是 Unity 序列化字段");
            Assert.Greater(ConfigurationTypeDiscovery.CountSerializedFields(topLevelTypes), 0);
            Assert.Greater(ConfigurationTypeDiscovery.CountSerializedFields(nestedTypes), 0);

            for (int i = 0; i < topLevelTypes.Count; i++)
            {
                CreateAssetMenuAttribute menu = topLevelTypes[i].GetCustomAttribute<CreateAssetMenuAttribute>();
                Assert.IsNotNull(menu, topLevelTypes[i].FullName);
                StringAssert.StartsWith(ConfigurationTypeDiscovery.ConfigMenuPrefix, menu.menuName);
            }
        }

        [Test]
        public void EffectManifests_FreezeTwentyThreeIndependentSingleSpriteFrames()
        {
            IReadOnlyList<Alpha017EffectFamilyContract> families = Alpha017VisualMigrationPreflight.EffectFamilies;
            HashSet<string> manifestPaths = new HashSet<string>(StringComparer.Ordinal);

            Assert.AreEqual(4, families.Count);

            for (int familyIndex = 0; familyIndex < families.Count; familyIndex++)
            {
                Alpha017EffectFamilyContract family = families[familyIndex];
                string manifestPath = Path.Combine(ManifestRoot, $"{family.Id}.json");
                Assert.IsTrue(File.Exists(manifestPath), manifestPath);
                string json = File.ReadAllText(manifestPath);

                StringAssert.Contains("\"schema_version\": 1", json);
                StringAssert.Contains("\"canvas\": { \"width\": 96, \"height\": 96 }", json);
                StringAssert.Contains("\"generation_canvas\": { \"width\": 1254, \"height\": 1254 }", json);
                StringAssert.Contains("\"uniform_scale\": 0.0765550239", json);
                StringAssert.Contains("\"pixels_per_unit\": 64", json);
                StringAssert.Contains($"\"frames_per_second\": {family.FramesPerSecond}", json);
                StringAssert.Contains($"\"loop\": {family.Loop.ToString().ToLowerInvariant()}", json);
                Assert.IsFalse(json.Contains("SpriteSheet", StringComparison.OrdinalIgnoreCase));

                MatchCollection images = Regex.Matches(json, "\\\"image\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                Assert.AreEqual(family.FrameCount, images.Count, family.Id);

                for (int frameIndex = 0; frameIndex < images.Count; frameIndex++)
                {
                    string relativeImagePath = images[frameIndex].Groups[1].Value.Replace('\\', '/');
                    int assetsIndex = relativeImagePath.IndexOf("Assets/", StringComparison.Ordinal);
                    Assert.GreaterOrEqual(assetsIndex, 0, relativeImagePath);
                    string assetPath = relativeImagePath.Substring(assetsIndex);
                    Assert.AreEqual(family.GetFramePath(frameIndex), assetPath);
                    Assert.IsTrue(manifestPaths.Add(assetPath), $"重复帧路径：{assetPath}");
                }
            }

            CollectionAssert.AreEquivalent(
                Alpha017VisualMigrationPreflight.GetExpectedEffectFramePaths(),
                manifestPaths);
            Assert.AreEqual(23, manifestPaths.Count);
        }

        [Test]
        public void EffectFrames_AreImportedAsIndependentContractSprites()
        {
            IReadOnlyList<string> paths = Alpha017VisualMigrationPreflight.GetExpectedEffectFramePaths();

            Assert.AreEqual(23, paths.Count);

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                Assert.IsTrue(File.Exists(path), path);
                Assert.IsTrue(Alpha017EffectAssetPostprocessor.IsEffectPath(path), path);

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, path);
                Assert.AreEqual(64f, importer.spritePixelsPerUnit, 0.001f, path);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                Assert.IsFalse(importer.crunchedCompression, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale, path);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, path);
                Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource, path);
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.IsTrue(importer.sRGBTexture, path);
                Assert.IsFalse(importer.isReadable, path);

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType, path);
                Assert.AreEqual((int)SpriteAlignment.Custom, settings.spriteAlignment, path);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), settings.spritePivot, path);
                Assert.AreEqual(Vector4.zero, settings.spriteBorder, path);
                Assert.IsFalse(settings.spriteGenerateFallbackPhysicsShape, path);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path), path);
            }
        }

        [Test]
        public void EffectClips_BindEveryIndependentFrameInContractOrder()
        {
            for (int familyIndex = 0; familyIndex < Alpha017VisualMigrationPreflight.EffectFamilies.Count; familyIndex++)
            {
                Alpha017EffectFamilyContract family = Alpha017VisualMigrationPreflight.EffectFamilies[familyIndex];
                string clipPath = Alpha017EffectAnimationBuilder.GetClipPath(family.Id);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                Assert.IsNotNull(clip, clipPath);
                Assert.AreEqual(family.FramesPerSecond, clip.frameRate, clipPath);
                Assert.AreEqual(family.Loop, AnimationUtility.GetAnimationClipSettings(clip).loopTime, clipPath);

                EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Assert.AreEqual(1, bindings.Length, clipPath);
                Assert.AreEqual(typeof(SpriteRenderer), bindings[0].type, clipPath);
                Assert.AreEqual("m_Sprite", bindings[0].propertyName, clipPath);

                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
                Assert.AreEqual(family.FrameCount + 1, keys.Length, clipPath);

                for (int frameIndex = 0; frameIndex < family.FrameCount; frameIndex++)
                {
                    Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(family.GetFramePath(frameIndex));
                    Assert.AreSame(expected, keys[frameIndex].value, family.GetFramePath(frameIndex));
                    Assert.AreEqual(frameIndex / (float)family.FramesPerSecond, keys[frameIndex].time, 0.0001f);
                }

                Assert.AreSame(keys[family.FrameCount - 1].value, keys[family.FrameCount].value, clipPath);
                Assert.AreEqual(
                    family.FrameCount / (float)family.FramesPerSecond,
                    keys[family.FrameCount].time,
                    0.0001f,
                    clipPath);
            }
        }

        [Test]
        public void ProjectileFlight_FreezesImporterAndRuntimeVisibleSize()
        {
            Alpha017VisualBaselineReport report = Alpha017VisualMigrationPreflight.Capture(
                Alpha017MigrationPhase.Preflight);
            Alpha017ProjectileBaselineSnapshot projectile = report.Projectile;

            Assert.IsTrue(projectile.Exists);
            Assert.AreEqual(96, projectile.CanvasWidth);
            Assert.AreEqual(96, projectile.CanvasHeight);
            Assert.AreEqual(64f, projectile.PixelsPerUnit);
            Assert.AreEqual(SpriteImportMode.Single, projectile.SpriteMode);
            Assert.AreEqual(SpriteMeshType.FullRect, projectile.MeshType);
            Assert.AreEqual(FilterMode.Point, projectile.FilterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, projectile.Compression);
            Assert.IsFalse(projectile.MipmapEnabled);
            Assert.AreEqual(TextureWrapMode.Clamp, projectile.WrapMode);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), projectile.Pivot);
            Assert.AreEqual(new Vector3(0.25f, 0.25f, 1f), projectile.PrefabRootScale);
            Assert.AreEqual(0.234375f, projectile.VisibleWorldSize.x, 0.00001f);
            Assert.AreEqual(0.1015625f, projectile.VisibleWorldSize.y, 0.00001f);
        }

        [Test]
        public void VisualMigrationValidation_UsesOnlyNamedTargetsAndHasNoRemainingGap()
        {
            Alpha017VisualBaselineReport report = Alpha017VisualMigrationPreflight.Capture(
                Alpha017MigrationPhase.Validate);

            Assert.AreEqual(Alpha017MigrationPhase.Validate, report.Phase);
            CollectionAssert.AreEqual(
                new[] { "Default", "Ground", "WorldObject", "WorldEffect", "WorldInfo" },
                report.SortingLayers);
            Assert.AreEqual(18, report.WorldTargets.Count);
            Assert.AreEqual(8, report.WorldTargets.Count(target => target.Kind == Alpha017WorldTargetKind.Prefab));
            Assert.AreEqual(10, report.WorldTargets.Count(target => target.Kind == Alpha017WorldTargetKind.SceneObject));
            Assert.IsTrue(report.WorldTargets.All(target => target.Exists));
            Assert.IsTrue(report.WorldTargets.All(target => target.RendererCount > 0));
            Assert.IsTrue(report.WorldTargets.All(target => target.DefaultRendererCount == 0));
            Assert.IsTrue(report.WorldTargets
                .Where(target => target.RequiresSortingGroup)
                .All(target => target.SortingGroupCount == 1));
            Assert.AreEqual(0, report.MissingEffectFrames.Count);
            Assert.AreEqual(0, report.Issues.Count);
            Assert.Throws<InvalidOperationException>(() =>
                Alpha017VisualMigrationPreflight.Capture(Alpha017MigrationPhase.Apply));
        }

        [Test]
        public void CombatEvents_ResolveBeforeDamage_AndNonDamageOutcomesNeverEmitDamage()
        {
            List<string> eventOrder = new List<string>();
            _architecture.RegisterEvent<DamageResolvedEvent>(_ => eventOrder.Add("resolved"));
            _architecture.RegisterEvent<ActorDamagedEvent>(_ => eventOrder.Add("damaged"));
            CombatActor attacker = CreateActor("alpha017_attacker", ActorTeam.Player, 100f, 0f);
            CombatSystem combat = _architecture.GetSystem<CombatSystem>();

            CombatActor hitDefender = CreateActor("alpha017_hit", ActorTeam.Monster, 0f, 0f);
            DamageResult hit = combat.ApplyDamage(CreateAttack(attacker, FindSeed(roll => roll < 0.5f), 10f), hitDefender);
            Assert.IsTrue(hit.DidDealDamage);
            CollectionAssert.AreEqual(new[] { "resolved", "damaged" }, eventOrder);

            eventOrder.Clear();
            CombatActor noDamageDefender = CreateActor("alpha017_no_damage", ActorTeam.Monster, 0f, 0f);
            float noDamageHealth = noDamageDefender.CurrentHealth;
            DamageResult noDamage = combat.ApplyDamage(
                CreateAttack(attacker, FindSeed(roll => roll < 0.5f), null),
                noDamageDefender);
            Assert.AreEqual(HitOutcome.NoDamage, noDamage.Outcome);
            Assert.AreEqual(noDamageHealth, noDamageDefender.CurrentHealth);
            CollectionAssert.AreEqual(new[] { "resolved" }, eventOrder);

            eventOrder.Clear();
            CombatActor missedDefender = CreateActor("alpha017_missed", ActorTeam.Monster, 0f, 0f);
            float missedHealth = missedDefender.CurrentHealth;
            DamageResult missed = combat.ApplyDamage(
                CreateAttack(attacker, FindSeed(roll => roll >= 0.95f), 10f),
                missedDefender);
            Assert.AreEqual(HitOutcome.Missed, missed.Outcome);
            Assert.AreEqual(missedHealth, missedDefender.CurrentHealth);
            CollectionAssert.AreEqual(new[] { "resolved" }, eventOrder);

            eventOrder.Clear();
            CombatActor evadedDefender = CreateActor("alpha017_evaded", ActorTeam.Monster, 0f, 100f);
            float evadedHealth = evadedDefender.CurrentHealth;
            DamageResult evaded = combat.ApplyDamage(
                CreateAttack(attacker, FindSeed(roll => roll >= 0.5f && roll < 0.95f), 10f),
                evadedDefender);
            Assert.AreEqual(HitOutcome.Evaded, evaded.Outcome);
            Assert.AreEqual(evadedHealth, evadedDefender.CurrentHealth);
            CollectionAssert.AreEqual(new[] { "resolved" }, eventOrder);
        }

        [Test]
        public void CombatText_FreezesEnemyTeamAndOutcomeSemantics()
        {
            Assert.AreEqual("-12", DamageNumberVisual.FormatText(12f, CombatTextKind.Damage));
            Assert.AreEqual("+12", DamageNumberVisual.FormatText(12f, CombatTextKind.Healing));
            Assert.AreEqual("未命中", DamageNumberVisual.FormatText(0f, CombatTextKind.Missed));
            Assert.AreEqual("闪避", DamageNumberVisual.FormatText(0f, CombatTextKind.Evaded));
            Assert.AreEqual("无伤害", DamageNumberVisual.FormatText(0f, CombatTextKind.NoDamage));
            Assert.AreNotEqual(
                DamageNumberVisual.GetColor(ActorTeam.Player, CombatTextKind.Damage),
                DamageNumberVisual.GetColor(ActorTeam.Monster, CombatTextKind.Damage));
            Assert.AreNotEqual(
                DamageNumberVisual.GetColor(ActorTeam.Player, CombatTextKind.Damage),
                DamageNumberVisual.GetColor(ActorTeam.Player, CombatTextKind.Healing));
            Assert.AreNotEqual(
                DamageNumberVisual.GetColor(ActorTeam.Monster, CombatTextKind.Damage),
                DamageNumberVisual.GetColor(ActorTeam.Monster, CombatTextKind.CriticalDamage));
        }

        CombatActor CreateActor(string id, ActorTeam team, float accuracy, float evasion)
        {
            GameObject actorObject = new GameObject(id);
            _objects.Add(actorObject);
            CombatActor actor = actorObject.AddComponent<CombatActor>();
            StatBlock stats = new StatBlock();
            stats.SetValue(StatIds.Accuracy, accuracy);
            stats.SetValue(StatIds.Evasion, evasion);
            actor.Configure(id, team, 100f, stats, TagSet.Empty);
            return actor;
        }

        static AttackSnapshot CreateAttack(CombatActor attacker, int seed, float? damage)
        {
            IReadOnlyList<DamagePacket> packets = damage.HasValue
                ? new[] { new DamagePacket(DamageType.Physical, damage.Value, TagSet.Empty) }
                : Array.Empty<DamagePacket>();

            return new AttackSnapshot(
                attacker.ActorId,
                attacker.Team,
                "alpha017_baseline",
                string.Empty,
                seed,
                packets,
                TagSet.Empty,
                attacker.Stats,
                Array.Empty<ModifierInstance>());
        }

        static int FindSeed(Func<float, bool> predicate)
        {
            for (int seed = 0; seed < 100000; seed++)
            {
                if (predicate(AttackRandomRolls.FromRootSeed(seed).HitRoll))
                {
                    return seed;
                }
            }

            Assert.Fail("找不到满足命中随机条件的种子");
            return 0;
        }
    }
}
