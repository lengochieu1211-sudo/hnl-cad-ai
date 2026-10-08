using HNL.VXT.Core.Models;

namespace HNL.VXT.UI.Hosting
{
    public interface IVxtHostBridge
    {
        bool IsDarkTheme { get; }
        string[] GetLinetypeNames();
        string[] GetDimStyleNames();
        void SelectBoundary();
        void PickBoundaryPoint();
        void PickDirection(MainDirectionMode mode);
        void PickBlock(BlockTarget target);
        void PickEquipment(EquipmentTarget target);
        void PickDimensionPosition(DimensionTarget target);
        void RequestPreview(VxtSettings settings);
        void ClearPreview();
        string AnalyzeDiagnostics(VxtSettings settings);
        string ExportDiagnostics(VxtSettings settings);
        void RequestRuntimeGolden();
        void HighlightBoundary(int boundaryIndex);
        void RequestCreate();
        void RequestCreateWithWarning();

        void RequestLayInPreview(LayInCeilingSettings settings);
        void ClearLayInPreview();
        void RequestLayInCreate(LayInCeilingSettings settings);
        void PickLayInManualStart(LayInCeilingSettings settings);
        void PickLayInDoor(LayInCeilingSettings settings);
        void PickLayInHangerBlock(LayInCeilingSettings settings);
        void PickLayInStartMarkerBlock(LayInCeilingSettings settings);
        void PickLayInDimensionPosition(bool horizontal, LayInCeilingSettings settings);
    }
}