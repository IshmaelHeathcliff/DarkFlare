using System;

namespace DarkFlare
{
    public readonly struct MapGeometryPoint
    {
        public double X { get; }
        public double Y { get; }
        public MapGeometryPoint(double x, double y) { X = x; Y = y; }
        public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
    }

    public readonly struct MapGeometryRect
    {
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public MapGeometryRect(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }
        public bool Contains(MapGeometryPoint p, double inset = 0)
        {
            return p.X >= MinX + inset && p.X <= MaxX - inset && p.Y >= MinY + inset && p.Y <= MaxY - inset;
        }
        public bool Intersects(MapGeometryRect b)
        {
            return MinX <= b.MaxX && MaxX >= b.MinX && MinY <= b.MaxY && MaxY >= b.MinY;
        }
    }

    // Double 世界几何；边界接触视作阻挡，避免零宽连接。
    public static class MapGeometryMath
    {
        public static double DistanceSquared(MapGeometryPoint a, MapGeometryPoint b)
        {
            double x = a.X - b.X, y = a.Y - b.Y;
            return x * x + y * y;
        }
        public static double PointSegmentSquared(MapGeometryPoint p, MapGeometryPoint a, MapGeometryPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double length = dx * dx + dy * dy;
            double t = length == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length, 0, 1);
            return DistanceSquared(p, new MapGeometryPoint(a.X + t * dx, a.Y + t * dy));
        }
        static double Cross(MapGeometryPoint a, MapGeometryPoint b, MapGeometryPoint c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
        public static double SegmentSquared(MapGeometryPoint a, MapGeometryPoint b, MapGeometryPoint c, MapGeometryPoint d)
        {
            double abC = Cross(a, b, c), abD = Cross(a, b, d), cdA = Cross(c, d, a), cdB = Cross(c, d, b);
            var ab = new MapGeometryRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            var cd = new MapGeometryRect(Math.Min(c.X, d.X), Math.Min(c.Y, d.Y), Math.Max(c.X, d.X), Math.Max(c.Y, d.Y));
            if (ab.Intersects(cd) && ((abC <= 0 && abD >= 0) || (abC >= 0 && abD <= 0))
                && ((cdA <= 0 && cdB >= 0) || (cdA >= 0 && cdB <= 0))) { return 0; }
            return Math.Min(Math.Min(PointSegmentSquared(a, c, d), PointSegmentSquared(b, c, d)),
                Math.Min(PointSegmentSquared(c, a, b), PointSegmentSquared(d, a, b)));
        }
        public static double SegmentRectSquared(MapGeometryPoint a, MapGeometryPoint b, MapGeometryRect rect)
        {
            if (rect.Contains(a) || rect.Contains(b)) { return 0; }
            var p = new MapGeometryPoint(rect.MinX, rect.MinY);
            var q = new MapGeometryPoint(rect.MaxX, rect.MinY);
            var r = new MapGeometryPoint(rect.MaxX, rect.MaxY);
            var s = new MapGeometryPoint(rect.MinX, rect.MaxY);
            return Math.Min(Math.Min(SegmentSquared(a, b, p, q), SegmentSquared(a, b, q, r)),
                Math.Min(SegmentSquared(a, b, r, s), SegmentSquared(a, b, s, p)));
        }
    }
}
