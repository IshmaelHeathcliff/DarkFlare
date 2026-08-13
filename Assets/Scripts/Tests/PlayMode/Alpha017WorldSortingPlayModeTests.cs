using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class Alpha017WorldSortingPlayModeTests
    {
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

        [UnityTest]
        public IEnumerator Participants_RemainStableAcrossCrossingFlipDisableAndIdentityReuse()
        {
            WorldSortingSystem system = _architecture.GetUtility<WorldSortingSystem>();
            WorldSortParticipant higher = CreateParticipant("monster_a", WorldSortCategory.Monster, 2f);
            WorldSortParticipant lower = CreateParticipant("monster_b", WorldSortCategory.Monster, 1f);
            yield return null;
            system.Tick();

            Assert.AreEqual(2, system.ParticipantCount);
            Assert.AreEqual(0, higher.SortingGroup.sortingOrder);
            Assert.AreEqual(1, lower.SortingGroup.sortingOrder);

            Vector3 flippedScale = higher.transform.localScale;
            flippedScale.x = -flippedScale.x;
            higher.transform.localScale = flippedScale;
            yield return null;
            system.Tick();
            Assert.AreEqual(0, higher.SortingGroup.sortingOrder);
            Assert.AreEqual("monster_a", higher.StableSortId);

            higher.gameObject.SetActive(false);
            Assert.AreEqual(1, system.ParticipantCount);
            higher.gameObject.SetActive(true);
            Assert.AreEqual(2, system.ParticipantCount);

            lower.transform.position = new Vector3(0f, 3f, 0f);
            yield return null;
            system.Tick();
            Assert.AreEqual(0, lower.SortingGroup.sortingOrder);
            Assert.AreEqual(1, higher.SortingGroup.sortingOrder);

            higher.gameObject.SetActive(false);
            Assert.AreEqual(1, system.ParticipantCount);
            WorldSortParticipant replacement = CreateParticipant("monster_a", WorldSortCategory.Monster, 0f);
            yield return null;
            system.Tick();
            Assert.AreEqual(2, system.ParticipantCount);
            Assert.AreEqual("monster_a", replacement.StableSortId);
        }

        WorldSortParticipant CreateParticipant(string stableSortId, WorldSortCategory category, float y)
        {
            GameObject instance = new GameObject(stableSortId);
            _objects.Add(instance);
            instance.SetActive(false);
            instance.transform.position = new Vector3(0f, y, 0f);
            instance.AddComponent<SortingGroup>();
            WorldSortParticipant participant = instance.AddComponent<WorldSortParticipant>();
            participant.ConfigureIdentity(category, stableSortId);
            instance.SetActive(true);
            return participant;
        }
    }
}
