using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Non-destructive Runtime Golden self-test. It exercises the real AutoCAD database API
    /// (layers, ModelSpace entities, BlockReference and RotatedDimension) inside one transaction
    /// that is intentionally NOT committed, so the user's DWG remains unchanged.
    /// </summary>
    internal static class VxtRuntimeGoldenService
    {
        private const int ExpectedMain = 5;
        private const int ExpectedFurring = 14;
        private const int ExpectedHangers = 35;
        private const int ExpectedDimensions = 29;

        public static string Run()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                const string noDoc = "FAIL Runtime Golden: Không có bản vẽ AutoCAD đang hoạt động.";
                VxtSession.Current.ViewModel?.SetRuntimeGoldenResult(false, noDoc, null);
                return noDoc;
            }

            var sw = Stopwatch.StartNew();
            var tempSuffix = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
            var tempBlockName = "HNL_VXT_GOLDEN_TY_" + tempSuffix;
            var settings = BuildGoldenSettings(tempSuffix, tempBlockName);
            var counts = new Dictionary<string, int>();

            try
            {
                var boundary = new Boundary2(new[]
                {
                    new Point2(0.0, 0.0),
                    new Point2(6000.0, 0.0),
                    new Point2(6000.0, 4000.0),
                    new Point2(0.0, 4000.0)
                });

                var plan = new VxtPreviewPlanBuilder().Build(boundary, settings, new VxtLayoutContext());
                ValidateCoreContract(plan, settings);

                var db = doc.Database;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // Resource creation is exercised in the same transaction and rolled back.
                    VxtCadResources.EnsureAll(db, tr, settings);

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                    if (bt.Has(tempBlockName))
                        throw new InvalidOperationException("Tên Block Runtime Golden tạm thời đã tồn tại ngoài dự kiến.");

                    var hangerBlock = CreateTemporaryHangerBlock(db, tr, bt, tempBlockName);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);

                    var mainLayer = lt[settings.MainLayer];
                    var furringLayer = lt[settings.FurringLayer];
                    var hangerLayer = lt[settings.HangerLayer];
                    var dimLayer = lt[settings.DimensionLayer];

                    var main = 0;
                    var furring = 0;
                    var hangers = 0;
                    var dimensions = 0;

                    foreach (var item in plan.Lines)
                    {
                        if (item.Kind != PreviewLineKind.Main && item.Kind != PreviewLineKind.Furring) continue;
                        var polyline = new Polyline(2);
                        polyline.SetDatabaseDefaults(db);
                        polyline.AddVertexAt(0, new Point2d(item.A.X, item.A.Y), 0.0, 0.0, 0.0);
                        polyline.AddVertexAt(1, new Point2d(item.B.X, item.B.Y), 0.0, 0.0, 0.0);
                        VxtCadResources.ApplyByLayer(polyline, item.Kind == PreviewLineKind.Main ? mainLayer : furringLayer);
                        ms.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                        if (item.Kind == PreviewLineKind.Main) main++;
                        else furring++;
                    }

                    foreach (var point in plan.HangerPoints)
                    {
                        var br = new BlockReference(new Point3d(point.X, point.Y, 0.0), hangerBlock);
                        br.SetDatabaseDefaults(db);
                        VxtCadResources.ApplyByLayer(br, hangerLayer);
                        ms.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);
                        hangers++;
                    }

                    var dimStyleId = VxtTransientPreview.ResolveDimStyle(settings.DimensionStyle, db, dst);
                    foreach (var item in plan.Dimensions)
                    {
                        var dim = new RotatedDimension(
                            item.RotationRadians,
                            new Point3d(item.ExtensionPoint1.X, item.ExtensionPoint1.Y, 0.0),
                            new Point3d(item.ExtensionPoint2.X, item.ExtensionPoint2.Y, 0.0),
                            new Point3d(item.DimensionLinePoint.X, item.DimensionLinePoint.Y, 0.0),
                            string.Empty,
                            dimStyleId);
                        dim.SetDatabaseDefaults(db);
                        VxtCadResources.ApplyByLayer(dim, dimLayer);
                        ms.AppendEntity(dim);
                        tr.AddNewlyCreatedDBObject(dim, true);
                        dimensions++;
                    }

                    counts["main"] = main;
                    counts["furring"] = furring;
                    counts["hangers"] = hangers;
                    counts["dimensions"] = dimensions;
                    ValidateCreatedCounts(main, furring, hangers, dimensions);

                    // Intentionally no Commit(): Dispose() aborts all temporary DB changes.
                }

                ValidateRollback(db, settings, tempBlockName);
                sw.Stop();
                var summary = "PASS Runtime Golden: AutoCAD DB API + Core 6000x4000 OK | XC " +
                              ExpectedMain + " • XP " + ExpectedFurring + " • Ty " + ExpectedHangers +
                              " • DIM " + ExpectedDimensions + " | " + sw.ElapsedMilliseconds + " ms | DWG không bị thay đổi.";
                WriteGoldenLog("PASS", summary, counts, null);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                VxtSession.Current.ViewModel?.SetRuntimeGoldenResult(true, summary, null);
                return summary;
            }
            catch (System.Exception ex)
            {
                sw.Stop();
                var diagnosticPath = VxtDiagnosticService.CaptureCreateFailure(settings, ex, "VxtRuntimeGoldenService.Run");
                var summary = "FAIL Runtime Golden: " + ex.Message +
                              (string.IsNullOrWhiteSpace(diagnosticPath) ? string.Empty : " | Diagnostic: " + diagnosticPath);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + summary);
                VxtSession.Current.ViewModel?.SetRuntimeGoldenResult(false, summary, diagnosticPath);
                return summary;
            }
        }

        private static VxtSettings BuildGoldenSettings(string suffix, string hangerBlockName)
        {
            return new VxtSettings
            {
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false,
                HangerBlockName = hangerBlockName,
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true,
                UseAvoidance = false,
                MainLayer = "HNL_VXT_GOLDEN_XC_" + suffix,
                FurringLayer = "HNL_VXT_GOLDEN_XP_" + suffix,
                HangerLayer = "HNL_VXT_GOLDEN_TY_" + suffix,
                DimensionLayer = "HNL_VXT_GOLDEN_DIM_" + suffix,
                DimensionStyle = string.Empty
            };
        }

        private static void ValidateCoreContract(VxtPreviewPlan plan, VxtSettings settings)
        {
            if (plan == null) throw new InvalidOperationException("Core không trả về Runtime Golden plan.");
            if (Math.Abs(settings.FurringSpacing - 1220.0 / 3.0) > 1e-8)
                throw new InvalidOperationException("XP mặc định không còn bằng 1220/3.");
            ValidateCreatedCounts(plan.MainSegmentCount, plan.FurringSegmentCount, plan.HangerCount, plan.DimensionSegmentCount);
        }

        private static void ValidateCreatedCounts(int main, int furring, int hangers, int dimensions)
        {
            if (main != ExpectedMain || furring != ExpectedFurring || hangers != ExpectedHangers || dimensions != ExpectedDimensions)
                throw new InvalidOperationException(
                    "Sai Golden counts. Kỳ vọng XC/XP/Ty/DIM=" + ExpectedMain + "/" + ExpectedFurring + "/" +
                    ExpectedHangers + "/" + ExpectedDimensions + ", thực tế=" + main + "/" + furring + "/" +
                    hangers + "/" + dimensions + ".");
        }

        private static ObjectId CreateTemporaryHangerBlock(Database db, Transaction tr, BlockTable bt, string name)
        {
            var btr = new BlockTableRecord { Name = name, Origin = Point3d.Origin };
            var id = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            var h = new Line(new Point3d(-25.0, 0.0, 0.0), new Point3d(25.0, 0.0, 0.0));
            h.SetDatabaseDefaults(db);
            btr.AppendEntity(h);
            tr.AddNewlyCreatedDBObject(h, true);

            var v = new Line(new Point3d(0.0, -25.0, 0.0), new Point3d(0.0, 25.0, 0.0));
            v.SetDatabaseDefaults(db);
            btr.AppendEntity(v);
            tr.AddNewlyCreatedDBObject(v, true);
            return id;
        }

        private static void ValidateRollback(Database db, VxtSettings settings, string tempBlockName)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (bt.Has(tempBlockName))
                    throw new InvalidOperationException("Runtime Golden để lại Block tạm trong DWG.");
                if (lt.Has(settings.MainLayer) || lt.Has(settings.FurringLayer) || lt.Has(settings.HangerLayer) || lt.Has(settings.DimensionLayer))
                    throw new InvalidOperationException("Runtime Golden để lại Layer tạm trong DWG.");
                tr.Commit();
            }
        }

        private static void WriteGoldenLog(string result, string summary, IDictionary<string, int> counts, string diagnosticPath)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool", "VXT Pro");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "runtime-golden.jsonl");
                var sb = new StringBuilder(1024);
                sb.Append("{\"timestamp\":\"").Append(Escape(DateTimeOffset.Now.ToString("o"))).Append("\",");
                sb.Append("\"result\":\"").Append(Escape(result)).Append("\",");
                sb.Append("\"stage\":\"RuntimeGoldenSelfTest\",");
                sb.Append("\"acadver\":\"").Append(Escape(SafeSystemVariable("ACADVER"))).Append("\",");
                sb.Append("\"summary\":\"").Append(Escape(summary)).Append("\",");
                sb.Append("\"diagnosticZip\":\"").Append(Escape(diagnosticPath ?? string.Empty)).Append("\",");
                sb.Append("\"counts\":{");
                var first = true;
                if (counts != null)
                {
                    foreach (var kv in counts)
                    {
                        if (!first) sb.Append(",");
                        sb.Append("\"").Append(Escape(kv.Key)).Append("\":").Append(kv.Value.ToString(CultureInfo.InvariantCulture));
                        first = false;
                    }
                }
                sb.Append("}}\n");
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
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
