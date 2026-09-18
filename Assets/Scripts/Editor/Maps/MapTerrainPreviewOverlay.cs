using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    static class MapTerrainPreviewOverlay
    {
        public static void Draw(Rect rect, MapTerrainPreviewFrame frame, Vector2 center, float halfHeight,
            bool logical, bool display, bool chunks, ref GUIStyle labelStyle)
        {
            labelStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
            labelStyle.normal.textColor = Color.white;
            GUI.BeginClip(rect);
            try
            {
                var local = new Rect(0, 0, rect.width, rect.height);
                if (frame.IsGallery)
                {
                    foreach (var cell in frame.Gallery)
                    {
                        Vector2 label = Project(cell.Key + new Vector2(0.5f, -0.5f), local, center, halfHeight);
                        TerrainWangKey key = cell.Value.Key;
                        string text = cell.Value.Variant == null ? $"{Letter(key.NorthWest)} {Letter(key.NorthEast)}\n{Letter(key.SouthWest)} {Letter(key.SouthEast)}"
                            : cell.Value.Variant == "diagnostic_a" ? "A" : "B";
                        float spacing = rect.height / (2 * halfHeight);
                        if (spacing >= 16) { GUI.Label(new Rect(label.x - spacing, label.y - 10, spacing * 2, 20), text, labelStyle); }
                    }
                    return;
                }
                CompiledMapDocument map = frame.Resolution.Map;
                if (logical) { DrawGrid(local, map, center, halfHeight, 0, 1, new Color(1, 1, 1, 0.22f)); }
                if (display) { DrawGrid(local, map, center, halfHeight, 0.5f, 1, new Color(0.5f, 1, 1, 0.35f)); }
                if (chunks) { DrawGrid(local, map, center, halfHeight, 0.5f, 32, new Color(1, 0.7f, 0.25f, 0.8f)); }
            }
            finally { GUI.EndClip(); }
        }

        static string Letter(ContentId id)
        {
            return id.LocalId.Substring("preview_".Length, 1).ToUpperInvariant();
        }

        static void DrawGrid(Rect rect, CompiledMapDocument map, Vector2 center, float halfHeight, float offset, int step, Color color)
        {
            float size = (float)map.CellSize;
            if (step == 1 && size * rect.height / (2 * halfHeight) < 5) { return; }
            Handles.BeginGUI();
            Color before = Handles.color;
            Handles.color = color;
            try
            {
                int start = offset == 0.5f && step == 1 ? -1 : 0;
                for (int x = start; x <= map.Width; x += step)
                {
                    float worldX = (float)map.OriginX + (x + offset) * size;
                    Handles.DrawLine(Project(new Vector2(worldX, (float)map.OriginY - size * 0.5f), rect, center, halfHeight),
                        Project(new Vector2(worldX, (float)map.OriginY + (map.Height + 0.5f) * size), rect, center, halfHeight));
                }
                for (int y = start; y <= map.Height; y += step)
                {
                    float worldY = (float)map.OriginY + (y + offset) * size;
                    Handles.DrawLine(Project(new Vector2((float)map.OriginX - size * 0.5f, worldY), rect, center, halfHeight),
                        Project(new Vector2((float)map.OriginX + (map.Width + 0.5f) * size, worldY), rect, center, halfHeight));
                }
            }
            finally { Handles.color = before; Handles.EndGUI(); }
        }

        static Vector2 Project(Vector2 world, Rect rect, Vector2 center, float halfHeight)
        {
            float scale = rect.height / (2 * halfHeight);
            return rect.center + new Vector2(world.x - center.x, center.y - world.y) * scale;
        }
    }
}
