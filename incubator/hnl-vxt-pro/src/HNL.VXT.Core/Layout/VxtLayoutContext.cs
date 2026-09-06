using System.Collections.Generic;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Runtime-only geometry passed to the pure layout engine. AutoCAD ObjectIds never
    /// enter Core; the bridge converts equipment extents and manual regions into these values.
    /// </summary>
    public sealed class VxtLayoutContext
    {
        public List<Box2> GeneralObstacles { get; } = new List<Box2>();
        public List<Box2> MainObstacles { get; } = new List<Box2>();
        public List<Box2> FurringObstacles { get; } = new List<Box2>();

        // Backward-compatible region list for a single boundary.
        public List<VxtLayoutRegion> Regions { get; } = new List<VxtLayoutRegion>();

        // V6.7.4 parity for many selected ceiling polylines: each selected boundary owns
        // its own rectangle-region list. Region group index == boundary selection index.
        // This prevents rectangles drawn for ceiling area 1 from leaking into area 2.
        public List<List<VxtLayoutRegion>> BoundaryRegionGroups { get; } = new List<List<VxtLayoutRegion>>();

        /// <summary>
        /// V6.7.4 ask_each does not rotate XP. It chooses which edge the fixed XP grid starts from.
        /// false = Left/Bottom (near local min), true = Right/Top (near local max).
        /// </summary>
        public bool GlobalFurringFromFarEdge { get; set; }

        public bool HasManualRegions => Regions.Count > 0;
    }

    public sealed class VxtLayoutRegion
    {
        public VxtLayoutRegion(Box2 worldBounds, double mainAngleDegrees, bool furringFromFarEdge = false)
        {
            WorldBounds = worldBounds;
            MainAngleDegrees = mainAngleDegrees;
            FurringFromFarEdge = furringFromFarEdge;
        }

        public Box2 WorldBounds { get; }
        public double MainAngleDegrees { get; }
        public bool FurringFromFarEdge { get; set; }
    }
}
