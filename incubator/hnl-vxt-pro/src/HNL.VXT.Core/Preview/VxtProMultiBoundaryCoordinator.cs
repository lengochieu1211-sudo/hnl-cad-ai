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
    /// Pro-only coordinator for two or more ceiling boundaries.
    /// It compares independent Auto directions against a shared construction axis.
    /// Economy never accepts a shared axis with a higher aggregate material index;
    /// Balanced/Conservative allow a very small material premium for cleaner alignment.
    /// A shared direction is rejected when it would rotate any ceiling into the perpendicular
    /// orientation family relative to that ceiling's own stable Auto result.
    /// </summary>
    public static class VxtProMultiBoundaryCoordinator
    {
        private const double Eps = 1e-8;
        private const double AngleTolerance = 1.5;
        private const double SharedOrientationLimit = 45.0;
        private const int MaxSharedCandidates = 24;

        private sealed class Strategy
        {
            public VxtPreviewPlan Plan;
            public double GlobalSortScore;
            public List<double> Directions = new List<double>();
        }

        public static VxtPreviewPlan Build(
            IReadOnlyList<Boundary2> boundaries,
            VxtSettings settings,
            VxtLayoutContext sourceContext)
        {
            if (boundaries == null) throw new ArgumentNullException(nameof(boundaries));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (boundaries.Count < 2)
                throw new ArgumentException("Multi-boundary Pro coordinator requires at least two boundaries.", nameof(boundaries));
            if (settings.OptimizationMode == VxtOptimizationMode.Legacy || settings.MainDirection != MainDirectionMode.Auto)
                throw new ArgumentException("Multi-boundary coordinator is Pro Auto only.", nameof(settings));

            sourceContext = sourceContext ?? new VxtLayoutContext();

            var independent = BuildIndependent(boundaries, settings, sourceContext);
            var shared = BuildBestShared(boundaries, settings, sourceContext);
            var chosen = Choose(independent, shared, settings.OptimizationMode);

            if (chosen?.Plan?.Quality != null)
                AttachGlobalLabel(boundaries, chosen.Plan);
            return chosen?.Plan ?? independent.Plan;
        }

        private static Strategy BuildIndependent(
            IReadOnlyList<Boundary2> boundaries,
            VxtSettings settings,
            VxtLayoutContext sourceContext)
        {
            var parts = new List<VxtPreviewPlan>();
            var directions = new List<double>();
            for (var i = 0; i < boundaries.Count; i++)
            {
                var context = BuildBoundaryContext(sourceContext, i);
                var part = VxtProAutoDirectionPlanBuilder.Build(boundaries[i], settings, context);
                // Multi-ceiling Preview uses one global Quality line only. Remove the per-ceiling
                // labels produced by the single-boundary Auto builder before the plans are merged.
                part.Texts.RemoveAll(x =>
                    x.Text != null && x.Text.StartsWith("HNL Pro Q", StringComparison.Ordinal));
                parts.Add(part);
                if (part.Quality != null)
                    directions.Add(Normalize180(part.Quality.SelectedDirectionDegrees));
            }

            var merged = Merge(parts);
            FinalizeAggregateQuality(merged, parts, usesSharedDirection: false);
            return new Strategy
            {
                Plan = merged,
                GlobalSortScore = GlobalScore(merged.Quality, settings.OptimizationMode),
                Directions = directions
            };
        }

        private static Strategy BuildBestShared(
            IReadOnlyList<Boundary2> boundaries,
            VxtSettings settings,
            VxtLayoutContext sourceContext)
        {
            Strategy best = null;
            foreach (var angle in BuildSharedCandidates(boundaries, settings))
            {
                var parts = new List<VxtPreviewPlan>();
                var failed = false;

                for (var i = 0; i < boundaries.Count; i++)
                {
                    try
                    {
                        var candidateSettings = settings.Clone();
                        candidateSettings.MainDirection = MainDirectionMode.TwoPoints;
                        candidateSettings.DirectionDegrees = angle;
                        var context = BuildBoundaryContext(sourceContext, i);
                        var part = new VxtProPreviewPlanBuilder().Build(boundaries[i], candidateSettings, context);
                        VxtConcaveMainPostProcessor.Apply(boundaries[i], candidateSettings, context, part);
                        part.Quality = VxtProPlanQualityEvaluator.Evaluate(part, candidateSettings, context, angle, 1);
                        parts.Add(part);
                    }
                    catch
                    {
                        failed = true;
                        break;
                    }
                }

                if (failed || parts.Count != boundaries.Count) continue;
                var merged = Merge(parts);
                FinalizeAggregateQuality(merged, parts, usesSharedDirection: true);
                merged.Quality.SelectedDirectionDegrees = Normalize180(angle);
                merged.Quality.DistinctDirectionCount = 1;
                merged.Quality.AlignmentScore100 = 100;

                var strategy = new Strategy
                {
                    Plan = merged,
                    GlobalSortScore = GlobalScore(merged.Quality, settings.OptimizationMode),
                    Directions = Enumerable.Repeat(Normalize180(angle), boundaries.Count).ToList()
                };

                if (best == null || IsLexicographicallyBetter(strategy, best))
                    best = strategy;
            }

            return best;
        }

        private static Strategy Choose(Strategy independent, Strategy shared, VxtOptimizationMode mode)
        {
            if (independent == null) return shared;
            if (shared == null) return independent;

            var a = independent.Plan.Quality;
            var b = shared.Plan.Quality;
            if (b.HardViolationCount < a.HardViolationCount) return shared;
            if (b.HardViolationCount > a.HardViolationCount) return independent;
            if (b.CollisionCount < a.CollisionCount) return shared;
            if (b.CollisionCount > a.CollisionCount) return independent;

            // Never trade a ceiling's natural construction orientation for a shared 90-degree
            // family merely to improve alignment/material score. Shared direction remains useful
            // when all independent Auto results are already reasonably close to that axis.
            if (independent.Directions != null && independent.Directions.Count > 0 &&
                shared.Directions != null && shared.Directions.Count > 0)
            {
                var sharedAngle = shared.Directions[0];
                if (independent.Directions.Any(x =>
                        AngularDistance180(x, sharedAngle) > SharedOrientationLimit + AngleTolerance))
                    return independent;
            }

            var allowance = mode == VxtOptimizationMode.ProEconomy ? 1.0000001
                          : mode == VxtOptimizationMode.ProConservative ? 1.05
                          : 1.02;
            if (b.MaterialIndex > a.MaterialIndex * allowance + 0.1)
                return independent;

            return shared.GlobalSortScore + Eps < independent.GlobalSortScore
                ? shared
                : independent;
        }

        private static bool IsLexicographicallyBetter(Strategy candidate, Strategy current)
        {
            var a = candidate.Plan.Quality;
            var b = current.Plan.Quality;
            if (a.HardViolationCount != b.HardViolationCount)
                return a.HardViolationCount < b.HardViolationCount;
            if (a.CollisionCount != b.CollisionCount)
                return a.CollisionCount < b.CollisionCount;
            return candidate.GlobalSortScore + Eps < current.GlobalSortScore;
        }

        private static double GlobalScore(VxtPlanQuality quality, VxtOptimizationMode mode)
        {
            if (quality == null) return double.MaxValue;
            var alignmentPenalty = mode == VxtOptimizationMode.ProEconomy ? 0.0
                                 : mode == VxtOptimizationMode.ProConservative ? 5000.0
                                 : 2500.0;
            return quality.SortScore + Math.Max(0, quality.DistinctDirectionCount - 1) * alignmentPenalty;
        }

        private static IReadOnlyList<double> BuildSharedCandidates(
            IReadOnlyList<Boundary2> boundaries,
            VxtSettings settings)
        {
            var result = new List<double>();
            foreach (var boundary in boundaries)
            {
                var legacy = ResolveLegacyAutoAngle(boundary, settings.AutoShadowline);
                foreach (var angle in VxtProAutoDirectionPlanBuilder.BuildCandidateAngles(boundary, legacy))
                {
                    var normalized = Normalize180(angle);
                    if (result.Any(x => AngularDistance180(x, normalized) <= AngleTolerance)) continue;
                    result.Add(normalized);
                    if (result.Count >= MaxSharedCandidates) return result;
                }
            }

            return result;
        }

        private static VxtPreviewPlan Merge(IEnumerable<VxtPreviewPlan> parts)
        {
            var merged = new VxtPreviewPlan();
            foreach (var part in parts)
            {
                if (part == null) continue;
                merged.Lines.AddRange(part.Lines);
                merged.Texts.AddRange(part.Texts);
                merged.HangerPoints.AddRange(part.HangerPoints);
                merged.Dimensions.AddRange(part.Dimensions);
                merged.MainSegmentCount += part.MainSegmentCount;
                merged.FurringSegmentCount += part.FurringSegmentCount;
                merged.HangerCount += part.HangerCount;
                merged.DimensionSegmentCount += part.DimensionSegmentCount;
            }
            return merged;
        }

        private static void FinalizeAggregateQuality(
            VxtPreviewPlan merged,
            IReadOnlyList<VxtPreviewPlan> parts,
            bool usesSharedDirection)
        {
            var qualities = parts.Select(x => x?.Quality).Where(x => x != null).ToList();
            merged.Quality = VxtProPlanQualityEvaluator.Aggregate(qualities);
            if (merged.Quality == null) return;

            merged.Quality.BoundaryCount = parts.Count;
            merged.Quality.DistinctDirectionCount = CountDistinctDirections(qualities.Select(x => x.SelectedDirectionDegrees));
            merged.Quality.AlignmentScore100 = Math.Max(0, 100 - Math.Max(0, merged.Quality.DistinctDirectionCount - 1) * 25);
            merged.Quality.UsesSharedDirection = usesSharedDirection && merged.Quality.DistinctDirectionCount == 1;
        }

        private static int CountDistinctDirections(IEnumerable<double> angles)
        {
            var unique = new List<double>();
            foreach (var angle in angles)
            {
                var normalized = Normalize180(angle);
                if (unique.All(x => AngularDistance180(x, normalized) > AngleTolerance))
                    unique.Add(normalized);
            }
            return Math.Max(1, unique.Count);
        }

        private static void AttachGlobalLabel(IReadOnlyList<Boundary2> boundaries, VxtPreviewPlan plan)
        {
            if (plan?.Quality == null || boundaries.Count == 0) return;
            var minX = boundaries.Min(x => x.GetBounds().Min.X);
            var maxY = boundaries.Max(x => x.GetBounds().Max.Y);
            var q = plan.Quality;
            var text = "HNL Pro T\u1ED5ng Q" + q.QualityScore100.ToString(CultureInfo.InvariantCulture) +
                       " | " + q.BoundaryCount.ToString(CultureInfo.InvariantCulture) + " m\u1EA3ng" +
                       " | A" + q.AlignmentScore100.ToString(CultureInfo.InvariantCulture) +
                       " | VT " + (q.MaterialIndex / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) +
                       " | VC " + q.CollisionCount.ToString(CultureInfo.InvariantCulture);
            if (q.UsesSharedDirection)
                text += " | H\u01B0\u1EDBng chung " + q.SelectedDirectionDegrees.ToString("0.#", CultureInfo.InvariantCulture) + "\u00B0";
            else
                text += " | " + q.DistinctDirectionCount.ToString(CultureInfo.InvariantCulture) + " h\u01B0\u1EDBng";
            plan.Texts.Add(new PreviewText(new Point2(minX, maxY + 260.0), text, PreviewLineKind.Direction));
        }

        private static double ResolveLegacyAutoAngle(Boundary2 boundary, bool shadowline)
        {
            var bounds = boundary.GetBounds();
            var width = bounds.Max.X - bounds.Min.X;
            var height = bounds.Max.Y - bounds.Min.Y;
            var wide = width > height;
            return shadowline ? (wide ? 0.0 : 90.0) : (wide ? 90.0 : 0.0);
        }

        private static double AngularDistance180(double a, double b)
        {
            var d = Math.Abs(Normalize180(a) - Normalize180(b));
            return Math.Min(d, 180.0 - d);
        }

        private static double Normalize180(double degrees)
        {
            degrees %= 180.0;
            if (degrees < 0.0) degrees += 180.0;
            return degrees;
        }

        private static VxtLayoutContext BuildBoundaryContext(VxtLayoutContext source, int boundaryIndex)
        {
            var local = new VxtLayoutContext
            {
                GlobalFurringFromFarEdge = boundaryIndex >= 0 && boundaryIndex < source.BoundaryFurringFromFarEdges.Count
                    ? source.BoundaryFurringFromFarEdges[boundaryIndex]
                    : source.GlobalFurringFromFarEdge
            };
            local.GeneralObstacles.AddRange(source.GeneralObstacles);
            local.MainObstacles.AddRange(source.MainObstacles);
            local.FurringObstacles.AddRange(source.FurringObstacles);

            if (source.BoundaryRegionGroups.Count > 0)
            {
                if (boundaryIndex >= 0 && boundaryIndex < source.BoundaryRegionGroups.Count)
                {
                    var regions = source.BoundaryRegionGroups[boundaryIndex];
                    if (regions != null) local.Regions.AddRange(regions);
                }
                else
                {
                    local.Regions.AddRange(source.Regions);
                }
            }
            else
            {
                local.Regions.AddRange(source.Regions);
            }

            return local;
        }
    }
}
