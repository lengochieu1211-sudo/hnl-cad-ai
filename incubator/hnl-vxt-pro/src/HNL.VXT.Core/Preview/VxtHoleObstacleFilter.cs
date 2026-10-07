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
            // test inside. A boundary vertex inside the envelope proves the box is not wholly void.
            if (boundary.Vertices.Any(v => box.Contains(v, Tol)))
                return false;

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
