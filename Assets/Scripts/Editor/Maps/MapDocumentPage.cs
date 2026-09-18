using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class MapDocumentPage
    {
        [ShowInInspector, LabelText("地图源资产（只读产物）"), AssetsOnly] UnityEngine.Object _source;
        [ShowInInspector, LabelText("新地图本地 ID")] string _newId = "new_map";
        [ShowInInspector, LabelText("地表集"), AssetsOnly] TerrainSetDefinition _terrainSet;
        [ShowInInspector, LabelText("宽度")] int _width = 64;
        [ShowInInspector, LabelText("高度")] int _height = 64;

        [ShowInInspector, ReadOnly, LabelText("导入摘要 / 错误"), MultiLineProperty(8)]
        public string Status { get; private set; } = "选择 .dfmap 资产后校验。布局请编辑源文件；导入产物不可直接修改。";

        [Button("新建地图源")]
        public void CreateSource()
        {
            Execute(() =>
            {
                if (_terrainSet == null) { throw new MapDocumentException("$.terrainSetId", "请先选择地表集"); }
                string[] palette = _terrainSet.Terrains.Select(terrain => terrain != null ? "terrain:" + terrain.Id : "").ToArray();
                var document = new MapDocument
                {
                    MapId = "map:" + _newId, Width = _width, Height = _height,
                    TerrainSetId = "terrain_set:" + _terrainSet.Id, TerrainPalette = palette,
                    DefaultTerrainId = palette.FirstOrDefault()
                };
                CompiledMapDocument compiled = MapDocumentCompiler.Compile(document, MapEditorDefinitionIndex.Capture());
                string path = EditorUtility.SaveFilePanelInProject("新建地图源", _newId, "dfmap", "保存地图唯一布局源", "Assets/Data/Maps");
                if (string.IsNullOrEmpty(path)) { return; }
                SaveSource(path, compiled.CanonicalJson);
                _source = AssetDatabase.LoadMainAssetAtPath(path);
                ValidateSource();
            });
        }

        [Button("校验并读取摘要")]
        public void ValidateSource()
        {
            Execute(() =>
            {
                string path = GetSourcePath();
                CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(path), MapEditorDefinitionIndex.Capture());
                MapDocument copy = map.CreateEditableCopy();
                Status = $"有效源：{map.MapId}\n尺寸：{map.Width} × {map.Height}，修订：{copy.Revision}\n对象：{copy.Objects.Length}，区域：{copy.Regions.Length}\nSHA256：{map.SourceHash}\n{path}";
            });
        }

        [Button("打开源文件")]
        public void OpenSource()
        {
            Execute(() => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(GetSourcePath(), 1));
        }

        [Button("打开地表诊断预览")]
        public void OpenPreview()
        {
            Execute(() => MapTerrainPreviewWindow.OpenSource(GetSourcePath()));
        }

        [Button("打开地图绘制工作台")]
        public void OpenEditor()
        {
            Execute(() => MapEditorWindow.OpenSource(GetSourcePath()));
        }

        [Button("导入交换 JSON")]
        public void ImportJson()
        {
            Execute(() =>
            {
                string from = EditorUtility.OpenFilePanel("导入地图 JSON", "", "json");
                if (string.IsNullOrEmpty(from)) { return; }
                CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(from), MapEditorDefinitionIndex.Capture());
                string path = EditorUtility.SaveFilePanelInProject("保存地图源", ContentId.Parse(map.MapId).LocalId, "dfmap", "验证成功后保存", "Assets/Data/Maps");
                if (string.IsNullOrEmpty(path)) { return; }
                SaveSource(path, map.CanonicalJson);
                _source = AssetDatabase.LoadMainAssetAtPath(path);
                ValidateSource();
            });
        }

        [Button("导出规范 JSON")]
        public void ExportJson()
        {
            Execute(() =>
            {
                CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(GetSourcePath()), MapEditorDefinitionIndex.Capture());
                string path = EditorUtility.SaveFilePanel("导出地图 JSON", "", ContentId.Parse(map.MapId).LocalId, "json");
                if (string.IsNullOrEmpty(path)) { return; }
                MapEditFiles.Write(path, map.CanonicalJson, MapEditFiles.Hash(path));
                Status = "已导出：" + path;
            });
        }

        public static void SaveSource(string path, string json)
        {
            string fullPath = Path.GetFullPath(path);
            string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase) || !fullPath.EndsWith(".dfmap", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("地图源必须保存在 Assets 内并使用 .dfmap 后缀", nameof(path));
            }
            MapEditorDefinitionIndex.UpdateDependency();
            CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentJson.Read(json), MapEditorDefinitionIndex.Capture());
            string assetPath = "Assets/" + fullPath.Substring(assetsRoot.Length).Replace('\\', '/');
            MapEditSessions.RequireUnique(map.MapId, assetPath);
            if (MapEditSessions.HasOpen(assetPath)) { throw new InvalidOperationException("地图已有编辑会话，请在工作台导入 / 保存"); }
            MapEditFiles.Write(fullPath, map.CanonicalJson, MapEditFiles.Hash(fullPath));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        string GetSourcePath()
        {
            string path = AssetDatabase.GetAssetPath(_source);
            if (!path.EndsWith(".dfmap", StringComparison.OrdinalIgnoreCase)) { throw new MapDocumentException("$", "请选择 .dfmap 资产"); }
            return path;
        }

        void Execute(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is MapDocumentException || error is MapEditException || error is InvalidOperationException || error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                Status = "失败：" + error.Message;
            }
        }
    }
}
