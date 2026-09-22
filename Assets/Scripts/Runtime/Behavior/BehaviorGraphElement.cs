using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public sealed class BehaviorGraphElement : VisualElement
    {
        IReadOnlyList<BehaviorDebugNode> _nodes;
        Rect[] _rects;
        float _width;
        float _height;
        int _topology;
        public BehaviorGraphElement() { AddToClassList("behavior-graph"); generateVisualContent += Draw; }
        public void Show(IReadOnlyList<BehaviorDebugNode> nodes) { _nodes = nodes; MarkDirtyRepaint(); }
        void LayoutNodes()
        {
            int topology = 17;
            int maximumDepth = 0;
            foreach (BehaviorDebugNode node in _nodes)
            {
                topology = unchecked(topology * 31 + node.Parent + node.Id.GetHashCode());
                maximumDepth = Mathf.Max(maximumDepth, node.Depth);
            }
            if (_rects != null && _rects.Length == _nodes.Count && _topology == topology
                && _width == contentRect.width && _height == contentRect.height) { return; }
            _topology = topology; _width = contentRect.width; _height = contentRect.height;
            _rects = new Rect[_nodes.Count];
            var counts = new int[maximumDepth + 1];
            var positions = new int[maximumDepth + 1];
            foreach (BehaviorDebugNode node in _nodes) { counts[node.Depth]++; }
            float stepX = _width / (maximumDepth + 1);
            for (int i = 0; i < _nodes.Count; i++)
            {
                int depth = _nodes[i].Depth;
                float stepY = _height / counts[depth];
                _rects[i] = new Rect(depth * stepX + 10, (positions[depth]++ + .5f) * stepY - 25, stepX - 22, 50);
            }
        }
        void Draw(MeshGenerationContext context)
        {
            if (_nodes == null || _nodes.Count == 0 || contentRect.width < 20) { return; }
            LayoutNodes();
            Painter2D painter = context.painter2D;
            painter.lineWidth = 2;
            for (int i = 0; i < _nodes.Count; i++)
            {
                int parent = _nodes[i].Parent;
                if (parent < 0) { continue; }
                Rect from = _rects[parent]; Rect to = _rects[i];
                painter.strokeColor = _nodes[i].State == "Running" ? new Color(.4f, .85f, .67f) : new Color(.35f, .40f, .48f);
                painter.BeginPath(); painter.MoveTo(new Vector2(from.xMax, from.center.y));
                painter.BezierCurveTo(new Vector2(from.xMax + 10, from.center.y), new Vector2(to.x - 10, to.center.y), new Vector2(to.x, to.center.y));
                painter.Stroke();
            }
            for (int i = 0; i < _nodes.Count; i++)
            {
                Rect rect = _rects[i]; string state = _nodes[i].State;
                Color color = state == "Running" ? new Color(.25f, .72f, .55f) : state == "Faulted" || state == "Failure" ?
                    new Color(.86f, .35f, .39f) : state == "Aborted" ? new Color(.88f, .65f, .3f) : new Color(.45f, .53f, .65f);
                painter.fillColor = new Color(.09f, .13f, .18f); painter.strokeColor = color;
                painter.BeginPath(); painter.MoveTo(rect.min); painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(rect.max); painter.LineTo(new Vector2(rect.xMin, rect.yMax)); painter.ClosePath(); painter.Fill(); painter.Stroke();
                context.DrawText(_nodes[i].Id, new Vector2(rect.x + 8, rect.y + 4), 13, Color.white);
                context.DrawText(state, new Vector2(rect.x + 8, rect.y + 26), 11, color);
            }
        }
    }
}
