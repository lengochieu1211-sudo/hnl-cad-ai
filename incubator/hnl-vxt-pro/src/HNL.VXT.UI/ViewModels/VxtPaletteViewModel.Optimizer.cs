using HNL.VXT.Core.Models;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        public string[] OptimizationModeOptions { get; } =
        {
            "Legacy VXT (Golden)",
            "Pro - Cân đối",
            "Pro - Tối ưu vật tư",
            "Pro - Ưu tiên ổn định"
        };

        public string SelectedOptimizationMode
        {
            get => OptimizationModeToText(_settings.OptimizationMode);
            set
            {
                var mode = TextToOptimizationMode(value);
                if (_settings.OptimizationMode == mode) return;
                _settings.OptimizationMode = mode;
                Changed();
            }
        }

        public bool IsProOptimization => _settings.OptimizationMode != VxtOptimizationMode.Legacy;

        private static string OptimizationModeToText(VxtOptimizationMode mode)
        {
            switch (mode)
            {
                case VxtOptimizationMode.ProBalanced: return "Pro - Cân đối";
                case VxtOptimizationMode.ProEconomy: return "Pro - Tối ưu vật tư";
                case VxtOptimizationMode.ProConservative: return "Pro - Ưu tiên ổn định";
                default: return "Legacy VXT (Golden)";
            }
        }

        private static VxtOptimizationMode TextToOptimizationMode(string value)
        {
            switch (value)
            {
                case "Pro - Cân đối": return VxtOptimizationMode.ProBalanced;
                case "Pro - Tối ưu vật tư": return VxtOptimizationMode.ProEconomy;
                case "Pro - Ưu tiên ổn định": return VxtOptimizationMode.ProConservative;
                default: return VxtOptimizationMode.Legacy;
            }
        }
    }
}
