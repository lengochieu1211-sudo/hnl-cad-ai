using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Final visual-identity pass for user-facing controls.
    /// Presentation only: does not read or modify VxtSettings or CAD geometry.
    /// </summary>
    public static class VxtPaletteVisualIdentity
    {
        private sealed class LayerDimIdentity
        {
            public string Title;
            public string Badge;
            public string ColorHex;
            public bool DarkBadgeText;
        }

        private static readonly LayerDimIdentity[] LayerDimIdentities =
        {
            new LayerDimIdentity { Title = "Xương chính", Badge = "XC",  ColorHex = "#22D3EE", DarkBadgeText = true },
            new LayerDimIdentity { Title = "Xương phụ",   Badge = "XP",  ColorHex = "#FACC15", DarkBadgeText = true },
            new LayerDimIdentity { Title = "Ty treo",     Badge = "TY",  ColorHex = "#22C55E", DarkBadgeText = true },
            new LayerDimIdentity { Title = "DIM",         Badge = "DIM", ColorHex = "#D946EF", DarkBadgeText = false }
        };

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            view.Loaded += (sender, args) =>
            {
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyNow(view)),
                    DispatcherPriority.ApplicationIdle);
            };
        }

        private static void ApplyNow(VxtPaletteView view)
        {
            NormalizePrimaryAction(view);
            DistinguishLayerDimExpanders(view);
        }

        private static void NormalizePrimaryAction(VxtPaletteView view)
        {
            foreach (var node in Walk(view))
            {
                var button = node as Button;
                if (button == null) continue;

                var content = button.Content as string;
                if (string.IsNullOrWhiteSpace(content)) continue;

                if (content.IndexOf("TẠO KHUNG XƯƠNG TRẦN", StringComparison.OrdinalIgnoreCase) >= 0)
                    button.Content = "✓ Tạo khung xương trần";
            }
        }

        private static void DistinguishLayerDimExpanders(VxtPaletteView view)
        {
            var root = FindLayerDimRoot(view);
            if (root == null) return;

            var body = root.Content as StackPanel;
            if (body == null) return;

            foreach (UIElement child in body.Children)
            {
                var expander = child as Expander;
                if (expander == null) continue;

                var title = HeaderText(expander.Header);
                var identity = FindIdentity(title);
                if (identity == null) continue;

                var header = CreateIdentityHeader(view, identity);
                expander.Header = header;

                RoutedEventHandler refresh = (sender, args) => UpdateHeaderState(view, header, expander.IsExpanded);
                expander.Expanded -= refresh;
                expander.Collapsed -= refresh;
                expander.Expanded += refresh;
                expander.Collapsed += refresh;

                UpdateHeaderState(view, header, expander.IsExpanded);
            }
        }

        private static Expander FindLayerDimRoot(DependencyObject root)
        {
            foreach (var node in Walk(root))
            {
                var expander = node as Expander;
                if (expander == null) continue;

                var title = HeaderText(expander.Header);
                if (string.Equals(title, "Cài đặt Layer & DIM", StringComparison.Ordinal) ||
                    string.Equals(title, "CÀI ĐẶT LAYER & DIM", StringComparison.Ordinal))
                    return expander;
            }
            return null;
        }

        private static LayerDimIdentity FindIdentity(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            foreach (var item in LayerDimIdentities)
                if (string.Equals(item.Title, title, StringComparison.Ordinal)) return item;
            return null;
        }

        private static Border CreateIdentityHeader(VxtPaletteView view, LayerDimIdentity identity)
        {
            var badgeBrush = Brush(identity.ColorHex);
            var primary = view.TryFindResource("PrimaryText") as Brush ?? Brushes.White;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var badge = new Border
            {
                Width = identity.Badge == "DIM" ? 34.0 : 28.0,
                Height = 20.0,
                CornerRadius = new CornerRadius(4),
                Background = badgeBrush,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = identity.Badge,
                FontSize = 9.0,
                FontWeight = FontWeights.Bold,
                Foreground = identity.DarkBadgeText ? Brushes.Black : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var title = new TextBlock
            {
                Text = identity.Title,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = primary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 1);

            grid.Children.Add(badge);
            grid.Children.Add(title);

            return new Border
            {
                Tag = badgeBrush,
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 8, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Child = grid
            };
        }

        private static void UpdateHeaderState(VxtPaletteView view, Border header, bool expanded)
        {
            if (header == null) return;

            var identityBrush = header.Tag as Brush ?? Brushes.Gray;
            var accentSoft = view.TryFindResource("AccentSoft") as Brush ?? Brushes.Transparent;
            var inputBackground = view.TryFindResource("InputBackground") as Brush ?? Brushes.Transparent;
            var inputBorder = view.TryFindResource("InputBorder") as Brush ?? Brushes.Gray;

            header.Background = expanded ? accentSoft : inputBackground;
            header.BorderBrush = expanded ? identityBrush : inputBorder;
            header.BorderThickness = expanded ? new Thickness(1.25) : new Thickness(1);
        }

        private static string HeaderText(object header)
        {
            var direct = header as string;
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            var text = header as TextBlock;
            if (text != null) return text.Text;

            var root = header as DependencyObject;
            if (root == null) return null;

            foreach (var node in Walk(root))
            {
                var block = node as TextBlock;
                if (block != null && !string.IsNullOrWhiteSpace(block.Text))
                {
                    var value = block.Text.Trim();
                    if (value == "XC" || value == "XP" || value == "TY") continue;
                    return value;
                }
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
                    foreach (UIElement child in panel.Children)
                        if (child != null) queue.Enqueue(child);

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
            }
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
