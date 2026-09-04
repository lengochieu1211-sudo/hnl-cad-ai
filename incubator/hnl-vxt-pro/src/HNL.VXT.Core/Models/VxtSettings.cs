namespace HNL.VXT.Core.Models
{
    public sealed class VxtSettings
    {
        // Xương chính - parity V6.7.4
        public bool DrawMain = true;
        public bool UseDynamicMainBlock = true;
        public string MainBlockName { get; set; } = "AP_THANH CHINH 06.2026";
        public double MainMinSpacing { get; set; } = 700.0;
        public double MainMaxSpacing { get; set; } = 1000.0;
        public double MainMinEdgeOffset { get; set; } = 300.0;
        public double MainMaxEdgeOffset { get; set; } = 400.0;
        public double MainBalanceStep { get; set; } = 50.0;
        public double MainSkipLimit { get; set; } = 500.0;
        public MainDirectionMode MainDirection { get; set; } = MainDirectionMode.Horizontal;
        public MainLayoutMode MainLayout { get; set; } = MainLayoutMode.BalancedTwoEnds;

        // Xương phụ - parity V6.7.4
        public bool DrawFurring = true;
        public bool UseDynamicFurringBlock = true;
        public string FurringBlockName { get; set; } = "AP_THANH PHU 06.2026";
        public double FurringSpacing { get; set; } = 1220.0 / 3.0;
        public bool AskDirectionEachRegion = false;

        // Ty treo - parity V6.7.4
        public bool DrawHangers = true;
        public string HangerBlockName { get; set; } = "AP_DIEM TY 06.2026";
        public double HangerMinSpacing { get; set; } = 700.0;
        public double HangerMaxSpacing { get; set; } = 1000.0;
        public double HangerMinEdgeOffset { get; set; } = 300.0;
        public double HangerMaxEdgeOffset { get; set; } = 400.0;
        public double HangerBalanceStep { get; set; } = 50.0;
        public HangerLayoutMode HangerLayout { get; set; } = HangerLayoutMode.BalancedTwoEnds;

        // Né thiết bị - parity V6.7.4
        public bool UseAvoidance = true;
        public bool ShiftAllForAvoidance = true;
        public double ClearanceDistance { get; set; } = 20.0;

        // DIM - parity V6.7.4
        public bool AutoDimension = false;
        public bool DimMain = false;
        public bool DimFurring = false;
        public bool DimHanger = false;
        public DimensionPosition MainDimPosition { get; set; } = DimensionPosition.Auto;
        public DimensionPosition FurringDimPosition { get; set; } = DimensionPosition.Auto;
        public DimensionPosition HangerDimPosition { get; set; } = DimensionPosition.Auto;
        public double DimensionDistance { get; set; } = 500.0;
        public double DimensionSpacing { get; set; } = 350.0;

        // Hướng preview theo góc thực tế. Horizontal=0, Vertical=90, TwoPoints cập nhật runtime.
        public double DirectionDegrees { get; set; } = 0.0;

        // Layer / DimStyle defaults retained from V6.7.4 for future Create engine parity.
        public string MainLayer { get; set; } = "AP_TC_THANH CHINH (KT)";
        public short MainColorIndex { get; set; } = 6;
        public string MainLinetype { get; set; } = "Continuous";
        public string MainLineweight { get; set; } = "-3";

        public string FurringLayer { get; set; } = "AP_TC_THANH PHU (KT)";
        public short FurringColorIndex { get; set; } = 4;
        public string FurringLinetype { get; set; } = "Continuous";
        public string FurringLineweight { get; set; } = "-3";

        public string HangerLayer { get; set; } = "AP_TC_DIEM TY (KT)";
        public short HangerColorIndex { get; set; } = 1;
        public string HangerLinetype { get; set; } = "Continuous";
        public string HangerLineweight { get; set; } = "-3";

        public string DimensionLayer { get; set; } = "AP_TC_DIM (KT)";
        public short DimensionColorIndex { get; set; } = 8;
        public string DimensionLinetype { get; set; } = "Continuous";
        public string DimensionLineweight { get; set; } = "-3";
        public string DimensionStyle { get; set; } = string.Empty;

        public VxtSettings Clone() => (VxtSettings)MemberwiseClone();

        public bool IsValid(out string error)
        {
            if (DrawMain)
            {
                if (MainMinSpacing <= 0 || MainMaxSpacing <= 0 || MainMinSpacing > MainMaxSpacing)
                {
                    error = "Khoảng cách tâm Xương chính không hợp lệ.";
                    return false;
                }
                if (MainMinEdgeOffset < 0 || MainMaxEdgeOffset < 0 || MainMinEdgeOffset > MainMaxEdgeOffset)
                {
                    error = "Khoảng cách biên Xương chính không hợp lệ.";
                    return false;
                }
                if (MainBalanceStep <= 0)
                {
                    error = "Bù khoảng cách Xương chính phải lớn hơn 0.";
                    return false;
                }
                if (MainSkipLimit < 0)
                {
                    error = "Giới hạn bỏ Xương chính không được âm.";
                    return false;
                }
            }

            if (DrawFurring && FurringSpacing <= 0)
            {
                error = "Khoảng cách Xương phụ phải lớn hơn 0.";
                return false;
            }

            if (DrawHangers)
            {
                if (HangerMinSpacing <= 0 || HangerMaxSpacing <= 0 || HangerMinSpacing > HangerMaxSpacing)
                {
                    error = "Khoảng cách tâm Ty treo không hợp lệ.";
                    return false;
                }
                if (HangerMinEdgeOffset < 0 || HangerMaxEdgeOffset < 0 || HangerMinEdgeOffset > HangerMaxEdgeOffset)
                {
                    error = "Khoảng cách biên Ty treo không hợp lệ.";
                    return false;
                }
                if (HangerBalanceStep <= 0)
                {
                    error = "Bù khoảng cách Ty treo phải lớn hơn 0.";
                    return false;
                }
            }

            if (UseAvoidance && ClearanceDistance < 0)
            {
                error = "Khoảng hở né thiết bị không được âm.";
                return false;
            }

            if (AutoDimension)
            {
                if (DimensionDistance < 0)
                {
                    error = "Khoảng cách DIM không được âm.";
                    return false;
                }
                if (DimensionSpacing <= 0)
                {
                    error = "Khoảng cách giữa các hàng DIM phải lớn hơn 0.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
