using System;
using System.Security.Cryptography;

namespace DarkFlare
{
    public sealed class CompiledMapDocument
    {
        readonly string[] _palette;
        readonly int[] _cells;
        readonly int _defaultIndex;

        public string MapId { get; }
        public string CanonicalJson { get; }
        public string SourceHash { get; }
        public int Width { get; }
        public int Height { get; }

        internal CompiledMapDocument(MapDocument document, int[] cells, int defaultIndex, string json)
        {
            MapId = document.MapId;
            Width = document.Width;
            Height = document.Height;
            _palette = (string[])document.TerrainPalette.Clone();
            _cells = (int[])cells.Clone();
            _defaultIndex = defaultIndex;
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
    }
}
