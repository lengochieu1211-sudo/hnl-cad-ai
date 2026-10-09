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
            => ClipVertical(new[] { polygon }, x);

        public static IReadOnlyList<Segment2> ClipVertical(
            IEnumerable<IReadOnlyList<Point2>> polygons,
            double x)
        {
            var hits = new List<double>();
            foreach (var polygon in polygons ?? Enumerable.Empty<IReadOnlyList<Point2>>())
            {
                if (polygon == null || polygon.Count < 3) continue;
                for (var i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    var crosses = (a.X <= x && b.X > x) || (b.X <= x && a.X > x);
                    if (!crosses) continue;
                    var t = (x - a.X) / (b.X - a.X);
                    hits.Add(a.Y + t * (b.Y - a.Y));
                }
            }
            hits.Sort();
            return PairHits(hits, y => new Point2(x, y));
        }

        public static IReadOnlyList<Segment2> ClipHorizontal(IReadOnlyList<Point2> polygon, double y)
            => ClipHorizontal(new[] { polygon }, y);

        public static IReadOnlyList<Segment2> ClipHorizontal(
            IEnumerable<IReadOnlyList<Point2>> polygons,
            double y)
        {
            var hits = new List<double>();
            foreach (var polygon in polygons ?? Enumerable.Empty<IReadOnlyList<Point2>>())
            {
                if (polygon == null || polygon.Count < 3) continue;
                for (var i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    var crosses = (a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y);
                    if (!crosses) continue;
                    var t = (y - a.Y) / (b.Y - a.Y);
                    hits.Add(a.X + t * (b.X - a.X));
                }
            }
            hits.Sort();
            return PairHits(hits, x => new Point2(x, y));
        }

        public static IReadOnlyList<Segment2> ClipVertical(
            IReadOnlyList<Point2> outer,
            IEnumerable<IReadOnlyList<Point2>> holes,
            double x)
        {
            var source = ClipVertical(outer, x);
            var cuts = (holes ?? Enumerable.Empty<IReadOnlyList<Point2>>())
                .Where(h => h != null && h.Count >= 3)
                .SelectMany(h => ClipVertical(h, x))
                .ToList();

            return SubtractVertical(source, cuts, x);
        }

        private static IReadOnlyList<Segment2> SubtractVertical(
            IEnumerable<Segment2> source,
            IEnumerable<Segment2> cuts,
            double x)
        {
            var cutRanges = (cuts ?? Enumerable.Empty<Segment2>())
                .Select(s => Tuple.Create(
                    Math.Min(s.A.Y, s.B.Y),
                    Math.Max(s.A.Y, s.B.Y)))
                .Where(r => r.Item2 - r.Item1 > Eps)
                .OrderBy(r => r.Item1)
                .ToList();

            var result = new List<Segment2>();
            foreach (var segment in source ?? Enumerable.Empty<Segment2>())
            {
                var pieces = new List<Tuple<double, double>>
                {
                    Tuple.Create(
                        Math.Min(segment.A.Y, segment.B.Y),
                        Math.Max(segment.A.Y, segment.B.Y))
                };

                foreach (var cut in cutRanges)
                {
                    var next = new List<Tuple<double, double>>();
                    foreach (var piece in pieces)
                    {
                        var a = piece.Item1;
                        var b = piece.Item2;
                        var c = cut.Item1;
                        var d = cut.Item2;

                        if (d <= a + Eps || c >= b - Eps)
                        {
                            next.Add(piece);
                            continue;
                        }

                        if (c > a + Eps)
                            next.Add(Tuple.Create(a, Math.Min(b, c)));
                        if (d < b - Eps)
                            next.Add(Tuple.Create(Math.Max(a, d), b));
                    }

                    pieces = next;
                    if (pieces.Count == 0) break;
                }

                foreach (var piece in pieces)
                {
                    if (piece.Item2 - piece.Item1 <= Eps) continue;
                    result.Add(new Segment2(
                        new Point2(x, piece.Item1),
                        new Point2(x, piece.Item2)));
                }
            }

            return result;
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
