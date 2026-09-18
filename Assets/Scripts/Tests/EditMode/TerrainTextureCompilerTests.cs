using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class TerrainTextureCompilerTests
    {
        [Test]
        public void AreaSampling_UsesWholeCanvasAndIntegerOverlapWeights()
        {
            Color32[] source = Enumerable.Repeat(new Color32(0, 0, 0, 255), 96 * 96).ToArray();
            source[0] = new Color32(90, 180, 225, 255);
            source[1] = new Color32(180, 90, 0, 255);
            source[96] = new Color32(0, 90, 180, 255);
            source[97] = new Color32(180, 180, 180, 255);
            source[^1] = new Color32(90, 180, 225, 255);
            Color32[] result = TerrainTextureCompiler.Resample(source, 96);
            Assert.AreEqual(new Color32(100, 140, 160, 255), result[0]);
            Assert.AreEqual(new Color32(40, 80, 100, 255), result[^1]);
            Assert.AreEqual(new Color32(0, 0, 0, 255), result[32 + 32 * 64]);
            Color32[] native = Pattern(0);
            CollectionAssert.AreEqual(native, TerrainTextureCompiler.Resample(native, 64));
        }

        [Test]
        public void Preparation_PreservesInteriorAndVariantInheritsOpaquePeriodicEdges()
        {
            Color32[] source = Pattern(0);
            Color32[] baseline = TerrainTextureCompiler.Prepare(source, 64);
            Color32[] alternate = Pattern(1);
            Color32[] variant = TerrainTextureCompiler.Prepare(alternate, 64, baseline);
            CollectionAssert.AreEqual(baseline, TerrainTextureCompiler.Prepare(source, 64));
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                int i = x + y * 64;
                int distance = Math.Min(Math.Min(x, y), Math.Min(63 - x, 63 - y));
                Assert.AreEqual(255, baseline[i].a);
                Assert.AreEqual(255, variant[i].a);
                if (distance < 2) { Assert.AreEqual(baseline[i], variant[i]); }
                if (distance >= 8)
                {
                    Assert.AreEqual(source[i], baseline[i]);
                    Assert.AreEqual(alternate[i], variant[i]);
                }
                if (x < 2) { Assert.AreEqual(baseline[i], baseline[63 - x + y * 64]); }
                if (y < 2) { Assert.AreEqual(baseline[i], baseline[x + (63 - y) * 64]); }
            }
            Assert.AreNotEqual(baseline[32 + 32 * 64], variant[32 + 32 * 64]);
        }

        [Test]
        public void EveryFourMaterialKeyAndVariant_HasIdenticalCompatibleEdges()
        {
            ContentId[] ids = Enumerable.Range(0, 4).Select(i => ContentId.Parse("terrain:material_" + i)).ToArray();
            var materials = new Dictionary<ContentId, Color32[]>();
            var variants = new Dictionary<ContentId, Color32[]>();
            for (int i = 0; i < ids.Length; i++)
            {
                materials.Add(ids[i], TerrainTextureCompiler.Prepare(Pattern(i), 64));
                variants.Add(ids[i], TerrainTextureCompiler.Prepare(Pattern(i + 4), 64, materials[ids[i]]));
            }
            // 相同端点的所有朝向共用一个边记录，验证兼容边不受另两角或变体影响。
            var horizontal = new Dictionary<(ContentId, ContentId), Color32[]>();
            var vertical = new Dictionary<(ContentId, ContentId), Color32[]>();
            foreach (ContentId nw in ids)
            foreach (ContentId ne in ids)
            foreach (ContentId se in ids)
            foreach (ContentId sw in ids)
            {
                var key = new TerrainWangKey(nw, ne, se, sw);
                foreach (Dictionary<ContentId, Color32[]> palette in new[] { materials, variants })
                {
                    Color32[] pixels = TerrainTextureCompiler.Compose(key, palette);
                    AssertEdge(horizontal, (nw, ne), Enumerable.Range(0, 64).Select(x => pixels[x + 63 * 64]).ToArray());
                    AssertEdge(horizontal, (sw, se), pixels.Take(64).ToArray());
                    AssertEdge(vertical, (sw, nw), Enumerable.Range(0, 64).Select(y => pixels[y * 64]).ToArray());
                    AssertEdge(vertical, (se, ne), Enumerable.Range(0, 64).Select(y => pixels[63 + y * 64]).ToArray());
                    // 独立边界预期：64 采样下边中点无平票，各端点占半边。
                    for (int x = 0; x < 64; x++)
                    {
                        Assert.AreEqual(materials[x < 32 ? sw : se][x], pixels[x]);
                        Assert.AreEqual(255, pixels[x].a);
                    }
                }
            }
        }

        [Test]
        public void InvalidOrCancelledInput_FailsWithoutChangingSource()
        {
            Color32[] pixels = Pattern(0);
            Color32[] before = (Color32[])pixels.Clone();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => TerrainTextureCompiler.Prepare(pixels, 64, token: cancellation.Token));
            CollectionAssert.AreEqual(before, pixels);
            Assert.Throws<ArgumentException>(() => TerrainTextureCompiler.Resample(pixels, 65));
            pixels[0].a = 254;
            Assert.Throws<ArgumentException>(() => TerrainTextureCompiler.Prepare(pixels, 64));
            ContentId id = ContentId.Parse("terrain:missing");
            Assert.Throws<ArgumentException>(() => TerrainTextureCompiler.Compose(new TerrainWangKey(id, id, id, id), new Dictionary<ContentId, Color32[]>()));
        }

        static void AssertEdge(Dictionary<(ContentId, ContentId), Color32[]> edges, (ContentId, ContentId) endpoints, Color32[] edge)
        {
            if (edges.TryGetValue(endpoints, out Color32[] previous)) { CollectionAssert.AreEqual(previous, edge); }
            else { edges.Add(endpoints, edge); }
        }

        static Color32[] Pattern(int seed)
        {
            return Enumerable.Range(0, 4096).Select(i => new Color32((byte)(i * 13 + seed * 31), (byte)(i / 64 * 19 + seed * 71), (byte)(i * 7 + seed * 53), 255)).ToArray();
        }
    }
}
