using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView
    {
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += OnBetaLoaded;
        }

        private void OnBetaLoaded(object sender, RoutedEventArgs e)
        {
            // Run after the existing enhancer has created Layer/UI panels and themed ComboBoxes.
            Dispatcher.BeginInvoke(new Action(() => FinalizeBetaUi(this)), DispatcherPriority.ContextIdle);
        }

        private static void FinalizeBetaUi(DependencyObject root)
        {
            if (root == null) return;

            var primary = FindBrush(root as FrameworkElement, "PrimaryText", Brushes.Black);
            var input = FindBrush(root as FrameworkElement, "InputBackground", Brushes.White);
            var border = FindBrush(root as FrameworkElement, "InputBorder", Brushes.Gray);

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                var text = child as TextBlock;
                if (text != null && !string.IsNullOrWhiteSpace(text.Text) &&
                    (text.Text.StartsWith("Alpha.2:", StringComparison.OrdinalIgnoreCase) ||
                     text.Text.StartsWith("Alpha.5:", StringComparison.OrdinalIgnoreCase)))
                {
                    text.Text = "Beta.1: Preview và Tạo thật dùng chung engine Golden V6.7.4 • Create đã mở để kiểm thử chức năng.";
                }

                var combo = child as ComboBox;
                if (combo != null)
                {
                    combo.Foreground = primary;
                    combo.Background = input;
                    combo.BorderBrush = border;
                    combo.ApplyTemplate();
                    ApplyEditableTextBoxColors(combo, primary, input, border);
                }

                FinalizeBetaUi(child);
            }
        }

        private static void ApplyEditableTextBoxColors(DependencyObject root, Brush foreground, Brush background, Brush border)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var box = child as TextBox;
                if (box != null)
                {
                    box.Foreground = foreground;
                    box.Background = background;
                    box.BorderBrush = border;
                    box.CaretBrush = foreground;
                }
                ApplyEditableTextBoxColors(child, foreground, background, border);
            }
        }

        private static Brush FindBrush(FrameworkElement element, string key, Brush fallback)
        {
            if (element != null)
            {
                var value = element.TryFindResource(key) as Brush;
                if (value != null) return value;
            }
            return fallback;
        }
    }
}
