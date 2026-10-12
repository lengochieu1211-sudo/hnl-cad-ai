using System;
using System.Collections.Generic;
using System.Linq;
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
        // The selected door family is resolved into model-space positions for this DWG only.
        public IReadOnlyList<Point2> DoorCandidates { get; set; } = Array.Empty<Point2>();
        public string DoorBlockName { get; set; } = string.Empty;

        // When OFF, selected existing HNL hatch supplies the grid; never create a duplicate.
        public bool DrawHatch { get; set; } = true;
        // Preview/Create toggles use the same effective settings.
        public bool DrawDimensions { get; set; } = true;
        // Per-boundary DWG hatch phase; null in the normal Polyline/Create workflow.
        public Point2? ExistingHatchOrigin { get; set; }
        public double? ExistingHatchAngleRadians { get; set; }

        public bool DrawHangers { get; set; } = true;
        // Keep zero Max/edge values as system defaults (1200/1220 and 300/305).
        // Ty minimums are SOFT; maximums are HARD, exactly like concealed ceiling.
        public double HangerMinSpacing { get; set; } = 700.0;
        public double HangerMaxSpacing { get; set; }
        public double HangerMinEdgeOffset { get; set; }
        public double HangerMaxEdgeOffset { get; set; }
        public double HangerBalanceStep { get; set; } = 50.0;
        // Historic setting retained for older sessions; new UI uses Min/Max edge.
        public double HangerEdgeTarget { get; set; }

        public LayInDimensionMode DimensionMode { get; set; } = LayInDimensionMode.GridAndEdges;
        // 0 = auto: dimension all consecutive whole modules on each axis.
        // Positive values remain supported for legacy/test compatibility.
        public int GroupedDimensionCount { get; set; } = 0;
        public LayInHorizontalDimSide HorizontalDimSide { get; set; } = LayInHorizontalDimSide.Auto;
        public LayInVerticalDimSide VerticalDimSide { get; set; } = LayInVerticalDimSide.Auto;
        public double HorizontalDimDistance { get; set; } = 1200.0;
        public double VerticalDimDistance { get; set; } = 1200.0;
        // Opt-in outer overall DIM row; default preserves the existing dimension chains.
        public bool AddOverallDimension { get; set; } = false;
        public double DimensionRowSpacing { get; set; } = 350.0;
        // Opt-in DIM of actual Ty coordinates along each main T segment.
        public bool DimensionHangers { get; set; } = false;
        public LayInHorizontalDimSide HangerDimSide { get; set; } = LayInHorizontalDimSide.Auto;
        public double HangerDimDistance { get; set; } = 180.0;

        public string HatchLayer { get; set; } = "HNL-CF-LAYIN";
        public short HatchColorIndex { get; set; } = 7;
        public string HatchLinetype { get; set; } = "Continuous";
        public string HatchLineweight { get; set; } = "25";
        public string HangerLayer { get; set; } = "HNL-CF-HANGER";
        public short HangerColorIndex { get; set; } = 2;
        public string HangerLinetype { get; set; } = "Continuous";
        public string HangerLineweight { get; set; } = "25";
        // Same preferred hanger block as concealed ceiling; CAD falls back to a
        // circle only when this standard block is absent from the current DWG.
        public const string DefaultHangerBlockName = "AP_DIEM TY 06.2026";
        public string HangerBlockName { get; set; } = DefaultHangerBlockName;
        public string DimensionLayer { get; set; } = "HNL-CF-DIM";
        public short DimensionColorIndex { get; set; } = 4;
        public string DimensionLinetype { get; set; } = "Continuous";
        public string DimensionLineweight { get; set; } = "25";
        // Empty = use current drawing DimStyle. Never set db.Dimstyle globally.
        public string DimensionStyle { get; set; } = string.Empty;
        // One start-panel symbol per ceiling region. Blank custom block uses
        // the built-in HNL_CF_FIRST_TILE geometry, sized to the selected module.
        public bool DrawStartTileBlock { get; set; } = true;
        public string StartMarkerLayer { get; set; } = "HNL-CF-START";
        public short StartMarkerColorIndex { get; set; } = 3;
        public string StartMarkerLinetype { get; set; } = "Continuous";
        public string StartMarkerLineweight { get; set; } = "25";
        public string StartMarkerBlockName { get; set; } = string.Empty;

        public LayInCeilingSettings Clone()
        {
            var copy = (LayInCeilingSettings)MemberwiseClone();
            copy.DoorCandidates = DoorCandidates == null
                ? Array.Empty<Point2>() : DoorCandidates.ToArray();
            return copy;
        }
    }
}
