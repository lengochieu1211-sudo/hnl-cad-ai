using System;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Field QA for the currently selected real ceiling. It forces the same live Preview renderer
    /// used by the palette, then compares final-plan counts against the transient objects that
    /// AutoCAD actually accepted. This is intentionally separate from HNLVXTQA because it depends
    /// on the user's current DWG/boundary/settings instead of a deterministic synthetic fixture.
    /// </summary>
    internal static class VxtPreviewRuntimeQaService
    {
        private const string Stage = "RuntimePreviewFieldQA";

        public static string Run()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return "FAIL Preview QA: Không có bản vẽ AutoCAD đang hoạt động.";

            var session = VxtSession.Current;
            if (!session.HasBoundary)
            {
                var missing = "FAIL Preview QA: Chưa chọn biên trần thực tế.";
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + missing);
                WriteLog("FAIL", missing);
                return missing;
            }

            try
            {
                var preview = VxtTransientPreview.Instance;
                preview.Refresh();

                var metrics = preview.LastPlanMetrics;
                var decision = preview.LastRenderDecision;
                if (metrics == null || decision == null)
                    throw new InvalidOperationException(
                        "Preview không tạo được snapshot runtime. Kiểm tra cảnh báo Preview ngay trước dòng này.");

                if (preview.LastActualDrawableCount != preview.LastExpectedDrawableCount)
                    throw new InvalidOperationException(
                        "Số transient thực tế " + preview.LastActualDrawableCount +
                        " khác số dự kiến " + preview.LastExpectedDrawableCount + ".");

                var expectedRenderedDimensions = decision.RenderDimensions ? metrics.DimensionCount : 0;
                if (preview.LastRenderedDimensionCount != expectedRenderedDimensions)
                    throw new InvalidOperationException(
                        "Plan có " + metrics.DimensionCount + " Dim, chế độ render yêu cầu " +
                        expectedRenderedDimensions + " Dim nhưng AutoCAD chỉ giữ " +
                        preview.LastRenderedDimensionCount + " RotatedDimension transient.");

                var mode = decision.IsReduced
                    ? (decision.RenderDimensions ? "REDUCED-Dim-KEPT" : "REDUCED-STRUCTURAL-ONLY")
                    : "FULL";

                var summary =
                    "PASS Preview QA: plan XC=" + metrics.MainCount +
                    " (" + metrics.MainLengthM.ToString("0.00", CultureInfo.InvariantCulture) + "m)" +
                    ", XP=" + metrics.FurringCount +
                    " (" + metrics.FurringLengthM.ToString("0.00", CultureInfo.InvariantCulture) + "m)" +
                    ", Ty=" + metrics.HangerCount +
                    ", Dim=" + metrics.DimensionCount +
                    " | render Dim=" + preview.LastRenderedDimensionCount +
                    "/" + expectedRenderedDimensions +
                    " | transient=" + preview.LastActualDrawableCount +
                    "/" + preview.LastExpectedDrawableCount +
                    " | mode=" + mode + ".";

                WriteLog("PASS", summary);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                return summary;
            }
            catch (System.Exception ex)
            {
                var summary = "FAIL Preview QA: " + ex.Message;
                WriteLog("FAIL", summary);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                return summary;
            }
        }

        private static void WriteLog(string result, string summary)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool", "VXT Pro");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "runtime-preview-qa.jsonl");
                var sb = new StringBuilder(768);
                sb.Append("{\"timestamp\":\"").Append(Escape(DateTimeOffset.Now.ToString("o"))).Append("\",");
                sb.Append("\"result\":\"").Append(Escape(result)).Append("\",");
                sb.Append("\"stage\":\"").Append(Stage).Append("\",");
                sb.Append("\"acadver\":\"").Append(Escape(SafeSystemVariable("ACADVER"))).Append("\",");
                sb.Append("\"summary\":\"").Append(Escape(summary)).Append("\"}\n");
                File.AppendAllText(file, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private static string SafeSystemVariable(string name)
        {
            try { return Convert.ToString(Application.GetSystemVariable(name), CultureInfo.InvariantCulture); }
            catch { return "N/A"; }
        }

        private static string Escape(string value)
            => (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
    }
}
