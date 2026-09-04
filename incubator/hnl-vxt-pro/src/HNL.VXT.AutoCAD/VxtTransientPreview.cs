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
    /// WYSIWYG transient renderer. Preview uses the same block definitions, layer appearance
    /// and DimStyle that the future Create engine will use, while keeping Model Space clean.
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
                var builder = new VxtPreviewPlanBuilder();
                var plan = builder.Build(session.Boundary, session.Settings);
                var settings = session.Settings;
                var db = doc.Database;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    var linetypeTable = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                    var dimStyleTable = tr.GetObject(db.DimStyleTableId, OpenMode.ForRead) as DimStyleTable;

                    RenderStructuralLines(plan, settings, db, blockTable, layerTable, linetypeTable);
                    RenderHangers(plan, settings, db, blockTable, layerTable, linetypeTable);
                    RenderDimensions(plan, session.Boundary, settings, db, dimStyleTable, layerTable, linetypeTable);
                    RenderGuides(plan, settings, db, layerTable, linetypeTable);

                    tr.Commit();
                }

                session.ViewModel?.SetPreviewStats(
                    plan.MainSegmentCount,
                    plan.FurringSegmentCount,
                    plan.HangerCount,
                    plan.DimensionSegmentCount);
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
                if (drawable is IDisposable disposable)
                    disposable.Dispose();
            }
            _drawables.Clear();
        }

        private void RenderStructuralLines(
            VxtPreviewPlan plan,
            VxtSettings settings,
            Database db,
            BlockTable blockTable,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            foreach (var item in plan.Lines)
            {
                if (item.Kind == PreviewLineKind.Main)
                {
                    if (settings.UseDynamicMainBlock && TryAddSegmentBlock(
                        item, settings.MainBlockName, settings.MainLayer, settings.MainColorIndex,
                        settings.MainLinetype, settings.MainLineweight, db, blockTable, layerTable, linetypeTable))
                        continue;

                    AddStyledLine(item, settings.MainLayer, settings.MainColorIndex,
                        settings.MainLinetype, settings.MainLineweight, db, layerTable, linetypeTable);
                }
                else if (item.Kind == PreviewLineKind.Furring)
                {
                    if (settings.UseDynamicFurringBlock && TryAddSegmentBlock(
                        item, settings.FurringBlockName, settings.FurringLayer, settings.FurringColorIndex,
                        settings.FurringLinetype, settings.FurringLineweight, db, blockTable, layerTable, linetypeTable))
                        continue;

                    AddStyledLine(item, settings.FurringLayer, settings.FurringColorIndex,
                        settings.FurringLinetype, settings.FurringLineweight, db, layerTable, linetypeTable);
                }
            }
        }

        private void RenderHangers(
            VxtPreviewPlan plan,
            VxtSettings settings,
            Database db,
            BlockTable blockTable,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            if (!settings.DrawHangers) return;

            var hangerLines = plan.Lines.Where(x => x.Kind == PreviewLineKind.Hanger).ToList();
            var blockAvailable = blockTable != null && !string.IsNullOrWhiteSpace(settings.HangerBlockName) && blockTable.Has(settings.HangerBlockName);

            if (blockAvailable)
            {
                // AddHangerMark emits two crossing lines per Ty point. Use their common midpoint.
                for (var i = 0; i + 1 < hangerLines.Count; i += 2)
                {
                    var a = hangerLines[i];
                    var center = new Point3d(
                        (a.A.X + a.B.X) * 0.5,
                        (a.A.Y + a.B.Y) * 0.5,
                        0.0);

                    var blockId = blockTable[settings.HangerBlockName];
                    var br = new BlockReference(center, blockId)
                    {
                        Rotation = settings.DirectionDegrees * Math.PI / 180.0
                    };
                    br.SetDatabaseDefaults(db);
                    ApplyAppearance(br, settings.HangerLayer, settings.HangerColorIndex,
                        settings.HangerLinetype, settings.HangerLineweight, layerTable, linetypeTable);
                    AddDrawable(br);
                }
                return;
            }

            foreach (var item in hangerLines)
                AddStyledLine(item, settings.HangerLayer, settings.HangerColorIndex,
                    settings.HangerLinetype, settings.HangerLineweight, db, layerTable, linetypeTable);
        }

        private void RenderDimensions(
            VxtPreviewPlan plan,
            Boundary2 boundary,
            VxtSettings settings,
            Database db,
            DimStyleTable dimStyleTable,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            if (!settings.AutoDimension) return;

            var dimLines = plan.Lines.Where(x => x.Kind == PreviewLineKind.Dimension).ToList();
            if (dimLines.Count == 0) return;

            var radians = settings.DirectionDegrees * Math.PI / 180.0;
            var localBoundary = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
            var minX = localBoundary.Min(p => p.X);
            var maxX = localBoundary.Max(p => p.X);
            var minY = localBoundary.Min(p => p.Y);
            var maxY = localBoundary.Max(p => p.Y);
            var dimStyleId = ResolveDimStyle(settings.DimensionStyle, db, dimStyleTable);

            foreach (var item in dimLines)
            {
                var aLocal = Transform2.ToLocal(item.A, radians);
                var bLocal = Transform2.ToLocal(item.B, radians);
                var dx = Math.Abs(bLocal.X - aLocal.X);
                var dy = Math.Abs(bLocal.Y - aLocal.Y);

                Point2 x1;
                Point2 x2;
                Point2 dimPoint;
                double rotation;

                if (dx >= dy)
                {
                    var yDim = (aLocal.Y + bLocal.Y) * 0.5;
                    var yBase = Math.Abs(yDim - maxY) <= Math.Abs(yDim - minY) ? maxY : minY;
                    x1 = Transform2.ToWorld(new Point2(aLocal.X, yBase), radians);
                    x2 = Transform2.ToWorld(new Point2(bLocal.X, yBase), radians);
                    dimPoint = Transform2.ToWorld(new Point2((aLocal.X + bLocal.X) * 0.5, yDim), radians);
                    rotation = radians;
                }
                else
                {
                    var xDim = (aLocal.X + bLocal.X) * 0.5;
                    var xBase = Math.Abs(xDim - maxX) <= Math.Abs(xDim - minX) ? maxX : minX;
                    x1 = Transform2.ToWorld(new Point2(xBase, aLocal.Y), radians);
                    x2 = Transform2.ToWorld(new Point2(xBase, bLocal.Y), radians);
                    dimPoint = Transform2.ToWorld(new Point2(xDim, (aLocal.Y + bLocal.Y) * 0.5), radians);
                    rotation = radians + Math.PI / 2.0;
                }

                var dim = new RotatedDimension(
                    rotation,
                    ToPoint3d(x1),
                    ToPoint3d(x2),
                    ToPoint3d(dimPoint),
                    string.Empty,
                    dimStyleId);

                dim.SetDatabaseDefaults(db);
                ApplyAppearance(dim, settings.DimensionLayer, settings.DimensionColorIndex,
                    settings.DimensionLinetype, settings.DimensionLineweight, layerTable, linetypeTable);
                AddDrawable(dim);
            }
        }

        private void RenderGuides(
            VxtPreviewPlan plan,
            VxtSettings settings,
            Database db,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            var hasRealDimensions = settings.AutoDimension && plan.Lines.Any(x => x.Kind == PreviewLineKind.Dimension);

            foreach (var item in plan.Lines)
            {
                if (item.Kind == PreviewLineKind.Main || item.Kind == PreviewLineKind.Furring || item.Kind == PreviewLineKind.Hanger)
                    continue;

                // Fake DIM line/extensions are retained in Core for geometry tests only.
                // The AutoCAD bridge renders real RotatedDimension objects instead.
                if (hasRealDimensions && (item.Kind == PreviewLineKind.Dimension || item.Kind == PreviewLineKind.DimensionExtension))
                    continue;

                AddGuideLine(item, db);
            }

            foreach (var text in plan.Texts)
            {
                if (hasRealDimensions && text.Kind == PreviewLineKind.Dimension)
                    continue;
                AddGuideText(text, db);
            }
        }

        private bool TryAddSegmentBlock(
            PreviewLine item,
            string blockName,
            string layerName,
            short colorIndex,
            string linetypeName,
            string lineweightText,
            Database db,
            BlockTable blockTable,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            if (blockTable == null || string.IsNullOrWhiteSpace(blockName) || !blockTable.Has(blockName))
                return false;

            var start = ToPoint3d(item.A);
            var end = ToPoint3d(item.B);
            var vector = end - start;
            var length = vector.Length;
            if (length <= 1e-9) return false;

            var br = new BlockReference(start, blockTable[blockName])
            {
                Rotation = Math.Atan2(vector.Y, vector.X)
            };
            br.SetDatabaseDefaults(db);
            ApplyAppearance(br, layerName, colorIndex, linetypeName, lineweightText, layerTable, linetypeTable);

            // Port of V6.7.4 draw-pline behavior: writable numeric dynamic properties that are
            // not angle/position/array/spacing metadata are treated as the member length.
            TryApplyDynamicLength(br, length);
            AddDrawable(br);
            return true;
        }

        private static void TryApplyDynamicLength(BlockReference br, double length)
        {
            try
            {
                if (!br.IsDynamicBlock) return;
                var props = br.DynamicBlockReferencePropertyCollection;
                foreach (DynamicBlockReferenceProperty prop in props)
                {
                    if (prop.ReadOnly) continue;
                    var value = prop.Value;
                    if (!(value is double)) continue;
                    if (ShouldSkipDynamicProperty(prop.PropertyName)) continue;
                    try { prop.Value = length; } catch { }
                }
            }
            catch
            {
                // Some AutoCAD builds do not expose dynamic properties until a BlockReference
                // is database-resident. Preview must remain non-destructive, so leave the
                // original block state rather than inserting temporary Model Space entities.
            }
        }

        private static bool ShouldSkipDynamicProperty(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.ToUpperInvariant();
            return n.Contains("ANG") || n.Contains("ROT") || n.Contains("POS") || n.Contains("ORI") ||
                   n.Contains("ARRAY") || n.Contains("XOAY") || n.Contains("K.C") || n.Contains("K. C") ||
                   n.Contains("KHOẢNG") || n.Contains("TY") || n.Contains("CHÍNH") || n.Contains("PHỤ") ||
                   n.Contains("DÃY");
        }

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
            if (item.Kind == PreviewLineKind.DimensionExtension)
                line.LinetypeScale = 0.5;
            AddDrawable(line);
        }

        private void AddGuideText(PreviewText item, Database db)
        {
            var bounds = VxtSession.Current.Boundary.GetBounds();
            var diag = bounds.Min.DistanceTo(bounds.Max);
            var textHeight = Math.Max(60.0, Math.Min(180.0, diag * 0.015));

            var text = new DBText
            {
                Position = ToPoint3d(item.Position),
                Height = textHeight,
                TextString = item.Text,
                Rotation = item.RotationRadians,
                Color = Color.FromColorIndex(ColorMethod.ByAci, GuideColorIndex(item.Kind))
            };
            text.SetDatabaseDefaults(db);
            AddDrawable(text);
        }

        private static void ApplyAppearance(
            Entity entity,
            string layerName,
            short colorIndex,
            string linetypeName,
            string lineweightText,
            LayerTable layerTable,
            LinetypeTable linetypeTable)
        {
            if (layerTable != null && !string.IsNullOrWhiteSpace(layerName) && layerTable.Has(layerName))
            {
                try { entity.LayerId = layerTable[layerName]; } catch { }
            }

            if (colorIndex >= 0 && colorIndex <= 256)
            {
                try { entity.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex); } catch { }
            }

            if (linetypeTable != null && !string.IsNullOrWhiteSpace(linetypeName) && linetypeTable.Has(linetypeName))
            {
                try { entity.LinetypeId = linetypeTable[linetypeName]; } catch { }
            }

            if (short.TryParse(lineweightText, out var lineweight))
            {
                try { entity.LineWeight = (LineWeight)lineweight; } catch { }
            }
        }

        private static ObjectId ResolveDimStyle(string dimStyleName, Database db, DimStyleTable dimStyleTable)
        {
            if (dimStyleTable != null && !string.IsNullOrWhiteSpace(dimStyleName) && dimStyleTable.Has(dimStyleName))
                return dimStyleTable[dimStyleName];
            return db.Dimstyle;
        }

        private void AddDrawable(Drawable drawable)
        {
            TransientManager.CurrentTransientManager.AddTransient(
                drawable,
                TransientDrawingMode.DirectShortTerm,
                SubDrawingMode,
                _viewports);
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
