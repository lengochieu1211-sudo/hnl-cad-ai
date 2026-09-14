using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Lightweight WYSIWYG geometry renderer. Preview and Create consume the exact same
    /// Core plan, but Preview deliberately draws XC/XP as lines and Ty as one circle each
    /// instead of instantiating hundreds of dynamic BlockReferences. This keeps large
    /// 300-500+ m2 ceilings responsive and avoids release-specific transient dynamic-block
    /// stretch issues that could visually collapse many XC members into one.
    /// </summary>
    internal sealed class VxtTransientPreview
    {
        private sealed class RetiredDrawable
        {
            public RetiredDrawable(Drawable drawable, DateTime retiredUtc)
            {
                Drawable = drawable;
                RetiredUtc = retiredUtc;
            }

            public Drawable Drawable { get; }
            public DateTime RetiredUtc { get; }
        }

        public static VxtTransientPreview Instance { get; } = new VxtTransientPreview();

        private readonly List<Drawable> _drawables = new List<Drawable>();
        private readonly List<RetiredDrawable> _retiredDrawables = new List<RetiredDrawable>();
        private readonly List<Drawable> _documentTransitionQuarantine = new List<Drawable>();
        private readonly IntegerCollection _viewports = new IntegerCollection();
        private const int SubDrawingMode = 190;
        private const int FullPreviewDrawableLimit = 3000;
        private static readonly TimeSpan RetiredDrawableGrace = TimeSpan.FromSeconds(15.0);
        private bool _isMutating;
        private bool _clearRequested;
        private bool _abandonRequested;
        private bool _largePreviewNoticeShown;

        internal int TrackedDrawableCount => _drawables.Count;
        internal int RetiredDrawableCount => _retiredDrawables.Count;
        internal int TransitionQuarantineCount => _documentTransitionQuarantine.Count;

        public void Refresh()
        {
            if (_isMutating)
            {
                _clearRequested = true;
                return;
            }

            _isMutating = true;
            try
            {
                // AutoCAD may release erased transient graphics asynchronously. Do not destroy the
                // managed/native wrapper immediately after EraseTransient reports success. A short
                // quarantine avoids a use-after-free during the next native redraw/paste/zoom.
                DrainRetiredDrawables();
                ClearCore();

                // If AutoCAD could not detach an older transient, keep its managed wrapper alive.
                // Disposing a drawable while the graphics system still references it can leave a
                // native dangling pointer and later crash during pan/zoom/redraw. Do not overlay a
                // new preview until the old graphics have actually been erased.
                if (_drawables.Count > 0)
                {
                    VxtSession.Current.ViewModel?.SetPreviewError(
                        "Preview cũ chưa giải phóng an toàn; HNL Tool đã hoãn vẽ lại để bảo vệ AutoCAD.");
                    return;
                }

                var session = VxtSession.Current;
                if (!session.HasBoundary) return;

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                try
                {
                    var settings = session.Settings;
                    var db = doc.Database;
                    VxtPreviewPlan plan;

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var context = VxtLayoutContextFactory.Build(session, tr);
                        plan = VxtMultiBoundaryPlanBuilder.Build(session.Boundaries, settings, context);

                        var layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                        var linetypeTable = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                        var dimStyleTable = tr.GetObject(db.DimStyleTableId, OpenMode.ForRead) as DimStyleTable;

                        var estimatedDrawables = EstimateFullPreviewDrawableCount(plan, settings);
                        var structuralOnly = estimatedDrawables > FullPreviewDrawableLimit;

                        RenderStructuralLines(plan, settings, db, layerTable, linetypeTable);

                        if (!structuralOnly)
                        {
                            RenderHangers(plan, settings, db, layerTable, linetypeTable);
                            RenderDimensions(plan, settings, db, dimStyleTable, layerTable, linetypeTable);
                            RenderGuides(plan, db);
                            _largePreviewNoticeShown = false;
                        }
                        else if (!_largePreviewNoticeShown)
                        {
                            _largePreviewNoticeShown = true;
                            doc.Editor.WriteMessage(
                                "\nHNL Tool - VXT Pro Preview: Bản vẽ lớn; chế độ an toàn chỉ hiển thị XC/XP. Ty/DIM vẫn được tính đầy đủ và sẽ tạo đúng khi bấm Tạo khung xương trần.");
                        }

                        // Preview is a strict read-only DB operation. Disposing an uncommitted read
                        // transaction guarantees no helper can accidentally persist drawing changes.
                    }

                    // Measure the exact final post-processed geometry shared with Create. Do not use
                    // builder bookkeeping counters here because concave split/merge and MEP finalizers
                    // may change the actual entity set after those counters were first populated.
                    session.ViewModel?.SetPreviewActualStats(VxtFinalPlanMetrics.FromPlan(plan));
                }
                catch (System.Exception ex)
                {
                    session.ViewModel?.SetPreviewError(ex.Message);
                    doc.Editor.WriteMessage("\nHNL Tool - VXT Pro Preview: " + ex.Message);
                }
            }
            finally
            {
                _isMutating = false;
                if (_abandonRequested)
                {
                    _abandonRequested = false;
                    AbandonForDocumentTransition();
                }
                else if (_clearRequested)
                {
                    _clearRequested = false;
                    Clear();
                }
            }
        }

        public void Clear()
        {
            if (_isMutating)
            {
                _clearRequested = true;
                return;
            }

            _isMutating = true;
            try
            {
                DrainRetiredDrawables();
                ClearCore();
            }
            finally
            {
                _isMutating = false;
                if (_abandonRequested)
                {
                    _abandonRequested = false;
                    AbandonForDocumentTransition();
                }
            }
        }

        /// <summary>
        /// DocumentActivated/DocumentToBeDestroyed are native document-transition callbacks.
        /// Never call TransientManager from those callbacks. Keep every wrapper alive and let
        /// AutoCAD tear down the old viewport/document graphics on its own.
        /// </summary>
        internal void AbandonForDocumentTransition()
        {
            if (_isMutating)
            {
                _abandonRequested = true;
                return;
            }

            if (_drawables.Count > 0)
            {
                _documentTransitionQuarantine.AddRange(_drawables);
                _drawables.Clear();
            }

            _clearRequested = false;
        }

        private void ClearCore()
        {
            if (_drawables.Count == 0) return;

            var manager = TransientManager.CurrentTransientManager;
            var allErased = false;
            try
            {
                allErased = manager.EraseTransients(
                    TransientDrawingMode.DirectShortTerm, SubDrawingMode, _viewports);
            }
            catch
            {
                allErased = false;
            }

            if (allErased)
            {
                foreach (var drawable in _drawables) RetireDrawable(drawable);
                _drawables.Clear();
                return;
            }

            // Bulk erase can fail during a viewport/document state transition. Retry each drawable
            // individually. Successful erases enter a grace-period quarantine instead of being
            // disposed immediately; failed erases remain strongly referenced as active survivors.
            var survivors = new List<Drawable>();
            foreach (var drawable in _drawables)
            {
                var erased = false;
                try { erased = manager.EraseTransient(drawable, _viewports); }
                catch { erased = false; }

                if (erased) RetireDrawable(drawable);
                else survivors.Add(drawable);
            }

            _drawables.Clear();
            _drawables.AddRange(survivors);
        }

        private static int EstimateFullPreviewDrawableCount(VxtPreviewPlan plan, VxtSettings settings)
        {
            if (plan == null) return 0;
            var count = plan.Lines.Count + plan.Texts.Count;
            if (settings.DrawHangers) count += plan.HangerPoints.Count;
            if (settings.AutoDimension) count += plan.Dimensions.Count;
            return count;
        }

        private void RetireDrawable(Drawable drawable)
        {
            if (drawable == null) return;
            _retiredDrawables.Add(new RetiredDrawable(drawable, DateTime.UtcNow));
        }

        private void DrainRetiredDrawables()
        {
            if (_retiredDrawables.Count == 0) return;
            var cutoff = DateTime.UtcNow - RetiredDrawableGrace;

            for (var i = _retiredDrawables.Count - 1; i >= 0; i--)
            {
                var retired = _retiredDrawables[i];
                if (retired.RetiredUtc > cutoff) continue;
                DisposeDrawable(retired.Drawable);
                _retiredDrawables.RemoveAt(i);
            }
        }

        private void RenderStructuralLines(
            VxtPreviewPlan plan,
            VxtSettings settings,
            Database db,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            foreach (var item in plan.Lines)
            {
                if (item.Kind == PreviewLineKind.Main)
                {
                    AddStyledLine(item, settings.MainLayer, settings.MainColorIndex,
                        settings.MainLinetype, settings.MainLineweight, db, layerTable, linetypeTable);
                }
                else if (item.Kind == PreviewLineKind.Furring)
                {
                    AddStyledLine(item, settings.FurringLayer, settings.FurringColorIndex,
                        settings.FurringLinetype, settings.FurringLineweight, db, layerTable, linetypeTable);
                }
            }
        }

        private void RenderHangers(
            VxtPreviewPlan plan,
            VxtSettings settings,
            Database db,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            if (!settings.DrawHangers) return;

            // One lightweight marker per Ty. Using the actual Ty Block in transient preview
            // created hundreds of dynamic block evaluations on large ceilings and was the
            // biggest source of palette lag. Create still inserts the exact selected Ty Block.
            foreach (var point in plan.HangerPoints)
            {
                var marker = new Circle(ToPoint3d(point), Vector3d.ZAxis, 38.0);
                marker.SetDatabaseDefaults(db);
                ApplyAppearance(marker, settings.HangerLayer, settings.HangerColorIndex,
                    settings.HangerLinetype, settings.HangerLineweight, layerTable, linetypeTable);
                AddDrawable(marker);
            }
        }

        private void RenderDimensions(
            VxtPreviewPlan plan,
            VxtSettings settings,
            Database db,
            DimStyleTable dimStyleTable,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            if (!settings.AutoDimension || plan.Dimensions.Count == 0) return;
            var dimStyleId = ResolveDimStyle(settings.DimensionStyle, db, dimStyleTable);

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
                ApplyAppearance(dim, settings.DimensionLayer, settings.DimensionColorIndex,
                    settings.DimensionLinetype, settings.DimensionLineweight, layerTable, linetypeTable);
                AddDrawable(dim);
            }
        }

        private void RenderGuides(VxtPreviewPlan plan, Database db)
        {
            foreach (var item in plan.Lines)
            {
                if (item.Kind == PreviewLineKind.Main || item.Kind == PreviewLineKind.Furring || item.Kind == PreviewLineKind.Hanger)
                    continue;
                AddGuideLine(item, db);
            }
            foreach (var text in plan.Texts) AddGuideText(text, db);
        }

        /// <summary>
        /// Backward-compatible entry point used by older Create code paths. New Create paths
        /// call VxtDynamicBlockAdapter with the effective block name so the selected property
        /// can be cached and Array/Spacing properties are never modified.
        /// </summary>
        internal static void TryApplyDynamicLength(BlockReference br, double length)
        {
            VxtDynamicBlockAdapter.ApplyMemberLength(br, string.Empty, length);
        }

        internal static bool ShouldSkipDynamicProperty(string name)
            => HNL.VXT.Core.Layout.DynamicBlockPropertyPolicy.IsRisky(name);

        private void AddStyledLine(
            PreviewLine item,
            string layerName,
            short colorIndex,
            string linetypeName,
            string lineweightText,
            Database db,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            var line = new Line(ToPoint3d(item.A), ToPoint3d(item.B));
            line.SetDatabaseDefaults(db);
            ApplyAppearance(line, layerName, colorIndex, linetypeName, lineweightText, layerTable, linetypeTable);
            AddDrawable(line);
        }

        private void AddGuideLine(PreviewLine item, Database db)
        {
            var line = new Line(ToPoint3d(item.A), ToPoint3d(item.B));
            line.SetDatabaseDefaults(db);
            line.Color = Color.FromColorIndex(ColorMethod.ByAci, GuideColorIndex(item.Kind));
            AddDrawable(line);
        }

        private void AddGuideText(PreviewText item, Database db)
        {
            var boundary = VxtSession.Current.Boundary;
            if (boundary == null) return;
            var bounds = boundary.GetBounds();
            var diag = bounds.Min.DistanceTo(bounds.Max);
            var text = new DBText
            {
                Position = ToPoint3d(item.Position),
                Height = Math.Max(60.0, Math.Min(180.0, diag * 0.015)),
                TextString = item.Text,
                Rotation = item.RotationRadians,
                Color = Color.FromColorIndex(ColorMethod.ByAci, GuideColorIndex(item.Kind))
            };
            text.SetDatabaseDefaults(db);
            AddDrawable(text);
        }

        internal static void ApplyAppearance(
            Entity entity,
            string layerName,
            short colorIndex,
            string linetypeName,
            string lineweightText,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            if (layerTable != null && !string.IsNullOrWhiteSpace(layerName) && layerTable.Has(layerName))
                try { entity.LayerId = layerTable[layerName]; } catch { }

            if (colorIndex >= 0 && colorIndex <= 256)
                try { entity.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex); } catch { }

            if (linetypeTable != null && !string.IsNullOrWhiteSpace(linetypeName) && linetypeTable.Has(linetypeName))
                try { entity.LinetypeId = linetypeTable[linetypeName]; } catch { }

            short lineweight;
            if (short.TryParse(lineweightText, out lineweight))
                try { entity.LineWeight = (LineWeight)lineweight; } catch { }
        }

        internal static ObjectId ResolveDimStyle(string dimStyleName, Database db, DimStyleTable dimStyleTable)
        {
            if (dimStyleTable != null && !string.IsNullOrWhiteSpace(dimStyleName) && dimStyleTable.Has(dimStyleName))
                return dimStyleTable[dimStyleName];
            return db.Dimstyle;
        }

        private void AddDrawable(Drawable drawable)
        {
            if (drawable == null) return;

            var added = false;
            try
            {
                added = TransientManager.CurrentTransientManager.AddTransient(
                    drawable, TransientDrawingMode.DirectShortTerm, SubDrawingMode, _viewports);
            }
            catch
            {
                // AddTransient threw before AutoCAD accepted ownership, so immediate disposal is safe.
                DisposeDrawable(drawable);
                throw;
            }

            if (!added)
            {
                // AutoCAD explicitly rejected the drawable and therefore does not retain it.
                DisposeDrawable(drawable);
                throw new InvalidOperationException("AutoCAD từ chối thêm đối tượng Preview transient.");
            }

            _drawables.Add(drawable);
        }

        internal int RunLifecycleProbe(Database db, int cycles)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (cycles < 1) throw new ArgumentOutOfRangeException(nameof(cycles));

            Clear();
            if (_drawables.Count != 0)
                throw new InvalidOperationException("Không thể giải phóng Preview cũ trước Soak QA.");

            for (var i = 0; i < cycles; i++)
            {
                var x = i * 0.01;
                var line = new Line(new Point3d(x, 0.0, 0.0), new Point3d(x + 10.0, 10.0, 0.0));
                line.SetDatabaseDefaults(db);
                AddDrawable(line);

                var circle = new Circle(new Point3d(x + 5.0, 5.0, 0.0), Vector3d.ZAxis, 2.0);
                circle.SetDatabaseDefaults(db);
                AddDrawable(circle);

                var text = new DBText
                {
                    Position = new Point3d(x, 12.0, 0.0),
                    Height = 1.0,
                    TextString = "HNL"
                };
                text.SetDatabaseDefaults(db);
                AddDrawable(text);

                if (_drawables.Count != 3)
                    throw new InvalidOperationException("Soak QA sai số drawable sau AddTransient.");

                Clear();
                if (_drawables.Count != 0)
                    throw new InvalidOperationException(
                        "Soak QA còn " + _drawables.Count + " transient chưa Erase an toàn ở vòng " + (i + 1) + ".");
            }

            return cycles;
        }

        private static void DisposeDrawable(Drawable drawable)
        {
            if (drawable is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }
        }

        private static Point3d ToPoint3d(Point2 point) => new Point3d(point.X, point.Y, 0.0);

        private static short GuideColorIndex(PreviewLineKind kind)
        {
            switch (kind)
            {
                case PreviewLineKind.Direction: return 5;
                case PreviewLineKind.Avoidance: return 1;
                case PreviewLineKind.Boundary: return 8;
                default: return 8;
            }
        }
    }
}
