using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HNL.VXT.UI.Hosting;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView : UserControl
    {
        public VxtPaletteView(IVxtHostBridge host)
        {
            InitializeComponent();
            ApplyTheme(host.IsDarkTheme);
            ViewModel = new VxtPaletteViewModel(host);
            DataContext = ViewModel;
            AddDiagnosticButton();
        }

        public VxtPaletteViewModel ViewModel { get; }

        private void AddDiagnosticButton()
        {
            var createButton = FindButtonByContent(this, "✓ TẠO KHUNG XƯƠNG TRẦN");
            var footer = createButton?.Parent as StackPanel;
            if (footer == null) return;

            var exportButton = new Button
            {
                Content = "Xuất lỗi",
                Command = ViewModel.ExportDiagnosticsCommand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 0),
                ToolTip = "Xuất thông tin kiểm tra HNL VXT để gửi khi cần chẩn đoán lỗi"
            };

            if (Resources["CompactButton"] is Style compactStyle)
                exportButton.Style = compactStyle;

            var insertIndex = footer.Children.IndexOf(createButton) + 1;
            footer.Children.Insert(insertIndex, exportButton);
        }

        private static Button FindButtonByContent(DependencyObject parent, string content)
        {
            if (parent == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button button && string.Equals(button.Content as string, content))
                    return button;
                var nested = FindButtonByContent(child, content);
                if (nested != null) return nested;
            }
            return null;
        }

        private void ApplyTheme(bool dark)
        {
            Resources["AppBackground"] = Brush(dark ? "#1B1F23" : "#F3F6F8");
            Resources["CardBackground"] = Brush(dark ? "#252A30" : "#FFFFFF");
            Resources["CardBorder"] = Brush(dark ? "#3B424A" : "#D7E0E7");
            Resources["PrimaryText"] = Brush(dark ? "#F1F5F9" : "#172033");
            Resources["SecondaryText"] = Brush(dark ? "#AEB8C4" : "#64748B");
            Resources["InputBackground"] = Brush(dark ? "#1E2328" : "#FFFFFF");
            Resources["InputBorder"] = Brush(dark ? "#4A535D" : "#C7D2DC");
            Resources["HoverBackground"] = Brush(dark ? "#323941" : "#EEF4F8");
            Resources["Accent"] = Brush("#0EA5E9");
            Resources["AccentStrong"] = Brush(dark ? "#38BDF8" : "#0284C7");
            Resources["AccentSoft"] = Brush(dark ? "#102F3E" : "#EAF8FE");
            Resources["AccentBorder"] = Brush(dark ? "#155E75" : "#BAE6FD");
            Resources["HeaderBackground"] = Brush(dark ? "#0B1118" : "#0F172A");
            Resources["Success"] = Brush("#22C55E");

            ApplyComboBoxTheme();
        }

        private void ApplyComboBoxTheme()
        {
            var inputBackground = (Brush)Resources["InputBackground"];
            var inputBorder = (Brush)Resources["InputBorder"];
            var primaryText = (Brush)Resources["PrimaryText"];
            var hoverBackground = (Brush)Resources["HoverBackground"];
            var selectedBackground = (Brush)Resources["AccentSoft"];

            // WPF's default ComboBox popup can otherwise use the Windows light system
            // colors while inheriting HNL's dark foreground, producing white-on-white text.
            // Override the system brushes only inside this palette.
            Resources[SystemColors.WindowBrushKey] = inputBackground;
            Resources[SystemColors.WindowTextBrushKey] = primaryText;
            Resources[SystemColors.ControlBrushKey] = inputBackground;
            Resources[SystemColors.ControlTextBrushKey] = primaryText;
            Resources[SystemColors.HighlightBrushKey] = selectedBackground;
            Resources[SystemColors.HighlightTextBrushKey] = primaryText;
            Resources[SystemColors.InactiveSelectionHighlightBrushKey] = selectedBackground;
            Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = primaryText;

            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, primaryText));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, inputBackground));
            itemStyle.Setters.Add(new Setter(Control.BorderBrushProperty, inputBorder));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
            itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, primaryText));
            itemStyle.Triggers.Add(hover);

            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, selectedBackground));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, primaryText));
            itemStyle.Triggers.Add(selected);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55));
            itemStyle.Triggers.Add(disabled);

            Resources[typeof(ComboBoxItem)] = itemStyle;
        }

        private static SolidColorBrush Brush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
