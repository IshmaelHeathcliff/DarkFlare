using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare.Editor
{
    public sealed class SkillBoardPreviewWindow : OdinEditorWindow
    {
        [SerializeField, LabelText("生成配置（可选）"), OnValueChanged(nameof(Refresh))] FirewoodGenerationDefinition _definition;
        [SerializeField, LabelText("第一件尺寸"), HideIf(nameof(HasDefinition)), OnValueChanged(nameof(Refresh))] FirewoodSize _size;
        [SerializeField, LabelText("第一件种子"), OnValueChanged(nameof(Refresh))] int _seed = 12345;
        [SerializeField, LabelText("总节点（0 随机）"), OnValueChanged(nameof(Refresh))] int _nodes;
        [SerializeField, LabelText("裂缝（0 随机）"), OnValueChanged(nameof(Refresh))] int _cracks;
        [SerializeField, LabelText("内圈天赋（0 随机）"), OnValueChanged(nameof(Refresh))] int _inner;
        [SerializeField, HorizontalGroup("A坐标"), LabelText("A · q"), OnValueChanged(nameof(Refresh))] int _q = 16;
        [SerializeField, HorizontalGroup("A坐标"), LabelText("r"), OnValueChanged(nameof(Refresh))] int _r;
        [SerializeField, LabelText("A · 旋转档"), Range(0, 5), OnValueChanged(nameof(Refresh))] int _rotation;
        [SerializeField, LabelText("显示第二件"), OnValueChanged(nameof(Refresh))] bool _second;
        [SerializeField, LabelText("第二件尺寸"), ShowIf(nameof(_second)), OnValueChanged(nameof(Refresh))] FirewoodSize _secondSize = FirewoodSize.Medium;
        [SerializeField, LabelText("第二件种子"), ShowIf(nameof(_second)), OnValueChanged(nameof(Refresh))] int _secondSeed = 41;
        [SerializeField, HorizontalGroup("B坐标"), LabelText("B · q"), ShowIf(nameof(_second)), OnValueChanged(nameof(Refresh))] int _secondQ = 20;
        [SerializeField, HorizontalGroup("B坐标"), LabelText("r"), ShowIf(nameof(_second)), OnValueChanged(nameof(Refresh))] int _secondR;
        [SerializeField, LabelText("B · 旋转档"), Range(0, 5), ShowIf(nameof(_second)), OnValueChanged(nameof(Refresh))] int _secondRotation;
        [SerializeField, LabelText("自动双向配对"), OnValueChanged(nameof(Refresh))] bool _autoConnect = true;
        [SerializeField, LabelText("检查大柴薪初始空洞"), OnValueChanged(nameof(Refresh))] bool _requireRoots = true;
        [SerializeField, LabelText("显示六根与中心"), OnValueChanged(nameof(RepaintDiagram))] bool _showBoard;
        PropertyTree _tree;
        VisualElement _canvas;
        Label _summary;
        Label _inspection;
        IReadOnlyList<FirewoodPlacement> _placements = Array.Empty<FirewoodPlacement>();
        IReadOnlyList<FirewoodConnection> _connections = Array.Empty<FirewoodConnection>();
        SkillBoardPlacementResult _result;
        Vector2 _viewCenter;
        Vector2 _pan;
        float _cellRadius = 24;
        float _zoom = 1;
        bool _dragging;
        Vector2 _previousPointer;

        bool HasDefinition => _definition != null;
        public IReadOnlyList<FirewoodPlacement> Placements => _placements;
        public IReadOnlyList<FirewoodConnection> Connections => _connections;
        public SkillBoardPlacementResult Result => _result;

        [MenuItem("DarkFlare/技能面板/柴薪几何预览")]
        public static void Open()
        {
            GetWindow<SkillBoardPreviewWindow>("柴薪几何预览").Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            _tree?.Dispose();
            _tree = PropertyTree.Create(this);
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SkillBoard/SkillBoardPreview.uxml");
            if (layout == null) { rootVisualElement.Add(new Label("缺少柴薪预览布局资产")); return; }
            layout.CloneTree(rootVisualElement);
            rootVisualElement.Q<ScrollView>("parameters").Add(new IMGUIContainer(() => _tree?.Draw(false)));
            _summary = rootVisualElement.Q<Label>("summary");
            _inspection = rootVisualElement.Q<Label>("inspection");
            _canvas = rootVisualElement.Q<VisualElement>("diagram");
            _canvas.generateVisualContent += DrawDiagram;
            _canvas.RegisterCallback<GeometryChangedEvent>(_ => RepaintDiagram());
            _canvas.RegisterCallback<WheelEvent>(e =>
            {
                _zoom = Mathf.Clamp(_zoom * (e.delta.y > 0 ? .9f : 1.1f), .3f, 4);
                RepaintDiagram();
                e.StopPropagation();
            });
            _canvas.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 1 || e.button == 2)
                {
                    _dragging = true;
                    _previousPointer = e.localPosition;
                    _canvas.CapturePointer(e.pointerId);
                }
                else { Inspect(e.localPosition); }
            });
            _canvas.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging) { return; }
                _pan += (Vector2)e.localPosition - _previousPointer;
                _previousPointer = e.localPosition;
                RepaintDiagram();
            });
            _canvas.RegisterCallback<PointerUpEvent>(e => { _dragging = false; _canvas.ReleasePointer(e.pointerId); });
            _canvas.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            minSize = new Vector2(800, 560);
            EditorApplication.projectChanged += Refresh;
        }

        protected override void OnDisable()
        {
            EditorApplication.projectChanged -= Refresh;
            _tree?.Dispose();
            _tree = null;
            _canvas = null;
            _placements = Array.Empty<FirewoodPlacement>();
            _connections = Array.Empty<FirewoodConnection>();
            base.OnDisable();
        }

        [Button("重新生成 / 校验")]
        public void Refresh()
        {
            if (_summary == null || _canvas == null) { return; }
            var timer = Stopwatch.StartNew();
            List<SkillBoardIssue> issues = new List<SkillBoardIssue>();
            List<FirewoodPlacement> placements = new List<FirewoodPlacement>();
            FirewoodGenerationResult first = _definition != null
                ? _definition.Generate(_seed, _nodes, _cracks, _inner)
                : FirewoodGenerator.Generate(_size, _seed, _nodes, _cracks, _inner);
            issues.AddRange(first.Issues);
            try
            {
                if (first.Success) { placements.Add(new FirewoodPlacement("A", first.Layout, new HexCoordinate(_q, _r), _rotation)); }
                if (_second)
                {
                    FirewoodGenerationResult second = FirewoodGenerator.Generate(_secondSize, _secondSeed, crackCount: 1);
                    issues.AddRange(second.Issues);
                    if (second.Success) { placements.Add(new FirewoodPlacement("B", second.Layout, new HexCoordinate(_secondQ, _secondR), _secondRotation)); }
                }
                _placements = placements.AsReadOnly();
                _connections = _autoConnect ? SkillBoardPlacementValidator.FindConnections(_placements) : Array.Empty<FirewoodConnection>();
                _result = SkillBoardPlacementValidator.Validate(_placements, _connections, _requireRoots);
                issues.AddRange(_result.Issues);
                if (_result.Issues.Any(issue => issue.Code == "coordinate_overflow"))
                {
                    _placements = Array.Empty<FirewoodPlacement>();
                    _connections = Array.Empty<FirewoodConnection>();
                }
            }
            catch (OverflowException)
            {
                issues.Add(new SkillBoardIssue("coordinate_overflow", "坐标超出可表示范围"));
                _placements = Array.Empty<FirewoodPlacement>();
                _connections = Array.Empty<FirewoodConnection>();
                _result = null;
            }
            if (issues.Count > 0) { _result = new SkillBoardPlacementResult(issues, _result?.OccupiedCells ?? Array.Empty<HexCoordinate>()); }
            timer.Stop();
            _summary.text = $"{(issues.Count == 0 ? "通过" : "失败")} · 占位 {_result?.OccupiedCells.Count ?? 0} 格 · 连接 {_connections.Count} 条 · {timer.Elapsed.TotalMilliseconds:F2} ms\n"
                + $"A：候选 {first.CandidateCount} / 最优 {first.BestCandidateCount} / 旋转类 {first.RotationClassCount}；{first.Symmetry}";
            _inspection.text = issues.Count == 0 ? "点击格子查看节点与端口身份；滚轮缩放，右键 / 中键平移。"
                : string.Join("\n", issues.Take(8)) + (issues.Count > 8 ? $"\n…共 {issues.Count} 项" : string.Empty);
            RepaintDiagram();
        }

        [Button("双向连接样例"), HorizontalGroup("样例")]
        public void ShowConnectionExample()
        {
            _definition = null;
            _size = FirewoodSize.Large;
            _seed = 41;
            _nodes = 0;
            _cracks = 1;
            _inner = 0;
            _q = 16;
            _r = 0;
            _second = true;
            _secondSize = FirewoodSize.Medium;
            _secondSeed = 41;
            _secondQ = 20;
            _secondR = 0;
            _rotation = FacingRotation(_size, _seed, 0);
            _secondRotation = FacingRotation(_secondSize, _secondSeed, 3);
            _autoConnect = true;
            _showBoard = false;
            _pan = default;
            _zoom = 1;
            Refresh();
        }

        [Button("共享第三格失败"), HorizontalGroup("样例")]
        public void ShowMiddleCellFailure()
        {
            ShowConnectionExample();
            _secondQ = 21;
            Refresh();
        }

        [Button("旋转 A 60°"), HorizontalGroup("操作")]
        void RotateFirst()
        {
            _rotation = (_rotation + 1) % 6;
            Refresh();
        }

        [Button("复位视口"), HorizontalGroup("操作")]
        void ResetView()
        {
            _pan = default;
            _zoom = 1;
            RepaintDiagram();
        }

        void RepaintDiagram()
        {
            if (_canvas == null) { return; }
            _canvas.Clear();
            List<HexCoordinate> cells = _placements.SelectMany(p => p.Layout.Body.Select(p.ToWorld)
                .Concat(p.Layout.Nodes.Where(n => n.Outward.HasValue).Select(n => p.ToWorld(n.Position.Add(n.Outward.Value))))).ToList();
            if (_showBoard) { cells.AddRange(SkillBoardGeometry.Roots); cells.AddRange(SkillBoardGeometry.CenterReservation); }
            if (cells.Count == 0) { _canvas.MarkDirtyRepaint(); return; }
            List<Vector2> centers = cells.Select(UnitPosition).ToList();
            Vector2 minimum = new Vector2(centers.Min(p => p.x) - 2, centers.Min(p => p.y) - 2);
            Vector2 maximum = new Vector2(centers.Max(p => p.x) + 2, centers.Max(p => p.y) + 2);
            _viewCenter = (minimum + maximum) * .5f;
            _cellRadius = Mathf.Min(_canvas.contentRect.width / (maximum.x - minimum.x), _canvas.contentRect.height / (maximum.y - minimum.y)) * _zoom;
            foreach (FirewoodPlacement placement in _placements)
            {
                foreach (FirewoodNode node in placement.Layout.Nodes)
                {
                    AddLabel(node.Kind == FirewoodNodeKind.Crack ? "裂" : "天", placement.ToWorld(node.Position));
                }
                if (placement.Layout.Size != FirewoodSize.Small)
                {
                    AddLabel(placement.Layout.Size == FirewoodSize.Large ? "火焰" : "空", placement.Anchor);
                }
            }
            if (_showBoard)
            {
                AddLabel("中心", default);
                for (int i = 0; i < 6; i++) { AddLabel($"空洞 {i + 1}", SkillBoardGeometry.Roots[i]); }
            }
            _canvas.MarkDirtyRepaint();
        }

        void AddLabel(string text, HexCoordinate position)
        {
            Vector2 point = ScreenPosition(position);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("cell-label");
            label.style.left = point.x - 24;
            label.style.top = point.y - 9;
            _canvas.Add(label);
        }

        void DrawDiagram(MeshGenerationContext context)
        {
            if (_cellRadius <= 0) { return; }
            Painter2D painter = context.painter2D;
            var connected = new HashSet<FirewoodPortReference>(_connections.SelectMany(c => new[] { c.First, c.Second }));
            if (_showBoard)
            {
                foreach (HexCoordinate cell in SkillBoardGeometry.CenterReservation) { DrawHex(painter, cell, new Color(.35f, .18f, .18f)); }
                foreach (HexCoordinate root in SkillBoardGeometry.Roots) { DrawHex(painter, root, new Color(.12f, .25f, .4f)); }
            }
            foreach (FirewoodPlacement placement in _placements)
            {
                Color fill = placement.Id == "A" ? new Color(.26f, .23f, .15f) : new Color(.12f, .28f, .28f);
                foreach (HexCoordinate cell in placement.Layout.Body) { DrawHex(painter, placement.ToWorld(cell), fill); }
                var nodes = placement.Layout.Nodes.ToDictionary(n => n.Id);
                foreach (FirewoodEdge edge in placement.Layout.Edges)
                {
                    HexCoordinate first = edge.First == SkillBoardGeometry.CenterNodeId ? placement.Anchor : placement.ToWorld(nodes[edge.First].Position);
                    HexCoordinate second = edge.Second == SkillBoardGeometry.CenterNodeId ? placement.Anchor : placement.ToWorld(nodes[edge.Second].Position);
                    DrawLine(painter, first, second, new Color(.65f, .65f, .5f), 2);
                }
                foreach (FirewoodNode node in placement.Layout.Nodes)
                {
                    if (node.Kind != FirewoodNodeKind.Crack) { continue; }
                    HexCoordinate position = placement.ToWorld(node.Position);
                    var reference = new FirewoodPortReference(placement.Id, node.Id);
                    if (connected.Contains(reference)) { DrawHex(painter, position, new Color(.12f, .45f, .28f)); }
                    else
                    {
                        HexCoordinate target = placement.ToWorld(node.Position.Add(node.Outward.Value));
                        DrawHex(painter, target, new Color(.3f, .19f, .38f));
                        DrawLine(painter, position, target, new Color(.8f, .6f, .95f), 3);
                    }
                }
            }
            if (_result != null)
            {
                foreach (SkillBoardIssue issue in _result.Issues.Where(i => i.Position.HasValue))
                {
                    DrawHex(painter, issue.Position.Value, new Color(.65f, .12f, .15f, .65f));
                }
            }
        }

        void DrawHex(Painter2D painter, HexCoordinate cell, Color fill)
        {
            Vector2 center = ScreenPosition(cell);
            painter.BeginPath();
            for (int i = 0; i < 6; i++)
            {
                float radians = (30 + i * 60) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (_cellRadius - 1);
                if (i == 0) { painter.MoveTo(point); } else { painter.LineTo(point); }
            }
            painter.ClosePath();
            painter.fillColor = fill;
            painter.Fill();
            painter.strokeColor = new Color(.55f, .55f, .55f);
            painter.lineWidth = 1;
            painter.Stroke();
        }

        void DrawLine(Painter2D painter, HexCoordinate first, HexCoordinate second, Color color, float width)
        {
            painter.BeginPath();
            painter.MoveTo(ScreenPosition(first));
            painter.LineTo(ScreenPosition(second));
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.Stroke();
        }

        void Inspect(Vector2 pointer)
        {
            if (_placements.Count == 0) { return; }
            List<(FirewoodPlacement Placement, FirewoodNode Node)> nodes = _placements.SelectMany(p => p.Layout.Nodes.Select(n => (p, n))).ToList();
            var nearest = nodes.OrderBy(n => (ScreenPosition(n.Placement.ToWorld(n.Node.Position)) - pointer).sqrMagnitude).First();
            _inspection.text = $"{nearest.Placement.Id}/{nearest.Node.Id} · {nearest.Node.Kind}\n世界 {nearest.Placement.ToWorld(nearest.Node.Position)} · 局部 {nearest.Node.Position}"
                + (nearest.Node.Outward.HasValue ? $" · 外伸目标 {nearest.Placement.ToWorld(nearest.Node.Position.Add(nearest.Node.Outward.Value))}" : string.Empty);
        }

        Vector2 ScreenPosition(HexCoordinate coordinate)
        {
            return (UnitPosition(coordinate) - _viewCenter) * _cellRadius + _canvas.contentRect.size * .5f + _pan;
        }

        static Vector2 UnitPosition(HexCoordinate coordinate)
        {
            return new Vector2(Mathf.Sqrt(3) * (coordinate.Q + coordinate.R * .5f), coordinate.R * 1.5f);
        }

        static int FacingRotation(FirewoodSize size, int seed, int direction)
        {
            FirewoodLayout layout = FirewoodGenerator.Generate(size, seed, crackCount: 1).Layout;
            FirewoodNode crack = layout.Nodes.Single(n => n.Kind == FirewoodNodeKind.Crack);
            return Enumerable.Range(0, 6).Single(r => crack.Outward.Value.Rotate(r).Equals(SkillBoardGeometry.NeighborDirections[direction]));
        }
    }
}
