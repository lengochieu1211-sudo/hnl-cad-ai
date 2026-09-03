using System.Collections.Generic;

namespace HNL.VXT.Core.Preview
{
    public sealed class VxtPreviewPlan
    {
        public List<PreviewLine> Lines { get; } = new List<PreviewLine>();
        public List<PreviewText> Texts { get; } = new List<PreviewText>();

        public int MainSegmentCount { get; set; }
        public int FurringSegmentCount { get; set; }
        public int HangerCount { get; set; }
        public int DimensionSegmentCount { get; set; }
    }
}
