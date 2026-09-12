using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using HNL.VXT.UI.Controls;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView
    {
        private const string FixedLayerDimThemeTag = "HNL_VXT_FIXED_LAYER_DIM";

        /// <summary>
        /// The fixed Layer/DIM card is created by the runtime polish pass after the original
        /// XAML tree exists. Upgrade its raw CAD resource inputs to the friendly selectors already
        /// exposed by the ViewModel, then apply the deterministic HNL dropdown chrome. The model
        /// still stores the original ACI/LineWeight values, so Preview/Create and existing settings
        /// remain compatible.
        /// </summary>
        public static void ApplyLayerDimComboThemeFix(VxtPaletteView view)
        {
            if (view == null) return;

            ApplyLayerDimComboThemeNow(view);
            view.Loaded += (sender, args) =>
            {
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyLayerDimComboThemeNow(view)),
                    DispatcherPriority.ContextIdle);
            };
        }

        private static void ApplyLayerDimComboThemeNow(VxtPaletteView view)
        {
            var card = FindFixedLayerDimCard(view);
            if (card == null) return;

            UpgradeFriendlySelectors(view, card);

            foreach (var node in WalkLayerDimLogicalTree(card))
            {
                var combo = node as ComboBox;
                if (combo == null) continue;

                // Friendly color/lineweight selectors are already deterministic dropdowns.
                // Remaining editable resource selectors are Linetype controls.
                if (combo.IsEditable)
                    ConvertResourceComboToHnlDropdown(combo);

                var primary = FindBrush(combo, "PrimaryText", Brushes.White);
                var secondary = FindBrush(combo, "SecondaryText", Brushes.Gray);
                var input = FindBrush(combo, "InputBackground", Brushes.Black);
                var border = FindBrush(combo, "InputBorder", Brushes.Gray);
                var selected = FindBrush(combo, "AccentSoft", Brushes.DimGray);
                FixCombo(combo, primary, secondary, input, border, selected);
            }
        }

        private static void UpgradeFriendlySelectors(VxtPaletteView view, Border card)
        {
            var rows = WalkLayerDimLogicalTree(card).OfType<Grid>().ToArray();
            foreach (var row in rows)
            {
                var label = row.Children.OfType<TextBlock>()
                    .FirstOrDefault(x => Grid.GetColumn(x) == 0);
                if (label == null) continue;

                if (string.Equals(label.Text, "Màu ACI", StringComparison.Ordinal) ||
                    string.Equals(label.Text, "Màu", StringComparison.Ordinal))
                {
                    UpgradeColorRow(view, row, label);
                    continue;
                }

                if (string.Equals(label.Text, "Lineweight", StringComparison.Ordinal) ||
                    string.Equals(label.Text, "Độ dày nét", StringComparison.Ordinal))
                {
                    UpgradeLineweightRow(view, row, label);
                    continue;
                }

                if (string.Equals(label.Text, "Linetype", StringComparison.Ordinal))
                    label.Text = "Kiểu nét";
            }
        }

        private static void UpgradeColorRow(VxtPaletteView view, Grid row, TextBlock label)
        {
            var numeric = row.Children.OfType<HnlNumericBox>().FirstOrDefault();
            if (numeric == null)
            {
                label.Text = "Màu";
                return;
            }

            var rawBinding = BindingOperations.GetBinding(numeric, HnlNumericBox.ValueProperty);
            var friendlyPath = FriendlyColorProperty(rawBinding?.Path?.Path);
            if (string.IsNullOrWhiteSpace(friendlyPath)) return;

            var current = ReadFriendlyValue(view.ViewModel, friendlyPath);
            var combo = new ComboBox
            {
                IsEditable = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = BuildOptions(view.ViewModel.QuickColorOptions, current)
            };
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding(friendlyPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            Grid.SetColumn(combo, Grid.GetColumn(numeric));
            Grid.SetColumnSpan(combo, Grid.GetColumnSpan(numeric));
            BindingOperations.ClearBinding(numeric, HnlNumericBox.ValueProperty);
            row.Children.Remove(numeric);
            row.Children.Add(combo);
            label.Text = "Màu";
        }

        private static void UpgradeLineweightRow(VxtPaletteView view, Grid row, TextBlock label)
        {
            var combo = row.Children.OfType<ComboBox>().FirstOrDefault();
            if (combo == null)
            {
                label.Text = "Độ dày nét";
                return;
            }

            var rawBinding = BindingOperations.GetBinding(combo, ComboBox.TextProperty);
            var friendlyPath = FriendlyLineweightProperty(rawBinding?.Path?.Path);
            if (string.IsNullOrWhiteSpace(friendlyPath))
            {
                label.Text = "Độ dày nét";
                return;
            }

            var current = ReadFriendlyValue(view.ViewModel, friendlyPath);
            BindingOperations.ClearBinding(combo, ComboBox.TextProperty);
            BindingOperations.ClearBinding(combo, ComboBox.SelectedItemProperty);
            combo.IsEditable = false;
            combo.ItemsSource = BuildOptions(view.ViewModel.LineweightOptions, current);
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding(friendlyPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            label.Text = "Độ dày nét";
        }

        private static string FriendlyColorProperty(string rawPath)
        {
            switch (rawPath)
            {
                case nameof(VxtPaletteViewModel.MainColorIndex): return nameof(VxtPaletteViewModel.SelectedMainColor);
                case nameof(VxtPaletteViewModel.FurringColorIndex): return nameof(VxtPaletteViewModel.SelectedFurringColor);
                case nameof(VxtPaletteViewModel.HangerColorIndex): return nameof(VxtPaletteViewModel.SelectedHangerColor);
                case nameof(VxtPaletteViewModel.DimensionColorIndex): return nameof(VxtPaletteViewModel.SelectedDimensionColor);
                default: return string.Empty;
            }
        }

        private static string FriendlyLineweightProperty(string rawPath)
        {
            switch (rawPath)
            {
                case nameof(VxtPaletteViewModel.MainLineweight): return nameof(VxtPaletteViewModel.SelectedMainLineweight);
                case nameof(VxtPaletteViewModel.FurringLineweight): return nameof(VxtPaletteViewModel.SelectedFurringLineweight);
                case nameof(VxtPaletteViewModel.HangerLineweight): return nameof(VxtPaletteViewModel.SelectedHangerLineweight);
                case nameof(VxtPaletteViewModel.DimensionLineweight): return nameof(VxtPaletteViewModel.SelectedDimensionLineweight);
                default: return string.Empty;
            }
        }

        private static string ReadFriendlyValue(VxtPaletteViewModel vm, string propertyName)
        {
            if (vm == null || string.IsNullOrWhiteSpace(propertyName)) return string.Empty;
            var property = typeof(VxtPaletteViewModel).GetProperty(propertyName);
            return property?.GetValue(vm, null) as string ?? string.Empty;
        }

        private static string[] BuildOptions(IEnumerable<string> source, string current)
        {
            var values = new List<string>();
            if (source != null)
            {
                foreach (var value in source)
                {
                    if (!string.IsNullOrWhiteSpace(value) &&
                        !values.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                        values.Add(value);
                }
            }

            // Preserve legacy/custom ACI and lineweight values. They stay visible and unchanged
            // until the user deliberately picks one of the compact HNL choices.
            if (!string.IsNullOrWhiteSpace(current) &&
                !values.Any(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase)))
                values.Insert(0, current);

            return values.ToArray();
        }

        private static void ConvertResourceComboToHnlDropdown(ComboBox combo)
        {
            var textBinding = BindingOperations.GetBinding(combo, ComboBox.TextProperty);
            var bindingPath = textBinding?.Path?.Path;
            var currentText = combo.Text;

            // Keep a currently selected resource visible even when a legacy/custom drawing
            // contains a value that was not present in the initial option array.
            if (!string.IsNullOrWhiteSpace(currentText))
            {
                var values = new List<string>();
                var source = combo.ItemsSource as IEnumerable;
                if (source != null)
                {
                    foreach (var item in source)
                    {
                        var value = item?.ToString();
                        if (!string.IsNullOrWhiteSpace(value) &&
                            !values.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                            values.Add(value);
                    }
                }

                if (!values.Any(x => string.Equals(x, currentText, StringComparison.OrdinalIgnoreCase)))
                    values.Insert(0, currentText);

                if (values.Count > 0)
                    combo.ItemsSource = values.ToArray();
            }

            BindingOperations.ClearBinding(combo, ComboBox.TextProperty);
            combo.IsEditable = false;

            if (!string.IsNullOrWhiteSpace(bindingPath))
            {
                combo.SetBinding(ComboBox.SelectedItemProperty, new Binding(bindingPath)
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            }
        }

        private static Border FindFixedLayerDimCard(DependencyObject root)
        {
            foreach (var node in WalkLayerDimLogicalTree(root))
            {
                var border = node as Border;
                if (border != null &&
                    string.Equals(border.Tag as string, FixedLayerDimThemeTag, StringComparison.Ordinal))
                    return border;
            }
            return null;
        }

        private static IEnumerable<DependencyObject> WalkLayerDimLogicalTree(DependencyObject root)
        {
            if (root == null) yield break;

            var seen = new HashSet<DependencyObject>();
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !seen.Add(current)) continue;
                yield return current;

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

                var userControl = current as UserControl;
                var userContent = userControl?.Content as DependencyObject;
                if (userContent != null) queue.Enqueue(userContent);
            }
        }
    }
}
