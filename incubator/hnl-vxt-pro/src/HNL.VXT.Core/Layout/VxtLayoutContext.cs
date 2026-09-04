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
        public List<VxtLayoutRegion> Regions { get; } = new List<VxtLayoutRegion>();

        public bool HasManualRegions => Regions.Count > 0;
    }

    public sealed class VxtLayoutRegion
    {
        public VxtLayoutRegion(Box2 worldBounds, double mainAngleDegrees)
        {
            WorldBounds = worldBounds;
            MainAngleDegrees = mainAngleDegrees;
        }

        public Box2 WorldBounds { get; }
        public double MainAngleDegrees { get; }
    }
}
