using System;

namespace DarkFlare
{
    public readonly struct TerrainWangKey : IEquatable<TerrainWangKey>
    {
        public ContentId NorthWest { get; }
        public ContentId NorthEast { get; }
        public ContentId SouthEast { get; }
        public ContentId SouthWest { get; }
        public bool IsValid => IsTerrain(NorthWest) && IsTerrain(NorthEast) && IsTerrain(SouthEast) && IsTerrain(SouthWest);

        public TerrainWangKey(ContentId northWest, ContentId northEast, ContentId southEast, ContentId southWest)
        {
            if (!IsTerrain(northWest) || !IsTerrain(northEast) || !IsTerrain(southEast) || !IsTerrain(southWest))
            {
                throw new ArgumentException("四角必须使用有效的 terrain ContentId");
            }
            NorthWest = northWest;
            NorthEast = northEast;
            SouthEast = southEast;
            SouthWest = southWest;
        }

        public int GetMask(ContentId terrain)
        {
            if (!IsValid || !IsTerrain(terrain)) { throw new ArgumentException("需要有效的四角和地表 ID"); }
            return (NorthWest == terrain ? 1 : 0) | (NorthEast == terrain ? 2 : 0)
                | (SouthEast == terrain ? 4 : 0) | (SouthWest == terrain ? 8 : 0);
        }

        public bool Equals(TerrainWangKey other)
        {
            return NorthWest == other.NorthWest && NorthEast == other.NorthEast
                && SouthEast == other.SouthEast && SouthWest == other.SouthWest;
        }

        public override bool Equals(object obj)
        {
            return obj is TerrainWangKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = NorthWest.GetHashCode();
                hash = hash * 31 + NorthEast.GetHashCode();
                hash = hash * 31 + SouthEast.GetHashCode();
                return hash * 31 + SouthWest.GetHashCode();
            }
        }

        public static bool operator ==(TerrainWangKey left, TerrainWangKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TerrainWangKey left, TerrainWangKey right)
        {
            return !left.Equals(right);
        }

        internal static TerrainWangKey FromVariant(TerrainTileVariant variant)
        {
            return new TerrainWangKey(
                new ContentId(ContentNamespaces.Terrain, variant.NorthWest.Id),
                new ContentId(ContentNamespaces.Terrain, variant.NorthEast.Id),
                new ContentId(ContentNamespaces.Terrain, variant.SouthEast.Id),
                new ContentId(ContentNamespaces.Terrain, variant.SouthWest.Id));
        }

        static bool IsTerrain(ContentId id)
        {
            return id.IsValid && id.Namespace == ContentNamespaces.Terrain;
        }
    }
}
