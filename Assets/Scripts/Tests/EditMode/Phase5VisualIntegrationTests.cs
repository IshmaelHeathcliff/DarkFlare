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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public class Phase5VisualIntegrationTests
    {
        const string ItemRoot = "Assets/Data/Preset/Items";

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
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(sprite, $"{item.Id} 的图标不是 Sprite: {path}");
                Assert.IsNotNull(importer, $"{item.Id} 缺少 TextureImporter: {path}");
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                Assert.AreEqual(64f, importer.spritePixelsPerUnit, 0.001f, path);
                Assert.IsTrue(importer.alphaIsTransparency, path);
                AssertSpriteCanvasAndAlphaBounds(path, 6, 72, 80);
            }
        }

        [Test]
        public void GeneratedArt_UsesEditableSpriteSheetsAndExpectedWorldScale()
        {
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/player_sheet.png", 12);
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/monster_basic_sheet.png", 12);
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/monster_swift_sheet.png", 12);
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/monster_heavy_sheet.png", 12);
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/merchant_idle_sheet.png", 4);
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/world_props_sheet.png", 12);
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/ui_icons_sheet.png", 12);
            AssertSpriteSheet("Assets/Art/SpriteSheets/Phase5/ui_frames_sheet.png", 4);
            AssertUniformSpriteGrid("Assets/Art/SpriteSheets/Phase5/player_sheet.png", 4, 3, 192f);
            AssertUniformSpriteGrid("Assets/Art/SpriteSheets/Phase5/monster_basic_sheet.png", 4, 3, 192f);

            AssertUniformPrefabScale("Assets/Prefabs/Combat/Monster_Swift.prefab", 0.9f);
            AssertUniformPrefabScale("Assets/Prefabs/Combat/Monster_Heavy.prefab", 0.75f);
            AssertUniformPrefabScale("Assets/Prefabs/World/Merchant.prefab", 0.8f);
        }

        [Test]
        public void WorldPropsSheet_BrazierAndCraftingStationUseCorrectedTightRects()
        {
            const string SheetPath = "Assets/Art/SpriteSheets/Phase5/world_props_sheet.png";
            Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);

            Assert.AreEqual(new Rect(404f, 72f, 207f, 225f), sprites["world_props_sheet_13"].rect);
            Assert.AreEqual(new Rect(678f, 45f, 359f, 296f), sprites["world_props_sheet_14"].rect);
        }

        [Test]
        public void UiFrameSheet_UsesCorrectedTightRects()
        {
            const string SheetPath = "Assets/Art/SpriteSheets/Phase5/ui_frames_sheet.png";
            Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);

            Assert.AreEqual(new Rect(79f, 648f, 534f, 527f), sprites["ui_frames_sheet_0"].rect);
            Assert.AreEqual(new Rect(693f, 679f, 436f, 465f), sprites["ui_frames_sheet_1"].rect);
            Assert.AreEqual(new Rect(105f, 110f, 479f, 469f), sprites["ui_frames_sheet_2"].rect);
            Assert.AreEqual(new Rect(693f, 110f, 436f, 469f), sprites["ui_frames_sheet_3"].rect);
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
                if (setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        [Test]
        public void InventoryNineSlice_UsesPerAssetSafeInsets()
        {
            string uss = File.ReadAllText(GetAbsolutePath("Assets/UI/Inventory.uss"));

            StringAssert.Contains("-unity-slice-left: 98;", uss);
            StringAssert.Contains("-unity-slice-left: 157;", uss);
            StringAssert.Contains("-unity-slice-left: 78;", uss);
            StringAssert.DoesNotContain("-unity-slice-left: 40;", uss);
            StringAssert.DoesNotContain("-unity-slice-left: 16;", uss);
            StringAssert.DoesNotContain("-unity-slice-left: 8;", uss);
            StringAssert.DoesNotContain("-unity-slice-left: 28;", uss);
            StringAssert.DoesNotContain("-unity-slice-left: 10;", uss);
        }

        [Test]
        public void CharactersAndUi_ReferenceSpriteSheetsInsteadOfDerivedSingles()
        {
            string[] characterDependencies = AssetDatabase.GetDependencies(
                new[]
                {
                    "Assets/Prefabs/Combat/Player.prefab",
                    "Assets/Prefabs/Combat/Monster_Basic.prefab",
                },
                true);
            string[] uiDependencies = AssetDatabase.GetDependencies(
                new[]
                {
                    "Assets/UI/Theme.uss",
                    "Assets/UI/Inventory.uss",
                    "Assets/Prefabs/Loot/LootPickup.prefab",
                },
                true);

            CollectionAssert.Contains(
                characterDependencies,
                "Assets/Art/SpriteSheets/Phase5/player_sheet.png");
            CollectionAssert.Contains(
                characterDependencies,
                "Assets/Art/SpriteSheets/Phase5/monster_basic_sheet.png");
            CollectionAssert.Contains(
                uiDependencies,
                "Assets/Art/SpriteSheets/Phase5/ui_icons_sheet.png");
            CollectionAssert.Contains(
                uiDependencies,
                "Assets/Art/SpriteSheets/Phase5/ui_frames_sheet.png");
            Assert.IsFalse(
                characterDependencies.Any(path => path.Contains("Assets/Art/Sprites/Characters/Player/")));
            Assert.IsFalse(
                characterDependencies.Any(path => path.Contains("Assets/Art/Sprites/Characters/Monsters/Basic/")));
            Assert.IsFalse(
                uiDependencies.Any(path => path.Contains("Assets/Art/Sprites/UI/Phase5/ui_")));

            GameObject craftingStation = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/CraftingStation.prefab");
            SpriteRenderer[] craftingRenderers = craftingStation.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.AreEqual("world_props_sheet_14", craftingRenderers.Single(renderer => renderer.name == "Outline").sprite.name);
            Assert.AreEqual("world_props_sheet_13", craftingRenderers.Single(renderer => renderer.name == "Fire").sprite.name);

            GameObject lootPickup = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Loot/LootPickup.prefab");
            SpriteRenderer halo = lootPickup.transform.Find("Visual/Halo").GetComponent<SpriteRenderer>();
            LootPickupVisual lootVisual = lootPickup.GetComponent<LootPickupVisual>();
            SerializedObject serializedLoot = new SerializedObject(lootVisual);
            Sprite missingIcon = serializedLoot.FindProperty("_missingIcon").objectReferenceValue as Sprite;
            Assert.AreEqual("ui_frames_sheet_2", halo.sprite.name);
            Assert.AreEqual("ui_icons_sheet_11", missingIcon.name);
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
                dependencies.Any(path => path.Contains("PrototypeSquare")),
                string.Join("\n", dependencies.Where(path => path.Contains("PrototypeSquare"))));
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
                LogType.Error,
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
            Object.DestroyImmediate(texture);
        }

        static void AssertSpriteSheet(string assetPath, int expectedSpriteCount)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();

            Assert.IsNotNull(importer, assetPath);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode, assetPath);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, assetPath);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, assetPath);
            Assert.IsTrue(importer.alphaIsTransparency, assetPath);
            Assert.AreEqual(expectedSpriteCount, sprites.Length, assetPath);
        }

        static void AssertUniformPrefabScale(string assetPath, float expectedScale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            Assert.IsNotNull(prefab, assetPath);
            Assert.AreEqual(expectedScale, prefab.transform.localScale.x, 0.001f, assetPath);
            Assert.AreEqual(prefab.transform.localScale.x, prefab.transform.localScale.y, 0.001f, assetPath);
        }

        static void AssertUniformSpriteGrid(
            string assetPath,
            int columns,
            int rows,
            float expectedPixelsPerUnit)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();

            Assert.IsNotNull(importer, assetPath);
            Assert.IsNotNull(texture, assetPath);
            Assert.AreEqual(expectedPixelsPerUnit, importer.spritePixelsPerUnit, 0.001f, assetPath);
            Assert.Zero(texture.width % columns, assetPath);
            Assert.Zero(texture.height % rows, assetPath);

            float cellWidth = texture.width / columns;
            float cellHeight = texture.height / rows;

            for (int i = 0; i < sprites.Length; i++)
            {
                Rect rect = sprites[i].rect;
                Assert.AreEqual(cellWidth, rect.width, 0.001f, sprites[i].name);
                Assert.AreEqual(cellHeight, rect.height, 0.001f, sprites[i].name);
                Assert.AreEqual(0f, rect.x % cellWidth, 0.001f, sprites[i].name);
                Assert.AreEqual(0f, rect.y % cellHeight, 0.001f, sprites[i].name);
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

        static List<T> LoadAssets<T>(string root) where T : Object
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
