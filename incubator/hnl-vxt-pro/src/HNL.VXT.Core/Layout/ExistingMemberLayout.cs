using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// AutoCAD-independent port of the legacy VXT get-bone-axis + manual-Ty path.
    /// The Lisp classifies an existing member from its bounding box: dx > dy is
    /// horizontal; equal or taller extents are vertical. Hangers are then laid out
    /// with the same calc-smart-layout rules used by generated main members.
    /// </summary>
    public static class ExistingMemberLayout
    {
        private const double Eps = 1e-8;

        public sealed class Axis
        {
            public Axis(bool isHorizontal, Point2 start, Point2 end)
            {
                IsHorizontal = isHorizontal;
                Start = start;
                End = end;
            }

            public bool IsHorizontal { get; }
            public Point2 Start { get; }
            public Point2 End { get; }
            public double Length => IsHorizontal
                ? Math.Abs(End.X - Start.X)
                : Math.Abs(End.Y - Start.Y);
            public double FixedCoordinate => IsHorizontal ? Start.Y : Start.X;
        }

        public static Axis FromBounds(Box2 bounds)
        {
            var horizontal = bounds.Width > bounds.Height; // exact Lisp: (> dx dy)
            if (horizontal)
            {
                var y = (bounds.MinY + bounds.MaxY) * 0.5;
                return new Axis(true, new Point2(bounds.MinX, y), new Point2(bounds.MaxX, y));
            }

            var x = (bounds.MinX + bounds.MaxX) * 0.5;
            return new Axis(false, new Point2(x, bounds.MinY), new Point2(x, bounds.MaxY));
        }

        public static IReadOnlyList<Point2> HangerPoints(
            Axis axis,
            VxtSettings settings,
            bool reverse,
            IEnumerable<Box2> avoidanceBoxes = null)
        {
            if (axis == null) throw new ArgumentNullException(nameof(axis));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (axis.Length <= Eps) return Array.Empty<Point2>();

            var layoutMode = settings.HangerLayout == HangerLayoutMode.OneSideFollowFurring
                ? MainLayoutMode.OneSide
                : MainLayoutMode.BalancedTwoEnds;

            var layout = SmartLayout1D.Calculate(
                axis.Length,
                settings.HangerMaxSpacing,
                settings.HangerMinSpacing,
                settings.HangerMaxEdgeOffset,
                settings.HangerMinEdgeOffset,
                settings.HangerBalanceStep,
                layoutMode,
                reverse);
            if (layout == null) return Array.Empty<Point2>();

            IReadOnlyList<double> coordinates = layout.Positions(0.0);
            var boxes = (avoidanceBoxes ?? Enumerable.Empty<Box2>()).ToList();
            if (settings.UseAvoidance && boxes.Count > 0 && coordinates.Count > 0)
            {
                var intervals = new List<Tuple<double, double>>();
                foreach (var source in boxes)
                {
                    var box = source.Expand(settings.ClearanceDistance);
                    if (axis.IsHorizontal)
                    {
                        if (axis.FixedCoordinate >= box.MinY - Eps && axis.FixedCoordinate <= box.MaxY + Eps)
                            intervals.Add(Tuple.Create(box.MinX - axis.Start.X, box.MaxX - axis.Start.X));
                    }
                    else
                    {
                        if (axis.FixedCoordinate >= box.MinX - Eps && axis.FixedCoordinate <= box.MaxX + Eps)
                            intervals.Add(Tuple.Create(box.MinY - axis.Start.Y, box.MaxY - axis.Start.Y));
                    }
                }

                if (intervals.Count > 0)
                {
                    coordinates = SmartLayout1D.AdjustGrid(
                        coordinates,
                        intervals,
                        0.0,
                        axis.Length,
                        settings.HangerMinSpacing,
                        settings.HangerMaxSpacing,
                        settings.HangerMinEdgeOffset,
                        settings.HangerMaxEdgeOffset,
                        settings.HangerBalanceStep);
                }
            }

            return coordinates.Select(value => axis.IsHorizontal
                    ? new Point2(axis.Start.X + value, axis.FixedCoordinate)
                    : new Point2(axis.FixedCoordinate, axis.Start.Y + value))
                .ToArray();
        }
    }
}
