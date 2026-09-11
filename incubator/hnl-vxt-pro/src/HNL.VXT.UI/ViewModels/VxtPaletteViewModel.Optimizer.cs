using HNL.VXT.Core.Models;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        public string[] OptimizationModeOptions { get; } =
        {
            "Bố trí truyền thống",
            "Cân đối",
            "Tiết kiệm vật tư",
            "Ưu tiên ổn định"
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
                case VxtOptimizationMode.ProBalanced: return "Cân đối";
                case VxtOptimizationMode.ProEconomy: return "Tiết kiệm vật tư";
                case VxtOptimizationMode.ProConservative: return "Ưu tiên ổn định";
                default: return "Bố trí truyền thống";
            }
        }

        private static VxtOptimizationMode TextToOptimizationMode(string value)
        {
            switch (value)
            {
                case "Cân đối": return VxtOptimizationMode.ProBalanced;
                case "Tiết kiệm vật tư": return VxtOptimizationMode.ProEconomy;
                case "Ưu tiên ổn định": return VxtOptimizationMode.ProConservative;
                default: return VxtOptimizationMode.Legacy;
            }
        }
    }
}
