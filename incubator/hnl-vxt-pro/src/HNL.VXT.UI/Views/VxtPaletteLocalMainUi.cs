using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HNL.VXT.UI.Controls;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Surfaces the legacy-Lisp concave/notch local-XC controls without changing the Core
    /// algorithm. The Core already owns UseLocalMainAdd + MinLocalMainLength; this class only
    /// restores their missing palette controls.
    /// </summary>
    public static class VxtPaletteLocalMainUi
    {
        private const string LocalMainUiTag = "HNL_VXT_LOCAL_MAIN_UI";

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            var mainExpander = FindExpanderByHeader(view, "XƯƠNG CHÍNH");
            var body = mainExpander?.Content as StackPanel;
            if (body == null || ContainsTag(body, LocalMainUiTag)) return;

            var toggleRow = CreateToggleRow(view);
            var minimumRow = CreateMinimumLengthRow(view);
            toggleRow.Tag = LocalMainUiTag;

            var insertIndex = FindDynamicBlockSectionIndex(body);
            body.Children.Insert(insertIndex, toggleRow);
            body.Children.Insert(insertIndex + 1, minimumRow);
        }

        private static Grid CreateToggleRow(VxtPaletteView view)
        {
            var row = CreateBaseRow(view, "Thêm XC cục bộ cạnh khuyết");
            row.Margin = new Thickness(0, 0, 0, 5);

            var toggle = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Giống Lisp: ưu tiên dịch Xương chính toàn cục trước; chỉ thêm Xương chính ngắn tại vùng cạnh khuyết khi vẫn chưa đạt điều kiện Max."
            };
            var style = view.TryFindResource("ToggleSwitch") as Style;
            if (style != null) toggle.Style = style;
            toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(VxtPaletteViewModel.UseLocalMainAdd))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);
            return row;
        }

        private static Grid CreateMinimumLengthRow(VxtPaletteView view)
        {
            var row = CreateBaseRow(view, "Chiều dài XC cục bộ tối thiểu");
            row.Margin = new Thickness(0, 0, 0, 7);

            var input = new HnlNumericBox
            {
                Height = 27.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Minimum = 0,
                Step = 50,
                ToolTip = "Không tạo Xương chính cục bộ ngắn hơn giá trị này. Mặc định HNL Tool: 500."
            };
            input.SetBinding(HnlNumericBox.ValueProperty, new Binding(nameof(VxtPaletteViewModel.MinLocalMainLength))
            {
                Mode = BindingMode.TwoWay
            });
            input.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(VxtPaletteViewModel.UseLocalMainAdd)));

            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            return row;
        }

        private static Grid CreateBaseRow(VxtPaletteView view, string label)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var text = new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = label
            };
            var style = view.TryFindResource("FieldLabel") as Style;
            if (style != null) text.Style = style;
            row.Children.Add(text);
            return row;
        }

        private static int FindDynamicBlockSectionIndex(StackPanel body)
        {
            for (var i = 0; i < body.Children.Count; i++)
            {
                var node = body.Children[i] as DependencyObject;
                if (ContainsText(node, "Sử dụng Block động") || ContainsText(node, "Block sử dụng"))
                    return i;
            }
            return body.Children.Count;
        }

        private static Expander FindExpanderByHeader(DependencyObject root, string title)
        {
            foreach (var node in Walk(root))
            {
                var expander = node as Expander;
                if (expander != null && ContainsText(expander.Header as DependencyObject, title))
                    return expander;
            }
            return null;
        }

        private static bool ContainsText(DependencyObject root, string value)
        {
            if (root == null) return false;
            foreach (var node in Walk(root))
            {
                var text = node as TextBlock;
                if (text != null && string.Equals(text.Text, value, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool ContainsTag(DependencyObject root, string tag)
        {
            foreach (var node in Walk(root))
            {
                var element = node as FrameworkElement;
                if (element != null && string.Equals(element.Tag as string, tag, StringComparison.Ordinal))
                    return true;
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

                var userControl = current as UserControl;
                var userContent = userControl?.Content as DependencyObject;
                if (userContent != null) queue.Enqueue(userContent);
            }
        }
    }
}
