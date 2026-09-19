using System;
using System.Collections.Generic;
using System.Linq;

namespace HNL.VXT.Core.Geometry
{
    public sealed class Boundary2
    {
        public Boundary2(IEnumerable<Point2> vertices)
        {
            Vertices = vertices?.ToList() ?? throw new ArgumentNullException(nameof(vertices));
            if (Vertices.Count < 3) throw new ArgumentException("Boundary requires at least three vertices.");
        }

        public IReadOnlyList<Point2> Vertices { get; }

        public (Point2 Min, Point2 Max) GetBounds()
        {
            return (
                new Point2(Vertices.Min(p => p.X), Vertices.Min(p => p.Y)),
                new Point2(Vertices.Max(p => p.X), Vertices.Max(p => p.Y))
            );
        }
    }
}
