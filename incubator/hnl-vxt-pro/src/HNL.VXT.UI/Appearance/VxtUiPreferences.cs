using System;
using Microsoft.Win32;

namespace HNL.VXT.UI.Appearance
{
    internal sealed class VxtUiPreferences
    {
        private const string RegistryPath = @"Software\HNL Tool\VXT Pro\UI";

        public string ThemeMode { get; set; } = "Theo AutoCAD";
        public string AccentColor { get; set; } = "Xanh HNL";
        public string TextColor { get; set; } = "Theo giao diện";
        public string TextScale { get; set; } = "100%";
        public string RowSpacing { get; set; } = "Gọn";
        public string ColumnSpacing { get; set; } = "Tiêu chuẩn";
        public string CardSpacing { get; set; } = "Gọn";

        public static readonly string[] ThemeModes = { "Theo AutoCAD", "Tối", "Sáng" };
        public static readonly string[] AccentColors = { "Xanh HNL", "Cyan", "Xanh lá", "Tím", "Cam" };
        public static readonly string[] TextColors = { "Theo giao diện", "Sáng", "Tối", "Xanh HNL" };
        public static readonly string[] TextScales = { "90%", "100%", "110%", "120%" };
        public static readonly string[] SpacingModes = { "Gọn", "Tiêu chuẩn", "Thoáng" };

        public static VxtUiPreferences Load()
        {
            var value = new VxtUiPreferences();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null) return value;
                    value.ThemeMode = ReadChoice(key, nameof(ThemeMode), value.ThemeMode, ThemeModes);
                    value.AccentColor = ReadChoice(key, nameof(AccentColor), value.AccentColor, AccentColors);
                    value.TextColor = ReadChoice(key, nameof(TextColor), value.TextColor, TextColors);
                    value.TextScale = ReadChoice(key, nameof(TextScale), value.TextScale, TextScales);
                    value.RowSpacing = ReadChoice(key, nameof(RowSpacing), value.RowSpacing, SpacingModes);
                    value.ColumnSpacing = ReadChoice(key, nameof(ColumnSpacing), value.ColumnSpacing, SpacingModes);
                    value.CardSpacing = ReadChoice(key, nameof(CardSpacing), value.CardSpacing, SpacingModes);
                }
            }
            catch
            {
                // UI preferences must never block the palette.
            }
            return value;
        }

        public void Save()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return;
                    key.SetValue(nameof(ThemeMode), ThemeMode ?? string.Empty, RegistryValueKind.String);
                    key.SetValue(nameof(AccentColor), AccentColor ?? string.Empty, RegistryValueKind.String);
                    key.SetValue(nameof(TextColor), TextColor ?? string.Empty, RegistryValueKind.String);
                    key.SetValue(nameof(TextScale), TextScale ?? string.Empty, RegistryValueKind.String);
                    key.SetValue(nameof(RowSpacing), RowSpacing ?? string.Empty, RegistryValueKind.String);
                    key.SetValue(nameof(ColumnSpacing), ColumnSpacing ?? string.Empty, RegistryValueKind.String);
                    key.SetValue(nameof(CardSpacing), CardSpacing ?? string.Empty, RegistryValueKind.String);
                }
            }
            catch
            {
                // Registry write failure is non-fatal.
            }
        }

        public void Reset()
        {
            ThemeMode = "Theo AutoCAD";
            AccentColor = "Xanh HNL";
            TextColor = "Theo giao diện";
            TextScale = "100%";
            RowSpacing = "Gọn";
            ColumnSpacing = "Tiêu chuẩn";
            CardSpacing = "Gọn";
        }

        private static string ReadChoice(RegistryKey key, string name, string fallback, string[] allowed)
        {
            var raw = Convert.ToString(key.GetValue(name, fallback));
            foreach (var item in allowed)
                if (string.Equals(item, raw, StringComparison.Ordinal)) return raw;
            return fallback;
        }
    }
}
