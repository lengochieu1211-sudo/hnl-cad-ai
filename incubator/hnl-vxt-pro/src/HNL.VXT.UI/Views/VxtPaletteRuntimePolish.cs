using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Final AutoCAD-hosted palette polish pass.
    /// Runs after dynamic panels have been created so placement does not depend on
    /// WPF visual-tree materialization order inside PaletteSet.
    /// </summary>
    public static class VxtPaletteRuntimePolish
    {
        private static readonly string[] RedundantSectionLabels =
        {
            "Bật Xương chính",
            "Bật Xương phụ",
            "Bật Ty treo"
        };

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
            CompactHeader(view);
            PlaceLayerAndDimAfterScope(view);
            HideRedundantSectionLabels(view);
        }

        private static void CompactHeader(VxtPaletteView view)
        {
            var root = view.Content as Grid;
            if (root == null) return;

            Border header = null;
            foreach (UIElement child in root.Children)
            {
                var border = child as Border;
                if (border != null && Grid.GetRow(border) == 0)
                {
                    header = border;
                    break;
                }
            }
            if (header == null) return;

            header.Padding = new Thickness(8, 4, 8, 4);

            var headerGrid = header.Child as Grid;
            if (headerGrid == null) return;

            StackPanel titlePanel = null;
            Border liveBadge = null;
            foreach (UIElement child in headerGrid.Children)
            {
                var stack = child as StackPanel;
                if (stack != null && Grid.GetColumn(stack) == 1)
                    titlePanel = stack;

                var badge = child as Border;
                if (badge != null && Grid.GetColumn(badge) == 2)
                    liveBadge = badge;
            }

            if (liveBadge != null)
                liveBadge.Visibility = Visibility.Collapsed;

            if (titlePanel == null) return;

            var texts = new List<TextBlock>();
            foreach (UIElement child in titlePanel.Children)
            {
                var text = child as TextBlock;
                if (text != null) texts.Add(text);
            }

            // XAML contract: HNL Tool / title / version / subtitle.
            // Keep only the product title and version in the compact production header.
            if (texts.Count > 0) texts[0].Visibility = Visibility.Collapsed;
            if (texts.Count > 1)
            {
                texts[1].Visibility = Visibility.Visible;
                texts[1].FontSize = 13.0;
                texts[1].Margin = new Thickness(0);
            }
            if (texts.Count > 2)
            {
                texts[2].Visibility = Visibility.Visible;
                texts[2].FontSize = 9.0;
                texts[2].Margin = new Thickness(0, 1, 0, 0);
            }
            if (texts.Count > 3) texts[3].Visibility = Visibility.Collapsed;
        }

        private static void PlaceLayerAndDimAfterScope(VxtPaletteView view)
        {
            var stack = GetContentStack(view);
            if (stack == null) return;

            FrameworkElement scopeCard = null;
            FrameworkElement layerCard = null;

            foreach (UIElement child in stack.Children)
            {
                var element = child as FrameworkElement;
                if (element == null) continue;

                if (scopeCard == null && ContainsText(element, "PHẠM VI BỐ TRÍ"))
                    scopeCard = element;

                if (layerCard == null &&
                    (ContainsText(element, "LAYER & KIỂU NÉT") || ContainsText(element, "LAYER & DIM")))
                    layerCard = element;
            }

            if (scopeCard == null || layerCard == null || ReferenceEquals(scopeCard, layerCard)) return;

            var layerTitle = FindText(layerCard, "LAYER & KIỂU NÉT") ?? FindText(layerCard, "LAYER & DIM");
            if (layerTitle != null)
                layerTitle.Text = "LAYER & DIM";

            var expander = FindFirst<Expander>(layerCard);
            if (expander != null)
                expander.IsExpanded = true;

            var oldIndex = stack.Children.IndexOf(layerCard);
            var scopeIndex = stack.Children.IndexOf(scopeCard);
            if (oldIndex < 0 || scopeIndex < 0) return;

            if (oldIndex == scopeIndex + 1) return;

            stack.Children.Remove(layerCard);
            scopeIndex = stack.Children.IndexOf(scopeCard);
            stack.Children.Insert(Math.Min(scopeIndex + 1, stack.Children.Count), layerCard);
        }

        private static void HideRedundantSectionLabels(VxtPaletteView view)
        {
            foreach (var label in RedundantSectionLabels)
            {
                foreach (var text in FindTexts(view, label))
                    text.Visibility = Visibility.Collapsed;
            }
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
            => FindText(root, value) != null;

        private static TextBlock FindText(DependencyObject root, string value)
        {
            foreach (var text in FindTexts(root, value)) return text;
            return null;
        }

        private static IEnumerable<TextBlock> FindTexts(DependencyObject root, string value)
        {
            foreach (var node in Walk(root))
            {
                var text = node as TextBlock;
                if (text != null && string.Equals(text.Text, value, StringComparison.Ordinal))
                    yield return text;
            }
        }

        private static T FindFirst<T>(DependencyObject root) where T : DependencyObject
        {
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
                if (border?.Child != null) queue.Enqueue(border.Child);

                var decorator = current as Decorator;
                if (decorator?.Child != null) queue.Enqueue(decorator.Child);

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
