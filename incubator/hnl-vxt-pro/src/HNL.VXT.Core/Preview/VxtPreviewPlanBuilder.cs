using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Single source of geometry for both transient Preview and permanent Create.
    /// Main direction semantics follow V6.7.4: 0°/Ngang = horizontal XC; XP is perpendicular.
    /// </summary>
    public sealed class VxtPreviewPlanBuilder
    {
        private const double Eps = 1e-7;

        public VxtPreviewPlan Build(Boundary2 boundary, VxtSettings settings)
            => Build(boundary, settings, null);

        public VxtPreviewPlan Build(Boundary2 boundary, VxtSettings settings, VxtLayoutContext context)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.IsValid(out var error)) throw new ArgumentException(error, nameof(settings));

            context = context ?? new VxtLayoutContext();

            if (settings.MainDirection == MainDirectionMode.RectangleRegions && context.HasManualRegions)
                return BuildRegions(boundary, settings, context);

            if (settings.MainDirection == MainDirectionMode.Auto)
            {
                var a = BuildAtAngle(boundary, settings, context, 0.0, null, context.GlobalFurringFromFarEdge, includeGuides: true);
                var b = BuildAtAngle(boundary, settings, context, 90.0, null, context.GlobalFurringFromFarEdge, includeGuides: true);
                return AutoScore(a) <= AutoScore(b) ? a : b;
            }

            var angle = ResolveAngle(settings);
            return BuildAtAngle(boundary, settings, context, angle, null, context.GlobalFurringFromFarEdge, includeGuides: true);
        }

        private VxtPreviewPlan BuildRegions(Boundary2 boundary, VxtSettings settings, VxtLayoutContext context)
        {
            var result = new VxtPreviewPlan();
            var seenLines = new HashSet<string>(StringComparer.Ordinal);
            var seenHangers = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < context.Regions.Count; i++)
            {
                var region = context.Regions[i];
                var partial = BuildAtAngle(
                    boundary,
                    settings,
                    context,
                    region.MainAngleDegrees,
                    region.WorldBounds,
                    region.FurringFromFarEdge,
                    includeGuides: i == 0);
                Merge(result, partial, seenLines, seenHangers);
            }

            var dims = result.Dimensions
                .GroupBy(DimensionKey, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();
            result.Dimensions.Clear();
            result.Dimensions.AddRange(dims);
            result.DimensionSegmentCount = result.Dimensions.Count;
            return result;
        }

        private VxtPreviewPlan BuildAtAngle(
            Boundary2 boundary,
            VxtSettings settings,
            VxtLayoutContext context,
            double angleDegrees,
            Box2? regionWorld,
            bool furringFromFarEdge,
            bool includeGuides)
        {
            var plan = new VxtPreviewPlan();
            var radians = angleDegrees * Math.PI / 180.0;
            var localPolygon = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
            var polygonBounds = Box2.FromPoints(localPolygon);

            Box2 domain;
            if (regionWorld.HasValue)
            {
                if (!TryIntersect(polygonBounds, TransformBox(regionWorld.Value, radians), out domain))
                    return plan;
            }
            else
            {
                domain = polygonBounds;
            }

            if (domain.Width <= Eps || domain.Height <= Eps) return plan;

            if (includeGuides)
            {
                AddBoundary(plan, boundary);
                var c = boundary.GetBounds();
                var center = new Point2((c.Min.X + c.Max.X) * 0.5, (c.Min.Y + c.Max.Y) * 0.5);
                var guideLength = Math.Max(800.0, center.DistanceTo(c.Max) * 0.35);
                var end = new Point2(center.X + Math.Cos(radians) * guideLength, center.Y + Math.Sin(radians) * guideLength);
                plan.Lines.Add(new PreviewLine(center, end, PreviewLineKind.Direction));
            }

            var mainObstacles = TransformObstacles(context.GeneralObstacles.Concat(context.MainObstacles), radians, settings.ClearanceDistance, domain);
            var furringObstacles = TransformObstacles(context.GeneralObstacles.Concat(context.FurringObstacles), radians, settings.ClearanceDistance, domain);

            var mainSegmentsLocal = new List<Segment2>();
            var mainCoords = new List<double>();
            var skipMainRegion = settings.MainSkipLimit > 0.0 &&
                                 (domain.Width <= settings.MainSkipLimit + Eps || domain.Height <= settings.MainSkipLimit + Eps);

            if (settings.DrawMain && !skipMainRegion)
            {
                var layoutMode = settings.MainLayout == MainLayoutMode.Auto ? MainLayoutMode.BalancedTwoEnds : settings.MainLayout;
                var layout = SmartLayout1D.Calculate(
                    domain.Height,
                    settings.MainMaxSpacing,
                    settings.MainMinSpacing,
                    settings.MainMaxEdgeOffset,
                    settings.MainMinEdgeOffset,
                    settings.MainBalanceStep,
                    layoutMode,
                    reverse: false);

                var startOffset = layout?.StartOffset ?? domain.Height * 0.5;
                if (settings.UseAvoidance && settings.ShiftAllForAvoidance && layout != null && mainObstacles.Count > 0)
                {
                    var optimized = SmartLayout1D.OptimizeOffset(
                        layout,
                        domain.MinY,
                        settings.MainMinEdgeOffset,
                        settings.MainMaxEdgeOffset,
                        settings.MainBalanceStep,
                        y => IsHorizontalGridCoordinateClear(y, mainObstacles));
                    if (!double.IsNaN(optimized)) startOffset = optimized;
                }

                var coordinates = Positions(domain.MinY, layout, startOffset);
                var previous = double.NaN;
                foreach (var idealY in coordinates)
                {
                    var y = idealY;
                    if (settings.UseAvoidance && mainObstacles.Count > 0 && !IsHorizontalGridCoordinateClear(y, mainObstacles))
                    {
                        y = ResolveHorizontalCoordinate(
                            y,
                            previous,
                            domain,
                            mainObstacles,
                            settings.MainMinSpacing,
                            settings.MainMaxSpacing,
                            settings.MainBalanceStep);
                        if (double.IsNaN(y)) continue;
                    }

                    var drewRow = false;
                    foreach (var raw in PolygonScanline.ClipHorizontal(localPolygon, y))
                    {
                        if (!TryTrimHorizontal(raw, domain, out var seg)) continue;
                        if (settings.UseAvoidance && SegmentHitsHorizontalObstacles(seg, mainObstacles)) continue;

                        mainSegmentsLocal.Add(seg);
                        AddWorldLine(plan, seg, radians, PreviewLineKind.Main);
                        plan.MainSegmentCount++;
                        drewRow = true;
                    }

                    if (drewRow)
                    {
                        mainCoords.Add(y);
                        previous = y;
                    }
                }
            }

            var furringCoords = new List<double>();
            if (settings.DrawFurring)
            {
                var coords = FixedEdgePositions(domain.MinX, domain.MaxX, settings.FurringSpacing, furringFromFarEdge);
                var adjusted = settings.UseAvoidance && settings.ShiftAllForAvoidance && furringObstacles.Count > 0
                    ? OptimizeFixedGrid(coords, domain.MinX, domain.MaxX, settings.FurringSpacing, furringObstacles, vertical: true)
                    : coords;

                foreach (var idealX in adjusted)
                {
                    var x = idealX;
                    if (settings.UseAvoidance && furringObstacles.Count > 0 && !IsVerticalGridCoordinateClear(x, furringObstacles))
                    {
                        x = ResolveVerticalCoordinate(x, domain, furringObstacles, Math.Min(50.0, Math.Max(10.0, settings.FurringSpacing / 8.0)));
                        if (double.IsNaN(x)) continue;
                    }

                    var drewColumn = false;
                    foreach (var raw in PolygonScanline.ClipVertical(localPolygon, x))
                    {
                        if (!TryTrimVertical(raw, domain, out var seg)) continue;
                        if (settings.UseAvoidance && SegmentHitsVerticalObstacles(seg, furringObstacles)) continue;
                        AddWorldLine(plan, seg, radians, PreviewLineKind.Furring);
                        plan.FurringSegmentCount++;
                        drewColumn = true;
                    }
                    if (drewColumn) furringCoords.Add(x);
                }
            }

            var hangerRows = new List<List<Point2>>();
            if (settings.DrawHangers && mainSegmentsLocal.Count > 0)
            {
                foreach (var main in mainSegmentsLocal)
                {
                    var minX = Math.Min(main.A.X, main.B.X);
                    var maxX = Math.Max(main.A.X, main.B.X);
                    var length = maxX - minX;
                    if (length <= Eps) continue;

                    var oneSide = settings.HangerLayout == HangerLayoutMode.OneSideFollowFurring;
                    var layout = SmartLayout1D.Calculate(
                        length,
                        settings.HangerMaxSpacing,
                        settings.HangerMinSpacing,
                        settings.HangerMaxEdgeOffset,
                        settings.HangerMinEdgeOffset,
                        settings.HangerBalanceStep,
                        oneSide ? MainLayoutMode.OneSide : MainLayoutMode.BalancedTwoEnds,
                        reverse: oneSide && furringFromFarEdge);
                    if (layout == null) continue;

                    var rowObstacles = mainObstacles
                        .Where(b => main.A.Y > b.MinY + Eps && main.A.Y < b.MaxY - Eps && b.MaxX >= minX && b.MinX <= maxX)
                        .ToList();
                    var startOffset = layout.StartOffset;
                    if (settings.UseAvoidance && settings.ShiftAllForAvoidance && rowObstacles.Count > 0)
                    {
                        var optimized = SmartLayout1D.OptimizeOffset(
                            layout,
                            minX,
                            settings.HangerMinEdgeOffset,
                            settings.HangerMaxEdgeOffset,
                            settings.HangerBalanceStep,
                            x => !rowObstacles.Any(b => x > b.MinX + Eps && x < b.MaxX - Eps));
                        if (!double.IsNaN(optimized)) startOffset = optimized;
                    }

                    var row = new List<Point2>();
                    foreach (var idealX in Positions(minX, layout, startOffset))
                    {
                        var x = idealX;
                        if (settings.UseAvoidance && rowObstacles.Any(b => x > b.MinX + Eps && x < b.MaxX - Eps))
                        {
                            x = ResolvePointCoordinate(
                                x,
                                minX,
                                maxX,
                                settings.HangerMinEdgeOffset,
                                settings.HangerBalanceStep,
                                v => !rowObstacles.Any(b => v > b.MinX + Eps && v < b.MaxX - Eps));
                            if (double.IsNaN(x)) continue;
                        }

                        var localPoint = new Point2(x, main.A.Y);
                        var world = Transform2.ToWorld(localPoint, radians);
                        if (plan.HangerPoints.Any(p => p.DistanceTo(world) < 0.01)) continue;
                        plan.HangerPoints.Add(world);
                        row.Add(localPoint);
                        AddHangerCross(plan, localPoint, radians);
                        plan.HangerCount++;
                    }
                    if (row.Count > 1) hangerRows.Add(row.OrderBy(p => p.X).ToList());
                }
            }

            if (settings.AutoDimension)
                AddDimensions(plan, settings, radians, domain, mainCoords, furringCoords, hangerRows);

            plan.DimensionSegmentCount = plan.Dimensions.Count;
            return plan;
        }

        private static double ResolveAngle(VxtSettings settings)
        {
            switch (settings.MainDirection)
            {
                case MainDirectionMode.Vertical: return 90.0;
                case MainDirectionMode.TwoPoints: return NormalizeDegrees(settings.DirectionDegrees);
                case MainDirectionMode.RectangleRegions: return NormalizeDegrees(settings.DirectionDegrees);
                default: return 0.0;
            }
        }

        private static int AutoScore(VxtPreviewPlan plan)
            => plan.MainSegmentCount * 100000 + plan.FurringSegmentCount;

        private static List<Box2> TransformObstacles(IEnumerable<Box2> boxes, double radians, double clearance, Box2 domain)
        {
            var result = new List<Box2>();
            foreach (var b in boxes ?? Enumerable.Empty<Box2>())
            {
                var transformed = TransformBox(b, radians).Expand(clearance);
                if (transformed.Intersects(domain)) result.Add(transformed);
            }
            return result;
        }

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
            if (maxX <= minX + Eps || maxY <= minY + Eps)
            {
                result = default(Box2);
                return false;
            }
            result = new Box2(minX, minY, maxX, maxY);
            return true;
        }

        private static IReadOnlyList<double> Positions(double min, SmartLayout1D.Result layout, double startOffset)
        {
            if (layout == null) return new[] { min + startOffset };
            var values = new List<double>(layout.PointCount);
            var value = min + startOffset;
            values.Add(value);
            for (var i = 0; i < layout.Steps.Count; i++)
            {
                value += layout.Steps[i];
                values.Add(value);
            }
            return values;
        }

        private static List<double> FixedEdgePositions(double min, double max, double spacing, bool fromFarEdge)
        {
            var length = max - min;
            var result = new List<double>();
            if (length <= Eps || spacing <= Eps) return result;

            var offset = fromFarEdge ? PositiveRemainder(length, spacing) : spacing;
            if (offset <= Eps) offset = spacing;
            for (var value = min + offset; value < max - Eps; value += spacing)
                result.Add(value);
            return result;
        }

        private static double PositiveRemainder(double value, double divisor)
        {
            var result = value % divisor;
            if (result < 0.0) result += divisor;
            return result;
        }

        private static List<double> OptimizeFixedGrid(List<double> baseGrid, double min, double max, double spacing, List<Box2> obstacles, bool vertical)
        {
            if (baseGrid.Count == 0 || obstacles.Count == 0) return baseGrid;
            Func<IEnumerable<double>, bool> clear = values => values.All(v => vertical
                ? IsVerticalGridCoordinateClear(v, obstacles)
                : IsHorizontalGridCoordinateClear(v, obstacles));
            if (clear(baseGrid)) return baseGrid;

            var step = Math.Min(50.0, Math.Max(10.0, spacing / 8.0));
            for (var k = 1; k <= 100; k++)
            {
                foreach (var sign in new[] { 1.0, -1.0 })
                {
                    var delta = sign * k * step;
                    var shifted = baseGrid.Select(v => v + delta).ToList();
                    if (shifted[0] <= min + Eps || shifted[shifted.Count - 1] >= max - Eps) continue;
                    if (clear(shifted)) return shifted;
                }
            }
            return baseGrid;
        }

        private static bool IsHorizontalGridCoordinateClear(double y, List<Box2> obstacles)
            => obstacles.All(b => y <= b.MinY + Eps || y >= b.MaxY - Eps);

        private static bool IsVerticalGridCoordinateClear(double x, List<Box2> obstacles)
            => obstacles.All(b => x <= b.MinX + Eps || x >= b.MaxX - Eps);

        private static bool SegmentHitsHorizontalObstacles(Segment2 s, List<Box2> obstacles)
            => obstacles.Any(b => b.IntersectsHorizontal(s.A.Y, s.A.X, s.B.X));

        private static bool SegmentHitsVerticalObstacles(Segment2 s, List<Box2> obstacles)
            => obstacles.Any(b => b.IntersectsVertical(s.A.X, s.A.Y, s.B.Y));

        private static double ResolveHorizontalCoordinate(double ideal, double previous, Box2 domain, List<Box2> obstacles, double minSpacing, double maxSpacing, double step)
        {
            for (var k = 1; k <= 100; k++)
            {
                foreach (var sign in new[] { 1.0, -1.0 })
                {
                    var v = ideal + sign * k * step;
                    if (v <= domain.MinY + Eps || v >= domain.MaxY - Eps) continue;
                    if (!double.IsNaN(previous))
                    {
                        var d = Math.Abs(v - previous);
                        if (d < minSpacing - Eps || d > maxSpacing + Eps) continue;
                    }
                    if (IsHorizontalGridCoordinateClear(v, obstacles)) return v;
                }
            }
            return double.NaN;
        }

        private static double ResolveVerticalCoordinate(double ideal, Box2 domain, List<Box2> obstacles, double step)
        {
            for (var k = 1; k <= 100; k++)
            {
                foreach (var sign in new[] { 1.0, -1.0 })
                {
                    var v = ideal + sign * k * step;
                    if (v <= domain.MinX + Eps || v >= domain.MaxX - Eps) continue;
                    if (IsVerticalGridCoordinateClear(v, obstacles)) return v;
                }
            }
            return double.NaN;
        }

        private static double ResolvePointCoordinate(double ideal, double min, double max, double minEdge, double step, Func<double, bool> clear)
        {
            for (var k = 1; k <= 100; k++)
            {
                foreach (var sign in new[] { 1.0, -1.0 })
                {
                    var v = ideal + sign * k * step;
                    if (v < min + minEdge - Eps || v > max - minEdge + Eps) continue;
                    if (clear(v)) return v;
                }
            }
            return double.NaN;
        }

        private static bool TryTrimHorizontal(Segment2 raw, Box2 domain, out Segment2 result)
        {
            var a = Math.Max(Math.Min(raw.A.X, raw.B.X), domain.MinX);
            var b = Math.Min(Math.Max(raw.A.X, raw.B.X), domain.MaxX);
            result = new Segment2(new Point2(a, raw.A.Y), new Point2(b, raw.A.Y));
            return b - a > Eps && raw.A.Y >= domain.MinY - Eps && raw.A.Y <= domain.MaxY + Eps;
        }

        private static bool TryTrimVertical(Segment2 raw, Box2 domain, out Segment2 result)
        {
            var a = Math.Max(Math.Min(raw.A.Y, raw.B.Y), domain.MinY);
            var b = Math.Min(Math.Max(raw.A.Y, raw.B.Y), domain.MaxY);
            result = new Segment2(new Point2(raw.A.X, a), new Point2(raw.A.X, b));
            return b - a > Eps && raw.A.X >= domain.MinX - Eps && raw.A.X <= domain.MaxX + Eps;
        }

        private static void AddWorldLine(VxtPreviewPlan plan, Segment2 local, double radians, PreviewLineKind kind)
            => plan.Lines.Add(new PreviewLine(Transform2.ToWorld(local.A, radians), Transform2.ToWorld(local.B, radians), kind));

        private static void AddHangerCross(VxtPreviewPlan plan, Point2 local, double radians)
        {
            const double half = 45.0;
            AddWorldLine(plan, new Segment2(new Point2(local.X - half, local.Y), new Point2(local.X + half, local.Y)), radians, PreviewLineKind.Hanger);
            AddWorldLine(plan, new Segment2(new Point2(local.X, local.Y - half), new Point2(local.X, local.Y + half)), radians, PreviewLineKind.Hanger);
        }

        private static void AddBoundary(VxtPreviewPlan plan, Boundary2 boundary)
        {
            for (var i = 0; i < boundary.Vertices.Count; i++)
            {
                var a = boundary.Vertices[i];
                var b = boundary.Vertices[(i + 1) % boundary.Vertices.Count];
                plan.Lines.Add(new PreviewLine(a, b, PreviewLineKind.Boundary));
            }
        }

        private static void AddDimensions(
            VxtPreviewPlan plan,
            VxtSettings settings,
            double radians,
            Box2 domain,
            List<double> mainCoords,
            List<double> furringCoords,
            List<List<Point2>> hangerRows)
        {
            var stack = new Dictionary<string, int>(StringComparer.Ordinal);

            if (settings.DimMain)
                AddVerticalChain(plan, mainCoords, settings.MainDimPosition, DimensionTarget.Main, radians, domain, settings.DimensionDistance, settings.DimensionSpacing, stack);
            if (settings.DimFurring)
                AddHorizontalChain(plan, furringCoords, settings.FurringDimPosition, DimensionTarget.Furring, radians, domain, settings.DimensionDistance, settings.DimensionSpacing, stack);
            if (settings.DimHanger)
            {
                foreach (var row in hangerRows)
                {
                    var xs = row.Select(p => p.X).Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
                    AddHorizontalChain(plan, xs, settings.HangerDimPosition, DimensionTarget.Hanger, radians, domain, settings.DimensionDistance, settings.DimensionSpacing, stack);
                }
            }
        }

        private static void AddVerticalChain(
            VxtPreviewPlan plan,
            IEnumerable<double> values,
            DimensionPosition position,
            DimensionTarget target,
            double radians,
            Box2 domain,
            double distance,
            double spacing,
            IDictionary<string, int> stack)
        {
            var ys = values.Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
            if (ys.Count < 2) return;

            var side = position == DimensionPosition.Left ? "L" : position == DimensionPosition.Right ? "R" : "C-V";
            var index = GetAndIncrement(stack, side);
            var x = position == DimensionPosition.Left
                ? domain.MinX - distance - index * spacing
                : position == DimensionPosition.Right
                    ? domain.MaxX + distance + index * spacing
                    : (domain.MinX + domain.MaxX) * 0.5 + index * spacing;

            for (var i = 0; i + 1 < ys.Count; i++)
            {
                var e1 = Transform2.ToWorld(new Point2(x, ys[i]), radians);
                var e2 = Transform2.ToWorld(new Point2(x, ys[i + 1]), radians);
                var dimLine = Transform2.ToWorld(new Point2(x, (ys[i] + ys[i + 1]) * 0.5), radians);
                plan.Dimensions.Add(new PreviewDimension(e1, e2, dimLine, radians + Math.PI / 2.0, target));
            }
        }

        private static void AddHorizontalChain(
            VxtPreviewPlan plan,
            IEnumerable<double> values,
            DimensionPosition position,
            DimensionTarget target,
            double radians,
            Box2 domain,
            double distance,
            double spacing,
            IDictionary<string, int> stack)
        {
            var xs = values.Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
            if (xs.Count < 2) return;

            var side = position == DimensionPosition.Bottom ? "B" : position == DimensionPosition.Top ? "T" : "C-H";
            var index = GetAndIncrement(stack, side);
            var y = position == DimensionPosition.Bottom
                ? domain.MinY - distance - index * spacing
                : position == DimensionPosition.Top
                    ? domain.MaxY + distance + index * spacing
                    : (domain.MinY + domain.MaxY) * 0.5 + index * spacing;

            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var e1 = Transform2.ToWorld(new Point2(xs[i], y), radians);
                var e2 = Transform2.ToWorld(new Point2(xs[i + 1], y), radians);
                var dimLine = Transform2.ToWorld(new Point2((xs[i] + xs[i + 1]) * 0.5, y), radians);
                plan.Dimensions.Add(new PreviewDimension(e1, e2, dimLine, radians, target));
            }
        }

        private static int GetAndIncrement(IDictionary<string, int> stack, string key)
        {
            int value;
            if (!stack.TryGetValue(key, out value)) value = 0;
            stack[key] = value + 1;
            return value;
        }

        private static void Merge(VxtPreviewPlan target, VxtPreviewPlan source, HashSet<string> seenLines, HashSet<string> seenHangers)
        {
            foreach (var line in source.Lines)
            {
                var key = LineKey(line);
                if (!seenLines.Add(key)) continue;
                target.Lines.Add(line);
                if (line.Kind == PreviewLineKind.Main) target.MainSegmentCount++;
                if (line.Kind == PreviewLineKind.Furring) target.FurringSegmentCount++;
            }
            foreach (var p in source.HangerPoints)
            {
                var key = PointKey(p);
                if (!seenHangers.Add(key)) continue;
                target.HangerPoints.Add(p);
                target.HangerCount++;
            }
            foreach (var d in source.Dimensions) target.Dimensions.Add(d);
            foreach (var t in source.Texts) target.Texts.Add(t);
        }

        private static string LineKey(PreviewLine line)
        {
            var a = PointKey(line.A);
            var b = PointKey(line.B);
            return string.CompareOrdinal(a, b) <= 0 ? line.Kind + ":" + a + ":" + b : line.Kind + ":" + b + ":" + a;
        }

        private static string DimensionKey(PreviewDimension d)
            => d.Target + ":" + PointKey(d.ExtensionPoint1) + ":" + PointKey(d.ExtensionPoint2) + ":" + PointKey(d.DimensionLinePoint);

        private static string PointKey(Point2 p) => Math.Round(p.X, 3) + "," + Math.Round(p.Y, 3);

        private static double NormalizeDegrees(double value)
        {
            value %= 360.0;
            return value < 0 ? value + 360.0 : value;
        }

        private sealed class DoubleToleranceComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) < 0.01;
            public int GetHashCode(double obj) => Math.Round(obj, 2).GetHashCode();
        }
    }
}
