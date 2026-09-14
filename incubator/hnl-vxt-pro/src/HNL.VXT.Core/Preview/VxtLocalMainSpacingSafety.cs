using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Final automatic notch pass.
    ///
    /// Construction contract:
    /// - the normal/global XC grid is solved first;
    /// - a local XC is added only when a real notch band would otherwise violate MaxEdge or
    ///   MainMaxSpacing;
    /// - a required local edge XC may be closer than MainMinSpacing to a neighbouring global XC;
    /// - a sub-MinSpacing local XC is removed only when it is redundant and the local band remains
    ///   hard-max safe without it.
    ///
    /// This deliberately treats MainMinSpacing as a preferred normal-grid rule, while MaxEdge and
    /// MainMaxSpacing are the hard constraints that justify local notch reinforcement.
    /// Manual RectangleRegions is user-authored and is intentionally excluded.
    /// </summary>
    internal static class VxtLocalMainSpacingSafety
    {
        private const double Tol = 0.5;
        private const double MinOverlap = 1.0;
        private const double MinDrawLength = 5.0;

        public static void Apply(
            Boundary2 boundary,
            VxtPreviewPlan plan,
            VxtSettings settings,
            double angleDegrees,
            VxtLayoutContext context)
        {
            if (boundary == null || plan == null || settings == null) return;
            if (!settings.UseLocalMainAdd || settings.MainDirection == MainDirectionMode.RectangleRegions) return;
            if (!settings.DrawMain || settings.MainBalanceStep <= 0.0) return;

            context = context ?? new VxtLayoutContext();
            var radians = NormalizeDegrees(angleDegrees) * Math.PI / 180.0;
            var polygon = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
            if (polygon.Count < 3) return;

            var obstacles = TransformMainObstacles(context, radians, settings.ClearanceDistance);

            // First enforce the reason this feature exists: the local ceiling edge/gap must respect
            // configured maxima. This pass is phase-locked to the existing XC lattice.
            EnsureHardMaxCoverage(plan, polygon, radians, settings, obstacles);

            // Then remove only truly redundant close local bars. A bar that protects MaxEdge or a
            // MaxSpacing gap is retained even when its distance to another XC is < MainMinSpacing.
            if (settings.MainMinSpacing > Tol)
            {
                while (true)
                {
                    var mains = BuildMainRecords(plan, radians);
                    MainRecord victim = null;

                    for (var i = 0; i + 1 < mains.Count && victim == null; i++)
                    {
                        for (var j = i + 1; j < mains.Count; j++)
                        {
                            var a = mains[i];
                            var b = mains[j];
                            var dy = Math.Abs(a.Y - b.Y);
                            if (dy <= Tol || dy >= settings.MainMinSpacing - Tol) continue;

                            var overlap = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
                            if (overlap <= MinOverlap) continue;

                            MainRecord candidate = null;
                            if (a.Length < b.Length - Tol)
                                candidate = a;
                            else if (b.Length < a.Length - Tol)
                                candidate = b;

                            // Equal-length/global rows are never deleted here. This pass only cleans
                            // optional shorter notch bars.
                            if (candidate == null) continue;
                            if (IsRequiredForHardMaxConstraint(candidate, mains, polygon, settings))
                                continue;

                            victim = candidate;
                            break;
                        }
                    }

                    if (victim == null) break;
                    RemoveMainAndItsHangers(plan, victim, radians);
                }
            }

            plan.MainSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Main);
            plan.HangerCount = plan.HangerPoints.Count;
        }

        private static void EnsureHardMaxCoverage(
            VxtPreviewPlan plan,
            IReadOnlyList<Point2> polygon,
            double radians,
            VxtSettings settings,
            IReadOnlyList<Box2> obstacles)
        {
            var domain = Box2.FromPoints(polygon);
            var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
            var step = settings.MainBalanceStep;

            for (var pass = 0; pass < 4; pass++)
            {
                var mains = BuildMainRecords(plan, radians);
                if (mains.Count == 0) return;
                var latticeOrigin = mains.OrderBy(m => m.Y).First().Y;
                var specs = new List<LocalSpec>();

                var xs = UniqueSort(polygon.Select(p => p.X)
                    .Concat(new[] { domain.MinX, domain.MaxX }), 0.5);

                for (var i = 0; i + 1 < xs.Count; i++)
                {
                    var bandA = Math.Max(domain.MinX, xs[i]);
                    var bandB = Math.Min(domain.MaxX, xs[i + 1]);
                    if (bandB - bandA <= MinDrawLength) continue;

                    var sampleXs = new[]
                    {
                        bandA + 0.25 * (bandB - bandA),
                        bandA + 0.50 * (bandB - bandA),
                        bandA + 0.75 * (bandB - bandA)
                    };

                    foreach (var sampleX in sampleXs)
                    {
                        foreach (var interval in PolygonScanline.ClipVertical(polygon, sampleX))
                        {
                            var a = Math.Max(domain.MinY, Math.Min(interval.A.Y, interval.B.Y));
                            var b = Math.Min(domain.MaxY, Math.Max(interval.A.Y, interval.B.Y));
                            if (b - a <= 1.0) continue;

                            var rows = mains
                                .Where(m => CrossesX(m, sampleX) && m.Y >= a - Tol && m.Y <= b + Tol)
                                .Select(m => m.Y)
                                .Distinct(new DoubleTolComparer())
                                .OrderBy(y => y)
                                .ToList();

                            foreach (var y in RequiredRows(rows, a, b, latticeOrigin, step, settings, maxEdge))
                                AddOrMergeSpec(specs, y, bandA, bandB);
                        }
                    }
                }

                if (specs.Count == 0) break;

                var addedAny = false;
                foreach (var spec in MergeSpecs(specs))
                    if (AddLocalMain(plan, polygon, radians, spec, settings, obstacles))
                        addedAny = true;

                if (!addedAny) break;
            }
        }

        private static IEnumerable<double> RequiredRows(
            IReadOnlyList<double> current,
            double a,
            double b,
            double latticeOrigin,
            double step,
            VxtSettings settings,
            double maxEdge)
        {
            var result = new List<double>();
            var rows = (current ?? new double[0]).OrderBy(y => y).ToList();

            if (rows.Count == 0)
            {
                var first = EdgePointFromBottom(a, b, latticeOrigin, step, settings, maxEdge);
                if (first.HasValue)
                {
                    AddUnique(result, first.Value);
                    rows.Add(first.Value);
                }
            }

            if (rows.Count > 0 && rows[0] - a > maxEdge + Tol)
            {
                var y = EdgePointFromBottom(a, rows[0], latticeOrigin, step, settings, maxEdge);
                if (y.HasValue) AddUnique(result, y.Value);
            }

            var combined = rows.Concat(result).OrderBy(y => y).ToList();
            var maxStep = FloorMultiple(settings.MainMaxSpacing, step);
            if (maxStep <= Tol) maxStep = settings.MainMaxSpacing;

            var index = 0;
            while (index + 1 < combined.Count)
            {
                var left = combined[index];
                var right = combined[index + 1];
                if (right - left > settings.MainMaxSpacing + Tol)
                {
                    var y = SnapAtOrBelow(left + settings.MainMaxSpacing, latticeOrigin, step);
                    if (y <= left + Tol) y = left + maxStep;
                    if (y > left + Tol && y < right - Tol)
                    {
                        AddUnique(result, y);
                        combined.Add(y);
                        combined.Sort();
                        continue;
                    }
                }
                index++;
            }

            combined = rows.Concat(result).OrderBy(y => y).ToList();
            if (combined.Count > 0 && b - combined[combined.Count - 1] > maxEdge + Tol)
            {
                var y = EdgePointFromTop(combined[combined.Count - 1], b, latticeOrigin, step, settings, maxEdge);
                if (y.HasValue) AddUnique(result, y.Value);
            }

            return result.Where(y => y > a + Tol && y < b - Tol).OrderBy(y => y);
        }

        private static double? EdgePointFromBottom(
            double a,
            double b,
            double origin,
            double step,
            VxtSettings settings,
            double maxEdge)
        {
            var high = Math.Min(b - Tol, a + maxEdge);
            var low = Math.Min(high, a + Math.Max(0.0, settings.MainMinEdgeOffset));
            var candidate = SnapAtOrBelow(high, origin, step);
            if (candidate < low - Tol)
                candidate = SnapAtOrAbove(low, origin, step);
            return candidate > a + Tol && candidate < b - Tol && candidate <= a + maxEdge + Tol
                ? (double?)candidate
                : null;
        }

        private static double? EdgePointFromTop(
            double a,
            double b,
            double origin,
            double step,
            VxtSettings settings,
            double maxEdge)
        {
            var low = Math.Max(a + Tol, b - maxEdge);
            var high = Math.Max(low, b - Math.Max(0.0, settings.MainMinEdgeOffset));
            var candidate = SnapAtOrAbove(low, origin, step);
            if (candidate > high + Tol)
                candidate = SnapAtOrBelow(high, origin, step);
            return candidate > a + Tol && candidate < b - Tol && candidate >= b - maxEdge - Tol
                ? (double?)candidate
                : null;
        }

        private static bool AddLocalMain(
            VxtPreviewPlan plan,
            IReadOnlyList<Point2> polygon,
            double radians,
            LocalSpec spec,
            VxtSettings settings,
            IReadOnlyList<Box2> obstacles)
        {
            var added = false;
            foreach (var raw in PolygonScanline.ClipHorizontal(polygon, spec.Y))
            {
                var x1 = Math.Max(Math.Min(raw.A.X, raw.B.X), spec.X1);
                var x2 = Math.Min(Math.Max(raw.A.X, raw.B.X), spec.X2);
                if (x2 - x1 <= MinDrawLength) continue;
                if (x2 - x1 < settings.MinLocalMainLength - Tol) continue;

                var local = new Segment2(new Point2(x1, spec.Y), new Point2(x2, spec.Y));
                if (settings.UseAvoidance && !SegmentClear(local, obstacles)) continue;
                if (MainAlreadyCovers(plan, local, radians)) continue;

                plan.Lines.Add(new PreviewLine(
                    Transform2.ToWorld(local.A, radians),
                    Transform2.ToWorld(local.B, radians),
                    PreviewLineKind.Main));
                plan.MainSegmentCount++;
                added = true;

                if (settings.DrawHangers)
                    AddHangers(plan, local, radians, settings, obstacles);
            }
            return added;
        }

        private static void AddHangers(
            VxtPreviewPlan plan,
            Segment2 main,
            double radians,
            VxtSettings settings,
            IReadOnlyList<Box2> obstacles)
        {
            var minX = Math.Min(main.A.X, main.B.X);
            var maxX = Math.Max(main.A.X, main.B.X);
            var length = maxX - minX;
            if (length <= MinDrawLength) return;

            var mode = settings.HangerLayout == HangerLayoutMode.OneSideFollowFurring
                ? MainLayoutMode.OneSide
                : MainLayoutMode.BalancedTwoEnds;
            var layout = SmartLayout1D.Calculate(
                length,
                settings.HangerMaxSpacing,
                settings.HangerMinSpacing,
                settings.HangerMaxEdgeOffset,
                settings.HangerMinEdgeOffset,
                settings.HangerBalanceStep,
                mode);
            if (layout == null) return;

            IReadOnlyList<double> xs = layout.Positions(minX)
                .Where(x => x > minX + 2.0 && x < maxX - 2.0)
                .ToList();

            if (settings.UseAvoidance && obstacles != null && obstacles.Count > 0)
            {
                var intervals = obstacles
                    .Where(b => main.A.Y >= b.MinY - Tol && main.A.Y <= b.MaxY + Tol)
                    .Select(b => Tuple.Create(b.MinX, b.MaxX))
                    .ToList();
                if (intervals.Count > 0)
                {
                    xs = LegacyGridAvoidance.AdjustGrid(
                        xs,
                        intervals,
                        minX,
                        maxX,
                        settings.HangerMinSpacing,
                        settings.HangerMaxSpacing,
                        settings.HangerMinEdgeOffset,
                        settings.HangerMaxEdgeOffset,
                        settings.HangerBalanceStep);
                }
            }

            foreach (var x in xs)
            {
                if (x <= minX + 2.0 || x >= maxX - 2.0) continue;
                var localPoint = new Point2(x, main.A.Y);
                var world = Transform2.ToWorld(localPoint, radians);
                if (plan.HangerPoints.Any(p => p.DistanceTo(world) <= 0.01)) continue;

                plan.HangerPoints.Add(world);
                plan.HangerCount++;
                const double half = 45.0;
                var h1 = Transform2.ToWorld(new Point2(x - half, main.A.Y), radians);
                var h2 = Transform2.ToWorld(new Point2(x + half, main.A.Y), radians);
                var v1 = Transform2.ToWorld(new Point2(x, main.A.Y - half), radians);
                var v2 = Transform2.ToWorld(new Point2(x, main.A.Y + half), radians);
                plan.Lines.Add(new PreviewLine(h1, h2, PreviewLineKind.Hanger));
                plan.Lines.Add(new PreviewLine(v1, v2, PreviewLineKind.Hanger));
            }
        }

        private static bool MainAlreadyCovers(VxtPreviewPlan plan, Segment2 local, double radians)
        {
            foreach (var line in plan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var a = Transform2.ToLocal(line.A, radians);
                var b = Transform2.ToLocal(line.B, radians);
                var y = (a.Y + b.Y) * 0.5;
                if (Math.Abs(y - local.A.Y) > Tol) continue;
                var x1 = Math.Min(a.X, b.X);
                var x2 = Math.Max(a.X, b.X);
                if (x1 <= local.A.X + Tol && x2 >= local.B.X - Tol) return true;
            }
            return false;
        }

        private static bool IsRequiredForHardMaxConstraint(
            MainRecord candidate,
            IReadOnlyList<MainRecord> mains,
            IReadOnlyList<Point2> polygon,
            VxtSettings settings)
        {
            var width = candidate.X2 - candidate.X1;
            if (width <= MinOverlap) return false;

            var sampleXs = new[]
            {
                candidate.X1 + width * 0.20,
                candidate.X1 + width * 0.50,
                candidate.X1 + width * 0.80
            };

            foreach (var x in sampleXs)
            {
                foreach (var interval in PolygonScanline.ClipVertical(polygon, x))
                {
                    var a = Math.Min(interval.A.Y, interval.B.Y);
                    var b = Math.Max(interval.A.Y, interval.B.Y);
                    if (candidate.Y < a - Tol || candidate.Y > b + Tol) continue;

                    var withCandidate = mains
                        .Where(m => CrossesX(m, x) && m.Y >= a - Tol && m.Y <= b + Tol)
                        .Select(m => m.Y)
                        .Distinct(new DoubleTolComparer())
                        .OrderBy(y => y)
                        .ToList();
                    var withoutCandidate = mains
                        .Where(m => !ReferenceEquals(m, candidate) && CrossesX(m, x) && m.Y >= a - Tol && m.Y <= b + Tol)
                        .Select(m => m.Y)
                        .Distinct(new DoubleTolComparer())
                        .OrderBy(y => y)
                        .ToList();

                    if (CountHardMaxViolations(withoutCandidate, a, b, settings) >
                        CountHardMaxViolations(withCandidate, a, b, settings))
                        return true;
                }
            }
            return false;
        }

        private static int CountHardMaxViolations(
            IReadOnlyList<double> ys,
            double edgeA,
            double edgeB,
            VxtSettings settings)
        {
            if (ys == null || ys.Count == 0) return 1;
            var violations = 0;
            var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
            if (ys[0] - edgeA > maxEdge + Tol) violations++;
            if (edgeB - ys[ys.Count - 1] > maxEdge + Tol) violations++;
            for (var i = 0; i + 1 < ys.Count; i++)
                if (ys[i + 1] - ys[i] > settings.MainMaxSpacing + Tol)
                    violations++;
            return violations;
        }

        private static bool CrossesX(MainRecord main, double x)
            => x >= main.X1 - Tol && x <= main.X2 + Tol;

        private static List<MainRecord> BuildMainRecords(VxtPreviewPlan plan, double radians)
        {
            var result = new List<MainRecord>();
            for (var index = 0; index < plan.Lines.Count; index++)
            {
                var line = plan.Lines[index];
                if (line.Kind != PreviewLineKind.Main) continue;
                var a = Transform2.ToLocal(line.A, radians);
                var b = Transform2.ToLocal(line.B, radians);
                if (Math.Abs(a.Y - b.Y) > Tol) continue;
                result.Add(new MainRecord(index, (a.Y + b.Y) * 0.5, Math.Min(a.X, b.X), Math.Max(a.X, b.X)));
            }
            return result;
        }

        private static void RemoveMainAndItsHangers(VxtPreviewPlan plan, MainRecord victim, double radians)
        {
            var removedHangers = plan.HangerPoints
                .Where(point => PointBelongsToMain(point, victim, radians))
                .ToList();

            if (victim.LineIndex >= 0 && victim.LineIndex < plan.Lines.Count)
                plan.Lines.RemoveAt(victim.LineIndex);

            if (removedHangers.Count == 0) return;
            plan.HangerPoints.RemoveAll(point => removedHangers.Any(removed => removed.DistanceTo(point) <= 0.01));
            plan.Lines.RemoveAll(line =>
            {
                if (line.Kind != PreviewLineKind.Hanger) return false;
                var midpoint = new Point2((line.A.X + line.B.X) * 0.5, (line.A.Y + line.B.Y) * 0.5);
                return removedHangers.Any(point => point.DistanceTo(midpoint) <= 0.01);
            });
        }

        private static bool PointBelongsToMain(Point2 worldPoint, MainRecord main, double radians)
        {
            var local = Transform2.ToLocal(worldPoint, radians);
            return Math.Abs(local.Y - main.Y) <= Tol && local.X >= main.X1 - Tol && local.X <= main.X2 + Tol;
        }

        private static IReadOnlyList<Box2> TransformMainObstacles(
            VxtLayoutContext context,
            double radians,
            double clearance)
        {
            return (context.GeneralObstacles ?? new List<Box2>())
                .Concat(context.MainObstacles ?? new List<Box2>())
                .Select(box => TransformBox(box, radians).Expand(Math.Max(0.0, clearance)))
                .ToList();
        }

        private static Box2 TransformBox(Box2 box, double radians)
        {
            return Box2.FromPoints(new[]
            {
                Transform2.ToLocal(new Point2(box.MinX, box.MinY), radians),
                Transform2.ToLocal(new Point2(box.MaxX, box.MinY), radians),
                Transform2.ToLocal(new Point2(box.MaxX, box.MaxY), radians),
                Transform2.ToLocal(new Point2(box.MinX, box.MaxY), radians)
            });
        }

        private static bool SegmentClear(Segment2 segment, IEnumerable<Box2> obstacles)
        {
            foreach (var box in obstacles ?? Enumerable.Empty<Box2>())
                if (box.IntersectsHorizontal(segment.A.Y, segment.A.X, segment.B.X, Tol))
                    return false;
            return true;
        }

        private static void AddOrMergeSpec(List<LocalSpec> specs, double y, double x1, double x2)
        {
            foreach (var spec in specs)
            {
                if (Math.Abs(spec.Y - y) > Tol) continue;
                if (x1 > spec.X2 + Tol || x2 < spec.X1 - Tol) continue;
                spec.X1 = Math.Min(spec.X1, x1);
                spec.X2 = Math.Max(spec.X2, x2);
                return;
            }
            specs.Add(new LocalSpec(y, x1, x2));
        }

        private static IEnumerable<LocalSpec> MergeSpecs(IEnumerable<LocalSpec> source)
        {
            var output = new List<LocalSpec>();
            foreach (var item in source.OrderBy(s => s.Y).ThenBy(s => s.X1))
                AddOrMergeSpec(output, item.Y, item.X1, item.X2);
            return output;
        }

        private static void AddUnique(List<double> values, double value)
        {
            if (!values.Any(x => Math.Abs(x - value) <= Tol)) values.Add(value);
        }

        private static List<double> UniqueSort(IEnumerable<double> values, double tolerance)
        {
            var result = new List<double>();
            foreach (var value in (values ?? Enumerable.Empty<double>()).OrderBy(v => v))
                if (result.Count == 0 || Math.Abs(result[result.Count - 1] - value) > tolerance)
                    result.Add(value);
            return result;
        }

        private static double FloorMultiple(double value, double step)
            => step <= 0.0 ? value : Math.Floor((value + 1e-9) / step) * step;

        private static double SnapAtOrBelow(double value, double origin, double step)
            => step <= 0.0 ? value : origin + Math.Floor(((value - origin) + 1e-9) / step) * step;

        private static double SnapAtOrAbove(double value, double origin, double step)
            => step <= 0.0 ? value : origin + Math.Ceiling(((value - origin) - 1e-9) / step) * step;

        private static double NormalizeDegrees(double value)
        {
            value %= 360.0;
            return value < 0.0 ? value + 360.0 : value;
        }

        private sealed class MainRecord
        {
            public MainRecord(int lineIndex, double y, double x1, double x2)
            {
                LineIndex = lineIndex;
                Y = y;
                X1 = x1;
                X2 = x2;
            }
            public int LineIndex { get; }
            public double Y { get; }
            public double X1 { get; }
            public double X2 { get; }
            public double Length => X2 - X1;
        }

        private sealed class LocalSpec
        {
            public LocalSpec(double y, double x1, double x2)
            {
                Y = y;
                X1 = Math.Min(x1, x2);
                X2 = Math.Max(x1, x2);
            }
            public double Y { get; }
            public double X1 { get; set; }
            public double X2 { get; set; }
        }

        private sealed class DoubleTolComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) <= Tol;
            public int GetHashCode(double obj) => Math.Round(obj / Tol).GetHashCode();
        }
    }
}
