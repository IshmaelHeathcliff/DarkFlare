using System;
using System.IO;
using UnityEditor;

namespace DarkFlare.Editor
{
    public static class MapFormalSamples
    {
        public const string SourcePath = "Assets/Data/Maps/Terrain/terrain_basic_sources.asset";
        public const string TopologyPath = MapDiagnosticSamples.Root + "/terrain_topology.dfmap";
        public const string BoundaryPath = MapDiagnosticSamples.Root + "/terrain_boundary.dfmap";

        public static void EnsureSamples()
        {
            RequireSource();
            SaveMissing(TopologyPath, CreateTopologyDocument());
            SaveMissing(BoundaryPath, CreateBoundaryDocument());
        }

        [MenuItem("DarkFlare/地图/打开正式拓扑样例")]
        public static void OpenTopology()
        {
            EnsureSamples();
            MapTerrainPreviewWindow.OpenFormal(TopologyPath, RequireSource());
        }

        [MenuItem("DarkFlare/地图/打开正式边界样例")]
        public static void OpenBoundary()
        {
            EnsureSamples();
            MapTerrainPreviewWindow.OpenFormal(BoundaryPath, RequireSource());
        }

        [MenuItem("DarkFlare/地图/生成并预览正式 256×256 压力地图")]
        public static void OpenStress()
        {
            TerrainTextureSources source = RequireSource();
            CompiledMapDocument map = MapDocumentCompiler.Compile(CreateStressDocument(), MapEditorDefinitionIndex.Capture());
            const string path = "Temp/DarkFlare/terrain_stress.dfmap";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, MapDocumentJson.Encode(map.CanonicalJson));
            MapTerrainPreviewWindow.OpenFormal(path, source);
        }

        public static MapDocument CreateTopologyDocument() { return UseFormalMaterials(MapDiagnosticSamples.CreateTopologyDocument(), "terrain_topology"); }
        public static MapDocument CreateBoundaryDocument() { return UseFormalMaterials(MapDiagnosticSamples.CreateBoundaryDocument(), "terrain_boundary"); }
        public static MapDocument CreateStressDocument(int seed = 19260817) { return UseFormalMaterials(MapDiagnosticSamples.CreateStressDocument(seed), "terrain_stress"); }

        static MapDocument UseFormalMaterials(MapDocument document, string id)
        {
            document.MapId = "map:" + id;
            document.TerrainSetId = "terrain_set:basic";
            document.TerrainPalette = new[] { "terrain:dirt", "terrain:grass", "terrain:water", "terrain:road" };
            document.DefaultTerrainId = document.TerrainPalette[0];
            foreach (MapVariantOverride variant in document.VariantOverrides)
            {
                string name = variant.VariantId.Substring("preview_".Length);
                variant.VariantId = name.Substring(0, name.Length - 2) + (name.EndsWith("_a", StringComparison.Ordinal) ? "_00" : "_01");
            }
            return document;
        }

        static TerrainTextureSources RequireSource()
        {
            var source = AssetDatabase.LoadAssetAtPath<TerrainTextureSources>(SourcePath);
            if (source == null) { throw new InvalidOperationException("请先创建并编译正式地表源：" + SourcePath); }
            source.RequireCurrent();
            return source;
        }

        static void SaveMissing(string path, MapDocument document)
        {
            if (!File.Exists(path)) { MapDocumentPage.SaveSource(path, MapDocumentJson.Write(document)); }
        }
    }
}
