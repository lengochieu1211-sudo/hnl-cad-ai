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
    /// WYSIWYG transient renderer. Preview and Create consume the exact same Core plan.
    /// No temporary entity is appended to Model Space.
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
                    plan = new VxtPreviewPlanBuilder().Build(session.Boundary, settings, context);

                    var blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    var linetypeTable = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                    var dimStyleTable = tr.GetObject(db.DimStyleTableId, OpenMode.ForRead) as DimStyleTable;

                    RenderStructuralLines(plan, settings, db, blockTable, layerTable, linetypeTable);
                    RenderHangers(plan, settings, db, blockTable, layerTable, linetypeTable);
                    RenderDimensions(plan, settings, db, dimStyleTable, layerTable, linetypeTable);
                    RenderGuides(plan, db);
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
                if (drawable is IDisposable disposable) disposable.Dispose();
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
            var blockAvailable = blockTable != null && !string.IsNullOrWhiteSpace(settings.HangerBlockName) && blockTable.Has(settings.HangerBlockName);

            if (blockAvailable)
            {
                foreach (var p in plan.HangerPoints)
                {
                    var br = new BlockReference(ToPoint3d(p), blockTable[settings.HangerBlockName])
                    {
                        Rotation = ResolveHangerRotation(plan, p)
                    };
                    br.SetDatabaseDefaults(db);
                    ApplyAppearance(br, settings.HangerLayer, settings.HangerColorIndex,
                        settings.HangerLinetype, settings.HangerLineweight, layerTable, linetypeTable);
                    AddDrawable(br);
                }
                return;
            }

            // Same fallback marks present in the Core plan when the Ty block is unavailable.
            foreach (var item in plan.Lines.Where(x => x.Kind == PreviewLineKind.Hanger))
                AddStyledLine(item, settings.HangerLayer, settings.HangerColorIndex,
                    settings.HangerLinetype, settings.HangerLineweight, db, layerTable, linetypeTable);
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
            if (blockTable == null || string.IsNullOrWhiteSpace(blockName) || !blockTable.Has(blockName)) return false;
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
            TryApplyDynamicLength(br, length);
            AddDrawable(br);
            return true;
        }

        internal static void TryApplyDynamicLength(BlockReference br, double length)
        {
            try
            {
                if (!br.IsDynamicBlock) return;
                foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
                {
                    if (prop.ReadOnly || !(prop.Value is double) || ShouldSkipDynamicProperty(prop.PropertyName)) continue;
                    try { prop.Value = length; } catch { }
                }
            }
            catch
            {
                // Non-resident transient dynamic blocks may not expose properties on every release.
            }
        }

        internal static bool ShouldSkipDynamicProperty(string name)
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
            AddDrawable(line);
        }

        private void AddGuideText(PreviewText item, Database db)
        {
            var bounds = VxtSession.Current.Boundary.GetBounds();
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
