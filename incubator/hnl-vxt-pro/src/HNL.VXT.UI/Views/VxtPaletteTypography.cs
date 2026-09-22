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
    /// - Sentence/title case for section names; keep technical acronyms such as Dim/HNL/ACI/Cad.
    /// - One deterministic type scale for XAML and dynamically-created panels.
    /// - Section titles use the dynamic HNL accent so theme/accent changes stay synchronized.
    /// - Preserve the existing user TextScale preference (90/100/110/120%).
    ///
    /// This class changes presentation only. It never reads or mutates VxtSettings.
    /// </summary>
    public static class VxtPaletteTypography
    {
        private const double ProductTitleSize = 13.0;
        private const double SectionTitleSize = 12.0;
        private const double BodySize = 12.0;
        private const double PrimaryActionSize = SectionTitleSize;
        private const double HintSize = 11.0;
        private const double VersionSize = 10.5;

        private static readonly Dictionary<string, string> SectionText =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "CẤU HÌNH", "Cấu hình" },
                { "PHẠM VI BỐ TRÍ", "Phạm vi bố trí" },
                { "XƯƠNG CHÍNH", "Xương chính" },
                { "XƯƠNG PHỤ", "Xương phụ" },
                { "TY TREO", "Ty treo" },
                { "NÉ THIẾT BỊ", "Né thiết bị" },
                { "NÉ THIẾT BỊ MEP", "Né thiết bị MEP" },
                { "KÍCH THƯỚC Dim", "Kích thước Dim" },
                { "CÀI ĐẶT LAYER & Dim", "Cài đặt Layer & Dim" },
                { "LAYER & Dim", "Layer & Dim" },
                { "LAYER & HIỂN THỊ", "Layer & hiển thị" },
                { "LAYER & KIỂU NÉT", "Layer & kiểu nét" },
                { "XEM TRƯỚC TRÊN BẢN VẼ", "Xem trước trên bản vẽ" },
                { "PHÂN TÍCH & KIỂM TRA LỖI", "Phân tích & kiểm tra lỗi" },
                { "KIỂM TRA BỐ TRÍ", "Kiểm tra bố trí" },
                { "GIAO DIỆN", "Giao diện" },
                { "CHẾ ĐỘ TỐI ƯU", "Chế độ tối ưu" },
                { "CHẨN ĐOÁN", "Chẩn đoán" }
            };

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            // Normalize base metrics before Loaded. VxtPaletteEnhancer captures these metrics
            // and therefore keeps the user's live TextScale feature deterministic. Do not
            // rename section text yet because older runtime layout passes still locate cards by
            // their original captions during Loaded.
            ApplyNow(view, 1.0, normalizeSectionText: false);

            // Run after all layout/runtime polish passes. At this point it is safe to apply the
            // final user-facing captions and re-assert the common typography scale.
            view.Loaded += (sender, args) =>
            {
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyNow(view, LoadTextScale(), normalizeSectionText: true)),
                    DispatcherPriority.ApplicationIdle);
            };
        }

        private static void ApplyNow(VxtPaletteView view, double scale, bool normalizeSectionText)
        {
            // Match the AutoCAD Properties palette baseline: Segoe UI, 12 DIP at 100% text scale.
            // Keep the explicit HNL product-title hierarchy, but make all property labels/values
            // share one readable technical UI size.
            view.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            view.FontSize = BodySize * scale;

            var sectionStyle = view.TryFindResource("SectionTitle") as Style;
            var fieldStyle = view.TryFindResource("FieldLabel") as Style;
            var hintStyle = view.TryFindResource("HintText") as Style;
            var primaryButtonStyle = view.TryFindResource("PrimaryButton") as Style;

            foreach (var node in Walk(view))
            {
                var text = node as TextBlock;
                if (text != null)
                {
                    if (normalizeSectionText)
                        NormalizeSectionText(text);

                    if (sectionStyle != null && ReferenceEquals(text.Style, sectionStyle))
                    {
                        text.FontSize = SectionTitleSize * scale;
                        text.FontWeight = FontWeights.SemiBold;
                        // DynamicResource semantics: section titles follow theme/accent changes.
                        text.SetResourceReference(TextBlock.ForegroundProperty, "AccentStrong");
                    }
                    else if (fieldStyle != null && ReferenceEquals(text.Style, fieldStyle))
                    {
                        text.FontSize = BodySize * scale;
                    }
                    else if (hintStyle != null && ReferenceEquals(text.Style, hintStyle))
                    {
                        text.FontSize = HintSize * scale;
                    }
                    else if (string.Equals(text.Text, "Vẽ xương trần", StringComparison.OrdinalIgnoreCase))
                    {
                        // Product/brand title uses natural sentence case; technical acronyms remain uppercase.
                        text.FontSize = ProductTitleSize * scale;
                        text.FontWeight = FontWeights.SemiBold;
                    }
                    else if (!string.IsNullOrWhiteSpace(text.Text) &&
                             text.Text.StartsWith("VXT Pro", StringComparison.OrdinalIgnoreCase))
                    {
                        text.FontSize = VersionSize * scale;
                    }
                    else if (!string.IsNullOrWhiteSpace(text.Text) &&
                             !string.Equals(text.Text, "HNL Tool", StringComparison.Ordinal) &&
                             !string.Equals(text.Text, "Live", StringComparison.OrdinalIgnoreCase))
                    {
                        // Dynamic panels created in code-behind/runtime polish must not fall back
                        // to smaller ad-hoc font sizes. They follow the same Properties body size.
                        text.FontSize = BodySize * scale;
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
