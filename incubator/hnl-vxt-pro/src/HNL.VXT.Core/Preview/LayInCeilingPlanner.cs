using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
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

            // Calculate the first actual complete panel independently from Hatch
            // phase. A phase point may be outside a notch or inside a void.
            var tileHeight = spec.HasShortCross ? spec.ModuleShort : spec.MainSpacing;
            var firstTile = settings.DrawStartTileBlock
                ? FindFirstFullTile(localOuter, localHoles, bounds,
                    phaseX, phaseY, spec.ModuleShort, tileHeight)
                : (Point2?)null;

            var plan = new LayInCeilingPlan
            {
                MainAngleRadians = angle,
                HatchOrigin = Transform2.ToWorld(new Point2(phaseX, phaseY), angle),
                FirstTileOrigin = firstTile.HasValue
                    ? (Point2?)Transform2.ToWorld(firstTile.Value, angle) : null,
                FirstTileWidth = spec.ModuleShort,
                FirstTileHeight = tileHeight,
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
                var minSpacing = settings.HangerMinSpacing > Eps
                    ? settings.HangerMinSpacing : 700.0;
                var maxSpacing = settings.HangerMaxSpacing > Eps
                    ? settings.HangerMaxSpacing : spec.DefaultHangerMax;
                var minEdge = settings.HangerMinEdgeOffset > Eps
                    ? settings.HangerMinEdgeOffset
                    : settings.HangerEdgeTarget > Eps
                        ? settings.HangerEdgeTarget : spec.DefaultHangerEdge;
                var maxEdge = settings.HangerMaxEdgeOffset > Eps
                    ? settings.HangerMaxEdgeOffset
                    : Math.Max(minEdge, spec.DefaultHangerEdge + 100.0);
                var balanceStep = settings.HangerBalanceStep;
                if (minSpacing > maxSpacing || minEdge > maxEdge ||
                    balanceStep <= Eps || balanceStep > maxSpacing)
                    throw new InvalidOperationException(
                        "HNL Tool - HCF: Thông số Ty treo Min/Max/Bước bù không hợp lệ.");

                foreach (var segment in localMainSegments)
                    AddHangers(plan, segment, angle, minSpacing,
                        maxSpacing, minEdge, maxEdge, balanceStep);
            }

            AddDimensions(plan, settings, spec, bounds, angle, phaseX, phaseY);
            var gridDimensionCount = plan.DimensionRuns.Count;
            if (settings.DimensionMode != LayInDimensionMode.Off &&
                settings.DrawHangers && settings.DimensionHangers)
                AddHangerDimensions(plan, localMainSegments, settings, angle);
            // Keep both Preview and Create on the same final, collision-checked
            // dimension plan. Manual side choices preserve their exact positions.
            PlaceAutoDimensions(plan, settings, localOuter, localHoles,
                bounds, angle, gridDimensionCount);
            TallyMaterials(plan, spec);
            return plan;
        }

        private static Point2? FindFirstFullTile(
            IReadOnlyList<Point2> outer, IReadOnlyList<IReadOnlyList<Point2>> holes,
            Box2 bounds, double phaseX, double phaseY, double width, double height)
        {
            if (width <= Eps || height <= Eps) return null;
            var ixFirst = (int)Math.Ceiling((bounds.MinX - phaseX) / width - Eps);
            var ixLast = (int)Math.Floor((bounds.MaxX - width - phaseX) / width + Eps);
            var iyFirst = (int)Math.Ceiling((bounds.MinY - phaseY) / height - Eps);
            var iyLast = (int)Math.Floor((bounds.MaxY - height - phaseY) / height + Eps);
            Point2? best = null;
            double bestDistance = double.MaxValue;

            for (var iy = iyFirst; iy <= iyLast; iy++)
            {
                var y = phaseY + iy * height;
                for (var ix = ixFirst; ix <= ixLast; ix++)
                {
                    var x = phaseX + ix * width;
                    if (!ContainsCompletePanel(outer, holes, x, y, width, height))
                        continue;
                    var dx = x - phaseX;
                    var dy = y - phaseY;
                    var dist = dx * dx + dy * dy;
                    if (dist <= Eps) return new Point2(x, y);
                    if (dist < bestDistance - Eps)
                    {
                        bestDistance = dist;
                        best = new Point2(x, y);
                    }
                }
            }
            return best;
        }

        private static bool ContainsCompletePanel(
            IReadOnlyList<Point2> outer, IReadOnlyList<IReadOnlyList<Point2>> holes,
            double x, double y, double width, double height)
        {
            // Polygon edges only change cross-section topology at a vertex Y.
            // Check the middle of each such band so no notch/hole is crossed
            // even when it does not intersect the geometric panel centre.
            var bands = new List<double> { y, y + height };
            foreach (var p in outer)
                if (p.Y > y + Eps && p.Y < y + height - Eps) bands.Add(p.Y);
            foreach (var hole in holes)
                foreach (var p in hole)
                    if (p.Y > y + Eps && p.Y < y + height - Eps) bands.Add(p.Y);
            bands.Sort();

            for (int i = 0; i + 1 < bands.Count; i++)
            {
                var midY = 0.5 * (bands[i] + bands[i + 1]);
                if (bands[i + 1] - bands[i] <= Eps) continue;
                var spans = PolygonScanline.ClipHorizontal(outer, holes, midY);
                if (!spans.Any(span =>
                    Math.Min(span.A.X, span.B.X) <= x + Eps &&
                    Math.Max(span.A.X, span.B.X) >= x + width - Eps))
                    return false;
            }
            return true;
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
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double balanceStep)
        {
            var x1 = Math.Min(mainSegment.A.X, mainSegment.B.X);
            var x2 = Math.Max(mainSegment.A.X, mainSegment.B.X);
            var length = x2 - x1;
            if (length <= Eps) return;

            // Reuse the concealed-ceiling lattice solver: HARD Max for Ty and
            // boundary, SOFT Min, and exact multiples for all center gaps.
            // The clipped T-main segment is the true Ty constraint interval.
            var layout = SmartLayout1D.Calculate(
                length, maxSpacing, minSpacing, maxEdge, minEdge,
                balanceStep, MainLayoutMode.BalancedTwoEnds);
            if (layout == null)
                throw new InvalidOperationException(
                    "HNL Tool - HCF: Không thể bố trí Ty hợp lệ trên thanh T chính dài " +
                    length.ToString("0.###") + " mm với Min/Max/Bước bù đã chọn.");

            foreach (var x in layout.Positions(x1))
                plan.HangerPoints.Add(
                    Transform2.ToWorld(new Point2(x, mainSegment.A.Y), angle));
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
            if (settings.DimensionMode == LayInDimensionMode.GridAndEdges)
            {
                // Like the supplied tn.dxf sample: a native measured DIM for the
                // complete grid run plus a separate DIM for every cut edge.
                // A balanced phase can create a cut at BOTH ends.
                AddGridAxisDimensions(plan, settings, bounds, angle,
                    bounds.MinX, bounds.MaxX, spec.ModuleShort, phaseX, true);
                AddGridAxisDimensions(plan, settings, bounds, angle,
                    bounds.MinY, bounds.MaxY, acrossModule, phaseY, false);
                AddAdditionalOverallDimensionRuns(plan, settings, bounds, angle);
                return;
            }
            if (settings.DimensionMode == LayInDimensionMode.Overall)
            {
                AddDimensionRun(plan, bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MinY,
                    bounds.Width, 1, FormatLength(bounds.Width), angle, bounds, settings);
                AddDimensionRun(plan, bounds.MinX, bounds.MinY, bounds.MinX, bounds.MaxY,
                    bounds.Height, 1, FormatLength(bounds.Height), angle, bounds, settings);
                return;
            }

            // "Một ô" measures EVERY complete panel independently.
            // "Gộp ô" chains groups of N complete panels across the whole grid.
            // In both modes, partial cuts at either boundary are measured separately.
            var groupSize = settings.DimensionMode == LayInDimensionMode.Grouped
                ? settings.GroupedDimensionCount // <= 0 auto: all whole modules per axis
                : 1;
            AddPanelAxisDimensions(plan, settings, bounds, angle,
                bounds.MinX, bounds.MaxX, spec.ModuleShort, phaseX, true, groupSize);
            AddPanelAxisDimensions(plan, settings, bounds, angle,
                bounds.MinY, bounds.MaxY, acrossModule, phaseY, false, groupSize);
            AddAdditionalOverallDimensionRuns(plan, settings, bounds, angle);
        }

        private static void AddPanelAxisDimensions(
            LayInCeilingPlan plan, LayInCeilingSettings settings, Box2 bounds,
            double angle, double min, double max, double module, double phase,
            bool horizontal, int groupSize)
        {
            if (module <= Eps || max - min <= Eps) return;
            var first = FirstStationAtOrAfter(min, module, phase);
            if (first >= max - Eps)
            {
                AddMeasuredDimension(plan, settings, bounds, angle, min, max, horizontal);
                return;
            }

            if (first > min + Eps)
                AddMeasuredDimension(plan, settings, bounds, angle, min, first, horizontal);

            // Do not count the cut edge as a full panel. Group remaining full
            // modules in lattice multiples and emit the final shorter group.
            var fullCount = Math.Max(0, (int)Math.Floor((max - first) / module + Eps));
            var actualGroupSize = groupSize <= 0 ? Math.Max(1, fullCount) : groupSize;
            for (var i = 0; i < fullCount; i += actualGroupSize)
            {
                var count = Math.Min(actualGroupSize, fullCount - i);
                var start = first + i * module;
                var end = first + (i + count) * module;
                if (count == 1)
                {
                    AddMeasuredDimension(plan, settings, bounds, angle,
                        start, end, horizontal);
                }
                else if (horizontal)
                {
                    AddDimensionRun(plan, start, bounds.MinY, end, bounds.MinY,
                        module, count, BuildModuleLabel(count, module), angle, bounds, settings);
                }
                else
                {
                    AddDimensionRun(plan, bounds.MinX, start, bounds.MinX, end,
                        module, count, BuildModuleLabel(count, module), angle, bounds, settings);
                }
            }

            var last = first + fullCount * module;
            if (last < max - Eps)
                AddMeasuredDimension(plan, settings, bounds, angle, last, max, horizontal);
        }

        // DIM Ty is measured from the ACTUAL hanger stations along each physical
        // main T segment, including end clearances; no guessed ty spacing.
        private static void AddHangerDimensions(
            LayInCeilingPlan plan, IReadOnlyList<Segment2> mainSegments,
            LayInCeilingSettings settings, double angle)
        {
            if (plan.HangerPoints.Count == 0 || mainSegments.Count == 0) return;
            var rows = plan.HangerPoints
                .Select(p => Transform2.ToLocal(p, angle))
                .GroupBy(p => Math.Round(p.Y, 3))
                .ToDictionary(g => g.Key, g => g.Select(p => p.X).OrderBy(x => x).ToArray());
            var side = settings.HangerDimSide == LayInHorizontalDimSide.Top ? 1.0 : -1.0;
            var distance = Math.Max(1.0, settings.HangerDimDistance);

            foreach (var segment in mainSegments)
            {
                var x1 = Math.Min(segment.A.X, segment.B.X);
                var x2 = Math.Max(segment.A.X, segment.B.X);
                var y = segment.A.Y;
                double[] hangerXs;
                if (!rows.TryGetValue(Math.Round(y, 3), out hangerXs)) continue;

                var stations = new List<double> { x1 };
                foreach (var x in hangerXs)
                {
                    if (x > x1 + Eps && x < x2 - Eps &&
                        x > stations[stations.Count - 1] + Eps)
                        stations.Add(x);
                }
                if (x2 > stations[stations.Count - 1] + Eps)
                    stations.Add(x2);

                // A hanger exactly at the first endpoint is already represented
                // by x1; otherwise the first/last distances are included.
                for (var i = 0; i + 1 < stations.Count; i++)
                {
                    var start = stations[i];
                    var end = stations[i + 1];
                    if (end - start <= Eps) continue;
                    var a = Transform2.ToWorld(new Point2(start, y), angle);
                    var b = Transform2.ToWorld(new Point2(end, y), angle);
                    var offset = Transform2.ToWorld(
                        new Point2((start + end) * 0.5, y + side * distance), angle);
                    plan.DimensionRuns.Add(new LayInDimensionRun(
                        a, b, end - start, 1, string.Empty, offset));
                }
            }
        }

        // Add a measured overall DIM outside the original grid/cut row only on opt-in.
        private static void AddAdditionalOverallDimensionRuns(
            LayInCeilingPlan plan, LayInCeilingSettings settings, Box2 bounds, double angle)
        {
            if (!settings.AddOverallDimension) return;
            AddDimensionRun(plan, bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MinY,
                bounds.Width, 1, string.Empty, angle, bounds, settings, 1);
            AddDimensionRun(plan, bounds.MinX, bounds.MinY, bounds.MinX, bounds.MaxY,
                bounds.Height, 1, string.Empty, angle, bounds, settings, 1);
        }

        private static void AddGridAxisDimensions(
            LayInCeilingPlan plan, LayInCeilingSettings settings,
            Box2 bounds, double angle, double min, double max,
            double module, double phase, bool horizontal)
        {
            if (module <= Eps || max - min <= Eps) return;

            var first = FirstStationAtOrAfter(min, module, phase);
            // If the selected phase has no internal module station, the entire
            // edge is a single cut length. Never manufacture a full module.
            if (first >= max - Eps)
            {
                AddMeasuredDimension(plan, settings, bounds, angle,
                    min, max, horizontal);
                return;
            }
            if (first > min + Eps)
                AddMeasuredDimension(plan, settings, bounds, angle,
                    min, first, horizontal);

            var whole = Math.Max(0, (int)Math.Floor((max - first) / module + Eps));
            var last = first + whole * module;
            if (whole > 0)
                AddMeasuredDimension(plan, settings, bounds, angle,
                    first, last, horizontal);

            if (last < max - Eps)
                AddMeasuredDimension(plan, settings, bounds, angle,
                    Math.Max(min, last), max, horizontal);
        }

        private static void AddMeasuredDimension(
            LayInCeilingPlan plan, LayInCeilingSettings settings,
            Box2 bounds, double angle, double start, double end,
            bool horizontal)
        {
            if (end - start <= Eps) return;
            if (horizontal)
                AddDimensionRun(plan, start, bounds.MinY, end, bounds.MinY,
                    end - start, 1, string.Empty, angle, bounds, settings);
            else
                AddDimensionRun(plan, bounds.MinX, start, bounds.MinX, end,
                    end - start, 1, string.Empty, angle, bounds, settings);
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
            double angle,
            Box2 bounds,
            LayInCeilingSettings settings,
            int rowIndex = 0)
        {
            var horizontal = Math.Abs(y2 - y1) < Eps;
            var distance = (horizontal
                ? Math.Max(1.0, settings.HorizontalDimDistance)
                : Math.Max(1.0, settings.VerticalDimDistance))
                + Math.Max(0, rowIndex) * Math.Max(1.0, settings.DimensionRowSpacing);
            var linePoint = horizontal
                ? new Point2((x1 + x2) * 0.5,
                    settings.HorizontalDimSide == LayInHorizontalDimSide.Top
                        ? bounds.MaxY + distance : bounds.MinY - distance)
                : new Point2(
                    settings.VerticalDimSide == LayInVerticalDimSide.Right
                        ? bounds.MaxX + distance : bounds.MinX - distance,
                    (y1 + y2) * 0.5);
            plan.DimensionRuns.Add(new LayInDimensionRun(
                Transform2.ToWorld(new Point2(x1, y1), angle),
                Transform2.ToWorld(new Point2(x2, y2), angle),
                module,
                count,
                label,
                Transform2.ToWorld(linePoint, angle)));
        }

        // Auto DIM uses an actual INTERIOR point of the selected ceiling region.
        // Its centre can otherwise lie in a notch or a polygon hole. Keep the
        // dimension geometry and measured labels unchanged; relocate DIM lines only.
        private static void PlaceAutoDimensions(
            LayInCeilingPlan plan, LayInCeilingSettings settings,
            IReadOnlyList<Point2> outer, IReadOnlyList<IReadOnlyList<Point2>> holes,
            Box2 bounds, double angle, int gridCount)
        {
            if (plan.DimensionRuns.Count == 0) return;
            var autoHorizontal = settings.HorizontalDimSide == LayInHorizontalDimSide.Auto;
            var autoVertical = settings.VerticalDimSide == LayInVerticalDimSide.Auto;
            var autoHanger = settings.HangerDimSide == LayInHorizontalDimSide.Auto;
            if (!autoHorizontal && !autoVertical && !(settings.DimensionHangers && autoHanger))
                return;

            var centre = FindInteriorDimensionCentre(outer, holes, bounds);
            var verticalIntervals = PolygonScanline.ClipVertical(outer, holes, centre.X);
            var horizontalIntervals = PolygonScanline.ClipHorizontal(outer, holes, centre.Y);
            var verticalClear = verticalIntervals.FirstOrDefault(s =>
                centre.Y >= Math.Min(s.A.Y, s.B.Y) - Eps &&
                centre.Y <= Math.Max(s.A.Y, s.B.Y) + Eps);
            var horizontalClear = horizontalIntervals.FirstOrDefault(s =>
                centre.X >= Math.Min(s.A.X, s.B.X) - Eps &&
                centre.X <= Math.Max(s.A.X, s.B.X) + Eps);
            var hBounds = new Tuple<double, double>(
                Math.Min(verticalClear.A.Y, verticalClear.B.Y),
                Math.Max(verticalClear.A.Y, verticalClear.B.Y));
            var vBounds = new Tuple<double, double>(
                Math.Min(horizontalClear.A.X, horizontalClear.B.X),
                Math.Max(horizontalClear.A.X, horizontalClear.B.X));
            var yLane = AutoLane(centre.Y, hBounds.Item1, hBounds.Item2,
                settings.HorizontalDimDistance);
            var xLane = AutoLane(centre.X, vBounds.Item1, vBounds.Item2,
                settings.VerticalDimDistance);

            // The optional overall row is reserved at the configured FULL spacing.
            // If too little interior space remains, retain a separate outside row
            // instead of colliding with the central DIM chain.
            var overall = settings.AddOverallDimension &&
                settings.DimensionMode != LayInDimensionMode.Overall;
            var yOverall = SeparateAutoRow(yLane, centre.Y, hBounds.Item1, hBounds.Item2,
                bounds.MinY, bounds.MaxY, settings.DimensionRowSpacing);
            var xOverall = SeparateAutoRow(xLane, centre.X, vBounds.Item1, vBounds.Item2,
                bounds.MinX, bounds.MaxX, settings.DimensionRowSpacing);
            for (var i = 0; i < gridCount; i++)
            {
                var run = plan.DimensionRuns[i];
                var a = Transform2.ToLocal(run.A, angle);
                var b = Transform2.ToLocal(run.B, angle);
                var line = Transform2.ToLocal(run.DimensionLinePoint.Value, angle);
                var isOverall = overall && i >= gridCount - 2;
                if (Math.Abs(a.Y - b.Y) <= Eps && autoHorizontal)
                    line = new Point2(line.X, isOverall ? yOverall : yLane);
                else if (Math.Abs(a.X - b.X) <= Eps && autoVertical)
                    line = new Point2(isOverall ? xOverall : xLane, line.Y);
                else
                    continue;
                plan.DimensionRuns[i] = WithDimensionLine(run,
                    Transform2.ToWorld(line, angle));
            }

            // Keep Ty DIM on its actual physical T rows, but when "Tự động"
            // is selected, shift conflicting parallel rows by DIM spacing.
            // Explicit Top/Bottom Ty positions are never silently overridden.
            if (!settings.DimensionHangers || !autoHanger || gridCount == plan.DimensionRuns.Count)
                return;
            var minimum = Math.Max(1.0, settings.DimensionRowSpacing);
            var rows = new Dictionary<double, double>();
            for (var i = gridCount; i < plan.DimensionRuns.Count; i++)
            {
                var run = plan.DimensionRuns[i];
                var a = Transform2.ToLocal(run.A, angle);
                var b = Transform2.ToLocal(run.B, angle);
                var key = Math.Round(a.Y, 3);
                double chosen;
                if (!rows.TryGetValue(key, out chosen))
                {
                    var baseDistance = Math.Max(1.0, settings.HangerDimDistance);
                    chosen = a.Y - baseDistance;
                    var bestScore = double.PositiveInfinity;
                    // Search both sides of the T row, expanding only if needed.
                    for (var level = 0; level < 12; level++)
                    {
                        foreach (var sign in new[] { -1.0, 1.0 })
                        {
                            var candidate = a.Y + sign * (baseDistance + level * minimum);
                            if (candidate < bounds.MinY + Eps || candidate > bounds.MaxY - Eps)
                                continue;
                            var collides = false;
                            for (var j = 0; j < i; j++)
                            {
                                var prev = plan.DimensionRuns[j];
                                var pa = Transform2.ToLocal(prev.A, angle);
                                var pb = Transform2.ToLocal(prev.B, angle);
                                if (Math.Abs(pa.Y - pb.Y) > Eps) continue;
                                var pp = Transform2.ToLocal(prev.DimensionLinePoint.Value, angle);
                                if (Math.Abs(candidate - pp.Y) >= minimum - Eps) continue;
                                if (Math.Min(b.X, a.X) < Math.Max(pa.X, pb.X) - Eps &&
                                    Math.Max(b.X, a.X) > Math.Min(pa.X, pb.X) + Eps)
                                {
                                    collides = true;
                                    break;
                                }
                            }
                            if (!collides)
                            {
                                var score = Math.Abs(candidate - a.Y) +
                                    (sign > 0 ? 0.1 : 0.0);
                                if (score < bestScore)
                                {
                                    chosen = candidate;
                                    bestScore = score;
                                }
                            }
                        }
                        if (bestScore < double.PositiveInfinity) break;
                    }
                    rows[key] = chosen;
                }
                plan.DimensionRuns[i] = WithDimensionLine(run,
                    Transform2.ToWorld(
                        new Point2((a.X + b.X) * 0.5, chosen), angle));
            }
        }

        private static LayInDimensionRun WithDimensionLine(
            LayInDimensionRun run, Point2 newPoint)
            => new LayInDimensionRun(run.A, run.B, run.Module,
                run.ModuleCount, run.Label, newPoint);

        private static double AutoLane(double centre, double min, double max, double distance)
        {
            if (max - min <= Eps) return centre;
            var margin = Math.Min(60.0, (max - min) * 0.08);
            var above = Math.Max(0.0, max - margin - centre);
            var below = Math.Max(0.0, centre - min - margin);
            var sign = above >= below ? 1.0 : -1.0;
            return centre + sign * Math.Min(
                Math.Max(1.0, distance), Math.Max(above, below) * 0.8);
        }

        private static double SeparateAutoRow(double lane, double centre,
            double min, double max, double outerMin, double outerMax,
            double rowSpacing)
        {
            var step = Math.Max(1.0, rowSpacing);
            var sign = lane >= centre ? 1.0 : -1.0;
            if (lane + sign * step < max - Eps &&
                lane + sign * step > min + Eps)
                return lane + sign * step;
            if (lane - sign * step < max - Eps &&
                lane - sign * step > min + Eps)
                return lane - sign * step;
            return sign > 0 ? outerMax + step : outerMin - step;
        }

        private static Point2 FindInteriorDimensionCentre(
            IReadOnlyList<Point2> outer, IReadOnlyList<IReadOnlyList<Point2>> holes,
            Box2 bounds)
        {
            var centre = bounds.Center;
            var best = centre;
            var bestScore = double.PositiveInfinity;
            // Sample near the geometric centre first, then look for an actual
            // open slice when the bounding-box centre is cut out or holed.
            for (var i = 0; i <= 16; i++)
            {
                var offset = i == 0 ? 0.0 :
                    (i % 2 == 1 ? 1.0 : -1.0) * ((i + 1) / 2) * 0.055;
                var y = centre.Y + offset * bounds.Height;
                if (y <= bounds.MinY + Eps || y >= bounds.MaxY - Eps) continue;
                foreach (var segment in PolygonScanline.ClipHorizontal(outer, holes, y))
                {
                    var left = Math.Min(segment.A.X, segment.B.X);
                    var right = Math.Max(segment.A.X, segment.B.X);
                    if (right - left <= Eps) continue;
                    var inset = Math.Min(80.0, (right - left) * 0.15);
                    var x = Math.Max(left + inset, Math.Min(right - inset, centre.X));
                    var score = Math.Pow((x - centre.X) / Math.Max(1, bounds.Width), 2) +
                        Math.Pow((y - centre.Y) / Math.Max(1, bounds.Height), 2);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new Point2(x, y);
                    }
                }
            }
            return best;
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
