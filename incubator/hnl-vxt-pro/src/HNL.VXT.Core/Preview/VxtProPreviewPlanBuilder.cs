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
    /// HNL VXT Pro geometry builder. It intentionally lives beside, not inside,
    /// VxtPreviewPlanBuilder so the certified Legacy V6.7.x path remains untouched.
    /// Preview and Create still consume the same VxtPreviewPlan.
    /// </summary>
    public sealed class VxtProPreviewPlanBuilder
    {
        private const double Eps = 1e-7;
        private const double LegacyMinDrawLength = 5.0;

        public VxtPreviewPlan Build(Boundary2 boundary, VxtSettings settings)
            => Build(boundary, settings, null);

        public VxtPreviewPlan Build(Boundary2 boundary, VxtSettings settings, VxtLayoutContext context)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.IsValid(out var error)) throw new ArgumentException(error, nameof(settings));
            if (settings.OptimizationMode == VxtOptimizationMode.Legacy)
                return new VxtPreviewPlanBuilder().Build(boundary, settings, context);

            context = context ?? new VxtLayoutContext();

            if (settings.MainDirection == MainDirectionMode.RectangleRegions && context.HasManualRegions)
                return BuildRegions(boundary, settings, context);

            if (settings.MainDirection == MainDirectionMode.Auto)
            {
                // Preserve the certified direction-selection rule. Pro optimization starts only
                // after the main angle has been resolved.
                var bounds = boundary.GetBounds();
                var width = bounds.Max.X - bounds.Min.X;
                var height = bounds.Max.Y - bounds.Min.Y;
                var wide = width > height;
                var angle = settings.AutoShadowline
                    ? (wide ? 0.0 : 90.0)
                    : (wide ? 90.0 : 0.0);
                return BuildAtAngle(boundary, settings, context, angle, null, context.GlobalFurringFromFarEdge, includeGuides: true);
            }

            return BuildAtAngle(
                boundary,
                settings,
                context,
                ResolveAngle(settings),
                null,
                context.GlobalFurringFromFarEdge,
                includeGuides: true);
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
            PackDimensions(result, settings);
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

            var mainObstacles = TransformObstacles(
                context.GeneralObstacles.Concat(context.MainObstacles), radians, settings.ClearanceDistance, domain);
            var furringObstacles = TransformObstacles(
                context.GeneralObstacles.Concat(context.FurringObstacles), radians, settings.ClearanceDistance, domain);

            var mainSegmentsLocal = new List<Segment2>();
            var mainCoords = new List<double>();
            var skipMainRegion = settings.MainSkipLimit > 0.0 &&
                                 (domain.Width <= settings.MainSkipLimit + Eps || domain.Height <= settings.MainSkipLimit + Eps);

            if (settings.DrawMain && !skipMainRegion)
            {
                var mainGrid = BuildMainGrid(domain, settings, mainObstacles);
                foreach (var y in mainGrid)
                {
                    if (y <= domain.MinY + 2.0 || y >= domain.MaxY - 2.0) continue;
                    var drew = false;
                    foreach (var raw in PolygonScanline.ClipHorizontal(localPolygon, y))
                    {
                        if (!TryTrimHorizontal(raw, domain, out var segment)) continue;
                        mainSegmentsLocal.Add(segment);
                        AddWorldLine(plan, segment, radians, PreviewLineKind.Main);
                        plan.MainSegmentCount++;
                        drew = true;
                    }
                    if (drew) mainCoords.Add(y);
                }
            }

            var furringCoords = new List<double>();
            if (settings.DrawFurring)
            {
                var furringGrid = BuildFurringGrid(domain, settings, furringObstacles, furringFromFarEdge);
                foreach (var x in furringGrid)
                {
                    if (x <= domain.MinX + 2.0 || x >= domain.MaxX - 2.0) continue;
                    var drew = false;
                    foreach (var raw in PolygonScanline.ClipVertical(localPolygon, x))
                    {
                        if (!TryTrimVertical(raw, domain, out var segment)) continue;
                        AddWorldLine(plan, segment, radians, PreviewLineKind.Furring);
                        plan.FurringSegmentCount++;
                        drew = true;
                    }
                    if (drew) furringCoords.Add(x);
                }
            }

            var hangerRows = new List<List<Point2>>();
            if (settings.DrawHangers && mainSegmentsLocal.Count > 0)
            {
                foreach (var main in mainSegmentsLocal)
                {
                    var row = BuildHangerRow(main, settings, mainObstacles, furringFromFarEdge);
                    if (row.Count == 0) continue;

                    var accepted = new List<Point2>();
                    foreach (var localPoint in row)
                    {
                        var world = Transform2.ToWorld(localPoint, radians);
                        if (plan.HangerPoints.Any(p => p.DistanceTo(world) < 0.01)) continue;
                        plan.HangerPoints.Add(world);
                        accepted.Add(localPoint);
                        AddHangerCross(plan, localPoint, radians);
                        plan.HangerCount++;
                    }
                    if (accepted.Count > 1)
                        hangerRows.Add(accepted.OrderBy(p => p.X).ToList());
                }
            }

            if (settings.AutoDimension)
            {
                AddDimensions(plan, settings, radians, domain, mainCoords, furringCoords, hangerRows);
                PackDimensions(plan, settings);
            }

            plan.DimensionSegmentCount = plan.Dimensions.Count;
            return plan;
        }

        private static IReadOnlyList<double> BuildMainGrid(Box2 domain, VxtSettings settings, List<Box2> obstacles)
        {
            var requestedMode = settings.MainLayout == MainLayoutMode.Auto
                ? MainLayoutMode.BalancedTwoEnds
                : settings.MainLayout;

            var absoluteIntervals = obstacles.Select(b => Tuple.Create(b.MinY, b.MaxY)).ToList();
            var relativeIntervals = settings.UseAvoidance && settings.ShiftAllForAvoidance
                ? obstacles.Select(b => Tuple.Create(b.MinY - domain.MinY, b.MaxY - domain.MinY)).ToList()
                : new List<Tuple<double, double>>();

            var optimized = VxtProLayoutOptimizer.Calculate(
                domain.Height,
                settings.MainMaxSpacing,
                settings.MainMinSpacing,
                settings.MainMaxEdgeOffset,
                settings.MainMinEdgeOffset,
                settings.MainBalanceStep,
                requestedMode,
                settings.OptimizationMode,
                relativeIntervals,
                settings.MainEdgeTolerance);
            if (optimized?.Layout == null) return Array.Empty<double>();

            var ideal = optimized.Layout.Positions(domain.MinY)
                .Where(v => v <= domain.MaxY - (settings.MainMinEdgeOffset - 0.1) + Eps)
                .ToList();

            if (!settings.UseAvoidance || absoluteIntervals.Count == 0)
                return ideal;

            // Whole-grid candidate search is used only when the user allows ShiftAll.
            // If collisions remain, repair individual coordinates with the proven legacy adjuster.
            if (settings.ShiftAllForAvoidance && optimized.Quality != null && optimized.Quality.IsClear)
                return ideal;

            var repaired = SmartLayout1D.AdjustGrid(
                ideal,
                absoluteIntervals,
                domain.MinY,
                domain.MaxY,
                settings.MainMinSpacing,
                settings.MainMaxSpacing,
                settings.MainMinEdgeOffset,
                settings.MainMaxEdgeOffset,
                settings.MainBalanceStep);
            return repaired.Count > 0 ? repaired : ideal;
        }

        private static IReadOnlyList<double> BuildFurringGrid(
            Box2 domain,
            VxtSettings settings,
            List<Box2> obstacles,
            bool fromFarEdge)
        {
            var spacing = settings.FurringSpacing;
            var length = domain.Width;
            var preferredOffset = fromFarEdge ? PositiveRemainder(length, spacing) : spacing;
            if (Math.Abs(preferredOffset) <= Eps) preferredOffset = spacing;

            var intervals = settings.UseAvoidance
                ? obstacles.Select(b => Tuple.Create(b.MinX, b.MaxX)).ToList()
                : new List<Tuple<double, double>>();

            var result = VxtProFurringOffsetOptimizer.Calculate(
                domain.MinX,
                domain.MaxX,
                spacing,
                preferredOffset,
                settings.ShiftAllForAvoidance ? intervals : null,
                settings.OptimizationMode,
                sampleStep: Math.Max(1.0, Math.Min(10.0, spacing / 20.0)));
            if (result == null) return Array.Empty<double>();

            if (!settings.UseAvoidance || intervals.Count == 0 ||
                (settings.ShiftAllForAvoidance && result.IsClear))
                return result.Positions;

            var repaired = SmartLayout1D.AdjustGrid(
                result.Positions,
                intervals,
                domain.MinX,
                domain.MaxX,
                spacing,
                spacing,
                0.0,
                spacing,
                spacing);
            return repaired.Count > 0 ? repaired : result.Positions;
        }

        private static List<Point2> BuildHangerRow(
            Segment2 main,
            VxtSettings settings,
            List<Box2> obstacles,
            bool furringFromFarEdge)
        {
            var minX = Math.Min(main.A.X, main.B.X);
            var maxX = Math.Max(main.A.X, main.B.X);
            var length = maxX - minX;
            if (length <= LegacyMinDrawLength) return new List<Point2>();

            var oneSide = settings.HangerLayout == HangerLayoutMode.OneSideFollowFurring;
            SmartLayout1D.Result layout;

            if (!oneSide)
            {
                var relativeIntervals = settings.UseAvoidance && settings.ShiftAllForAvoidance
                    ? obstacles
                        .Where(b => main.A.Y >= b.MinY - Eps && main.A.Y <= b.MaxY + Eps)
                        .Select(b => Tuple.Create(b.MinX - minX, b.MaxX - minX))
                        .ToList()
                    : null;
                var optimized = VxtProLayoutOptimizer.Calculate(
                    length,
                    settings.HangerMaxSpacing,
                    settings.HangerMinSpacing,
                    settings.HangerMaxEdgeOffset,
                    settings.HangerMinEdgeOffset,
                    settings.HangerBalanceStep,
                    MainLayoutMode.BalancedTwoEnds,
                    settings.OptimizationMode,
                    relativeIntervals,
                    settings.HangerEdgeTolerance);
                layout = optimized?.Layout;
            }
            else
            {
                // Preserve the exact user-selected near/far side for this directional mode.
                layout = SmartLayout1D.Calculate(
                    length,
                    settings.HangerMaxSpacing,
                    settings.HangerMinSpacing,
                    settings.HangerMaxEdgeOffset,
                    settings.HangerMinEdgeOffset,
                    settings.HangerBalanceStep,
                    MainLayoutMode.OneSide,
                    reverse: furringFromFarEdge);
            }

            if (layout == null) return new List<Point2>();

            var ideal = layout.Positions(minX)
                .Where(v => v <= maxX - (settings.HangerMinEdgeOffset - 0.1) + Eps)
                .ToList();

            var rowIntervals = obstacles
                .Where(b => main.A.Y >= b.MinY - Eps && main.A.Y <= b.MaxY + Eps)
                .Select(b => Tuple.Create(b.MinX, b.MaxX))
                .ToList();

            IReadOnlyList<double> final = ideal;
            if (settings.UseAvoidance && rowIntervals.Count > 0)
            {
                var repaired = VxtProHangerRepair.Repair(
                    ideal,
                    rowIntervals,
                    minX,
                    maxX,
                    settings.HangerMinSpacing,
                    settings.HangerMaxSpacing,
                    settings.HangerMinEdgeOffset,
                    settings.HangerMaxEdgeOffset,
                    settings.HangerBalanceStep,
                    settings.OptimizationMode,
                    settings.HangerEdgeTolerance);

                // Fail closed: an impossible obstacle band must not create an invalid Ty chain.
                if (!repaired.Success) return new List<Point2>();
                final = repaired.Positions;
            }

            return final
                .Where(x => x > minX + 2.0 && x < maxX - 2.0)
                .Select(x => new Point2(x, main.A.Y))
                .ToList();
        }

        private static void PackDimensions(VxtPreviewPlan plan, VxtSettings settings)
        {
            if (plan == null || plan.Dimensions.Count == 0) return;
            var packed = VxtProDimensionPacker.Pack(plan.Dimensions, settings.DimensionSpacing);
            plan.Dimensions.Clear();
            plan.Dimensions.AddRange(packed.Dimensions);
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

        private static List<Box2> TransformObstacles(IEnumerable<Box2> boxes, double radians, double clearance, Box2 domain)
        {
            var result = new List<Box2>();
            foreach (var box in boxes ?? Enumerable.Empty<Box2>())
            {
                var transformed = TransformBox(box, radians).Expand(clearance);
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

        private static double PositiveRemainder(double value, double divisor)
        {
            var result = value % divisor;
            if (result < 0.0) result += divisor;
            return result;
        }

        private static bool TryTrimHorizontal(Segment2 raw, Box2 domain, out Segment2 result)
        {
            var a = Math.Max(Math.Min(raw.A.X, raw.B.X), domain.MinX);
            var b = Math.Min(Math.Max(raw.A.X, raw.B.X), domain.MaxX);
            result = new Segment2(new Point2(a, raw.A.Y), new Point2(b, raw.A.Y));
            return b - a > LegacyMinDrawLength && raw.A.Y >= domain.MinY - Eps && raw.A.Y <= domain.MaxY + Eps;
        }

        private static bool TryTrimVertical(Segment2 raw, Box2 domain, out Segment2 result)
        {
            var a = Math.Max(Math.Min(raw.A.Y, raw.B.Y), domain.MinY);
            var b = Math.Min(Math.Max(raw.A.Y, raw.B.Y), domain.MaxY);
            result = new Segment2(new Point2(raw.A.X, a), new Point2(raw.A.X, b));
            return b - a > LegacyMinDrawLength && raw.A.X >= domain.MinX - Eps && raw.A.X <= domain.MaxX + Eps;
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

            if (settings.DimMain && mainCoords.Count > 0)
                AddVerticalChain(plan, WithBounds(mainCoords, domain.MinY, domain.MaxY), settings.MainDimPosition,
                    DimensionTarget.Main, radians, domain, settings.DimensionDistance, settings.DimensionSpacing, stack);

            if (settings.DimFurring && furringCoords.Count > 0)
                AddHorizontalChain(plan, WithBounds(furringCoords, domain.MinX, domain.MaxX), settings.FurringDimPosition,
                    DimensionTarget.Furring, radians, domain, settings.DimensionDistance, settings.DimensionSpacing, stack);

            if (settings.DimHanger)
            {
                var uniquePatterns = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in hangerRows)
                {
                    var xs = row.Select(p => p.X).Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
                    if (xs.Count == 0) continue;
                    if (!uniquePatterns.Add(PatternKey(xs))) continue;
                    var sourceRow = row[0].Y;
                    AddHorizontalChain(plan, WithBounds(xs, domain.MinX, domain.MaxX), settings.HangerDimPosition,
                        DimensionTarget.Hanger, radians, domain, settings.DimensionDistance, settings.DimensionSpacing, stack, sourceRow);
                }
            }
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
            VxtPreviewPlan plan,
            IEnumerable<double> values,
            DimensionPosition position,
            DimensionTarget target,
            double radians,
            Box2 domain,
            double distance,
            double spacing,
            IDictionary<string, int> stack,
            double? sourceBaseCoordinate = null)
        {
            var ys = values.Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
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
            IDictionary<string, int> stack,
            double? sourceBaseCoordinate = null)
        {
            var xs = values.Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
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
            foreach (var point in source.HangerPoints)
            {
                var key = PointKey(point);
                if (!seenHangers.Add(key)) continue;
                target.HangerPoints.Add(point);
                target.HangerCount++;
            }
            foreach (var dimension in source.Dimensions) target.Dimensions.Add(dimension);
            foreach (var text in source.Texts) target.Texts.Add(text);
        }

        private static string LineKey(PreviewLine line)
        {
            var a = PointKey(line.A);
            var b = PointKey(line.B);
            return string.CompareOrdinal(a, b) <= 0
                ? line.Kind + ":" + a + ":" + b
                : line.Kind + ":" + b + ":" + a;
        }

        private static string DimensionKey(PreviewDimension dimension)
            => dimension.Target + ":" + PointKey(dimension.ExtensionPoint1) + ":" +
               PointKey(dimension.ExtensionPoint2) + ":" + PointKey(dimension.DimensionLinePoint);

        private static string PointKey(Point2 point)
            => Math.Round(point.X, 3).ToString(CultureInfo.InvariantCulture) + "," +
               Math.Round(point.Y, 3).ToString(CultureInfo.InvariantCulture);

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
