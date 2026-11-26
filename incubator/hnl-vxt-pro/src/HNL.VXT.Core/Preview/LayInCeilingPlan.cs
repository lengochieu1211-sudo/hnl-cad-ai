using System.Collections.Generic;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    public readonly struct LayInTeeSegment
    {
        public LayInTeeSegment(Point2 a, Point2 b, LayInTeeKind kind)
        {
            A = a;
            B = b;
            Kind = kind;
        }

        public Point2 A { get; }
        public Point2 B { get; }
        public LayInTeeKind Kind { get; }
        public double Length => A.DistanceTo(B);
    }

    public readonly struct LayInDimensionRun
    {
        public LayInDimensionRun(
            Point2 a,
            Point2 b,
            double module,
            int moduleCount,
            string label,
            Point2? dimensionLinePoint = null)
        {
            A = a;
            B = b;
            Module = module;
            ModuleCount = moduleCount;
            Label = label ?? string.Empty;
            DimensionLinePoint = dimensionLinePoint;
        }

        public Point2 A { get; }
        public Point2 B { get; }
        public double Module { get; }
        public int ModuleCount { get; }
        public string Label { get; }
        public Point2? DimensionLinePoint { get; }
    }

    public sealed class LayInCeilingPlan
    {
        public List<LayInTeeSegment> TeeSegments { get; } = new List<LayInTeeSegment>();
        public List<Point2> HangerPoints { get; } = new List<Point2>();
        public List<LayInDimensionRun> DimensionRuns { get; } = new List<LayInDimensionRun>();

        public double MainAngleRadians { get; set; }
        public Point2 HatchOrigin { get; set; }
        // Physical first full panel, rather than a raw Hatch phase that can lie
        // outside a concave region or in a void.
        public Point2? FirstTileOrigin { get; set; }
        public double FirstTileWidth { get; set; }
        public double FirstTileHeight { get; set; }
        public string HatchPatternName { get; set; } = string.Empty;

        public double ModuleShort { get; set; }
        public double MainTeeSpacing { get; set; }
        public double MainTeeStockLength { get; set; }
        public double LongCrossTeeStockLength { get; set; }
        public double ShortCrossTeeStockLength { get; set; }

        public int MainTeeStockCount { get; set; }
        public int LongCrossTeeCount { get; set; }
        public int ShortCrossTeeCount { get; set; }

        public double MainTeeTotalLength { get; set; }
        public double LongCrossTeeTotalLength { get; set; }
        public double ShortCrossTeeTotalLength { get; set; }
        public double PurchasedLength { get; set; }
        public double WasteLength { get; set; }

        public int MainSegmentCount => TeeSegments.FindAll(x => x.Kind == LayInTeeKind.MainTee).Count;
        public int LongCrossSegmentCount => TeeSegments.FindAll(x => x.Kind == LayInTeeKind.LongCrossTee).Count;
        public int ShortCrossSegmentCount => TeeSegments.FindAll(x => x.Kind == LayInTeeKind.ShortCrossTee).Count;
    }
}
