using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public static class MapEditorValidation
    {
        public static List<ContentValidationIssue> Scan()
        {
            var issues = new List<ContentValidationIssue>();
            List<ScriptableObject> definitions = MapEditorDefinitionIndex.LoadDefinitions();
            var lookup = new MapDefinitionLookup(definitions);
            foreach (var group in definitions.GroupBy(asset => asset.GetType().FullName + ":" + ((IContentDefinition)asset).Id))
            {
                if (group.Count() > 1)
                {
                    foreach (ScriptableObject asset in group)
                    {
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, asset, "地图定义索引 ID 冲突：" + group.Key));
                    }
                }
            }
            foreach (ScriptableObject asset in definitions)
            {
                if (!(asset is TerrainDefinition || asset is TerrainSetDefinition || asset is MapObjectDefinition || asset is MapRegionDefinition)) { continue; }
                try { MapDocumentCompiler.ValidateDefinition(asset, lookup); }
                catch (MapDocumentException error) { issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, asset, error.Message)); }
            }
            var mapIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".dfmap", StringComparison.OrdinalIgnoreCase)))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                try
                {
                    CompiledMapDocument compiled = MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(path), lookup);
                    if (!mapIds.Add(compiled.MapId)) { throw new MapDocumentException("$.mapId", "地图 ID 重复"); }
                    if (!(asset is MapDefinition map) || map.SourceHash != compiled.SourceHash)
                    {
                        throw new MapDocumentException("$", "导入产物缺失或已过期，请重新导入");
                    }
                }
                catch (Exception error) when (error is MapDocumentException || error is IOException || error is UnauthorizedAccessException)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, asset, path + ": " + error.Message));
                }
            }
            return issues;
        }
    }
}
