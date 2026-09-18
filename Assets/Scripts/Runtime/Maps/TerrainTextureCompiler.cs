using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DarkFlare
{
    public static class TerrainTextureCompiler
    {
        public const int Size = 64;
        public const string Revision = "area-srgb-integer-64-periodic-2-8-xy-v1";

        public static Color32[] Prepare(Color32[] source, int size, Color32[] baseline = null, CancellationToken token = default)
        {
            Color32[] pixels = Resample(source, size, token);
            if (baseline != null)
            {
                Validate(baseline, Size);
                for (int y = 0; y < Size; y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int x = 0; x < Size; x++)
                    {
                        int distance = Math.Min(Math.Min(x, y), Math.Min(Size - 1 - x, Size - 1 - y));
                        if (distance < 8) { pixels[x + y * Size] = Blend(pixels[x + y * Size], baseline[x + y * Size], Math.Min(6, 8 - distance), 6); }
                    }
                }
                return pixels;
            }
            // 顺序固定；Y 的相同线性组合保留 X 已同步的边及四角。
            for (int axis = 0; axis < 2; axis++)
            {
                for (int row = 0; row < Size; row++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int offset = 0; offset < 8; offset++)
                    {
                        int a = axis == 0 ? offset + row * Size : row + offset * Size;
                        int b = axis == 0 ? Size - 1 - offset + row * Size : row + (Size - 1 - offset) * Size;
                        Color32 average = Blend(pixels[a], pixels[b], 1, 2);
                        int weight = Math.Min(6, 8 - offset);
                        pixels[a] = Blend(pixels[a], average, weight, 6);
                        pixels[b] = Blend(pixels[b], average, weight, 6);
                    }
                }
            }
            return pixels;
        }

        public static Color32[] Resample(Color32[] source, int size, CancellationToken token = default)
        {
            Validate(source, size);
            var result = new Color32[Size * Size];
            long area = (long)size * size;
            for (int y = 0; y < Size; y++)
            {
                token.ThrowIfCancellationRequested();
                int bottom = y * size;
                int top = (y + 1) * size;
                for (int x = 0; x < Size; x++)
                {
                    int left = x * size;
                    int right = (x + 1) * size;
                    long r = 0, g = 0, b = 0;
                    for (int sy = bottom / Size; sy < (top + Size - 1) / Size; sy++)
                    {
                        int dy = Math.Min(top, (sy + 1) * Size) - Math.Max(bottom, sy * Size);
                        for (int sx = left / Size; sx < (right + Size - 1) / Size; sx++)
                        {
                            int weight = dy * (Math.Min(right, (sx + 1) * Size) - Math.Max(left, sx * Size));
                            Color32 color = source[sx + sy * size];
                            r += color.r * weight; g += color.g * weight; b += color.b * weight;
                        }
                    }
                    result[x + y * Size] = new Color32((byte)((r + area / 2) / area), (byte)((g + area / 2) / area), (byte)((b + area / 2) / area), 255);
                }
            }
            return result;
        }

        public static Color32[] Compose(TerrainWangKey key, IReadOnlyDictionary<ContentId, Color32[]> materials, CancellationToken token = default)
        {
            if (materials == null) { throw new ArgumentNullException(nameof(materials)); }
            foreach (ContentId id in new[] { key.NorthWest, key.NorthEast, key.SouthEast, key.SouthWest })
            {
                if (!materials.TryGetValue(id, out Color32[] pixels)) { throw new ArgumentException("缺失正式材质：" + id); }
                Validate(pixels, Size);
            }
            TerrainPartitionRaster partition = TerrainPartition.Rasterize(key, Size, token);
            var result = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < Size; x++) { result[x + y * Size] = materials[partition.GetTerrain(x, y)][x + y * Size]; }
            }
            return result;
        }

        static void Validate(Color32[] pixels, int size)
        {
            if (size < 2 || size > 4096 || pixels == null || pixels.Length != size * size) { throw new ArgumentException("材质必须为 2..4096 的完整方形像素数组"); }
            foreach (Color32 color in pixels)
            {
                if (color.a != 255) { throw new ArgumentException("正式地表材质必须完全不透明"); }
            }
        }

        static Color32 Blend(Color32 value, Color32 target, int numerator, int denominator)
        {
            return new Color32(
                (byte)((value.r * (denominator - numerator) + target.r * numerator + denominator / 2) / denominator),
                (byte)((value.g * (denominator - numerator) + target.g * numerator + denominator / 2) / denominator),
                (byte)((value.b * (denominator - numerator) + target.b * numerator + denominator / 2) / denominator), 255);
        }
    }
}
