using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Lay-in CAD adapter. All positions, grid phase, tee/ty/DIM quantities come
    /// exclusively from LayInRuntimePlanner; concealed-ceiling CAD paths are untouched.
    /// </summary>
    internal static class LayInCadRuntime
    {
        private const int TransientSubMode = 191;
        private const int MaxPreviewDrawables = 3500;
        private static readonly IntegerCollection Viewports = new IntegerCollection();
        private static readonly List<Drawable> Active = new List<Drawable>();
        private static readonly List<Tuple<Drawable, DateTime>> Retired = new List<Tuple<Drawable, DateTime>>();
        private static readonly List<Drawable> Quarantine = new List<Drawable>();
        private static bool Mutating;

        internal static void AbandonForDocumentTransition()
        {
            // Native graphics may still refer to the wrappers; never call TransientManager
            // or dispose during a document activation/destruction callback.
            Quarantine.AddRange(Active);
            Active.Clear();
        }

        internal static void ClearPreview()
        {
            if (Mutating) return;
            Mutating = true;
            try { ClearCore(); DrainRetired(); }
            finally { Mutating = false; }
        }

        private static void ClearCore()
        {
            if (Active.Count == 0) return;
            var manager = TransientManager.CurrentTransientManager;
            var survivors = new List<Drawable>();
            foreach (var drawable in Active)
            {
                bool removed;
                try { removed = manager.EraseTransient(drawable, Viewports); }
                catch { removed = false; }
                if (removed) Retired.Add(Tuple.Create(drawable, DateTime.UtcNow));
                else survivors.Add(drawable);
            }
            Active.Clear();
            Active.AddRange(survivors);
        }

        private static void DrainRetired()
        {
            var limit = DateTime.UtcNow.AddSeconds(-15);
            for (int i = Retired.Count - 1; i >= 0; i--)
            {
                if (Retired[i].Item2 > limit) continue;
                (Retired[i].Item1 as IDisposable)?.Dispose();
                Retired.RemoveAt(i);
            }
        }

        internal static void Preview()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || Mutating) return;
            Mutating = true;
            try
            {
                ClearCore();
                DrainRetired();
                if (Active.Count > 0)
                    throw new InvalidOperationException("Cannot safely release previous Lay-in Preview.");
                var session = VxtSession.Current;
                if (!session.HasBoundary) return;
                var plans = LayInRuntimePlanner.Build(session, session.LayInSettings);
                var total = plans.Sum(p => p.Plan.TeeSegments.Count +
                    p.Plan.HangerPoints.Count + p.Plan.DimensionRuns.Count + 1);
                if (total > MaxPreviewDrawables)
                    throw new InvalidOperationException("Lay-in Preview exceeds 3500 graphics; Create still uses full Core plan.");

                var db = doc.Database;
                foreach (var item in plans)
                {
                    foreach (var tee in item.Plan.TeeSegments)
                    {
                        var line = new Line(ToCad(tee.A), ToCad(tee.B));
                        line.SetDatabaseDefaults(db);
                        line.ColorIndex = tee.Kind == LayInTeeKind.MainTee ? (short)1 :
                            tee.Kind == LayInTeeKind.LongCrossTee ? (short)3 : (short)5;
                        AddTransient(line);
                    }
                    foreach (var hanger in item.Plan.HangerPoints)
                    {
                        var marker = new Circle(ToCad(hanger), Vector3d.ZAxis, 18.0);
                        marker.SetDatabaseDefaults(db);
                        marker.ColorIndex = 2;
                        AddTransient(marker);
                    }
                    foreach (var run in item.Plan.DimensionRuns)
                    {
                        var dimension = BuildDimension(db, run, item.Plan.ModuleShort);
                        dimension.ColorIndex = 4;
                        // A transient dimension has no database owner. GenerateLayout
                        // before AddTransient so the complete labeled DIM is visible.
                        dimension.GenerateLayout();
                        AddTransient(dimension);
                    }
                    var startMarker = new Circle(ToCad(item.Plan.HatchOrigin), Vector3d.ZAxis, 28.0);
                    startMarker.SetDatabaseDefaults(db);
                    startMarker.ColorIndex = 3;
                    AddTransient(startMarker);
                }
                int main, longCross, shortCross, hangers;
                double waste;
                LayInRuntimePlanner.Summarize(plans, out main, out longCross, out shortCross, out hangers, out waste);
                session.ViewModel?.LayIn?.SetPreviewStats(main, longCross, shortCross, hangers, waste);
            }
            catch (Exception ex)
            {
                ClearCore();
                VxtSession.Current.ViewModel?.LayIn?.SetPreviewError(ex.Message);
                doc.Editor.WriteMessage("\nHNL Tool - Lay-in Preview: " + ex.Message);
            }
            finally { Mutating = false; }
        }

        private static void AddTransient(Drawable drawable)
        {
            bool attached;
            try
            {
                attached = TransientManager.CurrentTransientManager.AddTransient(
                    drawable, TransientDrawingMode.DirectShortTerm, TransientSubMode, Viewports);
            }
            catch
            {
                (drawable as IDisposable)?.Dispose();
                throw;
            }
            if (!attached)
            {
                (drawable as IDisposable)?.Dispose();
                throw new InvalidOperationException("AutoCAD rejected Lay-in transient graphic.");
            }
            Active.Add(drawable);
        }

        internal static void Create()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var session = VxtSession.Current;
            if (!session.HasBoundary)
            {
                doc.Editor.WriteMessage("\nHNL Tool - Lay-in: No ceiling boundary selected.");
                return;
            }

            try
            {
                // Preview and Create both invoke the identical Core planner and phases.
                var settings = session.LayInSettings.Clone();
                var plans = LayInRuntimePlanner.Build(session, settings);
                ClearPreview();
                if (Active.Count > 0)
                    throw new InvalidOperationException("Previous preview graphics are still attached.");

                int hatchCount = 0, hangerCount = 0, dimensionCount = 0;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var hatchLayer = VxtCadResources.EnsureLayer(db, tr, settings.HatchLayer, 7, "Continuous", "25");
                    var hangerLayer = VxtCadResources.EnsureLayer(db, tr, settings.HangerLayer, 2, "Continuous", "25");
                    var dimLayer = VxtCadResources.EnsureLayer(db, tr, settings.DimensionLayer, 4, "Continuous", "25");
                    var markerLayer = VxtCadResources.EnsureLayer(db, tr, "HNL-CF-START", 3, "Continuous", "25");
                    var hangerBlock = ResolveBlock(bt, settings.HangerBlockName);
                    var startBlock = ResolveBlock(bt, settings.StartMarkerBlockName);

                    foreach (var item in plans)
                    {
                        // Exactly one Hatch is materialized for each ceiling region.
                        CreateHatch(db, tr, ms, item, hatchLayer);
                        hatchCount++;

                        foreach (var hanger in item.Plan.HangerPoints)
                        {
                            if (hangerBlock.IsNull)
                            {
                                var circle = new Circle(ToCad(hanger), Vector3d.ZAxis, 18.0);
                                Append(db, tr, ms, circle, hangerLayer);
                            }
                            else
                            {
                                var block = new BlockReference(ToCad(hanger), hangerBlock);
                                Append(db, tr, ms, block, hangerLayer);
                            }
                            hangerCount++;
                        }

                        foreach (var run in item.Plan.DimensionRuns)
                        {
                            var dimension = BuildDimension(db, run, item.Plan.ModuleShort);
                            Append(db, tr, ms, dimension, dimLayer);
                            dimensionCount++;
                        }

                        // Mark the exact phase origin used by the hatch and physical grid.
                        var origin = ToCad(item.Plan.HatchOrigin);
                        if (startBlock.IsNull)
                        {
                            var point = new Circle(origin, Vector3d.ZAxis, 28.0);
                            Append(db, tr, ms, point, markerLayer);
                        }
                        else
                        {
                            var marker = new BlockReference(origin, startBlock);
                            Append(db, tr, ms, marker, markerLayer);
                        }
                    }

                    if (hatchCount != plans.Count ||
                        hangerCount != plans.Sum(p => p.Plan.HangerPoints.Count) ||
                        dimensionCount != plans.Sum(p => p.Plan.DimensionRuns.Count))
                        throw new InvalidOperationException("Lay-in Preview/Create materialization count mismatch.");
                    tr.Commit();
                }
                doc.Editor.WriteMessage("\nHNL Tool - Lay-in Create: " +
                    hatchCount + " Hatch, " + hangerCount + " Ty, " +
                    dimensionCount + " DIM. One Hatch per region.");
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage("\nHNL Tool - Lay-in Create ROLLBACK: " + ex.Message);
                VxtSession.Current.ViewModel?.LayIn?.SetPreviewError(ex.Message);
            }
        }

        private static void CreateHatch(Database db, Transaction tr,
            BlockTableRecord ms, LayInBoundaryRuntimePlan item, ObjectId layerId)
        {
            var ids = new List<ObjectId>();
            var temporaryBoundaries = new List<Autodesk.AutoCAD.DatabaseServices.Polyline>();
            try
            {
                var allLoops = new List<Boundary2> { item.Boundary };
                if (item.Holes != null) allLoops.AddRange(item.Holes.Where(h => h != null));
                foreach (var loop in allLoops)
                {
                    var vertices = loop.Vertices;
                    if (vertices.Count < 3) throw new InvalidOperationException("Invalid Lay-in hatch boundary.");
                    var poly = new Autodesk.AutoCAD.DatabaseServices.Polyline(vertices.Count);
                    poly.SetDatabaseDefaults(db);
                    for (var i = 0; i < vertices.Count; i++)
                        poly.AddVertexAt(i, new Point2d(vertices[i].X, vertices[i].Y), 0, 0, 0);
                    poly.Closed = true;
                    ms.AppendEntity(poly);
                    tr.AddNewlyCreatedDBObject(poly, true);
                    ids.Add(poly.ObjectId);
                    temporaryBoundaries.Add(poly);
                }

                var hatch = new Hatch();
                Append(db, tr, ms, hatch, layerId);
                hatch.Associative = false;
                hatch.SetHatchPattern(HatchPatternType.CustomDefined, item.Plan.HatchPatternName);
                hatch.PatternScale = 1.0;
                hatch.PatternAngle = item.Plan.MainAngleRadians;
                hatch.Origin = new Point2d(item.Plan.HatchOrigin.X, item.Plan.HatchOrigin.Y);
                hatch.HatchStyle = HatchStyle.Normal;
                hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { ids[0] });
                for (var i = 1; i < ids.Count; i++)
                    hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection { ids[i] });
                hatch.EvaluateHatch(true);
                // Nonassociative Hatch keeps its own loops; do not leave auxiliary polylines.
                foreach (var poly in temporaryBoundaries) poly.Erase();
            }
            catch
            {
                // Outer transaction rolls back both the Hatch and temporary boundaries.
                throw;
            }
        }

        // Preview and Create must use the same DIM geometry, text and offset.
        private static RotatedDimension BuildDimension(
            Database db, LayInDimensionRun run, double moduleShort)
        {
            var a = ToCad(run.A);
            var b = ToCad(run.B);
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= 1e-6)
                throw new InvalidOperationException("Zero-length Lay-in dimension run.");

            var offset = Math.Max(160.0, moduleShort * 0.3);
            var linePoint = new Point3d(
                (a.X + b.X) * 0.5 + dy * offset / length,
                (a.Y + b.Y) * 0.5 - dx * offset / length, 0.0);
            var result = new RotatedDimension(
                Math.Atan2(dy, dx), a, b, linePoint, run.Label, db.Dimstyle);
            result.SetDatabaseDefaults(db);
            return result;
        }

        private static ObjectId ResolveBlock(BlockTable table, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return ObjectId.Null;
            if (!table.Has(name))
                throw new InvalidOperationException("Block not found in current DWG: " + name);
            return table[name];
        }

        private static void Append(Database db, Transaction tr, BlockTableRecord ms,
            Entity entity, ObjectId layer)
        {
            entity.SetDatabaseDefaults(db);
            VxtCadResources.ApplyByLayer(entity, layer);
            ms.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
        }

        internal static void PickPoint(bool door)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var result = doc.Editor.GetPoint("\nHNL Tool - Lay-in: Pick " +
                (door ? "door reference point" : "grid start point") + ": ");
            if (result.Status != PromptStatus.OK) return;
            var point = new HNL.VXT.Core.Geometry.Point2(result.Value.X, result.Value.Y);
            if (door)
            {
                VxtSession.Current.LayInSettings.DoorPoint = point;
                VxtSession.Current.ViewModel?.LayIn?.SetDoorPoint(point);
            }
            else
            {
                VxtSession.Current.LayInSettings.ManualStartPoint = point;
                VxtSession.Current.ViewModel?.LayIn?.SetManualStart(point);
            }
        }

        internal static void PickBlock(bool startMarker)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var option = new PromptEntityOptions("\nHNL Tool - Lay-in: Pick " +
                (startMarker ? "start-marker" : "hanger") + " block: ");
            option.SetRejectMessage("\nHNL Tool - Lay-in: Select a BlockReference.");
            option.AddAllowedClass(typeof(BlockReference), true);
            var result = doc.Editor.GetEntity(option);
            if (result.Status != PromptStatus.OK) return;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var br = tr.GetObject(result.ObjectId, OpenMode.ForRead) as BlockReference;
                if (br == null) return;
                var id = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                var definition = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord;
                if (definition == null || string.IsNullOrWhiteSpace(definition.Name)) return;
                if (startMarker)
                {
                    VxtSession.Current.LayInSettings.StartMarkerBlockName = definition.Name;
                    VxtSession.Current.ViewModel?.LayIn?.SetStartMarkerBlock(definition.Name);
                }
                else
                {
                    VxtSession.Current.LayInSettings.HangerBlockName = definition.Name;
                    VxtSession.Current.ViewModel?.LayIn?.SetHangerBlock(definition.Name);
                }
                tr.Commit();
                doc.Editor.WriteMessage("\nHNL Tool - Lay-in: Selected " + definition.Name);
            }
        }

        private static Point3d ToCad(HNL.VXT.Core.Geometry.Point2 p)
            => new Point3d(p.X, p.Y, 0.0);
    }
}
