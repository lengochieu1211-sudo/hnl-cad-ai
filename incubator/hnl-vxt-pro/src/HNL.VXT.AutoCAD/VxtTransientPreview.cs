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
        public static VxtTransientPreview Instance { get; } = new VxtTransientPreview();

        private readonly List<Drawable> _drawables = new List<Drawable>();
        private readonly IntegerCollection _viewports = new IntegerCollection();
        private const int SubDrawingMode = 190;

        public void Refresh()
        {
            Clear();
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

                    RenderStructuralLines(plan, settings, db, layerTable, linetypeTable);
                    RenderHangers(plan, settings, db, layerTable, linetypeTable);
                    RenderDimensions(plan, settings, db, dimStyleTable, layerTable, linetypeTable);
                    RenderGuides(plan, db);

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

        public void Clear()
        {
            var manager = TransientManager.CurrentTransientManager;
            foreach (var drawable in _drawables)
            {
                try { manager.EraseTransient(drawable, _viewports); } catch { }
                if (drawable is IDisposable disposable) disposable.Dispose();
            }
            _drawables.Clear();
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
            TransientManager.CurrentTransientManager.AddTransient(
                drawable, TransientDrawingMode.DirectShortTerm, SubDrawingMode, _viewports);
            _drawables.Add(drawable);
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
