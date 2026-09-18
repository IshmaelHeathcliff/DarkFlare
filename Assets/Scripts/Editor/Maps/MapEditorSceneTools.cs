using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed partial class MapEditorWindow
    {
        public static Vector2Int LogicalCell(CompiledMapDocument map, Vector2 world)
        {
            return new Vector2Int((int)Math.Floor((world.x - map.OriginX) / map.CellSize), (int)Math.Floor((world.y - map.OriginY) / map.CellSize));
        }

        void DrawScene(SceneView view)
        {
            if (_stage != null && StageUtility.GetCurrentStage() != _stage) { ReleasePreview(); }
            if (_session == null || _stage == null || StageUtility.GetCurrentStage() != _stage || EditorApplication.isPlayingOrWillChangePlaymode) { CancelStroke(); return; }
            CompiledMapDocument map = _session.Current;
            if (_preview?.Current?.Renderer != null) { _preview.Current.Renderer.enabled = _showTerrain; }
            Event input = Event.current;
            _control = GUIUtility.GetControlID("DarkFlare.MapPaint".GetHashCode(), FocusType.Passive);
            if (input.type == EventType.Layout && !input.alt) { HandleUtility.AddDefaultControl(_control); }
            DrawOverlays(map);
            Handles.BeginGUI();
            GUI.Label(new Rect(12, 10, 640, 45), $"{map.MapId} · r{map.Revision} · {_tool} · {(_session.IsDirty ? "未保存" : "已保存")}\n{(_preview?.IsValid == true ? "" : "地表画面过期 / 构建中")}  Esc 取消 · Q 选择 · B 画笔 · Ctrl+S 保存", EditorStyles.whiteLabel);
            Handles.EndGUI();
            if (input.type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
            {
                if ((input.control || input.command) && input.keyCode == KeyCode.S) { SaveChanges(); input.Use(); return; }
                if ((input.control || input.command) && input.keyCode == KeyCode.Z) { if (input.shift) { Undo.PerformRedo(); } else { Undo.PerformUndo(); } input.Use(); return; }
                if ((input.control || input.command) && input.keyCode == KeyCode.Y) { Undo.PerformRedo(); input.Use(); return; }
                if (input.keyCode == KeyCode.Escape) { CancelStroke(); input.Use(); return; }
                if (input.keyCode == KeyCode.Delete) { DeleteSelection(); input.Use(); return; }
                if (!input.control && !input.command && !input.alt)
                {
                    string keys = "beirlfvqoz";
                    int key = keys.IndexOf(char.ToLowerInvariant((char)input.keyCode));
                    if (key >= 0) { CancelStroke(); _tool = (MapPaintTool)key; input.Use(); Repaint(); return; }
                }
            }
            if (input.alt || EditorGUIUtility.editingTextField) { return; }
            Ray ray = HandleUtility.GUIPointToWorldRay(input.mousePosition);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) { return; }
            Vector2 world = ray.GetPoint(distance);
            Vector2Int cell = LogicalCell(map, world);
            bool inside = cell.x >= 0 && cell.y >= 0 && cell.x < map.Width && cell.y < map.Height;
            if (_tool == MapPaintTool.Variant)
            {
                cell = LogicalCell(map, world - Vector2.one * (float)(map.CellSize * 0.5));
                inside = cell.x >= -1 && cell.y >= -1 && cell.x < map.Width && cell.y < map.Height;
            }
            if (input.type == EventType.MouseDown && input.button == 0 && inside)
            {
                if (_tool == MapPaintTool.Eyedropper) { _terrain = map.GetTerrain(cell.x, cell.y); input.Use(); Repaint(); return; }
                if (_tool == MapPaintTool.Select) { SelectAt(world, input.shift); input.Use(); return; }
                if ((_tool <= MapPaintTool.Variant && _lockTerrain) || (_tool == MapPaintTool.Object && _lockObjects) || (_tool == MapPaintTool.Region && _lockRegions)) { return; }
                _anchor = _last = cell; _strokeHash = map.SourceHash; _dragging = true; _strokeOverflow = false; _stroke.Clear();
                AddBrush(cell, map); GUIUtility.hotControl = _control; input.Use();
            }
            else if (_dragging && input.type == EventType.MouseDrag && input.button == 0)
            {
                if (inside)
                {
                    if (_tool == MapPaintTool.Brush || _tool == MapPaintTool.Erase)
                    { foreach (var p in MapEditService.Line(_last.x, _last.y, cell.x, cell.y)) { AddBrush(new Vector2Int(p.X, p.Y), map); } }
                    _last = cell;
                }
                input.Use(); view.Repaint();
            }
            else if (_dragging && input.type == EventType.MouseUp && input.button == 0)
            {
                Execute(() => CompleteStroke(map)); CancelStroke(); input.Use();
            }
            if (_dragging) { view.Repaint(); }
        }

        void AddBrush(Vector2Int cell, CompiledMapDocument map)
        {
            for (int y = 0; y < _brushSize; y++)
            {
                for (int x = 0; x < _brushSize; x++)
                {
                    var p = cell + new Vector2Int(x - _brushSize / 2, y - _brushSize / 2);
                    if (p.x >= 0 && p.y >= 0 && p.x < map.Width && p.y < map.Height)
                    {
                        if (_stroke.Count >= MapEditService.MaxVisits && !_stroke.Contains(p)) { _strokeOverflow = true; }
                        else { _stroke.Add(p); }
                    }
                }
            }
        }

        void CompleteStroke(CompiledMapDocument map)
        {
            if (_strokeOverflow) { throw new MapEditException("BudgetExceeded", "$stroke", "笔画超出访问预算，请拆分；整笔未提交"); }
            if (map.SourceHash != _strokeHash) { throw new MapEditException("Conflict", "$", "笔画期间地图已改变，重试笔画"); }
            string terrain = _tool == MapPaintTool.Erase ? map.DefaultTerrainId.ToString() : _terrain;
            JObject operation;
            switch (_tool)
            {
                case MapPaintTool.Brush: case MapPaintTool.Erase:
                    operation = new JObject { ["type"] = "PaintCells", ["terrainId"] = terrain, ["cells"] = new JArray(_stroke.Select(p => new JObject { ["x"] = p.x, ["y"] = p.y })) }; break;
                case MapPaintTool.Rectangle:
                    operation = new JObject { ["type"] = "FillRect", ["terrainId"] = terrain, ["x"] = Math.Min(_anchor.x, _last.x), ["y"] = Math.Min(_anchor.y, _last.y), ["width"] = Math.Abs(_last.x - _anchor.x) + 1, ["height"] = Math.Abs(_last.y - _anchor.y) + 1 }; break;
                case MapPaintTool.Line:
                    operation = new JObject { ["type"] = "PaintLine", ["terrainId"] = terrain, ["x"] = _anchor.x, ["y"] = _anchor.y, ["endX"] = _last.x, ["endY"] = _last.y }; break;
                case MapPaintTool.Flood:
                    operation = new JObject { ["type"] = "FillRegion", ["terrainId"] = terrain, ["x"] = _anchor.x, ["y"] = _anchor.y }; break;
                case MapPaintTool.Variant:
                    operation = new JObject { ["type"] = "SetVariant", ["x"] = _anchor.x, ["y"] = _anchor.y, ["variantId"] = string.IsNullOrEmpty(_variant) ? null : _variant }; break;
                case MapPaintTool.Object:
                    _objectId = "object_" + Guid.NewGuid().ToString("N");
                    _objectPosition = new Vector2((float)((_last.x + 0.5) * map.CellSize), (float)((_last.y + 0.5) * map.CellSize));
                    operation = ObjectOperation(_objectId, _objectPosition); break;
                case MapPaintTool.Region:
                    _regionId = "region_" + Guid.NewGuid().ToString("N"); _shape = MapRegionShape.Rectangle;
                    _points = new List<Vector2> { new(Math.Min(_anchor.x, _last.x), Math.Min(_anchor.y, _last.y)), new(Math.Max(_anchor.x, _last.x) + 1, Math.Max(_anchor.y, _last.y) + 1) };
                    operation = RegionOperation(); break;
                default: return;
            }
            Apply(operation);
        }

        void CancelStroke()
        {
            if (_dragging && GUIUtility.hotControl == _control) { GUIUtility.hotControl = 0; }
            _dragging = false; _stroke.Clear();
        }

        void SelectAt(Vector2 world, bool additive)
        {
            if (!additive) { _selection.Clear(); _selectedRegion = null; }
            if (_showObjects && !_lockObjects)
            {
                MapObjectRecord found = _viewDocument.Objects.OrderBy(item => Vector2.Distance(world, ObjectWorld(item))).FirstOrDefault(item => HitObject(item, world));
                if (found != null)
                {
                    _selection.Add(found.ObjectId); _objectId = found.ObjectId; _objectPosition = new Vector2((float)found.Position.X, (float)found.Position.Y);
                    _objectDefinition = _lookup.Resolve<MapObjectDefinition>(ContentId.Parse(found.DefinitionId)); _rotation = found.Rotation; _appearance = found.AppearanceVariant ?? "";
                    _interaction = found.Interaction == null ? null : found.Interaction.Kind == MapInteractionKind.Trader ? (ScriptableObject)_lookup.Resolve<TraderDefinition>(ContentId.Parse(found.Interaction.ContentId)) : _lookup.Resolve<CraftingDefinition>(ContentId.Parse(found.Interaction.ContentId));
                    Repaint(); return;
                }
            }
            if (_showRegions && !_lockRegions)
            {
                Vector2 local = new((float)((world.x - _session.Current.OriginX) / _session.Current.CellSize), (float)((world.y - _session.Current.OriginY) / _session.Current.CellSize));
                MapRegionRecord found = _viewDocument.Regions.OrderByDescending(item => item.Priority).FirstOrDefault(item => Contains(item, local));
                if (found != null)
                {
                    _selectedRegion = _regionId = found.RegionId; _regionDefinition = _lookup.Resolve<MapRegionDefinition>(ContentId.Parse(found.DefinitionId));
                    _priority = found.Priority; _shape = found.Shape; _points = found.Points.Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
                    _spawn = found.SpawnDefinitionId == null ? null : _lookup.Resolve<MonsterSpawnDefinition>(ContentId.Parse(found.SpawnDefinitionId));
                }
            }
            Repaint(); SceneView.RepaintAll();
        }

        static bool Contains(MapRegionRecord region, Vector2 p)
        {
            if (region.Shape == MapRegionShape.Rectangle) { return p.x >= region.Points[0].X && p.y >= region.Points[0].Y && p.x <= region.Points[1].X && p.y <= region.Points[1].Y; }
            bool inside = false;
            for (int i = 0, j = region.Points.Length - 1; i < region.Points.Length; j = i++)
            {
                MapPoint a = region.Points[j], b = region.Points[i];
                if ((a.Y > p.y) != (b.Y > p.y) && p.x < (b.X - a.X) * (p.y - a.Y) / (b.Y - a.Y) + a.X) { inside = !inside; }
            }
            return inside;
        }

        bool HitObject(MapObjectRecord record, Vector2 world)
        {
            MapObjectDefinition definition;
            try { definition = _lookup.Resolve<MapObjectDefinition>(ContentId.Parse(record.DefinitionId)); } catch { return false; }
            Vector2 size = record.Rotation % 180 == 0 ? definition.Footprint : new Vector2(definition.Footprint.y, definition.Footprint.x);
            Vector2 delta = world - ObjectWorld(record);
            return Math.Abs(delta.x) <= Math.Max(0.15f, size.x / 2) && Math.Abs(delta.y) <= Math.Max(0.15f, size.y / 2);
        }

        Vector2 ObjectWorld(MapObjectRecord record) { return new Vector2((float)(_session.Current.OriginX + record.Position.X), (float)(_session.Current.OriginY + record.Position.Y)); }
        Vector3 GridWorld(double x, double y) { return new Vector3((float)(_session.Current.OriginX + x * _session.Current.CellSize), (float)(_session.Current.OriginY + y * _session.Current.CellSize), -0.1f); }

        void DrawOverlays(CompiledMapDocument map)
        {
            if (_viewDocument == null) { return; }
            Handles.color = new Color(1, 1, 1, 0.15f);
            if (_showGrid)
            {
                double shift = _displayGrid ? 0.5 : 0;
                for (int x = 0; x <= map.Width; x++) { Handles.DrawLine(GridWorld(x + shift, 0), GridWorld(x + shift, map.Height)); }
                for (int y = 0; y <= map.Height; y++) { Handles.DrawLine(GridWorld(0, y + shift), GridWorld(map.Width, y + shift)); }
            }
            Handles.color = new Color(1, 0.8f, 0.2f, 0.65f);
            if (_dragging)
            {
                if (_tool == MapPaintTool.Rectangle || _tool == MapPaintTool.Region)
                { DrawRect(GridWorld(Math.Min(_anchor.x, _last.x), Math.Min(_anchor.y, _last.y)), GridWorld(Math.Max(_anchor.x, _last.x) + 1, Math.Max(_anchor.y, _last.y) + 1)); }
                else if (_tool == MapPaintTool.Line) { Handles.DrawLine(GridWorld(_anchor.x + 0.5, _anchor.y + 0.5), GridWorld(_last.x + 0.5, _last.y + 0.5)); }
                else if (_stroke.Count <= 4096) { foreach (Vector2Int p in _stroke) { DrawRect(GridWorld(p.x, p.y), GridWorld(p.x + 1, p.y + 1)); } }
                else
                {
                    DrawRect(GridWorld(_stroke.Min(p => p.x), _stroke.Min(p => p.y)), GridWorld(_stroke.Max(p => p.x) + 1, _stroke.Max(p => p.y) + 1));
                    Handles.Label(GridWorld(_last.x, _last.y), "待提交 " + _stroke.Count + " 格（仅显示范围）");
                }
            }
            if (_showObjects)
            {
                foreach (MapObjectRecord obj in _viewDocument.Objects)
                {
                    MapObjectDefinition definition;
                    try { definition = _lookup.Resolve<MapObjectDefinition>(ContentId.Parse(obj.DefinitionId)); } catch { continue; }
                    Vector2 point = ObjectWorld(obj);
                    Vector2 size = obj.Rotation % 180 == 0 ? definition.Footprint : new Vector2(definition.Footprint.y, definition.Footprint.x);
                    Handles.color = _selection.Contains(obj.ObjectId) ? Color.yellow : (definition.Capabilities & MapObjectCapabilities.Interaction) != 0 ? Color.cyan : Color.white;
                    DrawRect(point - size / 2, point + size / 2);
                    Handles.DrawSolidDisc(point, Vector3.forward, 0.06f);
                    if (_showObjectLabels || _selection.Contains(obj.ObjectId)) { Handles.Label(point, obj.ObjectId + " · " + definition.Capabilities); }
                }
                if (_tool == MapPaintTool.Select && !_lockObjects && _selection.Count == 1)
                {
                    MapObjectRecord obj = _viewDocument.Objects.FirstOrDefault(item => _selection.Contains(item.ObjectId));
                    if (obj != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 next = Handles.PositionHandle(ObjectWorld(obj), Quaternion.identity);
                        if (EditorGUI.EndChangeCheck()) { _objectPosition = new Vector2(Mathf.Round((next.x - (float)map.OriginX) / (float)map.CellSize * 2) * (float)map.CellSize / 2, Mathf.Round((next.y - (float)map.OriginY) / (float)map.CellSize * 2) * (float)map.CellSize / 2); }
                        // 拖拽只更新候选属性；释放后作为一个事务发布。
                        if (Event.current.rawType == EventType.MouseUp && ((Vector2)ObjectWorld(obj) - (_objectPosition + new Vector2((float)map.OriginX, (float)map.OriginY))).sqrMagnitude > 0.00001f) { UpsertObject(); }
                    }
                }
            }
            if (_showRegions)
            {
                foreach (MapRegionRecord region in _viewDocument.Regions)
                {
                    Handles.color = region.RegionId == _selectedRegion ? Color.yellow : Color.green;
                    if (region.Shape == MapRegionShape.Rectangle) { DrawRect(GridWorld(region.Points[0].X, region.Points[0].Y), GridWorld(region.Points[1].X, region.Points[1].Y)); }
                    else { Handles.DrawAAPolyLine(region.Points.Append(region.Points[0]).Select(p => GridWorld(p.X, p.Y)).ToArray()); }
                    Handles.Label(GridWorld(region.Points[0].X, region.Points[0].Y), region.RegionId);
                }
                if (_selectedRegion != null && !_lockRegions && _tool == MapPaintTool.Select)
                {
                    for (int i = 0; i < _points.Count; i++)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 p = Handles.PositionHandle(GridWorld(_points[i].x, _points[i].y), Quaternion.identity);
                        if (EditorGUI.EndChangeCheck()) { _points[i] = new Vector2((p.x - (float)map.OriginX) / (float)map.CellSize, (p.y - (float)map.OriginY) / (float)map.CellSize); }
                    }
                    Handles.color = Color.yellow;
                    if (_points.Count > 1) { Handles.DrawAAPolyLine(_points.Append(_points[0]).Select(p => GridWorld(p.x, p.y)).ToArray()); }
                    // 顶点可暂时自交，用户通过“应用区域”完成校验与一次 Undo。
                }
            }
            if (_geometry != null && _geometry.SourceHash == map.SourceHash)
            {
                Handles.color = Color.red;
                foreach (MapGeometryRect rect in _geometry.Rectangles.Take(8192)) { DrawRect(new Vector3((float)rect.MinX, (float)rect.MinY), new Vector3((float)rect.MaxX, (float)rect.MaxY)); }
                Handles.color = Color.cyan; Handles.DrawWireDisc(_start, Vector3.forward, (float)_radius); Handles.DrawWireDisc(_end, Vector3.forward, (float)_radius);
            }
        }

        static void DrawRect(Vector3 min, Vector3 max)
        {
            Handles.DrawAAPolyLine(min, new Vector3(max.x, min.y), max, new Vector3(min.x, max.y), min);
        }

    }
}
