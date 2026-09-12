using System;
using System.Collections.Generic;
using System.Linq;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        public string MainLayer { get => _settings.MainLayer; set => SetStringSetting(() => _settings.MainLayer, v => _settings.MainLayer = v, value); }
        public double MainColorIndex { get => _settings.MainColorIndex; set => SetColorIndex(() => _settings.MainColorIndex, v => _settings.MainColorIndex = v, value); }
        public string MainLinetype { get => _settings.MainLinetype; set => SetStringSetting(() => _settings.MainLinetype, v => _settings.MainLinetype = v, value); }
        public string MainLineweight { get => _settings.MainLineweight; set => SetStringSetting(() => _settings.MainLineweight, v => _settings.MainLineweight = v, value); }

        public string FurringLayer { get => _settings.FurringLayer; set => SetStringSetting(() => _settings.FurringLayer, v => _settings.FurringLayer = v, value); }
        public double FurringColorIndex { get => _settings.FurringColorIndex; set => SetColorIndex(() => _settings.FurringColorIndex, v => _settings.FurringColorIndex = v, value); }
        public string FurringLinetype { get => _settings.FurringLinetype; set => SetStringSetting(() => _settings.FurringLinetype, v => _settings.FurringLinetype = v, value); }
        public string FurringLineweight { get => _settings.FurringLineweight; set => SetStringSetting(() => _settings.FurringLineweight, v => _settings.FurringLineweight = v, value); }

        public string HangerLayer { get => _settings.HangerLayer; set => SetStringSetting(() => _settings.HangerLayer, v => _settings.HangerLayer = v, value); }
        public double HangerColorIndex { get => _settings.HangerColorIndex; set => SetColorIndex(() => _settings.HangerColorIndex, v => _settings.HangerColorIndex = v, value); }
        public string HangerLinetype { get => _settings.HangerLinetype; set => SetStringSetting(() => _settings.HangerLinetype, v => _settings.HangerLinetype = v, value); }
        public string HangerLineweight { get => _settings.HangerLineweight; set => SetStringSetting(() => _settings.HangerLineweight, v => _settings.HangerLineweight = v, value); }

        public double DimensionColorIndex { get => _settings.DimensionColorIndex; set => SetColorIndex(() => _settings.DimensionColorIndex, v => _settings.DimensionColorIndex = v, value); }
        public string DimensionLinetype { get => _settings.DimensionLinetype; set => SetStringSetting(() => _settings.DimensionLinetype, v => _settings.DimensionLinetype = v, value); }
        public string DimensionLineweight { get => _settings.DimensionLineweight; set => SetStringSetting(() => _settings.DimensionLineweight, v => _settings.DimensionLineweight = v, value); }

        // Friendly selectors used by the compact Layer & DIM surface. The model keeps the
        // original ACI/LineWeight values so Preview/Create and older settings remain binary-
        // compatible; only the presentation layer changes.
        public string SelectedMainColor { get => ColorIndexToText(_settings.MainColorIndex); set => SetQuickColor(() => _settings.MainColorIndex, v => _settings.MainColorIndex = v, value, nameof(SelectedMainColor), nameof(MainColorIndex)); }
        public string SelectedFurringColor { get => ColorIndexToText(_settings.FurringColorIndex); set => SetQuickColor(() => _settings.FurringColorIndex, v => _settings.FurringColorIndex = v, value, nameof(SelectedFurringColor), nameof(FurringColorIndex)); }
        public string SelectedHangerColor { get => ColorIndexToText(_settings.HangerColorIndex); set => SetQuickColor(() => _settings.HangerColorIndex, v => _settings.HangerColorIndex = v, value, nameof(SelectedHangerColor), nameof(HangerColorIndex)); }
        public string SelectedDimensionColor { get => ColorIndexToText(_settings.DimensionColorIndex); set => SetQuickColor(() => _settings.DimensionColorIndex, v => _settings.DimensionColorIndex = v, value, nameof(SelectedDimensionColor), nameof(DimensionColorIndex)); }

        public string SelectedMainLineweight { get => LineweightToText(_settings.MainLineweight); set => SetFriendlyLineweight(() => _settings.MainLineweight, v => _settings.MainLineweight = v, value, nameof(SelectedMainLineweight), nameof(MainLineweight)); }
        public string SelectedFurringLineweight { get => LineweightToText(_settings.FurringLineweight); set => SetFriendlyLineweight(() => _settings.FurringLineweight, v => _settings.FurringLineweight = v, value, nameof(SelectedFurringLineweight), nameof(FurringLineweight)); }
        public string SelectedHangerLineweight { get => LineweightToText(_settings.HangerLineweight); set => SetFriendlyLineweight(() => _settings.HangerLineweight, v => _settings.HangerLineweight = v, value, nameof(SelectedHangerLineweight), nameof(HangerLineweight)); }
        public string SelectedDimensionLineweight { get => LineweightToText(_settings.DimensionLineweight); set => SetFriendlyLineweight(() => _settings.DimensionLineweight, v => _settings.DimensionLineweight = v, value, nameof(SelectedDimensionLineweight), nameof(DimensionLineweight)); }

        public string SelectedDimensionStyle
        {
            get => string.IsNullOrWhiteSpace(_settings.DimensionStyle) ? "Hiện hành" : _settings.DimensionStyle;
            set => DimensionStyle = string.Equals(value, "Hiện hành", StringComparison.Ordinal) ? string.Empty : value;
        }

        public string[] LinetypeOptions
        {
            get
            {
                var names = _host.GetLinetypeNames() ?? Array.Empty<string>();
                if (names.Length == 0) return new[] { "Continuous" };
                return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
            }
        }

        public string[] DimStyleOptions
        {
            get
            {
                var result = new List<string> { "Hiện hành" };
                var names = _host.GetDimStyleNames() ?? Array.Empty<string>();
                result.AddRange(names.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x));
                return result.ToArray();
            }
        }

        // Deliberately small color set: no AutoCAD 256-color dialog and no ACI number typing.
        // These are the nine standard ACI colors most useful for CAD layer conventions.
        public string[] QuickColorOptions { get; } =
        {
            "Đỏ", "Vàng", "Xanh lá", "Cyan", "Xanh dương",
            "Magenta", "Trắng / Đen", "Xám", "Xám nhạt"
        };

        // Display real CAD lineweight units while preserving AutoCAD's integer enum value in
        // VxtSettings. Special values map to Default / ByBlock / ByLayer.
        public string[] LineweightOptions { get; } =
        {
            "Mặc định", "ByBlock", "ByLayer",
            "0.00 mm", "0.05 mm", "0.09 mm", "0.13 mm", "0.15 mm", "0.18 mm", "0.20 mm",
            "0.25 mm", "0.30 mm", "0.35 mm", "0.40 mm", "0.50 mm", "0.53 mm", "0.60 mm",
            "0.70 mm", "0.80 mm", "0.90 mm", "1.00 mm", "1.06 mm", "1.20 mm", "1.40 mm",
            "1.58 mm", "2.00 mm", "2.11 mm"
        };

        private void SetColorIndex(Func<short> getter, Action<short> setter, double value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var rounded = (short)Math.Max(0, Math.Min(256, Math.Round(value)));
            if (getter() == rounded) return;
            setter(rounded);
            Changed(propertyName);
            OnPropertyChanged(ColorIndexPropertyToFriendly(propertyName));
        }

        private void SetQuickColor(Func<short> getter, Action<short> setter, string value, string friendlyPropertyName, string rawPropertyName)
        {
            var color = TextToColorIndex(value, getter());
            if (getter() == color) return;
            setter(color);
            OnPropertyChanged(friendlyPropertyName);
            OnPropertyChanged(rawPropertyName);
            MarkCustom();
            RequestPreview();
        }

        private void SetFriendlyLineweight(Func<string> getter, Action<string> setter, string value, string friendlyPropertyName, string rawPropertyName)
        {
            var raw = TextToLineweight(value, getter());
            if (string.Equals(getter(), raw, StringComparison.Ordinal)) return;
            setter(raw);
            OnPropertyChanged(friendlyPropertyName);
            OnPropertyChanged(rawPropertyName);
            MarkCustom();
            RequestPreview();
        }

        private static string ColorIndexPropertyToFriendly(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(MainColorIndex): return nameof(SelectedMainColor);
                case nameof(FurringColorIndex): return nameof(SelectedFurringColor);
                case nameof(HangerColorIndex): return nameof(SelectedHangerColor);
                case nameof(DimensionColorIndex): return nameof(SelectedDimensionColor);
                default: return string.Empty;
            }
        }

        private static string ColorIndexToText(short value)
        {
            switch (value)
            {
                case 1: return "Đỏ";
                case 2: return "Vàng";
                case 3: return "Xanh lá";
                case 4: return "Cyan";
                case 5: return "Xanh dương";
                case 6: return "Magenta";
                case 7: return "Trắng / Đen";
                case 8: return "Xám";
                case 9: return "Xám nhạt";
                default: return "ACI " + value;
            }
        }

        private static short TextToColorIndex(string value, short fallback)
        {
            switch (value)
            {
                case "Đỏ": return 1;
                case "Vàng": return 2;
                case "Xanh lá": return 3;
                case "Cyan": return 4;
                case "Xanh dương": return 5;
                case "Magenta": return 6;
                case "Trắng / Đen": return 7;
                case "Xám": return 8;
                case "Xám nhạt": return 9;
                default: return fallback;
            }
        }

        private static string LineweightToText(string raw)
        {
            switch (raw)
            {
                case "-3": return "Mặc định";
                case "-2": return "ByBlock";
                case "-1": return "ByLayer";
                default:
                    short value;
                    return short.TryParse(raw, out value) && value >= 0
                        ? (value / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " mm"
                        : raw ?? "Mặc định";
            }
        }

        private static string TextToLineweight(string text, string fallback)
        {
            if (string.Equals(text, "Mặc định", StringComparison.Ordinal)) return "-3";
            if (string.Equals(text, "ByBlock", StringComparison.Ordinal)) return "-2";
            if (string.Equals(text, "ByLayer", StringComparison.Ordinal)) return "-1";

            if (!string.IsNullOrWhiteSpace(text) && text.EndsWith(" mm", StringComparison.Ordinal))
            {
                var number = text.Substring(0, text.Length - 3);
                double millimeters;
                if (double.TryParse(number, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out millimeters))
                    return Math.Round(millimeters * 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return fallback ?? "-3";
        }
    }
}
