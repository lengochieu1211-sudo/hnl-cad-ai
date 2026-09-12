using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// HNL Tool V6.7.6.15 concave/notch post-process.
    /// The global XC grid is kept as the primary/economic solution. For concave bands the
    /// processor first tries to move an existing global XC on the same strict-multiple lattice;
    /// only unresolved Max-edge/Max-spacing violations receive the minimum local short XC.
    /// Local XC is clipped to the affected band and receives Ty with the same strict Ty solver.
    /// </summary>
    internal static class VxtConcaveMainPostProcessor
    {
        private const double Eps = 1e-7;
        private const double Tol = 0.1;
        private const double MinDrawLength = 5.0;

        public static void Apply(Boundary2 boundary, VxtSettings settings, VxtLayoutContext context, VxtPreviewPlan plan)
        {
            if (boundary == null || settings == null || plan == null || !settings.DrawMain) return;
            context = context ?? new VxtLayoutContext();
            if (!plan.Lines.Any(x => x.Kind == PreviewLineKind.Main)) return;

            var scopes = BuildScopes(boundary, settings, context).ToList();
            if (scopes.Count == 0) return;

            // Exact Lisp priority: try to repair a normal single-direction ceiling by shifting
            // global XC first, regardless of Auto DIM. Final DIM is rebuilt from the post-processed
            // geometry by VxtPostProcessDimensionSynchronizer. Rectangle-region mode owns several
            // independent grids, therefore each region stays isolated and uses local fallback only.
            if (scopes.Count == 1 && settings.MainDirection != MainDirectionMode.RectangleRegions)
            {
                var scope = scopes[0];
                var grid = ExtractMainGrid(plan, scope);
                var checks = CollectChecks(scope);
                if (grid.Count > 0 && checks.Count > 0)
                {
                    var obstacles = TransformMainObstacles(context, scope, settings);
                    List<Segment2> regionSegments;
                    var hasRegionalCandidate = VxtOrthogonalNotchRegionPlanner.TryBuild(
                        scope.Polygon, scope.Domain, settings, obstacles, out regionSegments);

                    // Candidate A: Lisp-first global rebalance, then the minimum local fallback only
                    // when the global grid still cannot satisfy a real concave band.
                    var moved = RebalanceGlobalGrid(grid, checks, scope.Domain, settings, obstacles);
                    if (!SameGrid(grid, moved))
                    {
                        RebuildGlobalMains(plan, scope, moved, settings, obstacles);
                        grid = moved;
                    }
                    AddLocalRepairs(plan, scope, grid, settings, obstacles);

                    // Candidate B: automatic orthogonal-notch rectangle decomposition. Never choose
                    // it blindly: safety is mandatory, then construction economy wins lexicographically
                    // by fewer XC segments, fewer Ty, shorter total XC, and finally spacing nearer Max.
                    // XP is not rebuilt by either candidate, so its one-side chase phase is preserved.
                    if (hasRegionalCandidate)
                    {
                        var globalScore = ScoreMainStrategy(plan, scope, settings, obstacles);
                        var regionalPlan = CloneForMainStrategy(plan);
                        RebuildRegionalMains(regionalPlan, scope, regionSegments, settings, obstacles);
                        var regionalScore = ScoreMainStrategy(regionalPlan, scope, settings, obstacles);
                        if (regionalScore.IsBetterThan(globalScore))
                            CopyMainAndHangers(plan, regionalPlan);
                    }
                }
                return;
            }

            foreach (var scope in scopes)
            {
                var grid = ExtractMainGrid(plan, scope);
                if (grid.Count == 0) continue;
                var obstacles = TransformMainObstacles(context, scope, settings);
                AddLocalRepairs(plan, scope, grid, settings, obstacles);
            }
        }

        private static IEnumerable<Scope> BuildScopes(Boundary2 boundary, VxtSettings settings, VxtLayoutContext context)
        {
            if (settings.MainDirection == MainDirectionMode.RectangleRegions && context.HasManualRegions)
            {
                foreach (var region in context.Regions)
                {
                    var radians = NormalizeDegrees(region.MainAngleDegrees) * Math.PI / 180.0;
                    var polygon = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
                    var polygonBounds = Box2.FromPoints(polygon);
                    var regionLocal = TransformBox(region.WorldBounds, radians);
                    Box2 domain;
                    if (TryIntersect(polygonBounds, regionLocal, out domain) && domain.Width > Eps && domain.Height > Eps)
                        yield return new Scope(polygon, domain, radians, region.FurringFromFarEdge);
                }
                yield break;
            }

            var angle = ResolveAngle(boundary, settings);
            var r = angle * Math.PI / 180.0;
            var localPolygon = boundary.Vertices.Select(p => Transform2.ToLocal(p, r)).ToList();
            yield return new Scope(localPolygon, Box2.FromPoints(localPolygon), r, context.GlobalFurringFromFarEdge);
        }

        private static double ResolveAngle(Boundary2 boundary, VxtSettings settings)
        {
            switch (settings.MainDirection)
            {
                case MainDirectionMode.Vertical:
                    return 90.0;
                case MainDirectionMode.TwoPoints:
                case MainDirectionMode.RectangleRegions:
                    return NormalizeDegrees(settings.DirectionDegrees);
                case MainDirectionMode.Auto:
                    var b = boundary.GetBounds();
                    var wide = (b.Max.X - b.Min.X) > (b.Max.Y - b.Min.Y);
                    return settings.AutoShadowline ? (wide ? 0.0 : 90.0) : (wide ? 90.0 : 0.0);
                default:
                    return 0.0;
            }
        }

        private static List<double> ExtractMainGrid(VxtPreviewPlan plan, Scope scope)
        {
            var values = new List<double>();
            foreach (var line in plan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var a = Transform2.ToLocal(line.A, scope.Radians);
                var b = Transform2.ToLocal(line.B, scope.Radians);
                if (Math.Abs(a.Y - b.Y) > 0.5) continue;
                var y = (a.Y + b.Y) * 0.5;
                var minX = Math.Min(a.X, b.X);
                var maxX = Math.Max(a.X, b.X);
                if (y < scope.Domain.MinY - 0.5 || y > scope.Domain.MaxY + 0.5) continue;
                if (maxX < scope.Domain.MinX + 0.5 || minX > scope.Domain.MaxX - 0.5) continue;
                values.Add(y);
            }
            return UniqueSort(values, 0.5);
        }

        private static List<CheckInterval> CollectChecks(Scope scope)
        {
            var checks = new List<CheckInterval>();
            var levels = scope.Polygon.Select(p => p.X)
                .Concat(new[] { scope.Domain.MinX, scope.Domain.MaxX })
                .Where(x => x >= scope.Domain.MinX - Tol && x <= scope.Domain.MaxX + Tol);
            var xs = UniqueSort(levels, 0.5);
            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var bandA = Math.Max(xs[i], scope.Domain.MinX);
                var bandB = Math.Min(xs[i + 1], scope.Domain.MaxX);
                if (bandB - bandA <= 2.0) continue;
                var samples = new[]
                {
                    bandA + 0.25 * (bandB - bandA),
                    bandA + 0.50 * (bandB - bandA),
                    bandA + 0.75 * (bandB - bandA)
                };
                foreach (var fixedX in samples)
                {
                    foreach (var segment in PolygonScanline.ClipVertical(scope.Polygon, fixedX))
                    {
                        var a = Math.Max(Math.Min(segment.A.Y, segment.B.Y), scope.Domain.MinY);
                        var b = Math.Min(Math.Max(segment.A.Y, segment.B.Y), scope.Domain.MaxY);
                        if (b - a > 1.0)
                            checks.Add(new CheckInterval(a, b, bandA, bandB));
                    }
                }
            }
            return checks;
        }

        private static List<double> RebalanceGlobalGrid(
            List<double> source,
            List<CheckInterval> checks,
            Box2 domain,
            VxtSettings settings,
            List<Box2> obstacles)
        {
            var current = UniqueSort(source, 0.5);
            var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
            for (var pass = 0; pass < 4; pass++)
            {
                var changed = false;
                foreach (var check in checks)
                {
                    if (IsMaxSafe(current, check.A, check.B, settings.MainMaxSpacing, maxEdge)) continue;
                    var candidate = TryRebalanceInterval(current, check, checks, domain, settings, obstacles, maxEdge);
                    if (candidate != null && !SameGrid(current, candidate))
                    {
                        current = candidate;
                        changed = true;
                    }
                }
                if (!changed) break;
            }
            return GridStepsAreMultiples(current, settings.MainBalanceStep, Tol) ? current : source;
        }

        private static List<double> TryRebalanceInterval(
            List<double> grid,
            CheckInterval check,
            List<CheckInterval> allChecks,
            Box2 domain,
            VxtSettings settings,
            List<Box2> obstacles,
            double maxEdge)
        {
            var xs = PointsInInterval(grid, check.A, check.B);
            if (xs.Count == 0) return null;

            var rawMoves = new List<Tuple<double, double>>();
            var latticeOrigin = grid[0];
            var maxStep = FloorMultiple(settings.MainMaxSpacing, settings.MainBalanceStep);
            var first = xs[0];
            var last = xs[xs.Count - 1];

            if (first - check.A > maxEdge + Tol)
            {
                var target = SnapAtOrBelow(check.A + maxEdge, latticeOrigin, settings.MainBalanceStep);
                if (target >= check.A - Tol) rawMoves.Add(Tuple.Create(first, target));
            }
            if (check.B - last > maxEdge + Tol)
            {
                var target = SnapAtOrAbove(check.B - maxEdge, latticeOrigin, settings.MainBalanceStep);
                if (target <= check.B + Tol) rawMoves.Add(Tuple.Create(last, target));
            }
            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var left = xs[i];
                var right = xs[i + 1];
                if (right - left <= settings.MainMaxSpacing + Tol) continue;
                rawMoves.Add(Tuple.Create(left, right - maxStep));
                rawMoves.Add(Tuple.Create(right, left + maxStep));
            }

            var oldInvalid = CountInvalid(grid, allChecks, settings.MainMaxSpacing, maxEdge);
            List<double> best = null;
            var bestDistance = double.MaxValue;
            foreach (var move in rawMoves)
            {
                var candidate = ReplaceOne(grid, move.Item1, move.Item2);
                if (candidate == null || candidate.Count != grid.Count) continue;
                if (!GridStepsAreMultiples(candidate, settings.MainBalanceStep, Tol)) continue;
                if (!GlobalMaxSafe(candidate, domain.MinY, domain.MaxY, settings.MainMaxSpacing, maxEdge)) continue;
                if (!GridClear(candidate, obstacles)) continue;
                if (!IsMaxSafe(candidate, check.A, check.B, settings.MainMaxSpacing, maxEdge)) continue;
                if (!ChecksPreserved(grid, candidate, allChecks, settings.MainMaxSpacing, maxEdge)) continue;
                var invalid = CountInvalid(candidate, allChecks, settings.MainMaxSpacing, maxEdge);
                if (invalid >= oldInvalid) continue;
                var distance = Math.Abs(move.Item2 - move.Item1);
                if (distance < bestDistance - Eps)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static VxtPreviewPlan CloneForMainStrategy(VxtPreviewPlan source)
        {
            var clone = new VxtPreviewPlan();
            clone.Lines.AddRange(source.Lines);
            clone.Texts.AddRange(source.Texts);
            clone.HangerPoints.AddRange(source.HangerPoints);
            clone.Dimensions.AddRange(source.Dimensions);
            clone.MainSegmentCount = source.MainSegmentCount;
            clone.FurringSegmentCount = source.FurringSegmentCount;
            clone.HangerCount = source.HangerCount;
            clone.DimensionSegmentCount = source.DimensionSegmentCount;
            clone.Quality = source.Quality;
            return clone;
        }

        private static void CopyMainAndHangers(VxtPreviewPlan target, VxtPreviewPlan source)
        {
            target.Lines.RemoveAll(x => x.Kind == PreviewLineKind.Main || x.Kind == PreviewLineKind.Hanger);
            target.Lines.AddRange(source.Lines.Where(x =>
                x.Kind == PreviewLineKind.Main || x.Kind == PreviewLineKind.Hanger));
            target.HangerPoints.Clear();
            target.HangerPoints.AddRange(source.HangerPoints);
            target.MainSegmentCount = source.MainSegmentCount;
            target.HangerCount = source.HangerCount;
        }

        private static StrategyScore ScoreMainStrategy(
            VxtPreviewPlan candidate,
            Scope scope,
            VxtSettings settings,
            List<Box2> obstacles)
        {
            var mains = candidate.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToList();
            var violations = CountMainConstraintViolations(candidate, scope, settings);

            if (settings.UseAvoidance && obstacles != null && obstacles.Count > 0)
            {
                foreach (var line in mains)
                {
                    var a = Transform2.ToLocal(line.A, scope.Radians);
                    var b = Transform2.ToLocal(line.B, scope.Radians);
                    if (Math.Abs(a.Y - b.Y) > 0.5 ||
                        !LocalSegmentClear(new Segment2(a, b), obstacles))
                        violations++;
                }
            }

            var totalLength = mains.Sum(x => x.A.DistanceTo(x.B));
            var spacingSlack = CalculateSpacingSlack(candidate, scope, settings);
            return new StrategyScore(
                violations,
                mains.Count,
                candidate.HangerPoints.Count,
                totalLength,
                spacingSlack);
        }

        private static int CountMainConstraintViolations(
            VxtPreviewPlan candidate,
            Scope scope,
            VxtSettings settings)
        {
            var violations = 0;
            foreach (var check in CollectChecks(scope))
            {
                var sampleX = (check.BandA + check.BandB) * 0.5;
                var ys = UniqueSort(candidate.Lines
                    .Where(x => x.Kind == PreviewLineKind.Main)
                    .Select(x => new
                    {
                        A = Transform2.ToLocal(x.A, scope.Radians),
                        B = Transform2.ToLocal(x.B, scope.Radians)
                    })
                    .Where(x => Math.Abs(x.A.Y - x.B.Y) <= 0.5 &&
                                sampleX >= Math.Min(x.A.X, x.B.X) - Tol &&
                                sampleX <= Math.Max(x.A.X, x.B.X) + Tol)
                    .Select(x => (x.A.Y + x.B.Y) * 0.5)
                    .Where(y => y >= check.A - Tol && y <= check.B + Tol), 0.5);
                violations += CountIntervalViolations(ys, check.A, check.B, settings);
            }
            return violations;
        }

        private static int CountIntervalViolations(
            IReadOnlyList<double> grid,
            double a,
            double b,
            VxtSettings settings)
        {
            if (grid == null || grid.Count == 0) return 1;
            var violations = 0;
            var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
            var firstEdge = grid[0] - a;
            var lastEdge = b - grid[grid.Count - 1];
            if (firstEdge < settings.MainMinEdgeOffset - Tol || firstEdge > maxEdge + Tol) violations++;
            if (lastEdge < settings.MainMinEdgeOffset - Tol || lastEdge > maxEdge + Tol) violations++;
            for (var i = 0; i + 1 < grid.Count; i++)
            {
                var gap = grid[i + 1] - grid[i];
                if (gap < settings.MainMinSpacing - Tol || gap > settings.MainMaxSpacing + Tol)
                    violations++;
            }
            return violations;
        }

        private static double CalculateSpacingSlack(
            VxtPreviewPlan candidate,
            Scope scope,
            VxtSettings settings)
        {
            var slack = 0.0;
            foreach (var check in CollectChecks(scope))
            {
                var sampleX = (check.BandA + check.BandB) * 0.5;
                var ys = UniqueSort(candidate.Lines
                    .Where(x => x.Kind == PreviewLineKind.Main)
                    .Select(x => new
                    {
                        A = Transform2.ToLocal(x.A, scope.Radians),
                        B = Transform2.ToLocal(x.B, scope.Radians)
                    })
                    .Where(x => Math.Abs(x.A.Y - x.B.Y) <= 0.5 &&
                                sampleX >= Math.Min(x.A.X, x.B.X) - Tol &&
                                sampleX <= Math.Max(x.A.X, x.B.X) + Tol)
                    .Select(x => (x.A.Y + x.B.Y) * 0.5)
                    .Where(y => y >= check.A - Tol && y <= check.B + Tol), 0.5);
                for (var i = 0; i + 1 < ys.Count; i++)
                    slack += Math.Max(0.0, settings.MainMaxSpacing - (ys[i + 1] - ys[i]));
            }
            return slack;
        }

        private sealed class StrategyScore
        {
            public StrategyScore(
                int violationCount,
                int mainCount,
                int hangerCount,
                double mainLength,
                double spacingSlack)
            {
                ViolationCount = violationCount;
                MainCount = mainCount;
                HangerCount = hangerCount;
                MainLength = mainLength;
                SpacingSlack = spacingSlack;
            }

            public int ViolationCount { get; }
            public int MainCount { get; }
            public int HangerCount { get; }
            public double MainLength { get; }
            public double SpacingSlack { get; }

            public bool IsBetterThan(StrategyScore other)
            {
                if (other == null) return true;
                if (ViolationCount != other.ViolationCount)
                    return ViolationCount < other.ViolationCount;
                if (MainCount != other.MainCount)
                    return MainCount < other.MainCount;
                if (HangerCount != other.HangerCount)
                    return HangerCount < other.HangerCount;
                if (Math.Abs(MainLength - other.MainLength) > Tol)
                    return MainLength < other.MainLength;
                if (Math.Abs(SpacingSlack - other.SpacingSlack) > Tol)
                    return SpacingSlack < other.SpacingSlack;
                return false;
            }
        }

        private static void RebuildRegionalMains(
            VxtPreviewPlan plan,
            Scope scope,
            IReadOnlyList<Segment2> segments,
            VxtSettings settings,
            List<Box2> obstacles)
        {
            plan.Lines.RemoveAll(x => x.Kind == PreviewLineKind.Main || x.Kind == PreviewLineKind.Hanger);
            plan.HangerPoints.Clear();
            plan.MainSegmentCount = 0;
            plan.HangerCount = 0;

            foreach (var segment in segments ?? new Segment2[0])
            {
                if (Math.Abs(segment.B.X - segment.A.X) <= MinDrawLength) continue;
                if (settings.UseAvoidance && !LocalSegmentClear(segment, obstacles)) continue;
                AddMainLine(plan, segment, scope.Radians);
                if (settings.DrawHangers)
                    AddHangers(plan, segment, scope, settings, obstacles);
            }
        }

        private static void RebuildGlobalMains(
            VxtPreviewPlan plan,
            Scope scope,
            List<double> grid,
            VxtSettings settings,
            List<Box2> obstacles)
        {
            plan.Lines.RemoveAll(x => x.Kind == PreviewLineKind.Main || x.Kind == PreviewLineKind.Hanger);
            plan.HangerPoints.Clear();
            plan.MainSegmentCount = 0;
            plan.HangerCount = 0;

            foreach (var y in grid)
            {
                if (y <= scope.Domain.MinY + 2.0 || y >= scope.Domain.MaxY - 2.0) continue;
                foreach (var raw in PolygonScanline.ClipHorizontal(scope.Polygon, y))
                {
                    Segment2 segment;
                    if (!TryTrimHorizontal(raw, scope.Domain, out segment)) continue;
                    if (settings.UseAvoidance && !LocalSegmentClear(segment, obstacles)) continue;
                    AddMainLine(plan, segment, scope.Radians);
                    if (settings.DrawHangers)
                        AddHangers(plan, segment, scope, settings, obstacles);
                }
            }
        }

        private static void AddLocalRepairs(
            VxtPreviewPlan plan,
            Scope scope,
            List<double> baseGrid,
            VxtSettings settings,
            List<Box2> obstacles)
        {
            if (!settings.UseLocalMainAdd) return;
            var checks = CollectChecks(scope);
            if (checks.Count == 0) return;
            var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
            var specs = new List<LocalSpec>();

            foreach (var band in checks.GroupBy(c => BandKey(c.BandA, c.BandB)))
            {
                var first = band.First();
                var bandGrid = new List<double>(baseGrid);
                var bandChecks = band.ToList();
                foreach (var check in bandChecks)
                {
                    if (!IsMaxSafe(bandGrid, check.A, check.B, settings.MainMaxSpacing, maxEdge))
                        bandGrid = RepairInterval(bandGrid, check.A, check.B, settings, maxEdge);
                }
                bandGrid = PruneAdded(bandGrid, baseGrid, bandChecks, settings.MainMaxSpacing, maxEdge);
                bandGrid = ResolveAddedAgainstObstacles(
                    bandGrid, baseGrid, bandChecks, scope, first.BandA, first.BandB,
                    settings, obstacles, maxEdge);

                foreach (var y in bandGrid)
                {
                    if (!ContainsNear(baseGrid, y, 0.5))
                        AddOrMergeSpec(specs, y, first.BandA, first.BandB);
                }
            }

            foreach (var spec in specs)
            {
                foreach (var raw in PolygonScanline.ClipHorizontal(scope.Polygon, spec.Y))
                {
                    var x1 = Math.Max(Math.Min(raw.A.X, raw.B.X), Math.Max(scope.Domain.MinX, spec.X1));
                    var x2 = Math.Min(Math.Max(raw.A.X, raw.B.X), Math.Min(scope.Domain.MaxX, spec.X2));
                    if (x2 - x1 <= MinDrawLength) continue;
                    if (x2 - x1 < settings.MinLocalMainLength - Tol) continue;
                    var segment = new Segment2(new Point2(x1, spec.Y), new Point2(x2, spec.Y));
                    if (settings.UseAvoidance && !LocalSegmentClear(segment, obstacles)) continue;
                    if (MainLineExists(plan, segment, scope.Radians)) continue;
                    AddMainLine(plan, segment, scope.Radians);
                    if (settings.DrawHangers)
                        AddHangers(plan, segment, scope, settings, obstacles);
                }
            }
        }

        private static List<double> ResolveAddedAgainstObstacles(
            List<double> grid,
            List<double> baseGrid,
            List<CheckInterval> checks,
            Scope scope,
            double bandA,
            double bandB,
            VxtSettings settings,
            List<Box2> obstacles,
            double maxEdge)
        {
            var output = UniqueSort(grid, 0.5);
            if (!settings.UseAvoidance || obstacles == null || obstacles.Count == 0 ||
                settings.MainBalanceStep <= Eps)
                return output;

            var added = output.Where(x => !ContainsNear(baseGrid, x, 0.5)).ToList();
            foreach (var original in added)
            {
                if (LocalBandPositionClear(scope, original, bandA, bandB, settings, obstacles)) continue;

                double? replacement = null;
                var maxSteps = Math.Max(1, (int)Math.Ceiling(settings.MainMaxSpacing / settings.MainBalanceStep) + 1);
                for (var stepIndex = 1; stepIndex <= maxSteps && !replacement.HasValue; stepIndex++)
                {
                    var delta = stepIndex * settings.MainBalanceStep;
                    var candidates = new[] { original - delta, original + delta };
                    foreach (var candidateY in candidates)
                    {
                        if (candidateY <= scope.Domain.MinY + 2.0 || candidateY >= scope.Domain.MaxY - 2.0) continue;
                        if (!LocalBandPositionClear(scope, candidateY, bandA, bandB, settings, obstacles)) continue;

                        var candidateGrid = ReplaceOne(output, original, candidateY);
                        if (candidateGrid == null || candidateGrid.Count != output.Count) continue;
                        if (!GridStepsAreMultiples(candidateGrid, settings.MainBalanceStep, Tol)) continue;
                        if (!checks.All(c => IsMaxSafe(candidateGrid, c.A, c.B, settings.MainMaxSpacing, maxEdge))) continue;

                        replacement = candidateY;
                        break;
                    }
                }

                if (replacement.HasValue)
                {
                    var moved = ReplaceOne(output, original, replacement.Value);
                    if (moved != null) output = moved;
                }
            }

            return output;
        }

        private static bool LocalBandPositionClear(
            Scope scope,
            double y,
            double bandA,
            double bandB,
            VxtSettings settings,
            List<Box2> obstacles)
        {
            var foundDrawable = false;
            foreach (var raw in PolygonScanline.ClipHorizontal(scope.Polygon, y))
            {
                var x1 = Math.Max(Math.Min(raw.A.X, raw.B.X), Math.Max(scope.Domain.MinX, bandA));
                var x2 = Math.Min(Math.Max(raw.A.X, raw.B.X), Math.Min(scope.Domain.MaxX, bandB));
                if (x2 - x1 <= MinDrawLength) continue;
                if (x2 - x1 < settings.MinLocalMainLength - Tol) continue;
                foundDrawable = true;
                var segment = new Segment2(new Point2(x1, y), new Point2(x2, y));
                if (!LocalSegmentClear(segment, obstacles)) return false;
            }
            return foundDrawable;
        }

        private static bool LocalSegmentClear(Segment2 segment, IEnumerable<Box2> obstacles)
        {
            foreach (var box in obstacles ?? Enumerable.Empty<Box2>())
                if (box.IntersectsHorizontal(segment.A.Y, segment.A.X, segment.B.X, Tol))
                    return false;
            return true;
        }

        private static List<double> RepairInterval(List<double> source, double a, double b, VxtSettings settings, double maxEdge)
        {
            var original = UniqueSort(source, 0.5);
            var output = new List<double>(original);
            var xs = PointsInInterval(output, a, b);

            if (xs.Count == 0)
            {
                foreach (var x in SolveEmptyIntervalOnLattice(output, a, b, settings, maxEdge))
                    AddUnique(output, x, 0.5);
            }
            else
            {
                var first = xs[0];
                if (first - a > maxEdge + Tol)
                {
                    foreach (var x in BuildEdgeFillPoints(a, first, true, settings, maxEdge))
                        AddUnique(output, x, 0.5);
                }

                output = UniqueSort(output, 0.5);
                xs = PointsInInterval(output, a, b);
                var i = 0;
                while (i + 1 < xs.Count)
                {
                    var lo = xs[i];
                    var hi = xs[i + 1];
                    if (hi - lo > settings.MainMaxSpacing + Tol)
                    {
                        foreach (var x in SplitMultipleGap(lo, hi, settings.MainMaxSpacing, settings.MainBalanceStep))
                            AddUnique(output, x, 0.5);
                        output = UniqueSort(output, 0.5);
                        xs = PointsInInterval(output, a, b);
                    }
                    i++;
                }

                xs = PointsInInterval(output, a, b);
                if (xs.Count > 0)
                {
                    var last = xs[xs.Count - 1];
                    if (b - last > maxEdge + Tol)
                    {
                        foreach (var x in BuildEdgeFillPoints(b, last, false, settings, maxEdge))
                            AddUnique(output, x, 0.5);
                    }
                }
            }

            output = UniqueSort(output, 0.5);
            return GridStepsAreMultiples(output, settings.MainBalanceStep, Tol) ? output : original;
        }

        private static IEnumerable<double> SolveEmptyIntervalOnLattice(
            List<double> baseGrid,
            double a,
            double b,
            VxtSettings settings,
            double maxEdge)
        {
            var result = new List<double>();
            if (b - a <= 1.0 || settings.MainBalanceStep <= Eps) return result;

            if (baseGrid.Count == 0)
            {
                var layout = SmartLayout1D.Calculate(
                    b - a,
                    settings.MainMaxSpacing,
                    settings.MainMinSpacing,
                    maxEdge,
                    0.0,
                    settings.MainBalanceStep,
                    MainLayoutMode.BalancedTwoEnds);
                return layout == null ? result : layout.Positions(a);
            }

            var origin = baseGrid[0];
            var maxStep = FloorMultiple(settings.MainMaxSpacing, settings.MainBalanceStep);
            if (maxStep <= Eps) return result;
            var first = SnapAtOrBelow(a + maxEdge, origin, settings.MainBalanceStep);
            while (first < a - Tol) first += settings.MainBalanceStep;
            if (first > a + maxEdge + Tol) return result;
            result.Add(first);
            var current = first;
            while (b - current > maxEdge + Tol)
            {
                var next = current + maxStep;
                if (next >= b - Tol) break;
                result.Add(next);
                current = next;
            }
            return result;
        }

        private static IEnumerable<double> BuildEdgeFillPoints(
            double boundary,
            double fixedBar,
            bool leftSide,
            VxtSettings settings,
            double maxEdge)
        {
            var d = Math.Abs(fixedBar - boundary);
            var result = new List<double>();
            if (d <= maxEdge + Tol || settings.MainBalanceStep <= Eps) return result;

            var maxDisc = FloorMultiple(settings.MainMaxSpacing, settings.MainBalanceStep);
            var minDisc = CeilMultiple(settings.MainMinSpacing, settings.MainBalanceStep);
            if (maxDisc <= Eps) return result;
            var maxUnits = RoundUnits(maxDisc, settings.MainBalanceStep);
            var minUnits = Math.Max(1, RoundUnits(minDisc, settings.MainBalanceStep));
            var startK = Math.Max(1, (int)Math.Ceiling(Math.Max(0.0, d - maxEdge) / maxDisc - Eps));
            var maxK = Math.Max(startK, (int)Math.Floor(d / settings.MainBalanceStep + Eps));

            int? chosenK = null;
            int? totalUnits = null;
            var chosenMinUnits = minUnits;
            for (var densePass = 0; densePass < 2 && !chosenK.HasValue; densePass++)
            {
                var minU = densePass == 0 ? minUnits : 1;
                for (var k = startK; k <= maxK; k++)
                {
                    var lower = Math.Max(k * minU * settings.MainBalanceStep, d - maxEdge);
                    var upper = Math.Min(k * maxDisc, d - settings.MainMinEdgeOffset);
                    var units = SelectTotalUnits(lower, upper, settings.MainBalanceStep, k, minU, maxUnits);
                    if (!units.HasValue) continue;
                    chosenK = k;
                    totalUnits = units;
                    chosenMinUnits = minU;
                    break;
                }
            }
            if (!chosenK.HasValue || !totalUnits.HasValue) return result;

            var balanced = BuildBalancedUnits(chosenK.Value, totalUnits.Value, chosenMinUnits, maxUnits);
            if (balanced == null) return result;
            var steps = balanced.Select(u => u * settings.MainBalanceStep).ToArray();
            var edge = d - steps.Sum();
            if (edge < settings.MainMinEdgeOffset - Tol || edge > maxEdge + Tol) return result;

            if (leftSide)
            {
                var current = boundary + edge;
                result.Add(current);
                for (var i = 0; i + 1 < steps.Length; i++)
                {
                    current += steps[i];
                    result.Add(current);
                }
            }
            else
            {
                var current = boundary - edge;
                result.Add(current);
                for (var i = 0; i + 1 < steps.Length; i++)
                {
                    current -= steps[i];
                    result.Add(current);
                }
            }
            return result;
        }

        private static IEnumerable<double> SplitMultipleGap(double lo, double hi, double maxSpacing, double increment)
        {
            var result = new List<double>();
            var d = hi - lo;
            if (d <= maxSpacing + Tol || increment <= Eps || !StepIsMultiple(d, increment, Tol)) return result;
            var maxDisc = FloorMultiple(maxSpacing, increment);
            var maxUnits = RoundUnits(maxDisc, increment);
            var totalUnits = RoundUnits(d, increment);
            if (maxUnits <= 0 || totalUnits <= 0) return result;
            var segmentCount = Math.Max(2, (int)Math.Ceiling((double)totalUnits / maxUnits - Eps));
            var units = BuildBalancedUnits(segmentCount, totalUnits, 1, maxUnits);
            if (units == null) return result;
            var current = lo;
            for (var i = 0; i + 1 < units.Count; i++)
            {
                current += units[i] * increment;
                result.Add(current);
            }
            return result;
        }

        private static List<double> PruneAdded(
            List<double> grid,
            List<double> baseGrid,
            List<CheckInterval> checks,
            double maxSpacing,
            double maxEdge)
        {
            var output = UniqueSort(grid, 0.5);
            var added = output.Where(x => !ContainsNear(baseGrid, x, 0.5)).ToList();
            foreach (var x in added)
            {
                var test = RemoveOne(output, x, 0.5);
                if (checks.All(c => IsMaxSafe(test, c.A, c.B, maxSpacing, maxEdge)))
                    output = test;
            }
            return output;
        }

        private static void AddMainLine(VxtPreviewPlan plan, Segment2 local, double radians)
        {
            var a = Transform2.ToWorld(local.A, radians);
            var b = Transform2.ToWorld(local.B, radians);
            plan.Lines.Add(new PreviewLine(a, b, PreviewLineKind.Main));
            plan.MainSegmentCount++;
        }

        private static void AddHangers(VxtPreviewPlan plan, Segment2 main, Scope scope, VxtSettings settings, List<Box2> obstacles)
        {
            var minX = Math.Min(main.A.X, main.B.X);
            var maxX = Math.Max(main.A.X, main.B.X);
            var length = maxX - minX;
            if (length <= MinDrawLength) return;

            var oneSide = settings.HangerLayout == HangerLayoutMode.OneSideFollowFurring;
            var layout = SmartLayout1D.Calculate(
                length,
                settings.HangerMaxSpacing,
                settings.HangerMinSpacing,
                settings.HangerMaxEdgeOffset,
                settings.HangerMinEdgeOffset,
                settings.HangerBalanceStep,
                oneSide ? MainLayoutMode.OneSide : MainLayoutMode.BalancedTwoEnds,
                reverse: oneSide && scope.FurringFromFarEdge);
            if (layout == null) return;

            IReadOnlyList<double> coordinates = layout.Positions(minX);
            var intervals = obstacles
                .Where(b => main.A.Y >= b.MinY - Eps && main.A.Y <= b.MaxY + Eps)
                .Select(b => Tuple.Create(b.MinX, b.MaxX))
                .ToList();
            if (settings.UseAvoidance && intervals.Count > 0)
            {
                coordinates = SmartLayout1D.AdjustGrid(
                    coordinates,
                    intervals,
                    minX,
                    maxX,
                    settings.HangerMinSpacing,
                    settings.HangerMaxSpacing,
                    settings.HangerMinEdgeOffset,
                    settings.HangerMaxEdgeOffset + Math.Max(0.0, settings.HangerEdgeTolerance),
                    settings.HangerBalanceStep);
            }

            foreach (var x in coordinates)
            {
                if (x <= minX + 2.0 || x >= maxX - 2.0) continue;
                var localPoint = new Point2(x, main.A.Y);
                var world = Transform2.ToWorld(localPoint, scope.Radians);
                if (plan.HangerPoints.Any(p => p.DistanceTo(world) < 0.01)) continue;
                plan.HangerPoints.Add(world);
                plan.HangerCount++;
                AddHangerCross(plan, localPoint, scope.Radians);
            }
        }

        private static void AddHangerCross(VxtPreviewPlan plan, Point2 local, double radians)
        {
            const double half = 45.0;
            var a1 = Transform2.ToWorld(new Point2(local.X - half, local.Y), radians);
            var b1 = Transform2.ToWorld(new Point2(local.X + half, local.Y), radians);
            var a2 = Transform2.ToWorld(new Point2(local.X, local.Y - half), radians);
            var b2 = Transform2.ToWorld(new Point2(local.X, local.Y + half), radians);
            plan.Lines.Add(new PreviewLine(a1, b1, PreviewLineKind.Hanger));
            plan.Lines.Add(new PreviewLine(a2, b2, PreviewLineKind.Hanger));
        }

        private static List<Box2> TransformMainObstacles(VxtLayoutContext context, Scope scope, VxtSettings settings)
        {
            var result = new List<Box2>();
            foreach (var source in context.GeneralObstacles.Concat(context.MainObstacles))
            {
                var box = TransformBox(source, scope.Radians).Expand(settings.UseAvoidance ? settings.ClearanceDistance : 0.0);
                if (box.Intersects(scope.Domain)) result.Add(box);
            }
            return result;
        }

        private static bool IsMaxSafe(List<double> grid, double a, double b, double maxSpacing, double maxEdge)
        {
            var xs = PointsInInterval(grid, a, b);
            if (xs.Count == 0) return false;
            if (xs[0] - a > maxEdge + Tol) return false;
            if (b - xs[xs.Count - 1] > maxEdge + Tol) return false;
            for (var i = 0; i + 1 < xs.Count; i++)
                if (xs[i + 1] - xs[i] > maxSpacing + Tol) return false;
            return true;
        }

        private static bool GlobalMaxSafe(List<double> grid, double min, double max, double maxSpacing, double maxEdge)
        {
            if (grid == null || grid.Count == 0) return false;
            var xs = UniqueSort(grid, 0.5);
            if (xs[0] - min > maxEdge + Tol) return false;
            if (max - xs[xs.Count - 1] > maxEdge + Tol) return false;
            for (var i = 0; i + 1 < xs.Count; i++)
                if (xs[i + 1] - xs[i] > maxSpacing + Tol) return false;
            return true;
        }

        private static bool ChecksPreserved(List<double> oldGrid, List<double> newGrid, IEnumerable<CheckInterval> checks, double maxSpacing, double maxEdge)
        {
            foreach (var c in checks)
                if (IsMaxSafe(oldGrid, c.A, c.B, maxSpacing, maxEdge) && !IsMaxSafe(newGrid, c.A, c.B, maxSpacing, maxEdge))
                    return false;
            return true;
        }

        private static int CountInvalid(List<double> grid, IEnumerable<CheckInterval> checks, double maxSpacing, double maxEdge)
            => checks.Count(c => !IsMaxSafe(grid, c.A, c.B, maxSpacing, maxEdge));

        private static bool GridClear(IEnumerable<double> grid, IEnumerable<Box2> obstacles)
        {
            foreach (var y in grid)
                foreach (var box in obstacles)
                    if (y > box.MinY + Tol && y < box.MaxY - Tol)
                        return false;
            return true;
        }

        private static bool MainLineExists(VxtPreviewPlan plan, Segment2 local, double radians)
        {
            var worldA = Transform2.ToWorld(local.A, radians);
            var worldB = Transform2.ToWorld(local.B, radians);
            foreach (var line in plan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                if ((line.A.DistanceTo(worldA) < 0.01 && line.B.DistanceTo(worldB) < 0.01) ||
                    (line.A.DistanceTo(worldB) < 0.01 && line.B.DistanceTo(worldA) < 0.01))
                    return true;
            }
            return false;
        }

        private static bool TryTrimHorizontal(Segment2 raw, Box2 domain, out Segment2 result)
        {
            var a = Math.Max(Math.Min(raw.A.X, raw.B.X), domain.MinX);
            var b = Math.Min(Math.Max(raw.A.X, raw.B.X), domain.MaxX);
            result = new Segment2(new Point2(a, raw.A.Y), new Point2(b, raw.A.Y));
            return b - a > MinDrawLength && raw.A.Y >= domain.MinY - Eps && raw.A.Y <= domain.MaxY + Eps;
        }

        private static void AddOrMergeSpec(List<LocalSpec> specs, double y, double x1, double x2)
        {
            if (x1 > x2) { var t = x1; x1 = x2; x2 = t; }
            var merged = true;
            while (merged)
            {
                merged = false;
                for (var i = specs.Count - 1; i >= 0; i--)
                {
                    var s = specs[i];
                    if (Math.Abs(s.Y - y) <= 0.5 && x1 <= s.X2 + 1.0 && x2 >= s.X1 - 1.0)
                    {
                        x1 = Math.Min(x1, s.X1);
                        x2 = Math.Max(x2, s.X2);
                        specs.RemoveAt(i);
                        merged = true;
                    }
                }
            }
            specs.Add(new LocalSpec(y, x1, x2));
        }

        private static List<double> PointsInInterval(IEnumerable<double> grid, double a, double b)
            => UniqueSort(grid.Where(x => x >= a - Tol && x <= b + Tol), 0.5);

        private static List<double> ReplaceOne(List<double> grid, double oldValue, double newValue)
        {
            var output = new List<double>(grid.Count);
            var replaced = false;
            foreach (var x in grid)
            {
                if (!replaced && Math.Abs(x - oldValue) <= 0.5)
                {
                    output.Add(newValue);
                    replaced = true;
                }
                else output.Add(x);
            }
            if (!replaced) return null;
            output = UniqueSort(output, 0.5);
            return output.Count == grid.Count ? output : null;
        }

        private static List<double> RemoveOne(List<double> grid, double value, double tolerance)
        {
            var result = new List<double>();
            var removed = false;
            foreach (var x in grid)
            {
                if (!removed && Math.Abs(x - value) <= tolerance) removed = true;
                else result.Add(x);
            }
            return result;
        }

        private static bool GridStepsAreMultiples(IReadOnlyList<double> grid, double increment, double tolerance)
        {
            if (grid == null || increment <= Eps) return false;
            for (var i = 1; i < grid.Count; i++)
                if (!StepIsMultiple(grid[i] - grid[i - 1], increment, tolerance)) return false;
            return true;
        }

        private static bool StepIsMultiple(double value, double increment, double tolerance)
        {
            if (increment <= Eps) return false;
            var q = Math.Abs(value) / increment;
            var n = Math.Round(q);
            return Math.Abs(Math.Abs(value) - n * increment) <= tolerance;
        }

        private static int? SelectTotalUnits(double lower, double upper, double increment, int count, int minUnits, int maxUnits)
        {
            if (increment <= Eps || count <= 0 || lower > upper + Tol) return null;
            var lo = (int)Math.Ceiling((lower - Tol) / increment);
            var hi = (int)Math.Floor((upper + Tol) / increment);
            lo = Math.Max(lo, count * minUnits);
            hi = Math.Min(hi, count * maxUnits);
            if (lo > hi) return null;
            var uniformLow = Math.Max(minUnits, (int)Math.Ceiling((double)lo / count));
            var uniformHigh = Math.Min(maxUnits, (int)Math.Floor((double)hi / count));
            return uniformHigh >= uniformLow ? count * uniformHigh : hi;
        }

        private static IReadOnlyList<int> BuildBalancedUnits(int count, int totalUnits, int minUnits, int maxUnits)
        {
            if (count <= 0 || totalUnits < count * minUnits || totalUnits > count * maxUnits) return null;
            var baseUnits = totalUnits / count;
            var extra = totalUnits - baseUnits * count;
            if (baseUnits < minUnits || baseUnits > maxUnits || (extra > 0 && baseUnits >= maxUnits)) return null;
            var result = Enumerable.Repeat(baseUnits, count).ToArray();
            if (extra <= 0) return result;
            for (var j = 0; j < extra; j++)
            {
                var index = (int)Math.Floor(((j + 0.5) * count) / extra);
                if (index >= count) index = count - 1;
                result[index]++;
            }
            return result;
        }

        private static int RoundUnits(double value, double increment) => (int)Math.Round(value / increment);
        private static double FloorMultiple(double value, double increment) => Math.Floor((value + 1e-10) / increment) * increment;
        private static double CeilMultiple(double value, double increment) => Math.Ceiling((value - 1e-10) / increment) * increment;

        private static double SnapAtOrBelow(double value, double origin, double increment)
        {
            if (increment <= Eps) return value;
            return origin + Math.Floor((value - origin + 1e-10) / increment) * increment;
        }

        private static double SnapAtOrAbove(double value, double origin, double increment)
        {
            if (increment <= Eps) return value;
            return origin + Math.Ceiling((value - origin - 1e-10) / increment) * increment;
        }

        private static List<double> UniqueSort(IEnumerable<double> values, double tolerance)
        {
            var sorted = (values ?? Enumerable.Empty<double>()).Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).OrderBy(x => x).ToList();
            var result = new List<double>();
            foreach (var x in sorted)
                if (result.Count == 0 || Math.Abs(result[result.Count - 1] - x) > tolerance)
                    result.Add(x);
            return result;
        }

        private static void AddUnique(List<double> values, double value, double tolerance)
        {
            if (!ContainsNear(values, value, tolerance)) values.Add(value);
        }

        private static bool ContainsNear(IEnumerable<double> values, double value, double tolerance)
            => values.Any(x => Math.Abs(x - value) <= tolerance);

        private static bool SameGrid(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (Math.Abs(a[i] - b[i]) > 0.5) return false;
            return true;
        }

        private static string BandKey(double a, double b) => Math.Round(a, 2) + "|" + Math.Round(b, 2);

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

        private static double NormalizeDegrees(double value)
        {
            value %= 360.0;
            if (value < 0.0) value += 360.0;
            return value;
        }

        private sealed class Scope
        {
            public Scope(List<Point2> polygon, Box2 domain, double radians, bool furringFromFarEdge)
            {
                Polygon = polygon;
                Domain = domain;
                Radians = radians;
                FurringFromFarEdge = furringFromFarEdge;
            }
            public List<Point2> Polygon { get; }
            public Box2 Domain { get; }
            public double Radians { get; }
            public bool FurringFromFarEdge { get; }
        }

        private sealed class CheckInterval
        {
            public CheckInterval(double a, double b, double bandA, double bandB)
            {
                A = a;
                B = b;
                BandA = bandA;
                BandB = bandB;
            }
            public double A { get; }
            public double B { get; }
            public double BandA { get; }
            public double BandB { get; }
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
            public double X1 { get; }
            public double X2 { get; }
        }
    }
}
