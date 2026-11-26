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

        // A world-axis aligned extent is not axis-aligned after rotation.
        // Include all four corners; using just min/min and max/max underestimates
        // the local domain of 30/45-degree stretched member blocks.
        public static Box2 ToLocalBounds(Box2 worldBounds, double radians)
            => Box2.FromPoints(new[]
            {
                ToLocal(new Point2(worldBounds.MinX, worldBounds.MinY), radians),
                ToLocal(new Point2(worldBounds.MinX, worldBounds.MaxY), radians),
                ToLocal(new Point2(worldBounds.MaxX, worldBounds.MinY), radians),
                ToLocal(new Point2(worldBounds.MaxX, worldBounds.MaxY), radians)
            });
    }
}
