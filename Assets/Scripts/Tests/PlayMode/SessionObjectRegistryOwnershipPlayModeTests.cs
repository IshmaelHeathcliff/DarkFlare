using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class SessionObjectRegistryOwnershipPlayModeTests
    {
        [UnityTest]
        public IEnumerator DelayedDestroy_UnregistersCapturedRegistryOnly()
        {
            SessionObjectRegistry ownerRegistry = new SessionObjectRegistry();
            SessionObjectRegistry unrelatedRegistry = new SessionObjectRegistry();
            GameObject ownedObject = new GameObject("Delayed-Owned-Registry-Object");
            GameObject unrelatedObject = new GameObject("Delayed-Unrelated-Registry-Object");
            DamageNumberVisual visual = ownedObject.AddComponent<DamageNumberVisual>();
            SetCapturedRegistry(visual, ownerRegistry);
            ownerRegistry.Register(ownedObject);
            unrelatedRegistry.Register(unrelatedObject);

            UnityEngine.Object.Destroy(ownedObject);

            Assert.AreEqual(1, ownerRegistry.Count);
            Assert.AreEqual(1, unrelatedRegistry.Count);
            yield return null;

            Assert.AreEqual(0, ownerRegistry.Count);
            Assert.AreEqual(1, unrelatedRegistry.Count);
            UnityEngine.Object.Destroy(unrelatedObject);
            yield return null;
        }

        static void SetCapturedRegistry(
            DamageNumberVisual visual,
            SessionObjectRegistry registry)
        {
            FieldInfo field = typeof(DamageNumberVisual).GetField(
                "_sessionObjects",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field);
            field.SetValue(visual, registry);
        }
    }
}
