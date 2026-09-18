using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class MapTerrainPreviewWindow : OdinEditorWindow
    {
        [SerializeField, HideInInspector] string _sourcePath;
        [SerializeField, HideInInspector] string _sourceGuid;
        [SerializeField, LabelText("Key 画廊"), OnValueChanged(nameof(ScheduleRefresh))] bool _gallery = true;
        [SerializeField, HorizontalGroup("图层"), LabelText("逻辑网格")] bool _logicalGrid;
        [SerializeField, HorizontalGroup("图层"), LabelText("显示网格")] bool _displayGrid;
        [SerializeField, HorizontalGroup("图层"), LabelText("Chunk 边界")] bool _chunkGrid;
        [SerializeField, LabelText("正式材质模式"), HideIf(nameof(_gallery)), OnValueChanged(nameof(ScheduleRefresh))] bool _formal;
        [SerializeField, LabelText("正式材质源"), ShowIf(nameof(ShowFormalSource)), OnValueChanged(nameof(ScheduleRefresh))] TerrainTextureSources _formalSources;
        MapTerrainPreviewSession _session;
        Vector2 _center;
        float _halfHeight = 18;
        bool _fit = true;
        string _inspection = "左键检查格子；滚轮缩放，中键或右键拖动。";
        MapTerrainPreviewFrame _lastFrame;
        GUIStyle _labelStyle;

        [ShowInInspector, LabelText("地图源"), AssetsOnly, HideIf(nameof(_gallery))]
        UnityEngine.Object Source
        {
            get => string.IsNullOrEmpty(_sourcePath) ? null : AssetDatabase.LoadMainAssetAtPath(_sourcePath);
            set
            {
                _sourcePath = AssetDatabase.GetAssetPath(value);
                _sourceGuid = AssetDatabase.AssetPathToGUID(_sourcePath);
                _fit = true;
                ScheduleRefresh();
            }
        }

        public MapTerrainPreviewSession Session => _session;
        bool ShowFormalSource => _formal && !_gallery;

        [ShowInInspector, ReadOnly, LabelText("源路径"), HideIf(nameof(_gallery))]
        string SourcePath => _sourcePath;

        [MenuItem("DarkFlare/地图/地表诊断预览")]
        public static void Open()
        {
            GetWindow<MapTerrainPreviewWindow>("地表诊断预览").Show();
        }

        public static MapTerrainPreviewWindow OpenSource(string path)
        {
            var window = GetWindow<MapTerrainPreviewWindow>("地表诊断预览");
            window._sourcePath = path;
            window._sourceGuid = AssetDatabase.AssetPathToGUID(path);
            window._gallery = false;
            window._formal = false;
            window._fit = true;
            window.ScheduleRefresh();
            window.Show();
            return window;
        }

        public static MapTerrainPreviewWindow OpenFormal(string path, TerrainTextureSources sources)
        {
            var window = OpenSource(path);
            window._formal = true;
            window._formalSources = sources;
            window.ScheduleRefresh();
            return window;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            minSize = new Vector2(700, 560);
            _session = new MapTerrainPreviewSession();
            EditorApplication.projectChanged += ProjectChanged;
            MapPreviewSourceChanges.Changed += ProjectChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseSession;
            EditorApplication.quitting += ReleaseSession;
            ScheduleRefresh();
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= Refresh;
            EditorApplication.projectChanged -= ProjectChanged;
            MapPreviewSourceChanges.Changed -= ProjectChanged;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSession;
            EditorApplication.quitting -= ReleaseSession;
            ReleaseSession();
        }

        void ReleaseSession()
        {
            _session?.Dispose();
            _session = null;
            _lastFrame = null;
        }

        void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode) { ReleaseSession(); }
            if (state == PlayModeStateChange.EnteredEditMode) { ScheduleRefresh(); }
        }

        void ProjectChanged()
        {
            string movedPath = AssetDatabase.GUIDToAssetPath(_sourceGuid);
            if (!string.IsNullOrEmpty(movedPath)) { _sourcePath = movedPath; }
            if (!_gallery) { ScheduleRefresh(); }
        }

        void ScheduleRefresh()
        {
            _session?.Cancel();
            EditorApplication.delayCall -= Refresh;
            EditorApplication.delayCall += Refresh;
        }

        [HorizontalGroup("操作"), Button("重新加载")]
        void Refresh()
        {
            EditorApplication.delayCall -= Refresh;
            if (this == null || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            _session ??= new MapTerrainPreviewSession();
            RefreshAsync().Forget();
        }

        async UniTask RefreshAsync()
        {
            MapTerrainPreviewSession session = _session;
            if (_gallery) { await session.RebuildAsync(); }
            else { await session.RebuildAsync(CaptureSource, _formal ? CaptureTiles : null); }
            if (this != null && _session == session) { Repaint(); }
        }

        (CompiledMapDocument, TerrainRuleSnapshot) CaptureSource()
        {
            if (string.IsNullOrEmpty(_sourcePath) || !string.Equals(Path.GetExtension(_sourcePath), ".dfmap", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("请选择 .dfmap 地图源。");
            }
            IMapDefinitionLookup definitions = MapEditorDefinitionIndex.Capture();
            CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(_sourcePath), definitions);
            TerrainSetDefinition set = definitions.Resolve<TerrainSetDefinition>(map.TerrainSetId);
            return (map, TerrainRuleSnapshot.Compile(set, definitions));
        }

        TerrainTileCatalog CaptureTiles()
        {
            if (_formalSources == null) { throw new InvalidOperationException("请选择正式材质源配置"); }
            return _formalSources.RequireCurrent();
        }

        [HorizontalGroup("操作"), Button("取消构建")]
        void CancelBuild() { _session?.Cancel(); }

        [HorizontalGroup("操作"), Button("适配全图")]
        void Fit() { _fit = true; Repaint(); }

        [OnInspectorGUI]
        void DrawPreview()
        {
            EditorGUILayout.HelpBox(_session?.Status ?? "Play 模式期间暂停预览。", _session != null && _session.IsValid ? MessageType.Info : MessageType.Warning);
            if (_gallery) { EditorGUILayout.LabelField("角标签：上行 NW / NE，下行 SW / SE；D 泥土 · G 草地 · W 水 · R 道路；A / B 为诊断变体。", EditorStyles.miniLabel); }
            EditorGUILayout.LabelField(_inspection, EditorStyles.wordWrappedLabel, GUILayout.Height(80));
            Rect rect = GUILayoutUtility.GetRect(100, Mathf.Max(220, position.height - (_gallery ? 280 : _formal ? 340 : 300)), GUILayout.ExpandWidth(true));
            MapTerrainPreviewFrame frame = _session?.Current;
            if (frame == null) { return; }
            if (_lastFrame != frame)
            {
                if (_lastFrame == null || _lastFrame.IsGallery != frame.IsGallery) { _fit = true; }
                _lastFrame = frame;
            }
            float aspect = rect.width / Mathf.Max(1, rect.height);
            if (_fit)
            {
                _center = frame.Bounds.center;
                _halfHeight = Mathf.Max(frame.Bounds.extents.y, frame.Bounds.extents.x / aspect) * 1.06f;
                _fit = false;
            }
            HandleInput(rect, frame);
            if (Event.current.type == EventType.Repaint)
            {
                Texture texture = frame.Render(Mathf.Max(1, (int)rect.width), Mathf.Max(1, (int)rect.height), _center, _halfHeight);
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
                MapTerrainPreviewOverlay.Draw(rect, frame, _center, _halfHeight, _logicalGrid, _displayGrid, _chunkGrid, ref _labelStyle);
                if (!_session.IsValid) { GUI.Label(new Rect(rect.x + 12, rect.y + 12, rect.width - 24, 30), "过期画面 · 检查已禁用", EditorStyles.whiteLargeLabel); }
            }
            if (_session.IsBuilding) { Repaint(); }
        }

        void HandleInput(Rect rect, MapTerrainPreviewFrame frame)
        {
            Event input = Event.current;
            if (!rect.Contains(input.mousePosition)) { return; }
            if (input.type == EventType.ScrollWheel)
            {
                _halfHeight = Mathf.Clamp(_halfHeight * Mathf.Exp(input.delta.y * 0.08f), 0.05f, 1000000);
                input.Use();
                Repaint();
            }
            else if (input.type == EventType.MouseDrag && (input.button == 1 || input.button == 2))
            {
                _center += new Vector2(-input.delta.x, input.delta.y) * (2 * _halfHeight / rect.height);
                input.Use();
                Repaint();
            }
            else if (input.type == EventType.MouseDown && input.button == 0 && _session.IsValid)
            {
                Vector2 world = _center + new Vector2(input.mousePosition.x - rect.center.x, rect.center.y - input.mousePosition.y) * (2 * _halfHeight / rect.height);
                if (frame.TryInspect(world, out Vector2Int cell, out TerrainWangKey key, out string variant))
                {
                    _inspection = $"显示格 {cell} / Chunk {DualGridCoordinates.GetDisplayChunk(cell.x, cell.y)}\nNW {key.NorthWest}   NE {key.NorthEast}\nSW {key.SouthWest}   SE {key.SouthEast}\nVariant：{variant ?? "基准材质"}";
                }
                input.Use();
                Repaint();
            }
        }
    }
}
