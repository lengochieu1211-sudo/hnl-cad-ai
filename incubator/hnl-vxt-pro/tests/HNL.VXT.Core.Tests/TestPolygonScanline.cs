using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.Core.Tests
{
    internal static class TestPolygonScanline
    {
        public static IReadOnlyList<Segment2> ClipVertical(IReadOnlyList<Point2> polygon, double x)
        {
            var intersections = new List<double>();
            if (polygon == null || polygon.Count < 3) return Array.Empty<Segment2>();

            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var dx = b.X - a.X;
                if (Math.Abs(dx) <= 1e-9) continue;

                // Half-open crossing rule avoids double-counting a polygon vertex.
                var crosses = (a.X <= x && x < b.X) || (b.X <= x && x < a.X);
                if (!crosses) continue;

                var t = (x - a.X) / dx;
                intersections.Add(a.Y + t * (b.Y - a.Y));
            }

            intersections.Sort();
            var result = new List<Segment2>();
            for (var i = 0; i + 1 < intersections.Count; i += 2)
            {
                var y1 = intersections[i];
                var y2 = intersections[i + 1];
                if (y2 - y1 <= 1e-9) continue;
                result.Add(new Segment2(new Point2(x, y1), new Point2(x, y2)));
            }
            return result;
        }
    }
}
