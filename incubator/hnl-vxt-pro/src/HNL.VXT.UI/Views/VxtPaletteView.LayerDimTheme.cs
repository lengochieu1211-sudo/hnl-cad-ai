using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView
    {
        private const string FixedLayerDimThemeTag = "HNL_VXT_FIXED_LAYER_DIM";

        /// <summary>
        /// The fixed Layer/DIM card is created by the runtime polish pass after the original
        /// XAML tree exists. Linetype/Lineweight used editable system ComboBoxes, which caused
        /// the AutoCAD/Windows white chrome to leak through in dark themes. Convert those
        /// resource selectors to deterministic HNL dropdowns and re-apply the shared HNL combo
        /// template after the card exists and again after PaletteSet materializes the visual tree.
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

            foreach (var node in WalkLayerDimLogicalTree(card))
            {
                var combo = node as ComboBox;
                if (combo == null) continue;

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
