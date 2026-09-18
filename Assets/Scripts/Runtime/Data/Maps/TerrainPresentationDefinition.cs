using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/Maps/Terrain Presentation")]
    public sealed class TerrainPresentationDefinition : ScriptableObject
    {
        [SerializeField, LabelText("地表集")] TerrainSetDefinition _terrainSet;
        [SerializeField, ReadOnly, LabelText("已发布显示目录")] TerrainTileCatalog _tiles;

        public TerrainSetDefinition TerrainSet => _terrainSet;
        public TerrainTileCatalog Tiles => _tiles;
        public string Address => AddressFor(new ContentId(ContentNamespaces.TerrainSet, _terrainSet.Id));

        public static string AddressFor(ContentId terrainSetId)
        {
            if (!terrainSetId.IsValid || terrainSetId.Namespace != ContentNamespaces.TerrainSet) { throw new ArgumentException("需要 terrain_set 身份", nameof(terrainSetId)); }
            return "maps/terrain/" + terrainSetId.LocalId;
        }

        public void Validate()
        {
            if (_terrainSet == null || _tiles == null) { throw new InvalidOperationException("地表显示目录缺少地表集或编译产物"); }
            if (_terrainSet.Terrains.Count > 4) { throw new InvalidOperationException("首期正式地表集最多四材质"); }
            ScriptableObject[] definitions = _terrainSet.Terrains.Cast<ScriptableObject>().Append(_terrainSet).ToArray();
            TerrainRuleSnapshot rules = TerrainRuleSnapshot.Compile(_terrainSet, new MapDefinitionLookup(definitions));
            if (_tiles.TerrainSetId != rules.TerrainSetId.ToString() || _tiles.RuleHash != rules.RuleHash) { throw new InvalidOperationException("地表显示目录规则已过期"); }
            _tiles.Validate();
            ContentId[] ids = _terrainSet.Terrains.Select(terrain => new ContentId(ContentNamespaces.Terrain, terrain.Id)).ToArray();
            int expected = 0;
            foreach (ContentId nw in ids)
            foreach (ContentId ne in ids)
            foreach (ContentId se in ids)
            foreach (ContentId sw in ids)
            {
                var key = new TerrainWangKey(nw, ne, se, sw);
                _tiles.GetTile(key, null); expected++;
                foreach (string variant in rules.GetCandidates(key)) { _tiles.GetTile(key, variant); expected++; }
            }
            if (_tiles.Entries.Count != expected) { throw new InvalidOperationException("地表显示目录包含多余条目"); }
        }
    }
}
