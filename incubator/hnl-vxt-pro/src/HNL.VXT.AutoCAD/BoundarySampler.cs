using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.AutoCAD
{
    internal static class BoundarySampler
    {
        public static Boundary2 FromPolyline(Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (!polyline.Closed) throw new ArgumentException("Polyline must be closed.", nameof(polyline));

            var points = new List<Point2>();
            var segments = polyline.NumberOfVertices;

            for (var i = 0; i < segments; i++)
            {
                var bulge = polyline.GetBulgeAt(i);
                var samples = Math.Abs(bulge) > 1e-9 ? 12 : 1;

                for (var j = 0; j < samples; j++)
                {
                    var parameter = i + (double)j / samples;
                    var p = polyline.GetPointAtParameter(parameter);
                    points.Add(new Point2(p.X, p.Y));
                }
            }

            return new Boundary2(points);
        }

        public static Boundary2 FromPolyline2d(Polyline2d polyline, Transaction tr)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (!polyline.Closed) throw new ArgumentException("Polyline2d must be closed.", nameof(polyline));

            var points = new List<Point2>();
            foreach (ObjectId vertexId in polyline)
            {
                var vertex = tr.GetObject(vertexId, OpenMode.ForRead, false) as Vertex2d;
                if (vertex == null) continue;
                points.Add(new Point2(vertex.Position.X, vertex.Position.Y));
            }
            return new Boundary2(points);
        }
    }
}