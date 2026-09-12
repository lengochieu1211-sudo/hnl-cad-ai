using System;

namespace HNL.VXT.Core.Geometry
{
    public readonly struct Point2
    {
        public Point2(double x, double y) { X = x; Y = y; }
        public double X { get; }
        public double Y { get; }

        public static Point2 operator +(Point2 a, Point2 b) => new Point2(a.X + b.X, a.Y + b.Y);
        public static Point2 operator -(Point2 a, Point2 b) => new Point2(a.X - b.X, a.Y - b.Y);
        public static Point2 operator *(Point2 a, double k) => new Point2(a.X * k, a.Y * k);

        public double DistanceTo(Point2 other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
