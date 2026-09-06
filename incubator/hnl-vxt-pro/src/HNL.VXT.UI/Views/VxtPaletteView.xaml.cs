using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            AddDimensionResourceFields();
            AddDiagnosticCenter();
            VxtPaletteEnhancer.Apply(this, host, ViewModel);
        }

        public VxtPaletteViewModel ViewModel { get; }

        private void AddDimensionResourceFields()
        {
            var dimExpander = FindExpanderByHeaderText(this, "KÍCH THƯỚC DIM");
            var content = dimExpander?.Content as StackPanel;
            if (content == null) return;

            var block = new Border
            {
                BorderBrush = (Brush)Resources["CardBorder"],
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 9, 0, 0),
                Margin = new Thickness(0, 9, 0, 0)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateDimensionResourceRow(
                "Layer DIM",
                nameof(VxtPaletteViewModel.DimensionLayer),
                "Layer dùng cho DIM và Preview DIM. Nếu layer không tồn tại, Preview dùng thiết lập hiện hành."));
            panel.Children.Add(CreateDimensionResourceRow(
                "DimStyle",
                nameof(VxtPaletteViewModel.DimensionStyle),
                "Tên DimStyle dùng cho DIM. Để trống để dùng DimStyle hiện hành của bản vẽ.",
                new Thickness(0, 7, 0, 0)));

            var hint = new TextBlock
            {
                Text = "Để trống DimStyle = dùng DimStyle hiện hành của bản vẽ.",
                Margin = new Thickness(190, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            if (Resources["HintText"] is Style hintStyle)
                hint.Style = hintStyle;
            panel.Children.Add(hint);

            block.Child = panel;
            content.Children.Add(block);
        }

        private Grid CreateDimensionResourceRow(string label, string bindingPath, string toolTip, Thickness? margin = null)
        {
            var row = new Grid { Margin = margin ?? new Thickness(0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var text = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = toolTip
            };
            if (Resources["FieldLabel"] is Style fieldLabelStyle)
                text.Style = fieldLabelStyle;

            var input = new TextBox
            {
                ToolTip = toolTip,
                MinWidth = 120
            };
            input.SetBinding(TextBox.TextProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });
            Grid.SetColumn(input, 1);

            row.Children.Add(text);
            row.Children.Add(input);
            return row;
        }

        private void AddDiagnosticCenter()
        {
            var liveTitle = FindTextBlockByText(this, "XEM TRƯỚC TRÊN BẢN VẼ");
            if (liveTitle == null) return;

            DependencyObject current = liveTitle;
            Border liveCard = null;
            while (current != null)
            {
                liveCard = current as Border;
                if (liveCard != null && liveCard.Parent is StackPanel) break;
                current = (current as FrameworkElement)?.Parent;
                liveCard = null;
            }

            var parent = liveCard?.Parent as StackPanel;
            if (parent == null) return;

            var card = new Border
            {
                Style = Resources["SectionCard"] as Style,
                BorderBrush = (Brush)Resources["AccentBorder"]
            };

            var panel = new StackPanel();

            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titlePanel.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 4,
                Height = 18,
                RadiusX = 2,
                RadiusY = 2,
                Fill = Brush("#F59E0B"),
                Margin = new Thickness(0, 0, 8, 0)
            });
            var title = new TextBlock { Text = "PHÂN TÍCH & KIỂM TRA LỖI" };
            if (Resources["SectionTitle"] is Style sectionTitle) title.Style = sectionTitle;
            titlePanel.Children.Add(title);
            header.Children.Add(titlePanel);

            var stateBadge = new Border
            {
                GridColumn = 1,
                Background = (Brush)Resources["AccentSoft"],
                BorderBrush = (Brush)Resources["AccentBorder"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 3, 7, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            var stateText = new TextBlock
            {
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Resources["AccentStrong"]
            };
            stateText.SetBinding(TextBlock.TextProperty, new Binding(nameof(VxtPaletteViewModel.DiagnosticState)));
            stateBadge.Child = stateText;
            Grid.SetColumn(stateBadge, 1);
            header.Children.Add(stateBadge);
            panel.Children.Add(header);

            var scope = new TextBlock
            {
                Text = "Kiểm tra: cấu hình Min/Max • biên trần • Block XC/XP/Ty • DimStyle • tài nguyên DWG • trạng thái Runtime.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            if (Resources["HintText"] is Style hintStyle) scope.Style = hintStyle;
            panel.Children.Add(scope);

            var statusBox = new Border
            {
                Background = (Brush)Resources["InputBackground"],
                BorderBrush = (Brush)Resources["InputBorder"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 7, 8, 7),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10.5
            };
            status.SetBinding(TextBlock.TextProperty, new Binding(nameof(VxtPaletteViewModel.DiagnosticStatus)));
            statusBox.Child = status;
            panel.Children.Add(statusBox);

            var buttons = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var analyze = new Button
            {
                Content = "✓ Phân tích nhanh",
                Command = ViewModel.AnalyzeDiagnosticsCommand,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "Kiểm tra cấu hình, biên trần, Block, DimStyle và tài nguyên bản vẽ"
            };
            if (Resources["CompactButton"] is Style compactStyle) analyze.Style = compactStyle;
            buttons.Children.Add(analyze);

            var export = new Button
            {
                Content = "⬇ Xuất Diagnostic ZIP",
                Command = ViewModel.ExportDiagnosticsUiCommand,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Xuất gói đầy đủ để phân tích sâu khi có lỗi"
            };
            if (Resources["CompactButton"] is Style compactStyle2) export.Style = compactStyle2;
            Grid.SetColumn(export, 1);
            buttons.Children.Add(export);
            panel.Children.Add(buttons);

            var packageLabel = new TextBlock
            {
                Text = "Gói lỗi gần nhất:",
                FontSize = 9.5,
                Foreground = (Brush)Resources["SecondaryText"]
            };
            panel.Children.Add(packageLabel);

            var packagePath = new TextBlock
            {
                FontSize = 9.5,
                Foreground = (Brush)Resources["SecondaryText"],
                TextWrapping = TextWrapping.Wrap
            };
            packagePath.SetBinding(TextBlock.TextProperty, new Binding(nameof(VxtPaletteViewModel.DiagnosticPackagePath)));
            panel.Children.Add(packagePath);

            var commandHint = new TextBlock
            {
                Text = "Lệnh kỹ thuật: VXTANALYZE • VXTDIAGZIP",
                FontSize = 9,
                Foreground = (Brush)Resources["SecondaryText"],
                Margin = new Thickness(0, 5, 0, 0)
            };
            panel.Children.Add(commandHint);

            card.Child = panel;
            parent.Children.Insert(parent.Children.IndexOf(liveCard), card);
        }

        private static Expander FindExpanderByHeaderText(DependencyObject parent, string text)
        {
            if (parent == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Expander expander && ContainsText(expander.Header as DependencyObject, text))
                    return expander;
                var nested = FindExpanderByHeaderText(child, text);
                if (nested != null) return nested;
            }
            return null;
        }

        private static TextBlock FindTextBlockByText(DependencyObject parent, string text)
        {
            if (parent == null) return null;
            if (parent is TextBlock direct && direct.Text == text) return direct;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var found = FindTextBlockByText(VisualTreeHelper.GetChild(parent, i), text);
                if (found != null) return found;
            }
            return null;
        }

        private static bool ContainsText(DependencyObject parent, string text)
        {
            if (parent == null) return false;
            if (parent is TextBlock textBlock && textBlock.Text == text) return true;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                if (ContainsText(VisualTreeHelper.GetChild(parent, i), text)) return true;
            }
            return false;
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