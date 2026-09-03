using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
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

            try
            {
                var builder = new VxtPreviewPlanBuilder();
                var plan = builder.Build(session.Boundary, session.Settings);

                var bounds = session.Boundary.GetBounds();
                var diag = bounds.Min.DistanceTo(bounds.Max);
                var textHeight = Math.Max(60.0, Math.Min(180.0, diag * 0.015));

                foreach (var line in plan.Lines)
                    AddLine(line);

                foreach (var text in plan.Texts)
                    AddText(text, textHeight);

                session.ViewModel?.SetPreviewStats(
                    plan.MainSegmentCount,
                    plan.FurringSegmentCount,
                    plan.HangerCount,
                    plan.DimensionSegmentCount);
            }
            catch (System.Exception ex)
            {
                session.ViewModel?.SetPreviewError(ex.Message);
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage("\nHNL Tool - VXT Pro Preview: " + ex.Message);
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

        private void AddLine(PreviewLine item)
        {
            var line = new Line(
                new Point3d(item.A.X, item.A.Y, 0),
                new Point3d(item.B.X, item.B.Y, 0))
            {
                Color = Color.FromColorIndex(ColorMethod.ByAci, ColorIndex(item.Kind))
            };

            if (item.Kind == PreviewLineKind.DimensionExtension)
                line.LinetypeScale = 0.5;

            AddDrawable(line);
        }

        private void AddText(PreviewText item, double height)
        {
            var text = new DBText
            {
                Position = new Point3d(item.Position.X, item.Position.Y, 0),
                Height = height,
                TextString = item.Text,
                Rotation = item.RotationRadians,
                Color = Color.FromColorIndex(ColorMethod.ByAci, ColorIndex(item.Kind))
            };

            AddDrawable(text);
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

        private static short ColorIndex(PreviewLineKind kind)
        {
            switch (kind)
            {
                case PreviewLineKind.Main: return 4;
                case PreviewLineKind.Furring: return 2;
                case PreviewLineKind.Hanger: return 3;
                case PreviewLineKind.Dimension:
                case PreviewLineKind.DimensionExtension: return 6;
                case PreviewLineKind.Direction: return 5;
                case PreviewLineKind.Avoidance: return 1;
                default: return 8;
            }
        }
    }
}
