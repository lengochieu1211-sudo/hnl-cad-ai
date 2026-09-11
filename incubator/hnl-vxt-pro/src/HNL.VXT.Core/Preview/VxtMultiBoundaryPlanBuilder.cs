using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Multi-boundary adapter. Legacy keeps the certified V6.7.x builder untouched;
    /// opt-in Pro profiles use the separate Pro builders and quality telemetry.
    /// Pro Auto with two or more ceilings compares independent directions with a shared axis.
    /// </summary>
    public static class VxtMultiBoundaryPlanBuilder
    {
        public static VxtPreviewPlan Build(
            IEnumerable<Boundary2> boundaries,
            VxtSettings settings,
            VxtLayoutContext context)
        {
            if (boundaries == null) throw new ArgumentNullException(nameof(boundaries));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            context = context ?? new VxtLayoutContext();

            var boundaryList = boundaries.Where(x => x != null).ToList();
            if (boundaryList.Count == 0)
                throw new InvalidOperationException("Không có Polyline kín hợp lệ để rải xương.");

            if (settings.OptimizationMode != VxtOptimizationMode.Legacy &&
                settings.MainDirection == MainDirectionMode.Auto &&
                boundaryList.Count > 1)
            {
                return VxtProMultiBoundaryCoordinator.Build(boundaryList, settings, context);
            }

            var merged = new VxtPreviewPlan();
            var legacyBuilder = new VxtPreviewPlanBuilder();
            var proBuilder = settings.OptimizationMode == VxtOptimizationMode.Legacy
                ? null
                : new VxtProPreviewPlanBuilder();
            var qualities = new List<VxtPlanQuality>();

            for (var count = 0; count < boundaryList.Count; count++)
            {
                var boundary = boundaryList[count];
                var boundaryContext = BuildBoundaryContext(context, count);
                VxtPreviewPlan part;

                if (proBuilder == null)
                {
                    part = legacyBuilder.Build(boundary, settings, boundaryContext);
                    VxtConcaveMainPostProcessor.Apply(boundary, settings, boundaryContext, part);
                    VxtPostProcessDimensionSynchronizer.Synchronize(
                        boundary, settings, boundaryContext, part, ResolveDirectionDegrees(settings, boundary));
                }
                else if (settings.MainDirection == MainDirectionMode.Auto)
                {
                    // Single-boundary Auto Pro tries dominant polygon directions and applies
                    // concave post-process before scoring each candidate.
                    part = VxtProAutoDirectionPlanBuilder.Build(boundary, settings, boundaryContext);
                }
                else
                {
                    part = proBuilder.Build(boundary, settings, boundaryContext);
                    VxtConcaveMainPostProcessor.Apply(boundary, settings, boundaryContext, part);
                    var angle = ResolveDirectionDegrees(settings, boundary);
                    VxtPostProcessDimensionSynchronizer.Synchronize(boundary, settings, boundaryContext, part, angle);
                    part.Quality = VxtProPlanQualityEvaluator.Evaluate(
                        part, settings, boundaryContext, angle, 1);
                    VxtProPlanQualityEvaluator.AttachCompactPreviewLabel(boundary, part);
                }

                if (part.Quality != null) qualities.Add(part.Quality);
                merged.Lines.AddRange(part.Lines);
                merged.Texts.AddRange(part.Texts);
                merged.HangerPoints.AddRange(part.HangerPoints);
                merged.Dimensions.AddRange(part.Dimensions);
                merged.MainSegmentCount += part.MainSegmentCount;
                merged.FurringSegmentCount += part.FurringSegmentCount;
                merged.HangerCount += part.HangerCount;
                merged.DimensionSegmentCount += part.DimensionSegmentCount;
            }

            if (qualities.Count > 0)
            {
                merged.Quality = VxtProPlanQualityEvaluator.Aggregate(qualities);
                if (merged.Quality != null)
                {
                    merged.Quality.BoundaryCount = boundaryList.Count;
                    // A manually selected/fixed direction is inherently shared by all boundaries.
                    if (settings.OptimizationMode != VxtOptimizationMode.Legacy &&
                        settings.MainDirection != MainDirectionMode.Auto)
                    {
                        merged.Quality.DistinctDirectionCount = 1;
                        merged.Quality.AlignmentScore100 = 100;
                        merged.Quality.UsesSharedDirection = boundaryList.Count > 1;
                    }
                }
            }
            return merged;
        }

        private static double ResolveDirectionDegrees(VxtSettings settings, Boundary2 boundary)
        {
            switch (settings.MainDirection)
            {
                case MainDirectionMode.Vertical: return 90.0;
                case MainDirectionMode.TwoPoints:
                case MainDirectionMode.RectangleRegions:
                    return settings.DirectionDegrees;
                case MainDirectionMode.Auto:
                    var bounds = boundary.GetBounds();
                    var wide = bounds.Max.X - bounds.Min.X > bounds.Max.Y - bounds.Min.Y;
                    return settings.AutoShadowline
                        ? (wide ? 0.0 : 90.0)
                        : (wide ? 90.0 : 0.0);
                default: return 0.0;
            }
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

            // Manual HCN mode stores XP direction directly on every region. Normal modes use
            // the per-boundary GlobalFurringFromFarEdge resolved above.
            if (source.BoundaryRegionGroups.Count > 0)
            {
                if (boundaryIndex >= 0 && boundaryIndex < source.BoundaryRegionGroups.Count)
                {
                    var regions = source.BoundaryRegionGroups[boundaryIndex];
                    if (regions != null) local.Regions.AddRange(regions);
                }
                else
                {
                    // Defensive fallback for contexts assembled by older callers.
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
