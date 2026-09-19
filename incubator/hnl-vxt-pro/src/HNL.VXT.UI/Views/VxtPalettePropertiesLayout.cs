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
        private const double PropertyColumnWidth = 136.0;
        private const double PropertyRowMinHeight = 26.0;
        private const double EditorHeight = 24.0;

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
            scroll.Padding = new Thickness(3, 3, 3, 4);

            var stack = scroll.Content as StackPanel;
            if (stack == null) return;

            stack.Margin = new Thickness(0);

            foreach (UIElement child in stack.Children)
            {
                var card = child as Border;
                if (card == null) continue;
                FlattenSection(view, card);
            }

            NormalizeVisibleSectionTitles(view);
        }

        private static void NormalizeVisibleSectionTitles(DependencyObject root)
        {
            foreach (var node in Walk(root))
            {
                var text = node as TextBlock;
                if (text == null || string.IsNullOrWhiteSpace(text.Text)) continue;

                switch (text.Text.Trim())
                {
                    case "CẤU HÌNH": text.Text = "Cấu hình"; break;
                    case "PHẠM VI BỐ TRÍ": text.Text = "Phạm vi bố trí"; break;
                    case "XƯƠNG CHÍNH": text.Text = "Xương chính"; break;
                    case "XƯƠNG PHỤ": text.Text = "Xương phụ"; break;
                    case "TY TREO": text.Text = "Ty treo"; break;
                    case "NÉ THIẾT BỊ MEP": text.Text = "Né thiết bị MEP"; break;
                    case "NÉ THIẾT BỊ": text.Text = "Né thiết bị"; break;
                    case "KÍCH THƯỚC DIM": text.Text = "Kích thước DIM"; break;
                    case "XEM TRƯỚC TRÊN BẢN VẼ": text.Text = "Xem trước trên bản vẽ"; break;
                    case "LAYER & KIỂU NÉT": text.Text = "Layer & kiểu nét"; break;
                    case "LAYER & DIM": text.Text = "Layer & DIM"; break;
                    case "LAYER & HIỂN THỊ": text.Text = "Layer & hiển thị"; break;
                    case "CÀI ĐẶT LAYER & DIM": text.Text = "Cài đặt Layer & DIM"; break;
                    case "GIAO DIỆN": text.Text = "Giao diện"; break;
                    case "PHÂN TÍCH & KIỂM TRA LỖI": text.Text = "Phân tích & kiểm tra lỗi"; break;
                    case "CHẨN ĐOÁN": text.Text = "Chẩn đoán"; break;
                }
            }
        }

        private static void FlattenSection(VxtPaletteView view, Border card)
        {
            var cardBorder = ResourceBrush(view, "CardBorder", Brushes.DimGray);
            var headerBackground = Brushes.Transparent;

            // Preserve any deliberate HNL accent BorderBrush/Background already assigned by the
            // original palette. Only spacing, shape and separators are normalized here.
            if (card.BorderBrush == null)
                card.BorderBrush = cardBorder;

            card.CornerRadius = new CornerRadius(2);
            card.BorderThickness = new Thickness(1);
            card.Margin = new Thickness(0, 0, 0, 4);

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
                    content.Margin = new Thickness(5, 3, 5, 4);
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
                existingBorder.Background = Brushes.Transparent;
                existingBorder.BorderBrush = Brushes.Transparent;
                existingBorder.BorderThickness = new Thickness(0);
                existingBorder.Padding = new Thickness(6, 2, 6, 2);
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
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(6, 2, 6, 2),
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

                grid.Margin = new Thickness(0, 0, 0, 1);
                grid.MinHeight = PropertyRowMinHeight;
                grid.SnapsToDevicePixels = true;

                NormalizeColumns(grid, labelColumn);
                NormalizeCellMargins(grid, labelColumn);
                NormalizeValueEditors(grid);
                RemoveDivider(grid, RowDividerTag);
                RemoveDivider(grid, ColumnDividerTag);
            }
        }

        private static void NormalizeColumns(Grid grid, int labelColumn)
        {
            var columns = grid.ColumnDefinitions;
            if (columns == null || columns.Count < 2) return;

            if (IsPairedMinMaxRow(grid))
            {
                // CompactTuner intentionally combines Min + Max into one six-column row:
                // Property | Min | value | gap | Max | value.
                // Do NOT collapse this back into a two-column Properties row.
                columns[0].Width = new GridLength(PropertyColumnWidth);
                columns[1].Width = new GridLength(34);
                columns[2].Width = new GridLength(1, GridUnitType.Star);
                columns[3].Width = new GridLength(6);
                columns[4].Width = new GridLength(34);
                columns[5].Width = new GridLength(1, GridUnitType.Star);
                return;
            }

            if (labelColumn == 0)
            {
                // Match the visual divider behavior of AutoCAD Properties: keep one stable
                // property-name column and let the value area absorb palette resizing. Any
                // trailing Pick/Setup button column keeps its existing fixed width.
                columns[0].Width = new GridLength(PropertyColumnWidth);
                columns[1].Width = new GridLength(1, GridUnitType.Star);
                return;
            }

            if (labelColumn == 1 && columns.Count >= 3)
            {
                // DIM rows carry an enable CheckBox before the property label. Preserve that
                // leading affordance while keeping the visual Property/Value divider aligned
                // with standard rows.
                columns[0].Width = new GridLength(30);
                columns[1].Width = new GridLength(PropertyColumnWidth - 30.0);
                columns[2].Width = new GridLength(1, GridUnitType.Star);
            }
        }

        private static bool IsPairedMinMaxRow(Grid grid)
        {
            var columns = grid.ColumnDefinitions;
            if (columns == null || columns.Count != 6) return false;

            var hasFirstNumeric = false;
            var hasSecondNumeric = false;
            var hasMinToken = false;
            var hasMaxToken = false;

            foreach (UIElement child in grid.Children)
            {
                if (IsDivider(child)) continue;

                if (child is HnlNumericBox)
                {
                    if (Grid.GetColumn(child) == 2) hasFirstNumeric = true;
                    if (Grid.GetColumn(child) == 5) hasSecondNumeric = true;
                }

                var text = child as TextBlock;
                if (text == null) continue;

                if (Grid.GetColumn(text) == 1 &&
                    string.Equals(text.Text, "Min", StringComparison.OrdinalIgnoreCase))
                    hasMinToken = true;

                if (Grid.GetColumn(text) == 4 &&
                    string.Equals(text.Text, "Max", StringComparison.OrdinalIgnoreCase))
                    hasMaxToken = true;
            }

            return hasFirstNumeric && hasSecondNumeric && hasMinToken && hasMaxToken;
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
                    if (IsPairedMinMaxRow(grid) && (column == 1 || column == 4))
                    {
                        // Min/Max are compact tokens, not property labels. Give the full token
                        // column to the word so "Max" cannot be clipped by label-cell margins.
                        text.Margin = new Thickness(0);
                        text.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                    else
                    {
                        text.Margin = new Thickness(1, 0, 5, 0);
                    }
                    text.VerticalAlignment = VerticalAlignment.Center;
                    continue;
                }

                var check = child as CheckBox;
                if (check != null)
                {
                    check.Margin = column <= labelColumn
                        ? new Thickness(1, 0, 4, 0)
                        : new Thickness(3, 0, 2, 0);
                    check.VerticalAlignment = VerticalAlignment.Center;
                    continue;
                }

                var button = child as Button;
                if (button != null)
                {
                    button.Margin = new Thickness(4, 0, 0, 0);
                    continue;
                }

                // Editors occupy the value cell rather than floating inside card padding.
                if (child is TextBox || child is ComboBox || child is HnlNumericBox)
                    ((FrameworkElement)child).Margin = new Thickness(2, 0, 0, 0);
            }
        }

        private static void NormalizeValueEditors(Grid grid)
        {
            foreach (var node in Walk(grid))
            {
                var textBox = node as TextBox;
                if (textBox != null)
                {
                    textBox.Height = EditorHeight;
                    textBox.MinHeight = 0;
                    textBox.Padding = new Thickness(5, 0, 5, 0);
                    textBox.VerticalContentAlignment = VerticalAlignment.Center;
                    textBox.BorderThickness = new Thickness(1);
                    continue;
                }

                var combo = node as ComboBox;
                if (combo != null)
                {
                    combo.Height = EditorHeight;
                    combo.MinHeight = 0;
                    combo.Padding = new Thickness(5, 0, 5, 0);
                    combo.VerticalContentAlignment = VerticalAlignment.Center;
                    combo.BorderThickness = new Thickness(1);
                    continue;
                }

                var numeric = node as HnlNumericBox;
                if (numeric != null)
                {
                    numeric.Height = EditorHeight;
                    numeric.MinHeight = EditorHeight;
                    numeric.VerticalAlignment = VerticalAlignment.Center;
                    continue;
                }

                var button = node as Button;
                if (button != null)
                {
                    button.Height = EditorHeight;
                    button.MinHeight = EditorHeight;
                    button.Padding = new Thickness(6, 1, 6, 1);
                    button.VerticalContentAlignment = VerticalAlignment.Center;
                }
            }
        }

        private static void RemoveDivider(Grid grid, string tag)
        {
            var divider = FindTaggedBorder(grid, tag);
            if (divider != null)
                grid.Children.Remove(divider);
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

                border.CornerRadius = new CornerRadius(2);
                border.BorderBrush = borderBrush;
                border.BorderThickness = new Thickness(1);
                border.Padding = new Thickness(4, 3, 4, 4);
                border.Margin = new Thickness(0, 0, 0, 3);

                var panel = border.Child as StackPanel;
                if (panel != null && panel.Children.Count > 0)
                {
                    var title = panel.Children[0] as TextBlock;
                    if (title != null)
                        title.Margin = new Thickness(1, 0, 1, 2);
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
