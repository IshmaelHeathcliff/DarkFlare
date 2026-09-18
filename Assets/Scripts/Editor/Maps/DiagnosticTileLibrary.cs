using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace DarkFlare.Editor
{
    public sealed class DiagnosticTileLibrary : IDisposable
    {
        public const int Size = 64;
        // 修改颜色、像素规格或内部标记算法时递增。分区修订单独参与键。
        public const string DisplayRevision = "diagnostic-rgba32-point-64-v1";
        readonly Dictionary<(string Rule, string Partition, string Display, TerrainWangKey Key, string Variant), Entry> _entries = new();
        bool _disposed;

        public int Count => _entries.Count;

        public sealed class Lease : IDisposable
        {
            readonly Action _release;
            bool _disposed;
            public Tile Tile { get; }

            internal Lease(Tile tile, Action release) { Tile = tile; _release = release; }
            public void Dispose()
            {
                if (_disposed) { return; }
                _disposed = true;
                _release();
            }
        }

        sealed class Entry : IDisposable
        {
            public Texture2D Texture { get; set; }
            public Sprite Sprite { get; set; }
            public Tile Tile { get; set; }
            public int References { get; set; }

            public void Dispose()
            {
                if (Tile != null) { Object.DestroyImmediate(Tile); }
                if (Sprite != null) { Object.DestroyImmediate(Sprite); }
                if (Texture != null) { Object.DestroyImmediate(Texture); }
            }
        }

        public Lease Acquire(string ruleHash, TerrainWangKey key, string variant)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(DiagnosticTileLibrary)); }
            var cacheKey = (ruleHash, TerrainPartition.Revision, DisplayRevision, key, variant);
            if (!_entries.TryGetValue(cacheKey, out Entry entry))
            {
                entry = Create(key, variant);
                _entries.Add(cacheKey, entry);
            }
            entry.References++;
            return new Lease(entry.Tile, () =>
            {
                if (_disposed || --entry.References != 0) { return; }
                _entries.Remove(cacheKey);
                entry.Dispose();
            });
        }

        public static Color32 TerrainColor(ContentId terrain)
        {
            string id = terrain.LocalId;
            if (id == "dirt" || id.EndsWith("_dirt", StringComparison.Ordinal)) { return new Color32(151, 103, 65, 255); }
            if (id == "grass" || id.EndsWith("_grass", StringComparison.Ordinal)) { return new Color32(79, 151, 86, 255); }
            if (id == "water" || id.EndsWith("_water", StringComparison.Ordinal)) { return new Color32(52, 119, 189, 255); }
            if (id == "road" || id.EndsWith("_road", StringComparison.Ordinal)) { return new Color32(202, 178, 123, 255); }
            return Color.HSVToRGB((uint)terrain.GetHashCode() % 360 / 360f, 0.5f, 0.8f);
        }

        static Entry Create(TerrainWangKey key, string variant)
        {
            var entry = new Entry();
            try
            {
                TerrainPartitionRaster raster = TerrainPartition.Rasterize(key, Size);
                var pixels = new Color32[Size * Size];
                uint marker = 2166136261;
                if (variant != null) { foreach (char c in variant) { marker = unchecked((marker ^ c) * 16777619); } }
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Color32 color = TerrainColor(raster.GetTerrain(x, y));
                        // 只改内部亮度，外沿至少两像素不变；不改材质分区。
                        if (variant != null && x >= 8 && x < 56 && y >= 8 && y < 56
                            && ((marker >> ((x / 8 + y / 8 * 6) % 32)) & 1) != 0 && x % 8 < 3 && y % 8 < 3)
                        {
                            color = new Color32((byte)(color.r * 0.78f), (byte)(color.g * 0.78f), (byte)(color.b * 0.78f), 255);
                        }
                        pixels[x + y * Size] = color;
                    }
                }
                entry.Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = "Map diagnostic texture", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                };
                entry.Texture.SetPixels32(pixels);
                entry.Texture.Apply(false, false);
                entry.Sprite = Sprite.Create(entry.Texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
                entry.Sprite.name = "Map diagnostic sprite";
                entry.Sprite.hideFlags = HideFlags.HideAndDontSave;
                entry.Tile = ScriptableObject.CreateInstance<Tile>();
                entry.Tile.name = "Map diagnostic tile";
                entry.Tile.hideFlags = HideFlags.HideAndDontSave;
                entry.Tile.sprite = entry.Sprite;
                entry.Tile.colliderType = Tile.ColliderType.None;
                return entry;
            }
            catch { entry.Dispose(); throw; }
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            foreach (Entry entry in _entries.Values) { entry.Dispose(); }
            _entries.Clear();
        }
    }
}
