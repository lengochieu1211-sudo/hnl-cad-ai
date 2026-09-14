using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.Win32;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtDiagnosticService
    {
        private const string ProductLabel = "HNL Tool - VXT Pro v7.0.0-beta.1";
        private static readonly object Sync = new object();

        public static string LastPackagePath { get; private set; }
        public static string LastAnalysis { get; private set; } = "Chưa có dữ liệu phân tích.";

        public static void AnalyzeAndReport(VxtSettings settings)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                var analysis = AnalyzeCurrentState(settings, null, "ManualAnalyze");
                LastAnalysis = analysis.Summary;
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro Diagnostic: " + analysis.Summary);
                foreach (var item in analysis.Issues)
                    doc.Editor.WriteMessage("\n  - " + item);
                if (analysis.Issues.Count == 0)
                    doc.Editor.WriteMessage("\n  Không phát hiện lỗi cấu hình/tài nguyên trước khi tạo.");
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro Diagnostic: Không phân tích được: " + ex.Message);
            }
        }

        public static string ExportInteractive(VxtSettings settings)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;

            var dialog = new SaveFileDialog
            {
                Title = "HNL Tool - Xuất gói phân tích lỗi VXT Pro",
                Filter = "HNL VXT Diagnostic ZIP (*.zip)|*.zip|All files (*.*)|*.*",
                FileName = "HNL-VXT-DIAGNOSTIC-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".zip",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                AddExtension = true,
                DefaultExt = ".zip",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog() != true) return null;

            try
            {
                var path = BuildPackage(dialog.FileName, settings, null, "ManualExport", false, null);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Đã xuất gói lỗi: " + path);
                return path;
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Không thể xuất gói lỗi: " + ex.Message);
                return null;
            }
        }

        public static string CaptureCreateFailure(VxtSettings settings, System.Exception exception, string stage)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool", "VXT Pro", "Diagnostics");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "HNL-VXT-DIAGNOSTIC-ERROR-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmssfff") + ".zip");
                var path = BuildPackage(file, settings, exception, stage, true, null);
                RecordGolden("FAIL", settings, null, exception, path, stage);
                if (doc != null)
                    doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Đã tự lưu gói phân tích lỗi: " + path);
                return path;
            }
            catch (System.Exception exportError)
            {
                if (doc != null)
                    doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Lỗi phụ khi ghi diagnostic: " + exportError.Message);
                return null;
            }
        }

        public static void RecordCreateSuccess(VxtSettings settings, int main, int furring, int hangers, int dimensions)
        {
            var counts = new Dictionary<string, int>
            {
                { "main", main }, { "furring", furring }, { "hangers", hangers }, { "dimensions", dimensions }
            };
            RecordGolden("PASS", settings, counts, null, null, "CreateCommitted");
        }

        private static string BuildPackage(
            string targetPath,
            VxtSettings settings,
            System.Exception exception,
            string stage,
            bool automatic,
            Dictionary<string, int> counts)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");

            var fullPath = Path.GetFullPath(targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            if (File.Exists(fullPath)) File.Delete(fullPath);

            var analysis = AnalyzeCurrentState(settings, exception, stage);
            LastAnalysis = analysis.Summary;

            var environmentJson = BuildEnvironmentJson(doc, stage, automatic);
            var settingsJson = BuildSettingsJson(settings);
            var geometryJson = BuildGeometryJson();
            var diagnosticJson = BuildDiagnosticJson(doc, settings, analysis, exception, stage, automatic, counts);
            var report = BuildHumanReport(doc, settings, analysis, exception, stage, automatic, fullPath);
            var stack = exception == null ? "No exception captured." : exception.ToString();
            var golden = ReadGoldenTail(30);
            var repro = BuildReproSteps(settings, analysis);

            using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddText(zip, "HNL-VXT-DIAGNOSTIC.json", diagnosticJson);
                AddText(zip, "HNL-VXT-ERROR-REPORT.txt", report);
                AddText(zip, "settings.json", settingsJson);
                AddText(zip, "geometry.json", geometryJson);
                AddText(zip, "environment.json", environmentJson);
                AddText(zip, "stacktrace.txt", stack);
                AddText(zip, "runtime-golden-last.jsonl", golden);
                AddText(zip, "repro-steps.txt", repro);
            }

            LastPackagePath = fullPath;
            return fullPath;
        }

        private static AnalysisResult AnalyzeCurrentState(VxtSettings settings, System.Exception exception, string stage)
        {
            var issues = new List<string>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            var session = VxtSession.Current;

            if (doc == null)
                issues.Add("Không có Document AutoCAD đang hoạt động.");
            if (!session.HasBoundary)
                issues.Add("Chưa chọn Polyline kín làm biên trần.");

            string validationError;
            if (settings == null)
                issues.Add("Không có VxtSettings.");
            else if (!settings.IsValid(out validationError))
                issues.Add("Cấu hình không hợp lệ: " + validationError);

            if (doc != null && settings != null)
            {
                try
                {
                    using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                        var dst = (DimStyleTable)tr.GetObject(doc.Database.DimStyleTableId, OpenMode.ForRead);

                        if (settings.UseDynamicMainBlock && settings.DrawMain && !Has(bt, settings.MainBlockName))
                            issues.Add("Thiếu Block Xương chính: " + settings.MainBlockName);
                        if (settings.UseDynamicFurringBlock && settings.DrawFurring && !Has(bt, settings.FurringBlockName))
                            issues.Add("Thiếu Block Xương phụ: " + settings.FurringBlockName);
                        if (settings.DrawHangers && !Has(bt, settings.HangerBlockName))
                            issues.Add("Thiếu Block Ty treo: " + settings.HangerBlockName);
                        if (!string.IsNullOrWhiteSpace(settings.DimensionStyle) && !Has(dst, settings.DimensionStyle))
                            issues.Add("Thiếu DimStyle: " + settings.DimensionStyle);
                        tr.Commit();
                    }
                }
                catch (System.Exception ex)
                {
                    issues.Add("Không kiểm tra được tài nguyên DWG: " + ex.Message);
                }
            }

            if (session.Regions.Count > 0 && settings != null && settings.MainDirection != MainDirectionMode.RectangleRegions)
                issues.Add("Có vùng chữ nhật runtime nhưng chế độ hướng hiện tại không phải RectangleRegions.");

            if (exception != null)
                issues.Add("Exception: " + exception.GetType().Name + " - " + exception.Message);

            var category = Classify(exception, issues, stage);
            var summary = issues.Count == 0
                ? "PASS sơ bộ - chưa phát hiện lỗi cấu hình/tài nguyên. Vẫn cần Runtime Golden trong CAD thật."
                : "Phát hiện " + issues.Count + " vấn đề. Nhóm lỗi: " + category + ".";
            return new AnalysisResult(category, summary, issues);
        }

        private static string Classify(System.Exception exception, IList<string> issues, string stage)
        {
            var text = ((exception?.Message ?? string.Empty) + " " + string.Join(" ", issues) + " " + stage).ToLowerInvariant();
            if (text.Contains("boundary") || text.Contains("biên") || text.Contains("polyline")) return "BOUNDARY";
            if (text.Contains("dimstyle") || text.Contains("dim ") || text.Contains("dimension")) return "DIMENSION";
            if (text.Contains("block") || text.Contains("dynamic")) return "BLOCK_RESOURCE";
            if (text.Contains("layer") || text.Contains("linetype") || text.Contains("mline")) return "CAD_RESOURCE";
            if (text.Contains("avoid") || text.Contains("thiết bị") || text.Contains("obstacle")) return "AVOIDANCE";
            if (text.Contains("bridge") || text.Contains("runtime") || text.Contains("assembly") || text.Contains("methodnotfound")) return "RUNTIME_BRIDGE";
            if (text.Contains("layout") || text.Contains("geometry") || text.Contains("xương") || text.Contains("hanger")) return "LAYOUT_GEOMETRY";
            return exception == null && issues.Count == 0 ? "NONE" : "GENERAL";
        }

        private static string BuildDiagnosticJson(
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            VxtSettings settings,
            AnalysisResult analysis,
            System.Exception exception,
            string stage,
            bool automatic,
            Dictionary<string, int> counts)
        {
            var sb = new StringBuilder(4096);
            sb.Append("{\n");
            JsonProp(sb, "schema", "hnl-vxt-diagnostic-v1", true);
            JsonProp(sb, "product", ProductLabel, true);
            JsonProp(sb, "timestamp", DateTimeOffset.Now.ToString("o"), true);
            JsonProp(sb, "stage", stage, true);
            JsonProp(sb, "automatic", automatic, true);
            JsonProp(sb, "drawing", SafeDrawingName(doc), true);
            JsonProp(sb, "category", analysis.Category, true);
            JsonProp(sb, "summary", analysis.Summary, true);
            JsonProp(sb, "exceptionType", exception?.GetType().FullName ?? string.Empty, true);
            JsonProp(sb, "exceptionMessage", exception?.Message ?? string.Empty, true);
            sb.Append("  \"issues\": [");
            for (var i = 0; i < analysis.Issues.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\n    \"").Append(JsonEscape(analysis.Issues[i])).Append("\"");
            }
            if (analysis.Issues.Count > 0) sb.Append("\n  ");
            sb.Append("],\n");
            sb.Append("  \"counts\": {");
            if (counts != null)
            {
                var first = true;
                foreach (var kv in counts)
                {
                    if (!first) sb.Append(", ");
                    sb.Append("\"").Append(JsonEscape(kv.Key)).Append("\": ").Append(kv.Value.ToString(CultureInfo.InvariantCulture));
                    first = false;
                }
            }
            sb.Append("}\n}");
            return sb.ToString();
        }

        private static string BuildEnvironmentJson(Autodesk.AutoCAD.ApplicationServices.Document doc, string stage, bool automatic)
        {
            var asm = Assembly.GetExecutingAssembly();
            var sb = new StringBuilder(2048);
            sb.Append("{\n");
            JsonProp(sb, "product", ProductLabel, true);
            JsonProp(sb, "assemblyVersion", asm.GetName().Version?.ToString() ?? "N/A", true);
            JsonProp(sb, "assemblyFileVersion", SafeFileVersion(asm.Location), true);
            JsonProp(sb, "timestamp", DateTimeOffset.Now.ToString("o"), true);
            JsonProp(sb, "stage", stage, true);
            JsonProp(sb, "automatic", automatic, true);
            JsonProp(sb, "os", Environment.OSVersion.ToString(), true);
            JsonProp(sb, "clr", Environment.Version.ToString(), true);
            JsonProp(sb, "is64BitProcess", Environment.Is64BitProcess, true);
            JsonProp(sb, "acadver", SafeSystemVariable("ACADVER"), true);
            JsonProp(sb, "acadProductVersion", SafeAcadFileVersion(), true);
            JsonProp(sb, "insunits", SafeSystemVariable("INSUNITS"), true);
            JsonProp(sb, "measurement", SafeSystemVariable("MEASUREMENT"), true);
            JsonProp(sb, "worldUcs", SafeSystemVariable("WORLDUCS"), true);
            JsonProp(sb, "drawing", SafeDrawingName(doc), false);
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string BuildSettingsJson(VxtSettings s)
        {
            if (s == null) return "{}";
            var sb = new StringBuilder(4096);
            sb.Append("{\n");
            JsonProp(sb, "drawMain", s.DrawMain, true);
            JsonProp(sb, "useDynamicMainBlock", s.UseDynamicMainBlock, true);
            JsonProp(sb, "mainBlockName", s.MainBlockName, true);
            JsonProp(sb, "mainMinSpacing", s.MainMinSpacing, true);
            JsonProp(sb, "mainMaxSpacing", s.MainMaxSpacing, true);
            JsonProp(sb, "mainMinEdgeOffset", s.MainMinEdgeOffset, true);
            JsonProp(sb, "mainMaxEdgeOffset", s.MainMaxEdgeOffset, true);
            JsonProp(sb, "mainBalanceStep", s.MainBalanceStep, true);
            JsonProp(sb, "mainSkipLimit", s.MainSkipLimit, true);
            JsonProp(sb, "mainDirection", s.MainDirection.ToString(), true);
            JsonProp(sb, "mainLayout", s.MainLayout.ToString(), true);
            JsonProp(sb, "directionDegrees", s.DirectionDegrees, true);
            JsonProp(sb, "drawFurring", s.DrawFurring, true);
            JsonProp(sb, "useDynamicFurringBlock", s.UseDynamicFurringBlock, true);
            JsonProp(sb, "furringBlockName", s.FurringBlockName, true);
            JsonProp(sb, "furringSpacing", s.FurringSpacing, true);
            JsonProp(sb, "askDirectionEachRegion", s.AskDirectionEachRegion, true);
            JsonProp(sb, "drawHangers", s.DrawHangers, true);
            JsonProp(sb, "hangerBlockName", s.HangerBlockName, true);
            JsonProp(sb, "hangerMinSpacing", s.HangerMinSpacing, true);
            JsonProp(sb, "hangerMaxSpacing", s.HangerMaxSpacing, true);
            JsonProp(sb, "hangerMinEdgeOffset", s.HangerMinEdgeOffset, true);
            JsonProp(sb, "hangerMaxEdgeOffset", s.HangerMaxEdgeOffset, true);
            JsonProp(sb, "hangerBalanceStep", s.HangerBalanceStep, true);
            JsonProp(sb, "hangerLayout", s.HangerLayout.ToString(), true);
            JsonProp(sb, "useAvoidance", s.UseAvoidance, true);
            JsonProp(sb, "shiftAllForAvoidance", s.ShiftAllForAvoidance, true);
            JsonProp(sb, "clearanceDistance", s.ClearanceDistance, true);
            JsonProp(sb, "autoDimension", s.AutoDimension, true);
            JsonProp(sb, "dimMain", s.DimMain, true);
            JsonProp(sb, "dimFurring", s.DimFurring, true);
            JsonProp(sb, "dimHanger", s.DimHanger, true);
            JsonProp(sb, "mainDimPosition", s.MainDimPosition.ToString(), true);
            JsonProp(sb, "furringDimPosition", s.FurringDimPosition.ToString(), true);
            JsonProp(sb, "hangerDimPosition", s.HangerDimPosition.ToString(), true);
            JsonProp(sb, "dimensionDistance", s.DimensionDistance, true);
            JsonProp(sb, "dimensionSpacing", s.DimensionSpacing, true);
            JsonProp(sb, "mainLayer", s.MainLayer, true);
            JsonProp(sb, "furringLayer", s.FurringLayer, true);
            JsonProp(sb, "hangerLayer", s.HangerLayer, true);
            JsonProp(sb, "dimensionLayer", s.DimensionLayer, true);
            JsonProp(sb, "dimensionStyle", s.DimensionStyle, false);
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string BuildGeometryJson()
        {
            var session = VxtSession.Current;
            var sb = new StringBuilder(8192);
            sb.Append("{\n");
            JsonProp(sb, "hasBoundary", session.HasBoundary, true);
            JsonProp(sb, "globalFurringFromFarEdge", session.GlobalFurringFromFarEdge, true);
            JsonProp(sb, "generalEquipmentCount", session.GeneralEquipmentIds.Length, true);
            JsonProp(sb, "mainEquipmentCount", session.MainEquipmentIds.Length, true);
            JsonProp(sb, "furringEquipmentCount", session.FurringEquipmentIds.Length, true);
            sb.Append("  \"boundary\": [");
            if (session.HasBoundary)
            {
                for (var i = 0; i < session.Boundary.Vertices.Count; i++)
                {
                    var p = session.Boundary.Vertices[i];
                    if (i > 0) sb.Append(",");
                    sb.Append("\n    {\"x\": ").Append(Num(p.X)).Append(", \"y\": ").Append(Num(p.Y)).Append("}");
                }
                if (session.Boundary.Vertices.Count > 0) sb.Append("\n  ");
            }
            sb.Append("],\n  \"regions\": [");
            for (var i = 0; i < session.Regions.Count; i++)
            {
                var r = session.Regions[i];
                if (i > 0) sb.Append(",");
                sb.Append("\n    {\"minX\": ").Append(Num(r.WorldBounds.MinX))
                  .Append(", \"minY\": ").Append(Num(r.WorldBounds.MinY))
                  .Append(", \"maxX\": ").Append(Num(r.WorldBounds.MaxX))
                  .Append(", \"maxY\": ").Append(Num(r.WorldBounds.MaxY))
                  .Append(", \"mainAngleDegrees\": ").Append(Num(r.MainAngleDegrees))
                  .Append(", \"furringFromFarEdge\": ").Append(r.FurringFromFarEdge ? "true" : "false").Append("}");
            }
            if (session.Regions.Count > 0) sb.Append("\n  ");
            sb.Append("]\n}\n");
            return sb.ToString();
        }

        private static string BuildHumanReport(
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            VxtSettings settings,
            AnalysisResult analysis,
            System.Exception exception,
            string stage,
            bool automatic,
            string packagePath)
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("HNL TOOL - VXT PRO DIAGNOSTIC REPORT");
            sb.AppendLine("========================================");
            sb.AppendLine("Product: " + ProductLabel);
            sb.AppendLine("Time: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            sb.AppendLine("Stage: " + stage);
            sb.AppendLine("Mode: " + (automatic ? "AUTO-ERROR-CAPTURE" : "MANUAL"));
            sb.AppendLine("Drawing: " + SafeDrawingName(doc));
            sb.AppendLine("Category: " + analysis.Category);
            sb.AppendLine("Summary: " + analysis.Summary);
            sb.AppendLine("Package: " + packagePath);
            sb.AppendLine();
            sb.AppendLine("[ISSUES]");
            if (analysis.Issues.Count == 0) sb.AppendLine("- None detected by static/runtime preflight.");
            else foreach (var issue in analysis.Issues) sb.AppendLine("- " + issue);
            sb.AppendLine();
            sb.AppendLine("[EXCEPTION]");
            sb.AppendLine(exception == null ? "None" : exception.ToString());
            sb.AppendLine();
            sb.AppendLine("[NEXT ACTION]");
            sb.AppendLine(analysis.Issues.Count == 0
                ? "Run Runtime Golden: Preview -> VXTCREATE -> verify XC/XP/Ty/DIM -> UNDO."
                : "Resolve issues above, then rerun Preview and VXTCREATE. Send this ZIP if the failure persists.");
            return sb.ToString();
        }

        private static string BuildReproSteps(VxtSettings settings, AnalysisResult analysis)
        {
            var sb = new StringBuilder();
            sb.AppendLine("HNL Tool - VXT Pro | Reproduction Steps");
            sb.AppendLine("1. Open the original DWG in the same AutoCAD version.");
            sb.AppendLine("2. Run VXT.");
            sb.AppendLine("3. Restore settings from settings.json.");
            sb.AppendLine("4. Select the same closed boundary using geometry.json as reference.");
            sb.AppendLine("5. Restore rectangle regions/equipment selections if used.");
            sb.AppendLine("6. Refresh Preview and verify summary.");
            sb.AppendLine("7. Run VXTCREATE.");
            sb.AppendLine("8. If creation succeeds, verify XC/XP/Ty/DIM then UNDO.");
            sb.AppendLine("9. If it fails, export a new diagnostic ZIP immediately.");
            sb.AppendLine();
            sb.AppendLine("Analyzer: " + analysis.Summary);
            return sb.ToString();
        }

        private static void RecordGolden(
            string result,
            VxtSettings settings,
            Dictionary<string, int> counts,
            System.Exception exception,
            string packagePath,
            string stage)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool", "VXT Pro");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "runtime-golden.jsonl");
                var doc = Application.DocumentManager.MdiActiveDocument;
                var session = VxtSession.Current;
                var sb = new StringBuilder(1024);
                sb.Append("{");
                sb.Append("\"timestamp\":\"").Append(JsonEscape(DateTimeOffset.Now.ToString("o"))).Append("\",");
                sb.Append("\"product\":\"").Append(JsonEscape(ProductLabel)).Append("\",");
                sb.Append("\"result\":\"").Append(JsonEscape(result)).Append("\",");
                sb.Append("\"stage\":\"").Append(JsonEscape(stage)).Append("\",");
                sb.Append("\"drawing\":\"").Append(JsonEscape(doc == null ? "N/A" : SafeDrawingName(doc))).Append("\",");
                sb.Append("\"acadver\":\"").Append(JsonEscape(SafeSystemVariable("ACADVER"))).Append("\",");
                sb.Append("\"hasBoundary\":").Append(session.HasBoundary ? "true" : "false").Append(",");
                sb.Append("\"regions\":").Append(session.Regions.Count).Append(",");
                sb.Append("\"error\":\"").Append(JsonEscape(exception?.Message ?? string.Empty)).Append("\",");
                sb.Append("\"diagnosticZip\":\"").Append(JsonEscape(packagePath ?? string.Empty)).Append("\",");
                sb.Append("\"counts\":{");
                if (counts != null)
                {
                    var first = true;
                    foreach (var kv in counts)
                    {
                        if (!first) sb.Append(",");
                        sb.Append("\"").Append(JsonEscape(kv.Key)).Append("\":").Append(kv.Value.ToString(CultureInfo.InvariantCulture));
                        first = false;
                    }
                }
                sb.Append("}}\n");
                lock (Sync)
                    File.AppendAllText(file, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private static string ReadGoldenTail(int maxLines)
        {
            try
            {
                var file = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool", "VXT Pro", "runtime-golden.jsonl");
                if (!File.Exists(file)) return string.Empty;
                var lines = File.ReadAllLines(file);
                return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - maxLines)));
            }
            catch { return string.Empty; }
        }

        private static void AddText(ZipArchive zip, string name, string text)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(text ?? string.Empty);
        }

        private static bool Has(SymbolTable table, string name)
        {
            try { return table != null && !string.IsNullOrWhiteSpace(name) && table.Has(name); }
            catch { return false; }
        }

        private static string SafeSystemVariable(string name)
        {
            try { return Convert.ToString(Application.GetSystemVariable(name), CultureInfo.InvariantCulture); }
            catch { return "N/A"; }
        }

        private static string SafeAcadFileVersion()
        {
            try
            {
                var fileName = Process.GetCurrentProcess().MainModule?.FileName;
                return string.IsNullOrWhiteSpace(fileName) ? "N/A" : FileVersionInfo.GetVersionInfo(fileName).FileVersion;
            }
            catch { return "N/A"; }
        }

        private static string SafeFileVersion(string file)
        {
            try { return string.IsNullOrWhiteSpace(file) ? "N/A" : FileVersionInfo.GetVersionInfo(file).FileVersion; }
            catch { return "N/A"; }
        }

        private static string SafeDrawingName(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            try { return doc?.Name ?? "N/A"; }
            catch { return "N/A"; }
        }

        private static string Num(double value) => value.ToString("0.##########", CultureInfo.InvariantCulture);

        private static void JsonProp(StringBuilder sb, string name, string value, bool comma)
            => sb.Append("  \"").Append(JsonEscape(name)).Append("\": \"").Append(JsonEscape(value ?? string.Empty)).Append("\"").Append(comma ? ",\n" : "\n");

        private static void JsonProp(StringBuilder sb, string name, bool value, bool comma)
            => sb.Append("  \"").Append(JsonEscape(name)).Append("\": ").Append(value ? "true" : "false").Append(comma ? ",\n" : "\n");

        private static void JsonProp(StringBuilder sb, string name, int value, bool comma)
            => sb.Append("  \"").Append(JsonEscape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture)).Append(comma ? ",\n" : "\n");

        private static void JsonProp(StringBuilder sb, string name, double value, bool comma)
            => sb.Append("  \"").Append(JsonEscape(name)).Append("\": ").Append(Num(value)).Append(comma ? ",\n" : "\n");

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length + 16);
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        private sealed class AnalysisResult
        {
            public AnalysisResult(string category, string summary, List<string> issues)
            {
                Category = category;
                Summary = summary;
                Issues = issues;
            }

            public string Category { get; }
            public string Summary { get; }
            public List<string> Issues { get; }
        }
    }
}
