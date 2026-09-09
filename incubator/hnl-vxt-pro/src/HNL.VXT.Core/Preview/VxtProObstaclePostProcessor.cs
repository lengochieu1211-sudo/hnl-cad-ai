using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Final Pro-only safety net for residual MEP crossings.
    /// The normal solvers still get first priority: whole-grid shift and coordinate repair.
    /// Only a segment that still crosses an expanded obstacle is split at the clearance box.
    /// Legacy geometry never passes through this post-processor.
    /// </summary>
    public static class VxtProObstaclePostProcessor
    {
        private const double Eps = 1e-8;
        private const double Tol = 0.1;
        private const double MinPieceLength = 5.0;

        public static int Apply(
            VxtPreviewPlan plan,
            VxtSettings settings,
            VxtLayoutContext context,
            double selectedDirectionDegrees)
        {
            if (plan == null || settings == null ||
                settings.OptimizationMode == VxtOptimizationMode.Legacy ||
                !settings.UseAvoidance)
                return 0;

            context = context ?? new VxtLayoutContext();
            var radians = Normalize180(selectedDirectionDegrees) * Math.PI / 180.0;
            var mainObstacles = TransformAndExpand(
                context.GeneralObstacles.Concat(context.MainObstacles), radians, settings.ClearanceDistance);
            var furringObstacles = TransformAndExpand(
                context.GeneralObstacles.Concat(context.FurringObstacles), radians, settings.ClearanceDistance);

            if (mainObstacles.Count == 0 && furringObstacles.Count == 0) return 0;

            var output = new List<PreviewLine>(plan.Lines.Count + 8);
            var fallbackCount = 0;

            foreach (var line in plan.Lines)
            {
                if (line.Kind != PreviewLineKind.Main && line.Kind != PreviewLineKind.Furring)
                {
                    output.Add(line);
                    continue;
                }

                var localA = Transform2.ToLocal(line.A, radians);
                var localB = Transform2.ToLocal(line.B, radians);
                var obstacles = line.Kind == PreviewLineKind.Main ? mainObstacles : furringObstacles;
                IReadOnlyList<Segment2> pieces;

                if (line.Kind == PreviewLineKind.Main && Math.Abs(localA.Y - localB.Y) <= 0.5)
                    pieces = SplitHorizontal(new Segment2(localA, localB), obstacles);
                else if (line.Kind == PreviewLineKind.Furring && Math.Abs(localA.X - localB.X) <= 0.5)
                    pieces = SplitVertical(new Segment2(localA, localB), obstacles);
                else
                    pieces = new[] { new Segment2(localA, localB) };

                if (Changed(new Segment2(localA, localB), pieces)) fallbackCount++;

                foreach (var piece in pieces)
                {
                    var worldA = Transform2.ToWorld(piece.A, radians);
                    var worldB = Transform2.ToWorld(piece.B, radians);
                    output.Add(new PreviewLine(worldA, worldB, line.Kind));
                }
            }

            if (fallbackCount == 0) return 0;

            plan.Lines.Clear();
            plan.Lines.AddRange(output);
            plan.MainSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Main);
            plan.FurringSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Furring);
            return fallbackCount;
        }

        private static IReadOnlyList<Segment2> SplitHorizontal(Segment2 source, IReadOnlyList<Box2> boxes)
        {
            var y = (source.A.Y + source.B.Y) * 0.5;
            var lo = Math.Min(source.A.X, source.B.X);
            var hi = Math.Max(source.A.X, source.B.X);
            var forbidden = new List<Tuple<double, double>>();

            foreach (var box in boxes)
            {
                if (y <= box.MinY + Tol || y >= box.MaxY - Tol) continue;
                var a = Math.Max(lo, box.MinX);
                var b = Math.Min(hi, box.MaxX);
                if (b > a + Eps) forbidden.Add(Tuple.Create(a, b));
            }

            var ranges = Subtract(lo, hi, forbidden);
            var result = new List<Segment2>();
            foreach (var range in ranges)
            {
                if (range.Item2 - range.Item1 <= MinPieceLength) continue;
                result.Add(new Segment2(new Point2(range.Item1, y), new Point2(range.Item2, y)));
            }
            return result;
        }

        private static IReadOnlyList<Segment2> SplitVertical(Segment2 source, IReadOnlyList<Box2> boxes)
        {
            var x = (source.A.X + source.B.X) * 0.5;
            var lo = Math.Min(source.A.Y, source.B.Y);
            var hi = Math.Max(source.A.Y, source.B.Y);
            var forbidden = new List<Tuple<double, double>>();

            foreach (var box in boxes)
            {
                if (x <= box.MinX + Tol || x >= box.MaxX - Tol) continue;
                var a = Math.Max(lo, box.MinY);
                var b = Math.Min(hi, box.MaxY);
                if (b > a + Eps) forbidden.Add(Tuple.Create(a, b));
            }

            var ranges = Subtract(lo, hi, forbidden);
            var result = new List<Segment2>();
            foreach (var range in ranges)
            {
                if (range.Item2 - range.Item1 <= MinPieceLength) continue;
                result.Add(new Segment2(new Point2(x, range.Item1), new Point2(x, range.Item2)));
            }
            return result;
        }

        private static IReadOnlyList<Tuple<double, double>> Subtract(
            double lo,
            double hi,
            IEnumerable<Tuple<double, double>> forbidden)
        {
            var merged = Merge(forbidden, lo, hi);
            if (merged.Count == 0) return new[] { Tuple.Create(lo, hi) };

            var result = new List<Tuple<double, double>>();
            var cursor = lo;
            foreach (var interval in merged)
            {
                if (interval.Item1 > cursor + Eps)
                    result.Add(Tuple.Create(cursor, interval.Item1));
                cursor = Math.Max(cursor, interval.Item2);
                if (cursor >= hi - Eps) break;
            }
            if (cursor < hi - Eps) result.Add(Tuple.Create(cursor, hi));
            return result;
        }

        private static List<Tuple<double, double>> Merge(
            IEnumerable<Tuple<double, double>> source,
            double lo,
            double hi)
        {
            var ordered = (source ?? Enumerable.Empty<Tuple<double, double>>())
                .Where(x => x != null)
                .Select(x => Tuple.Create(
                    Math.Max(lo, Math.Min(x.Item1, x.Item2)),
                    Math.Min(hi, Math.Max(x.Item1, x.Item2))))
                .Where(x => x.Item2 > x.Item1 + Eps)
                .OrderBy(x => x.Item1)
                .ThenBy(x => x.Item2)
                .ToList();

            var merged = new List<Tuple<double, double>>();
            foreach (var interval in ordered)
            {
                if (merged.Count == 0 || interval.Item1 > merged[merged.Count - 1].Item2 + Eps)
                {
                    merged.Add(interval);
                    continue;
                }

                var last = merged[merged.Count - 1];
                merged[merged.Count - 1] = Tuple.Create(last.Item1, Math.Max(last.Item2, interval.Item2));
            }
            return merged;
        }

        private static bool Changed(Segment2 source, IReadOnlyList<Segment2> pieces)
        {
            if (pieces == null || pieces.Count != 1) return true;
            var piece = pieces[0];
            var same = (source.A.DistanceTo(piece.A) < 0.01 && source.B.DistanceTo(piece.B) < 0.01) ||
                       (source.A.DistanceTo(piece.B) < 0.01 && source.B.DistanceTo(piece.A) < 0.01);
            return !same;
        }

        private static List<Box2> TransformAndExpand(
            IEnumerable<Box2> boxes,
            double radians,
            double clearance)
        {
            var result = new List<Box2>();
            foreach (var box in boxes ?? Enumerable.Empty<Box2>())
                result.Add(TransformBox(box, radians).Expand(Math.Max(0.0, clearance)));
            return result;
        }

        private static Box2 TransformBox(Box2 box, double radians)
        {
            var points = new[]
            {
                new Point2(box.MinX, box.MinY), new Point2(box.MaxX, box.MinY),
                new Point2(box.MaxX, box.MaxY), new Point2(box.MinX, box.MaxY)
            }.Select(point => Transform2.ToLocal(point, radians));
            return Box2.FromPoints(points);
        }

        private static double Normalize180(double degrees)
        {
            degrees %= 180.0;
            if (degrees < 0.0) degrees += 180.0;
            return degrees;
        }
    }
}
