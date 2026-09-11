using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtCreateEngine
    {
        public static void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var session = VxtSession.Current;
            var settings = session.ViewModel?.Snapshot() ?? session.Settings.Clone();
            var manualHangerOnly = !settings.DrawMain && !settings.DrawFurring && settings.DrawHangers &&
                                   !settings.AutoDimension && session.ManualMainIds.Length > 0;
            if (!session.HasBoundary && !manualHangerOnly)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Chưa chọn biên trần. Nếu chỉ rải Ty trên Xương chính có sẵn, hãy tắt XC/XP, bật Ty rồi Tạo.");
                return;
            }

            if (!settings.IsValid(out var error))
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: " + error);
                return;
            }

            VxtTransientPreview.Instance.Clear();
            var db = doc.Database;
            var counts = new CreateCounts();

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    VxtPreviewPlan plan;
                    if (session.HasBoundary)
                    {
                        var context = VxtLayoutContextFactory.Build(session, tr);
                        plan = VxtMultiBoundaryPlanBuilder.Build(session.Boundaries, settings, context);
                    }
                    else
                    {
                        plan = new VxtPreviewPlan();
                    }

                    ValidateRequiredResources(settings, plan, session, db, tr);
                    VxtCadResources.EnsureAll(db, tr, settings);

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);

                    var mainLayer = settings.DrawMain ? lt[settings.MainLayer] : ObjectId.Null;
                    var furringLayer = settings.DrawFurring ? lt[settings.FurringLayer] : ObjectId.Null;
                    var hangerLayer = settings.DrawHangers ? lt[settings.HangerLayer] : ObjectId.Null;
                    var dimLayer = VxtCadResources.NeedsDimensionResources(settings)
                        ? lt[settings.DimensionLayer]
                        : ObjectId.Null;

                    foreach (var item in plan.Lines)
                    {
                        if (item.Kind == PreviewLineKind.Main)
                        {
                            var blockDone = settings.UseDynamicMainBlock && bt.Has(settings.MainBlockName) &&
                                            AppendMemberBlock(ms, tr, db, bt[settings.MainBlockName], settings.MainBlockName,
                                                item, mainLayer, ref counts.Main);
                            if (!blockDone)
                                AppendPolyline(ms, tr, db, item, mainLayer, ref counts.Main);
                        }
                        else if (item.Kind == PreviewLineKind.Furring)
                        {
                            var blockDone = settings.UseDynamicFurringBlock && bt.Has(settings.FurringBlockName) &&
                                            AppendMemberBlock(ms, tr, db, bt[settings.FurringBlockName], settings.FurringBlockName,
                                                item, furringLayer, ref counts.Furring);
                            if (!blockDone)
                            {
                                if (!TryAppendFurringMline(ms, tr, db, item, furringLayer))
                                    AppendPolyline(ms, tr, db, item, furringLayer, ref counts.Furring);
                                else
                                    counts.Furring++;
                            }
                        }
                    }

                    if (settings.DrawHangers && plan.HangerPoints.Count > 0)
                    {
                        var hangerBlockId = bt[settings.HangerBlockName];
                        foreach (var point in plan.HangerPoints)
                        {
                            var br = new BlockReference(ToPoint3d(point), hangerBlockId)
                            {
                                Rotation = ResolveHangerRotation(plan, point)
                            };
                            br.SetDatabaseDefaults(db);
                            VxtCadResources.ApplyByLayer(br, hangerLayer);
                            ms.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);
                            counts.Hangers++;
                        }
                    }

                    if (settings.AutoDimension && plan.Dimensions.Count > 0)
                    {
                        var dimStyleId = VxtTransientPreview.ResolveDimStyle(settings.DimensionStyle, db, dst);
                        foreach (var item in plan.Dimensions)
                        {
                            var dim = new RotatedDimension(
                                item.RotationRadians,
                                ToPoint3d(item.ExtensionPoint1),
                                ToPoint3d(item.ExtensionPoint2),
                                ToPoint3d(item.DimensionLinePoint),
                                string.Empty,
                                dimStyleId);
                            dim.SetDatabaseDefaults(db);
                            VxtCadResources.ApplyByLayer(dim, dimLayer);
                            ms.AppendEntity(dim);
                            tr.AddNewlyCreatedDBObject(dim, true);
                            counts.Dimensions++;
                        }
                    }

                    var manualCounts = VxtManualExistingEngine.Append(
                        db, tr, ms, bt, lt, dst, session, settings);
                    counts.Hangers += manualCounts.Hangers;
                    counts.Dimensions += manualCounts.Dimensions;

                    tr.Commit();
                    session.Settings = settings.Clone();
                }

                VxtDiagnosticService.RecordCreateSuccess(
                    settings, counts.Main, counts.Furring, counts.Hangers, counts.Dimensions);

                doc.Editor.WriteMessage(
                    "\nHNL Tool - VXT Pro: Đã tạo thành công " + counts.Main + " Xương chính, " +
                    counts.Furring + " Xương phụ, " + counts.Hangers + " Ty treo, " +
                    counts.Dimensions + " DIM. Dùng UNDO để hoàn tác toàn bộ thao tác tạo.");

                if (session.HasBoundary) VxtTransientPreview.Instance.Refresh();
                else VxtTransientPreview.Instance.Clear();
            }
            catch (System.Exception ex)
            {
                var diagnosticPath = VxtDiagnosticService.CaptureCreateFailure(
                    settings, ex, "VxtCreateEngine.Execute");

                doc.Editor.WriteMessage(
                    "\nHNL Tool - VXT Pro: Không tạo được khung xương. Đã rollback toàn bộ. " + ex.Message +
                    (string.IsNullOrWhiteSpace(diagnosticPath) ? string.Empty : " | Diagnostic: " + diagnosticPath));
                session.ViewModel?.SetPreviewError(
                    "Tạo thất bại - đã rollback. Diagnostic ZIP đã được ghi tự động.");
                try { if (session.HasBoundary) VxtTransientPreview.Instance.Refresh(); } catch { }
            }
        }

        private static void ValidateRequiredResources(VxtSettings settings, VxtPreviewPlan plan, VxtSession session, Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var needsManualHangers = settings.DrawHangers && !settings.DrawMain && session.ManualMainIds.Length > 0;
            if (settings.DrawHangers && (plan.HangerPoints.Count > 0 || needsManualHangers) &&
                (string.IsNullOrWhiteSpace(settings.HangerBlockName) || !bt.Has(settings.HangerBlockName)))
                throw new InvalidOperationException("Không tìm thấy Block Ty treo '" + settings.HangerBlockName + "'. Hãy chọn Block Ty trước khi tạo.");

            var needsPlanDimensions = settings.AutoDimension && plan.Dimensions.Count > 0;
            var needsManualDimensions = settings.AutoDimension && session.HasBoundary &&
                                        ((settings.DimMain && !settings.DrawMain && session.ManualMainIds.Length > 0) ||
                                         (settings.DimFurring && !settings.DrawFurring && session.ManualFurringIds.Length > 0) ||
                                         (settings.DimHanger && !settings.DrawHangers && session.ManualHangerIds.Length > 0));
            if ((needsPlanDimensions || needsManualDimensions) && !string.IsNullOrWhiteSpace(settings.DimensionStyle))
            {
                var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                if (!dst.Has(settings.DimensionStyle))
                    throw new InvalidOperationException("Không tìm thấy DimStyle '" + settings.DimensionStyle + "'.");
            }
        }

        private static bool AppendMemberBlock(
            BlockTableRecord ms,
            Transaction tr,
            Database db,
            ObjectId blockId,
            string effectiveName,
            PreviewLine item,
            ObjectId layerId,
            ref int count)
        {
            var start = ToPoint3d(item.A);
            var end = ToPoint3d(item.B);
            var vector = end - start;
            if (vector.Length <= 1e-9) return false;

            var br = new BlockReference(start, blockId)
            {
                Rotation = Math.Atan2(vector.Y, vector.X)
            };
            br.SetDatabaseDefaults(db);
            VxtCadResources.ApplyByLayer(br, layerId);
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);

            var mode = VxtDynamicBlockAdapter.ApplyMemberLength(br, effectiveName, vector.Length);
            if (string.Equals(mode, "unchanged", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase))
            {
                try { br.Erase(); } catch { }
                return false;
            }

            count++;
            return true;
        }

        private static void AppendPolyline(
            BlockTableRecord ms,
            Transaction tr,
            Database db,
            PreviewLine item,
            ObjectId layerId,
            ref int count)
        {
            var pl = new Polyline(2);
            pl.SetDatabaseDefaults(db);
            pl.AddVertexAt(0, new Point2d(item.A.X, item.A.Y), 0.0, 0.0, 0.0);
            pl.AddVertexAt(1, new Point2d(item.B.X, item.B.Y), 0.0, 0.0, 0.0);
            VxtCadResources.ApplyByLayer(pl, layerId);
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            count++;
        }

        private static bool TryAppendFurringMline(
            BlockTableRecord ms,
            Transaction tr,
            Database db,
            PreviewLine item,
            ObjectId layerId)
        {
            try
            {
                var styleId = EnsureXp35Style(db, tr);
                if (styleId.IsNull) return false;

                var ml = new Mline
                {
                    Style = styleId,
                    Scale = 1.0,
                    Justification = MlineJustification.Zero,
                    Normal = Vector3d.ZAxis
                };
                ml.SetDatabaseDefaults(db);
                VxtCadResources.ApplyByLayer(ml, layerId);
                ml.AppendSegment(ToPoint3d(item.A));
                ml.AppendSegment(ToPoint3d(item.B));
                ms.AppendEntity(ml);
                tr.AddNewlyCreatedDBObject(ml, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ObjectId EnsureXp35Style(Database db, Transaction tr)
        {
            const string dictionaryName = "ACAD_MLINESTYLE";
            const string styleName = "XP_35";
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(dictionaryName)) return ObjectId.Null;
            var dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForRead);
            if (dict.Contains(styleName)) return dict.GetAt(styleName);

            try { db.LoadLineTypeFile("ACAD_ISO10W100", "acadiso.lin"); } catch { }
            var ltypes = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            var byLayerId = ltypes.Has("ByLayer") ? ltypes["ByLayer"] : db.Celtype;
            var centerId = ltypes.Has("ACAD_ISO10W100") ? ltypes["ACAD_ISO10W100"] : byLayerId;

            dict.UpgradeOpen();
            var style = new MlineStyle
            {
                Name = styleName,
                Description = "HNL Tool - Xương Phụ 35mm",
                StartAngle = Math.PI * 0.5,
                EndAngle = Math.PI * 0.5
            };
            style.Elements.Add(new MlineStyleElement(17.5, Color.FromColorIndex(ColorMethod.ByAci, 256), byLayerId), true);
            style.Elements.Add(new MlineStyleElement(0.0, Color.FromColorIndex(ColorMethod.ByAci, 9), centerId), false);
            style.Elements.Add(new MlineStyleElement(-17.5, Color.FromColorIndex(ColorMethod.ByAci, 256), byLayerId), false);
            var id = dict.SetAt(styleName, style);
            tr.AddNewlyCreatedDBObject(style, true);
            return id;
        }

        private static double ResolveHangerRotation(VxtPreviewPlan plan, Point2 point)
        {
            var nearest = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => new { Line = x, Distance = DistanceToSegment(point, x.A, x.B) })
                .OrderBy(x => x.Distance)
                .FirstOrDefault();
            return nearest == null ? 0.0 : Math.Atan2(nearest.Line.B.Y - nearest.Line.A.Y, nearest.Line.B.X - nearest.Line.A.X);
        }

        private static double DistanceToSegment(Point2 p, Point2 a, Point2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var l2 = dx * dx + dy * dy;
            if (l2 <= 1e-12) return p.DistanceTo(a);
            var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return p.DistanceTo(new Point2(a.X + t * dx, a.Y + t * dy));
        }

        private static Point3d ToPoint3d(Point2 p) => new Point3d(p.X, p.Y, 0.0);

        private sealed class CreateCounts
        {
            public int Main;
            public int Furring;
            public int Hangers;
            public int Dimensions;
        }
    }
}
