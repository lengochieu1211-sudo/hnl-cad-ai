using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Resolves the real world axis of an existing main member for Pro manual-existing paths.
    /// Legacy deliberately keeps the original V6.7.x bounding-box classification.
    /// </summary>
    internal static class VxtExistingMemberAxisResolver
    {
        internal sealed class ResolvedAxis
        {
            public ResolvedAxis(Point2 start, Point2 end, double angleRadians, bool verticalLike)
            {
                Start = start;
                End = end;
                AngleRadians = angleRadians;
                IsVerticalLike = verticalLike;
            }

            public Point2 Start { get; }
            public Point2 End { get; }
            public double AngleRadians { get; }
            public bool IsVerticalLike { get; }
            public bool IsHorizontalLike => !IsVerticalLike;
            public double Length => Start.DistanceTo(End);
        }

        public static ResolvedAxis Resolve(Entity entity, Transaction tr, VxtSettings settings)
        {
            if (entity == null || tr == null || settings == null) return null;

            if (settings.OptimizationMode == VxtOptimizationMode.Legacy)
                return ResolveLegacy(entity);

            var curve = entity as Curve;
            if (curve != null)
            {
                var curveAxis = ResolveCurve(curve);
                if (curveAxis != null) return curveAxis;
            }

            var block = entity as BlockReference;
            if (block != null)
            {
                var blockAxis = ResolveBlock(block, tr);
                if (blockAxis != null) return blockAxis;
            }

            // Fail closed to the certified legacy interpretation for unsupported CAD entity shapes.
            return ResolveLegacy(entity);
        }

        private static ResolvedAxis ResolveCurve(Curve curve)
        {
            try
            {
                var start = curve.StartPoint;
                var end = curve.EndPoint;
                return FromWorldEndpoints(
                    new Point2(start.X, start.Y),
                    new Point2(end.X, end.Y));
            }
            catch
            {
                return null;
            }
        }

        private static ResolvedAxis ResolveBlock(BlockReference block, Transaction tr)
        {
            try
            {
                var record = tr.GetObject(block.BlockTableRecord, OpenMode.ForRead, false) as BlockTableRecord;
                if (record == null) return null;

                var hasExtents = false;
                double minX = 0.0, minY = 0.0, maxX = 0.0, maxY = 0.0;
                foreach (ObjectId id in record)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null) continue;
                    try
                    {
                        var ext = entity.GeometricExtents;
                        if (!hasExtents)
                        {
                            minX = ext.MinPoint.X;
                            minY = ext.MinPoint.Y;
                            maxX = ext.MaxPoint.X;
                            maxY = ext.MaxPoint.Y;
                            hasExtents = true;
                        }
                        else
                        {
                            minX = Math.Min(minX, ext.MinPoint.X);
                            minY = Math.Min(minY, ext.MinPoint.Y);
                            maxX = Math.Max(maxX, ext.MaxPoint.X);
                            maxY = Math.Max(maxY, ext.MaxPoint.Y);
                        }
                    }
                    catch
                    {
                        // Skip non-graphical/invalid members inside a block definition.
                    }
                }

                if (!hasExtents) return null;
                var centerX = (minX + maxX) * 0.5;
                var centerY = (minY + maxY) * 0.5;
                Point3d localStart;
                Point3d localEnd;
                if (maxX - minX >= maxY - minY)
                {
                    localStart = new Point3d(minX, centerY, 0.0);
                    localEnd = new Point3d(maxX, centerY, 0.0);
                }
                else
                {
                    localStart = new Point3d(centerX, minY, 0.0);
                    localEnd = new Point3d(centerX, maxY, 0.0);
                }

                var matrix = block.BlockTransform;
                var worldStart = localStart.TransformBy(matrix);
                var worldEnd = localEnd.TransformBy(matrix);
                return FromWorldEndpoints(
                    new Point2(worldStart.X, worldStart.Y),
                    new Point2(worldEnd.X, worldEnd.Y));
            }
            catch
            {
                return null;
            }
        }

        private static ResolvedAxis ResolveLegacy(Entity entity)
        {
            try
            {
                var ext = entity.GeometricExtents;
                var legacy = ExistingMemberLayout.FromBounds(new Box2(
                    ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y));
                return new ResolvedAxis(
                    legacy.Start,
                    legacy.End,
                    legacy.IsHorizontal ? 0.0 : Math.PI * 0.5,
                    !legacy.IsHorizontal);
            }
            catch
            {
                return null;
            }
        }

        private static ResolvedAxis FromWorldEndpoints(Point2 a, Point2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= 1e-8) return null;

            var verticalLike = Math.Abs(dy) > Math.Abs(dx);
            var mustFlip = verticalLike ? dy < 0.0 : dx < 0.0;
            if (mustFlip)
            {
                var swap = a;
                a = b;
                b = swap;
                dx = -dx;
                dy = -dy;
            }

            return new ResolvedAxis(a, b, Math.Atan2(dy, dx), verticalLike);
        }
    }
}
