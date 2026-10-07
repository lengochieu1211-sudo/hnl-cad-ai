using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Removes only MEP obstacle boxes whose full clearance envelope is strictly inside
    /// a non-drawable ceiling hole. Obstacles that touch/cross a hole edge remain active.
    /// </summary>
    internal static class VxtHoleObstacleFilter
    {
        private const double Tol = 0.1;

        public static IEnumerable<Box2> KeepDrawable(
            IEnumerable<Box2> boxes,
            IEnumerable<Boundary2> holes,
            double clearance)
        {
            var validHoles = (holes ?? Enumerable.Empty<Boundary2>())
                .Where(h => h != null && h.Vertices.Count >= 3)
                .ToList();

            foreach (var box in boxes ?? Enumerable.Empty<Box2>())
            {
                if (validHoles.Count == 0)
                {
                    yield return box;
                    continue;
                }

                var envelope = box.Expand(Math.Max(0.0, clearance));
                if (!validHoles.Any(h => BoxStrictlyInsideBoundary(envelope, h)))
                    yield return box;
            }
        }

        private static bool BoxStrictlyInsideBoundary(Box2 box, Boundary2 boundary)
        {
            var corners = new[]
            {
                new Point2(box.MinX, box.MinY),
                new Point2(box.MaxX, box.MinY),
                new Point2(box.MaxX, box.MaxY),
                new Point2(box.MinX, box.MaxY)
            };

            if (corners.Any(p => !StrictlyInside(boundary, p)))
                return false;

            // A concave hole boundary can enter the box even when all four corners happen to
            // test inside. Keep the obstacle whenever the hole boundary enters/touches the clearance
            // envelope; only a strictly contained envelope may be ignored.
            if (boundary.Vertices.Any(v => box.Contains(v, Tol)))
                return false;

            var boxEdges = new[]
            {
                Tuple.Create(corners[0], corners[1]),
                Tuple.Create(corners[1], corners[2]),
                Tuple.Create(corners[2], corners[3]),
                Tuple.Create(corners[3], corners[0])
            };

            for (var i = 0; i < boundary.Vertices.Count; i++)
            {
                var a = boundary.Vertices[i];
                var b = boundary.Vertices[(i + 1) % boundary.Vertices.Count];
                foreach (var edge in boxEdges)
                {
                    if (SegmentsIntersect(a, b, edge.Item1, edge.Item2))
                        return false;
                }
            }

            return true;
        }

        private static bool StrictlyInside(Boundary2 boundary, Point2 point)
        {
            var inside = false;
            var vertices = boundary.Vertices;

            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Count];

                if (DistanceToSegment(point, a, b) <= Tol)
                    return false;

                var crosses = (a.Y > point.Y) != (b.Y > point.Y);
                if (!crosses) continue;

                var x = a.X + (point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (x > point.X) inside = !inside;
            }

            return inside;
        }

        private static bool SegmentsIntersect(Point2 a, Point2 b, Point2 c, Point2 d)
        {
            var o1 = Cross(a, b, c);
            var o2 = Cross(a, b, d);
            var o3 = Cross(c, d, a);
            var o4 = Cross(c, d, b);

            if (((o1 > Tol && o2 < -Tol) || (o1 < -Tol && o2 > Tol)) &&
                ((o3 > Tol && o4 < -Tol) || (o3 < -Tol && o4 > Tol)))
                return true;

            if (Math.Abs(o1) <= Tol && OnSegment(a, b, c)) return true;
            if (Math.Abs(o2) <= Tol && OnSegment(a, b, d)) return true;
            if (Math.Abs(o3) <= Tol && OnSegment(c, d, a)) return true;
            if (Math.Abs(o4) <= Tol && OnSegment(c, d, b)) return true;
            return false;
        }

        private static bool OnSegment(Point2 a, Point2 b, Point2 p)
            => p.X >= Math.Min(a.X, b.X) - Tol &&
               p.X <= Math.Max(a.X, b.X) + Tol &&
               p.Y >= Math.Min(a.Y, b.Y) - Tol &&
               p.Y <= Math.Max(a.Y, b.Y) + Tol;

        private static double Cross(Point2 a, Point2 b, Point2 c)
            => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        private static double DistanceToSegment(Point2 p, Point2 a, Point2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length2 = dx * dx + dy * dy;
            if (length2 <= 1e-12) return p.DistanceTo(a);

            var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length2;
            t = Math.Max(0.0, Math.Min(1.0, t));
            var q = new Point2(a.X + t * dx, a.Y + t * dy);
            return p.DistanceTo(q);
        }
    }
}
