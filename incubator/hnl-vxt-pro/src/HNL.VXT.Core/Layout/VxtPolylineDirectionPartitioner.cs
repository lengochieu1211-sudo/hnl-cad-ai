using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Splits one ceiling boundary into non-overlapping direction regions from an open
    /// multi-segment guide path. Adjacent regions meet on the angle bisector at each bend.
    /// A single path segment intentionally produces no region split and is handled by the
    /// certified TwoPoints path instead.
    /// </summary>
    public static class VxtPolylineDirectionPartitioner
    {
        private const double Eps = 1e-7;
        private const double MinSegmentLength = 1.0;

        public static IReadOnlyList<VxtLayoutRegion> Partition(
            Boundary2 boundary,
            IReadOnlyList<Point2> pathPoints,
            bool furringFromFarEdge = false)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            if (pathPoints == null) throw new ArgumentNullException(nameof(pathPoints));
            if (pathPoints.Count < 3)
                throw new ArgumentException("Tuyến gấp khúc cần ít nhất 3 điểm.", nameof(pathPoints));

            var points = RemoveConsecutiveDuplicates(pathPoints);
            if (points.Count < 3)
                throw new ArgumentException("Tuyến gấp khúc cần ít nhất 2 đoạn hợp lệ.", nameof(pathPoints));

            ValidatePath(points);

            var directions = new List<Vector2>(points.Count - 1);
            for (var i = 0; i + 1 < points.Count; i++)
                directions.Add(Unit(points[i], points[i + 1]));

            var regions = new List<VxtLayoutRegion>(directions.Count);
            for (var segmentIndex = 0; segmentIndex < directions.Count; segmentIndex++)
            {
                var polygon = boundary.Vertices.ToList();

                if (segmentIndex > 0)
                {
                    var normal = BisectorNormal(directions[segmentIndex - 1], directions[segmentIndex]);
                    polygon = ClipHalfPlane(polygon, points[segmentIndex], normal, keepPositive: true);
                }

                if (segmentIndex + 1 < directions.Count)
                {
                    var normal = BisectorNormal(directions[segmentIndex], directions[segmentIndex + 1]);
                    polygon = ClipHalfPlane(polygon, points[segmentIndex + 1], normal, keepPositive: false);
                }

                polygon = CleanPolygon(polygon);
                if (polygon.Count < 3 || Math.Abs(SignedArea(polygon)) < 1.0)
                    throw new InvalidOperationException(
                        "Tuyến hướng tạo một vùng rỗng hoặc quá nhỏ tại đoạn " + (segmentIndex + 1) + ".");

                var angle = NormalizeDegrees(
                    Math.Atan2(directions[segmentIndex].Y, directions[segmentIndex].X) * 180.0 / Math.PI);
                regions.Add(new VxtLayoutRegion(new Boundary2(polygon), angle, furringFromFarEdge));
            }

            return regions;
        }

        private static List<Point2> RemoveConsecutiveDuplicates(IReadOnlyList<Point2> source)
        {
            var result = new List<Point2>();
            foreach (var point in source)
            {
                if (result.Count == 0 || result[result.Count - 1].DistanceTo(point) > MinSegmentLength)
                    result.Add(point);
            }
            return result;
        }

        private static void ValidatePath(IReadOnlyList<Point2> points)
        {
            for (var i = 0; i + 1 < points.Count; i++)
            {
                if (points[i].DistanceTo(points[i + 1]) <= MinSegmentLength)
                    throw new InvalidOperationException("Tuyến hướng có đoạn quá ngắn.");
            }

            for (var i = 0; i + 2 < points.Count; i++)
            {
                var a = Unit(points[i], points[i + 1]);
                var b = Unit(points[i + 1], points[i + 2]);
                var sumX = a.X + b.X;
                var sumY = a.Y + b.Y;
                if (Math.Sqrt(sumX * sumX + sumY * sumY) < 1e-5)
                    throw new InvalidOperationException(
                        "Tuyến hướng có đoạn quay ngược 180 độ tại điểm " + (i + 2) + ".");
            }

            for (var i = 0; i + 1 < points.Count; i++)
            {
                for (var j = i + 2; j + 1 < points.Count; j++)
                {
                    if (i == 0 && j + 1 == points.Count - 1)
                    {
                        // Open paths may have first/last segments touching only if the shared
                        // endpoint rule below applies; otherwise a closed/self-crossing guide
                        // is intentionally rejected.
                    }

                    if (SegmentsProperlyIntersect(points[i], points[i + 1], points[j], points[j + 1]))
                        throw new InvalidOperationException("Tuyến hướng tự cắt; hãy vẽ một tuyến mở không giao nhau.");
                }
            }
        }

        private static Vector2 BisectorNormal(Vector2 previous, Vector2 next)
        {
            var x = previous.X + next.X;
            var y = previous.Y + next.Y;
            var length = Math.Sqrt(x * x + y * y);
            if (length < 1e-5)
                throw new InvalidOperationException("Không thể tạo đường phân vùng tại góc quay ngược 180 độ.");
            return new Vector2(x / length, y / length);
        }

        private static List<Point2> ClipHalfPlane(
            IReadOnlyList<Point2> polygon,
            Point2 origin,
            Vector2 normal,
            bool keepPositive)
        {
            var output = new List<Point2>();
            if (polygon == null || polygon.Count == 0) return output;

            var previous = polygon[polygon.Count - 1];
            var previousValue = SignedDistance(previous, origin, normal);
            var previousInside = IsInside(previousValue, keepPositive);

            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var currentValue = SignedDistance(current, origin, normal);
                var currentInside = IsInside(currentValue, keepPositive);

                if (currentInside != previousInside)
                {
                    var denominator = previousValue - currentValue;
                    if (Math.Abs(denominator) > Eps)
                    {
                        var t = previousValue / denominator;
                        output.Add(new Point2(
                            previous.X + (current.X - previous.X) * t,
                            previous.Y + (current.Y - previous.Y) * t));
                    }
                }

                if (currentInside)
                    output.Add(current);

                previous = current;
                previousValue = currentValue;
                previousInside = currentInside;
            }

            return output;
        }

        private static bool IsInside(double signedDistance, bool keepPositive)
            => keepPositive ? signedDistance >= -Eps : signedDistance <= Eps;

        private static double SignedDistance(Point2 point, Point2 origin, Vector2 normal)
            => (point.X - origin.X) * normal.X + (point.Y - origin.Y) * normal.Y;

        private static List<Point2> CleanPolygon(IReadOnlyList<Point2> polygon)
        {
            var result = new List<Point2>();
            if (polygon == null) return result;

            foreach (var point in polygon)
            {
                if (result.Count == 0 || result[result.Count - 1].DistanceTo(point) > Eps)
                    result.Add(point);
            }

            if (result.Count > 1 && result[0].DistanceTo(result[result.Count - 1]) <= Eps)
                result.RemoveAt(result.Count - 1);

            return result;
        }

        private static double SignedArea(IReadOnlyList<Point2> polygon)
        {
            var sum = 0.0;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                sum += a.X * b.Y - b.X * a.Y;
            }
            return sum * 0.5;
        }

        private static Vector2 Unit(Point2 a, Point2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= MinSegmentLength)
                throw new InvalidOperationException("Tuyến hướng có đoạn quá ngắn.");
            return new Vector2(dx / length, dy / length);
        }

        private static bool SegmentsProperlyIntersect(Point2 a, Point2 b, Point2 c, Point2 d)
        {
            if (a.DistanceTo(c) <= Eps || a.DistanceTo(d) <= Eps ||
                b.DistanceTo(c) <= Eps || b.DistanceTo(d) <= Eps)
                return false;

            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            return ((abC > Eps && abD < -Eps) || (abC < -Eps && abD > Eps)) &&
                   ((cdA > Eps && cdB < -Eps) || (cdA < -Eps && cdB > Eps));
        }

        private static double Cross(Point2 a, Point2 b, Point2 p)
            => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

        private static double NormalizeDegrees(double value)
        {
            value %= 180.0;
            return value < 0.0 ? value + 180.0 : value;
        }

        private struct Vector2
        {
            public Vector2(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X;
            public double Y;
        }
    }
}
