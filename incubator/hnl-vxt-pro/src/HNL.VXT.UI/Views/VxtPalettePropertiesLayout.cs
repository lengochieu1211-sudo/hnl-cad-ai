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
    /// Final presentation-only layout pass inspired by the native AutoCAD Properties palette.
    ///
    /// This pass never replaces bindings, commands, HNL colors or geometry logic. It only
    /// normalizes the palette chrome into dense group bars and Property | Value rows.
    /// Existing CardBackground/HoverBackground/CardBorder resources remain authoritative so
    /// Dark/Light themes and XC/XP/Ty/DIM/MEP colors continue to come from the existing theme.
    /// </summary>
    public static class VxtPalettePropertiesLayout
    {
        private const string HeaderTag = "HNL_VXT_PROPERTIES_GROUP_HEADER";
        private const string RowDividerTag = "HNL_VXT_PROPERTIES_ROW_DIVIDER";
        private const string ColumnDividerTag = "HNL_VXT_PROPERTIES_COLUMN_DIVIDER";
        private const double PropertyRowMinHeight = 24.0;

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            ApplyNow(view);
            view.Loaded += (sender, args) =>
            {
                // Run after dynamic panels, typography and visual-identity passes have finished.
                // The pass is intentionally idempotent because theme/appearance controls can
                // cause the palette tree to be revisited during the same AutoCAD session.
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyNow(view)),
                    DispatcherPriority.ApplicationIdle);
            };
        }

        private static void ApplyNow(VxtPaletteView view)
        {
            var scroll = FindFirst<ScrollViewer>(view);
            if (scroll == null) return;

            // Native Properties uses a very small outer gutter rather than floating cards.
            scroll.Padding = new Thickness(2);

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

            // Preserve any deliberate HNL accent BorderBrush/Background already assigned by the
            // original palette. Only spacing, shape and separators are normalized here.
            if (card.BorderBrush == null)
                card.BorderBrush = cardBorder;

            card.CornerRadius = new CornerRadius(0);
            card.BorderThickness = new Thickness(1);
            card.Margin = new Thickness(0, 0, 0, 1);

            var expander = card.Child as Expander;
            if (expander != null)
            {
                card.Padding = new Thickness(0);
                expander.HorizontalContentAlignment = HorizontalAlignment.Stretch;

                WrapGroupHeader(expander, headerBackground, cardBorder);

                var content = expander.Content as FrameworkElement;
                if (content != null)
                {
                    // Property rows themselves provide their own cell padding, so the content can
                    // sit almost flush with the group border like AutoCAD Properties.
                    content.Margin = new Thickness(1, 2, 1, 2);
                    NormalizePropertyRows(content, cardBorder);
                    FlattenNestedPropertyGroups(content, cardBorder);
                }
                return;
            }

            // Preset / Scope / diagnostics / Preview are not all pure property grids. Keep a small
            // inner inset for their action/status content, while any detected two-column property
            // row is still normalized to the same grid contract.
            card.Padding = new Thickness(6, 4, 6, 5);
            NormalizePropertyRows(card.Child, cardBorder);
            FlattenNestedPropertyGroups(card.Child, cardBorder);
        }

        private static void WrapGroupHeader(Expander expander, Brush background, Brush borderBrush)
        {
            var existingBorder = expander.Header as Border;
            if (existingBorder != null &&
                string.Equals(existingBorder.Tag as string, HeaderTag, StringComparison.Ordinal))
            {
                existingBorder.Background = background;
                existingBorder.BorderBrush = borderBrush;
                existingBorder.Padding = new Thickness(6, 3, 6, 3);
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
                Padding = new Thickness(6, 3, 6, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = visual
            };

            expander.Header = header;
        }

        private static void NormalizePropertyRows(DependencyObject root, Brush dividerBrush)
        {
            if (root == null) return;

            // Snapshot first. Adding divider Borders while traversing a Grid must not make the
            // classification of later rows depend on mutation order.
            var rows = new List<Grid>();
            foreach (var node in Walk(root))
            {
                var grid = node as Grid;
                if (grid != null && LooksLikePropertyRow(grid))
                    rows.Add(grid);
            }

            foreach (var grid in rows)
            {
                var labelColumn = FindLabelColumn(grid);
                if (labelColumn < 0) continue;

                grid.Margin = new Thickness(0);
                grid.MinHeight = PropertyRowMinHeight;
                grid.SnapsToDevicePixels = true;

                NormalizeColumns(grid, labelColumn);
                NormalizeCellMargins(grid, labelColumn);
                NormalizeValueEditors(grid);
                AddOrUpdateHorizontalDivider(grid, dividerBrush);
                AddOrUpdateColumnDivider(grid, labelColumn, dividerBrush);
            }
        }

        private static void NormalizeColumns(Grid grid, int labelColumn)
        {
            var columns = grid.ColumnDefinitions;
            if (columns == null || columns.Count < 2) return;

            if (labelColumn == 0)
            {
                // Standard Properties contract: one stable property column and one elastic value
                // column. Any third action column (Pick/Setup) keeps its original fixed width.
                columns[0].Width = new GridLength(43, GridUnitType.Star);
                columns[1].Width = new GridLength(57, GridUnitType.Star);
                return;
            }

            if (labelColumn == 1 && columns.Count >= 3)
            {
                // DIM rows carry an enable CheckBox before the property label. Preserve that
                // leading affordance, then split the remaining area into Property | Value.
                columns[0].Width = new GridLength(30);
                columns[1].Width = new GridLength(36, GridUnitType.Star);
                columns[2].Width = new GridLength(64, GridUnitType.Star);
            }
        }

        private static void NormalizeCellMargins(Grid grid, int labelColumn)
        {
            foreach (UIElement child in grid.Children)
            {
                if (IsDivider(child)) continue;

                var column = Grid.GetColumn(child);
                var text = child as TextBlock;
                if (text != null)
                {
                    text.Margin = new Thickness(5, 0, 5, 0);
                    text.VerticalAlignment = VerticalAlignment.Center;
                    continue;
                }

                var check = child as CheckBox;
                if (check != null)
                {
                    check.Margin = column <= labelColumn
                        ? new Thickness(5, 0, 3, 0)
                        : new Thickness(4, 0, 2, 0);
                    check.VerticalAlignment = VerticalAlignment.Center;
                    continue;
                }

                var button = child as Button;
                if (button != null)
                {
                    button.Margin = new Thickness(3, 0, 2, 0);
                    continue;
                }

                // Editors occupy the value cell rather than floating inside card padding.
                if (child is TextBox || child is ComboBox || child is HnlNumericBox)
                    ((FrameworkElement)child).Margin = new Thickness(0);
            }
        }

        private static void NormalizeValueEditors(Grid grid)
        {
            foreach (var node in Walk(grid))
            {
                var textBox = node as TextBox;
                if (textBox != null)
                {
                    textBox.Height = PropertyRowMinHeight;
                    textBox.MinHeight = 0;
                    textBox.Padding = new Thickness(4, 0, 4, 0);
                    textBox.VerticalContentAlignment = VerticalAlignment.Center;
                    textBox.BorderThickness = new Thickness(1);
                    continue;
                }

                var combo = node as ComboBox;
                if (combo != null)
                {
                    combo.Height = PropertyRowMinHeight;
                    combo.MinHeight = 0;
                    combo.Padding = new Thickness(4, 0, 4, 0);
                    combo.VerticalContentAlignment = VerticalAlignment.Center;
                    combo.BorderThickness = new Thickness(1);
                    continue;
                }

                var numeric = node as HnlNumericBox;
                if (numeric != null)
                {
                    numeric.Height = PropertyRowMinHeight;
                    numeric.MinHeight = PropertyRowMinHeight;
                    numeric.VerticalAlignment = VerticalAlignment.Center;
                    continue;
                }

                var button = node as Button;
                if (button != null)
                {
                    button.Height = PropertyRowMinHeight;
                    button.MinHeight = PropertyRowMinHeight;
                    button.Padding = new Thickness(6, 1, 6, 1);
                    button.VerticalContentAlignment = VerticalAlignment.Center;
                }
            }
        }

        private static void AddOrUpdateHorizontalDivider(Grid grid, Brush dividerBrush)
        {
            var divider = FindTaggedBorder(grid, RowDividerTag);
            if (divider == null)
            {
                divider = new Border
                {
                    Tag = RowDividerTag,
                    Height = 1,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    IsHitTestVisible = false
                };
                Grid.SetColumn(divider, 0);
                Grid.SetColumnSpan(divider, Math.Max(1, grid.ColumnDefinitions.Count));
                Panel.SetZIndex(divider, 1000);
                grid.Children.Add(divider);
            }

            divider.Background = dividerBrush;
            divider.Opacity = 0.68;
        }

        private static void AddOrUpdateColumnDivider(Grid grid, int labelColumn, Brush dividerBrush)
        {
            var divider = FindTaggedBorder(grid, ColumnDividerTag);
            if (divider == null)
            {
                divider = new Border
                {
                    Tag = ColumnDividerTag,
                    Width = 1,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsHitTestVisible = false
                };
                Grid.SetColumn(divider, labelColumn);
                Panel.SetZIndex(divider, 1000);
                grid.Children.Add(divider);
            }
            else
            {
                Grid.SetColumn(divider, labelColumn);
            }

            divider.Background = dividerBrush;
            divider.Opacity = 0.82;
        }

        private static Border FindTaggedBorder(Grid grid, string tag)
        {
            foreach (UIElement child in grid.Children)
            {
                var border = child as Border;
                if (border != null && string.Equals(border.Tag as string, tag, StringComparison.Ordinal))
                    return border;
            }
            return null;
        }

        private static bool IsDivider(UIElement element)
        {
            var border = element as Border;
            if (border == null) return false;
            var tag = border.Tag as string;
            return string.Equals(tag, RowDividerTag, StringComparison.Ordinal) ||
                   string.Equals(tag, ColumnDividerTag, StringComparison.Ordinal);
        }

        private static int FindLabelColumn(Grid grid)
        {
            var best = int.MaxValue;
            foreach (UIElement child in grid.Children)
            {
                if (IsDivider(child)) continue;

                var text = child as TextBlock;
                if (text == null || string.IsNullOrWhiteSpace(text.Text)) continue;

                var column = Grid.GetColumn(text);
                if (column < best)
                    best = column;
            }

            return best == int.MaxValue ? -1 : best;
        }

        private static bool LooksLikePropertyRow(Grid grid)
        {
            if (grid.ColumnDefinitions == null || grid.ColumnDefinitions.Count < 2)
                return false;

            var labelColumn = FindLabelColumn(grid);
            if (labelColumn < 0) return false;

            // Require a real editor/action after the label. This deliberately avoids treating
            // structural header/preview grids as property rows merely because they contain text.
            foreach (UIElement child in grid.Children)
            {
                if (IsDivider(child)) continue;
                if (Grid.GetColumn(child) <= labelColumn) continue;

                if (child is TextBox || child is ComboBox || child is HnlNumericBox ||
                    child is Button || child is CheckBox)
                    return true;
            }

            return false;
        }

        private static void FlattenNestedPropertyGroups(DependencyObject root, Brush borderBrush)
        {
            if (root == null) return;

            foreach (var node in Walk(root))
            {
                var border = node as Border;
                if (border == null || border.Child == null) continue;

                // Only flatten genuinely rounded mini-cards that contain multiple property rows.
                // Status badges/alerts use rounded borders too, but contain no property grid and
                // therefore retain their original HNL appearance and colors.
                if (!HasRoundedCorner(border.CornerRadius)) continue;
                if (CountPropertyRows(border.Child, 2) < 2) continue;

                border.CornerRadius = new CornerRadius(0);
                border.BorderBrush = borderBrush;
                border.BorderThickness = new Thickness(1);
                border.Padding = new Thickness(0);
                border.Margin = new Thickness(0, 0, 0, 1);

                var panel = border.Child as StackPanel;
                if (panel != null && panel.Children.Count > 0)
                {
                    var title = panel.Children[0] as TextBlock;
                    if (title != null)
                        title.Margin = new Thickness(5, 3, 5, 3);
                }
            }
        }

        private static int CountPropertyRows(DependencyObject root, int stopAfter)
        {
            var count = 0;
            foreach (var node in Walk(root))
            {
                var grid = node as Grid;
                if (grid == null || !LooksLikePropertyRow(grid)) continue;
                count++;
                if (count >= stopAfter) break;
            }
            return count;
        }

        private static bool HasRoundedCorner(CornerRadius radius)
        {
            return radius.TopLeft > 0 || radius.TopRight > 0 ||
                   radius.BottomLeft > 0 || radius.BottomRight > 0;
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
