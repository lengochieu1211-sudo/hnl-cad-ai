using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HNL.VXT.UI.Controls
{
    /// <summary>
    /// HNL Tool vector icon primitive for compact AutoCAD palette controls.
    /// Presentation only: no settings, solver, CAD database or command state.
    /// All glyphs share one 24x24 rounded-stroke language and scale cleanly at high DPI.
    /// </summary>
    public sealed class HnlIcon : Viewbox
    {
        public static readonly DependencyProperty KindProperty =
            DependencyProperty.Register(
                nameof(Kind),
                typeof(string),
                typeof(HnlIcon),
                new PropertyMetadata("Pick", OnKindChanged));

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(
                nameof(Stroke),
                typeof(Brush),
                typeof(HnlIcon),
                new PropertyMetadata(Brushes.White, OnAppearanceChanged));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(
                nameof(StrokeThickness),
                typeof(double),
                typeof(HnlIcon),
                new PropertyMetadata(1.55, OnAppearanceChanged));

        private static readonly Dictionary<string, Geometry> Icons =
            new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase)
            {
                // CAD selection / common actions.
                ["Pick"] = G("M5 3 L5 19 L9.5 14.5 L12.5 20 L15 18.5 L12 13.5 L18 13.5 Z"),
                ["Settings"] = G("M4 6 L7 6 M11 6 L20 6 M7 4 L11 4 L11 8 L7 8 Z M4 12 L13 12 M17 12 L20 12 M13 10 L17 10 L17 14 L13 14 Z M4 18 L9 18 M13 18 L20 18 M9 16 L13 16 L13 20 L9 20 Z"),
                ["Refresh"] = G("M19 8 L19 4 L15 8 M19 8 C17.4 5.4 14.8 4 12 4 C7.6 4 4 7.6 4 12 M5 16 L5 20 L9 16 M5 16 C6.6 18.6 9.2 20 12 20 C16.4 20 20 16.4 20 12"),
                ["Hide"] = G("M3 12 C5.3 8.5 8.4 6.5 12 6.5 C15.6 6.5 18.7 8.5 21 12 C18.7 15.5 15.6 17.5 12 17.5 C8.4 17.5 5.3 15.5 3 12 M10 10 C11.1 8.9 12.9 8.9 14 10 C15.1 11.1 15.1 12.9 14 14 C12.9 15.1 11.1 15.1 10 14 C8.9 12.9 8.9 11.1 10 10 M4 4 L20 20"),
                ["Reset"] = G("M7 7 L3 7 L3 3 M4 7 C5.6 4.7 8.3 3.5 11.3 3.5 C16.2 3.5 20 7.3 20 12 C20 16.8 16.2 20.5 11.3 20.5 C7.4 20.5 4.2 18.1 3.2 14.7"),
                ["Create"] = G("M4 5 L17 5 L17 18 L4 18 Z M10.5 5 L10.5 18 M4 11.5 L17 11.5 M18 15 L18 21 M15 18 L21 18"),
                ["Warning"] = G("M12 3 L22 21 L2 21 Z M12 9 L12 14 M12 18 L12.01 18"),
                ["Add"] = G("M12 4 L12 20 M4 12 L20 12"),
                ["Remove"] = G("M4 12 L20 12"),
                ["Up"] = G("M5 15 L12 8 L19 15"),
                ["Down"] = G("M5 9 L12 16 L19 9"),
                ["Folder"] = G("M3 7 L9 7 L11 9 L21 9 L21 19 L3 19 Z"),
                ["Save"] = G("M4 4 L18 4 L20 6 L20 20 L4 20 Z M7 4 L7 9 L16 9 L16 4 M8 14 L16 14 L16 20 L8 20 Z"),

                // HNL VXT domain glyphs: distinct silhouettes at 14–16 DIP.
                ["Main"] = G("M4 16 L20 16 M8 6 L8 16 M16 6 L16 16"),
                ["Furring"] = G("M4 8 L20 8 M7 8 L7 19 M12 8 L12 19 M17 8 L17 19"),
                ["Hanger"] = G("M5 4 L19 4 M12 4 L12 18 M9 9 L15 9 M9 12 L15 12 M7 18 L17 18"),
                ["Mep"] = G("M9 9 C10.7 7.3 13.3 7.3 15 9 C16.7 10.7 16.7 13.3 15 15 C13.3 16.7 10.7 16.7 9 15 C7.3 13.3 7.3 10.7 9 9 M3 13 L6 13 L6 6 L18 6 L18 13 L21 13"),
                ["Dimension"] = G("M4 7 L4 17 M20 7 L20 17 M4 12 L20 12 M7 9 L4 12 L7 15 M17 9 L20 12 L17 15"),
                ["Preview"] = G("M3 8 L3 4 L7 4 M17 4 L21 4 L21 8 M3 16 L3 20 L7 20 M17 20 L21 20 L21 16 M6 12 C7.6 9.5 9.6 8.5 12 8.5 C14.4 8.5 16.4 9.5 18 12 C16.4 14.5 14.4 15.5 12 15.5 C9.6 15.5 7.6 14.5 6 12 M10 12 C10 10.9 10.9 10 12 10 C13.1 10 14 10.9 14 12 C14 13.1 13.1 14 12 14 C10.9 14 10 13.1 10 12")
            };

        private readonly Path _path;

        public HnlIcon()
        {
            Stretch = Stretch.Uniform;
            IsHitTestVisible = false;

            _path = new Path
            {
                Fill = Brushes.Transparent,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                SnapsToDevicePixels = true
            };

            Child = _path;
            UpdateGeometry();
            UpdateAppearance();
        }

        public string Kind
        {
            get => (string)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HnlIcon)d).UpdateGeometry();
        }

        private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HnlIcon)d).UpdateAppearance();
        }

        private void UpdateGeometry()
        {
            Geometry geometry;
            if (string.IsNullOrWhiteSpace(Kind) || !Icons.TryGetValue(Kind, out geometry))
                geometry = Icons["Pick"];
            _path.Data = geometry;
        }

        private void UpdateAppearance()
        {
            _path.Stroke = Stroke;
            _path.StrokeThickness = StrokeThickness;
        }

        private static Geometry G(string data)
        {
            var geometry = Geometry.Parse(data);
            if (geometry.CanFreeze) geometry.Freeze();
            return geometry;
        }
    }
}
