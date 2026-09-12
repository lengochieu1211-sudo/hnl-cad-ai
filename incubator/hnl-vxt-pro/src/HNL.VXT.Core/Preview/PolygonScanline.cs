using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.Core.Preview
{
    internal static class PolygonScanline
    {
        private const double Eps = 1e-8;

        public static IReadOnlyList<Segment2> ClipVertical(IReadOnlyList<Point2> polygon, double x)
        {
            var hits = new List<double>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                // Half-open crossing rule avoids duplicate intersections at vertices.
                var crosses = (a.X <= x && b.X > x) || (b.X <= x && a.X > x);
                if (!crosses) continue;

                var t = (x - a.X) / (b.X - a.X);
                hits.Add(a.Y + t * (b.Y - a.Y));
            }

            hits.Sort();
            return PairHits(hits, y => new Point2(x, y));
        }

        public static IReadOnlyList<Segment2> ClipHorizontal(IReadOnlyList<Point2> polygon, double y)
        {
            var hits = new List<double>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                var crosses = (a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y);
                if (!crosses) continue;

                var t = (y - a.Y) / (b.Y - a.Y);
                hits.Add(a.X + t * (b.X - a.X));
            }

            hits.Sort();
            return PairHits(hits, x => new Point2(x, y));
        }

        private static IReadOnlyList<Segment2> PairHits(List<double> hits, Func<double, Point2> makePoint)
        {
            var deduped = new List<double>();
            foreach (var h in hits)
            {
                if (deduped.Count == 0 || Math.Abs(deduped[deduped.Count - 1] - h) > Eps)
                    deduped.Add(h);
            }

            var segments = new List<Segment2>();
            for (var i = 0; i + 1 < deduped.Count; i += 2)
            {
                var a = makePoint(deduped[i]);
                var b = makePoint(deduped[i + 1]);
                if (a.DistanceTo(b) > Eps)
                    segments.Add(new Segment2(a, b));
            }

            return segments;
        }
    }
}
