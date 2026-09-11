using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Adds one compact optimization selector to the existing CẤU HÌNH card without expanding
    /// the XC/XP/Ty/DIM sections. Created before the final ComboBox theme pass.
    /// </summary>
    public static class VxtPaletteOptimizerUi
    {
        private const string AppliedTag = "HNL_VXT_OPTIMIZER_SELECTOR";

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;
            ApplyNow(view);
            view.Loaded += (sender, args) => ApplyNow(view);
        }

        private static void ApplyNow(VxtPaletteView view)
        {
            var card = FindConfigCard(view);
            if (card == null || string.Equals(card.Tag as string, AppliedTag, StringComparison.Ordinal)) return;

            var original = card.Child as Grid;
            if (original == null) return;

            card.Child = null;
            var stack = new StackPanel();
            stack.Children.Add(original);

            var row = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(122.0) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = "CHẾ ĐỘ TỐI ƯU",
                Style = view.TryFindResource("SectionTitle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            var combo = new ComboBox
            {
                Height = 27.0,
                ToolTip = "Bố trí truyền thống: giữ cách bố trí đã kiểm chứng. Cân đối: ưu tiên bố trí đều và hợp lý. Tiết kiệm vật tư: ưu tiên giảm tổng vật tư. Ưu tiên ổn định: ưu tiên khoảng cách an toàn, tính đều và né thiết bị."
            };
            combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("OptimizationModeOptions"));
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedOptimizationMode")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetColumn(combo, 1);
            row.Children.Add(label);
            row.Children.Add(combo);
            stack.Children.Add(row);

            card.Child = stack;
            card.Tag = AppliedTag;
        }

        private static Border FindConfigCard(DependencyObject root)
        {
            foreach (var node in Walk(root))
            {
                var border = node as Border;
                if (border != null && ContainsText(border, "CẤU HÌNH")) return border;
            }
            return null;
        }

        private static bool ContainsText(DependencyObject root, string expected)
        {
            foreach (var node in Walk(root))
            {
                var text = node as TextBlock;
                if (text != null && string.Equals(text.Text, expected, StringComparison.Ordinal)) return true;
            }
            return false;
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
                    foreach (UIElement child in panel.Children)
                        if (child != null) queue.Enqueue(child);

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
            }
        }
    }
}
