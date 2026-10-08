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
        private const string DefaultStartTileBlockName = "HNL_CF_FIRST_TILE_V1";
        private static readonly IReadOnlyList<HNL.VXT.Core.Geometry.Point2[]> StartTileStrokes = MakeStartTileStrokes();

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
                    p.Plan.HangerPoints.Count + p.Plan.DimensionRuns.Count +
                    (session.LayInSettings.DrawStartTileBlock && p.Plan.FirstTileOrigin.HasValue
                        ? (string.IsNullOrWhiteSpace(session.LayInSettings.StartMarkerBlockName)
                            ? StartTileStrokes.Count : 1) : 0));
                if (total > MaxPreviewDrawables)
                    throw new InvalidOperationException("Lay-in Preview exceeds 3500 graphics; Create still uses full Core plan.");

                var db = doc.Database;
                var dimStyleId = ResolveDimensionStyle(db, session.LayInSettings.DimensionStyle);
                ObjectId previewHangerBlock;
                using (var readTr = db.TransactionManager.StartTransaction())
                {
                    var blockTable = (BlockTable)readTr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    previewHangerBlock = session.LayInSettings.DrawHangers
                        ? ResolveHangerBlock(blockTable, session.LayInSettings.HangerBlockName)
                        : ObjectId.Null;
                }
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
                        Entity marker = previewHangerBlock.IsNull
                            ? (Entity)new Circle(ToCad(hanger), Vector3d.ZAxis, 18.0)
                            : new BlockReference(ToCad(hanger), previewHangerBlock);
                        marker.SetDatabaseDefaults(db);
                        marker.ColorIndex = 2;
                        AddTransient(marker);
                    }
                    foreach (var run in item.Plan.DimensionRuns)
                    {
                        var dimension = BuildDimension(db, run, item.Plan.ModuleShort, dimStyleId);
                        dimension.ColorIndex = 4;
                        // A transient dimension has no database owner. GenerateLayout
                        // before AddTransient so the complete labeled DIM is visible.
                        dimension.GenerateLayout();
                        AddTransient(dimension);
                    }
                    if (session.LayInSettings.DrawStartTileBlock &&
                        item.Plan.FirstTileOrigin.HasValue)
                        PreviewStartTile(db, item.Plan, session.LayInSettings);
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

                int hatchCount = 0, hangerCount = 0, dimensionCount = 0, startTileCount = 0;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var hatchLayer = VxtCadResources.EnsureLayInLayer(db, tr, settings.HatchLayer, 7, "Continuous", "25");
                    var hangerLayer = settings.DrawHangers
                        ? VxtCadResources.EnsureLayInLayer(db, tr, settings.HangerLayer, 2, "Continuous", "25")
                        : ObjectId.Null;
                    var dimLayer = settings.DimensionMode != LayInDimensionMode.Off
                        ? VxtCadResources.EnsureLayInLayer(db, tr, settings.DimensionLayer, 4, "Continuous", "25")
                        : ObjectId.Null;
                    var markerLayer = settings.DrawStartTileBlock
                        ? VxtCadResources.EnsureLayInLayer(db, tr, "HNL-CF-START", 3, "Continuous", "25")
                        : ObjectId.Null;
                    var hangerBlock = settings.DrawHangers
                        ? ResolveHangerBlock(bt, settings.HangerBlockName) : ObjectId.Null;
                    var dimStyleId = ResolveDimensionStyle(db, tr, settings.DimensionStyle);
                    var startBlock = settings.DrawStartTileBlock
                        ? (string.IsNullOrWhiteSpace(settings.StartMarkerBlockName)
                            ? EnsureDefaultStartTileBlock(db, tr)
                            : ResolveBlock(bt, settings.StartMarkerBlockName))
                        : ObjectId.Null;

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
                            var dimension = BuildDimension(db, run, item.Plan.ModuleShort, dimStyleId);
                            Append(db, tr, ms, dimension, dimLayer);
                            dimensionCount++;
                        }

                        // One physical FIRST TILE symbol per region; always anchor at the
                        // valid full panel chosen by the Core planner, not an outside phase.
                        if (settings.DrawStartTileBlock && item.Plan.FirstTileOrigin.HasValue)
                        {
                            var marker = MakeStartTileReference(db, tr, item.Plan,
                                startBlock,
                                string.IsNullOrWhiteSpace(settings.StartMarkerBlockName));
                            Append(db, tr, ms, marker, markerLayer);
                            startTileCount++;
                        }
                    }

                    if (hatchCount != plans.Count ||
                        hangerCount != plans.Sum(p => p.Plan.HangerPoints.Count) ||
                        dimensionCount != plans.Sum(p => p.Plan.DimensionRuns.Count) ||
                        startTileCount != plans.Count(p =>
                            settings.DrawStartTileBlock && p.Plan.FirstTileOrigin.HasValue))
                        throw new InvalidOperationException("Lay-in Preview/Create materialization count mismatch.");
                    tr.Commit();
                }
                doc.Editor.WriteMessage("\nHNL Tool - Lay-in Create: " +
                    hatchCount + " Hatch, " + hangerCount + " Ty, " +
                    dimensionCount + " DIM, " + startTileCount +
                    " Block o tran xuat phat. One Hatch per region.");
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage("\nHNL Tool - Lay-in Create ROLLBACK: " + ex.Message);
                VxtSession.Current.ViewModel?.LayIn?.SetPreviewError(ex.Message);
            }
        }

        private static Hatch CreateHatch(Database db, Transaction tr,
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
                return hatch;
            }
            catch
            {
                // Outer transaction rolls back both the Hatch and temporary boundaries.
                throw;
            }
        }

        private static IReadOnlyList<HNL.VXT.Core.Geometry.Point2[]> MakeStartTileStrokes()
        {
            // Normalized from tn.dxf: one tile border, diagonals, directions
            // toward top/right and a small center ring. Scale 1 x 1 to the
            // actual module, e.g. 610 x 610 instead of 305 x 2.
            HNL.VXT.Core.Geometry.Point2 P(double x, double y)
                => new HNL.VXT.Core.Geometry.Point2(x, y);
            var ring = Enumerable.Range(0, 17).Select(i =>
                P(0.5 + Math.Cos(i * Math.PI / 8) * 0.0347,
                  0.5 + Math.Sin(i * Math.PI / 8) * 0.0347)).ToArray();
            return new[]
            {
                new[] { P(0,0), P(1,0), P(1,1), P(0,1), P(0,0) },
                new[] { P(0,0), P(1,1) },
                new[] { P(1,0), P(0,1) },
                new[] { P(1,0.5), P(0.5,0.5), P(0.5,1) },
                new[] { P(0.928,0.38), P(1,0.5), P(0.928,0.62) },
                new[] { P(0.38,0.928), P(0.5,1), P(0.62,0.928) },
                ring
            };
        }

        private static Autodesk.AutoCAD.DatabaseServices.Polyline MakeSymbolPolyline(
            IReadOnlyList<HNL.VXT.Core.Geometry.Point2> points)
        {
            var line = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            for (int i = 0; i < points.Count; i++)
                line.AddVertexAt(i, new Point2d(points[i].X, points[i].Y),
                    0.0, 0.0, 0.0);
            return line;
        }

        private static ObjectId EnsureDefaultStartTileBlock(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(DefaultStartTileBlockName))
                return bt[DefaultStartTileBlockName];

            bt.UpgradeOpen();
            var definition = new BlockTableRecord
            {
                Name = DefaultStartTileBlockName,
                Origin = Point3d.Origin
            };
            var id = bt.Add(definition);
            tr.AddNewlyCreatedDBObject(definition, true);
            foreach (var path in StartTileStrokes)
            {
                var stroke = MakeSymbolPolyline(path);
                stroke.SetDatabaseDefaults(db);
                stroke.ColorIndex = 0; // ByBlock; inherits HNL-CF-START layer.
                definition.AppendEntity(stroke);
                tr.AddNewlyCreatedDBObject(stroke, true);
            }
            return id;
        }

        private static BlockReference MakeStartTileReference(
            Database db, Transaction tr, LayInCeilingPlan plan,
            ObjectId definitionId, bool builtin)
        {
            double minX = 0.0, minY = 0.0, sourceWidth = 1.0, sourceHeight = 1.0;
            if (!builtin)
            {
                var definition = (BlockTableRecord)tr.GetObject(
                    definitionId, OpenMode.ForRead);
                bool hasExtents = false;
                double maxX = 0.0, maxY = 0.0;
                foreach (ObjectId id in definition)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;
                    try
                    {
                        var extent = entity.GeometricExtents;
                        if (!hasExtents)
                        {
                            minX = extent.MinPoint.X;
                            minY = extent.MinPoint.Y;
                            maxX = extent.MaxPoint.X;
                            maxY = extent.MaxPoint.Y;
                            hasExtents = true;
                        }
                        else
                        {
                            minX = Math.Min(minX, extent.MinPoint.X);
                            minY = Math.Min(minY, extent.MinPoint.Y);
                            maxX = Math.Max(maxX, extent.MaxPoint.X);
                            maxY = Math.Max(maxY, extent.MaxPoint.Y);
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        // Dimension/Attribute entities may not expose usable extents.
                    }
                }
                if (hasExtents && maxX - minX > 1e-5 && maxY - minY > 1e-5)
                {
                    sourceWidth = maxX - minX;
                    sourceHeight = maxY - minY;
                }
                else
                {
                    // Do not resize degenerate arbitrary custom annotations.
                    minX = 0.0;
                    minY = 0.0;
                    sourceWidth = plan.FirstTileWidth;
                    sourceHeight = plan.FirstTileHeight;
                }
            }

            var scaleX = plan.FirstTileWidth / sourceWidth;
            var scaleY = plan.FirstTileHeight / sourceHeight;
            var angle = plan.MainAngleRadians;
            var anchor = plan.FirstTileOrigin.Value;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            // Nonzero base-extents from custom blocks are translated to the
            // exact first panel corner before rotating the BlockReference.
            var insert = new Point3d(
                anchor.X - (minX * scaleX * cos - minY * scaleY * sin),
                anchor.Y - (minX * scaleX * sin + minY * scaleY * cos), 0.0);
            return new BlockReference(insert, definitionId)
            {
                Rotation = angle,
                ScaleFactors = new Scale3d(scaleX, scaleY, 1.0)
            };
        }

        private static void PreviewStartTile(
            Database db, LayInCeilingPlan plan, LayInCeilingSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.StartMarkerBlockName))
            {
                // Render the user's actual selected BlockRef, with identical scale
                // and rotation to Create. Never make a temporary DB block here.
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var id = ResolveBlock(bt, settings.StartMarkerBlockName);
                    var marker = MakeStartTileReference(db, tr, plan, id, false);
                    marker.SetDatabaseDefaults(db);
                    marker.ColorIndex = 3;
                    AddTransient(marker);
                }
                return;
            }
            var anchor = plan.FirstTileOrigin.Value;
            var cos = Math.Cos(plan.MainAngleRadians);
            var sin = Math.Sin(plan.MainAngleRadians);
            foreach (var path in StartTileStrokes)
            {
                var vertices = path.Select(p =>
                    new HNL.VXT.Core.Geometry.Point2(
                        anchor.X + cos * p.X * plan.FirstTileWidth -
                            sin * p.Y * plan.FirstTileHeight,
                        anchor.Y + sin * p.X * plan.FirstTileWidth +
                            cos * p.Y * plan.FirstTileHeight)).ToList();
                var line = MakeSymbolPolyline(vertices);
                line.SetDatabaseDefaults(db);
                line.ColorIndex = 3;
                AddTransient(line);
            }
        }

        // Explicit field test: runs inside AutoCAD and always rolls back all test entities.
        // No palette/session settings, DWG selection, or Golden solver are changed.
        internal static void RunRuntimeQa()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            int passed = 0, failed = 0;
            var gridSystems = new[]
            {
                LayInGridSystem.Module600x600, LayInGridSystem.Module610x610,
                LayInGridSystem.Module600x1200, LayInGridSystem.Module610x1220
            };

            ed.WriteMessage("\nHNL Tool - Lay-in Runtime QA: 4 grids x 4 geometries; test entities will be rolled back.");
            foreach (var grid in gridSystems)
            {
                for (int scenario = 0; scenario < 4; scenario++)
                {
                    string caseName = grid + " / " + new[] { "Rectangle", "Hole", "Rotated30", "Concave" }[scenario];
                    try
                    {
                        var outer = scenario == 3
                            ? new Boundary2(new[]
                            {
                                new HNL.VXT.Core.Geometry.Point2(0, 0),
                                new HNL.VXT.Core.Geometry.Point2(6500, 0),
                                new HNL.VXT.Core.Geometry.Point2(6500, 1400),
                                new HNL.VXT.Core.Geometry.Point2(4900, 1400),
                                new HNL.VXT.Core.Geometry.Point2(4900, 2900),
                                new HNL.VXT.Core.Geometry.Point2(6500, 2900),
                                new HNL.VXT.Core.Geometry.Point2(6500, 5000),
                                new HNL.VXT.Core.Geometry.Point2(0, 5000)
                            })
                            : QaRectangle(0, 0, 6500, 5000);
                        if (scenario == 2) outer = QaRotate(outer, Math.PI / 6.0);

                        var holes = scenario == 1
                            ? new List<Boundary2> { QaRectangle(2300, 1700, 3500, 2900) }
                            : new List<Boundary2>();
                        var settings = new LayInCeilingSettings
                        {
                            GridSystem = grid,
                            MainDirection = scenario == 2
                                ? LayInMainDirectionMode.ParallelLongSide
                                : scenario == 3 ? LayInMainDirectionMode.Vertical
                                : LayInMainDirectionMode.Horizontal,
                            StartMode = scenario == 2 ? LayInStartMode.ManualStart
                                : scenario == 3 ? LayInStartMode.FromDoor : LayInStartMode.Balanced,
                            ManualStartPoint = new HNL.VXT.Core.Geometry.Point2(140, 100),
                            DoorPoint = new HNL.VXT.Core.Geometry.Point2(45, 60),
                            DrawHangers = true,
                            DimensionMode = LayInDimensionMode.Grouped,
                            GroupedDimensionCount = 3
                        };
                        var plan = LayInCeilingPlanner.Build(outer, holes, settings);
                        var item = new LayInBoundaryRuntimePlan
                        {
                            Boundary = outer, Holes = holes, Plan = plan
                        };

                        // The Hatch and sampled DIM/Hanger entities use the very same
                        // materialization helpers as Create(). Never Commit this transaction.
                        using (var tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            var db = doc.Database;
                            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            // Verify Lay-in does not restyle an existing drawing layer.
                            var existing = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
                            var colorIndex = existing.Color.ColorIndex;
                            var lineWeight = existing.LineWeight;
                            var lineTypeId = existing.LinetypeObjectId;
                            var reusedId = VxtCadResources.EnsureLayInLayer(
                                db, tr, existing.Name, 1, "Continuous", "50");
                            if (reusedId != db.Clayer ||
                                existing.Color.ColorIndex != colorIndex ||
                                existing.LineWeight != lineWeight ||
                                existing.LinetypeObjectId != lineTypeId)
                                throw new InvalidOperationException("Lay-in changed existing DWG layer styling.");

                            var hatch = CreateHatch(db, tr, ms, item, db.Clayer);
                            if (hatch.IsErased || hatch.NumberOfLoops != 1 + holes.Count)
                                throw new InvalidOperationException("Hatch missing or wrong number of boundary loops.");
                            if (!string.Equals(hatch.PatternName, plan.HatchPatternName, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Actual Hatch pattern differs from Core plan.");
                            if (Math.Abs(hatch.PatternAngle - plan.MainAngleRadians) > 1e-6)
                                throw new InvalidOperationException("Actual Hatch angle differs from Core plan.");
                            var origin = hatch.Origin;
                            if (Math.Abs(origin.X - plan.HatchOrigin.X) > 1e-5 ||
                                Math.Abs(origin.Y - plan.HatchOrigin.Y) > 1e-5)
                                throw new InvalidOperationException("Actual Hatch origin differs from Core plan.");

                            if (plan.DimensionRuns.Count == 0 || plan.HangerPoints.Count == 0)
                                throw new InvalidOperationException("Core plan did not create test DIM/Ty.");
                            var dimension = BuildDimension(db, plan.DimensionRuns[0], plan.ModuleShort, db.Dimstyle);
                            Append(db, tr, ms, dimension, db.Clayer);
                            if (dimension.ObjectId.IsNull)
                                throw new InvalidOperationException("DIM was not materialized.");
                            var hanger = new Circle(ToCad(plan.HangerPoints[0]), Vector3d.ZAxis, 18.0);
                            Append(db, tr, ms, hanger, db.Clayer);
                            if (hanger.ObjectId.IsNull)
                                throw new InvalidOperationException("Ty marker was not materialized.");
                            if (plan.FirstTileOrigin.HasValue)
                            {
                                var symbolId = EnsureDefaultStartTileBlock(db, tr);
                                var marker = new BlockReference(
                                    ToCad(plan.FirstTileOrigin.Value), symbolId)
                                {
                                    Rotation = plan.MainAngleRadians,
                                    ScaleFactors = new Scale3d(
                                        plan.FirstTileWidth, plan.FirstTileHeight, 1.0)
                                };
                                Append(db, tr, ms, marker, db.Clayer);
                                if (marker.ObjectId.IsNull ||
                                    marker.BlockTableRecord != symbolId ||
                                    Math.Abs(marker.ScaleFactors.X - plan.FirstTileWidth) > 1e-6 ||
                                    Math.Abs(marker.ScaleFactors.Y - plan.FirstTileHeight) > 1e-6)
                                    throw new InvalidOperationException(
                                        "First-tile Block insertion or module scale is invalid.");
                            }
                            // Deliberately no Commit: closing the transaction removes every
                            // QA Hatch, boundary, DIM and Ty marker from the user's drawing.
                        }
                        passed++;
                        ed.WriteMessage("\nHNL Tool - Lay-in QA PASS: " + caseName);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        ed.WriteMessage("\nHNL Tool - Lay-in QA FAIL: " + caseName + " | " + ex.Message);
                    }
                }
            }
            ed.WriteMessage("\nHNL Tool - Lay-in Runtime QA: PASS=" + passed +
                " FAIL=" + failed + " TOTAL=16; all test transactions rolled back.");
        }

        private static Boundary2 QaRectangle(double x0, double y0, double x1, double y1)
        {
            return new Boundary2(new[]
            {
                new HNL.VXT.Core.Geometry.Point2(x0, y0),
                new HNL.VXT.Core.Geometry.Point2(x1, y0),
                new HNL.VXT.Core.Geometry.Point2(x1, y1),
                new HNL.VXT.Core.Geometry.Point2(x0, y1)
            });
        }

        private static Boundary2 QaRotate(Boundary2 boundary, double radians)
        {
            double cosine = Math.Cos(radians), sine = Math.Sin(radians);
            return new Boundary2(boundary.Vertices.Select(p =>
                new HNL.VXT.Core.Geometry.Point2(
                    p.X * cosine - p.Y * sine, p.X * sine + p.Y * cosine)));
        }

        // Preview and Create must use the same DIM geometry, text and offset.
        private static RotatedDimension BuildDimension(
            Database db, LayInDimensionRun run, double moduleShort, ObjectId dimStyleId)
        {
            var a = ToCad(run.A);
            var b = ToCad(run.B);
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= 1e-6)
                throw new InvalidOperationException("Zero-length Lay-in dimension run.");

            var offset = Math.Max(160.0, moduleShort * 0.3);
            var linePoint = run.DimensionLinePoint.HasValue
                ? ToCad(run.DimensionLinePoint.Value)
                : new Point3d(
                    (a.X + b.X) * 0.5 + dy * offset / length,
                    (a.Y + b.Y) * 0.5 - dx * offset / length, 0.0);
            var result = new RotatedDimension(
                Math.Atan2(dy, dx), a, b, linePoint, run.Label, dimStyleId);
            result.SetDatabaseDefaults(db);
            return result;
        }

        private static ObjectId ResolveDimensionStyle(
            Database db, string dimStyleName)
        {
            if (string.IsNullOrWhiteSpace(dimStyleName)) return db.Dimstyle;
            using (var tr = db.TransactionManager.StartTransaction())
                return ResolveDimensionStyle(db, tr, dimStyleName);
        }

        private static ObjectId ResolveDimensionStyle(
            Database db, Transaction tr, string dimStyleName)
        {
            if (string.IsNullOrWhiteSpace(dimStyleName)) return db.Dimstyle;
            var table = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (!table.Has(dimStyleName))
                throw new InvalidOperationException(
                    "HNL Tool - DimStyle not found in current DWG: " + dimStyleName);
            return table[dimStyleName];
        }

        private static ObjectId ResolveHangerBlock(BlockTable table, string name)
        {
            // Same preferred named Ty symbol as the concealed ceiling.
            // New/empty DWG drawings remain usable without bundled user blocks.
            if (string.IsNullOrWhiteSpace(name)) return ObjectId.Null;
            if (!table.Has(name) &&
                string.Equals(name, LayInCeilingSettings.DefaultHangerBlockName,
                    StringComparison.OrdinalIgnoreCase))
                return ObjectId.Null;
            return ResolveBlock(table, name);
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

        // Like the concealed tab's Pick DIM control: choose a point OUTSIDE
        // the layout to set side + distance in the rotated local grid axes.
        internal static void PickDimensionLocation(bool horizontal)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var session = VxtSession.Current;
            if (!session.HasBoundary) return;
            var settings = session.LayInSettings;
            var plans = LayInRuntimePlanner.Build(session, settings);
            if (plans.Count == 0) return;

            var first = plans[0];
            var angle = first.Plan.MainAngleRadians;
            var bounds = Box2.FromPoints(first.Boundary.Vertices
                .Select(p => Transform2.ToLocal(p, angle)));

            var options = new PromptPointOptions(horizontal
                ? "\nHNL Tool - Lay-in: Chon diem dat DIM ngang (phia tren/duoi khung): "
                : "\nHNL Tool - Lay-in: Chon diem dat DIM doc (ben trai/phai khung): ");
            var result = doc.Editor.GetPoint(options);
            if (result.Status != PromptStatus.OK) return;

            var local = Transform2.ToLocal(
                new HNL.VXT.Core.Geometry.Point2(result.Value.X, result.Value.Y), angle);
            bool farSide;
            double distance;
            if (horizontal)
            {
                if (local.Y >= bounds.MinY && local.Y <= bounds.MaxY)
                {
                    doc.Editor.WriteMessage("\nHNL Tool - Lay-in: Vi tri DIM phai nam ngoai bien tran.");
                    return;
                }
                farSide = local.Y > bounds.MaxY;
                distance = farSide ? local.Y - bounds.MaxY : bounds.MinY - local.Y;
                settings.HorizontalDimSide = farSide ? LayInHorizontalDimSide.Top : LayInHorizontalDimSide.Bottom;
                settings.HorizontalDimDistance = distance;
            }
            else
            {
                if (local.X >= bounds.MinX && local.X <= bounds.MaxX)
                {
                    doc.Editor.WriteMessage("\nHNL Tool - Lay-in: Vi tri DIM phai nam ngoai bien tran.");
                    return;
                }
                farSide = local.X > bounds.MaxX;
                distance = farSide ? local.X - bounds.MaxX : bounds.MinX - local.X;
                settings.VerticalDimSide = farSide ? LayInVerticalDimSide.Right : LayInVerticalDimSide.Left;
                settings.VerticalDimDistance = distance;
            }
            session.ViewModel?.LayIn?.SetPickedDimensionPosition(horizontal, farSide, distance);
            doc.Editor.WriteMessage("\nHNL Tool - Lay-in: Da cap nhat vi tri DIM.");
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
                // First-tile art must match the actual selected dynamic/anonymous
                // geometry (tn.dxf uses *U6 at 2x), not the unstretched parent.
                var id = startMarker ? br.BlockTableRecord :
                    (br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord);
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
