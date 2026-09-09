using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Non-destructive runtime QA for the Pro Auto-direction/quality phase.
    /// It runs deterministic Core scenarios inside the real AutoCAD host and writes evidence,
    /// but never opens a write transaction and never changes the active DWG.
    /// </summary>
    internal static class VxtProAutoRuntimeQaService
    {
        private const string Stage = "RuntimeProAutoQualityQA";

        public static string Run()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return "FAIL Pro Auto QA: Không có bản vẽ AutoCAD đang hoạt động.";

            try
            {
                var rotated = CheckRotatedRectangle();
                var concave = CheckConcaveCeiling();
                var mep = CheckMepAvoidance();
                var legacy = CheckLegacyIsolation();

                var summary = "PASS Pro Auto QA: HCN xoay=" + rotated +
                              " | Trần lõm=" + concave +
                              " | MEP=" + mep +
                              " | Legacy=" + legacy +
                              " | DWG không thay đổi.";
                WriteLog("PASS", summary, null);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                return summary;
            }
            catch (System.Exception ex)
            {
                var diagnostic = VxtDiagnosticService.CaptureCreateFailure(
                    new VxtSettings { OptimizationMode = VxtOptimizationMode.ProEconomy },
                    ex,
                    "VxtProAutoRuntimeQaService.Run");
                var summary = "FAIL Pro Auto QA: " + ex.Message +
                              (string.IsNullOrWhiteSpace(diagnostic) ? string.Empty : " | Diagnostic: " + diagnostic);
                WriteLog("FAIL", summary, diagnostic);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                return summary;
            }
        }

        private static string CheckRotatedRectangle()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { RotatedRectangle(6000.0, 4000.0, 30.0) },
                settings,
                new VxtLayoutContext());

            if (plan.Quality == null) throw new InvalidOperationException("HCN xoay không có Quality telemetry.");
            if (plan.Quality.AutoDirectionCandidateCount < 4)
                throw new InvalidOperationException("HCN xoay không quét đủ phương án Auto.");
            var angle = plan.Quality.SelectedDirectionDegrees;
            if (AngularDistance180(angle, 30.0) > 2.0 && AngularDistance180(angle, 120.0) > 2.0)
                throw new InvalidOperationException("Auto không bám trục HCN xoay. Hướng=" + angle.ToString("0.###", CultureInfo.InvariantCulture));
            if (!plan.Texts.Any(x => x.Text != null && x.Text.StartsWith("HNL Pro Q", StringComparison.Ordinal)))
                throw new InvalidOperationException("Preview thiếu Quality label HNL Pro.");
            return angle.ToString("0.#", CultureInfo.InvariantCulture) + "°/Q" + plan.Quality.QualityScore100;
        }

        private static string CheckConcaveCeiling()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.DrawFurring = true;
            settings.DrawHangers = true;
            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(7000, 0),
                new Point2(7000, 2600),
                new Point2(4200, 2600),
                new Point2(4200, 5200),
                new Point2(0, 5200)
            });
            var plan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, new VxtLayoutContext());
            if (plan.Quality == null || plan.Quality.HardViolationCount != 0)
                throw new InvalidOperationException("Trần lõm có hard violation trong Pro Auto.");
            if (plan.MainSegmentCount <= 0 || plan.FurringSegmentCount <= 0 || plan.HangerCount <= 0)
                throw new InvalidOperationException("Trần lõm thiếu XC/XP/Ty sau Pro Auto.");
            return "XC" + plan.MainSegmentCount + "/XP" + plan.FurringSegmentCount + "/Ty" + plan.HangerCount;
        }

        private static string CheckMepAvoidance()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Horizontal;
            settings.UseAvoidance = true;
            settings.ShiftAllForAvoidance = true;
            settings.ClearanceDistance = 0.0;
            var context = new VxtLayoutContext();
            context.MainObstacles.Add(new Box2(0.0, 325.0, 6000.0, 375.0));
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4100.0) }, settings, context);
            if (plan.Quality == null || plan.Quality.CollisionCount != 0 || plan.Quality.QualityScore100 != 100)
                throw new InvalidOperationException("Quality gate phát hiện va chạm MEP sau khi có phương án dịch hợp lệ.");
            return "Q100/VC0";
        }

        private static string CheckLegacyIsolation()
        {
            var settings = BaseSettings(VxtOptimizationMode.Legacy);
            settings.MainDirection = MainDirectionMode.Auto;
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) }, settings, new VxtLayoutContext());
            if (plan.MainSegmentCount != 5)
                throw new InvalidOperationException("Legacy Auto Golden không còn 5 XC.");
            if (plan.Quality != null)
                throw new InvalidOperationException("Legacy bị nhiễm Pro Quality telemetry.");
            return "XC5/isolated";
        }

        private static VxtSettings BaseSettings(VxtOptimizationMode mode)
            => new VxtSettings
            {
                OptimizationMode = mode,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

        private static Boundary2 Rectangle(double width, double height)
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0), new Point2(width, 0.0),
                new Point2(width, height), new Point2(0.0, height)
            });

        private static Boundary2 RotatedRectangle(double width, double height, double degrees)
        {
            var r = degrees * Math.PI / 180.0;
            var c = Math.Cos(r);
            var s = Math.Sin(r);
            return new Boundary2(new[]
            {
                Rotate(0.0, 0.0, c, s),
                Rotate(width, 0.0, c, s),
                Rotate(width, height, c, s),
                Rotate(0.0, height, c, s)
            });
        }

        private static Point2 Rotate(double x, double y, double c, double s)
            => new Point2(x * c - y * s, x * s + y * c);

        private static double AngularDistance180(double a, double b)
        {
            a %= 180.0; if (a < 0.0) a += 180.0;
            b %= 180.0; if (b < 0.0) b += 180.0;
            var d = Math.Abs(a - b);
            return Math.Min(d, 180.0 - d);
        }

        private static void WriteLog(string result, string summary, string diagnosticPath)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool", "VXT Pro");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "runtime-pro-auto-qa.jsonl");
                var sb = new StringBuilder(768);
                sb.Append("{\"timestamp\":\"").Append(Escape(DateTimeOffset.Now.ToString("o"))).Append("\",");
                sb.Append("\"result\":\"").Append(Escape(result)).Append("\",");
                sb.Append("\"stage\":\"").Append(Stage).Append("\",");
                sb.Append("\"engine\":\"ProAutoMultiAngle\",");
                sb.Append("\"acadver\":\"").Append(Escape(SafeSystemVariable("ACADVER"))).Append("\",");
                sb.Append("\"summary\":\"").Append(Escape(summary)).Append("\",");
                sb.Append("\"diagnosticZip\":\"").Append(Escape(diagnosticPath ?? string.Empty)).Append("\"}\n");
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
