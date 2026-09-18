using System;

namespace DarkFlare
{
    internal static class TerrainVariantHash
    {
        const ulong OffsetBasis = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        const string Domain = "DarkFlare.TerrainVariant.v1";

        internal static ulong Compute(int seed, int x, int y, ContentId terrainSetId, TerrainWangKey key)
        {
            if (!terrainSetId.IsValid || terrainSetId.Namespace != ContentNamespaces.TerrainSet || !key.IsValid)
            {
                throw new ArgumentException("变体 Hash 需要合法地表集与四角 ID");
            }
            ulong hash = OffsetBasis;
            AddInt(ref hash, Domain.Length);
            AddAscii(ref hash, Domain);
            AddInt(ref hash, seed);
            AddInt(ref hash, x);
            AddInt(ref hash, y);
            AddId(ref hash, terrainSetId);
            AddId(ref hash, key.NorthWest);
            AddId(ref hash, key.NorthEast);
            AddId(ref hash, key.SouthEast);
            AddId(ref hash, key.SouthWest);
            return hash;
        }

        static void AddId(ref ulong hash, ContentId id)
        {
            // ContentId 合同只允许 ASCII；逐字节等价于 UTF-8，避免每格构造字符串或字节数组。
            AddInt(ref hash, id.Namespace.Length + 1 + id.LocalId.Length);
            AddAscii(ref hash, id.Namespace);
            AddByte(ref hash, (byte)':');
            AddAscii(ref hash, id.LocalId);
        }

        static void AddAscii(ref ulong hash, string value)
        {
            for (int i = 0; i < value.Length; i++) { AddByte(ref hash, (byte)value[i]); }
        }

        static void AddInt(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8) { AddByte(ref hash, unchecked((byte)(bits >> shift))); }
        }

        static void AddByte(ref ulong hash, byte value)
        {
            hash = unchecked((hash ^ value) * Prime);
        }
    }
}
