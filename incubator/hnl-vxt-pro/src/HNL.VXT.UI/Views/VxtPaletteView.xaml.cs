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
        }

        private static SolidColorBrush Brush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
