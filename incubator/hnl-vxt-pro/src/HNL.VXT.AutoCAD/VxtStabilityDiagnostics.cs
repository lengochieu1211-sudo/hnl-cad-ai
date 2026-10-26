using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Read-only field measurements for intermittent CAD stalls; never forces GC,
    /// disposes graphics, changes document state or invokes Preview.
    /// </summary>
    internal static class VxtStabilityDiagnostics
    {
        internal static void Capture()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    process.Refresh();
                    var privateMb = process.PrivateMemorySize64 / (1024L * 1024L);
                    var workingMb = process.WorkingSet64 / (1024L * 1024L);
                    var handles = process.HandleCount;
                    var concealed = VxtTransientPreview.Instance;
                    var drawing = Path.GetFileName(doc.Name ?? string.Empty);
                    var dll = typeof(HnlVxtCommands).Assembly.Location;
                    var timestamp = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);

                    var line = new StringBuilder();
                    line.Append("{\"time\":\"").Append(Escape(timestamp)).Append("\"");
                    line.Append(",\"dwg\":\"").Append(Escape(drawing)).Append("\"");
                    line.Append(",\"loadedDll\":\"").Append(Escape(dll)).Append("\"");
                    line.Append(",\"privateMb\":").Append(privateMb);
                    line.Append(",\"workingMb\":").Append(workingMb);
                    line.Append(",\"handles\":").Append(handles);
                    line.Append(",\"concealedActive\":").Append(concealed.TrackedDrawableCount);
                    line.Append(",\"concealedRetired\":").Append(concealed.RetiredDrawableCount);
                    line.Append(",\"concealedQuarantine\":").Append(concealed.TransitionQuarantineCount);
                    line.Append(",\"concealedLastPreviewMs\":").Append(concealed.LastPreviewDurationMs);
                    line.Append(",\"concealedLastExpected\":").Append(concealed.LastExpectedDrawableCount);
                    line.Append(",\"concealedLastActual\":").Append(concealed.LastActualDrawableCount);
                    line.Append(",\"layinActive\":").Append(LayInCadRuntime.ActiveDrawableCount);
                    line.Append(",\"layinRetired\":").Append(LayInCadRuntime.RetiredDrawableCount);
                    line.Append(",\"layinQuarantine\":").Append(LayInCadRuntime.TransitionQuarantineCount);
                    line.Append(",\"layinLastPreviewMs\":").Append(LayInCadRuntime.LastPreviewDurationMs);
                    line.Append("}");

                    doc.Editor.WriteMessage(
                        "\nHNL Tool - Stability: Private " + privateMb + " MB | Working " +
                        workingMb + " MB | Handles " + handles +
                        " | Preview ms (concealed/lay-in) " +
                        concealed.LastPreviewDurationMs + "/" + LayInCadRuntime.LastPreviewDurationMs +
                        " | Quarantine (concealed/lay-in) " +
                        concealed.TransitionQuarantineCount + "/" +
                        LayInCadRuntime.TransitionQuarantineCount);
                    doc.Editor.WriteMessage("\nHNL Tool - Loaded DLL: " + dll);

                    try
                    {
                        var folder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "HNL Tool", "Ceiling Framing Pro");
                        Directory.CreateDirectory(folder);
                        var path = Path.Combine(folder, "stability-snapshots.jsonl");
                        File.AppendAllText(path, line.ToString() + Environment.NewLine, new UTF8Encoding(false));
                        doc.Editor.WriteMessage("\nHNL Tool - Stability log: " + path);
                    }
                    catch (Exception ex)
                    {
                        doc.Editor.WriteMessage("\nHNL Tool - Stability log not saved: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage("\nHNL Tool - Stability snapshot error: " + ex.Message);
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
