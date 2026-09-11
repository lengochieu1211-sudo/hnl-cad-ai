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
    /// Non-destructive runtime QA for the Pro multi-boundary phase.
    /// Runs deterministic Core scenarios inside the real AutoCAD host without opening
    /// a write transaction, then records evidence under LOCALAPPDATA/HNL Tool/VXT Pro.
    /// </summary>
    internal static class VxtProMultiRuntimeQaService
    {
        private const string Stage = "RuntimeProMultiBoundaryQA";

        public static string Run()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return "FAIL Pro Multi QA: Không có bản vẽ AutoCAD đang hoạt động.";

            try
            {
                var economy = CheckEconomyNoMaterialRegression();
                var alignment = CheckSharedAlignment();
                var independence = CheckModerateAngleIndependence();
                var mep = CheckDenseMepStress();
                var legacy = CheckLegacyIsolation();

                var summary = "PASS Pro Multi QA: VT=" + economy +
                              " | Căn trục=" + alignment +
                              " | Độc lập=" + independence +
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
                    new VxtSettings { OptimizationMode = VxtOptimizationMode.ProBalanced },
                    ex,
                    "VxtProMultiRuntimeQaService.Run");
                var summary = "FAIL Pro Multi QA: " + ex.Message +
                              (string.IsNullOrWhiteSpace(diagnostic) ? string.Empty : " | Diagnostic: " + diagnostic);
                WriteLog("FAIL", summary, diagnostic);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                return summary;
            }
        }

        private static string CheckEconomyNoMaterialRegression()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.DrawFurring = true;
            settings.DrawHangers = true;

            var boundaries = new[]
            {
                RotatedRectangle(6200.0, 4100.0, 18.0, 0.0, 0.0),
                RotatedRectangle(5600.0, 3600.0, 63.0, 9000.0, 500.0)
            };

            var independent = boundaries
                .Select(x => VxtProAutoDirectionPlanBuilder.Build(x, settings, new VxtLayoutContext()))
                .Select(x => x.Quality)
                .ToList();
            var independentQuality = VxtProPlanQualityEvaluator.Aggregate(independent);
            var combined = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());

            if (independentQuality == null || combined.Quality == null)
                throw new InvalidOperationException("Thiếu Quality telemetry cho bài test nhiều mảng Economy.");
            if (combined.Quality.HardViolationCount != 0 || combined.Quality.CollisionCount != 0)
                throw new InvalidOperationException("Economy nhiều mảng có Hard/VC khác 0.");
            if (combined.Quality.MaterialIndex > independentQuality.MaterialIndex + 0.1)
                throw new InvalidOperationException("Economy căn trục làm tăng chỉ số vật tư tổng.");
            if (combined.Quality.BoundaryCount != 2)
                throw new InvalidOperationException("Quality tổng không nhận đủ 2 mảng trần.");

            return combined.Quality.MaterialIndex.ToString("0", CultureInfo.InvariantCulture) +
                   "<= " + independentQuality.MaterialIndex.ToString("0", CultureInfo.InvariantCulture);
        }

        private static string CheckSharedAlignment()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.Auto;
            var boundaries = new[]
            {
                RotatedRectangle(6000.0, 4000.0, 27.0, 0.0, 0.0),
                RotatedRectangle(5200.0, 3200.0, 27.0, 8000.0, 0.0),
                RotatedRectangle(4500.0, 2800.0, 27.0, 14500.0, 500.0)
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());
            if (plan.Quality == null)
                throw new InvalidOperationException("Thiếu Quality tổng cho 3 mảng đồng trục.");
            if (plan.Quality.HardViolationCount != 0 || plan.Quality.CollisionCount != 0)
                throw new InvalidOperationException("3 mảng đồng trục có Hard/VC khác 0.");
            if (plan.Quality.DistinctDirectionCount != 1 || plan.Quality.AlignmentScore100 != 100)
                throw new InvalidOperationException("3 mảng cùng góc không đạt Alignment 100.");
            if (!plan.Quality.UsesSharedDirection)
                throw new InvalidOperationException("Balanced không chọn hướng chung cho 3 mảng cùng trục.");

            var globalLabels = plan.Texts.Count(x => x.Text != null && x.Text.StartsWith("HNL Pro Tổng Q", StringComparison.Ordinal));
            var localLabels = plan.Texts.Count(x => x.Text != null && x.Text.StartsWith("HNL Pro Q", StringComparison.Ordinal));
            if (globalLabels != 1 || localLabels != 0)
                throw new InvalidOperationException("Preview nhiều mảng không giữ đúng 1 Quality tổng.");

            return "A100/1 hướng/3 mảng";
        }

        private static string CheckModerateAngleIndependence()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.AutoShadowline = true;
            var boundaries = new[]
            {
                RotatedRectangle(6000.0, 4000.0, 0.0, 0.0, 0.0),
                RotatedRectangle(6000.0, 4000.0, 30.0, 9000.0, 0.0)
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());
            if (plan.Quality == null)
                throw new InvalidOperationException("Thiếu Quality cho bài test 2 mảng lệch 30°.");
            if (plan.Quality.HardViolationCount != 0 || plan.Quality.CollisionCount != 0)
                throw new InvalidOperationException("2 mảng lệch 30° có Hard/VC khác 0.");
            if (plan.Quality.UsesSharedDirection || plan.Quality.DistinctDirectionCount != 2)
                throw new InvalidOperationException(
                    "Pro Multi đã ép 2 mảng lệch 30° về một hướng chung dù cả hai đều hợp lệ.");

            return "30°=2 hướng";
        }

        private static string CheckDenseMepStress()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProConservative);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.DrawFurring = true;
            settings.DrawHangers = true;
            settings.UseAvoidance = true;
            settings.ShiftAllForAvoidance = true;
            settings.ClearanceDistance = 10.0;

            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0), new Point2(9000, 0), new Point2(9000, 2800),
                new Point2(6500, 2800), new Point2(6500, 6200), new Point2(3600, 6200),
                new Point2(3600, 7800), new Point2(0, 7800)
            });
            var context = new VxtLayoutContext();
            context.GeneralObstacles.AddRange(new[]
            {
                new Box2(900, 760, 1500, 820), new Box2(2400, 1510, 3100, 1570),
                new Box2(4700, 2260, 5400, 2320), new Box2(7000, 3110, 7600, 3170),
                new Box2(5200, 3860, 5900, 3920), new Box2(2500, 4610, 3200, 4670),
                new Box2(800, 5360, 1400, 5420), new Box2(4300, 6110, 5000, 6170),
                new Box2(1800, 6860, 2500, 6920), new Box2(700, 7310, 1300, 7370)
            });

            var plan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, context);
            if (plan.Quality == null)
                throw new InvalidOperationException("Stress MEP thiếu Quality telemetry.");
            if (plan.Quality.HardViolationCount != 0 || plan.Quality.CollisionCount != 0)
                throw new InvalidOperationException(
                    "Stress MEP chưa sạch: Hard=" + plan.Quality.HardViolationCount +
                    ", XC=" + plan.Quality.MainCollisionCount +
                    ", XP=" + plan.Quality.FurringCollisionCount +
                    ", Ty=" + plan.Quality.HangerCollisionCount + ".");
            if (plan.MainSegmentCount <= 0 || plan.FurringSegmentCount <= 0 || plan.HangerCount <= 0)
                throw new InvalidOperationException("Stress MEP làm mất XC/XP/Ty.");

            return "VC0/S" + plan.Quality.ObstacleSplitFallbackCount.ToString(CultureInfo.InvariantCulture);
        }

        private static string CheckLegacyIsolation()
        {
            var settings = BaseSettings(VxtOptimizationMode.Legacy);
            settings.MainDirection = MainDirectionMode.Auto;
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) }, settings, new VxtLayoutContext());
            if (plan.MainSegmentCount != 5)
                throw new InvalidOperationException("Legacy Golden không còn 5 XC.");
            if (plan.Quality != null)
                throw new InvalidOperationException("Legacy bị nhiễm Pro Quality/fallback telemetry.");
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

        private static Boundary2 RotatedRectangle(
            double width,
            double height,
            double degrees,
            double offsetX,
            double offsetY)
        {
            var r = degrees * Math.PI / 180.0;
            var c = Math.Cos(r);
            var s = Math.Sin(r);
            Point2 Rotate(double x, double y)
                => new Point2(offsetX + x * c - y * s, offsetY + x * s + y * c);
            return new Boundary2(new[]
            {
                Rotate(0.0, 0.0), Rotate(width, 0.0),
                Rotate(width, height), Rotate(0.0, height)
            });
        }

        private static void WriteLog(string result, string summary, string diagnosticPath)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool", "VXT Pro");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "runtime-pro-multi-qa.jsonl");
                var sb = new StringBuilder(1024);
                sb.Append("{\"timestamp\":\"").Append(Escape(DateTimeOffset.Now.ToString("o"))).Append("\",");
                sb.Append("\"result\":\"").Append(Escape(result)).Append("\",");
                sb.Append("\"stage\":\"").Append(Stage).Append("\",");
                sb.Append("\"engine\":\"ProMultiBoundaryQuality\",");
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
