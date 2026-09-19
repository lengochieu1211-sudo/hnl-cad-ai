using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HNL.VXT.UI.Controls;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Final presentation-only layout pass inspired by AutoCAD Properties.
    ///
    /// It intentionally does not replace bindings, commands, colors or HNL identity. Existing
    /// CardBackground/HoverBackground/CardBorder resources are reused so Dark/Light themes and
    /// XC/XP/Ty/DIM/MEP colors remain exactly under the existing theme system.
    /// </summary>
    public static class VxtPalettePropertiesLayout
    {
        private const string HeaderTag = "HNL_VXT_PROPERTIES_GROUP_HEADER";
        private const string RowDividerTag = "HNL_VXT_PROPERTIES_ROW_DIVIDER";

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            ApplyNow(view);
            view.Loaded += (sender, args) =>
            {
                // Run after the dynamic palette, typography and visual identity passes have
                // materialized their final controls. This keeps the pass idempotent and visual only.
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyNow(view)),
                    DispatcherPriority.ApplicationIdle);
            };
        }

        private static void ApplyNow(VxtPaletteView view)
        {
            var scroll = FindFirst<ScrollViewer>(view);
            if (scroll == null) return;

            // AutoCAD Properties uses a narrow gutter instead of floating cards.
            scroll.Padding = new Thickness(4, 4, 4, 4);

            var stack = scroll.Content as StackPanel;
            if (stack == null) return;

            stack.Margin = new Thickness(0);

            foreach (UIElement child in stack.Children)
            {
                var card = child as Border;
                if (card == null) continue;
                FlattenSection(view, card);
            }
        }

        private static void FlattenSection(VxtPaletteView view, Border card)
        {
            var cardBorder = ResourceBrush(view, "CardBorder", Brushes.DimGray);
            var headerBackground = ResourceBrush(view, "HoverBackground", Brushes.DimGray);

            // Keep the existing Background/BorderBrush when a section deliberately uses an HNL
            // accent (Preset, Preview, DIM etc.). Only geometry/spacing is normalized here.
            if (card.BorderBrush == null)
                card.BorderBrush = cardBorder;

            card.CornerRadius = new CornerRadius(0);
            card.BorderThickness = new Thickness(1);
            card.Margin = new Thickness(0, 0, 0, 2);

            var expander = card.Child as Expander;
            if (expander != null)
            {
                card.Padding = new Thickness(0);
                expander.HorizontalContentAlignment = HorizontalAlignment.Stretch;

                WrapGroupHeader(expander, headerBackground, cardBorder);

                var content = expander.Content as FrameworkElement;
                if (content != null)
                {
                    content.Margin = new Thickness(7, 4, 7, 5);
                    NormalizePropertyRows(content, cardBorder);
                }
                return;
            }

            // Non-expandable cards (Preset, Scope, diagnostics, Preview) still become flat blocks,
            // but retain their original accent/background and functional content.
            card.Padding = new Thickness(7, 5, 7, 6);
            NormalizePropertyRows(card.Child, cardBorder);
        }

        private static void WrapGroupHeader(Expander expander, Brush background, Brush borderBrush)
        {
            var existingBorder = expander.Header as Border;
            if (existingBorder != null &&
                string.Equals(existingBorder.Tag as string, HeaderTag, StringComparison.Ordinal))
            {
                existingBorder.Background = background;
                existingBorder.BorderBrush = borderBrush;
                return;
            }

            var original = expander.Header;
            if (original == null) return;

            // Detach first so WPF never sees the same UIElement with two logical parents.
            expander.Header = null;

            UIElement visual;
            var element = original as UIElement;
            if (element != null)
            {
                visual = element;
            }
            else
            {
                visual = new ContentPresenter
                {
                    Content = original,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            var header = new Border
            {
                Tag = HeaderTag,
                Background = background,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(7, 4, 7, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = visual
            };

            expander.Header = header;
        }

        private static void NormalizePropertyRows(DependencyObject root, Brush dividerBrush)
        {
            if (root == null) return;

            foreach (var node in Walk(root))
            {
                var grid = node as Grid;
                if (grid == null || !LooksLikePropertyRow(grid)) continue;

                grid.Margin = new Thickness(0);
                grid.MinHeight = Math.Max(grid.MinHeight, 28.0);

                var alreadyHasDivider = false;
                foreach (UIElement child in grid.Children)
                {
                    var border = child as Border;
                    if (border != null &&
                        string.Equals(border.Tag as string, RowDividerTag, StringComparison.Ordinal))
                    {
                        alreadyHasDivider = true;
                        border.Background = dividerBrush;
                        break;
                    }
                }

                if (alreadyHasDivider) continue;

                var divider = new Border
                {
                    Tag = RowDividerTag,
                    Height = 1,
                    Background = dividerBrush,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    IsHitTestVisible = false,
                    Opacity = 0.65
                };
                Grid.SetColumn(divider, 0);
                Grid.SetColumnSpan(divider, Math.Max(1, grid.ColumnDefinitions.Count));
                Panel.SetZIndex(divider, 1000);
                grid.Children.Add(divider);
            }
        }

        private static bool LooksLikePropertyRow(Grid grid)
        {
            if (grid.ColumnDefinitions == null || grid.ColumnDefinitions.Count < 2)
                return false;

            var hasValueControl = false;
            var hasLabelOrStatus = false;

            foreach (var node in Walk(grid))
            {
                if (ReferenceEquals(node, grid)) continue;

                if (node is TextBox || node is ComboBox || node is HnlNumericBox ||
                    node is Button || node is CheckBox)
                    hasValueControl = true;

                var text = node as TextBlock;
                if (text != null && !string.IsNullOrWhiteSpace(text.Text))
                    hasLabelOrStatus = true;

                if (hasValueControl && hasLabelOrStatus)
                    return true;
            }

            return false;
        }

        private static Brush ResourceBrush(FrameworkElement element, string key, Brush fallback)
            => element?.TryFindResource(key) as Brush ?? fallback;

        private static T FindFirst<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            foreach (var node in Walk(root))
            {
                var typed = node as T;
                if (typed != null) return typed;
            }
            return null;
        }

        private static IEnumerable<DependencyObject> Walk(DependencyObject root)
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
                if (border != null && border.Child != null)
                    queue.Enqueue(border.Child);

                var headered = current as HeaderedContentControl;
                if (headered != null)
                {
                    var header = headered.Header as DependencyObject;
                    var content = headered.Content as DependencyObject;
                    if (header != null) queue.Enqueue(header);
                    if (content != null) queue.Enqueue(content);
                    continue;
                }

                var contentControl = current as ContentControl;
                if (contentControl != null)
                {
                    var content = contentControl.Content as DependencyObject;
                    if (content != null) queue.Enqueue(content);
                }

                var userControl = current as UserControl;
                if (userControl != null && userControl.Content is DependencyObject userContent)
                    queue.Enqueue(userContent);
            }
        }
    }
}
