namespace HNL.VXT.Core.Models
{
    public sealed class VxtSettings
    {
        public bool DrawMain { get; set; } = true;
        public double MainMinSpacing { get; set; } = 700.0;
        public double MainMaxSpacing { get; set; } = 1000.0;
        public double MainMinEdgeOffset { get; set; } = 300.0;
        public double MainMaxEdgeOffset { get; set; } = 400.0;

        public bool DrawFurring { get; set; } = true;
        public double FurringSpacing { get; set; } = 1220.0 / 3.0;

        public bool DrawHangers { get; set; } = true;
        public double HangerMinSpacing { get; set; } = 700.0;
        public double HangerMaxSpacing { get; set; } = 1000.0;
        public double HangerMinEdgeOffset { get; set; } = 300.0;
        public double HangerMaxEdgeOffset { get; set; } = 400.0;

        public bool DimMain { get; set; } = true;
        public bool DimFurring { get; set; } = true;
        public bool DimHanger { get; set; } = true;

        public DimensionPosition MainDimPosition { get; set; } = DimensionPosition.Auto;
        public DimensionPosition FurringDimPosition { get; set; } = DimensionPosition.Auto;
        public DimensionPosition HangerDimPosition { get; set; } = DimensionPosition.Auto;

        public double DimensionDistance { get; set; } = 500.0;
        public double DimensionSpacing { get; set; } = 350.0;

        public double DirectionDegrees { get; set; } = 0.0;

        public VxtSettings Clone() => (VxtSettings)MemberwiseClone();

        public bool IsValid(out string error)
        {
            if (MainMinSpacing <= 0 || MainMaxSpacing <= 0 || MainMinSpacing > MainMaxSpacing)
            {
                error = "Khoảng cách Xương chính không hợp lệ.";
                return false;
            }

            if (FurringSpacing <= 0)
            {
                error = "Khoảng cách Xương phụ phải lớn hơn 0.";
                return false;
            }

            if (HangerMinSpacing <= 0 || HangerMaxSpacing <= 0 || HangerMinSpacing > HangerMaxSpacing)
            {
                error = "Khoảng cách Ty treo không hợp lệ.";
                return false;
            }

            if (DimensionDistance < 0 || DimensionSpacing <= 0)
            {
                error = "Khoảng cách DIM không hợp lệ.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
