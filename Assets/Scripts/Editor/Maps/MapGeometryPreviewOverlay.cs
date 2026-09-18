using System;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    static class MapGeometryPreviewOverlay
    {
        public static void Draw(Rect rect, MapGeometrySnapshot geometry, Vector2 center, float halfHeight, float radius,
            MapGeometryPoint? start, MapGeometryPoint? end)
        {
            GUI.BeginClip(rect);
            Handles.BeginGUI();
            Color previous = Handles.color;
            try
            {
                float scale = rect.height / (2 * halfHeight);
                Vector3 Project(double x, double y) { return new Vector3(rect.width / 2 + ((float)x - center.x) * scale, rect.height / 2 - ((float)y - center.y) * scale); }
                var visible = new MapGeometryRect(center.x - rect.width / scale / 2, center.y - halfHeight, center.x + rect.width / scale / 2, center.y + halfHeight);
                Handles.color = new Color(1, 0.25f, 0.2f, 0.75f);
                int drawn = 0;
                foreach (MapGeometryRect block in geometry.Rectangles)
                {
                    if (!block.Intersects(visible)) { continue; }
                    if (++drawn > 4096) { break; }
                    Handles.DrawLine(Project(block.MinX, block.MinY), Project(block.MaxX, block.MinY));
                    Handles.DrawLine(Project(block.MaxX, block.MinY), Project(block.MaxX, block.MaxY));
                    Handles.DrawLine(Project(block.MaxX, block.MaxY), Project(block.MinX, block.MaxY));
                    Handles.DrawLine(Project(block.MinX, block.MaxY), Project(block.MinX, block.MinY));
                }
                var profile = new MapGroundProfile(Math.Max(0.001, radius));
                if (geometry.NodeSize * scale >= 6)
                {
                    int minX = (int)Math.Clamp(Math.Floor((visible.MinX - geometry.Bounds.MinX) / geometry.NodeSize), 0, geometry.Width - 1);
                    int maxX = (int)Math.Clamp(Math.Ceiling((visible.MaxX - geometry.Bounds.MinX) / geometry.NodeSize), 0, geometry.Width - 1);
                    int minY = (int)Math.Clamp(Math.Floor((visible.MinY - geometry.Bounds.MinY) / geometry.NodeSize), 0, geometry.Height - 1);
                    int maxY = (int)Math.Clamp(Math.Ceiling((visible.MaxY - geometry.Bounds.MinY) / geometry.NodeSize), 0, geometry.Height - 1);
                    int count = 0;
                    for (int y = minY; y <= maxY && count < 4096; y++)
                    {
                        for (int x = minX; x <= maxX && count++ < 4096; x++)
                        {
                            MapGeometryPoint p = geometry.GetNode(x, y);
                            if (geometry.CheckPoint(p, profile) != MapGeometryResult.Valid) { continue; }
                            Vector3 screen = Project(p.X, p.Y);
                            EditorGUI.DrawRect(new Rect(screen.x - 1, screen.y - 1, 2, 2), Color.green);
                        }
                    }
                }
                if (start.HasValue)
                {
                    MapGeometryPoint a = start.Value, b = end ?? a;
                    Handles.color = geometry.CheckSegment(a, b, profile) == MapGeometryResult.Valid ? Color.green : Color.red;
                    Handles.DrawLine(Project(a.X, a.Y), Project(b.X, b.Y));
                    Handles.DrawWireDisc(Project(a.X, a.Y), Vector3.forward, (float)profile.Clearance * scale);
                    Handles.DrawWireDisc(Project(b.X, b.Y), Vector3.forward, (float)profile.Clearance * scale);
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(Project(b.X, b.Y), Vector3.forward, (float)profile.Radius * scale);
                }
            }
            finally { Handles.color = previous; Handles.EndGUI(); GUI.EndClip(); }
        }
    }
}
