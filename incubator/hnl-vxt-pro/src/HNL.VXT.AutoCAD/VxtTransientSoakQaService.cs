using System;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Real-host stress check for the modeless Preview transient lifecycle. The test repeatedly
    /// adds and erases lightweight drawables through the exact VxtTransientPreview code path and
    /// verifies that no tracked drawable survives a successful erase cycle. It never commits DWG data.
    /// </summary>
    internal static class VxtTransientSoakQaService
    {
        private const string Stage = "RuntimeTransientSoakQA";
        private const int Cycles = 120;

        public static string Run()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return "FAIL Transient Soak QA: Không có bản vẽ AutoCAD đang hoạt động.";

            var preview = VxtTransientPreview.Instance;
            var restorePreview = VxtSession.Current.HasBoundary;

            try
            {
                var completed = preview.RunLifecycleProbe(doc.Database, Cycles);
                if (completed != Cycles)
                    throw new InvalidOperationException(
                        "Soak QA chỉ chạy " + completed + "/" + Cycles + " vòng.");
                if (preview.TrackedDrawableCount != 0)
                    throw new InvalidOperationException(
                        "Còn " + preview.TrackedDrawableCount + " transient được theo dõi sau Soak QA.");

                if (restorePreview) preview.Refresh();

                var summary = "PASS Transient Soak QA: " + Cycles +
                              " vòng Add/Erase x 3 drawable | tracked=0 | Preview chạy trong command context.";
                WriteLog("PASS", summary);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                return summary;
            }
            catch (System.Exception ex)
            {
                try { preview.Clear(); } catch { }
                if (restorePreview && preview.TrackedDrawableCount == 0)
                {
                    try { preview.Refresh(); } catch { }
                }

                var summary = "FAIL Transient Soak QA: " + ex.Message;
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
                var file = Path.Combine(folder, "runtime-transient-soak-qa.jsonl");
                var sb = new StringBuilder(512);
                sb.Append("{\"timestamp\":\"").Append(Escape(DateTimeOffset.Now.ToString("o"))).Append("\",");
                sb.Append("\"result\":\"").Append(Escape(result)).Append("\",");
                sb.Append("\"stage\":\"").Append(Stage).Append("\",");
                sb.Append("\"cycles\":").Append(Cycles.ToString(CultureInfo.InvariantCulture)).Append(",");
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
