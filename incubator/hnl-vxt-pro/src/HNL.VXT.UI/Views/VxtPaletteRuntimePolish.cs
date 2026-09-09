using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using HNL.VXT.UI.Controls;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Final AutoCAD-hosted palette polish pass.
    /// Runs after dynamic panels have been created so placement and column alignment do not
    /// depend on WPF visual-tree materialization order inside PaletteSet.
    /// </summary>
    public static class VxtPaletteRuntimePolish
    {
        private const double CommonLabelWidth = 142.0;
        private const double CommonPickButtonWidth = 68.0;
        private const string FixedLayerDimTag = "HNL_VXT_FIXED_LAYER_DIM";

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
            EnsureFixedLayerAndDimPanel(view);
            HideRedundantSectionLabels(view);
            AlignInputColumns(view);
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

            // Keep only product title + version. Branding is already carried by the HNL logo.
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

        private static void EnsureFixedLayerAndDimPanel(VxtPaletteView view)
        {
            var stack = GetContentStack(view);
            if (stack == null) return;

            FrameworkElement scopeCard = null;
            Border fixedCard = null;
            var legacyCards = new List<UIElement>();

            foreach (UIElement child in stack.Children)
            {
                var element = child as FrameworkElement;
                if (element == null) continue;

                if (scopeCard == null && ContainsText(element, "PHẠM VI BỐ TRÍ"))
                    scopeCard = element;

                var border = element as Border;
                if (border != null && string.Equals(border.Tag as string, FixedLayerDimTag, StringComparison.Ordinal))
                {
                    fixedCard = border;
                    continue;
                }

                if (ContainsText(element, "LAYER & KIỂU NÉT") || ContainsText(element, "LAYER & DIM"))
                    legacyCards.Add(child);
            }

            // The old Enhancer-created panel lived near Preview and depended on a later move.
            // Remove that copy and own one deterministic panel directly after PHẠM VI.
            foreach (var legacy in legacyCards)
                stack.Children.Remove(legacy);

            if (scopeCard == null) return;

            if (fixedCard == null)
            {
                fixedCard = CreateFixedLayerAndDimCard(view);
                var scopeIndex = stack.Children.IndexOf(scopeCard);
                stack.Children.Insert(Math.Min(scopeIndex + 1, stack.Children.Count), fixedCard);
            }
            else
            {
                var oldIndex = stack.Children.IndexOf(fixedCard);
                var scopeIndex = stack.Children.IndexOf(scopeCard);
                if (oldIndex >= 0 && scopeIndex >= 0 && oldIndex != scopeIndex + 1)
                {
                    stack.Children.Remove(fixedCard);
                    scopeIndex = stack.Children.IndexOf(scopeCard);
                    stack.Children.Insert(Math.Min(scopeIndex + 1, stack.Children.Count), fixedCard);
                }
            }

            var topExpander = FindFirst<Expander>(fixedCard);
            if (topExpander != null) topExpander.IsExpanded = true;
        }

        private static Border CreateFixedLayerAndDimCard(VxtPaletteView view)
        {
            var card = new Border
            {
                Tag = FixedLayerDimTag,
                Style = view.TryFindResource("SectionCard") as Style,
                BorderBrush = view.TryFindResource("AccentBorder") as Brush
            };

            var expander = new Expander { IsExpanded = true };
            expander.Header = CreateLayerDimHeader(view);

            var body = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            body.Children.Add(CreateResourceExpander(
                view, "Xương chính", false,
                nameof(VxtPaletteViewModel.MainLayer),
                nameof(VxtPaletteViewModel.MainColorIndex),
                nameof(VxtPaletteViewModel.MainLinetype),
                nameof(VxtPaletteViewModel.MainLineweight)));
            body.Children.Add(CreateResourceExpander(
                view, "Xương phụ", false,
                nameof(VxtPaletteViewModel.FurringLayer),
                nameof(VxtPaletteViewModel.FurringColorIndex),
                nameof(VxtPaletteViewModel.FurringLinetype),
                nameof(VxtPaletteViewModel.FurringLineweight)));
            body.Children.Add(CreateResourceExpander(
                view, "Ty treo", false,
                nameof(VxtPaletteViewModel.HangerLayer),
                nameof(VxtPaletteViewModel.HangerColorIndex),
                nameof(VxtPaletteViewModel.HangerLinetype),
                nameof(VxtPaletteViewModel.HangerLineweight)));
            body.Children.Add(CreateResourceExpander(
                view, "DIM", true,
                nameof(VxtPaletteViewModel.DimensionLayer),
                nameof(VxtPaletteViewModel.DimensionColorIndex),
                nameof(VxtPaletteViewModel.DimensionLinetype),
                nameof(VxtPaletteViewModel.DimensionLineweight)));

            expander.Content = body;
            card.Child = expander;
            return card;
        }

        private static Grid CreateLayerDimHeader(VxtPaletteView view)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var marker = new Border
            {
                Width = 4,
                Height = 18,
                CornerRadius = new CornerRadius(2),
                Background = view.TryFindResource("AccentStrong") as Brush ?? Brushes.DeepSkyBlue,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var title = new TextBlock
            {
                Text = "CÀI ĐẶT LAYER & DIM",
                Style = view.TryFindResource("SectionTitle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 1);
            grid.Children.Add(marker);
            grid.Children.Add(title);
            return grid;
        }

        private static Expander CreateResourceExpander(
            VxtPaletteView view,
            string title,
            bool expanded,
            string layerPath,
            string colorPath,
            string linetypePath,
            string lineweightPath)
        {
            var expander = new Expander
            {
                IsExpanded = expanded,
                Margin = new Thickness(0, 0, 0, 3),
                Header = new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 10.5
                }
            };

            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 3) };
            panel.Children.Add(CreateTextRow(view, "Layer", layerPath));
            panel.Children.Add(CreateColorRow(view, "Màu ACI", colorPath));
            panel.Children.Add(CreateEditableComboRow(view, "Linetype", linetypePath, view.ViewModel.LinetypeOptions));
            panel.Children.Add(CreateEditableComboRow(view, "Lineweight", lineweightPath, view.ViewModel.LineweightOptions));

            if (title == "DIM")
            {
                panel.Children.Add(CreateDimStyleRow(view));
                panel.Children.Add(new TextBlock
                {
                    Text = "Layer/DimStyle này được dùng cho cả Preview DIM và DIM tạo thật.",
                    Style = view.TryFindResource("HintText") as Style,
                    Margin = new Thickness(CommonLabelWidth, 2, 0, 0)
                });
            }

            expander.Content = panel;
            return expander;
        }

        private static Grid CreateBaseRow(VxtPaletteView view, string label)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CommonLabelWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock
            {
                Text = label,
                Style = view.TryFindResource("FieldLabel") as Style,
                VerticalAlignment = VerticalAlignment.Center
            });
            return row;
        }

        private static Grid CreateTextRow(VxtPaletteView view, string label, string bindingPath)
        {
            var row = CreateBaseRow(view, label);
            var input = new TextBox { Height = 27.0, HorizontalAlignment = HorizontalAlignment.Stretch };
            input.SetBinding(TextBox.TextProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });
            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            return row;
        }

        private static Grid CreateColorRow(VxtPaletteView view, string label, string bindingPath)
        {
            var row = CreateBaseRow(view, label);
            var input = new HnlNumericBox
            {
                Height = 27.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Minimum = 0,
                Maximum = 256,
                Step = 1,
                Unit = "ACI"
            };
            input.SetBinding(HnlNumericBox.ValueProperty, new Binding(bindingPath) { Mode = BindingMode.TwoWay });
            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            return row;
        }

        private static Grid CreateEditableComboRow(VxtPaletteView view, string label, string bindingPath, string[] items)
        {
            var row = CreateBaseRow(view, label);
            var combo = new ComboBox
            {
                Height = 27.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEditable = true,
                ItemsSource = items
            };
            combo.SetBinding(ComboBox.TextProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            return row;
        }

        private static Grid CreateDimStyleRow(VxtPaletteView view)
        {
            var row = CreateBaseRow(view, "DimStyle");
            var combo = new ComboBox
            {
                Height = 27.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = view.ViewModel.DimStyleOptions
            };
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding(nameof(VxtPaletteViewModel.SelectedDimensionStyle))
            {
                Mode = BindingMode.TwoWay
            });
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            return row;
        }

        private static void AlignInputColumns(VxtPaletteView view)
        {
            var stack = GetContentStack(view);
            if (stack == null) return;

            foreach (var node in Walk(stack))
            {
                var grid = node as Grid;
                if (grid != null)
                    NormalizeGrid(grid);

                var textBox = node as TextBox;
                if (textBox != null)
                {
                    textBox.Height = 27.0;
                    textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                }

                var combo = node as ComboBox;
                if (combo != null)
                {
                    combo.Height = 27.0;
                    combo.HorizontalAlignment = HorizontalAlignment.Stretch;
                }

                var numeric = node as HnlNumericBox;
                if (numeric != null)
                {
                    numeric.Height = 27.0;
                    numeric.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
            }
        }

        private static void NormalizeGrid(Grid grid)
        {
            var columns = grid.ColumnDefinitions;
            if (columns == null || columns.Count < 2) return;

            // Compact Min/Max rows produced by VxtPaletteCompactTuner:
            // label + token + input + gap + token + input.
            if (columns.Count == 6 &&
                columns[0].Width.IsAbsolute && columns[1].Width.IsAbsolute &&
                columns[1].Width.Value >= 20.0 && columns[1].Width.Value <= 35.0)
            {
                columns[1].Width = new GridLength(27.0);
                columns[0].Width = new GridLength(CommonLabelWidth - 27.0);
                return;
            }

            // DIM target rows: checkbox + label + selector + pick button.
            if (columns.Count == 4 &&
                columns[0].Width.IsAbsolute && columns[1].Width.IsAbsolute &&
                columns[0].Width.Value >= 20.0 && columns[0].Width.Value <= 45.0 &&
                columns[1].Width.Value >= 80.0 && columns[1].Width.Value <= 130.0)
            {
                columns[0].Width = new GridLength(24.0);
                columns[1].Width = new GridLength(CommonLabelWidth - 24.0);
                columns[2].Width = new GridLength(1, GridUnitType.Star);
                if (columns[3].Width.IsAbsolute)
                    columns[3].Width = new GridLength(CommonPickButtonWidth);
                return;
            }

            var first = columns[0].Width;
            if (!first.IsAbsolute || first.Value < 105.0 || first.Value > 200.0) return;

            columns[0].Width = new GridLength(CommonLabelWidth);

            if (columns.Count == 2)
            {
                columns[1].Width = new GridLength(1, GridUnitType.Star);
                return;
            }

            if (columns.Count == 3)
            {
                columns[1].Width = new GridLength(1, GridUnitType.Star);
                var third = columns[2].Width;
                if (third.IsAbsolute && third.Value >= 55.0 && third.Value <= 110.0)
                    columns[2].Width = new GridLength(CommonPickButtonWidth);
            }
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
