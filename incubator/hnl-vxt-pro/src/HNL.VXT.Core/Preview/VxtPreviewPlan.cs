using System.Collections.Generic;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.Core.Preview
{
    public sealed class VxtPreviewPlan
    {
        public List<PreviewLine> Lines { get; } = new List<PreviewLine>();
        public List<PreviewText> Texts { get; } = new List<PreviewText>();

        // WYSIWYG bridge data. These let the AutoCAD renderer use the same Block/DimStyle
        // that Create will use without polluting Model Space with temporary DB entities.
        public List<Point2> HangerPoints { get; } = new List<Point2>();
        public List<PreviewDimension> Dimensions { get; } = new List<PreviewDimension>();

        public int MainSegmentCount { get; set; }
        public int FurringSegmentCount { get; set; }
        public int HangerCount { get; set; }
        public int DimensionSegmentCount { get; set; }
    }
}
