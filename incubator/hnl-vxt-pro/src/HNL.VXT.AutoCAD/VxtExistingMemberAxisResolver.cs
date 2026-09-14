using System;
using System.Linq;
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
        private const double MinimumAxisLength = 1e-8;
        private const double StraightnessRelativeTolerance = 1e-4;
        private const double StraightnessAbsoluteTolerance = 0.1;

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
                // Pro must not turn a bent/curved/closed polyline into a fake 0/90 member by
                // falling back to its world AABB. A manual XC is valid only when its actual
                // curve length agrees with the endpoint chord within drafting tolerance.
                return ResolveStraightCurve(curve);
            }

            var block = entity as BlockReference;
            if (block != null)
            {
                var blockAxis = ResolveBlock(block, tr);
                if (blockAxis != null) return blockAxis;
            }

            // Unsupported entity types keep the certified legacy interpretation. The selection
            // filter normally limits manual members to Curve/BlockReference, so this is only a
            // compatibility fallback for unusual proxy/entity subclasses.
            return ResolveLegacy(entity);
        }

        private static ResolvedAxis ResolveStraightCurve(Curve curve)
        {
            try
            {
                var start = curve.StartPoint;
                var end = curve.EndPoint;
                var a = new Point2(start.X, start.Y);
                var b = new Point2(end.X, end.Y);
                var chord = a.DistanceTo(b);
                if (chord <= MinimumAxisLength) return null;

                var startDistance = curve.GetDistanceAtParameter(curve.StartParam);
                var endDistance = curve.GetDistanceAtParameter(curve.EndParam);
                var pathLength = Math.Abs(endDistance - startDistance);
                if (pathLength <= MinimumAxisLength) return null;

                var tolerance = Math.Max(
                    StraightnessAbsoluteTolerance,
                    pathLength * StraightnessRelativeTolerance);
                if (Math.Abs(pathLength - chord) > tolerance) return null;

                return FromWorldEndpoints(a, b);
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
                var definitionAxis = FromWorldEndpoints(
                    new Point2(worldStart.X, worldStart.Y),
                    new Point2(worldEnd.X, worldEnd.Y));
                if (definitionAxis == null) return null;

                // Dynamic blocks may have a length/stretch action whose current instance is much
                // longer than the base definition. Preserve the definition's true direction but
                // project the visible instance extents onto that direction so Ty spans the actual
                // stretched member instead of being truncated to the authoring length.
                return ExtendAxisToVisibleInstance(block, definitionAxis) ?? definitionAxis;
            }
            catch
            {
                return null;
            }
        }

        private static ResolvedAxis ExtendAxisToVisibleInstance(BlockReference block, ResolvedAxis axis)
        {
            try
            {
                var dx = axis.End.X - axis.Start.X;
                var dy = axis.End.Y - axis.Start.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= MinimumAxisLength) return null;
                var ux = dx / length;
                var uy = dy / length;

                var ext = block.GeometricExtents;
                var corners = new[]
                {
                    new Point2(ext.MinPoint.X, ext.MinPoint.Y),
                    new Point2(ext.MinPoint.X, ext.MaxPoint.Y),
                    new Point2(ext.MaxPoint.X, ext.MinPoint.Y),
                    new Point2(ext.MaxPoint.X, ext.MaxPoint.Y)
                };
                var projections = corners.Select(point => point.X * ux + point.Y * uy).ToArray();
                var minProjection = projections.Min();
                var maxProjection = projections.Max();
                if (maxProjection - minProjection <= MinimumAxisLength) return null;

                var center = new Point2(
                    (axis.Start.X + axis.End.X) * 0.5,
                    (axis.Start.Y + axis.End.Y) * 0.5);
                var centerProjection = center.X * ux + center.Y * uy;
                var start = new Point2(
                    center.X + (minProjection - centerProjection) * ux,
                    center.Y + (minProjection - centerProjection) * uy);
                var end = new Point2(
                    center.X + (maxProjection - centerProjection) * ux,
                    center.Y + (maxProjection - centerProjection) * uy);
                return FromWorldEndpoints(start, end);
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
            if (length <= MinimumAxisLength) return null;

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
