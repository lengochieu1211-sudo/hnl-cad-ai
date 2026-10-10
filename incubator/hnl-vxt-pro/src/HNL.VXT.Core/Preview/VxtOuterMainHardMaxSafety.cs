using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Optional HARD Max supplement for normal convex non-orthogonal outer boundaries.
    ///
    /// This pass is gated by "Thêm XC cạnh khuyết" / UseLocalMainAdd:
    /// - OFF preserves the certified base XC geometry exactly;
    /// - ON may supplement missing outer-edge / adjacent-spacing HARD coverage;
    /// - orthogonal/notch geometry remains owned by VxtLocalMainSpacingSafety;
    /// - concave non-orthogonal geometry is not auto-repaired here;
    /// - the certified global XC grid is never shifted or replaced.
    /// </summary>
    internal static class VxtOuterMainHardMaxSafety
    {
        private const double Tol = 0.5;
        private const double MinDrawLength = 5.0;

        public static bool Apply(
            Boundary2 boundary,
            VxtPreviewPlan plan,
            VxtSettings settings,
            double angleDegrees,
            VxtLayoutContext context)
        {
            if (boundary == null || plan == null || settings == null) return false;
            if (!settings.DrawMain || !settings.UseLocalMainAdd || settings.MainBalanceStep <= Tol) return false;
            if (settings.MainDirection == MainDirectionMode.RectangleRegions ||
                settings.MainDirection == MainDirectionMode.PolylinePath)
                return false;

            context = context ?? new VxtLayoutContext();
            var radians = Normalize180(angleDegrees) * Math.PI / 180.0;
            var polygon = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
            if (polygon.Count < 3) return false;

            // Normal rectangles/orthogonal notches must be bit-for-bit governed by their existing
            // paths. Concave sloped/curved geometry remains manual until a separately certified
            // concave-curve contract exists.
            if (IsOrthogonalPolygon(polygon) || !IsConvexPolygon(polygon))
                return false;

            var obstacles = settings.UseAvoidance
                ? TransformMainObstacles(context, radians, settings.ClearanceDistance)
                : new List<Box2>();

            var changed = false;
            for (var pass = 0; pass < 4; pass++)
            {
                var mains = BuildMainRecords(plan, radians);
                if (mains.Count == 0) break;

                var origin = mains.OrderBy(x => x.Y).First().Y;
                var specs = BuildRequiredSpecs(
                    polygon,
                    mains,
                    origin,
                    settings.MainBalanceStep,
                    settings.MainMinEdgeOffset,
                    settings.MainMaxEdgeOffset,
                    settings.MainMaxSpacing);

                if (specs.Count == 0) break;

                var added = false;
                foreach (var spec in MergeSpecs(specs))
                {
                    if (AddLocalMain(plan, polygon, radians, spec, settings, obstacles))
                        added = true;
                }

                if (!added) break;
                changed = true;
            }

            if (changed)
            {
                plan.MainSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Main);
                plan.HangerCount = plan.HangerPoints.Count;
            }

            return changed;
        }

        private static List<LocalSpec> BuildRequiredSpecs(
            IReadOnlyList<Point2> polygon,
            IReadOnlyList<MainRecord> mains,
            double latticeOrigin,
            double step,
            double minEdge,
            double maxEdge,
            double maxSpacing)
        {
            var result = new List<LocalSpec>();
            var domain = Box2.FromPoints(polygon);
            var xs = UniqueSort(
                polygon.Select(p => p.X).Concat(new[] { domain.MinX, domain.MaxX }),
                Tol);

            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var x1 = Math.Max(domain.MinX, xs[i]);
                var x2 = Math.Min(domain.MaxX, xs[i + 1]);
                if (x2 - x1 <= 1.0) continue;

                var samples = new[]
                {
                    x1 + 0.25 * (x2 - x1),
                    x1 + 0.50 * (x2 - x1),
                    x1 + 0.75 * (x2 - x1)
                };

                foreach (var x in samples)
                {
                    var intervals = PolygonScanline.ClipVertical(polygon, x).ToList();
                    if (intervals.Count != 1) continue;

                    var a = Math.Min(intervals[0].A.Y, intervals[0].B.Y);
                    var b = Math.Max(intervals[0].A.Y, intervals[0].B.Y);
                    if (b - a <= 1.0) continue;

                    var rows = mains
                        .Where(m => CrossesX(m, x) &&
                                    m.Y >= a - Tol &&
                                    m.Y <= b + Tol)
                        .Select(m => m.Y)
                        .Distinct(new DoubleTolComparer())
                        .OrderBy(y => y)
                        .ToList();

                    foreach (var y in RequiredRows(
                        rows, a, b, latticeOrigin, step, minEdge, maxEdge, maxSpacing))
                    {
                        AddOrMergeSpec(result, y, x1, x2);
                    }
                }
            }

            return result;
        }

        private static IEnumerable<double> RequiredRows(
            IReadOnlyList<double> existing,
            double a,
            double b,
            double origin,
            double step,
            double minEdge,
            double maxEdge,
            double maxSpacing)
        {
            var additions = new List<double>();
            // Use a stable edge-guard lattice rather than the fine balance lattice. For example,
            // MaxEdge=400 and Step=50 yields guard rows every 400 mm (all still valid 50-mm
            // lattice points). This prevents a smooth curve from creating a new XC every 50 mm.
            var edgeStep = FloorMultiple(maxEdge, step);
            if (edgeStep <= Tol) edgeStep = step;

            var rows = (existing ?? Array.Empty<double>())
                .Where(y => y > a + Tol && y < b - Tol)
                .OrderBy(y => y)
                .ToList();

            if (rows.Count == 0)
            {
                var first = FindBottomEdgeRow(a, b, origin, edgeStep, minEdge, maxEdge);
                if (!first.HasValue) return additions;
                rows.Add(first.Value);
                AddUnique(additions, first.Value);
            }

            while (rows.Count > 0 && rows[0] - a > maxEdge + Tol)
            {
                var y = FindBottomEdgeRow(a, rows[0], origin, edgeStep, minEdge, maxEdge);
                if (!y.HasValue || y.Value >= rows[0] - Tol) break;
                rows.Add(y.Value);
                rows.Sort();
                AddUnique(additions, y.Value);
            }

            var maxStep = FloorMultiple(maxSpacing, step);
            if (maxStep <= Tol) maxStep = step;

            var index = 0;
            while (index + 1 < rows.Count)
            {
                var left = rows[index];
                var right = rows[index + 1];
                if (right - left > maxSpacing + Tol)
                {
                    var y = left + maxStep;
                    if (y <= left + Tol || y >= right - Tol)
                        y = SnapAtOrBelow(right - Tol, origin, step);

                    if (y > left + Tol && y < right - Tol)
                    {
                        rows.Add(y);
                        rows.Sort();
                        AddUnique(additions, y);
                        continue;
                    }
                }
                index++;
            }

            while (rows.Count > 0 && b - rows[rows.Count - 1] > maxEdge + Tol)
            {
                var last = rows[rows.Count - 1];
                var y = FindTopEdgeRow(last, b, origin, edgeStep, minEdge, maxEdge);
                if (!y.HasValue || y.Value <= last + Tol) break;
                rows.Add(y.Value);
                rows.Sort();
                AddUnique(additions, y.Value);
            }

            return additions.OrderBy(y => y);
        }

        private static double? FindBottomEdgeRow(
            double edge,
            double limit,
            double origin,
            double step,
            double minEdge,
            double maxEdge)
        {
            var low = edge + Tol;
            var high = Math.Min(limit - Tol, edge + maxEdge);
            if (high < low) return null;

            var preferred = Math.Max(low, edge + Math.Max(0.0, minEdge));
            var y = SnapAtOrAbove(preferred, origin, step);
            if (y >= low - Tol && y <= high + Tol) return y;

            y = SnapAtOrBelow(high, origin, step);
            return y >= low - Tol && y <= high + Tol ? y : (double?)null;
        }

        private static double? FindTopEdgeRow(
            double limit,
            double edge,
            double origin,
            double step,
            double minEdge,
            double maxEdge)
        {
            var low = Math.Max(limit + Tol, edge - maxEdge);
            var high = edge - Tol;
            if (high < low) return null;

            var preferred = Math.Min(high, edge - Math.Max(0.0, minEdge));
            var y = SnapAtOrBelow(preferred, origin, step);
            if (y >= low - Tol && y <= high + Tol) return y;

            y = SnapAtOrAbove(low, origin, step);
            return y >= low - Tol && y <= high + Tol ? y : (double?)null;
        }

        private static bool AddLocalMain(
            VxtPreviewPlan plan,
            IReadOnlyList<Point2> polygon,
            double radians,
            LocalSpec spec,
            VxtSettings settings,
            IReadOnlyList<Box2> obstacles)
        {
            var minLength = Math.Max(MinDrawLength, settings.MinLocalMainLength);
            var added = false;

            foreach (var raw in PolygonScanline.ClipHorizontal(polygon, spec.Y))
            {
                var rawA = Math.Min(raw.A.X, raw.B.X);
                var rawB = Math.Max(raw.A.X, raw.B.X);
                var a = Math.Max(rawA, spec.X1);
                var b = Math.Min(rawB, spec.X2);
                if (b - a <= Tol) continue;

                // The required curved-edge band often lies at one end of the horizontal chord.
                // Expanding symmetrically would push half of MinLocalMainLength outside the ceiling
                // and incorrectly reject a constructible local XC. Expand inside the real chord:
                // preserve the required overlap first, then shift any missing length inward.
                if (b - a < minLength - Tol)
                {
                    var center = (a + b) * 0.5;
                    var expandedA = center - minLength * 0.5;
                    var expandedB = center + minLength * 0.5;

                    if (expandedA < rawA)
                    {
                        expandedB += rawA - expandedA;
                        expandedA = rawA;
                    }
                    if (expandedB > rawB)
                    {
                        expandedA -= expandedB - rawB;
                        expandedB = rawB;
                    }

                    a = Math.Max(rawA, expandedA);
                    b = Math.Min(rawB, expandedB);
                }

                if (b - a < minLength - Tol) continue;

                var local = new Segment2(new Point2(a, spec.Y), new Point2(b, spec.Y));
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
                mode,
                minEdgeTolerance: settings.HangerEdgeTolerance);
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
                var localPoint = new Point2(x, main.A.Y);
                var world = Transform2.ToWorld(localPoint, radians);
                if (plan.HangerPoints.Any(p => p.DistanceTo(world) <= 0.01)) continue;

                plan.HangerPoints.Add(world);
                plan.HangerCount++;
                const double half = 45.0;
                plan.Lines.Add(new PreviewLine(
                    Transform2.ToWorld(new Point2(x - half, main.A.Y), radians),
                    Transform2.ToWorld(new Point2(x + half, main.A.Y), radians),
                    PreviewLineKind.Hanger));
                plan.Lines.Add(new PreviewLine(
                    Transform2.ToWorld(new Point2(x, main.A.Y - half), radians),
                    Transform2.ToWorld(new Point2(x, main.A.Y + half), radians),
                    PreviewLineKind.Hanger));
            }
        }

        private static bool MainAlreadyCovers(VxtPreviewPlan plan, Segment2 local, double radians)
        {
            foreach (var line in plan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var a = Transform2.ToLocal(line.A, radians);
                var b = Transform2.ToLocal(line.B, radians);
                if (Math.Abs(((a.Y + b.Y) * 0.5) - local.A.Y) > Tol) continue;

                var x1 = Math.Min(a.X, b.X);
                var x2 = Math.Max(a.X, b.X);
                if (x1 <= local.A.X + Tol && x2 >= local.B.X - Tol)
                    return true;
            }
            return false;
        }

        private static List<MainRecord> BuildMainRecords(VxtPreviewPlan plan, double radians)
        {
            var result = new List<MainRecord>();
            foreach (var line in plan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var a = Transform2.ToLocal(line.A, radians);
                var b = Transform2.ToLocal(line.B, radians);
                if (Math.Abs(a.Y - b.Y) > Tol) continue;
                result.Add(new MainRecord(
                    (a.Y + b.Y) * 0.5,
                    Math.Min(a.X, b.X),
                    Math.Max(a.X, b.X)));
            }
            return result;
        }

        private static bool IsOrthogonalPolygon(IReadOnlyList<Point2> polygon)
        {
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var dx = Math.Abs(b.X - a.X);
                var dy = Math.Abs(b.Y - a.Y);
                if (dx <= Tol || dy <= Tol) continue;
                return false;
            }
            return true;
        }

        private static bool IsConvexPolygon(IReadOnlyList<Point2> polygon)
        {
            var sign = 0;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var c = polygon[(i + 2) % polygon.Count];
                var cross =
                    (b.X - a.X) * (c.Y - b.Y) -
                    (b.Y - a.Y) * (c.X - b.X);
                if (Math.Abs(cross) <= Tol) continue;

                var current = cross > 0.0 ? 1 : -1;
                if (sign == 0) sign = current;
                else if (sign != current) return false;
            }
            return sign != 0;
        }

        private static IReadOnlyList<Box2> TransformMainObstacles(
            VxtLayoutContext context,
            double radians,
            double clearance)
        {
            return VxtHoleObstacleFilter.KeepDrawable(
                    (context.GeneralObstacles ?? new List<Box2>())
                        .Concat(context.MainObstacles ?? new List<Box2>()),
                    context.BoundaryHoles,
                    clearance)
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

        private static bool CrossesX(MainRecord main, double x)
            => x >= main.X1 - Tol && x <= main.X2 + Tol;

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
            var result = new List<LocalSpec>();
            foreach (var spec in source.OrderBy(x => x.Y).ThenBy(x => x.X1))
                AddOrMergeSpec(result, spec.Y, spec.X1, spec.X2);
            return result;
        }

        private static List<double> UniqueSort(IEnumerable<double> values, double tolerance)
        {
            var result = new List<double>();
            foreach (var value in values.OrderBy(x => x))
                if (result.Count == 0 || Math.Abs(result[result.Count - 1] - value) > tolerance)
                    result.Add(value);
            return result;
        }

        private static void AddUnique(List<double> values, double value)
        {
            if (!values.Any(x => Math.Abs(x - value) <= Tol))
                values.Add(value);
        }

        private static double FloorMultiple(double value, double step)
            => step <= Tol ? value : Math.Floor((value + 1e-9) / step) * step;

        private static double SnapAtOrBelow(double value, double origin, double step)
            => step <= Tol ? value : origin + Math.Floor(((value - origin) + 1e-9) / step) * step;

        private static double SnapAtOrAbove(double value, double origin, double step)
            => step <= Tol ? value : origin + Math.Ceiling(((value - origin) - 1e-9) / step) * step;

        private static double Normalize180(double value)
        {
            value %= 180.0;
            return value < 0.0 ? value + 180.0 : value;
        }

        private sealed class MainRecord
        {
            public MainRecord(double y, double x1, double x2)
            {
                Y = y;
                X1 = x1;
                X2 = x2;
            }

            public double Y { get; }
            public double X1 { get; }
            public double X2 { get; }
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
