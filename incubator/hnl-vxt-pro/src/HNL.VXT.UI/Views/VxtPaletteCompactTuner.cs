using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.Controls;
using HNL.VXT.UI.Hosting;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Runtime layout pass for the production palette.  Keep the original XAML as the
    /// stable functional template, then make the AutoCAD-hosted palette denser and align
    /// every form row to one common label/input/button grid.  This is intentionally a
    /// separate pass so the legacy/Golden bindings stay untouched.
    /// </summary>
    internal static class VxtPaletteCompactTuner
    {
        private const double LabelWidth = 142.0;
        private const double PickButtonWidth = 68.0;
        private static bool _blockLayerSyncHooked;

        public static void Apply(VxtPaletteView view, IVxtHostBridge host, VxtPaletteViewModel vm)
        {
            if (view == null || host == null || vm == null) return;

            ApplyNow(view);
            HookBlockLayerSync(host, vm);

            // Some bindings/header visuals are materialized only after AutoCAD hosts the
            // UserControl.  Re-run once at Loaded so the compact metrics are deterministic.
            view.Loaded += (sender, args) => ApplyNow(view);
        }

        private static void HookBlockLayerSync(IVxtHostBridge host, VxtPaletteViewModel vm)
        {
            if (_blockLayerSyncHooked) return;
            _blockLayerSyncHooked = true;

            vm.PropertyChanged += (sender, args) =>
            {
                if (args == null) return;
                if (args.PropertyName == nameof(VxtPaletteViewModel.MainBlockName))
                    SyncLayerFromPickedBlock(host, vm, BlockTarget.Main);
                else if (args.PropertyName == nameof(VxtPaletteViewModel.FurringBlockName))
                    SyncLayerFromPickedBlock(host, vm, BlockTarget.Furring);
                else if (args.PropertyName == nameof(VxtPaletteViewModel.HangerBlockName))
                    SyncLayerFromPickedBlock(host, vm, BlockTarget.Hanger);
            };
        }

        private static void SyncLayerFromPickedBlock(IVxtHostBridge host, VxtPaletteViewModel vm, BlockTarget target)
        {
            try
            {
                // Keep UI independent of the AutoCAD assembly.  The concrete host exposes
                // this optional method; reflection avoids widening the stable bridge contract.
                var method = host.GetType().GetMethod(
                    "GetSelectedBlockLayer",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(BlockTarget) },
                    null);
                if (method == null) return;

                var layer = method.Invoke(host, new object[] { target }) as string;
                if (string.IsNullOrWhiteSpace(layer)) return;

                switch (target)
                {
                    case BlockTarget.Main:
                        vm.MainLayer = layer;
                        break;
                    case BlockTarget.Furring:
                        vm.FurringLayer = layer;
                        break;
                    case BlockTarget.Hanger:
                        vm.HangerLayer = layer;
                        break;
                }
            }
            catch
            {
                // Layer sync is a convenience parity feature; a host/version-specific
                // reflection failure must never block drawing.
            }
        }

        private static void ApplyNow(VxtPaletteView view)
        {
            CompactHeaderAndFooter(view);
            SurfaceLayerAndDimPanel(view);
            RemoveDuplicateDimResourceRows(view);
            CompactScrollableContent(view);
        }

        private static void CompactHeaderAndFooter(VxtPaletteView view)
        {
            var root = view.Content as Grid;
            if (root == null) return;

            foreach (var child in root.Children)
            {
                var border = child as Border;
                if (border == null) continue;

                var row = Grid.GetRow(border);
                if (row == 0)
                {
                    border.Padding = new Thickness(10, 5, 10, 5);
                    foreach (var text in Descendants<TextBlock>(border))
                    {
                        if (text.Text == "HNL Tool")
                            text.Visibility = Visibility.Collapsed;
                        else if (text.Text == "VẼ XƯƠNG TRẦN")
                        {
                            text.FontSize = 13.0;
                            text.Margin = new Thickness(0);
                        }
                        else if (!string.IsNullOrWhiteSpace(text.Text) && text.Text.StartsWith("VXT Pro", StringComparison.OrdinalIgnoreCase))
                        {
                            text.FontSize = 9.0;
                            text.Margin = new Thickness(0, 1, 0, 0);
                        }
                        else if (!string.IsNullOrWhiteSpace(text.Text) && text.Text.IndexOf("WYSIWYG", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            text.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                else if (row == 2)
                {
                    border.Padding = new Thickness(8, 5, 8, 5);
                    foreach (var text in Descendants<TextBlock>(border))
                    {
                        if (!string.IsNullOrWhiteSpace(text.Text) &&
                            (text.Text.StartsWith("Alpha.", StringComparison.OrdinalIgnoreCase) ||
                             text.Text.IndexOf("Golden Test", StringComparison.OrdinalIgnoreCase) >= 0))
                            text.Visibility = Visibility.Collapsed;
                    }
                    foreach (var button in Descendants<Button>(border))
                        button.Height = 34.0;
                }
            }
        }

        private static void SurfaceLayerAndDimPanel(VxtPaletteView view)
        {
            var layerTitle = FindText(view, "LAYER & KIỂU NÉT") ?? FindText(view, "LAYER & DIM");
            if (layerTitle == null) return;

            layerTitle.Text = "LAYER & DIM";
            var layerExpander = Ancestor<Expander>(layerTitle);
            if (layerExpander != null) layerExpander.IsExpanded = true;

            var layerCard = AncestorBorderOwnedByStack(layerTitle);
            var scopeTitle = FindText(view, "PHẠM VI BỐ TRÍ");
            var scopeCard = AncestorBorderOwnedByStack(scopeTitle);
            var stack = layerCard?.Parent as StackPanel;
            if (stack == null || scopeCard == null || !ReferenceEquals(scopeCard.Parent, stack)) return;

            var oldIndex = stack.Children.IndexOf(layerCard);
            var scopeIndex = stack.Children.IndexOf(scopeCard);
            if (oldIndex < 0 || scopeIndex < 0) return;

            stack.Children.Remove(layerCard);
            scopeIndex = stack.Children.IndexOf(scopeCard);
            stack.Children.Insert(Math.Min(scopeIndex + 1, stack.Children.Count), layerCard);
        }

        private static void RemoveDuplicateDimResourceRows(VxtPaletteView view)
        {
            // Old view injected Layer DIM + DimStyle a second time inside the DIM card.
            // The new visible LAYER & DIM panel owns these fields, so remove the duplicate.
            var text = FindText(view, "Layer DIM");
            if (text == null) return;
            var duplicate = AncestorBorderOwnedByStack(text);
            var parent = duplicate?.Parent as StackPanel;
            if (duplicate != null && parent != null)
                parent.Children.Remove(duplicate);
        }

        private static void CompactScrollableContent(VxtPaletteView view)
        {
            foreach (var scroll in Descendants<ScrollViewer>(view))
                scroll.Padding = new Thickness(8, 6, 8, 6);

            var sectionStyle = view.Resources["SectionCard"] as Style;
            foreach (var border in Descendants<Border>(view))
            {
                if (sectionStyle != null && ReferenceEquals(border.Style, sectionStyle))
                {
                    border.Padding = new Thickness(8, 6, 8, 6);
                    border.Margin = new Thickness(0, 0, 0, 5);
                    border.CornerRadius = new CornerRadius(7);
                }
            }

            foreach (var expander in Descendants<Expander>(view))
            {
                var stack = expander.Content as StackPanel;
                if (stack != null)
                    stack.Margin = new Thickness(stack.Margin.Left, Math.Min(stack.Margin.Top, 6), stack.Margin.Right, stack.Margin.Bottom);
            }

            foreach (var grid in Descendants<Grid>(view))
            {
                if (grid.Margin.Bottom > 4 || grid.Margin.Top > 3)
                    grid.Margin = new Thickness(grid.Margin.Left, Math.Min(grid.Margin.Top, 3), grid.Margin.Right, Math.Min(grid.Margin.Bottom, 4));
                AlignFormColumns(grid);
            }

            foreach (var text in Descendants<TextBlock>(view))
            {
                if (text.Style == view.Resources["SectionTitle"] as Style)
                    text.FontSize = 11.0;
                else if (text.Style == view.Resources["FieldLabel"] as Style)
                    text.FontSize = 10.5;

                if (text.TextWrapping != TextWrapping.NoWrap && text.FontSize > 0)
                {
                    text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                    text.LineHeight = Math.Max(12.0, text.FontSize + 2.0);
                }
            }

            foreach (var textBox in Descendants<TextBox>(view))
            {
                textBox.Height = 27.0;
                textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            foreach (var combo in Descendants<ComboBox>(view))
            {
                combo.Height = 27.0;
                combo.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            foreach (var numeric in Descendants<HnlNumericBox>(view))
            {
                numeric.Height = 27.0;
                numeric.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            foreach (var button in Descendants<Button>(view))
            {
                if (button.Height > 34.0 || double.IsNaN(button.Height)) button.Height = 27.0;
            }
        }

        private static void AlignFormColumns(Grid grid)
        {
            if (grid.ColumnDefinitions.Count < 2) return;
            var first = grid.ColumnDefinitions[0].Width;
            if (!first.IsAbsolute) return;

            // Functional form grids in the original XAML use 112 / 155 / 190 px labels.
            // Normalize those three families to the same vertical axis.
            if (first.Value < 105.0 || first.Value > 195.0) return;

            grid.ColumnDefinitions[0].Width = new GridLength(LabelWidth);

            if (grid.ColumnDefinitions.Count == 2)
            {
                grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                return;
            }

            if (grid.ColumnDefinitions.Count == 3)
            {
                grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                var third = grid.ColumnDefinitions[2].Width;
                if (third.IsAbsolute && third.Value >= 60.0 && third.Value <= 105.0)
                    grid.ColumnDefinitions[2].Width = new GridLength(PickButtonWidth);
            }
        }

        private static TextBlock FindText(DependencyObject root, string value)
        {
            if (root == null) return null;
            foreach (var text in Descendants<TextBlock>(root))
                if (string.Equals(text.Text, value, StringComparison.Ordinal)) return text;
            return null;
        }

        private static Border AncestorBorderOwnedByStack(DependencyObject child)
        {
            var current = child;
            while (current != null)
            {
                var border = current as Border;
                if (border != null && border.Parent is StackPanel) return border;
                current = Parent(current);
            }
            return null;
        }

        private static T Ancestor<T>(DependencyObject child) where T : DependencyObject
        {
            var current = Parent(child);
            while (current != null)
            {
                var typed = current as T;
                if (typed != null) return typed;
                current = Parent(current);
            }
            return null;
        }

        private static DependencyObject Parent(DependencyObject child)
        {
            if (child == null) return null;
            var framework = child as FrameworkElement;
            if (framework?.Parent != null) return framework.Parent;
            try { return VisualTreeHelper.GetParent(child); }
            catch { return null; }
        }

        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            var count = 0;
            try { count = VisualTreeHelper.GetChildrenCount(root); }
            catch { }

            for (var i = 0; i < count; i++)
            {
                DependencyObject child;
                try { child = VisualTreeHelper.GetChild(root, i); }
                catch { continue; }

                var typed = child as T;
                if (typed != null) yield return typed;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }

            // Dynamically-created controls can exist in the logical tree before Loaded.
            foreach (var logical in LogicalTreeHelper.GetChildren(root))
            {
                var dep = logical as DependencyObject;
                if (dep == null) continue;
                var typed = dep as T;
                if (typed != null) yield return typed;
                foreach (var nested in Descendants<T>(dep)) yield return nested;
            }
        }
    }
}
