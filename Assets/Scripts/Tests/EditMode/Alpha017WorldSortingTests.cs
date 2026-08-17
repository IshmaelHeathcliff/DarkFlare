using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha017WorldSortingTests
    {
        static readonly string[] WorldObjectPrefabPaths =
        {
            "Assets/Prefabs/Combat/Player.prefab",
            "Assets/Prefabs/Combat/Monster_Basic.prefab",
            "Assets/Prefabs/Combat/Monster_Swift.prefab",
            "Assets/Prefabs/Combat/Monster_Heavy.prefab",
            "Assets/Prefabs/World/Merchant.prefab",
            "Assets/Prefabs/World/CraftingStation.prefab",
            "Assets/Prefabs/Loot/LootPickup.prefab",
        };

        readonly List<GameObject> _objects = new List<GameObject>();

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
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            _architecture?.Deinit();
            _architecture = null;
        }

        [Test]
        public void SortKey_QuantizesYAndUsesFrozenTieBreakerOrder()
        {
            Assert.AreEqual(64, WorldSortingSystem.QuantizeY(1.001f));
            Assert.AreEqual(65, WorldSortingSystem.QuantizeY(1.009f));

            WorldSortKey higher = WorldSortingSystem.CreateKey(2f, WorldSortCategory.Player, "player");
            WorldSortKey lower = WorldSortingSystem.CreateKey(1f, WorldSortCategory.StaticProp, "prop");
            Assert.Less(higher.CompareTo(lower), 0, "世界高处应先分配较小 Order");

            WorldSortKey staticProp = WorldSortingSystem.CreateKey(1f, WorldSortCategory.StaticProp, "z");
            WorldSortKey interactable = WorldSortingSystem.CreateKey(1f, WorldSortCategory.Interactable, "a");
            WorldSortKey loot = WorldSortingSystem.CreateKey(1f, WorldSortCategory.Loot, "a");
            WorldSortKey monster = WorldSortingSystem.CreateKey(1f, WorldSortCategory.Monster, "a");
            WorldSortKey player = WorldSortingSystem.CreateKey(1f, WorldSortCategory.Player, "a");
            Assert.Less(staticProp.CompareTo(interactable), 0);
            Assert.Less(interactable.CompareTo(loot), 0);
            Assert.Less(loot.CompareTo(monster), 0);
            Assert.Less(monster.CompareTo(player), 0);
            Assert.Less(
                WorldSortingSystem.CreateKey(1f, WorldSortCategory.Monster, "monster_a").CompareTo(
                    WorldSortingSystem.CreateKey(1f, WorldSortCategory.Monster, "monster_b")),
                0);
        }

        [Test]
        public void SortingSystem_UpdatesOnlyAfterQuantizedMovementAndAssignsUniqueOrders()
        {
            WorldSortingSystem system = _architecture.GetUtility<WorldSortingSystem>();
            WorldSortParticipant higher = CreateParticipant("higher", WorldSortCategory.Monster, 2f);
            WorldSortParticipant lower = CreateParticipant("lower", WorldSortCategory.Monster, 1f);
            system.Tick();

            Assert.AreEqual(0, higher.SortingGroup.sortingOrder);
            Assert.AreEqual(1, lower.SortingGroup.sortingOrder);
            int initialOrder = higher.SortingGroup.sortingOrder;

            higher.transform.position += new Vector3(0f, 0.001f, 0f);
            system.Tick();
            Assert.AreEqual(initialOrder, higher.SortingGroup.sortingOrder);

            lower.transform.position = new Vector3(0f, 3f, 0f);
            system.Tick();
            Assert.AreEqual(0, lower.SortingGroup.sortingOrder);
            Assert.AreEqual(1, higher.SortingGroup.sortingOrder);
        }

        [Test]
        public void SortingSystem_UnchangedKeysAllocateNoManagedMemory()
        {
            WorldSortingSystem system = _architecture.GetUtility<WorldSortingSystem>();
            CreateParticipant("allocation_a", WorldSortCategory.StaticProp, 2f);
            CreateParticipant("allocation_b", WorldSortCategory.Monster, 1f);
            system.Tick();

            for (int i = 0; i < 64; i++)
            {
                system.Tick();
            }

            long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1024; i++)
            {
                system.Tick();
            }

            long allocatedAfter = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, allocatedAfter - allocatedBefore, "排序键未变化时不得产生托管分配");
        }

        [Test]
        public void SortingSystem_RejectsDuplicateOrEmptyStableIdsAndReleasesCleanly()
        {
            WorldSortingSystem system = _architecture.GetUtility<WorldSortingSystem>();
            WorldSortParticipant first = CreateParticipant("stable", WorldSortCategory.StaticProp, 0f);
            WorldSortParticipant duplicate = CreateParticipant("duplicate", WorldSortCategory.StaticProp, 0f, false);
            duplicate.ConfigureIdentity(WorldSortCategory.StaticProp, "stable");
            LogAssert.Expect(LogType.Warning, "[WorldSortingSystem] StableSortId 重复：stable");
            Assert.IsFalse(system.Register(duplicate));
            WorldSortParticipant empty = CreateParticipant("empty", WorldSortCategory.StaticProp, 0f, false);
            empty.ConfigureIdentity(WorldSortCategory.StaticProp, string.Empty);
            LogAssert.Expect(LogType.Warning, "[WorldSortingSystem] StableSortId 不能为空");
            Assert.IsFalse(system.Register(empty));

            Assert.IsNotNull(first);
            Assert.AreEqual(1, system.ParticipantCount);
            system.ReleaseAll();
            Assert.AreEqual(0, system.ParticipantCount);
        }

        [Test]
        public void WorldObjectPrefabs_HaveOneGroupParticipantAnchorAndNoDefaultRenderer()
        {
            for (int i = 0; i < WorldObjectPrefabPaths.Length; i++)
            {
                string path = WorldObjectPrefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                SortingGroup[] groups = prefab.GetComponentsInChildren<SortingGroup>(true);
                WorldSortParticipant[] participants = prefab.GetComponentsInChildren<WorldSortParticipant>(true);
                Assert.AreEqual(1, groups.Length, path);
                Assert.AreEqual(1, participants.Length, path);
                Assert.IsNotNull(participants[0].SortAnchor, path);
                Assert.IsFalse(string.IsNullOrWhiteSpace(participants[0].StableSortId), path);
                Assert.AreEqual("WorldObject", groups[0].sortingLayerName, path);
                SerializedObject participant = new SerializedObject(participants[0]);
                bool expectedRuntimeIdentity = i <= 3 || i == WorldObjectPrefabPaths.Length - 1;
                Assert.AreEqual(
                    expectedRuntimeIdentity,
                    participant.FindProperty("_requiresRuntimeIdentity").boolValue,
                    path);

                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Assert.AreNotEqual("Default", renderers[rendererIndex].sortingLayerName, path);
                }
            }
        }

        [Test]
        public void DedicatedEffectPrefabs_UseFrozenLayersAndOrders()
        {
            AssertRendererContract("Assets/Prefabs/Combat/Projectile_Default.prefab", "WorldEffect", 0);
            AssertRendererContract("Assets/Prefabs/Effects/Projectile_Arcane_Impact.prefab", "WorldEffect", 10);
            AssertRendererContract("Assets/Prefabs/Effects/Actor_Hit_Default.prefab", "WorldEffect", 10);
            AssertRendererContract("Assets/Prefabs/Effects/Actor_Hit_Critical.prefab", "WorldEffect", 10);
        }

        [Test]
        public void WorldObjectPrefabs_UseFrozenInternalOrders()
        {
            AssertInternalOrder("Assets/Prefabs/Combat/Player.prefab", "Player", 0);

            string[] monsterPaths =
            {
                "Assets/Prefabs/Combat/Monster_Basic.prefab",
                "Assets/Prefabs/Combat/Monster_Swift.prefab",
                "Assets/Prefabs/Combat/Monster_Heavy.prefab",
            };

            for (int i = 0; i < monsterPaths.Length; i++)
            {
                AssertInternalOrder(monsterPaths[i], "HealthBar", 20);
                AssertInternalOrder(monsterPaths[i], "Fill", 21);
                AssertInternalOrder(monsterPaths[i], "AffixLabel", 30);
            }

            AssertInternalOrder("Assets/Prefabs/World/Merchant.prefab", "Merchant", 0);
            AssertInternalOrder("Assets/Prefabs/World/Merchant.prefab", "Outline", -10);
            AssertInternalOrder("Assets/Prefabs/World/Merchant.prefab", "Nameplate", 30);
            AssertInternalOrder("Assets/Prefabs/World/Merchant.prefab", "FocusPrompt", 30);
            AssertInternalOrder("Assets/Prefabs/World/CraftingStation.prefab", "CraftingStation", 0);
            AssertInternalOrder("Assets/Prefabs/World/CraftingStation.prefab", "Outline", -10);
            AssertInternalOrder("Assets/Prefabs/World/CraftingStation.prefab", "Fire", 10);
            AssertInternalOrder("Assets/Prefabs/World/CraftingStation.prefab", "Nameplate", 30);
            AssertInternalOrder("Assets/Prefabs/World/CraftingStation.prefab", "FocusPrompt", 30);
            AssertInternalOrder("Assets/Prefabs/Loot/LootPickup.prefab", "Halo", -20);
            AssertInternalOrder("Assets/Prefabs/Loot/LootPickup.prefab", "Icon", 0);
            AssertInternalOrder("Assets/Prefabs/Loot/LootPickup.prefab", "Label", 30);
        }

        [Test]
        public void MainScene_HasFrozenGroundAndWorldObjectContracts()
        {
            const string mainScenePath = "Assets/Scenes/Main.unity";
            Scene scene = SceneManager.GetSceneByPath(mainScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;

            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(mainScenePath, OpenSceneMode.Additive);
            }

            try
            {
                WorldSortingRunner[] runners = FindComponentsInScene<WorldSortingRunner>(scene);
                Assert.AreEqual(1, runners.Length);
                MonoBehaviour[] behaviours = FindComponentsInScene<MonoBehaviour>(scene);
                MonoBehaviour[] lights = System.Array.FindAll(
                    behaviours,
                    behaviour => behaviour.GetType().FullName == "UnityEngine.Rendering.Universal.Light2D");
                Assert.AreEqual(1, lights.Length, "Main 应只有一个全局 2D 光源");
                System.Reflection.PropertyInfo targetLayersProperty = lights[0].GetType().GetProperty("targetSortingLayers");
                Assert.IsNotNull(targetLayersProperty, "Light2D 缺少 targetSortingLayers 属性");
                int[] targetLayers = targetLayersProperty.GetValue(lights[0]) as int[];
                Assert.IsNotNull(targetLayers, "Light2D targetSortingLayers 无法读取");
                HashSet<int> lightLayers = new HashSet<int>(targetLayers);
                string[] requiredLightLayers =
                {
                    "Default",
                    "Ground",
                    "WorldObject",
                    "WorldEffect",
                    "WorldInfo",
                };

                for (int i = 0; i < requiredLightLayers.Length; i++)
                {
                    string layerName = requiredLightLayers[i];
                    Assert.IsTrue(
                        lightLayers.Contains(SortingLayer.NameToID(layerName)),
                        $"Global Light 2D 未照亮 {layerName} Sorting Layer");
                }

                AssertSceneRenderer(scene, "GroundGrid/GroundBaseTilemap", "Ground", 0);
                AssertSceneRenderer(scene, "GroundGrid/GroundDetailTilemap", "Ground", 10);

                string[] targetPaths =
                {
                    "WorldVisuals/Camp/CampTent",
                    "WorldVisuals/Camp/SupplyCrates",
                    "WorldVisuals/Camp/Brazier",
                    "WorldVisuals/Camp/CampFlag",
                    "WorldVisuals/CombatClusters/WestCluster/DeadTreeLeft",
                    "WorldVisuals/CombatClusters/WestCluster/RockClusterA",
                    "WorldVisuals/CombatClusters/EastCluster/DeadTreeRight",
                    "WorldVisuals/CombatClusters/EastCluster/RockClusterB",
                };

                HashSet<string> stableIds = new HashSet<string>();

                for (int i = 0; i < targetPaths.Length; i++)
                {
                    GameObject target = FindSceneObject(scene, targetPaths[i]);
                    Assert.IsNotNull(target, targetPaths[i]);
                    Assert.AreEqual(1, target.GetComponentsInChildren<SortingGroup>(true).Length, targetPaths[i]);
                    WorldSortParticipant participant = target.GetComponent<WorldSortParticipant>();
                    Assert.IsNotNull(participant, targetPaths[i]);
                    Assert.IsNotNull(participant.SortAnchor, targetPaths[i]);
                    Assert.IsTrue(stableIds.Add(participant.StableSortId), targetPaths[i]);
                    Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Assert.AreEqual("WorldObject", renderers[rendererIndex].sortingLayerName, targetPaths[i]);
                        Assert.AreEqual(0, renderers[rendererIndex].sortingOrder, targetPaths[i]);
                    }
                }
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void SortingSystem_ReleasesDestroyedStableIdForReuse()
        {
            WorldSortingSystem system = _architecture.GetUtility<WorldSortingSystem>();
            WorldSortParticipant participant = CreateParticipant("reusable", WorldSortCategory.Loot, 0f);
            Object.DestroyImmediate(participant.gameObject);
            system.Tick();
            WorldSortParticipant replacement = CreateParticipant("reusable", WorldSortCategory.Loot, 0f);
            Assert.IsNotNull(replacement);
            Assert.AreEqual(1, system.ParticipantCount);
        }

        WorldSortParticipant CreateParticipant(
            string stableSortId,
            WorldSortCategory category,
            float y,
            bool activate = true)
        {
            GameObject instance = new GameObject(stableSortId);
            _objects.Add(instance);
            instance.SetActive(false);
            instance.transform.position = new Vector3(0f, y, 0f);
            instance.AddComponent<SortingGroup>();
            WorldSortParticipant participant = instance.AddComponent<WorldSortParticipant>();
            participant.ConfigureIdentity(category, stableSortId);

            if (activate)
            {
                Assert.IsTrue(_architecture.GetUtility<WorldSortingSystem>().Register(participant));
            }

            return participant;
        }

        static void AssertRendererContract(string path, string sortingLayer, int sortingOrder)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers, path);

            for (int i = 0; i < renderers.Length; i++)
            {
                Assert.AreEqual(sortingLayer, renderers[i].sortingLayerName, path);
                Assert.AreEqual(sortingOrder, renderers[i].sortingOrder, path);
            }
        }

        static void AssertInternalOrder(string path, string rendererName, int sortingOrder)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Renderer renderer = System.Array.Find(renderers, candidate => candidate.gameObject.name == rendererName);
            Assert.IsNotNull(renderer, $"{path}/{rendererName}");
            Assert.AreEqual("WorldObject", renderer.sortingLayerName, $"{path}/{rendererName}");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, $"{path}/{rendererName}");
        }

        static void AssertSceneRenderer(Scene scene, string path, string layer, int order)
        {
            GameObject target = FindSceneObject(scene, path);
            Assert.IsNotNull(target, path);
            Renderer renderer = target.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, path);
            Assert.AreEqual(layer, renderer.sortingLayerName, path);
            Assert.AreEqual(order, renderer.sortingOrder, path);
        }

        static T[] FindComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                result.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            return result.ToArray();
        }

        static GameObject FindSceneObject(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            Transform current = null;

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == parts[0])
                {
                    current = roots[i].transform;
                    break;
                }
            }

            for (int i = 1; current != null && i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
            }

            return current != null ? current.gameObject : null;
        }
    }
}
