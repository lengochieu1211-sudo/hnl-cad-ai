using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Deterministic production order for the AutoCAD palette sections.
    /// Keep calculation/binding logic untouched; only reorders top-level cards.
    /// Desired order after the structural sections:
    /// DIM -> avoidance -> Layer/DIM resources -> Preview.
    /// </summary>
    public static class VxtPaletteSectionOrder
    {
        private const string FixedLayerDimTag = "HNL_VXT_FIXED_LAYER_DIM";

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            ApplyNow(view);
            view.Loaded += (sender, args) =>
            {
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyNow(view)),
                    DispatcherPriority.ContextIdle);
            };
        }

        private static void ApplyNow(VxtPaletteView view)
        {
            var stack = GetContentStack(view);
            if (stack == null) return;

            UIElement dim = null;
            UIElement avoidance = null;
            UIElement layerDim = null;
            UIElement preview = null;

            foreach (UIElement child in stack.Children)
            {
                var element = child as FrameworkElement;
                if (element == null) continue;

                var border = element as Border;
                if (border != null && string.Equals(border.Tag as string, FixedLayerDimTag, StringComparison.Ordinal))
                {
                    layerDim = child;
                    continue;
                }

                if (ContainsText(element, "KÍCH THƯỚC DIM"))
                    dim = child;
                else if (ContainsText(element, "NÉ THIẾT BỊ"))
                    avoidance = child;
                else if (ContainsText(element, "XEM TRƯỚC TRÊN BẢN VẼ"))
                    preview = child;
            }

            // Reorder only when all production cards are present. This avoids disturbing
            // partial designer/test trees and preserves all existing bindings/controls.
            if (dim == null || avoidance == null || layerDim == null || preview == null) return;

            stack.Children.Remove(dim);
            stack.Children.Remove(avoidance);
            stack.Children.Remove(layerDim);
            stack.Children.Remove(preview);

            // Append the final production tail in a stable order.
            stack.Children.Add(dim);
            stack.Children.Add(avoidance);
            stack.Children.Add(layerDim);
            stack.Children.Add(preview);
        }

        private static StackPanel GetContentStack(VxtPaletteView view)
        {
            var root = view.Content as Grid;
            if (root == null) return null;

            foreach (UIElement child in root.Children)
            {
                var scroll = child as ScrollViewer;
                if (scroll != null && Grid.GetRow(scroll) == 1)
                    return scroll.Content as StackPanel;
            }
            return null;
        }

        private static bool ContainsText(DependencyObject root, string value)
        {
            foreach (var node in Walk(root))
            {
                var text = node as TextBlock;
                if (text != null && string.Equals(text.Text, value, StringComparison.Ordinal))
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
