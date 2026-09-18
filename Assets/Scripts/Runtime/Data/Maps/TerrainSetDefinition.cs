using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Serializable]
    public sealed class TerrainTileVariant
    {
        [SerializeField, LabelText("变体 ID")] string _id;
        [SerializeField, LabelText("左下地表")] TerrainDefinition _southWest;
        [SerializeField, LabelText("右下地表")] TerrainDefinition _southEast;
        [SerializeField, LabelText("左上地表")] TerrainDefinition _northWest;
        [SerializeField, LabelText("右上地表")] TerrainDefinition _northEast;

        public string Id => _id;
        public TerrainDefinition SouthWest => _southWest;
        public TerrainDefinition SouthEast => _southEast;
        public TerrainDefinition NorthWest => _northWest;
        public TerrainDefinition NorthEast => _northEast;
    }

    [ContentDefinition(ContentNamespaces.TerrainSet)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Maps/Terrain Set Definition")]
    public sealed class TerrainSetDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField, LabelText("稳定 ID")] string _id;
        [SerializeField, LabelText("有序地表表")] TerrainDefinition[] _terrains = Array.Empty<TerrainDefinition>();
        [SerializeField, LabelText("四角兼容变体")] TerrainTileVariant[] _variants = Array.Empty<TerrainTileVariant>();

        public string Id => _id;
        public IReadOnlyList<TerrainDefinition> Terrains => Array.AsReadOnly(_terrains ?? Array.Empty<TerrainDefinition>());
        public IReadOnlyList<TerrainTileVariant> Variants => Array.AsReadOnly(_variants ?? Array.Empty<TerrainTileVariant>());
    }
}
