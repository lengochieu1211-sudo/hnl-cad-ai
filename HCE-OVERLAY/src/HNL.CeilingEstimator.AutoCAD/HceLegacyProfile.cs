using System;
using System.Globalization;
using HNL.CeilingEstimator.Core.Models;
namespace HNL.CeilingEstimator.AutoCAD
{
    // Per-DWG option storage: a closed Database does not retain palette state.
    // This is an AutoCAD adapter/view model only; protected DemtcEngine is unchanged.
    internal sealed class HceLegacyProfile
    {
        public int Family = 610;
        public string Module = "M";
        public string Priority = "S";
        public string Direction = "X";
        public string GridMode = "3";
        public bool SnapEnabled = true;
        public double Tolerance = 3.0;
        // 0-height and empty style preserve RC5.3.3 Table formatting unchanged.
        // These are presentation settings, not core tile/packing options.
        public string TableTextStyle = string.Empty;
        public double TableTextHeight = 0.0;
        // Legacy separate height for N/G/L labels placed in the CAD drawing.
        public double LabelTextHeight = 100.0;

        public HceLegacyProfile Clone()
        {
            return (HceLegacyProfile)MemberwiseClone();
        }

        public DemtcOptions ToOptions()
        {
            var n = Family;
            if (n != 600 && n != 610) throw new InvalidOperationException("Invalid ceiling family");
            if (Module != "S" && Module != "D" && Module != "M")
                throw new InvalidOperationException("Invalid module");
            if (GridMode != "1" && GridMode != "2" && GridMode != "3")
                throw new InvalidOperationException("Invalid grid mode");
            if (Tolerance < 0 || Tolerance > 10 || double.IsNaN(Tolerance) || double.IsInfinity(Tolerance))
                throw new InvalidOperationException("Grid tolerance must be within 0..10mm");
            var isLongY = Direction == "Y";
            var longW = isLongY ? n : n * 2;
            var longH = isLongY ? n * 2 : n;
            var small = new StockSpec("S", n, n, n == 600 ? "595x595" : "605x605");
            var large = new StockSpec("D", longW, longH, n == 600 ? "595x1190" : "605x1210");
            var isMixed = Module == "M";
            var largeMain = Module == "D" || (isMixed && Priority == "D");
            return new DemtcOptions
            {
                GridWidth = largeMain ? longW : n,
                GridHeight = largeMain ? longH : n,
                SnapEnabled = SnapEnabled,
                SnapTolerance = Tolerance,
                AllowRotate90 = true,
                MixedMode = isMixed,
                MixedPrimary = largeMain ? MixedPrimaryMode.LargeMain : MixedPrimaryMode.SmallMain,
                // PackSingleStock always uses SmallStock; for a D-only mode it
                // must be the long board, not the default short board.
                SmallStock = Module == "D" ? large : small,
                LargeStock = large
            };
        }

        public string Summary()
        {
            var options = ToOptions();
            return "Lưới " + options.GridWidth.ToString("0", CultureInfo.InvariantCulture) +
                " × " + options.GridHeight.ToString("0", CultureInfo.InvariantCulture) +
                " mm | " + (Module == "M" ? "Kết hợp • " + (Priority == "D" ? "dài chính" : "ngắn chính") :
                 Module == "D" ? "Chỉ tấm dài" : "Chỉ tấm ngắn") +
                " | " + (GridMode == "3" ? "Theo Hatch" : GridMode == "2" ? "Chọn gốc/hướng" : "WCS 0,0");
        }
    }


}
