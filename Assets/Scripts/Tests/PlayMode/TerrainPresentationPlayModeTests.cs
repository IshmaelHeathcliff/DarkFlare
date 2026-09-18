using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkFlare.Tests
{
    public sealed class TerrainPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator PublishedTerrain_LoadsCompleteTilesAndReleasesBetweenOwners()
        {
            using var service = new AddressableAssetService();
            using var cancellation = new CancellationTokenSource(20000);
            string address = TerrainPresentationDefinition.AddressFor(ContentId.Parse("terrain_set:basic"));
            for (int cycle = 0; cycle < 2; cycle++)
            {
                using AssetOwnerScope firstOwner = service.CreateOwner("terrain-first");
                using AssetOwnerScope secondOwner = service.CreateOwner("terrain-second");
                ResourceLoadResult<TerrainPresentationDefinition> first = null;
                ResourceLoadResult<TerrainPresentationDefinition> second = null;
                yield return service.AcquireAsync<TerrainPresentationDefinition>(firstOwner, address, address, cancellation.Token)
                    .ToCoroutine(value => first = value);
                Assert.IsTrue(first.Succeeded, first.Exception?.ToString());
                yield return service.AcquireAsync<TerrainPresentationDefinition>(secondOwner, address, address, cancellation.Token)
                    .ToCoroutine(value => second = value);
                Assert.IsTrue(second.Succeeded, second.Exception?.ToString());
                Assert.AreSame(first.Lease.Asset, second.Lease.Asset);
                Assert.DoesNotThrow(second.Lease.Asset.Validate);
                Assert.AreEqual(address, second.Lease.Asset.Address);
                firstOwner.Close();
                Assert.IsTrue(first.Lease.IsDisposed);
                Assert.IsFalse(second.Lease.IsDisposed);
                Assert.AreEqual(1, service.GetDiagnostics().ActiveEntries);
                foreach (TerrainTileEntry entry in second.Lease.Asset.Tiles.Entries)
                {
                    Assert.IsNotNull(entry.Tile.sprite.texture);
                    Assert.AreEqual(64, entry.Tile.sprite.pixelsPerUnit);
                }
                secondOwner.Close();
                ResourceDiagnosticsSnapshot released = service.GetDiagnostics();
                Assert.AreEqual(0, released.ActiveOwners);
                Assert.AreEqual(0, released.ActiveEntries);
                Assert.AreEqual(0, released.ActiveLeases);
                Assert.AreEqual(0, released.InFlightLoads);
            }
        }
    }
}
