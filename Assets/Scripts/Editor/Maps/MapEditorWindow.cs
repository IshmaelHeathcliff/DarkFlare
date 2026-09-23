using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkFlare.Editor
{
    public enum MapPaintTool
    {
        [LabelText("画笔 B")] Brush, [LabelText("擦除 E")] Erase, [LabelText("吸管 I")] Eyedropper,
        [LabelText("矩形 R")] Rectangle, [LabelText("直线 L")] Line, [LabelText("洪水填充 F")] Flood,
        [LabelText("显示格变体 V")] Variant, [LabelText("选择 / 移动 Q")] Select,
        [LabelText("放置对象 O")] Object, [LabelText("矩形区域 Z")] Region, [LabelText("放置入口 P")] Entry
    }

    public sealed partial class MapEditorWindow : OdinEditorWindow
    {
        static MapEditorWindow s_activeEditor;
        [SerializeField, HideInInspector] string _path;
        [SerializeField, TabGroup("文档"), LabelText("源地图"), AssetsOnly] MapDefinition _source;
        [SerializeField, TabGroup("文档"), LabelText("新地图 ID")] string _newId = "new_map";
        [SerializeField, TabGroup("文档"), LabelText("地表集"), AssetsOnly] TerrainSetDefinition _set;
        [SerializeField, TabGroup("文档"), LabelText("新图尺寸")] Vector2Int _size = new(64, 64);
        [SerializeField, TabGroup("文档"), LabelText("正式地表资源"), AssetsOnly, OnValueChanged(nameof(TerrainMaterialsChanged))] TerrainTextureSources _materials;
        [SerializeField, TabGroup("绘制"), LabelText("工具"), EnumToggleButtons] MapPaintTool _tool;
        [SerializeField, TabGroup("绘制"), LabelText("地表"), ValueDropdown(nameof(TerrainChoices))] string _terrain = "terrain:grass";
        [SerializeField, TabGroup("绘制"), LabelText("笔刷边长"), Range(1, 16)] int _brushSize = 1;
        [SerializeField, TabGroup("绘制"), LabelText("显示格变体（空则清除）"), ValueDropdown(nameof(VariantChoices))] string _variant = "";
        [SerializeField, TabGroup("绘制"), LabelText("重绘时清除受影响变体")] bool _clearOverrides;
        [SerializeField, TabGroup("绘制"), LabelText("锁定地表")] bool _lockTerrain;
        [SerializeField, TabGroup("绘制"), LabelText("显示地表")] bool _showTerrain = true;
        [SerializeField, TabGroup("绘制"), LabelText("显示网格")] bool _showGrid = true;
        [SerializeField, TabGroup("绘制"), LabelText("显示格网格")] bool _displayGrid;
        [SerializeField, TabGroup("对象与区域"), LabelText("显示对象")] bool _showObjects = true;
        [SerializeField, TabGroup("对象与区域"), LabelText("显示全部对象标签（默认仅选中）")] bool _showObjectLabels;
        [SerializeField, TabGroup("对象与区域"), LabelText("锁定对象")] bool _lockObjects;
        [SerializeField, TabGroup("对象与区域"), LabelText("显示区域")] bool _showRegions = true;
        [SerializeField, TabGroup("对象与区域"), LabelText("锁定区域")] bool _lockRegions;
        [SerializeField, TabGroup("对象与区域"), LabelText("目录搜索")] string _objectSearch = "";
        [SerializeField, TabGroup("对象与区域"), LabelText("能力筛选")] MapObjectCapabilities _capabilityFilter;
        [SerializeField, TabGroup("对象与区域"), LabelText("对象定义"), AssetsOnly, ValueDropdown(nameof(ObjectChoices))] MapObjectDefinition _objectDefinition;
        [SerializeField, TabGroup("对象与区域"), LabelText("对象 ID")] string _objectId = "object";
        [SerializeField, TabGroup("对象与区域"), LabelText("对象位置（局部世界）")] Vector2 _objectPosition;
        [SerializeField, TabGroup("对象与区域"), LabelText("旋转角度"), ValueDropdown(nameof(Rotations))] int _rotation;
        [SerializeField, TabGroup("对象与区域"), LabelText("外观变体"), ValueDropdown(nameof(AppearanceChoices))] string _appearance = "";
        [SerializeField, TabGroup("对象与区域"), LabelText("交互定义"), AssetsOnly] ScriptableObject _interaction;
        [SerializeField, TabGroup("对象与区域"), LabelText("出口目标地图 ID")] string _targetMapId;
        [SerializeField, TabGroup("对象与区域"), LabelText("出口目标入口 ID")] string _targetEntryId;
        [SerializeField, TabGroup("对象与区域"), LabelText("区域定义"), AssetsOnly] MapRegionDefinition _regionDefinition;
        [SerializeField, TabGroup("对象与区域"), LabelText("区域 ID")] string _regionId = "region";
        [SerializeField, TabGroup("对象与区域"), LabelText("区域优先级")] int _priority;
        [SerializeField, TabGroup("对象与区域"), LabelText("区域形状")] MapRegionShape _shape;
        [SerializeField, TabGroup("对象与区域"), LabelText("区域顶点（逻辑格单位）")] List<Vector2> _points = new() { Vector2.one, new Vector2(4, 4) };
        [SerializeField, TabGroup("对象与区域"), LabelText("刷怪定义"), AssetsOnly] MonsterSpawnDefinition _spawn;
        [SerializeField, TabGroup("诊断与 AI"), LabelText("起点（世界）")] Vector2 _start = new(2, 2);
        [SerializeField, TabGroup("诊断与 AI"), LabelText("终点（世界）")] Vector2 _end = new(6, 6);
        [SerializeField, TabGroup("诊断与 AI"), LabelText("单位半径"), MinValue(0.001)] double _radius = 0.36;
        [SerializeField, TabGroup("诊断与 AI"), LabelText("Patch JSON"), TextArea(5, 15)] string _patchJson = "";
        [SerializeField, TabGroup("文档"), LabelText("元数据 JSON（白名单字段）"), TextArea(2, 5)] string _metadata = "{}";
        MapEditSession _session;
        MapTerrainPreviewSession _preview;
        MapEditingStage _stage;
        MapDocument _viewDocument;
        IMapDefinitionLookup _lookup;
        readonly HashSet<Vector2Int> _stroke = new();
        readonly HashSet<string> _selection = new();
        Vector2Int _anchor, _last;
        bool _dragging;
        bool _strokeOverflow;
        int _control;
        string _strokeHash;
        string _status = "打开或新建地图，然后进入绘制舞台。SceneView：左键绘制，中键 / Alt 浏览，Esc 取消。";
        bool _fit;
        string _selectedRegion;
        string _geometryStatus = "几何未构建";
        MapGeometrySnapshot _geometry;
        MapObjectDefinition[] _objectCatalog = Array.Empty<MapObjectDefinition>();

        public MapEditSession Session => _session;
        public MapTerrainPreviewSession Preview => _preview;
        public MapEditingStage EditingStage => _stage;
        IEnumerable<string> TerrainChoices => _session?.Current.TerrainPalette.Select(id => id.ToString()) ?? Array.Empty<string>();
        IEnumerable<string> VariantChoices => new[] { "" }.Concat(_set != null ? _set.Variants.Select(item => item.Id) : Array.Empty<string>());
        IEnumerable<int> Rotations => new[] { 0, 90, 180, 270 };
        IEnumerable<string> AppearanceChoices => new[] { "" }.Concat(_objectDefinition != null ? _objectDefinition.AppearanceVariants : Array.Empty<string>());
        IEnumerable<ValueDropdownItem<MapObjectDefinition>> ObjectChoices => _objectCatalog.Where(item => item != null && (item.Capabilities & _capabilityFilter) == _capabilityFilter && item.Id.Contains(_objectSearch ?? "", StringComparison.OrdinalIgnoreCase)).Select(item => new ValueDropdownItem<MapObjectDefinition>(item.Id + " · " + item.Capabilities, item));

        [MenuItem("DarkFlare/地图/地图编辑器")]
        public static void Open() { GetWindow<MapEditorWindow>("地图编辑器").Show(); }

        public static MapEditorWindow OpenSource(string path)
        {
            var window = GetWindow<MapEditorWindow>("地图编辑器");
            window.Attach(MapEditSessions.Open(path)); window.EnterStage(); window.Show(); return window;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            minSize = new Vector2(430, 520);
            SceneView.duringSceneGui += DrawScene;
            EditorApplication.projectChanged += DefinitionsChanged;
            EditorApplication.playModeStateChanged += PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleasePreview;
            if (!string.IsNullOrEmpty(_path)) { EditorApplication.delayCall += Reopen; }
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= Reopen; EditorApplication.delayCall -= Rebuild;
            SceneView.duringSceneGui -= DrawScene;
            EditorApplication.projectChanged -= DefinitionsChanged;
            EditorApplication.playModeStateChanged -= PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleasePreview;
            if (_session != null) { _session.Changed -= Changed; _session.Closed -= SessionClosed; _session.Checkpoint(); }
            ReleasePreview();
        }

        void Reopen() { if (this != null && !EditorApplication.isPlayingOrWillChangePlaymode) { Execute(() => Attach(MapEditSessions.Open(_path))); } }
        void OnLostFocus() { if (focusedWindow is not SceneView) { CancelStroke(); } }
        void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { ReleasePreview(); }
            if (state == PlayModeStateChange.EnteredEditMode) { Reopen(); }
        }

        void ReleasePreview()
        {
            ReleaseObjectPreview();
            CancelStroke(); _preview?.Dispose(); _preview = null; _geometry = null;
            if (_stage != null && StageUtility.GetCurrentStage() == _stage) { StageUtility.GoBackToPreviousStage(); }
            _stage = null;
            if (s_activeEditor == this) { s_activeEditor = null; }
        }

        void Attach(MapEditSession session)
        {
            if (_session != session && _session != null && _session.IsDirty)
            {
                int choice = EditorUtility.DisplayDialogComplex("未保存地图", "切换前处理当前地图。", "保存", "取消", "放弃");
                if (choice == 1) { return; }
                if (choice == 0) { _session.Save(); } else { _session.Discard(); }
            }
            if (_session != null) { _session.Changed -= Changed; _session.Closed -= SessionClosed; }
            CancelStroke(); _session = session; _path = session.SourcePath;
            _source = AssetDatabase.LoadAssetAtPath<MapDefinition>(_path);
            _lookup = MapEditorDefinitionIndex.Capture();
            _objectCatalog = MapEditorDefinitionIndex.LoadDefinitions().OfType<MapObjectDefinition>().ToArray();
            _set = _lookup.Resolve<TerrainSetDefinition>(session.Current.TerrainSetId);
            _materials ??= AssetDatabase.LoadAssetAtPath<TerrainTextureSources>(MapFormalSamples.SourcePath);
            _session.Changed += Changed; _session.Closed += SessionClosed; _fit = true; Changed();
        }

        public void LoadSource(string path) { Attach(MapEditSessions.Open(path)); }

        void SessionClosed()
        {
            ReleasePreview(); _session = null; hasUnsavedChanges = false; Repaint();
        }

        [TabGroup("文档"), Button("打开选择的地图")]
        void OpenSelected() { Execute(() => { Attach(MapEditSessions.Open(AssetDatabase.GetAssetPath(_source))); EnterStage(); }); }

        [TabGroup("文档"), Button("新建空地图")]
        void NewMap()
        {
            Execute(() =>
            {
                if (_set == null) { throw new InvalidOperationException("请选择地表集"); }
                string path = EditorUtility.SaveFilePanelInProject("创建地图", _newId, "dfmap", "创建唯一布局源");
                if (string.IsNullOrEmpty(path)) { return; }
                string[] palette = _set.Terrains.Select(item => "terrain:" + item.Id).ToArray();
                Attach(MapEditSessions.Create(path, new MapDocument { MapId = "map:" + _newId, Width = _size.x, Height = _size.y, TerrainSetId = "terrain_set:" + _set.Id, TerrainPalette = palette, DefaultTerrainId = palette[0] }));
                EnterStage();
            });
        }

        [TabGroup("文档"), Button("保存 Ctrl/Cmd+S")]
        public override void SaveChanges()
        {
            Execute(() => { RequireSession(); _session.Save(); hasUnsavedChanges = false; });
        }

        public override void DiscardChanges()
        {
            Execute(() => { _session?.Discard(); hasUnsavedChanges = false; });
        }

        [TabGroup("文档"), Button("另存为新地图")]
        void SaveAs()
        {
            Execute(() =>
            {
                RequireSession(); string path = EditorUtility.SaveFilePanelInProject("另存为", _newId, "dfmap", "生成新的地图身份");
                if (string.IsNullOrEmpty(path)) { return; }
                MapDocument copy = _session.Current.CreateEditableCopy(); copy.MapId = "map:" + _newId; copy.Revision = 0;
                Attach(MapEditSessions.Create(path, copy));
            });
        }

        [TabGroup("文档"), Button("导入 JSON（预检差异）")]
        void Import()
        {
            Execute(() =>
            {
                RequireSession(); string path = EditorUtility.OpenFilePanel("导入 JSON", "", "json");
                if (string.IsNullOrEmpty(path)) { return; }
                MapDocument dto = MapDocumentImporter.ReadSource(path);
                if (dto.MapId != _session.Current.MapId) { throw new InvalidOperationException("导入必须保持 MapId；使用新建 / 另存为创建独立身份"); }
                dto.Revision = _session.Current.Revision;
                CompiledMapDocument candidate = MapDocumentCompiler.Compile(dto, MapEditorDefinitionIndex.Capture());
                string diff = MapEditApi.Diff(_session.Current, candidate).ToString();
                if (EditorUtility.DisplayDialog("导入差异", diff, "应用为一次撤销事务", "取消")) { _session.Import(candidate.CanonicalJson); }
            });
        }

        [TabGroup("文档"), Button("导出规范 JSON")]
        void Export()
        {
            Execute(() =>
            {
                RequireSession(); string path = EditorUtility.SaveFilePanel("导出 JSON", "", ContentId.Parse(_session.Current.MapId).LocalId, "json");
                if (!string.IsNullOrEmpty(path)) { MapEditFiles.Write(path, _session.Current.CanonicalJson, MapEditFiles.Hash(path)); }
            });
        }

        [TabGroup("文档"), Button("应用元数据（不静默裁剪）")]
        void Metadata()
        {
            Execute(() =>
            {
                RequireSession();
                var operation = new JObject { ["type"] = "SetMapMetadata", ["values"] = JObject.Parse(_metadata) };
                CompiledMapDocument candidate = _session.Preview(MapEditService.Patch(_session.Current, operation));
                string diff = MapEditApi.Diff(_session.Current, candidate).ToString();
                string size = $"范围 {_session.Current.Width}×{_session.Current.Height} → {candidate.Width}×{candidate.Height}；格宽 {_session.Current.CellSize} → {candidate.CellSize}\n对象位置保持局部世界单位，区域随逻辑格尺度变化；所有派生预览将重建。\n";
                if (EditorUtility.DisplayDialog("元数据变更预检", size + diff, "应用", "取消")) { Apply(operation); }
            });
        }

        [TabGroup("绘制"), Button("进入 / 适配绘制舞台")]
        public void EnterStage()
        {
            RequireSession();
            if (s_activeEditor != null && s_activeEditor != this) { s_activeEditor.ReleasePreview(); }
            s_activeEditor = this; _stage = MapEditingStage.Enter(); _fit = true; SchedulePreview();
        }

        [TabGroup("绘制"), Button("撤销")]
        void UndoEdit() { Undo.PerformUndo(); }
        [TabGroup("绘制"), Button("重做")]
        void RedoEdit() { Undo.PerformRedo(); }

        [TabGroup("对象与区域"), Button("应用对象属性")]
        void UpsertObject() { Execute(() => { if (_lockObjects) { throw new InvalidOperationException("对象层已锁定"); } Apply(ObjectOperation(_objectId, _objectPosition)); }); }

        JObject ObjectOperation(string id, Vector2 position)
        {
            if (_objectDefinition == null) { throw new InvalidOperationException("请选择对象定义"); }
            MapInteractionKind kind = _objectDefinition.InteractionKind;
            string content = _interaction is TraderDefinition trader ? "trader:" + trader.Id : _interaction is CraftingDefinition craft ? "crafting:" + craft.Id : null;
            return new JObject { ["type"] = "UpsertObject", ["record"] = new JObject
            {
                ["objectId"] = id, ["definitionId"] = "map_object:" + _objectDefinition.Id,
                ["position"] = new JObject { ["x"] = position.x, ["y"] = position.y }, ["rotation"] = _rotation,
                ["appearanceVariant"] = string.IsNullOrEmpty(_appearance) ? null : _appearance,
                ["interaction"] = kind == MapInteractionKind.None ? null : new JObject
                {
                    ["kind"] = kind.ToString(), ["contentId"] = kind == MapInteractionKind.Portal ? null : content,
                    ["targetMapId"] = kind == MapInteractionKind.Portal ? _targetMapId : null,
                    ["targetEntryId"] = kind == MapInteractionKind.Portal ? _targetEntryId : null
                }
            } };
        }

        [TabGroup("对象与区域"), Button("复制选中对象")]
        void Duplicate()
        {
            Execute(() =>
            {
                RequireSession();
                if (_lockObjects) { throw new InvalidOperationException("对象层已锁定"); }
                var operations = new List<JObject>();
                foreach (MapObjectRecord record in _viewDocument.Objects.Where(item => _selection.Contains(item.ObjectId)))
                {
                    JObject obj = JObject.Parse(MapDocumentJson.Write(_viewDocument))["objects"].First(item => (string)item["objectId"] == record.ObjectId) as JObject;
                    obj["objectId"] = "object_" + Guid.NewGuid().ToString("N");
                    operations.Add(new JObject { ["type"] = "UpsertObject", ["record"] = obj });
                }
                Apply(operations.ToArray());
            });
        }

        [TabGroup("对象与区域"), Button("删除选中对象 / 区域")]
        void DeleteSelection()
        {
            Execute(() =>
            {
                var ops = new List<JObject>();
                if (!_lockObjects) { ops.AddRange(_selection.Select(id => new JObject { ["type"] = "RemoveObject", ["id"] = id })); }
                if (!_lockRegions && _selectedRegion != null) { ops.Add(new JObject { ["type"] = "RemoveRegion", ["id"] = _selectedRegion }); }
                Apply(ops.ToArray()); _selection.Clear(); _selectedRegion = null;
            });
        }

        [TabGroup("对象与区域"), Button("应用区域 / 顶点属性")]
        void UpsertRegion() { Execute(() => { if (_lockRegions) { throw new InvalidOperationException("区域层已锁定"); } Apply(RegionOperation()); }); }

        JObject RegionOperation()
        {
            if (_regionDefinition == null) { throw new InvalidOperationException("请选择区域定义"); }
            return new JObject { ["type"] = "UpsertRegion", ["record"] = new JObject
            {
                ["regionId"] = _regionId, ["definitionId"] = "map_region:" + _regionDefinition.Id, ["priority"] = _priority,
                ["shape"] = _shape.ToString(), ["points"] = new JArray(_points.Select(point => new JObject { ["x"] = point.x, ["y"] = point.y })),
                ["spawnDefinitionId"] = _spawn != null ? "spawn:" + _spawn.Id : null
            } };
        }

        [TabGroup("诊断与 AI"), Button("构建静态几何并诊断连通")]
        void Diagnose()
        {
            Execute(() =>
            {
                RequireSession(); _geometry = null; _geometryStatus = "GeometryUnavailable · 诊断尚未完成";
                _geometry = MapGeometryCompiler.Compile(MapGeometryInput.Capture(_session.Current, MapEditorDefinitionIndex.Capture()));
                _geometryStatus = MapEditConnectivity.Check(_geometry, new MapGeometryPoint(_start.x, _start.y), new MapGeometryPoint(_end.x, _end.y), new MapGroundProfile(_radius));
                _status = "静态连通：" + _geometryStatus; SceneView.RepaintAll();
            });
        }

        [TabGroup("诊断与 AI"), Button("读取地图与空 Patch")]
        void ReadPatch() { Execute(() => { RequireSession(); _patchJson = MapEditService.Patch(_session.Current).ToString(); _status = MapEditApi.ReadMap(_path); }); }
        [TabGroup("诊断与 AI"), Button("预检 Patch")]
        void PreviewPatch() { Execute(() => { RequireSession(); _status = MapEditApi.PreviewPatch(_path, _patchJson); }); }
        [TabGroup("诊断与 AI"), Button("应用 Patch（不保存）")]
        void ApplyPatch() { Execute(() => { RequireSession(); _status = _session.Apply(MapEditService.ReadPatch(_patchJson)).ToString(); }); }

        [TabGroup("诊断与 AI"), Button("诊断起点到所选交互对象的周边站位")]
        void DiagnoseInteraction()
        {
            Execute(() =>
            {
                RequireSession();
                if (_objectDefinition == null || _objectDefinition.InteractionKind == MapInteractionKind.None || !_selection.Contains(_objectId)) { throw new InvalidOperationException("先选择一个交互对象"); }
                _geometry = null; _geometryStatus = "GeometryUnavailable";
                _geometry = MapGeometryCompiler.Compile(MapGeometryInput.Capture(_session.Current, MapEditorDefinitionIndex.Capture()));
                var profile = new MapGroundProfile(_radius);
                Vector2 size = _rotation % 180 == 0 ? _objectDefinition.Footprint : new Vector2(_objectDefinition.Footprint.y, _objectDefinition.Footprint.x);
                Vector2 center = _objectPosition + new Vector2((float)_session.Current.OriginX, (float)_session.Current.OriginY);
                float margin = (float)(profile.Clearance + _geometry.NodeSize);
                var candidates = new[] { center + Vector2.right * (size.x / 2 + margin), center + Vector2.left * (size.x / 2 + margin), center + Vector2.up * (size.y / 2 + margin), center + Vector2.down * (size.y / 2 + margin) };
                _geometryStatus = "NoLegalEndpoint";
                foreach (Vector2 candidate in candidates.OrderBy(p => Vector2.Distance(_start, p)))
                {
                    string result = MapEditConnectivity.Check(_geometry, new MapGeometryPoint(_start.x, _start.y), new MapGeometryPoint(candidate.x, candidate.y), profile);
                    if (result == "NoLegalEndpoint") { continue; }
                    _geometryStatus = result; _end = candidate;
                    if (result == "Connected" || result == "BudgetExceeded") { break; }
                }
                _status = "交互周边站位：" + _geometryStatus; SceneView.RepaintAll();
            });
        }

        [OnInspectorGUI]
        void Footer()
        {
            EditorGUILayout.HelpBox(_status, MessageType.Info);
            if (_session != null)
            {
                EditorGUILayout.LabelField($"{_session.Current.MapId} · r{_session.Current.Revision} · {(_session.IsDirty ? "未保存" : "已保存")}");
                EditorGUILayout.HelpBox(_session.Notice + "\n" + (_preview?.Status ?? "舞台未开启") + "\n" + _geometryStatus, MessageType.None);
                EditorGUILayout.HelpBox(_objectStatus, MessageType.None);
            }
        }

        public void Apply(params JObject[] operations)
        {
            RequireSession();
            JObject patch = MapEditService.Patch(_session.Current, operations); patch["clearAffectedOverrides"] = _clearOverrides;
            _session.Apply(patch);
        }

        void RequireSession()
        {
            if (_session == null) { throw new InvalidOperationException("请先打开地图"); }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Play 期间暂停编辑"); }
        }

        void Changed()
        {
            CancelStroke(); _path = _session.SourcePath; _viewDocument = _session.Current.CreateEditableCopy();
            hasUnsavedChanges = _session.IsDirty; saveChangesMessage = "地图有未保存的工作副本。保存、放弃或取消关闭。";
            _geometry = null; _geometryStatus = "几何已失效，需重新诊断";
            SchedulePreview(); Repaint();
        }

        void DefinitionsChanged() { _lookup = MapEditorDefinitionIndex.Capture(); _objectCatalog = MapEditorDefinitionIndex.LoadDefinitions().OfType<MapObjectDefinition>().ToArray(); _geometry = null; _geometryStatus = "定义已改变，请重新诊断"; CancelStroke(); SchedulePreview(); }
        void SchedulePreview()
        {
            string key = _session == null ? null : TerrainPreviewKey();
            if (_session == null || key != _terrainPreviewKey) { _preview?.Cancel(); }
            EditorApplication.delayCall -= Rebuild; EditorApplication.delayCall += Rebuild;
        }
        void Rebuild()
        {
            if (this == null || _session == null || _stage == null || StageUtility.GetCurrentStage() != _stage || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            RebuildAsync().Forget();
        }
        async UniTask RebuildAsync()
        {
            RebuildObjectsAsync().Forget();
            string terrainKey = TerrainPreviewKey();
            if (_preview != null && (_preview.IsValid || _preview.IsBuilding) && _terrainPreviewKey == terrainKey) { return; }
            _preview ??= new MapTerrainPreviewSession();
            MapTerrainPreviewSession preview = _preview;
            _terrainPreviewKey = terrainKey;
            await preview.RebuildAsync(() =>
            {
                IMapDefinitionLookup definitions = MapEditorDefinitionIndex.Capture();
                CompiledMapDocument map = MapDocumentCompiler.Compile(_session.Current.CreateEditableCopy(), definitions);
                return (map, TerrainRuleSnapshot.Compile(definitions.Resolve<TerrainSetDefinition>(map.TerrainSetId), definitions));
            }, _materials != null && _materials.TerrainSet != null && "terrain_set:" + _materials.TerrainSet.Id == _session.Current.TerrainSetId.ToString() ? () => _materials.RequireCurrent() : null);
            if (this == null || _preview != preview || _stage == null || _terrainPreviewKey != terrainKey) { return; }
            if (!preview.IsValid) { if (_terrainPreviewKey == terrainKey) { _terrainPreviewKey = null; } return; }
            preview.Current.MoveToScene(_stage.scene);
            if (_fit && SceneView.lastActiveSceneView != null) { SceneView.lastActiveSceneView.Frame(preview.Current.Bounds, true); _fit = false; }
            SceneView.RepaintAll(); Repaint();
        }

        void Execute(Action action)
        {
            try { action(); }
            catch (Exception error) { _status = (error is MapEditException edit ? edit.Code + " " + edit.Path : error is MapGeometryCapacityException ? "GeometryUnavailable" : "失败") + "：" + error.Message; }
            Repaint();
        }
    }
}
