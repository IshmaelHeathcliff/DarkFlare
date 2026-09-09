using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public class Phase5VisualIntegrationTests
    {
        const string AddressableSettingsPath = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
        const string ItemRoot = "Assets/Data/Preset/Items";
        const string ProductionSpriteRoot = "Assets/Art/Sprites";
        const string ActorClipRoot = "Assets/Art/Animations/Clips";
        const string UiIconRoot = "Assets/Art/Sprites/UI/Icons";
        const string UiFrameRoot = "Assets/Art/Sprites/UI/Frames";
        const string WorldPropRoot = "Assets/Art/Sprites/Environment/WorldProps";
        const string GroundBaseRoot = "Assets/Art/Sprites/Environment/GroundTiles/Base";
        const string ProjectilePath = "Assets/Art/Sprites/Effects/Projectile/Arcane/Flight/effect_projectile_arcane_flight_e_00.png";
        const string LootRarityEffectPath = "Assets/Art/Sprites/Effects/effect_loot_rarity_ring.png";
        const string GroundSlicePath = "Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png";
        const string HealthBarBackgroundPath = "Assets/Art/Sprites/UI/Phase5/world_health_bar_background.png";
        const string HealthBarFillPath = "Assets/Art/Sprites/UI/Phase5/world_health_bar_fill.png";
        const string PrototypeSquarePath = "Assets/Art/Textures/Prototype/PrototypeSquare.png";

        static readonly string[] ActorRoots =
        {
            "Assets/Art/Sprites/Characters/Player",
            "Assets/Art/Sprites/Monsters/Basic",
            "Assets/Art/Sprites/Monsters/Swift",
            "Assets/Art/Sprites/Monsters/Heavy",
            "Assets/Art/Sprites/NPCs/Merchant",
        };

        static readonly string[] UiIconPaths =
        {
            $"{UiIconRoot}/ui_icon_health.png",
            $"{UiIconRoot}/ui_icon_gold.png",
            $"{UiIconRoot}/ui_icon_weapon.png",
            $"{UiIconRoot}/ui_icon_armor.png",
            $"{UiIconRoot}/ui_icon_ring.png",
            $"{UiIconRoot}/ui_icon_inventory.png",
            $"{UiIconRoot}/ui_icon_shop.png",
            $"{UiIconRoot}/ui_icon_crafting.png",
            $"{UiIconRoot}/ui_icon_close.png",
            $"{UiIconRoot}/ui_icon_back.png",
            $"{UiIconRoot}/ui_icon_weapon_empty.png",
            $"{UiIconRoot}/ui_icon_missing.png",
        };

        static readonly string[] UiFramePaths =
        {
            $"{UiFrameRoot}/ui_inventory_panel_base.png",
            $"{UiFrameRoot}/ui_inventory_slot_default.png",
            $"{UiFrameRoot}/ui_inventory_slot_focused.png",
            $"{UiFrameRoot}/ui_inventory_slot_disabled.png",
            $"{UiFrameRoot}/ui_item_window_base.png",
            $"{UiFrameRoot}/ui_hud_resource_base.png",
            $"{UiFrameRoot}/ui_control_base.png",
            $"{UiFrameRoot}/ui_slot_base.png",
        };

        static readonly string[] WorldPropPaths =
        {
            $"{WorldPropRoot}/world_prop_ground_cracked.png",
            $"{WorldPropRoot}/world_prop_ground_dirt.png",
            $"{WorldPropRoot}/world_prop_ground_stone_path.png",
            $"{WorldPropRoot}/world_prop_ruined_wall.png",
            $"{WorldPropRoot}/world_prop_rock_small.png",
            $"{WorldPropRoot}/world_prop_rock_large.png",
            $"{WorldPropRoot}/world_prop_dead_tree.png",
            $"{WorldPropRoot}/world_prop_tattered_banner.png",
            $"{WorldPropRoot}/world_prop_supplies.png",
            $"{WorldPropRoot}/world_prop_brazier.png",
            $"{WorldPropRoot}/world_prop_crafting_station.png",
            $"{WorldPropRoot}/world_prop_tent.png",
        };

        static readonly string[] UsedWorldPropPaths =
        {
            $"{WorldPropRoot}/world_prop_rock_small.png",
            $"{WorldPropRoot}/world_prop_rock_large.png",
            $"{WorldPropRoot}/world_prop_dead_tree.png",
            $"{WorldPropRoot}/world_prop_tattered_banner.png",
            $"{WorldPropRoot}/world_prop_supplies.png",
            $"{WorldPropRoot}/world_prop_brazier.png",
            $"{WorldPropRoot}/world_prop_crafting_station.png",
            $"{WorldPropRoot}/world_prop_tent.png",
        };

        static readonly string[] LegacySheetPaths =
        {
            "Assets/Art/SpriteSheets/Phase5/player_sheet.png",
            "Assets/Art/SpriteSheets/Phase5/monster_basic_sheet.png",
            "Assets/Art/SpriteSheets/Phase5/monster_swift_sheet.png",
            "Assets/Art/SpriteSheets/Phase5/monster_heavy_sheet.png",
            "Assets/Art/SpriteSheets/Phase5/merchant_idle_sheet.png",
            "Assets/Art/SpriteSheets/Phase5/world_props_sheet.png",
            "Assets/Art/SpriteSheets/Phase5/ui_icons_sheet.png",
            "Assets/Art/SpriteSheets/Phase5/ui_frames_sheet.png",
        };

        static readonly Regex ActorFrameNamePattern = new Regex(
            "^(?:actor_player|monster_(?:basic|swift|heavy)|npc_merchant)_(?:idle|move|attack|hit|death)_se_[0-9]{2}\\.png$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [Test]
        public void OfficialItems_HaveUniqueProductionReadyIcons()
        {
            List<ItemBaseDefinition> items = LoadAssets<ItemBaseDefinition>(ItemRoot);
            HashSet<string> iconGuids = new HashSet<string>();

            Assert.AreEqual(7, items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                ItemBaseDefinition item = items[i];
                Assert.IsNotNull(item.Icon, $"{item.Id} 缺少图标引用");
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Icon.AssetGUID), $"{item.Id} 缺少图标 GUID");
                Assert.IsTrue(iconGuids.Add(item.Icon.AssetGUID), $"{item.Id} 与其他装备共用了图标");

                string path = AssetDatabase.GUIDToAssetPath(item.Icon.AssetGUID);
                Assert.That(
                    path,
                    Does.StartWith("Assets/Art/Sprites/Items/Equipment/"),
                    $"{item.Id} 未引用独立装备图标");
                AssertSpriteContract(path, 96, 96, 64f, new Vector2(0.5f, 0.5f), Vector4.zero);
                AssertSpriteCanvasAndAlphaBounds(path, 5, 72, 80);
            }
        }

        [Test]
        public void ProductionArt_UsesOneSingleSpritePerPng()
        {
            string[] paths = FindPngAssetPaths(ProductionSpriteRoot);

            Assert.IsNotEmpty(paths);

            for (int i = 0; i < paths.Length; i++)
            {
                AssertSingleSpriteContract(paths[i]);
            }
        }

        [Test]
        public void ActorFrames_UseFrozen128PixelContract()
        {
            string[] paths = FindPngAssetPaths(ActorRoots);

            Assert.AreEqual(52, paths.Length);

            for (int i = 0; i < paths.Length; i++)
            {
                Assert.IsTrue(
                    ActorFrameNamePattern.IsMatch(Path.GetFileName(paths[i])),
                    $"角色帧命名不符合单图规范: {paths[i]}");
                AssertSpriteContract(
                    paths[i],
                    128,
                    128,
                    64f,
                    new Vector2(0.5f, 12f / 128f),
                    Vector4.zero);
            }
        }

        [Test]
        public void FixedUiIconsAndProjectile_Use96PixelContract()
        {
            CollectionAssert.AreEquivalent(UiIconPaths, FindPngAssetPaths(UiIconRoot));

            for (int i = 0; i < UiIconPaths.Length; i++)
            {
                AssertSpriteContract(
                    UiIconPaths[i],
                    96,
                    96,
                    64f,
                    new Vector2(0.5f, 0.5f),
                    Vector4.zero);
            }

            AssertSpriteContract(
                ProjectilePath,
                96,
                96,
                64f,
                new Vector2(0.5f, 0.5f),
                Vector4.zero);
            AssertSpriteContract(
                LootRarityEffectPath,
                96,
                96,
                100f,
                new Vector2(0.5f, 0.5f),
                Vector4.zero);
        }

        [Test]
        public void WorldProps_UseFrozen384PixelContract()
        {
            CollectionAssert.AreEquivalent(WorldPropPaths, FindPngAssetPaths(WorldPropRoot));

            for (int i = 0; i < WorldPropPaths.Length; i++)
            {
                AssertSpriteContract(
                    WorldPropPaths[i],
                    384,
                    384,
                    181f,
                    new Vector2(0.5f, 0.5f),
                    Vector4.zero);
            }
        }

        [Test]
        public void UiFrames_UseExactCanvasAndNineSliceContracts()
        {
            CollectionAssert.AreEquivalent(UiFramePaths, FindPngAssetPaths(UiFrameRoot));
            AssertSpriteContract(
                UiFramePaths[0],
                534,
                527,
                100f,
                new Vector2(0.5f, 0.5f),
                new Vector4(98f, 98f, 98f, 98f));
            AssertSpriteContract(
                UiFramePaths[1],
                436,
                465,
                100f,
                new Vector2(0.5f, 0.5f),
                new Vector4(78f, 78f, 78f, 78f));
            AssertSpriteContract(
                UiFramePaths[2],
                479,
                469,
                100f,
                new Vector2(0.5f, 0.5f),
                new Vector4(157f, 157f, 157f, 157f));
            foreach (string path in new[] { UiFramePaths[4], UiFramePaths[5] })
            {
                AssertSpriteContract(path, 1254, 1254, 100f,
                    new Vector2(0.5f, 0.5f), new Vector4(96f, 96f, 96f, 96f));
            }
            foreach (string path in new[] { UiFramePaths[6], UiFramePaths[7] })
            {
                AssertSpriteContract(path, 1254, 1254, 100f,
                    new Vector2(0.5f, 0.5f), new Vector4(32f, 32f, 32f, 32f));
            }
            AssertSpriteContract(
                UiFramePaths[3],
                436,
                469,
                100f,
                new Vector2(0.5f, 0.5f),
                new Vector4(157f, 157f, 157f, 157f));
        }

        [Test]
        public void SupportingProductionSprites_UseFrozenCanvasContracts()
        {
            AssertSpriteContract(
                GroundSlicePath,
                512,
                512,
                64f,
                new Vector2(0.5f, 0.5f),
                Vector4.zero);
            AssertSpriteContract(
                HealthBarBackgroundPath,
                64,
                8,
                64f,
                new Vector2(0.5f, 0.5f),
                Vector4.zero);
            AssertSpriteContract(
                HealthBarFillPath,
                64,
                8,
                64f,
                new Vector2(0.5f, 0.5f),
                Vector4.zero);
        }

        [Test]
        public void ProductionPrefabRootScales_PreserveHistoricalRuntimeExceptions()
        {
            AssertPrefabScale("Assets/Prefabs/Combat/Player.prefab", new Vector3(0.8f, 0.8f, 1f));
            AssertPrefabScale("Assets/Prefabs/Combat/Monster_Basic.prefab", new Vector3(0.75f, 0.75f, 1f));
            AssertPrefabScale("Assets/Prefabs/Combat/Monster_Swift.prefab", new Vector3(0.9f, 0.9f, 0.9f));
            AssertPrefabScale("Assets/Prefabs/Combat/Monster_Heavy.prefab", new Vector3(0.75f, 0.75f, 1f));
            AssertPrefabScale("Assets/Prefabs/World/Merchant.prefab", new Vector3(0.8f, 0.8f, 0.8f));
            AssertPrefabScale("Assets/Prefabs/Combat/Projectile_Default.prefab", new Vector3(0.25f, 0.25f, 1f));
        }

        [Test]
        public void MerchantCraftingStationAndBrazier_HaveNoScaleAnimation()
        {
            const string MerchantPath = "Assets/Prefabs/World/Merchant.prefab";
            const string CraftingPath = "Assets/Prefabs/World/CraftingStation.prefab";
            GameObject merchant = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPath);
            GameObject craftingStation = AssetDatabase.LoadAssetAtPath<GameObject>(CraftingPath);

            Assert.IsNotNull(merchant);
            Assert.IsNotNull(craftingStation);
            Assert.Zero(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(merchant), MerchantPath);
            Assert.Zero(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(craftingStation), CraftingPath);
            Assert.IsEmpty(
                AssetDatabase.FindAssets("AmbientPulseVisual t:MonoScript", new[] { "Assets/Scripts" }));
            Assert.IsNull(
                typeof(WorldInteractionVisual).GetField(
                    "_pulseTween",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
                int missingScriptCount = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    missingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
                }

                Assert.Zero(missingScriptCount);
            }
            finally
            {
                RestoreSceneSetup(setup);
            }
        }

        [Test]
        public void AnimationsAndCharacterPrefabs_ReferenceIndependentFrames()
        {
            string[] actorPaths = FindPngAssetPaths(ActorRoots);
            string[] clipPaths = AssetDatabase.FindAssets("t:AnimationClip", new[] { ActorClipRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            HashSet<string> referencedActorPaths = new HashSet<string>(StringComparer.Ordinal);

            Assert.AreEqual(21, clipPaths.Length);

            for (int clipIndex = 0; clipIndex < clipPaths.Length; clipIndex++)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[clipIndex]);
                EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(
                        clip,
                        bindings[bindingIndex]);

                    for (int keyIndex = 0; keyIndex < keyframes.Length; keyIndex++)
                    {
                        if (keyframes[keyIndex].value is not Sprite sprite)
                        {
                            continue;
                        }

                        string path = AssetDatabase.GetAssetPath(sprite);
                        Assert.IsTrue(
                            ActorRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal)),
                            $"{clipPaths[clipIndex]} 仍引用非单图角色帧: {path}");
                        referencedActorPaths.Add(path);
                    }
                }
            }

            CollectionAssert.AreEquivalent(actorPaths, referencedActorPaths);
            AssertPrefabRootSpritePath(
                "Assets/Prefabs/Combat/Player.prefab",
                "Assets/Art/Sprites/Characters/Player/actor_player_idle_se_00.png");
            AssertPrefabRootSpritePath(
                "Assets/Prefabs/Combat/Monster_Basic.prefab",
                "Assets/Art/Sprites/Monsters/Basic/monster_basic_idle_se_00.png");
            AssertPrefabRootSpritePath(
                "Assets/Prefabs/Combat/Monster_Swift.prefab",
                "Assets/Art/Sprites/Monsters/Swift/monster_swift_idle_se_00.png");
            AssertPrefabRootSpritePath(
                "Assets/Prefabs/Combat/Monster_Heavy.prefab",
                "Assets/Art/Sprites/Monsters/Heavy/monster_heavy_idle_se_00.png");
            AssertPrefabRootSpritePath(
                "Assets/Prefabs/World/Merchant.prefab",
                "Assets/Art/Sprites/NPCs/Merchant/npc_merchant_idle_se_00.png");
        }

        [Test]
        public void UiAndWorldConsumers_ReferenceIndependentSprites()
        {
            string theme = File.ReadAllText(GetAbsolutePath("Assets/UI/Theme.uss"));
            string inventory = File.ReadAllText(GetAbsolutePath("Assets/UI/Inventory.uss"));

            for (int i = 0; i < UiIconPaths.Length; i++)
            {
                if (UiIconPaths[i].EndsWith("ui_icon_back.png", StringComparison.Ordinal)
                    || UiIconPaths[i].EndsWith("ui_icon_weapon_empty.png", StringComparison.Ordinal))
                {
                    continue;
                }

                StringAssert.Contains(UiIconPaths[i], theme);
            }

            StringAssert.DoesNotContain("SpriteSheets/Phase5", theme);
            StringAssert.DoesNotContain("SpriteSheets/Phase5", inventory);

            GameObject lootPickup = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Loot/LootPickup.prefab");
            SpriteRenderer halo = lootPickup.transform.Find("Visual/Halo").GetComponent<SpriteRenderer>();
            LootPickupVisual lootVisual = lootPickup.GetComponent<LootPickupVisual>();
            SerializedObject serializedLoot = new SerializedObject(lootVisual);
            Sprite missingIcon = serializedLoot.FindProperty("_missingIcon").objectReferenceValue as Sprite;
            AssertSpritePath(halo.sprite, LootRarityEffectPath, "LootPickup/Visual/Halo");
            Assert.AreEqual(Vector3.one, halo.transform.localScale);
            AssertSpritePath(missingIcon, UiIconPaths[11], "LootPickupVisual._missingIcon");

            GameObject craftingStation = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/CraftingStation.prefab");
            SpriteRenderer[] craftingRenderers = craftingStation.GetComponentsInChildren<SpriteRenderer>(true);
            AssertSpritePath(
                craftingRenderers.Single(renderer => renderer.name == "Outline").sprite,
                $"{WorldPropRoot}/world_prop_crafting_station.png",
                "CraftingStation/Outline");
            AssertSpritePath(
                craftingRenderers.Single(renderer => renderer.name == "Fire").sprite,
                $"{WorldPropRoot}/world_prop_brazier.png",
                "CraftingStation/Fire");

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
                HashSet<string> sceneSpritePaths = new HashSet<string>(StringComparer.Ordinal);

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        if (renderers[rendererIndex].sprite != null)
                        {
                            sceneSpritePaths.Add(AssetDatabase.GetAssetPath(renderers[rendererIndex].sprite));
                        }
                    }
                }

                for (int i = 0; i < UsedWorldPropPaths.Length; i++)
                {
                    CollectionAssert.Contains(sceneSpritePaths, UsedWorldPropPaths[i]);
                }

                Assert.IsFalse(
                    sceneSpritePaths.Any(path => path.Contains("/SpriteSheets/", StringComparison.Ordinal)),
                    string.Join("\n", sceneSpritePaths));
            }
            finally
            {
                RestoreSceneSetup(setup);
            }
        }

        [Test]
        public void MonsterPrefabs_HaveDistinctVisualAssetsAndFeedbackContract()
        {
            GameObject swift = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Combat/Monster_Swift.prefab");
            GameObject heavy = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Combat/Monster_Heavy.prefab");

            AssertMonsterVisualContract(swift);
            AssertMonsterVisualContract(heavy);
            Assert.AreNotEqual(
                AssetDatabase.GetAssetPath(swift.GetComponent<SpriteRenderer>().sprite),
                AssetDatabase.GetAssetPath(heavy.GetComponent<SpriteRenderer>().sprite));
            Assert.AreNotEqual(
                AssetDatabase.GetAssetPath(swift.GetComponent<Animator>().runtimeAnimatorController),
                AssetDatabase.GetAssetPath(heavy.GetComponent<Animator>().runtimeAnimatorController));
        }

        [Test]
        public void LegacySpriteSheets_AreRetiredAndHaveNoProductionDependencies()
        {
            for (int i = 0; i < LegacySheetPaths.Length; i++)
            {
                Assert.IsFalse(AssetDatabase.AssetPathExists(LegacySheetPaths[i]), LegacySheetPaths[i]);
                Assert.IsFalse(File.Exists(GetAbsolutePath(LegacySheetPaths[i])), LegacySheetPaths[i]);
                Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(LegacySheetPaths[i]), LegacySheetPaths[i]);
            }

            List<string> consumerPaths = AssetDatabase.FindAssets(
                    "t:AnimationClip",
                    new[] { ActorClipRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();
            consumerPaths.AddRange(new[]
            {
                "Assets/Scenes/Main.unity",
                "Assets/Prefabs/Combat/Player.prefab",
                "Assets/Prefabs/Combat/Monster_Basic.prefab",
                "Assets/Prefabs/Combat/Monster_Swift.prefab",
                "Assets/Prefabs/Combat/Monster_Heavy.prefab",
                "Assets/Prefabs/Loot/LootPickup.prefab",
                "Assets/Prefabs/World/Merchant.prefab",
                "Assets/Prefabs/World/CraftingStation.prefab",
                "Assets/UI/Theme.uss",
                "Assets/UI/Inventory.uss",
            });
            string[] dependencies = AssetDatabase.GetDependencies(consumerPaths.ToArray(), true);

            for (int i = 0; i < LegacySheetPaths.Length; i++)
            {
                CollectionAssert.DoesNotContain(dependencies, LegacySheetPaths[i]);
            }

            Assert.IsTrue(AssetDatabase.AssetPathExists(PrototypeSquarePath));
        }

        [Test]
        public void ProductionSceneAndPrefabs_DoNotDependOnPrototypeSquare()
        {
            string[] roots =
            {
                "Assets/Scenes/Main.unity",
                "Assets/Prefabs/Combat/Player.prefab",
                "Assets/Prefabs/Combat/Monster_Basic.prefab",
                "Assets/Prefabs/Combat/Monster_Swift.prefab",
                "Assets/Prefabs/Combat/Monster_Heavy.prefab",
                "Assets/Prefabs/Combat/Projectile_Default.prefab",
                "Assets/Prefabs/Loot/LootPickup.prefab",
                "Assets/Prefabs/World/Merchant.prefab",
                "Assets/Prefabs/World/CraftingStation.prefab",
            };
            string[] dependencies = AssetDatabase.GetDependencies(roots, true);

            Assert.IsFalse(
                dependencies.Any(path => path.Contains("PrototypeSquare", StringComparison.Ordinal)),
                string.Join("\n", dependencies.Where(path => path.Contains("PrototypeSquare", StringComparison.Ordinal))));
        }

        [Test]
        public void ItemVisualPresenter_ClearsInlineBackgroundForThemeFallback()
        {
            VisualElement icon = new VisualElement();
            icon.style.backgroundImage = new StyleBackground(StyleKeyword.None);

            ItemVisualPresenter.ApplyIcon(icon, string.Empty);

            Assert.AreEqual(StyleKeyword.Null, icon.style.backgroundImage.keyword);
        }

        [UnityTest]
        public IEnumerator SpriteAssetLoader_DeduplicatesCachesCancelsAndReleasesSafely()
        {
            UnityEngine.Object addressableSettings = AssetDatabase.LoadMainAssetAtPath(AddressableSettingsPath);
            Assert.IsNotNull(addressableSettings);
            SerializedObject serializedSettings = new SerializedObject(addressableSettings);
            Assert.AreEqual(
                0f,
                serializedSettings.FindProperty("m_simulatedLoadDelay").floatValue,
                "EditMode 下 Fast Mode 的模拟延迟必须为 0，否则 Addressables 的延迟调度不会推进。");

            AsyncOperationHandle<IResourceLocator> initializationHandle = Addressables.InitializeAsync(false);
            Assert.IsNotNull(initializationHandle.WaitForCompletion());

            ItemBaseDefinition item = LoadAssets<ItemBaseDefinition>(ItemRoot)[0];
            SpriteAssetLoader loader = new SpriteAssetLoader();

            yield return loader.PreloadAsync(
                    new[] { item.Icon, item.Icon },
                    CancellationToken.None)
                .ToCoroutine();

            Sprite first = loader.GetSprite(item.Icon.AssetGUID);
            Assert.IsNotNull(first);

            yield return loader.PreloadAsync(
                    new[] { item.Icon },
                    CancellationToken.None)
                .ToCoroutine();

            Assert.AreSame(first, loader.GetSprite(item.Icon.AssetGUID));
            loader.ReleaseAll();
            loader.ReleaseAll();
            LogAssert.Expect(
                LogType.Warning,
                new Regex("图标未预热或加载失败"));
            Assert.IsNull(loader.GetSprite(item.Icon.AssetGUID));

            SpriteAssetLoader cancelledLoader = new SpriteAssetLoader();
            CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool cancelled = false;

            yield return cancelledLoader.PreloadAsync(
                    new[] { item.Icon },
                    cancellation.Token)
                .SuppressCancellationThrow()
                .ToCoroutine(value => cancelled = value);

            Assert.IsTrue(cancelled);
            cancelledLoader.ReleaseAll();
            cancellation.Dispose();
            Addressables.Release(initializationHandle);
        }

        static void AssertMonsterVisualContract(GameObject prefab)
        {
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<SpriteRenderer>());
            Assert.IsNotNull(prefab.GetComponent<MonsterController>());
            Assert.IsNotNull(prefab.GetComponent<ActorVisualFeedbackController>());
            Assert.IsNotNull(prefab.GetComponent<MonsterHealthBarVisual>());
            Animator animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.IsNotNull(controller);
            Dictionary<string, AnimatorControllerParameterType> parameters = controller.parameters
                .ToDictionary(parameter => parameter.name, parameter => parameter.type);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, parameters["Moving"]);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, parameters["Attack"]);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, parameters["Hit"]);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, parameters["Death"]);
        }

        static void AssertSpriteCanvasAndAlphaBounds(
            string assetPath,
            int minimumMargin,
            int minimumExtent,
            int maximumExtent)
        {
            Texture2D texture = LoadReadableTexture(assetPath);
            RectInt bounds = GetAlphaBounds(texture);

            Assert.AreEqual(96, texture.width, assetPath);
            Assert.AreEqual(96, texture.height, assetPath);
            Assert.That(Mathf.Max(bounds.width, bounds.height), Is.InRange(minimumExtent, maximumExtent), assetPath);
            Assert.GreaterOrEqual(bounds.xMin, minimumMargin, assetPath);
            Assert.GreaterOrEqual(bounds.yMin, minimumMargin, assetPath);
            Assert.GreaterOrEqual(texture.width - bounds.xMax, minimumMargin, assetPath);
            Assert.GreaterOrEqual(texture.height - bounds.yMax, minimumMargin, assetPath);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void AssertSingleSpriteContract(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();

            Assert.IsNotNull(importer, assetPath);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, assetPath);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, assetPath);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, assetPath);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, assetPath);
            Assert.IsFalse(importer.mipmapEnabled, assetPath);
            Assert.IsTrue(importer.sRGBTexture, assetPath);
            Assert.AreEqual(
                assetPath == GroundSlicePath ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                importer.wrapMode,
                assetPath);

            if (assetPath != GroundSlicePath
                && !assetPath.StartsWith(GroundBaseRoot, StringComparison.Ordinal))
            {
                Assert.IsTrue(importer.alphaIsTransparency, assetPath);
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType, assetPath);
            Assert.AreEqual(1, sprites.Length, assetPath);
            Assert.IsTrue(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    sprites[0],
                    out string _,
                    out long localId),
                assetPath);
            Assert.AreEqual(21300000L, localId, assetPath);
        }

        static void AssertSpriteContract(
            string assetPath,
            int expectedWidth,
            int expectedHeight,
            float expectedPixelsPerUnit,
            Vector2 expectedPivot,
            Vector4 expectedBorder)
        {
            AssertSingleSpriteContract(assetPath);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            Assert.IsNotNull(sprite, assetPath);
            Assert.AreEqual(expectedWidth, width, assetPath);
            Assert.AreEqual(expectedHeight, height, assetPath);
            Assert.AreEqual(expectedPixelsPerUnit, importer.spritePixelsPerUnit, 0.001f, assetPath);
            Assert.AreEqual(expectedPivot.x, importer.spritePivot.x, 0.0001f, assetPath);
            Assert.AreEqual(expectedPivot.y, importer.spritePivot.y, 0.0001f, assetPath);
            AssertVector4(expectedBorder, importer.spriteBorder, assetPath);
            Assert.AreEqual(expectedWidth, sprite.rect.width, 0.001f, assetPath);
            Assert.AreEqual(expectedHeight, sprite.rect.height, 0.001f, assetPath);
            Assert.AreEqual(expectedWidth * expectedPivot.x, sprite.pivot.x, 0.001f, assetPath);
            Assert.AreEqual(expectedHeight * expectedPivot.y, sprite.pivot.y, 0.001f, assetPath);
            Assert.AreEqual(expectedPixelsPerUnit, sprite.pixelsPerUnit, 0.001f, assetPath);
        }

        static void AssertVector4(Vector4 expected, Vector4 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f, message);
            Assert.AreEqual(expected.y, actual.y, 0.001f, message);
            Assert.AreEqual(expected.z, actual.z, 0.001f, message);
            Assert.AreEqual(expected.w, actual.w, 0.001f, message);
        }

        static void AssertPrefabScale(string assetPath, Vector3 expectedScale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            Assert.IsNotNull(prefab, assetPath);
            Assert.AreEqual(expectedScale.x, prefab.transform.localScale.x, 0.001f, assetPath);
            Assert.AreEqual(expectedScale.y, prefab.transform.localScale.y, 0.001f, assetPath);
            Assert.AreEqual(expectedScale.z, prefab.transform.localScale.z, 0.001f, assetPath);
        }

        static void AssertPrefabRootSpritePath(string prefabPath, string expectedSpritePath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.IsNotNull(prefab, prefabPath);
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, prefabPath);
            AssertSpritePath(renderer.sprite, expectedSpritePath, prefabPath);
        }

        static void AssertSpritePath(Sprite sprite, string expectedPath, string message)
        {
            Assert.IsNotNull(sprite, message);
            Assert.AreEqual(expectedPath, AssetDatabase.GetAssetPath(sprite), message);
        }

        static string[] FindPngAssetPaths(params string[] roots)
        {
            return AssetDatabase.FindAssets("t:Texture2D", roots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        static void RestoreSceneSetup(SceneSetup[] setup)
        {
            if (setup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static Texture2D LoadReadableTexture(string assetPath)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(GetAbsolutePath(assetPath))), assetPath);
            return texture;
        }

        static RectInt GetAlphaBounds(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int xMin = texture.width;
            int yMin = texture.height;
            int xMax = -1;
            int yMax = -1;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a <= 8)
                    {
                        continue;
                    }

                    xMin = Mathf.Min(xMin, x);
                    yMin = Mathf.Min(yMin, y);
                    xMax = Mathf.Max(xMax, x);
                    yMax = Mathf.Max(yMax, y);
                }
            }

            Assert.GreaterOrEqual(xMax, xMin);
            Assert.GreaterOrEqual(yMax, yMin);
            return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        static string GetAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            return Path.Combine(projectRoot, assetPath);
        }

        static List<T> LoadAssets<T>(string root) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root });
            List<T> result = new List<T>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }
    }
}
