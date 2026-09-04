using HNL.VXT.Core.Models;

namespace HNL.VXT.UI.Hosting
{
    public interface IVxtHostBridge
    {
        bool IsDarkTheme { get; }
        void SelectBoundary();
        void PickDirection(MainDirectionMode mode);
        void PickBlock(BlockTarget target);
        void PickEquipment(EquipmentTarget target);
        void PickDimensionPosition(DimensionTarget target);
        void RequestPreview(VxtSettings settings);
        void ClearPreview();
        void ExportDiagnostics(VxtSettings settings);
        void RequestCreate();
    }
}
