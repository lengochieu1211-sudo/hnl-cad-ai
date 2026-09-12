using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using HNL.VXT.UI.Appearance;
using HNL.VXT.UI.Controls;
using HNL.VXT.UI.Hosting;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    internal static class VxtPaletteEnhancer
    {
        private sealed class UiState
        {
            public VxtUiPreferences Preferences;
            public IVxtHostBridge Host;
            public VxtPaletteViewModel ViewModel;
            public bool Captured;
            public readonly Dictionary<TextBlock, double> TextSizes = new Dictionary<TextBlock, double>();
            public readonly Dictionary<Control, double> ControlTextSizes = new Dictionary<Control, double>();
            public readonly Dictionary<FrameworkElement, Thickness> Margins = new Dictionary<FrameworkElement, Thickness>();
            public readonly Dictionary<Border, Thickness> CardPaddings = new Dictionary<Border, Thickness>();
            public readonly HashSet<Border> Cards = new HashSet<Border>();
        }

        public static void Apply(VxtPaletteView view, IVxtHostBridge host, VxtPaletteViewModel vm)
        {
            var state = new UiState
            {
                Preferences = VxtUiPreferences.Load(),
                Host = host,
                ViewModel = vm
            };

            ApplyTheme(view, state);
            AddLayerAppearancePanel(view, state);
            AddAppearancePanel(view, state);
            FixFooterVersion(view);
            FixComboBoxes(view, state);

            view.Loaded += (sender, args) =>
            {
                if (!state.Captured)
                {
                    CaptureMetrics(view, state);
                    state.Captured = true;
                }
                ApplyAppearance(view, state);
            };
        }

        private static void AddLayerAppearancePanel(VxtPaletteView view, UiState state)
        {
            var stack = FindContentStack(view);
            if (stack == null) return;
            var insert = FindInsertBeforePreview(stack);

            var border = CreateSectionCard(view);
            var expander = new Expander { IsExpanded = false };
            expander.Header = CreateHeader(view, "LAYER & KIỂU NÉT", "#64748B");

            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            panel.Children.Add(CreateResourceGroup(view, state.ViewModel, "Xương chính",
                nameof(VxtPaletteViewModel.MainLayer), nameof(VxtPaletteViewModel.MainColorIndex),
                nameof(VxtPaletteViewModel.MainLinetype), nameof(VxtPaletteViewModel.MainLineweight), false));
            panel.Children.Add(CreateResourceGroup(view, state.ViewModel, "Xương phụ",
                nameof(VxtPaletteViewModel.FurringLayer), nameof(VxtPaletteViewModel.FurringColorIndex),
                nameof(VxtPaletteViewModel.FurringLinetype), nameof(VxtPaletteViewModel.FurringLineweight), false));
            panel.Children.Add(CreateResourceGroup(view, state.ViewModel, "Ty treo",
                nameof(VxtPaletteViewModel.HangerLayer), nameof(VxtPaletteViewModel.HangerColorIndex),
                nameof(VxtPaletteViewModel.HangerLinetype), nameof(VxtPaletteViewModel.HangerLineweight), false));
            panel.Children.Add(CreateResourceGroup(view, state.ViewModel, "DIM",
                nameof(VxtPaletteViewModel.DimensionLayer), nameof(VxtPaletteViewModel.DimensionColorIndex),
                nameof(VxtPaletteViewModel.DimensionLinetype), nameof(VxtPaletteViewModel.DimensionLineweight), true));

            var hint = new TextBlock
            {
                Text = "Màu dùng chỉ số ACI 0–256. Lineweight -3 = mặc định, -2 = ByBlock, -1 = ByLayer.",
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            if (view.Resources["HintText"] is Style hintStyle) hint.Style = hintStyle;
            panel.Children.Add(hint);

            expander.Content = panel;
            border.Child = expander;
            stack.Children.Insert(insert, border);
        }

        private static Border CreateResourceGroup(
            VxtPaletteView view,
            VxtPaletteViewModel vm,
            string title,
            string layerPath,
            string colorPath,
            string linetypePath,
            string lineweightPath,
            bool includeDimStyle)
        {
            var border = new Border
            {
                BorderBrush = GetBrush(view, "CardBorder"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var panel = new StackPanel();
            var titleText = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            panel.Children.Add(titleText);
            panel.Children.Add(CreateTextRow(view, "Layer", layerPath));
            panel.Children.Add(CreateColorRow(view, "Màu ACI", colorPath));
            panel.Children.Add(CreateEditableComboRow(view, vm, "Linetype", linetypePath, vm.LinetypeOptions));
            panel.Children.Add(CreateEditableComboRow(view, vm, "Lineweight", lineweightPath, vm.LineweightOptions));
            if (includeDimStyle)
                panel.Children.Add(CreateComboRow(view, "DimStyle", nameof(VxtPaletteViewModel.SelectedDimensionStyle), vm.DimStyleOptions));
            border.Child = panel;
            return border;
        }

        private static Grid CreateTextRow(VxtPaletteView view, string label, string bindingPath)
        {
            var row = CreateFormRow(view, label);
            var input = new TextBox();
            input.SetBinding(TextBox.TextProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });
            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            return row;
        }

        private static Grid CreateColorRow(VxtPaletteView view, string label, string bindingPath)
        {
            var row = CreateFormRow(view, label);
            var input = new HnlNumericBox { Minimum = 0, Maximum = 256, Step = 1, Unit = "ACI" };
            input.SetBinding(HnlNumericBox.ValueProperty, new Binding(bindingPath) { Mode = BindingMode.TwoWay });
            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            return row;
        }

        private static Grid CreateEditableComboRow(VxtPaletteView view, VxtPaletteViewModel vm, string label, string bindingPath, string[] items)
        {
            var row = CreateFormRow(view, label);
            var combo = new ComboBox { IsEditable = true, ItemsSource = items };
            combo.SetBinding(ComboBox.TextProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            return row;
        }

        private static Grid CreateComboRow(VxtPaletteView view, string label, string bindingPath, string[] items)
        {
            var row = CreateFormRow(view, label);
            var combo = new ComboBox { ItemsSource = items };
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding(bindingPath) { Mode = BindingMode.TwoWay });
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            return row;
        }

        private static Grid CreateFormRow(VxtPaletteView view, string label)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            if (view.Resources["FieldLabel"] is Style style) text.Style = style;
            row.Children.Add(text);
            return row;
        }

        private static void AddAppearancePanel(VxtPaletteView view, UiState state)
        {
            var stack = FindContentStack(view);
            if (stack == null) return;
            var insert = FindInsertBeforePreview(stack);

            var border = CreateSectionCard(view);
            var expander = new Expander { IsExpanded = false };
            expander.Header = CreateHeader(view, "GIAO DIỆN", "#0EA5E9");

            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            panel.Children.Add(CreatePreferenceRow(view, state, "Chế độ màu", VxtUiPreferences.ThemeModes,
                () => state.Preferences.ThemeMode, v => state.Preferences.ThemeMode = v));
            panel.Children.Add(CreatePreferenceRow(view, state, "Màu nhấn HNL", VxtUiPreferences.AccentColors,
                () => state.Preferences.AccentColor, v => state.Preferences.AccentColor = v));
            panel.Children.Add(CreatePreferenceRow(view, state, "Màu chữ", VxtUiPreferences.TextColors,
                () => state.Preferences.TextColor, v => state.Preferences.TextColor = v));
            panel.Children.Add(CreatePreferenceRow(view, state, "Cỡ chữ", VxtUiPreferences.TextScales,
                () => state.Preferences.TextScale, v => state.Preferences.TextScale = v));
            panel.Children.Add(CreatePreferenceRow(view, state, "Khoảng cách dòng", VxtUiPreferences.SpacingModes,
                () => state.Preferences.RowSpacing, v => state.Preferences.RowSpacing = v));
            panel.Children.Add(CreatePreferenceRow(view, state, "Khoảng cách cột", VxtUiPreferences.SpacingModes,
                () => state.Preferences.ColumnSpacing, v => state.Preferences.ColumnSpacing = v));
            panel.Children.Add(CreatePreferenceRow(view, state, "Khoảng cách các khối", VxtUiPreferences.SpacingModes,
                () => state.Preferences.CardSpacing, v => state.Preferences.CardSpacing = v));

            var reset = new Button
            {
                Content = "Khôi phục giao diện mặc định",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };
            if (view.Resources["CompactButton"] is Style compact) reset.Style = compact;
            reset.Click += (sender, args) =>
            {
                state.Preferences.Reset();
                state.Preferences.Save();
                RefreshPreferenceCombos(panel, state.Preferences);
                ApplyAppearance(view, state);
            };
            panel.Children.Add(reset);

            expander.Content = panel;
            border.Child = expander;
            stack.Children.Insert(insert, border);
        }

        private static Grid CreatePreferenceRow(
            VxtPaletteView view,
            UiState state,
            string label,
            string[] items,
            Func<string> getter,
            Action<string> setter)
        {
            var row = CreateFormRow(view, label);
            var combo = new ComboBox
            {
                ItemsSource = items,
                SelectedItem = getter(),
                Tag = label
            };
            combo.SelectionChanged += (sender, args) =>
            {
                if (!(combo.SelectedItem is string selected)) return;
                setter(selected);
                state.Preferences.Save();
                ApplyAppearance(view, state);
            };
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            return row;
        }

        private static void RefreshPreferenceCombos(DependencyObject root, VxtUiPreferences preferences)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ComboBox combo && combo.Tag is string label)
                {
                    switch (label)
                    {
                        case "Chế độ màu": combo.SelectedItem = preferences.ThemeMode; break;
                        case "Màu nhấn HNL": combo.SelectedItem = preferences.AccentColor; break;
                        case "Màu chữ": combo.SelectedItem = preferences.TextColor; break;
                        case "Cỡ chữ": combo.SelectedItem = preferences.TextScale; break;
                        case "Khoảng cách dòng": combo.SelectedItem = preferences.RowSpacing; break;
                        case "Khoảng cách cột": combo.SelectedItem = preferences.ColumnSpacing; break;
                        case "Khoảng cách các khối": combo.SelectedItem = preferences.CardSpacing; break;
                    }
                }
                RefreshPreferenceCombos(child, preferences);
            }
        }

        private static void ApplyAppearance(VxtPaletteView view, UiState state)
        {
            ApplyTheme(view, state);
            FixComboBoxes(view, state);
            if (!state.Captured) return;

            var textFactor = ParsePercent(state.Preferences.TextScale);
            var rowFactor = SpacingFactor(state.Preferences.RowSpacing, 0.62, 0.88, 1.18);
            var columnFactor = SpacingFactor(state.Preferences.ColumnSpacing, 0.72, 1.0, 1.28);
            var cardFactor = SpacingFactor(state.Preferences.CardSpacing, 0.68, 1.0, 1.25);

            foreach (var pair in state.TextSizes)
                pair.Key.FontSize = Math.Max(8.0, pair.Value * textFactor);
            foreach (var pair in state.ControlTextSizes)
                pair.Key.FontSize = Math.Max(8.0, pair.Value * textFactor);

            foreach (var pair in state.Margins)
            {
                var element = pair.Key;
                var m = pair.Value;
                if (element is Border card && state.Cards.Contains(card))
                {
                    element.Margin = new Thickness(m.Left * columnFactor, m.Top * cardFactor, m.Right * columnFactor, m.Bottom * cardFactor);
                }
                else
                {
                    element.Margin = new Thickness(m.Left * columnFactor, m.Top * rowFactor, m.Right * columnFactor, m.Bottom * rowFactor);
                }
            }

            foreach (var pair in state.CardPaddings)
            {
                var p = pair.Value;
                pair.Key.Padding = new Thickness(
                    Math.Max(6, p.Left * cardFactor),
                    Math.Max(5, p.Top * cardFactor),
                    Math.Max(6, p.Right * cardFactor),
                    Math.Max(5, p.Bottom * cardFactor));
            }
        }

        private static void ApplyTheme(VxtPaletteView view, UiState state)
        {
            var dark = state.Preferences.ThemeMode == "Tối" ||
                       (state.Preferences.ThemeMode == "Theo AutoCAD" && state.Host.IsDarkTheme);

            view.Resources["AppBackground"] = Brush(dark ? "#1B1F23" : "#F3F6F8");
            view.Resources["CardBackground"] = Brush(dark ? "#252A30" : "#FFFFFF");
            view.Resources["CardBorder"] = Brush(dark ? "#3B424A" : "#D7E0E7");
            view.Resources["InputBackground"] = Brush(dark ? "#1E2328" : "#FFFFFF");
            view.Resources["InputBorder"] = Brush(dark ? "#4A535D" : "#C7D2DC");
            view.Resources["HoverBackground"] = Brush(dark ? "#323941" : "#EEF4F8");
            view.Resources["HeaderBackground"] = Brush(dark ? "#0B1118" : "#0F172A");
            view.Resources["Success"] = Brush("#22C55E");

            var primary = dark ? "#F1F5F9" : "#172033";
            var secondary = dark ? "#AEB8C4" : "#64748B";
            switch (state.Preferences.TextColor)
            {
                case "Sáng": primary = "#F8FAFC"; secondary = "#CBD5E1"; break;
                case "Tối": primary = "#172033"; secondary = "#475569"; break;
                case "Xanh HNL": primary = dark ? "#93C5FD" : "#1D4ED8"; secondary = dark ? "#60A5FA" : "#3B82F6"; break;
            }
            view.Resources["PrimaryText"] = Brush(primary);
            view.Resources["SecondaryText"] = Brush(secondary);

            var accent = Accent(state.Preferences.AccentColor, dark);
            view.Resources["Accent"] = Brush(accent.Strong);
            view.Resources["AccentStrong"] = Brush(accent.Strong);
            view.Resources["AccentSoft"] = Brush(accent.Soft);
            view.Resources["AccentBorder"] = Brush(accent.Border);
        }

        private static TupleColor Accent(string name, bool dark)
        {
            switch (name)
            {
                case "Cyan": return new TupleColor("#22D3EE", dark ? "#12343B" : "#ECFEFF", dark ? "#155E75" : "#A5F3FC");
                case "Xanh lá": return new TupleColor("#22C55E", dark ? "#153622" : "#F0FDF4", dark ? "#166534" : "#BBF7D0");
                case "Tím": return new TupleColor("#A855F7", dark ? "#2D1B3D" : "#FAF5FF", dark ? "#6B21A8" : "#E9D5FF");
                case "Cam": return new TupleColor("#F97316", dark ? "#3B2415" : "#FFF7ED", dark ? "#9A3412" : "#FED7AA");
                default: return new TupleColor(dark ? "#38BDF8" : "#0284C7", dark ? "#102F3E" : "#EAF8FE", dark ? "#155E75" : "#BAE6FD");
            }
        }

        private sealed class TupleColor
        {
            public TupleColor(string strong, string soft, string border) { Strong = strong; Soft = soft; Border = border; }
            public string Strong { get; }
            public string Soft { get; }
            public string Border { get; }
        }

        private static void FixComboBoxes(DependencyObject root, UiState state)
        {
            var primary = GetBrush(root as FrameworkElement, "PrimaryText") ?? Brushes.White;
            var input = GetBrush(root as FrameworkElement, "InputBackground") ?? Brushes.Black;
            var border = GetBrush(root as FrameworkElement, "InputBorder") ?? Brushes.Gray;
            var selected = GetBrush(root as FrameworkElement, "AccentSoft") ?? Brushes.DimGray;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ComboBox combo)
                {
                    combo.Foreground = primary;
                    combo.Background = input;
                    combo.BorderBrush = border;
                    combo.ItemTemplate = CreateComboItemTemplate(primary);
                    combo.ItemContainerStyle = CreateComboItemStyle(primary, input, selected);
                }
                FixComboBoxes(child, state);
            }
        }

        private static DataTemplate CreateComboItemTemplate(Brush foreground)
        {
#pragma warning disable 0618
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding("."));
            text.SetValue(TextBlock.ForegroundProperty, foreground);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            return new DataTemplate { VisualTree = text };
