using System;
using System.Collections.Generic;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Multi-boundary adapter. Legacy keeps the certified V6.7.x builder untouched;
    /// opt-in Pro profiles use the separate Pro builders and quality telemetry.
    /// Every selected closed polyline is still solved independently before merge.
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

            var merged = new VxtPreviewPlan();
            var legacyBuilder = new VxtPreviewPlanBuilder();
            var proBuilder = settings.OptimizationMode == VxtOptimizationMode.Legacy
                ? null
                : new VxtProPreviewPlanBuilder();
            var qualities = new List<VxtPlanQuality>();
            var count = 0;

            foreach (var boundary in boundaries)
            {
                if (boundary == null) continue;
                var boundaryContext = BuildBoundaryContext(context, count);
                VxtPreviewPlan part;

                if (proBuilder == null)
                {
                    part = legacyBuilder.Build(boundary, settings, boundaryContext);
                    VxtConcaveMainPostProcessor.Apply(boundary, settings, boundaryContext, part);
                }
                else if (settings.MainDirection == MainDirectionMode.Auto)
                {
                    // Auto Pro tries dominant polygon directions and applies concave post-process
                    // before scoring each candidate. Do not post-process the winner a second time.
                    part = VxtProAutoDirectionPlanBuilder.Build(boundary, settings, boundaryContext);
                }
                else
                {
                    part = proBuilder.Build(boundary, settings, boundaryContext);
                    VxtConcaveMainPostProcessor.Apply(boundary, settings, boundaryContext, part);
                    var angle = ResolveDirectionDegrees(settings);
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
                count++;
            }

            if (count == 0)
                throw new InvalidOperationException("Không có Polyline kín hợp lệ để rải xương.");

            if (qualities.Count > 0)
                merged.Quality = VxtProPlanQualityEvaluator.Aggregate(qualities);
            return merged;
        }

        private static double ResolveDirectionDegrees(VxtSettings settings)
        {
            switch (settings.MainDirection)
            {
                case MainDirectionMode.Vertical: return 90.0;
                case MainDirectionMode.TwoPoints:
                case MainDirectionMode.RectangleRegions:
                    return settings.DirectionDegrees;
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
