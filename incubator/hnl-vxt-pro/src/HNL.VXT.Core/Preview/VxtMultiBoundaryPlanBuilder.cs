using System;
using System.Collections.Generic;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// V6.7.4 parity adapter: build every selected closed polyline independently,
    /// then merge only the resulting preview/create entities. This deliberately
    /// avoids a shared bounding box between disconnected ceiling areas.
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
            // Normal modes and legacy single-boundary callers keep the original context.
            if (source.BoundaryRegionGroups.Count == 0)
                return source;

            var local = new VxtLayoutContext
            {
                GlobalFurringFromFarEdge = source.GlobalFurringFromFarEdge
            };
            local.GeneralObstacles.AddRange(source.GeneralObstacles);
            local.MainObstacles.AddRange(source.MainObstacles);
            local.FurringObstacles.AddRange(source.FurringObstacles);

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
            return local;
        }
    }
}
