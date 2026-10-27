using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DarkFlare.Editor
{
    [ScriptedImporter(1, "dfmap")]
    public sealed class MapDocumentImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            context.DependsOnCustomDependency(MapEditorDefinitionIndex.DependencyName);
            try
            {
                CompiledMapDocument document = MapDocumentCompiler.Compile(ReadSource(context.assetPath), MapEditorDefinitionIndex.Capture(context));
                var asset = ScriptableObject.CreateInstance<MapDefinition>();
                asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
                string history = context.assetPath == "Assets/Data/Maps/World/town.dfmap" ? "Assets/Data/Maps/History/town-r1.json"
                    : context.assetPath == "Assets/Data/Maps/World/wilderness.dfmap" ? "Assets/Data/Maps/History/wilderness-r9.json" : null;
                string previous = null;
                if (history != null)
                {
                    context.DependsOnSourceAsset(history);
                    previous = File.ReadAllText(history, System.Text.Encoding.UTF8);
                }
                asset.Initialize(document, previous);
                context.AddObjectToAsset("map_definition", asset);
                context.SetMainObject(asset);
            }
            catch (Exception error) when (error is MapDocumentException || error is IOException || error is UnauthorizedAccessException)
            {
                // 明确替换成功产物；任何旧 MapDefinition 引用失效，不能发布过期布局。
                var report = new TextAsset(error.Message) { name = "地图导入失败" };
                context.AddObjectToAsset("import_error", report);
                context.SetMainObject(report);
                context.LogImportError(error.Message);
            }
        }

        public static MapDocument ReadSource(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MapDocumentLimits.MaxBytes) { throw new MapDocumentException("$", "文件超过 8 MiB"); }
            byte[] bytes = new byte[(int)stream.Length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = stream.Read(bytes, offset, bytes.Length - offset);
                if (count == 0) { throw new IOException("读取地图时文件长度发生变化"); }
                offset += count;
            }
            return MapDocumentJson.Read(bytes);
        }
    }

    public sealed class MapBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var issues = MapEditorValidation.Scan();
            if (issues.Count > 0) { throw new BuildFailedException("地图校验失败：\n" + string.Join("\n", issues.ConvertAll(issue => issue.Message))); }
        }
    }
}
