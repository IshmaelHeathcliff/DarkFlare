using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DarkFlare
{
    [Serializable]
    public sealed class TerrainTileEntry
    {
        [SerializeField, LabelText("西北地表")] string _northWest;
        [SerializeField, LabelText("东北地表")] string _northEast;
        [SerializeField, LabelText("东南地表")] string _southEast;
        [SerializeField, LabelText("西南地表")] string _southWest;
        [SerializeField, LabelText("变体 ID（空为基准）")] string _variant;
        [SerializeField, LabelText("编译 Tile")] Tile _tile;
        [SerializeField, LabelText("PNG 内容摘要")] string _textureHash;

        public TerrainWangKey Key => new(ContentId.Parse(_northWest), ContentId.Parse(_northEast), ContentId.Parse(_southEast), ContentId.Parse(_southWest));
        public string Variant => string.IsNullOrEmpty(_variant) ? null : _variant;
        public Tile Tile => _tile;
        public string TextureHash => _textureHash;

        public TerrainTileEntry(TerrainWangKey key, string variant, Tile tile, string textureHash)
        {
            _northWest = key.NorthWest.ToString(); _northEast = key.NorthEast.ToString();
            _southEast = key.SouthEast.ToString(); _southWest = key.SouthWest.ToString();
            _variant = variant; _tile = tile; _textureHash = textureHash;
        }
    }

    // 只读编译产物，不提供 CreateAssetMenu；不持有高分辨率源图。
    public sealed class TerrainTileCatalog : ScriptableObject
    {
        [SerializeField, ReadOnly, LabelText("地表集 ID")] string _terrainSetId;
        [SerializeField, ReadOnly, LabelText("规则摘要")] string _ruleHash;
        [SerializeField, ReadOnly, LabelText("显示内容修订")] string _revision;
        [SerializeField, ReadOnly, LabelText("编译条目")] TerrainTileEntry[] _entries = Array.Empty<TerrainTileEntry>();
        Dictionary<(TerrainWangKey, string), Tile> _lookup;

        public string TerrainSetId => _terrainSetId;
        public string RuleHash => _ruleHash;
        public string Revision => _revision;
        public IReadOnlyList<TerrainTileEntry> Entries => Array.AsReadOnly(_entries);

        public void Initialize(string terrainSetId, string ruleHash, string revision, TerrainTileEntry[] entries)
        {
            if (!string.IsNullOrEmpty(_revision)) { throw new InvalidOperationException("编译产物不可原地覆盖"); }
            _terrainSetId = terrainSetId; _ruleHash = ruleHash; _revision = revision;
            _entries = (TerrainTileEntry[])entries.Clone();
            Validate();
        }

        public void Validate()
        {
            var lookup = new Dictionary<(TerrainWangKey, string), Tile>();
            foreach (TerrainTileEntry entry in _entries)
            {
                if (entry == null || entry.Tile == null || entry.Tile.sprite == null || entry.Tile.sprite.texture == null
                    || !lookup.TryAdd((entry.Key, entry.Variant), entry.Tile)) { throw new InvalidOperationException("编译 Tile 缺失或重复"); }
            }
            if (lookup.Count == 0 || string.IsNullOrEmpty(_revision)) { throw new InvalidOperationException("编译目录为空"); }
            _lookup = lookup;
        }

        public Tile GetTile(TerrainWangKey key, string variant)
        {
            if (_lookup == null) { Validate(); }
            if (!_lookup.TryGetValue((key, variant), out Tile tile) || tile == null || tile.sprite == null)
            {
                throw new InvalidOperationException("缺失正式 Tile，禁止诊断回退：" + variant);
            }
            return tile;
        }
    }
}
