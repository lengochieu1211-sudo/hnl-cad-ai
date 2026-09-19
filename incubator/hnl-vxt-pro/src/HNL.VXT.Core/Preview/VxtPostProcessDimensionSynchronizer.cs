using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Keeps Auto DIM synchronized with final framing geometry after concave/local XC repair.
    /// Initial builders create dimensions before the concave post-processor; local XC/Ty added
    /// afterwards would otherwise be absent from the dimension chains.
    ///
    /// No spacing/layout solver is changed here. The helper only reconstructs the same DIM chains
    /// from the final XC/XP/Ty geometry. Rectangle-region mode is rebuilt scope-by-scope so each
    /// HCN keeps its own local axis and domain.
    /// </summary>
    public static class VxtPostProcessDimensionSynchronizer
    {
        private const double Tol = 0.1;
        private const double AxisTol = 0.5;

        public static void Synchronize(
            Boundary2 boundary,
            VxtSettings settings,
            VxtPreviewPlan plan,
            double angleDegrees)
            => Synchronize(boundary, settings, null, plan, angleDegrees);

        public static void Synchronize(
            Boundary2 boundary,
            VxtSettings settings,
            VxtLayoutContext context,
            VxtPreviewPlan plan,
            double angleDegrees)
        {
            if (boundary == null || settings == null || plan == null) return;
            if (!settings.AutoDimension) return;
            if (!settings.DimMain && !settings.DimFurring && !settings.DimHanger) return;

            var rebuilt = new List<PreviewDimension>();

            if (settings.MainDirection == MainDirectionMode.RectangleRegions &&
                context != null && context.HasManualRegions)
            {
                foreach (var region in context.Regions)
                {
                    var radians = NormalizeDegrees(region.MainAngleDegrees) * Math.PI / 180.0;
                    var localBoundary = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
                    if (localBoundary.Count < 3) continue;
                    var polygonBounds = Box2.FromPoints(localBoundary);
                    var regionLocal = TransformBox(region.WorldBounds, radians);
                    Box2 domain;
                    if (!TryIntersect(polygonBounds, regionLocal, out domain)) continue;
                    if (domain.Width <= Tol || domain.Height <= Tol) continue;
                    AddScopeDimensions(plan, settings, radians, domain, rebuilt);
                }

                rebuilt = rebuilt
                    .GroupBy(DimensionKey, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();
            }
            else
            {
                var radians = NormalizeDegrees(angleDegrees) * Math.PI / 180.0;
                var localBoundary = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
                if (localBoundary.Count < 3) return;
                var domain = Box2.FromPoints(localBoundary);
                if (domain.Width <= Tol || domain.Height <= Tol) return;
                AddScopeDimensions(plan, settings, radians, domain, rebuilt);
            }

            if (settings.OptimizationMode != VxtOptimizationMode.Legacy && rebuilt.Count > 0)
                rebuilt = VxtProDimensionPacker.Pack(rebuilt, settings.DimensionSpacing).Dimensions.ToList();

            if (SameDimensions(plan.Dimensions, rebuilt))
            {
                plan.DimensionSegmentCount = plan.Dimensions.Count;
                return;
            }

            plan.Dimensions.Clear();
            plan.Dimensions.AddRange(rebuilt);
            plan.DimensionSegmentCount = rebuilt.Count;
        }

        private static void AddScopeDimensions(
            VxtPreviewPlan plan,
            VxtSettings settings,
            double radians,
            Box2 domain,
            ICollection<PreviewDimension> target)
        {
            var mainSegments = FinalSegments(plan, PreviewLineKind.Main, radians, horizontal: true, domain);
            var furringSegments = FinalSegments(plan, PreviewLineKind.Furring, radians, horizontal: false, domain);
            var mainCoords = DistinctSorted(mainSegments.Select(s => (s.A.Y + s.B.Y) * 0.5));
            var furringCoords = DistinctSorted(furringSegments.Select(s => (s.A.X + s.B.X) * 0.5));
            var hangerRows = BuildFinalHangerRows(plan, mainSegments, radians, domain);
            var stack = new Dictionary<string, int>(StringComparer.Ordinal);

            if (settings.DimMain && mainCoords.Count > 0)
                AddVerticalChain(target, WithBounds(mainCoords, domain.MinY, domain.MaxY),
                    settings.MainDimPosition, DimensionTarget.Main, radians, domain,
                    settings.DimensionDistance, settings.DimensionSpacing, stack);

            if (settings.DimFurring && furringCoords.Count > 0)
                AddHorizontalChain(target, WithBounds(furringCoords, domain.MinX, domain.MaxX),
                    settings.FurringDimPosition, DimensionTarget.Furring, radians, domain,
                    settings.DimensionDistance, settings.DimensionSpacing, stack);

            if (settings.DimHanger)
            {
                var uniquePatterns = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in hangerRows)
                {
                    var xs = DistinctSorted(row.Select(p => p.X));
                    if (xs.Count == 0) continue;
                    if (!uniquePatterns.Add(PatternKey(xs))) continue;
                    AddHorizontalChain(target, WithBounds(xs, domain.MinX, domain.MaxX),
                        settings.HangerDimPosition, DimensionTarget.Hanger, radians, domain,
                        settings.DimensionDistance, settings.DimensionSpacing, stack, row[0].Y);
                }
            }
        }

        private static List<Segment2> FinalSegments(
            VxtPreviewPlan plan,
            PreviewLineKind kind,
            double radians,
            bool horizontal,
            Box2 domain)
        {
            var result = new List<Segment2>();
            foreach (var line in plan.Lines.Where(x => x.Kind == kind))
            {
                var a = Transform2.ToLocal(line.A, radians);
                var b = Transform2.ToLocal(line.B, radians);
                if (horizontal)
                {
                    if (Math.Abs(a.Y - b.Y) > AxisTol) continue;
                    var y = (a.Y + b.Y) * 0.5;
                    if (y < domain.MinY - AxisTol || y > domain.MaxY + AxisTol) continue;
                    if (Math.Max(a.X, b.X) < domain.MinX + AxisTol ||
                        Math.Min(a.X, b.X) > domain.MaxX - AxisTol) continue;
                }
                else
                {
                    if (Math.Abs(a.X - b.X) > AxisTol) continue;
                    var x = (a.X + b.X) * 0.5;
                    if (x < domain.MinX - AxisTol || x > domain.MaxX + AxisTol) continue;
                    if (Math.Max(a.Y, b.Y) < domain.MinY + AxisTol ||
                        Math.Min(a.Y, b.Y) > domain.MaxY - AxisTol) continue;
                }
                result.Add(new Segment2(a, b));
            }
            return result;
        }

        private static List<List<Point2>> BuildFinalHangerRows(
            VxtPreviewPlan plan,
            IReadOnlyList<Segment2> mainSegments,
            double radians,
            Box2 domain)
        {
            var localHangers = plan.HangerPoints
                .Select(p => Transform2.ToLocal(p, radians))
                .Where(p => p.X >= domain.MinX - AxisTol && p.X <= domain.MaxX + AxisTol &&
                            p.Y >= domain.MinY - AxisTol && p.Y <= domain.MaxY + AxisTol)
                .ToList();
            var rows = new List<List<Point2>>();

            foreach (var main in mainSegments)
            {
                var y = (main.A.Y + main.B.Y) * 0.5;
                var minX = Math.Max(domain.MinX, Math.Min(main.A.X, main.B.X)) - AxisTol;
                var maxX = Math.Min(domain.MaxX, Math.Max(main.A.X, main.B.X)) + AxisTol;
                var row = localHangers
                    .Where(p => Math.Abs(p.Y - y) <= AxisTol && p.X >= minX && p.X <= maxX)
                    .OrderBy(p => p.X)
                    .ToList();
                row = DistinctPointsByX(row);
                if (row.Count > 1) rows.Add(row);
            }
            return rows;
        }

        private static List<Point2> DistinctPointsByX(IEnumerable<Point2> source)
        {
            var result = new List<Point2>();
            foreach (var p in source.OrderBy(x => x.X))
            {
                if (result.Count == 0 || Math.Abs(result[result.Count - 1].X - p.X) > Tol)
                    result.Add(p);
            }
            return result;
        }

        private static List<double> DistinctSorted(IEnumerable<double> source)
        {
            var result = new List<double>();
            foreach (var value in source.OrderBy(x => x))
            {
                if (result.Count == 0 || Math.Abs(result[result.Count - 1] - value) > Tol)
                    result.Add(value);
            }
            return result;
        }

        private static IEnumerable<double> WithBounds(IEnumerable<double> values, double min, double max)
        {
            yield return min;
            foreach (var value in values) yield return value;
            yield return max;
        }

        private static string PatternKey(IEnumerable<double> values)
            => string.Join("|", values.Select(v => Math.Round(v, 2).ToString("0.00", CultureInfo.InvariantCulture)));

        private static void AddVerticalChain(
            ICollection<PreviewDimension> target,
            IEnumerable<double> values,
            DimensionPosition position,
            DimensionTarget dimensionTarget,
            double radians,
            Box2 domain,
            double distance,
            double spacing,
            IDictionary<string, int> stack,
            double? sourceBaseCoordinate = null)
        {
            var ys = DistinctSorted(values);
            if (ys.Count < 2) return;

            string side;
            double baseX;
            double textX;
            if (position == DimensionPosition.Auto)
            {
                side = "C-V";
                var index = GetAndIncrement(stack, side);
                baseX = (domain.MinX + domain.MaxX) * 0.5;
                textX = baseX + index * spacing;
            }
            else if (position == DimensionPosition.Left || position == DimensionPosition.Bottom)
            {
                side = "L";
                var index = GetAndIncrement(stack, side);
                baseX = domain.MinX;
                textX = domain.MinX - distance - index * spacing;
            }
            else
            {
                side = "R";
                var index = GetAndIncrement(stack, side);
                baseX = domain.MaxX;
                textX = domain.MaxX + distance + index * spacing;
            }

            if (sourceBaseCoordinate.HasValue) baseX = sourceBaseCoordinate.Value;

            for (var i = 0; i + 1 < ys.Count; i++)
            {
                if (Math.Abs(ys[i + 1] - ys[i]) <= 1.0) continue;
                var e1 = Transform2.ToWorld(new Point2(baseX, ys[i]), radians);
                var e2 = Transform2.ToWorld(new Point2(baseX, ys[i + 1]), radians);
                var dimLine = Transform2.ToWorld(new Point2(textX, (ys[i] + ys[i + 1]) * 0.5), radians);
                target.Add(new PreviewDimension(e1, e2, dimLine, radians + Math.PI / 2.0, dimensionTarget));
            }
        }

        private static void AddHorizontalChain(
            ICollection<PreviewDimension> target,
            IEnumerable<double> values,
            DimensionPosition position,
            DimensionTarget dimensionTarget,
            double radians,
            Box2 domain,
            double distance,
            double spacing,
            IDictionary<string, int> stack,
            double? sourceBaseCoordinate = null)
        {
            var xs = DistinctSorted(values);
            if (xs.Count < 2) return;

            string side;
            double baseY;
            double textY;
            if (position == DimensionPosition.Auto)
            {
                side = "C-H";
                var index = GetAndIncrement(stack, side);
                baseY = (domain.MinY + domain.MaxY) * 0.5;
                textY = baseY + index * spacing;
            }
            else if (position == DimensionPosition.Top || position == DimensionPosition.Left)
            {
                side = "T";
                var index = GetAndIncrement(stack, side);
                baseY = domain.MaxY;
                textY = domain.MaxY + distance + index * spacing;
            }
            else
            {
                side = "B";
                var index = GetAndIncrement(stack, side);
                baseY = domain.MinY;
                textY = domain.MinY - distance - index * spacing;
            }

            if (sourceBaseCoordinate.HasValue) baseY = sourceBaseCoordinate.Value;

            for (var i = 0; i + 1 < xs.Count; i++)
            {
                if (Math.Abs(xs[i + 1] - xs[i]) <= 1.0) continue;
                var e1 = Transform2.ToWorld(new Point2(xs[i], baseY), radians);
                var e2 = Transform2.ToWorld(new Point2(xs[i + 1], baseY), radians);
                var dimLine = Transform2.ToWorld(new Point2((xs[i] + xs[i + 1]) * 0.5, textY), radians);
                target.Add(new PreviewDimension(e1, e2, dimLine, radians, dimensionTarget));
            }
        }

        private static int GetAndIncrement(IDictionary<string, int> stack, string key)
        {
            int value;
            if (!stack.TryGetValue(key, out value)) value = 0;
            stack[key] = value + 1;
            return value;
        }

        private static bool SameDimensions(IReadOnlyList<PreviewDimension> current, IReadOnlyList<PreviewDimension> rebuilt)
        {
            if (current.Count != rebuilt.Count) return false;
            var a = current.Select(DimensionKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var b = rebuilt.Select(DimensionKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            return a.SequenceEqual(b, StringComparer.Ordinal);
        }

        private static string DimensionKey(PreviewDimension d)
        {
            return d.Target + ":" + PointKey(d.ExtensionPoint1) + ":" + PointKey(d.ExtensionPoint2) + ":" +
                   PointKey(d.DimensionLinePoint) + ":" + Math.Round(NormalizeRadians(d.RotationRadians), 4)
                       .ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private static string PointKey(Point2 p)
            => Math.Round(p.X, 2).ToString("0.00", CultureInfo.InvariantCulture) + "," +
               Math.Round(p.Y, 2).ToString("0.00", CultureInfo.InvariantCulture);

        private static Box2 TransformBox(Box2 box, double radians)
        {
            var points = new[]
            {
                new Point2(box.MinX, box.MinY), new Point2(box.MaxX, box.MinY),
                new Point2(box.MaxX, box.MaxY), new Point2(box.MinX, box.MaxY)
            }.Select(p => Transform2.ToLocal(p, radians));
            return Box2.FromPoints(points);
        }

        private static bool TryIntersect(Box2 a, Box2 b, out Box2 result)
        {
            var minX = Math.Max(a.MinX, b.MinX);
            var minY = Math.Max(a.MinY, b.MinY);
            var maxX = Math.Min(a.MaxX, b.MaxX);
            var maxY = Math.Min(a.MaxY, b.MaxY);
            if (maxX <= minX + Tol || maxY <= minY + Tol)
            {
                result = default(Box2);
                return false;
            }
            result = new Box2(minX, minY, maxX, maxY);
            return true;
        }

        private static double NormalizeDegrees(double degrees)
        {
            degrees %= 180.0;
            return degrees < 0.0 ? degrees + 180.0 : degrees;
        }

        private static double NormalizeRadians(double radians)
        {
            radians %= Math.PI;
            return radians < 0.0 ? radians + Math.PI : radians;
        }
    }
}
