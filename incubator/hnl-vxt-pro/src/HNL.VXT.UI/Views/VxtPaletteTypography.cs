using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HNL.VXT.UI.Appearance;
using HNL.VXT.UI.Controls;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Final user-facing typography pass for the AutoCAD palette.
    ///
    /// Rules:
    /// - Sentence/title case for section names; keep technical acronyms such as DIM/HNL/ACI/CAD.
    /// - One deterministic type scale for XAML and dynamically-created panels.
    /// - Preserve the existing user TextScale preference (90/100/110/120%).
    ///
    /// This class changes presentation only. It never reads or mutates VxtSettings.
    /// </summary>
    public static class VxtPaletteTypography
    {
        private const double ProductTitleSize = 13.0;
        private const double SectionTitleSize = 11.5;
        private const double BodySize = 10.5;
        private const double PrimaryActionSize = 11.0;
        private const double HintSize = 9.0;
        private const double VersionSize = 9.0;

        private static readonly Dictionary<string, string> SectionText =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "CẤU HÌNH", "Cấu hình" },
                { "PHẠM VI BỐ TRÍ", "Phạm vi bố trí" },
                { "XƯƠNG CHÍNH", "Xương chính" },
                { "XƯƠNG PHỤ", "Xương phụ" },
                { "TY TREO", "Ty treo" },
                { "NÉ THIẾT BỊ", "Né thiết bị" },
                { "KÍCH THƯỚC DIM", "Kích thước DIM" },
                { "CÀI ĐẶT LAYER & DIM", "Cài đặt Layer & DIM" },
                { "LAYER & DIM", "Layer & DIM" },
                { "LAYER & KIỂU NÉT", "Layer & kiểu nét" },
                { "XEM TRƯỚC TRÊN BẢN VẼ", "Xem trước trên bản vẽ" },
                { "PHÂN TÍCH & KIỂM TRA LỖI", "Phân tích & kiểm tra lỗi" },
                { "GIAO DIỆN", "Giao diện" },
                { "CHẾ ĐỘ TỐI ƯU", "Chế độ tối ưu" }
            };

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            // Normalize base metrics before Loaded. VxtPaletteEnhancer captures these metrics
            // and therefore keeps the user's live TextScale feature deterministic.
            ApplyNow(view, 1.0);

            // Other legacy/runtime polish passes also run during Loaded. Re-assert one final
            // typography scale after those passes have completed, without touching layout logic.
            view.Loaded += (sender, args) =>
            {
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyNow(view, LoadTextScale())),
                    DispatcherPriority.ApplicationIdle);
            };
        }

        private static void ApplyNow(VxtPaletteView view, double scale)
        {
            var sectionStyle = view.TryFindResource("SectionTitle") as Style;
            var fieldStyle = view.TryFindResource("FieldLabel") as Style;
            var hintStyle = view.TryFindResource("HintText") as Style;
            var primaryButtonStyle = view.TryFindResource("PrimaryButton") as Style;

            foreach (var node in Walk(view))
            {
                var text = node as TextBlock;
                if (text != null)
                {
                    NormalizeSectionText(text);

                    if (sectionStyle != null && ReferenceEquals(text.Style, sectionStyle))
                    {
                        text.FontSize = SectionTitleSize * scale;
                        text.FontWeight = FontWeights.SemiBold;
                    }
                    else if (fieldStyle != null && ReferenceEquals(text.Style, fieldStyle))
                    {
                        text.FontSize = BodySize * scale;
                    }
                    else if (hintStyle != null && ReferenceEquals(text.Style, hintStyle))
                    {
                        text.FontSize = HintSize * scale;
                    }
                    else if (string.Equals(text.Text, "VẼ XƯƠNG TRẦN", StringComparison.Ordinal))
                    {
                        // Product/brand title may remain uppercase; section names below do not.
                        text.FontSize = ProductTitleSize * scale;
                        text.FontWeight = FontWeights.SemiBold;
                    }
                    else if (!string.IsNullOrWhiteSpace(text.Text) &&
                             text.Text.StartsWith("VXT Pro", StringComparison.OrdinalIgnoreCase))
                    {
                        text.FontSize = VersionSize * scale;
                    }
                }

                var combo = node as ComboBox;
                if (combo != null)
                {
                    combo.FontSize = BodySize * scale;
                    continue;
                }

                var textBox = node as TextBox;
                if (textBox != null)
                {
                    textBox.FontSize = BodySize * scale;
                    continue;
                }

                var numeric = node as HnlNumericBox;
                if (numeric != null)
                {
                    numeric.FontSize = BodySize * scale;
                    continue;
                }

                var check = node as CheckBox;
                if (check != null)
                {
                    check.FontSize = BodySize * scale;
                    continue;
                }

                var button = node as Button;
                if (button != null)
                {
                    if (primaryButtonStyle != null && ReferenceEquals(button.Style, primaryButtonStyle))
                    {
                        button.FontSize = PrimaryActionSize * scale;
                        button.FontWeight = FontWeights.SemiBold;
                    }
                    else
                    {
                        button.FontSize = BodySize * scale;
                    }
                }
            }
        }

        private static void NormalizeSectionText(TextBlock text)
        {
            if (text == null || string.IsNullOrEmpty(text.Text)) return;
            string replacement;
            if (SectionText.TryGetValue(text.Text, out replacement))
                text.Text = replacement;
        }

        private static double LoadTextScale()
        {
            var raw = VxtUiPreferences.Load().TextScale;
            if (string.IsNullOrWhiteSpace(raw)) return 1.0;

            raw = raw.Trim().TrimEnd('%');
            double percent;
            if (!double.TryParse(raw, out percent)) return 1.0;
            return Math.Max(0.8, Math.Min(1.3, percent / 100.0));
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
            }
        }
    }
}
