using System;

namespace HNL.VXT.Core.Geometry
{
    public static class Transform2
    {
        public static Point2 Rotate(Point2 p, double radians)
        {
            var c = Math.Cos(radians);
            var s = Math.Sin(radians);
            return new Point2(p.X * c - p.Y * s, p.X * s + p.Y * c);
        }

        public static Point2 ToLocal(Point2 p, double radians) => Rotate(p, -radians);
        public static Point2 ToWorld(Point2 p, double radians) => Rotate(p, radians);
    }
}
