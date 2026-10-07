using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HNL.VXT.UI.Controls
{
    /// <summary>
    /// HNL Tool vector icon primitive for compact AutoCAD palette controls.
    /// Presentation only: no settings, solver, CAD database or command state.
    /// Icons use original 24x24 stroke geometry and scale cleanly at high DPI.
    /// </summary>
    public sealed class HnlIcon : Path
    {
        public static readonly DependencyProperty KindProperty =
            DependencyProperty.Register(
                nameof(Kind),
                typeof(string),
                typeof(HnlIcon),
                new PropertyMetadata("Pick", OnKindChanged));

        private static readonly Dictionary<string, Geometry> Icons =
            new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase)
            {
                ["Pick"] = G("M12 2 L12 8 M12 16 L12 22 M2 12 L8 12 M16 12 L22 12 M9 9 L15 9 L15 15 L9 15 Z"),
                ["Settings"] = G("M4 6 L20 6 M9 3 L9 9 M4 12 L20 12 M15 9 L15 15 M4 18 L20 18 M11 15 L11 21"),
                ["Refresh"] = G("M20 7 L20 3 L16 7 M20 7 C18 4.5 15 3 12 3 C7 3 3 7 3 12 C3 17 7 21 12 21 C16 21 19 18.5 20 15 M4 17 L4 21 L8 17"),
                ["Hide"] = G("M3 12 C5.5 7.5 8.5 6 12 6 C15.5 6 18.5 7.5 21 12 C18.5 16.5 15.5 18 12 18 C8.5 18 5.5 16.5 3 12 Z M4 4 L20 20"),
                ["Reset"] = G("M7 6 L3 10 L7 14 M4 10 L12 10 C17 10 20 12.8 20 16.5 C20 20 17 22 12 22 L8 22"),
                ["Create"] = G("M4 4 L20 4 L20 20 L4 20 Z M8 12 L11 15 L17 9"),
                ["Warning"] = G("M12 3 L22 21 L2 21 Z M12 9 L12 14 M12 18 L12.01 18"),
                ["Add"] = G("M12 4 L12 20 M4 12 L20 12"),
                ["Remove"] = G("M4 12 L20 12"),
                ["Up"] = G("M5 15 L12 8 L19 15"),
                ["Down"] = G("M5 9 L12 16 L19 9"),
                ["Folder"] = G("M3 7 L9 7 L11 9 L21 9 L21 20 L3 20 Z"),
                ["Save"] = G("M4 3 L18 3 L21 6 L21 21 L3 21 L3 3 Z M7 3 L7 9 L17 9 L17 3 M7 15 L17 15 L17 21 L7 21 Z"),
                ["Main"] = G("M4 7 L20 7 M4 17 L20 17"),
                ["Furring"] = G("M4 6 L20 6 M4 12 L20 12 M4 18 L20 18"),
                ["Hanger"] = G("M12 3 L12 14 M8 14 L16 14 M9 18 C9 16.3 10.3 15 12 15 C13.7 15 15 16.3 15 18 C15 19.7 13.7 21 12 21 C10.3 21 9 19.7 9 18 Z"),
                ["Mep"] = G("M4 5 L20 5 L20 19 L4 19 Z M8 12 L16 12 M12 8 L12 16"),
                ["Dimension"] = G("M4 7 L4 17 M20 7 L20 17 M4 12 L20 12 M7 9 L4 12 L7 15 M17 9 L20 12 L17 15")
            };

        public HnlIcon()
        {
            Stretch = Stretch.Uniform;
            Fill = Brushes.Transparent;
            StrokeThickness = 1.65;
            StrokeLineJoin = PenLineJoin.Round;
            StrokeStartLineCap = PenLineCap.Round;
            StrokeEndLineCap = PenLineCap.Round;
            SnapsToDevicePixels = true;
            IsHitTestVisible = false;
            UpdateGeometry();
        }

        public string Kind
        {
            get => (string)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HnlIcon)d).UpdateGeometry();
        }

        private void UpdateGeometry()
        {
            Geometry geometry;
            if (string.IsNullOrWhiteSpace(Kind) || !Icons.TryGetValue(Kind, out geometry))
                geometry = Icons["Pick"];
            Data = geometry;
        }

        private static Geometry G(string data)
        {
            var geometry = Geometry.Parse(data);
            if (geometry.CanFreeze) geometry.Freeze();
            return geometry;
        }
    }
}
