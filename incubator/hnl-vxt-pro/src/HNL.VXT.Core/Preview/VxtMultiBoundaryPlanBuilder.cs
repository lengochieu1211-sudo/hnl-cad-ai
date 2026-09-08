using System;
using System.Collections.Generic;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// HNL VXT V6.7.6.15 parity adapter: build every selected closed polyline independently,
    /// run StrictMultiple/PostProcess on that ceiling area, then merge only the resulting
    /// preview/create entities. This deliberately avoids a shared bounding box between
    /// disconnected ceiling areas.
    /// </summary>
    public static class VxtMultiBoundaryPlanBuilder
    {
        public static VxtPreviewPlan Build(
            IEnumerable<Boundary2> boundaries,
            VxtSettings settings,
            VxtLayoutContext context)
        {
            if (boundaries == null) throw new ArgumentNullException(nameof(boundaries));
            context = context ?? new VxtLayoutContext();

            var merged = new VxtPreviewPlan();
            var builder = new VxtPreviewPlanBuilder();
            var count = 0;

            foreach (var boundary in boundaries)
            {
                if (boundary == null) continue;
                var boundaryContext = BuildBoundaryContext(context, count);
                var part = builder.Build(boundary, settings, boundaryContext);
                VxtConcaveMainPostProcessor.Apply(boundary, settings, boundaryContext, part);
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
            return merged;
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
