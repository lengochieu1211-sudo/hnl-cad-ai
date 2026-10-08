using HNL.VXT.Core.Geometry;

namespace HNL.VXT.Core.Models
{
    public enum LayInGridSystem
    {
        Module600x600,
        Module610x610,
        Module600x1200,
        Module610x1220
    }

    public enum LayInMainDirectionMode
    {
        Horizontal,
        Vertical,
        ParallelShortSide,
        ParallelLongSide,
        AutoOptimize
    }

    public enum LayInStartMode
    {
        Balanced,
        ManualStart,
        FromDoor
    }

    public enum LayInDimensionMode
    {
        Off,
        Module,
        Grouped,
        Overall,
        GridAndEdges
    }

    public enum LayInHorizontalDimSide { Auto, Top, Bottom }
    public enum LayInVerticalDimSide { Auto, Left, Right }

    public enum LayInTeeKind
    {
        MainTee,
        LongCrossTee,
        ShortCrossTee
    }

    public sealed class LayInCeilingSettings
    {
        public LayInGridSystem GridSystem { get; set; } = LayInGridSystem.Module600x600;
        public LayInMainDirectionMode MainDirection { get; set; } = LayInMainDirectionMode.AutoOptimize;
        public LayInStartMode StartMode { get; set; } = LayInStartMode.Balanced;

        public Point2? ManualStartPoint { get; set; }
        public Point2? DoorPoint { get; set; }

        public bool DrawHangers { get; set; } = true;
        public double HangerMaxSpacing { get; set; }
        public double HangerEdgeTarget { get; set; }

        public LayInDimensionMode DimensionMode { get; set; } = LayInDimensionMode.GridAndEdges;
        public int GroupedDimensionCount { get; set; } = 12;
        public LayInHorizontalDimSide HorizontalDimSide { get; set; } = LayInHorizontalDimSide.Auto;
        public LayInVerticalDimSide VerticalDimSide { get; set; } = LayInVerticalDimSide.Auto;
        public double HorizontalDimDistance { get; set; } = 1200.0;
        public double VerticalDimDistance { get; set; } = 1200.0;

        public string HatchLayer { get; set; } = "HNL-CF-LAYIN";
        public string HangerLayer { get; set; } = "HNL-CF-HANGER";
        public string HangerBlockName { get; set; } = string.Empty;
        public string DimensionLayer { get; set; } = "HNL-CF-DIM";
        // One start-panel symbol per ceiling region. Blank custom block uses
        // the built-in HNL_CF_FIRST_TILE geometry, sized to the selected module.
        public bool DrawStartTileBlock { get; set; } = true;
        public string StartMarkerBlockName { get; set; } = string.Empty;

        public LayInCeilingSettings Clone()
        {
            return (LayInCeilingSettings)MemberwiseClone();
        }
    }
}
