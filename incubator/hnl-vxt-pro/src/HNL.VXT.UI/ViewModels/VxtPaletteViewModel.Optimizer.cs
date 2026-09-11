using HNL.VXT.Core.Models;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        public string[] OptimizationModeOptions { get; } =
        {
            "Tiêu chuẩn - Tương thích VXT",
            "Nâng cao - Cân đối",
            "Nâng cao - Tiết kiệm vật tư",
            "Nâng cao - Ưu tiên ổn định"
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
                case VxtOptimizationMode.ProBalanced: return "Nâng cao - Cân đối";
                case VxtOptimizationMode.ProEconomy: return "Nâng cao - Tiết kiệm vật tư";
                case VxtOptimizationMode.ProConservative: return "Nâng cao - Ưu tiên ổn định";
                default: return "Tiêu chuẩn - Tương thích VXT";
            }
        }

        private static VxtOptimizationMode TextToOptimizationMode(string value)
        {
            switch (value)
            {
                case "Nâng cao - Cân đối": return VxtOptimizationMode.ProBalanced;
                case "Nâng cao - Tiết kiệm vật tư": return VxtOptimizationMode.ProEconomy;
                case "Nâng cao - Ưu tiên ổn định": return VxtOptimizationMode.ProConservative;
                default: return VxtOptimizationMode.Legacy;
            }
        }
    }
}