#pragma warning restore 0618
        }

        private static Style CreateComboItemStyle(Brush foreground, Brush background, Brush selectedBackground)
        {
            var style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            style.Setters.Add(new Setter(Control.BackgroundProperty, background));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7, 3, 7, 3)));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, selectedBackground));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            style.Triggers.Add(hover);
            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, selectedBackground));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            style.Triggers.Add(selected);
            return style;
        }

        private static void CaptureMetrics(VxtPaletteView view, UiState state)
        {
            CaptureTextMetrics(view, state);
            var scroll = FindFirst<ScrollViewer>(view);
            if (scroll?.Content is DependencyObject content)
                CaptureLayoutMetrics(content, state);
        }

        private static void CaptureTextMetrics(DependencyObject root, UiState state)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock text && !state.TextSizes.ContainsKey(text)) state.TextSizes[text] = text.FontSize;
                if (child is Control control && !(control is VxtPaletteView) && !state.ControlTextSizes.ContainsKey(control))
                    state.ControlTextSizes[control] = control.FontSize;
                CaptureTextMetrics(child, state);
            }
        }

        private static void CaptureLayoutMetrics(DependencyObject root, UiState state)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement element && !state.Margins.ContainsKey(element))
                    state.Margins[element] = element.Margin;
                if (child is Border border && IsSectionCard(border))
                {
                    state.Cards.Add(border);
                    state.CardPaddings[border] = border.Padding;
                }
                CaptureLayoutMetrics(child, state);
            }
        }

        private static bool IsSectionCard(Border border)
            => border.CornerRadius.TopLeft >= 8 && border.BorderThickness.Left > 0;

        private static double ParsePercent(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 1.0;
            if (double.TryParse(value.Replace("%", string.Empty), out var percent))
                return Math.Max(0.8, Math.Min(1.3, percent / 100.0));
            return 1.0;
        }

        private static double SpacingFactor(string value, double compact, double normal, double comfortable)
        {
            if (value == "Gọn") return compact;
            if (value == "Thoáng") return comfortable;
            return normal;
        }

        private static Border CreateSectionCard(VxtPaletteView view)
        {
            var border = new Border();
            if (view.Resources["SectionCard"] is Style style) border.Style = style;
            return border;
        }

        private static Grid CreateHeader(VxtPaletteView view, string title, string color)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var marker = new Border
            {
                Width = 4,
                Height = 18,
                CornerRadius = new CornerRadius(2),
                Background = Brush(color),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var text = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
            if (view.Resources["SectionTitle"] is Style style) text.Style = style;
            Grid.SetColumn(text, 1);
            grid.Children.Add(marker);
            grid.Children.Add(text);
            return grid;
        }

        private static void FixFooterVersion(VxtPaletteView view)
        {
            foreach (var text in FindAll<TextBlock>(view))
            {
                if (!string.IsNullOrWhiteSpace(text.Text) && text.Text.StartsWith("Alpha.2:", StringComparison.Ordinal))
                    text.Text = "Alpha.5: Giao diện tùy biến • Layer/kiểu nét tương thích V6.7.4 • Tạo thật vẫn khóa đến khi Golden Test hoàn tất.";
            }
        }

        private static StackPanel FindContentStack(VxtPaletteView view)
        {
            var scroll = FindFirst<ScrollViewer>(view);
            return scroll?.Content as StackPanel;
        }

        private static int FindInsertBeforePreview(StackPanel stack)
        {
            for (var i = 0; i < stack.Children.Count; i++)
                if (ContainsText(stack.Children[i] as DependencyObject, "XEM TRƯỚC TRÊN BẢN VẼ")) return i;
            return stack.Children.Count;
        }

        private static bool ContainsText(DependencyObject root, string value)
        {
            if (root == null) return false;
            if (root is TextBlock text && text.Text == value) return true;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
                if (ContainsText(VisualTreeHelper.GetChild(root, i), value)) return true;
            return false;
        }

        private static T FindFirst<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed) return typed;
                var nested = FindFirst<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
        {
            var result = new List<T>();
            FindAll(root, result);
            return result;
        }

        private static void FindAll<T>(DependencyObject root, List<T> result) where T : DependencyObject
        {
            if (root == null) return;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed) result.Add(typed);
                FindAll(child, result);
            }
        }

        private static Brush GetBrush(FrameworkElement element, string key)
        {
            if (element == null) return null;
            return element.TryFindResource(key) as Brush;
        }

        private static SolidColorBrush Brush(string hex)
            => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
