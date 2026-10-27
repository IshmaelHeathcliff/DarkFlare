using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public static class SkillBoardProjection
    {
        static readonly string[] Attributes = { StatIds.Strength, StatIds.Dexterity, StatIds.Intelligence };

        public static HexCoordinate AttributeCell(int index)
        {
            if (index < 0 || index >= Attributes.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
            return SkillBoardGeometry.NeighborDirections[4 - index * 2];
        }

        public static string AttributeAt(HexCoordinate cell)
        {
            for (int i = 0; i < Attributes.Length; i++)
            {
                if (AttributeCell(i).Equals(cell)) { return Attributes[i]; }
            }
            return string.Empty;
        }

        public static Vector2 Project(HexCoordinate cell)
        {
            return new Vector2(cell.Q * 1.5f, Mathf.Sqrt(3) * (cell.R + cell.Q * 0.5f));
        }

        public static Vector2 Corner(int index)
        {
            float angle = (60 + index * 60) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public static HexCoordinate Unproject(Vector2 point)
        {
            double q = point.x / 1.5;
            double r = point.y / Math.Sqrt(3) - q * 0.5;
            double s = -q - r;
            double rq = Math.Round(q), rr = Math.Round(r), rs = Math.Round(s);
            double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
            if (dq > dr && dq > ds) { rq = -rr - rs; }
            else if (dr > ds) { rr = -rq - rs; }
            return new HexCoordinate(checked((int)rq), checked((int)rr));
        }

        public static HexCoordinate Step(HexCoordinate from, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) { return from; }
            Vector2 screen = new Vector2(direction.x, -direction.y).normalized;
            HexCoordinate best = default;
            float score = float.MinValue;
            foreach (HexCoordinate delta in SkillBoardGeometry.NeighborDirections)
            {
                float candidate = Vector2.Dot(Project(delta).normalized, screen);
                if (candidate > score) { best = delta; score = candidate; }
            }
            return from.Add(best);
        }
    }

    public sealed class SkillBoardViewport : VisualElement, IDisposable
    {
        readonly BoardPointer _pointer;
        readonly List<Label> _labels = new List<Label>();
        SkillBoardState _board = new SkillBoardState();
        PlayerGrowthState _growth = new PlayerGrowthState();
        SkillBoardState _candidate;
        ItemInstance _moving;
        bool _valid;
        Vector2 _pan;
        Vector2 _center;
        float _radius = 18;
        bool _fitPending;
        string _centerText = string.Empty;
        string[] _attributes = Array.Empty<string>();
        readonly HashSet<string> _targets = new HashSet<string>();

        public HexCoordinate Cursor { get; private set; }
        public event Action<HexCoordinate> CursorChanged;
        public event Action Activated;
        public event Action<Vector2, int> TakeRequested;

        public SkillBoardViewport()
        {
            name = "board-canvas";
            focusable = true;
            tabIndex = 0;
            AddToClassList("board-canvas");
            generateVisualContent += Draw;
            _pointer = new BoardPointer(this);
            this.AddManipulator(_pointer);
            RegisterCallback<NavigationSubmitEvent>(OnSubmit);
            RegisterCallback<GeometryChangedEvent>(OnGeometry);
        }

        public void SetState(SkillBoardState board, PlayerGrowthState growth, SkillBoardState candidate,
            ItemInstance moving, bool valid, string centerText, string[] attributes, IEnumerable<string> targets)
        {
            _board = board; _growth = growth; _candidate = candidate; _moving = moving; _valid = valid;
            _centerText = centerText; _attributes = attributes;
            _targets.Clear();
            if (targets != null) { foreach (string id in targets) { _targets.Add(id); } }
            UpdateLabels();
            MarkDirtyRepaint();
        }

        public Vector2 CellPosition(HexCoordinate cell)
        {
            return (SkillBoardProjection.Project(cell) - _center) * _radius + contentRect.size * 0.5f + _pan;
        }

        public bool SelectAt(Vector2 local)
        {
            try { Select(SkillBoardProjection.Unproject((local - contentRect.size * 0.5f - _pan) / _radius + _center)); return true; }
            catch (OverflowException) { return false; }
        }

        public void Select(HexCoordinate cell)
        {
            Cursor = cell;
            MarkDirtyRepaint();
            CursorChanged?.Invoke(cell);
        }

        public void Navigate(Vector2 direction)
        {
            try
            {
                Select(SkillBoardProjection.Step(Cursor, direction));
                Vector2 point = CellPosition(Cursor);
                if (point.x < 36 || point.x > contentRect.width - 36 || point.y < 36 || point.y > contentRect.height - 36) { CenterOn(Cursor); }
            }
            catch (OverflowException) { }
        }

        public void CenterOn(HexCoordinate cell)
        {
            _center = SkillBoardProjection.Project(cell); _pan = Vector2.zero;
            UpdateLabels(); MarkDirtyRepaint();
        }

        public void Zoom(float multiplier)
        {
            _radius = Mathf.Clamp(_radius * multiplier, 7, 52);
            UpdateLabels(); MarkDirtyRepaint();
        }

        public void Fit()
        {
            if (contentRect.width < 1 || contentRect.height < 1) { _fitPending = true; return; }
            _fitPending = false;
            Vector2 minimum = Vector2.positiveInfinity, maximum = Vector2.negativeInfinity;
            Include(_board);
            if (_candidate != null) { Include(_candidate); }
            _center = (minimum + maximum) * 0.5f; _pan = Vector2.zero;
            _radius = Mathf.Clamp(Mathf.Min((contentRect.width - 80) / (maximum.x - minimum.x),
                (contentRect.height - 48) / (maximum.y - minimum.y)), 7, 52);
            UpdateLabels(); MarkDirtyRepaint();

            void Include(SkillBoardState board)
            {
                foreach (HexCoordinate cell in SkillBoardGeometry.CenterReservation) { IncludeCell(cell); }
                foreach (HexCoordinate cell in SkillBoardGeometry.Roots) { IncludeCell(cell); }
                foreach (InstalledFirewood wood in board.Firewoods)
                {
                    try
                    {
                        foreach (HexCoordinate cell in wood.Placement.Layout.Body) { IncludeCell(wood.Placement.ToWorld(cell)); }
                        foreach (FirewoodNode node in wood.Placement.Layout.Nodes)
                        {
                            if (node.Outward.HasValue && !HasConnection(board, new FirewoodPortReference(wood.Item.InstanceId, node.Id)))
                            { IncludeCell(wood.Placement.ToWorld(node.Position.Add(node.Outward.Value))); }
                        }
                    }
                    catch (OverflowException) { }
                }
            }
            void IncludeCell(HexCoordinate cell)
            {
                Vector2 center = SkillBoardProjection.Project(cell);
                for (int i = 0; i < 6; i++)
                {
                    Vector2 corner = center + SkillBoardProjection.Corner(i);
                    minimum = Vector2.Min(minimum, corner); maximum = Vector2.Max(maximum, corner);
                }
            }
        }

        void OnGeometry(GeometryChangedEvent evt)
        {
            if (_fitPending) { Fit(); }
            else { UpdateLabels(); }
        }
        void OnSubmit(NavigationSubmitEvent evt) { Activated?.Invoke(); evt.StopImmediatePropagation(); }

        void UpdateLabels()
        {
            foreach (Label label in _labels) { label.RemoveFromHierarchy(); }
            _labels.Clear();
            AddLabel(_centerText, default);
            for (int i = 0; i < _attributes.Length; i++) { AddLabel(_attributes[i], SkillBoardProjection.AttributeCell(i), true); }
            for (int i = 0; i < 6; i++) { AddLabel((i + 1).ToString(), SkillBoardGeometry.Roots[i]); }
        }

        Vector2 LabelPosition(HexCoordinate cell, bool attribute)
        {
            Vector2 position = CellPosition(cell);
            if (attribute)
            {
                Vector2 direction = SkillBoardProjection.Project(cell);
                position += direction.normalized * Mathf.Max(0, 48 - direction.magnitude * _radius);
            }
            return position;
        }

        void AddLabel(string text, HexCoordinate cell, bool attribute = false)
        {
            Vector2 position = LabelPosition(cell, attribute);
            if (position.x < -50 || position.x > contentRect.width + 50 || position.y < -20 || position.y > contentRect.height + 20) { return; }
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("board-cell-label");
            label.style.left = position.x; label.style.top = position.y;
            Add(label); _labels.Add(label);
        }

        void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (contentRect.width < 1) { return; }
            HexCoordinate origin;
            try { origin = SkillBoardProjection.Unproject(_center - _pan / _radius); }
            catch (OverflowException) { return; }
            int qReach = Mathf.CeilToInt(contentRect.width / (_radius * 3)) + 2;
            int rReach = Mathf.CeilToInt(contentRect.height / (_radius * Mathf.Sqrt(3) * 2) + contentRect.width / (_radius * 6)) + 2;
            for (int q = -qReach; q <= qReach; q++)
            {
                for (int r = -rReach; r <= rReach; r++)
                {
                    try { Hex(painter, origin.Add(new HexCoordinate(q, r)), new Color(0.06f, 0.07f, 0.09f), new Color(0.14f, 0.15f, 0.17f)); }
                    catch (OverflowException) { }
                }
            }
            foreach (HexCoordinate cell in SkillBoardGeometry.CenterReservation)
            {
                string attribute = SkillBoardProjection.AttributeAt(cell);
                Color fill = attribute == StatIds.Strength ? new Color(0.36f, 0.1f, 0.08f)
                    : attribute == StatIds.Dexterity ? new Color(0.1f, 0.29f, 0.15f)
                    : attribute == StatIds.Intelligence ? new Color(0.1f, 0.18f, 0.38f) : new Color(0.21f, 0.15f, 0.11f);
                Hex(painter, cell, fill, new Color(0.65f, 0.5f, 0.27f));
                if (attribute.Length > 0 && _radius * Mathf.Sqrt(3) < 48)
                {
                    painter.BeginPath(); painter.MoveTo(CellPosition(cell)); painter.LineTo(LabelPosition(cell, true));
                    painter.strokeColor = new Color(0.65f, 0.5f, 0.27f); painter.lineWidth = 1; painter.Stroke();
                }
            }
            foreach (HexCoordinate cell in SkillBoardGeometry.Roots) { Hex(painter, cell, new Color(0.09f, 0.17f, 0.25f), new Color(0.38f, 0.57f, 0.7f)); }
            PaintBoard(painter, _board, false);
            if (_candidate != null && _moving != null) { PaintBoard(painter, _candidate, true); }
            Hex(painter, Cursor, new Color(0.6f, 0.48f, 0.24f, 0.25f), new Color(0.95f, 0.78f, 0.42f));
        }

        void PaintBoard(Painter2D painter, SkillBoardState board, bool preview)
        {
            foreach (InstalledFirewood wood in board.Firewoods)
            {
                if (preview && wood.Item != _moving || !preview && wood.Item == _moving && _candidate != null) { continue; }
                Color fill = preview ? _valid ? new Color(0.12f, 0.38f, 0.25f, 0.8f) : new Color(0.45f, 0.12f, 0.12f, 0.8f)
                    : _growth.IsActive(wood.Item.InstanceId) ? new Color(0.22f, 0.19f, 0.13f) : new Color(0.13f, 0.14f, 0.16f);
                foreach (HexCoordinate cell in wood.Placement.Layout.Body) { Hex(painter, wood.Placement.ToWorld(cell), fill, new Color(0.4f, 0.35f, 0.25f)); }
                foreach (FirewoodEdge edge in wood.Placement.Layout.Edges)
                {
                    HexCoordinate first = Position(wood, edge.First), second = Position(wood, edge.Second);
                    Line(painter, first, second, new Color(0.59f, 0.51f, 0.32f), 2);
                }
                foreach (FirewoodNode node in wood.Placement.Layout.Nodes)
                {
                    HexCoordinate cell = wood.Placement.ToWorld(node.Position);
                    bool lit = _growth.IsActive(wood.Item.InstanceId, node.Id);
                    Color color = node.Kind == FirewoodNodeKind.Crack ? new Color(0.52f, 0.3f, 0.65f)
                        : lit ? new Color(0.95f, 0.72f, 0.27f) : new Color(0.48f, 0.47f, 0.43f);
                    Dot(painter, cell, color, 0.24f);
                    if (node.Outward.HasValue && !HasConnection(board, new FirewoodPortReference(wood.Item.InstanceId, node.Id)))
                    {
                        HexCoordinate end = wood.Placement.ToWorld(node.Position.Add(node.Outward.Value));
                        Hex(painter, end, new Color(0.23f, 0.14f, 0.28f, 0.45f), color);
                        Line(painter, cell, end, color, 2);
                    }
                }
            }
            foreach (FirewoodConnection connection in board.Connections)
            {
                InstalledFirewood first = Find(board, connection.First.PlacementId), second = Find(board, connection.Second.PlacementId);
                if (first != null && second != null) { Line(painter, Position(first, connection.First.NodeId), Position(second, connection.Second.NodeId), new Color(0.3f, 0.8f, 0.62f), 4); }
            }
            if (preview) { return; }
            foreach (SkillBoardEmbedding embed in board.Embeddings)
            {
                InstalledFirewood wood = Find(board, embed.Slot.PlacementId);
                if (wood == null) { continue; }
                Color color = embed.Item.BaseDefinition.ItemType == ItemType.Flame ? new Color(1, 0.36f, 0.08f) : new Color(0.9f, 0.48f, 0.8f);
                Dot(painter, Position(wood, embed.Slot.NodeId), color, _targets.Contains(embed.Item.InstanceId) ? 0.65f : 0.42f);
            }
        }

        static InstalledFirewood Find(SkillBoardState board, string id)
        {
            foreach (InstalledFirewood wood in board.Firewoods) { if (wood.Item.InstanceId == id) { return wood; } }
            return null;
        }
        static bool HasConnection(SkillBoardState board, FirewoodPortReference port)
        {
            foreach (FirewoodConnection link in board.Connections) { if (link.First.Equals(port) || link.Second.Equals(port)) { return true; } }
            return false;
        }
        static HexCoordinate Position(InstalledFirewood wood, string id)
        {
            if (id == SkillBoardGeometry.CenterNodeId) { return wood.Placement.Anchor; }
            foreach (FirewoodNode node in wood.Placement.Layout.Nodes) { if (node.Id == id) { return wood.Placement.ToWorld(node.Position); } }
            return wood.Placement.Anchor;
        }
        void Hex(Painter2D painter, HexCoordinate cell, Color fill, Color border)
        {
            Vector2 point = CellPosition(cell);
            if (point.x < -_radius || point.y < -_radius || point.x > contentRect.width + _radius || point.y > contentRect.height + _radius) { return; }
            painter.BeginPath();
            for (int i = 0; i < 6; i++)
            {
                Vector2 corner = point + SkillBoardProjection.Corner(i) * (_radius - 1);
                if (i == 0) { painter.MoveTo(corner); } else { painter.LineTo(corner); }
            }
            painter.ClosePath(); painter.fillColor = fill; painter.Fill();
            painter.strokeColor = border; painter.lineWidth = 1; painter.Stroke();
        }
        void Dot(Painter2D painter, HexCoordinate cell, Color color, float size)
        {
            Vector2 point = CellPosition(cell);
            painter.BeginPath(); painter.Arc(point, _radius * size, Angle.Degrees(0), Angle.Degrees(360));
            painter.fillColor = color; painter.Fill();
        }
        void Line(Painter2D painter, HexCoordinate first, HexCoordinate second, Color color, float width)
        {
            painter.BeginPath(); painter.MoveTo(CellPosition(first)); painter.LineTo(CellPosition(second));
            painter.strokeColor = color; painter.lineWidth = width; painter.Stroke();
        }
        public void Dispose()
        {
            this.RemoveManipulator(_pointer); generateVisualContent -= Draw;
            UnregisterCallback<NavigationSubmitEvent>(OnSubmit); UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            CursorChanged = null; Activated = null; TakeRequested = null; Clear();
        }

        sealed class BoardPointer : PointerManipulator
        {
            readonly SkillBoardViewport _view;
            Vector2 _previous;
            Vector2 _pressed;
            bool _panning;
            public BoardPointer(SkillBoardViewport view) { _view = view; }
            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(Down); target.RegisterCallback<PointerMoveEvent>(Move);
                target.RegisterCallback<PointerUpEvent>(Up); target.RegisterCallback<WheelEvent>(Wheel);
                target.RegisterCallback<PointerCaptureOutEvent>(Lost);
            }
            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(Down); target.UnregisterCallback<PointerMoveEvent>(Move);
                target.UnregisterCallback<PointerUpEvent>(Up); target.UnregisterCallback<WheelEvent>(Wheel);
                target.UnregisterCallback<PointerCaptureOutEvent>(Lost);
            }
            void Down(PointerDownEvent evt)
            {
                target.Focus(); _view.SelectAt(evt.localPosition); _pressed = evt.position;
                if (evt.button == 2) { _panning = true; _previous = evt.localPosition; target.CapturePointer(evt.pointerId); evt.StopPropagation(); }
            }
            void Move(PointerMoveEvent evt)
            {
                if (_panning)
                {
                    _view._pan += (Vector2)evt.localPosition - _previous; _previous = evt.localPosition;
                    _view.UpdateLabels(); _view.MarkDirtyRepaint(); evt.StopPropagation();
                }
                else if ((evt.pressedButtons & 1) != 0 && Vector2.Distance(evt.position, _pressed) > 10) { _view.TakeRequested?.Invoke(evt.position, evt.pointerId); }
            }
            void Up(PointerUpEvent evt)
            {
                if (_panning) { target.ReleasePointer(evt.pointerId); _panning = false; }
                else if (evt.button == 0) { _view.Activated?.Invoke(); }
            }
            void Lost(PointerCaptureOutEvent evt) { _panning = false; }
            void Wheel(WheelEvent evt) { _view.Zoom(evt.delta.y < 0 ? 1.15f : 1 / 1.15f); evt.StopPropagation(); }
        }
    }
}
