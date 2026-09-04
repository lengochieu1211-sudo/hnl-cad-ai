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

        public string[] LineweightOptions { get; } =
        {
            "-3", "-2", "-1", "0", "5", "9", "13", "15", "18", "20", "25", "30", "35", "40", "50", "53", "60", "70", "80", "90", "100", "106", "120", "140", "158", "200", "211"
        };

        private void SetColorIndex(Func<short> getter, Action<short> setter, double value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var rounded = (short)Math.Max(0, Math.Min(256, Math.Round(value)));
            if (getter() == rounded) return;
            setter(rounded);
            Changed(propertyName);
        }
    }
}
