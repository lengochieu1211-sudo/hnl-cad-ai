using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Keeps every user-facing XC / XP / Ty / DIM color marker in sync with the same
    /// ACI settings consumed by CAD Preview / Create resources.
    ///
    /// This covers both:
    /// - the short swatches inside the Preview legend; and
    /// - the tall markers beside the main section titles.
    ///
    /// The old XAML colors were hard-coded and could disagree with the actual layer/ACI.
    /// </summary>
    public static class VxtPalettePreviewLegend
    {
        private sealed class Swatches
        {
            public Rectangle PreviewMain;
            public Rectangle PreviewFurring;
            public Rectangle PreviewHanger;
            public Rectangle PreviewDimension;

            public Rectangle SectionMain;
            public Rectangle SectionFurring;
            public Rectangle SectionHanger;
            public Rectangle SectionDimension;
        }

        public static void Apply(VxtPaletteView view)
        {
            if (view == null || view.ViewModel == null) return;

            var swatches = FindSwatches(view);
            if (swatches == null) return;

            UpdateAll(swatches, view.ViewModel, view);
            view.ViewModel.PropertyChanged += (sender, args) =>
            {
                if (args == null || string.IsNullOrEmpty(args.PropertyName) ||
                    args.PropertyName == nameof(VxtPaletteViewModel.MainColorIndex) ||
                    args.PropertyName == nameof(VxtPaletteViewModel.FurringColorIndex) ||
                    args.PropertyName == nameof(VxtPaletteViewModel.HangerColorIndex) ||
                    args.PropertyName == nameof(VxtPaletteViewModel.DimensionColorIndex))
                {
                    UpdateAll(swatches, view.ViewModel, view);
                }
            };
        }

        private static void UpdateAll(Swatches swatches, VxtPaletteViewModel vm, FrameworkElement resourceRoot)
        {
            SetRole(swatches.PreviewMain, swatches.SectionMain, vm.MainColorIndex, "XC", resourceRoot);
            SetRole(swatches.PreviewFurring, swatches.SectionFurring, vm.FurringColorIndex, "XP", resourceRoot);
            SetRole(swatches.PreviewHanger, swatches.SectionHanger, vm.HangerColorIndex, "Ty", resourceRoot);
            SetRole(swatches.PreviewDimension, swatches.SectionDimension, vm.DimensionColorIndex, "DIM", resourceRoot);
        }

        private static void SetRole(
            Rectangle preview,
            Rectangle section,
            double value,
            string label,
            FrameworkElement resourceRoot)
        {
            var index = (short)Math.Max(0, Math.Min(256, Math.Round(value)));
            var brush = ResolveAciBrush(index, resourceRoot);

            if (preview != null)
            {
                preview.Fill = brush;
                preview.ToolTip = label + " • ACI " + index;
            }

            if (section != null)
            {
                section.Fill = brush;
                section.ToolTip = label + " • ACI " + index + " • cùng màu Preview/CAD";
            }
        }

        private static Brush ResolveAciBrush(short index, FrameworkElement resourceRoot)
        {
            // ByBlock/ByLayer are context-dependent inside AutoCAD. Use the active HNL
            // foreground as a neutral marker instead of inventing a misleading fixed color.
            if (index == 0 || index == 256)
                return resourceRoot.TryFindResource("PrimaryText") as Brush ?? Brushes.White;

            byte r;
            byte g;
            byte b;

            switch (index)
            {
                case 1: r = 255; g = 0; b = 0; break;
                case 2: r = 255; g = 255; b = 0; break;
                case 3: r = 0; g = 255; b = 0; break;
                case 4: r = 0; g = 255; b = 255; break;
                case 5: r = 0; g = 0; b = 255; break;
                case 6: r = 255; g = 0; b = 255; break;
                case 7: r = 255; g = 255; b = 255; break;
                case 8: r = 128; g = 128; b = 128; break;
                case 9: r = 192; g = 192; b = 192; break;
                case 250: r = g = b = 51; break;
                case 251: r = g = b = 80; break;
                case 252: r = g = b = 105; break;
                case 253: r = g = b = 130; break;
                case 254: r = g = b = 190; break;
                case 255: r = g = b = 255; break;
                default:
                    if (index >= 10 && index <= 249)
                    {
                        ResolveHueShade(index, out r, out g, out b);
                        break;
                    }
                    r = g = b = 192;
                    break;
            }

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static void ResolveHueShade(short index, out byte r, out byte g, out byte b)
        {
            var group = (index - 10) / 10;
            var shade = (index - 10) % 10;
            var hue = group * 15.0;
            var saturation = (shade % 2 == 0) ? 1.0 : 0.5;

            double value;
            switch (shade / 2)
            {
                case 0: value = 1.0; break;
                case 1: value = 0.8; break;
                case 2: value = 0.6; break;
                case 3: value = 0.5; break;
                default: value = 0.3; break;
            }

            HsvToRgb(hue, saturation, value, out r, out g, out b);
        }

        private static void HsvToRgb(double hue, double saturation, double value, out byte r, out byte g, out byte b)
        {
            var chroma = value * saturation;
            var h = hue / 60.0;
            var x = chroma * (1.0 - Math.Abs(h % 2.0 - 1.0));

            double rr = 0.0;
            double gg = 0.0;
            double bb = 0.0;
            if (h < 1.0) { rr = chroma; gg = x; }
            else if (h < 2.0) { rr = x; gg = chroma; }
            else if (h < 3.0) { gg = chroma; bb = x; }
            else if (h < 4.0) { gg = x; bb = chroma; }
            else if (h < 5.0) { rr = x; bb = chroma; }
            else { rr = chroma; bb = x; }

            var m = value - chroma;
            r = ClampByte((rr + m) * 255.0);
            g = ClampByte((gg + m) * 255.0);
            b = ClampByte((bb + m) * 255.0);
        }

        private static byte ClampByte(double value)
            => (byte)Math.Max(0, Math.Min(255, Math.Round(value)));

        private static Swatches FindSwatches(DependencyObject root)
        {
            var result = new Swatches();
            foreach (var text in Walk<TextBlock>(root))
            {
                if (text == null || string.IsNullOrWhiteSpace(text.Text)) continue;
                var label = text.Text.Trim();
                var rectangle = FindSiblingRectangle(text);
                if (rectangle == null) continue;

                var isPreview = rectangle.Height <= 6.0;
                var isSection = rectangle.Height >= 12.0;

                switch (label)
                {
                    case "XƯƠNG CHÍNH":
                    case "Xương chính":
                        if (isPreview) result.PreviewMain = rectangle;
                        else if (isSection) result.SectionMain = rectangle;
                        break;

                    case "XƯƠNG PHỤ":
                    case "Xương phụ":
                        if (isPreview) result.PreviewFurring = rectangle;
                        else if (isSection) result.SectionFurring = rectangle;
                        break;

                    case "TY TREO":
                    case "Ty treo":
                    case "Ty":
                        if (isPreview) result.PreviewHanger = rectangle;
                        else if (isSection) result.SectionHanger = rectangle;
                        break;

                    case "KÍCH THƯỚC DIM":
                    case "Kích thước DIM":
                    case "DIM":
                        if (isPreview) result.PreviewDimension = rectangle;
                        else if (isSection) result.SectionDimension = rectangle;
                        break;
                }
            }

            return result.PreviewMain == null && result.PreviewFurring == null &&
                   result.PreviewHanger == null && result.PreviewDimension == null &&
                   result.SectionMain == null && result.SectionFurring == null &&
                   result.SectionHanger == null && result.SectionDimension == null
                ? null
                : result;
        }

        private static Rectangle FindSiblingRectangle(TextBlock text)
        {
            var panel = text.Parent as Panel;
            if (panel == null) return null;
            foreach (UIElement child in panel.Children)
            {
                var rectangle = child as Rectangle;
                if (rectangle != null) return rectangle;
            }
            return null;
        }

        private static IEnumerable<T> Walk<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            var seen = new HashSet<DependencyObject>();
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !seen.Add(current)) continue;
                var typed = current as T;
                if (typed != null) yield return typed;

                var panel = current as Panel;
                if (panel != null)
                {
                    foreach (UIElement child in panel.Children)
                        if (child != null) queue.Enqueue(child);
                }

                var border = current as Border;
                if (border?.Child != null) queue.Enqueue(border.Child);

                var headered = current as HeaderedContentControl;
                if (headered != null)
                {
                    var header = headered.Header as DependencyObject;
                    var content = headered.Content as DependencyObject;
                    if (header != null) queue.Enqueue(header);
                    if (content != null) queue.Enqueue(content);
                }
                else
                {
                    var contentControl = current as ContentControl;
                    var content = contentControl?.Content as DependencyObject;
                    if (content != null) queue.Enqueue(content);
                }
            }
        }
    }
}
