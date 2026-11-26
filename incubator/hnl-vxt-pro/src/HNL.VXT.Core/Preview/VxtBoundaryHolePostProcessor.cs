using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Subtracts point-picked inner loops from the exact final XC/XP/Ty geometry.
    /// Existing layout solvers are not changed; Preview and Create consume this same final plan.
    /// </summary>
    public static class VxtBoundaryHolePostProcessor
    {
        private const double Tol = 1e-6;
        private const double BoundaryTol = 0.1;

        public static bool Apply(Boundary2 outerBoundary, VxtLayoutContext context, VxtPreviewPlan plan)
        {
            if (outerBoundary == null || context == null || plan == null ||
                context.BoundaryHoles == null || context.BoundaryHoles.Count == 0)
                return false;

            var holes = context.BoundaryHoles.Where(x => x != null).ToList();
            if (holes.Count == 0) return false;

            var changed = false;
            var rebuilt = new List<PreviewLine>(plan.Lines.Count + holes.Count * 4);

            foreach (var line in plan.Lines)
            {
                if (line.Kind != PreviewLineKind.Main && line.Kind != PreviewLineKind.Furring)
                {
                    rebuilt.Add(line);
                    continue;
                }

                var pieces = ClipSegmentToRegion(line.A, line.B, outerBoundary, holes);
                if (pieces.Count != 1 ||
                    !SamePoint(pieces[0].A, line.A) ||
                    !SamePoint(pieces[0].B, line.B))
                    changed = true;

                foreach (var piece in pieces)
                    if (piece.A.DistanceTo(piece.B) > Tol)
                        rebuilt.Add(new PreviewLine(piece.A, piece.B, line.Kind));
            }

            var keptHangers = plan.HangerPoints
                .Where(p => IsInsideRegion(p, outerBoundary, holes))
                .ToList();
            if (keptHangers.Count != plan.HangerPoints.Count)
                changed = true;

            if (!changed) return false;

            plan.Lines.Clear();
            plan.Lines.AddRange(rebuilt);
            plan.HangerPoints.Clear();
            plan.HangerPoints.AddRange(keptHangers);
            plan.MainSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Main);
            plan.FurringSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Furring);
            plan.HangerCount = plan.HangerPoints.Count;
            return true;
        }

        private static IReadOnlyList<Segment2> ClipSegmentToRegion(
            Point2 a, Point2 b, Boundary2 outer, IReadOnlyList<Boundary2> holes)
        {
            var ts = new List<double> { 0.0, 1.0 };
            AddBoundaryIntersections(a, b, outer, ts);
            foreach (var hole in holes) AddBoundaryIntersections(a, b, hole, ts);

            ts.Sort();
            var unique = new List<double>();
            foreach (var t in ts)
            {
                var clamped = Math.Max(0.0, Math.Min(1.0, t));
                if (unique.Count == 0 || Math.Abs(unique[unique.Count - 1] - clamped) > Tol)
                    unique.Add(clamped);
            }

            var pieces = new List<Segment2>();
            for (var i = 0; i + 1 < unique.Count; i++)
            {
                var t1 = unique[i];
                var t2 = unique[i + 1];
                if (t2 - t1 <= Tol) continue;
                if (!IsInsideRegion(Lerp(a, b, (t1 + t2) * 0.5), outer, holes)) continue;

                var p1 = Lerp(a, b, t1);
                var p2 = Lerp(a, b, t2);
                if (p1.DistanceTo(p2) <= Tol) continue;

                if (pieces.Count > 0 && SamePoint(pieces[pieces.Count - 1].B, p1))
                {
                    var prior = pieces[pieces.Count - 1];
                    pieces[pieces.Count - 1] = new Segment2(prior.A, p2);
                }
                else
                {
                    pieces.Add(new Segment2(p1, p2));
                }
            }
            return pieces;
        }

        private static void AddBoundaryIntersections(
            Point2 a, Point2 b, Boundary2 boundary, ICollection<double> target)
        {
            if (boundary == null || boundary.Vertices.Count < 3) return;
            for (var i = 0; i < boundary.Vertices.Count; i++)
            {
                var c = boundary.Vertices[i];
                var d = boundary.Vertices[(i + 1) % boundary.Vertices.Count];
                AddSegmentIntersectionParameters(a, b, c, d, target);
            }
        }

        private static void AddSegmentIntersectionParameters(
            Point2 a, Point2 b, Point2 c, Point2 d, ICollection<double> target)
        {
            var rx = b.X - a.X;
            var ry = b.Y - a.Y;
            var sx = d.X - c.X;
            var sy = d.Y - c.Y;
            var cross = Cross(rx, ry, sx, sy);
            var qx = c.X - a.X;
            var qy = c.Y - a.Y;

            if (Math.Abs(cross) <= Tol)
            {
                if (Math.Abs(Cross(qx, qy, rx, ry)) > BoundaryTol) return;
                var rr = rx * rx + ry * ry;
                if (rr <= Tol) return;
                target.Add((qx * rx + qy * ry) / rr);
                var q2x = d.X - a.X;
                var q2y = d.Y - a.Y;
                target.Add((q2x * rx + q2y * ry) / rr);
                return;
            }

            var t = Cross(qx, qy, sx, sy) / cross;
            var u = Cross(qx, qy, rx, ry) / cross;
            if (t >= -Tol && t <= 1.0 + Tol && u >= -Tol && u <= 1.0 + Tol)
                target.Add(t);
        }

        private static bool IsInsideRegion(
            Point2 point, Boundary2 outer, IReadOnlyList<Boundary2> holes)
        {
            if (!IsInsideOrOnBoundary(point, outer)) return false;
            foreach (var hole in holes)
                if (IsInsideOrOnBoundary(point, hole))
                    return false;
            return true;
        }

        private static bool IsInsideOrOnBoundary(Point2 point, Boundary2 boundary)
        {
            var vertices = boundary.Vertices;
            var inside = false;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Count];
                if (DistanceToSegment(point, a, b) <= BoundaryTol) return true;

                var crosses = (a.Y > point.Y) != (b.Y > point.Y);
                if (!crosses) continue;
                var x = a.X + (point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (x > point.X) inside = !inside;
            }
            return inside;
        }

        private static double DistanceToSegment(Point2 p, Point2 a, Point2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            if (len2 <= Tol) return p.DistanceTo(a);
            var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return p.DistanceTo(new Point2(a.X + t * dx, a.Y + t * dy));
        }

        private static Point2 Lerp(Point2 a, Point2 b, double t)
            => new Point2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        private static bool SamePoint(Point2 a, Point2 b) => a.DistanceTo(b) <= Tol;
        private static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;
    }
}
