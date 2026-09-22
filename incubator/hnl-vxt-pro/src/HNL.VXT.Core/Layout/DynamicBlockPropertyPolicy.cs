using System;
using System.Globalization;
using System.Text;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Pure policy used by the AutoCAD bridge to avoid writing member length into
    /// dynamic-block Array/Count/Spacing/Rotation properties. The legacy Lisp wrote
    /// the target length into every writable Double that was not blacklisted; real
    /// production blocks can expose localized/custom array properties that escape
    /// that blacklist. V7 therefore chooses one length-like property instead.
    /// </summary>
    public static class DynamicBlockPropertyPolicy
    {
        public static bool IsRisky(string propertyName, string description = null)
        {
            var text = Normalize((propertyName ?? string.Empty) + " " + (description ?? string.Empty));
            if (text.Length == 0) return false;

            return ContainsAny(text,
                "ANG", "ANGLE", "ROT", "ROTATION", "XOAY",
                "POS", "POSITION", "ORI", "ORIGIN",
                "ARRAY", "ASSOCARRAY", "COUNT", "SO LUONG", "SOLUONG",
                "ROW", "ROWS", "COLUMN", "COLUMNS", "COLS",
                "SPACING", "GAP", "PITCH", "STEP",
                "K C", "KHOANG CACH", "KHOANG",
                "LOOKUP", "VISIBILITY", "FLIP",
                "TY", "CHINH", "PHU", "DAY");
        }

        public static int ScoreLengthProperty(
            string propertyName,
            string description,
            double currentValue,
            double referenceLength)
        {
            if (IsRisky(propertyName, description)) return int.MinValue;

            var name = Normalize(propertyName);
            var desc = Normalize(description);
            var text = name + " " + desc;
            var score = 0;

            if (ContainsAny(text, "LENGTH", "CHIEU DAI", "CHIEUDAI")) score += 120;
            if (ContainsAny(text, "STRETCH", "LINEAR")) score += 90;
            if (ContainsAny(text, "DISTANCE", "DIST ", "DIST1", "DIST2")) score += 70;
            if (ContainsAny(text, "DAI", "LEN")) score += 45;

            // A genuine length parameter usually has a current value close to the
            // visible member length of the selected sample. This helps distinguish
            // Distance/Distance1 from array pitch or small offsets.
            if (referenceLength > 1e-6 && currentValue > 1e-6)
            {
                var ratio = currentValue / referenceLength;
                if (ratio < 1.0) ratio = 1.0 / ratio;
                if (ratio <= 1.05) score += 80;
                else if (ratio <= 1.25) score += 55;
                else if (ratio <= 2.0) score += 25;
                else if (ratio >= 8.0) score -= 35;
            }

            return score;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                var x = c == 'Đ' || c == 'đ' ? 'D' : c;
                sb.Append(char.ToUpperInvariant(x));
            }
            return sb.ToString().Replace('_', ' ').Replace('-', ' ');
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            foreach (var token in tokens)
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
