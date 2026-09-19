using System;
using System.Collections.Generic;
using System.Linq;

namespace HNL.VXT.Core.Geometry
{
    /// <summary>
    /// Axis-aligned 2D box used by the AutoCAD-independent layout engine for
    /// equipment avoidance and manual rectangular regions.
    /// </summary>
    public readonly struct Box2
    {
        public Box2(double minX, double minY, double maxX, double maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public Point2 Center => new Point2((MinX + MaxX) * 0.5, (MinY + MaxY) * 0.5);

        public Box2 Expand(double value)
            => new Box2(MinX - value, MinY - value, MaxX + value, MaxY + value);

        public bool Contains(Point2 point, double tolerance = 0.0)
            => point.X >= MinX - tolerance && point.X <= MaxX + tolerance &&
               point.Y >= MinY - tolerance && point.Y <= MaxY + tolerance;

        public bool Intersects(Box2 other, double tolerance = 0.0)
            => !(other.MaxX < MinX - tolerance || other.MinX > MaxX + tolerance ||
                 other.MaxY < MinY - tolerance || other.MinY > MaxY + tolerance);

        public bool IntersectsHorizontal(double y, double x1, double x2, double tolerance = 1e-6)
        {
            if (y <= MinY + tolerance || y >= MaxY - tolerance) return false;
            var a = Math.Min(x1, x2);
            var b = Math.Max(x1, x2);
            return b > MinX + tolerance && a < MaxX - tolerance;
        }

        public bool IntersectsVertical(double x, double y1, double y2, double tolerance = 1e-6)
        {
            if (x <= MinX + tolerance || x >= MaxX - tolerance) return false;
            var a = Math.Min(y1, y2);
            var b = Math.Max(y1, y2);
            return b > MinY + tolerance && a < MaxY - tolerance;
        }

        public static Box2 FromPoints(IEnumerable<Point2> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var list = points.ToList();
            if (list.Count == 0) throw new ArgumentException("At least one point is required.", nameof(points));
            return new Box2(list.Min(p => p.X), list.Min(p => p.Y), list.Max(p => p.X), list.Max(p => p.Y));
        }

        public override string ToString() => $"[{MinX:0.###},{MinY:0.###}]→[{MaxX:0.###},{MaxY:0.###}]";
    }
}
