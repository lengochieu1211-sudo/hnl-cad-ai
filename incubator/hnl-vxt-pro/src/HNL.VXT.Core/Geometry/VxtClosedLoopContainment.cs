using System;

namespace HNL.VXT.Core.Geometry
{
    /// <summary>
    /// Conservative geometric containment for user-selected ceiling loops.
    /// Distinct from layout planners: touching, crossing and partially
    /// overlapping paths are NOT inner ceiling holes.
    /// </summary>
    public static class VxtClosedLoopContainment
    {
        public static bool ContainsWholeLoop(Boundary2 inner, Boundary2 outer)
        {
            if (inner == null || outer == null ||
                inner.Vertices.Count < 3 || outer.Vertices.Count < 3)
                return false;

            var innerBox = inner.GetBounds();
            var outerBox = outer.GetBounds();
            const double eps = 1e-8;
            if (innerBox.Min.X < outerBox.Min.X - eps ||
                innerBox.Min.Y < outerBox.Min.Y - eps ||
                innerBox.Max.X > outerBox.Max.X + eps ||
                innerBox.Max.Y > outerBox.Max.Y + eps)
                return false;

            // Check all vertices and midpoints, plus every true intersection
            // between inner and outer edges. A narrow notch can cut an edge
            // despite all sample points lying inside the outer polygon.
            for (var i = 0; i < inner.Vertices.Count; i++)
            {
                var a = inner.Vertices[i];
                var b = inner.Vertices[(i + 1) % inner.Vertices.Count];
                var middle = new Point2((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                if (!ContainsPoint(outer, a) || !ContainsPoint(outer, middle))
                    return false;

                for (var j = 0; j < outer.Vertices.Count; j++)
                {
                    var c = outer.Vertices[j];
                    var d = outer.Vertices[(j + 1) % outer.Vertices.Count];
                    if (SegmentsIntersect(a, b, c, d))
                        return false;
                }
            }
            return true;
        }

        private static bool ContainsPoint(Boundary2 loop, Point2 p)
        {
            var inside = false;
            for (var i = 0; i < loop.Vertices.Count; i++)
            {
                var a = loop.Vertices[i];
                var b = loop.Vertices[(i + 1) % loop.Vertices.Count];
                if ((a.Y > p.Y) == (b.Y > p.Y))
                    continue;
                var x = a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (x > p.X) inside = !inside;
            }
            return inside;
        }

        private static bool SegmentsIntersect(Point2 a, Point2 b, Point2 c, Point2 d)
        {
            const double eps = 1e-8;
            if (Math.Max(a.X, b.X) + eps < Math.Min(c.X, d.X) ||
                Math.Max(c.X, d.X) + eps < Math.Min(a.X, b.X) ||
                Math.Max(a.Y, b.Y) + eps < Math.Min(c.Y, d.Y) ||
                Math.Max(c.Y, d.Y) + eps < Math.Min(a.Y, b.Y))
                return false;

            var o1 = Cross(a, b, c);
            var o2 = Cross(a, b, d);
            var o3 = Cross(c, d, a);
            var o4 = Cross(c, d, b);
            if ((o1 > eps && o2 > eps) || (o1 < -eps && o2 < -eps) ||
                (o3 > eps && o4 > eps) || (o3 < -eps && o4 < -eps))
                return false;
            return true; // Proper crossing, touching or collinear overlap.
        }

        private static double Cross(Point2 a, Point2 b, Point2 c)
            => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }
}
