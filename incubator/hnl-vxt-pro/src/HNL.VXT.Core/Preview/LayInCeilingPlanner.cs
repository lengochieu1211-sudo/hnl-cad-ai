using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    public static class LayInCeilingPlanner
    {
        private const double Eps = 1e-6;
        private const double BoundaryStationTolerance = 0.5;

        private sealed class SystemSpec
        {
            public double ModuleShort;
            public double MainSpacing;
            public double MainStock;
            public double LongCrossStock;
            public double ShortCrossStock;
            public bool HasShortCross;
            public string HatchPatternName;
            public double DefaultHangerMax;
            public double DefaultHangerEdge;
        }

        public static LayInCeilingPlan Build(
            Boundary2 boundary,
            IEnumerable<Boundary2> holes,
            LayInCeilingSettings settings)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (boundary.Vertices.Count < 3)
                throw new ArgumentException("Lay-in Ceiling requires a valid closed boundary.", nameof(boundary));

            var holeList = (holes ?? Enumerable.Empty<Boundary2>())
                .Where(h => h != null && h.Vertices.Count >= 3)
                .ToList();
            var spec = ResolveSpec(settings.GridSystem);

            if (settings.MainDirection == LayInMainDirectionMode.AutoOptimize)
            {
                double longAngle;
                double shortAngle;
                ResolvePrincipalAxes(boundary, out longAngle, out shortAngle);

                var longPlan = BuildAtAngle(boundary, holeList, settings, spec, longAngle);
                var shortPlan = BuildAtAngle(boundary, holeList, settings, spec, shortAngle);
                return ComparePlans(longPlan, shortPlan) <= 0 ? longPlan : shortPlan;
            }

            var angle = ResolveRequestedAngle(boundary, settings.MainDirection);
            return BuildAtAngle(boundary, holeList, settings, spec, angle);
        }

        private static LayInCeilingPlan BuildAtAngle(
            Boundary2 boundary,
            IReadOnlyList<Boundary2> holes,
            LayInCeilingSettings settings,
            SystemSpec spec,
            double angle)
        {
            angle = NormalizePi(angle);
            var localOuter = boundary.Vertices.Select(p => Transform2.ToLocal(p, angle)).ToList();
            var localHoles = holes
                .Select(h => (IReadOnlyList<Point2>)h.Vertices.Select(p => Transform2.ToLocal(p, angle)).ToList())
                .ToList();
            var bounds = Box2.FromPoints(localOuter);

            double phaseX;
            double phaseY;
            ResolvePhases(boundary, settings, spec, angle, bounds, out phaseX, out phaseY);

            var mainStations = GenerateStations(
                bounds.MinY, bounds.MaxY, spec.MainSpacing, phaseY);
            var longStations = GenerateStations(
                bounds.MinX, bounds.MaxX, spec.ModuleShort, phaseX);

            var plan = new LayInCeilingPlan
            {
                MainAngleRadians = angle,
                HatchOrigin = Transform2.ToWorld(new Point2(phaseX, phaseY), angle),
                HatchPatternName = spec.HatchPatternName,
                ModuleShort = spec.ModuleShort,
                MainTeeSpacing = spec.MainSpacing,
                MainTeeStockLength = spec.MainStock,
                LongCrossTeeStockLength = spec.LongCrossStock,
                ShortCrossTeeStockLength = spec.ShortCrossStock
            };

            var localMainSegments = new List<Segment2>();

            // Main Tee runs along local X and repeats across local Y.
            foreach (var y in mainStations)
            {
                foreach (var segment in PolygonScanline.ClipHorizontal(localOuter, localHoles, y))
                {
                    if (segment.A.DistanceTo(segment.B) <= Eps) continue;
                    localMainSegments.Add(segment);
                    AddWorldSegment(plan, segment, LayInTeeKind.MainTee, angle);
                }
            }

            // T1200/T1220 is perpendicular to Main Tee. Split at each Main Tee so every
            // physical piece maps to one connector-to-connector stock member.
            foreach (var x in longStations)
            {
                foreach (var interval in PolygonScanline.ClipVertical(localOuter, localHoles, x))
                {
                    foreach (var piece in SplitVertical(interval, mainStations))
                        AddWorldSegment(plan, piece, LayInTeeKind.LongCrossTee, angle);
                }
            }

            // Square modules add T600/T610 halfway between adjacent Main Tee grid lines.
            // These short tees are perpendicular to T1200/T1220, hence parallel to Main Tee.
            if (spec.HasShortCross)
            {
                var shortStations = GenerateStations(
                    bounds.MinY,
                    bounds.MaxY,
                    spec.MainSpacing,
                    phaseY + spec.MainSpacing * 0.5);

                foreach (var y in shortStations)
                {
                    foreach (var interval in PolygonScanline.ClipHorizontal(localOuter, localHoles, y))
                    {
                        foreach (var piece in SplitHorizontal(interval, longStations))
                            AddWorldSegment(plan, piece, LayInTeeKind.ShortCrossTee, angle);
                    }
                }
            }

            if (settings.DrawHangers)
            {
                var maxSpacing = settings.HangerMaxSpacing > Eps
                    ? settings.HangerMaxSpacing
                    : spec.DefaultHangerMax;
                var edgeTarget = settings.HangerEdgeTarget > Eps
                    ? settings.HangerEdgeTarget
                    : spec.DefaultHangerEdge;

                foreach (var segment in localMainSegments)
                    AddHangers(plan, segment, angle, maxSpacing, edgeTarget);
            }

            AddDimensions(plan, settings, spec, bounds, angle, phaseX, phaseY);
            TallyMaterials(plan, spec);
            return plan;
        }

        private static void ResolvePhases(
            Boundary2 boundary,
            LayInCeilingSettings settings,
            SystemSpec spec,
            double angle,
            Box2 bounds,
            out double phaseX,
            out double phaseY)
        {
            if (settings.StartMode == LayInStartMode.Balanced)
            {
                phaseX = BalancedPhase(bounds.MinX, bounds.MaxX, spec.ModuleShort);
                phaseY = BalancedPhase(bounds.MinY, bounds.MaxY, spec.MainSpacing);
                return;
            }

            Point2 reference;
            if (settings.StartMode == LayInStartMode.FromDoor && settings.DoorPoint.HasValue)
            {
                var door = settings.DoorPoint.Value;
                reference = boundary.Vertices
                    .OrderBy(p => p.DistanceTo(door))
                    .First();
            }
            else if (settings.ManualStartPoint.HasValue)
            {
                reference = settings.ManualStartPoint.Value;
            }
            else
            {
                reference = Transform2.ToWorld(new Point2(bounds.MinX, bounds.MinY), angle);
            }

            var local = Transform2.ToLocal(reference, angle);
            phaseX = local.X;
            phaseY = local.Y;
        }

        private static double BalancedPhase(double min, double max, double spacing)
        {
            var span = Math.Max(0.0, max - min);
            if (span <= Eps || spacing <= Eps) return min;

            var whole = Math.Floor(span / spacing);
            var remainder = span - whole * spacing;
            return min + remainder * 0.5;
        }

        private static IReadOnlyList<double> GenerateStations(
            double min,
            double max,
            double spacing,
            double phase)
        {
            var result = new List<double>();
            if (spacing <= Eps || max - min <= Eps) return result;

            var firstIndex = (int)Math.Ceiling((min - phase) / spacing - Eps);
            var lastIndex = (int)Math.Floor((max - phase) / spacing + Eps);

            for (var i = firstIndex; i <= lastIndex; i++)
            {
                var value = phase + i * spacing;
                if (value <= min + BoundaryStationTolerance ||
                    value >= max - BoundaryStationTolerance)
                    continue;
                result.Add(value);
            }

            return result;
        }

        private static IEnumerable<Segment2> SplitVertical(
            Segment2 segment,
            IReadOnlyList<double> breaks)
        {
            var min = Math.Min(segment.A.Y, segment.B.Y);
            var max = Math.Max(segment.A.Y, segment.B.Y);
            var values = new List<double> { min };
            values.AddRange(breaks.Where(v => v > min + Eps && v < max - Eps));
            values.Add(max);
            values.Sort();

            for (var i = 0; i + 1 < values.Count; i++)
            {
                if (values[i + 1] - values[i] <= Eps) continue;
                yield return new Segment2(
                    new Point2(segment.A.X, values[i]),
                    new Point2(segment.A.X, values[i + 1]));
            }
        }

        private static IEnumerable<Segment2> SplitHorizontal(
            Segment2 segment,
            IReadOnlyList<double> breaks)
        {
            var min = Math.Min(segment.A.X, segment.B.X);
            var max = Math.Max(segment.A.X, segment.B.X);
            var values = new List<double> { min };
            values.AddRange(breaks.Where(v => v > min + Eps && v < max - Eps));
            values.Add(max);
            values.Sort();

            for (var i = 0; i + 1 < values.Count; i++)
            {
                if (values[i + 1] - values[i] <= Eps) continue;
                yield return new Segment2(
                    new Point2(values[i], segment.A.Y),
                    new Point2(values[i + 1], segment.A.Y));
            }
        }

        private static void AddWorldSegment(
            LayInCeilingPlan plan,
            Segment2 local,
            LayInTeeKind kind,
            double angle)
        {
            plan.TeeSegments.Add(new LayInTeeSegment(
                Transform2.ToWorld(local.A, angle),
                Transform2.ToWorld(local.B, angle),
                kind));
        }

        private static void AddHangers(
            LayInCeilingPlan plan,
            Segment2 mainSegment,
            double angle,
            double maxSpacing,
            double edgeTarget)
        {
            var x1 = Math.Min(mainSegment.A.X, mainSegment.B.X);
            var x2 = Math.Max(mainSegment.A.X, mainSegment.B.X);
            var length = x2 - x1;
            if (length <= Eps || maxSpacing <= Eps) return;

            var edge = Math.Min(Math.Max(0.0, edgeTarget), length * 0.5);
            var first = x1 + edge;
            var last = x2 - edge;

            if (last - first <= Eps)
            {
                plan.HangerPoints.Add(
                    Transform2.ToWorld(new Point2((x1 + x2) * 0.5, mainSegment.A.Y), angle));
                return;
            }

            var intervals = Math.Max(1, (int)Math.Ceiling((last - first) / maxSpacing));
            var step = (last - first) / intervals;

            for (var i = 0; i <= intervals; i++)
            {
                var x = first + step * i;
                plan.HangerPoints.Add(
                    Transform2.ToWorld(new Point2(x, mainSegment.A.Y), angle));
            }
        }

        private static void AddDimensions(
            LayInCeilingPlan plan,
            LayInCeilingSettings settings,
            SystemSpec spec,
            Box2 bounds,
            double angle,
            double phaseX,
            double phaseY)
        {
            if (settings.DimensionMode == LayInDimensionMode.Off) return;

            var acrossModule = spec.HasShortCross ? spec.ModuleShort : spec.MainSpacing;
            if (settings.DimensionMode == LayInDimensionMode.Overall)
            {
                AddDimensionRun(plan, bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MinY,
                    bounds.Width, 1, FormatLength(bounds.Width), angle);
                AddDimensionRun(plan, bounds.MinX, bounds.MinY, bounds.MinX, bounds.MaxY,
                    bounds.Height, 1, FormatLength(bounds.Height), angle);
                return;
            }

            var requestedCount = settings.DimensionMode == LayInDimensionMode.Grouped
                ? Math.Max(1, settings.GroupedDimensionCount)
                : 1;

            var xStart = FirstStationAtOrAfter(bounds.MinX, spec.ModuleShort, phaseX);
            var yStart = FirstStationAtOrAfter(bounds.MinY, acrossModule, phaseY);

            // Count only complete modules after the selected grid start. Counting
            // from the overall boundary width could label 10 x 600 while the
            // clipped dimension spans just 5700 when Manual Start is offset.
            var xCount = Math.Min(requestedCount, Math.Max(0,
                (int)Math.Floor((bounds.MaxX - xStart) / spec.ModuleShort + Eps)));
            var yCount = Math.Min(requestedCount, Math.Max(0,
                (int)Math.Floor((bounds.MaxY - yStart) / acrossModule + Eps)));

            if (xCount > 0)
            {
                AddDimensionRun(
                    plan, xStart, bounds.MinY,
                    xStart + xCount * spec.ModuleShort, bounds.MinY,
                    spec.ModuleShort, xCount,
                    BuildModuleLabel(xCount, spec.ModuleShort), angle);
            }

            if (yCount > 0)
            {
                AddDimensionRun(
                    plan, bounds.MinX, yStart,
                    bounds.MinX, yStart + yCount * acrossModule,
                    acrossModule, yCount,
                    BuildModuleLabel(yCount, acrossModule), angle);
            }
        }

        private static void AddDimensionRun(
            LayInCeilingPlan plan,
            double x1,
            double y1,
            double x2,
            double y2,
            double module,
            int count,
            string label,
            double angle)
        {
            plan.DimensionRuns.Add(new LayInDimensionRun(
                Transform2.ToWorld(new Point2(x1, y1), angle),
                Transform2.ToWorld(new Point2(x2, y2), angle),
                module,
                count,
                label));
        }

        private static double FirstStationAtOrAfter(double min, double spacing, double phase)
        {
            if (spacing <= Eps) return min;
            var index = Math.Ceiling((min - phase) / spacing - Eps);
            return phase + index * spacing;
        }

        private static string BuildModuleLabel(int count, double module)
            => count + " × " + FormatLength(module) + " = " + FormatLength(count * module);

        private static string FormatLength(double value)
            => Math.Round(value, 3).ToString("0.###");

        private static void TallyMaterials(LayInCeilingPlan plan, SystemSpec spec)
        {
            var main = plan.TeeSegments.Where(x => x.Kind == LayInTeeKind.MainTee).ToList();
            var longCross = plan.TeeSegments.Where(x => x.Kind == LayInTeeKind.LongCrossTee).ToList();
            var shortCross = plan.TeeSegments.Where(x => x.Kind == LayInTeeKind.ShortCrossTee).ToList();

            plan.MainTeeTotalLength = main.Sum(x => x.Length);
            plan.LongCrossTeeTotalLength = longCross.Sum(x => x.Length);
            plan.ShortCrossTeeTotalLength = shortCross.Sum(x => x.Length);

            plan.MainTeeStockCount = CountStocks(main, spec.MainStock);
            plan.LongCrossTeeCount = CountStocks(longCross, spec.LongCrossStock);
            plan.ShortCrossTeeCount = spec.HasShortCross
                ? CountStocks(shortCross, spec.ShortCrossStock)
                : 0;

            plan.PurchasedLength =
                plan.MainTeeStockCount * spec.MainStock +
                plan.LongCrossTeeCount * spec.LongCrossStock +
                plan.ShortCrossTeeCount * spec.ShortCrossStock;

            plan.WasteLength = Math.Max(
                0.0,
                plan.PurchasedLength -
                plan.MainTeeTotalLength -
                plan.LongCrossTeeTotalLength -
                plan.ShortCrossTeeTotalLength);
        }

        private static int CountStocks(IEnumerable<LayInTeeSegment> segments, double stockLength)
        {
            if (stockLength <= Eps) return 0;
            return segments.Sum(s =>
                s.Length <= Eps ? 0 : Math.Max(1, (int)Math.Ceiling(s.Length / stockLength - Eps)));
        }

        private static int ComparePlans(LayInCeilingPlan a, LayInCeilingPlan b)
        {
            var purchased = a.PurchasedLength.CompareTo(b.PurchasedLength);
            if (purchased != 0) return purchased;

            var waste = a.WasteLength.CompareTo(b.WasteLength);
            if (waste != 0) return waste;

            var stocksA = a.MainTeeStockCount + a.LongCrossTeeCount + a.ShortCrossTeeCount;
            var stocksB = b.MainTeeStockCount + b.LongCrossTeeCount + b.ShortCrossTeeCount;
            return stocksA.CompareTo(stocksB);
        }

        private static double ResolveRequestedAngle(
            Boundary2 boundary,
            LayInMainDirectionMode mode)
        {
            if (mode == LayInMainDirectionMode.Horizontal) return 0.0;
            if (mode == LayInMainDirectionMode.Vertical) return Math.PI * 0.5;

            double longAngle;
            double shortAngle;
            ResolvePrincipalAxes(boundary, out longAngle, out shortAngle);
            return mode == LayInMainDirectionMode.ParallelShortSide
                ? shortAngle
                : longAngle;
        }

        private static void ResolvePrincipalAxes(
            Boundary2 boundary,
            out double longAngle,
            out double shortAngle)
        {
            var bestArea = double.MaxValue;
            var bestAngle = 0.0;
            Box2 bestBounds = default(Box2);
            var found = false;

            var vertices = boundary.Vertices;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Count];
                if (a.DistanceTo(b) <= Eps) continue;

                var angle = NormalizeHalfPi(Math.Atan2(b.Y - a.Y, b.X - a.X));
                var local = vertices.Select(p => Transform2.ToLocal(p, angle)).ToList();
                var bounds = Box2.FromPoints(local);
                var area = bounds.Width * bounds.Height;

                if (!found || area < bestArea - 1e-5)
                {
                    found = true;
                    bestArea = area;
                    bestAngle = angle;
                    bestBounds = bounds;
                }
            }

            if (!found)
            {
                longAngle = 0.0;
                shortAngle = Math.PI * 0.5;
                return;
            }

            if (bestBounds.Width >= bestBounds.Height)
            {
                longAngle = NormalizePi(bestAngle);
                shortAngle = NormalizePi(bestAngle + Math.PI * 0.5);
            }
            else
            {
                longAngle = NormalizePi(bestAngle + Math.PI * 0.5);
                shortAngle = NormalizePi(bestAngle);
            }
        }

        private static double NormalizeHalfPi(double angle)
        {
            var halfPi = Math.PI * 0.5;
            while (angle < 0.0) angle += halfPi;
            while (angle >= halfPi) angle -= halfPi;
            return angle;
        }

        private static double NormalizePi(double angle)
        {
            while (angle < 0.0) angle += Math.PI;
            while (angle >= Math.PI) angle -= Math.PI;
            return angle;
        }

        private static SystemSpec ResolveSpec(LayInGridSystem system)
        {
            switch (system)
            {
                case LayInGridSystem.Module610x610:
                    return new SystemSpec
                    {
                        ModuleShort = 610.0,
                        MainSpacing = 1220.0,
                        MainStock = 3660.0,
                        LongCrossStock = 1220.0,
                        ShortCrossStock = 610.0,
                        HasShortCross = true,
                        HatchPatternName = "HNL_CF_610X610",
                        DefaultHangerMax = 1220.0,
                        DefaultHangerEdge = 305.0
                    };

                case LayInGridSystem.Module600x1200:
                    return new SystemSpec
                    {
                        ModuleShort = 600.0,
                        MainSpacing = 1200.0,
                        MainStock = 3600.0,
                        LongCrossStock = 1200.0,
                        ShortCrossStock = 600.0,
                        HasShortCross = false,
                        HatchPatternName = "HNL_CF_600X1200",
                        DefaultHangerMax = 1200.0,
                        DefaultHangerEdge = 300.0
                    };

                case LayInGridSystem.Module610x1220:
                    return new SystemSpec
                    {
                        ModuleShort = 610.0,
                        MainSpacing = 1220.0,
                        MainStock = 3660.0,
                        LongCrossStock = 1220.0,
                        ShortCrossStock = 610.0,
                        HasShortCross = false,
                        HatchPatternName = "HNL_CF_610X1220",
                        DefaultHangerMax = 1220.0,
                        DefaultHangerEdge = 305.0
                    };

                default:
                    return new SystemSpec
                    {
                        ModuleShort = 600.0,
                        MainSpacing = 1200.0,
                        MainStock = 3600.0,
                        LongCrossStock = 1200.0,
                        ShortCrossStock = 600.0,
                        HasShortCross = true,
                        HatchPatternName = "HNL_CF_600X600",
                        DefaultHangerMax = 1200.0,
                        DefaultHangerEdge = 300.0
                    };
            }
        }
    }
}
