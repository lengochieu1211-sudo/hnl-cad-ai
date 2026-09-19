using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.AutoCAD
{
    internal struct BoundarySampleInfo
    {
        public bool AutoClosed;
        public double ClosureGap;
        public bool TinyZNormalized;
        public double MaxAbsZ;
        public string RejectionReason;
    }

    internal static class BoundarySampler
    {
        public static Boundary2 FromPolyline(Polyline polyline)
        {
            if (!TryFromPolyline(polyline, out var boundary, out var info))
                throw new ArgumentException(info.RejectionReason ?? "Polyline is not a valid ceiling boundary.", nameof(polyline));
            return boundary;
        }

        public static Boundary2 FromPolyline2d(Polyline2d polyline, Transaction tr)
        {
            if (!TryFromPolyline2d(polyline, tr, out var boundary, out var info))
                throw new ArgumentException(info.RejectionReason ?? "Polyline2d is not a valid ceiling boundary.", nameof(polyline));
            return boundary;
        }

        public static bool TryFromPolyline(Polyline polyline, out Boundary2 boundary, out BoundarySampleInfo info)
        {
            boundary = null;
            info = new BoundarySampleInfo();

            if (polyline == null)
            {
                info.RejectionReason = "Unsupported";
                return false;
            }

            var vertexCount = polyline.NumberOfVertices;
            if (vertexCount < 3)
            {
                info.RejectionReason = "Unsupported";
                return false;
            }

            var maxAbsZ = 0.0;
            for (var i = 0; i < vertexCount; i++)
            {
                var p = polyline.GetPoint3dAt(i);
                maxAbsZ = Math.Max(maxAbsZ, Math.Abs(p.Z));
            }

            info.MaxAbsZ = maxAbsZ;
            if (!BoundaryInputTolerancePolicy.AcceptZ(maxAbsZ))
            {
                info.RejectionReason = "Z";
                return false;
            }
            info.TinyZNormalized = BoundaryInputTolerancePolicy.IsTinyNonZeroZ(maxAbsZ);

            if (!polyline.Closed)
            {
                var first = polyline.GetPoint3dAt(0);
                var last = polyline.GetPoint3dAt(vertexCount - 1);
                var dx = last.X - first.X;
                var dy = last.Y - first.Y;
                var gap = Math.Sqrt(dx * dx + dy * dy);
                info.ClosureGap = gap;

                if (!BoundaryInputTolerancePolicy.AcceptOpenGap(gap))
                {
                    info.RejectionReason = "OpenGap";
                    return false;
                }

                info.AutoClosed = true;
            }

            var points = new List<Point2>();
            var segmentCount = polyline.Closed ? vertexCount : vertexCount - 1;

            for (var i = 0; i < segmentCount; i++)
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

            // For an accepted almost-closed open Polyline, keep the actual last vertex and let
            // Boundary2 close the tiny final gap in memory. The AutoCAD entity is never modified.
            if (!polyline.Closed)
            {
                var p = polyline.GetPoint3dAt(vertexCount - 1);
                points.Add(new Point2(p.X, p.Y));
            }

            if (points.Count < 3)
            {
                info.RejectionReason = "Unsupported";
                return false;
            }

            boundary = new Boundary2(points);
            return true;
        }

        public static bool TryFromPolyline2d(
            Polyline2d polyline,
            Transaction tr,
            out Boundary2 boundary,
            out BoundarySampleInfo info)
        {
            boundary = null;
            info = new BoundarySampleInfo();

            if (polyline == null || tr == null)
            {
                info.RejectionReason = "Unsupported";
                return false;
            }

            var positions = new List<Autodesk.AutoCAD.Geometry.Point3d>();
            foreach (ObjectId vertexId in polyline)
            {
                var vertex = tr.GetObject(vertexId, OpenMode.ForRead, false) as Vertex2d;
                if (vertex == null) continue;
                positions.Add(vertex.Position);
            }

            if (positions.Count < 3)
            {
                info.RejectionReason = "Unsupported";
                return false;
            }

            var maxAbsZ = 0.0;
            foreach (var p in positions)
                maxAbsZ = Math.Max(maxAbsZ, Math.Abs(p.Z));

            info.MaxAbsZ = maxAbsZ;
            if (!BoundaryInputTolerancePolicy.AcceptZ(maxAbsZ))
            {
                info.RejectionReason = "Z";
                return false;
            }
            info.TinyZNormalized = BoundaryInputTolerancePolicy.IsTinyNonZeroZ(maxAbsZ);

            if (!polyline.Closed)
            {
                var first = positions[0];
                var last = positions[positions.Count - 1];
                var dx = last.X - first.X;
                var dy = last.Y - first.Y;
                var gap = Math.Sqrt(dx * dx + dy * dy);
                info.ClosureGap = gap;

                if (!BoundaryInputTolerancePolicy.AcceptOpenGap(gap))
                {
                    info.RejectionReason = "OpenGap";
                    return false;
                }

                info.AutoClosed = true;
            }

            var points = new List<Point2>(positions.Count);
            foreach (var p in positions)
                points.Add(new Point2(p.X, p.Y));

            boundary = new Boundary2(points);
            return true;
        }
    }
}
