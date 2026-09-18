using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DarkFlare
{
    public readonly struct TerrainVariantSelection
    {
        public TerrainWangKey Key { get; }
        public string VariantId { get; }
        public bool HasVariant => VariantId != null;
        public bool IsOverride { get; }

        internal TerrainVariantSelection(TerrainWangKey key, string variantId, bool isOverride)
        {
            Key = key;
            VariantId = variantId;
            IsOverride = isOverride;
        }
    }

    public sealed class TerrainRuleSnapshot
    {
        const string RuleDomain = "DarkFlare.TerrainRules.v1";
        static readonly IReadOnlyList<string> NoCandidates = Array.AsReadOnly(Array.Empty<string>());

        readonly Dictionary<TerrainWangKey, IReadOnlyList<string>> _candidates;
        readonly Dictionary<string, TerrainWangKey> _variants;
        readonly HashSet<ContentId> _terrains;

        public ContentId TerrainSetId { get; }
        public string RuleHash { get; }

        TerrainRuleSnapshot(TerrainSetDefinition definition)
        {
            TerrainSetId = new ContentId(ContentNamespaces.TerrainSet, definition.Id);
            ContentId[] terrains = definition.Terrains.Select(terrain => new ContentId(ContentNamespaces.Terrain, terrain.Id)).ToArray();
            _terrains = new HashSet<ContentId>(terrains);
            _variants = new Dictionary<string, TerrainWangKey>(StringComparer.Ordinal);
            var groups = new Dictionary<TerrainWangKey, List<string>>();
            foreach (TerrainTileVariant variant in definition.Variants.OrderBy(variant => variant.Id, StringComparer.Ordinal))
            {
                TerrainWangKey key = TerrainWangKey.FromVariant(variant);
                _variants.Add(variant.Id, key);
                if (!groups.TryGetValue(key, out List<string> candidates)) { groups.Add(key, candidates = new List<string>()); }
                candidates.Add(variant.Id);
            }
            _candidates = groups.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()));
            RuleHash = ComputeRuleHash(terrains);
        }

        public static TerrainRuleSnapshot Compile(TerrainSetDefinition definition, IMapDefinitionLookup definitions)
        {
            if (definition == null) { throw new ArgumentNullException(nameof(definition)); }
            if (definitions == null) { throw new ArgumentNullException(nameof(definitions)); }
            MapDocumentCompiler.ValidateDefinition(definition, definitions);
            var id = new ContentId(ContentNamespaces.TerrainSet, definition.Id);
            if (definitions.Resolve<TerrainSetDefinition>(id) != definition) { throw new MapDocumentException(id.ToString(), "地表集引用冲突"); }
            return new TerrainRuleSnapshot(definition);
        }

        public IReadOnlyList<string> GetCandidates(TerrainWangKey key)
        {
            ValidateKey(key);
            return _candidates.TryGetValue(key, out IReadOnlyList<string> candidates) ? candidates : NoCandidates;
        }

        public TerrainVariantSelection Resolve(CompiledMapDocument map, int x, int y)
        {
            if (map == null) { throw new ArgumentNullException(nameof(map)); }
            if (map.TerrainSetId != TerrainSetId) { throw new MapDocumentException("$.terrainSetId", "地图与规则快照的地表集不匹配"); }
            TerrainWangKey key = DualGridCoordinates.Sample(map, x, y);
            IReadOnlyList<string> candidates = GetCandidates(key);
            if (map.TryGetVariantOverride(x, y, out string variantId))
            {
                if (!_variants.TryGetValue(variantId, out TerrainWangKey expected) || expected != key)
                {
                    throw new MapDocumentException("$.variantOverrides", $"显示格 ({x},{y}) 的变体已缺失或不兼容：{variantId}");
                }
                return new TerrainVariantSelection(key, variantId, true);
            }
            if (candidates.Count == 0) { return new TerrainVariantSelection(key, null, false); }
            ulong hash = TerrainVariantHash.Compute(map.Seed, x, y, TerrainSetId, key);
            return new TerrainVariantSelection(key, candidates[(int)(hash % (ulong)candidates.Count)], false);
        }

        void ValidateKey(TerrainWangKey key)
        {
            if (!key.IsValid || !_terrains.Contains(key.NorthWest) || !_terrains.Contains(key.NorthEast)
                || !_terrains.Contains(key.SouthEast) || !_terrains.Contains(key.SouthWest))
            {
                throw new MapDocumentException("$key", "四角包含无效或地表集之外的材质");
            }
        }

        string ComputeRuleHash(ContentId[] terrains)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            WriteText(writer, RuleDomain);
            WriteText(writer, TerrainSetId.ToString());
            writer.Write(terrains.Length);
            foreach (ContentId terrain in terrains) { WriteText(writer, terrain.ToString()); }
            writer.Write(_variants.Count);
            foreach (var variant in _variants.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                WriteText(writer, variant.Key);
                WriteText(writer, variant.Value.NorthWest.ToString());
                WriteText(writer, variant.Value.NorthEast.ToString());
                WriteText(writer, variant.Value.SouthEast.ToString());
                WriteText(writer, variant.Value.SouthWest.ToString());
            }
            writer.Flush();
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        static void WriteText(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
