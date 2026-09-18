using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace DarkFlare
{
    public sealed class CompiledMapDocument
    {
        readonly string[] _palette;
        readonly ContentId[] _terrainIds;
        readonly Dictionary<Vector2Int, string> _variantOverrides = new Dictionary<Vector2Int, string>();
        readonly int[] _cells;
        readonly int _defaultIndex;

        public string MapId { get; }
        public string CanonicalJson { get; }
        public string SourceHash { get; }
        public int Width { get; }
        public int Height { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public double CellSize { get; }
        public int Seed { get; }
        public int Revision { get; }
        public ContentId TerrainSetId { get; }
        public ContentId DefaultTerrainId => _terrainIds[_defaultIndex];

        internal CompiledMapDocument(MapDocument document, int[] cells, int defaultIndex, string json)
        {
            MapId = document.MapId;
            Width = document.Width;
            Height = document.Height;
            OriginX = document.Origin.X;
            OriginY = document.Origin.Y;
            CellSize = document.CellSize;
            Seed = document.Seed;
            Revision = document.Revision;
            TerrainSetId = ContentId.Parse(document.TerrainSetId);
            _palette = (string[])document.TerrainPalette.Clone();
            _terrainIds = _palette.Select(ContentId.Parse).ToArray();
            _cells = (int[])cells.Clone();
            _defaultIndex = defaultIndex;
            // 重复覆盖仍由编译器定位并拒绝；尚未通过校验的快照不会对外发布。
            foreach (MapVariantOverride entry in document.VariantOverrides)
            {
                _variantOverrides[new Vector2Int(entry.X, entry.Y)] = entry.VariantId;
            }
            CanonicalJson = json;
            using SHA256 hash = SHA256.Create();
            SourceHash = BitConverter.ToString(hash.ComputeHash(MapDocumentJson.Encode(json))).Replace("-", "").ToLowerInvariant();
        }

        public string GetTerrain(int x, int y)
        {
            return _palette[x < 0 || y < 0 || x >= Width || y >= Height ? _defaultIndex : _cells[x + y * Width]];
        }

        public MapDocument CreateEditableCopy()
        {
            return MapDocumentJson.Read(CanonicalJson);
        }

        public ContentId GetTerrainId(int x, int y)
        {
            return _terrainIds[x < 0 || y < 0 || x >= Width || y >= Height ? _defaultIndex : _cells[x + y * Width]];
        }

        public bool TryGetVariantOverride(int x, int y, out string variantId)
        {
            return _variantOverrides.TryGetValue(new Vector2Int(x, y), out variantId);
        }
    }
}
