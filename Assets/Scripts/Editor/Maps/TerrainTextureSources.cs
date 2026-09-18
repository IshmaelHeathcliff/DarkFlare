using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    [Serializable]
    public sealed class TerrainMaterialSource
    {
        [SerializeField, LabelText("地表")] TerrainDefinition _terrain;
        [SerializeField, LabelText("基准源图")] Texture2D _source;
        public TerrainDefinition Terrain => _terrain;
        public Texture2D Source => _source;
    }

    [Serializable]
    public sealed class TerrainVariantSource
    {
        [SerializeField, LabelText("规则变体 ID")] string _variantId;
        [SerializeField, LabelText("该变体覆盖的材质源")] TerrainMaterialSource[] _materials = Array.Empty<TerrainMaterialSource>();
        public string VariantId => _variantId;
        public IReadOnlyList<TerrainMaterialSource> Materials => Array.AsReadOnly(_materials);
    }

    [CreateAssetMenu(menuName = "DarkFlare/Maps/Terrain Texture Sources")]
    public sealed class TerrainTextureSources : ScriptableObject
    {
        [SerializeField, LabelText("地表集")] TerrainSetDefinition _terrainSet;
        [SerializeField, LabelText("完整基准材质")] TerrainMaterialSource[] _materials = Array.Empty<TerrainMaterialSource>();
        [SerializeField, LabelText("变体绑定")] TerrainVariantSource[] _variants = Array.Empty<TerrainVariantSource>();
        [SerializeField, ReadOnly, LabelText("最近成功编译")] TerrainTileCatalog _compiled;
        [SerializeField, LabelText("运行时发布目标（可空）")] TerrainPresentationDefinition _presentation;

        public TerrainSetDefinition TerrainSet => _terrainSet;
        public IReadOnlyList<TerrainMaterialSource> Materials => Array.AsReadOnly(_materials);
        public IReadOnlyList<TerrainVariantSource> Variants => Array.AsReadOnly(_variants);
        public TerrainTileCatalog Compiled => _compiled;
        public TerrainPresentationDefinition Presentation => _presentation;

        [Button("编译正式 Tile（整批成功后发布）")]
        void Compile() { TerrainTextureBuild.Compile(this); }

        [Button("发布已编译地表")]
        void Publish() { TerrainPresentationPublisher.Publish(this); }

        [MenuItem("DarkFlare/地图/打开正式泥土样例")]
        static void OpenPilot()
        {
            var sources = AssetDatabase.LoadAssetAtPath<TerrainTextureSources>("Assets/Data/Development/Maps/terrain_pilot_sources.asset");
            MapTerrainPreviewWindow.OpenFormal("Assets/Data/Development/Maps/terrain_pilot.dfmap", sources);
        }

        [MenuItem("DarkFlare/地图/打开正式四材质样例")]
        static void OpenMaterials()
        {
            var sources = AssetDatabase.LoadAssetAtPath<TerrainTextureSources>("Assets/Data/Maps/Terrain/terrain_basic_sources.asset");
            MapTerrainPreviewWindow.OpenFormal("Assets/Data/Development/Maps/terrain_materials.dfmap", sources);
        }

        public TerrainTileCatalog RequireCurrent()
        {
            TerrainTextureBuild.Input input = TerrainTextureBuild.Capture(this);
            TerrainTextureBuild.ValidateArtifact(input, _compiled);
            return _compiled;
        }
    }
}
