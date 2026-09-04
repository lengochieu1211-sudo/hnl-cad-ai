using HNL.VXT.Core.Models;

namespace HNL.VXT.UI.Hosting
{
    public interface IVxtHostBridge
    {
        bool IsDarkTheme { get; }
        void SelectBoundary();
        void PickDirection();
        void PickDimensionPosition(DimensionTarget target);
        void RequestPreview(VxtSettings settings);
        void ClearPreview();
        void RequestCreate();
    }
}
