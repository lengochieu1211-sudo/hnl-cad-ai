using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
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
        // A closed two-vertex lightweight Polyline may be a circle (two bulge
        // semicircles); do not reject it merely because it has two DXF vertices.
        public static bool TryFromEntity(Entity entity, Transaction tr, out Boundary2 boundary, out BoundarySampleInfo info)
        {
            if (entity is Polyline pl)
                return TryFromPolyline(pl, out boundary, out info);
            if (entity is Polyline2d pl2)
                return TryFromPolyline2d(pl2, tr, out boundary, out info);
            if (entity is Circle circle)
                return TryFromCircle(circle, out boundary, out info);
            if (entity is Spline spline)
                return TryFromClosedSpline(spline, out boundary, out info);
            if (entity is Mline mline)
                return TryFromClosedMline(mline, out boundary, out info);

            boundary = null;
            info = new BoundarySampleInfo { RejectionReason = "Unsupported" };
            return false;
        }

        private static bool TryFromCircle(Circle circle, out Boundary2 boundary, out BoundarySampleInfo info)
        {
            boundary = null;
            info = new BoundarySampleInfo();
            if (circle == null || circle.Radius <= 1e-8 || Math.Abs(circle.Normal.Z) < 0.999999)
            {
                info.RejectionReason = "Unsupported";
                return false;
            }

            info.MaxAbsZ = Math.Abs(circle.Center.Z);
            if (!BoundaryInputTolerancePolicy.AcceptZ(info.MaxAbsZ))
            {
                info.RejectionReason = "Z";
                return false;
            }
            info.TinyZNormalized = BoundaryInputTolerancePolicy.IsTinyNonZeroZ(info.MaxAbsZ);
            var segments = Math.Min(512, Math.Max(64, (int)Math.Ceiling(2.0 * Math.PI * circle.Radius / 25.0)));
            var points = new List<Point2>(segments);
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0 * Math.PI * i / segments;
                points.Add(new Point2(circle.Center.X + circle.Radius * Math.Cos(angle),
                    circle.Center.Y + circle.Radius * Math.Sin(angle)));
            }
            boundary = new Boundary2(points);
            return true;
        }

        private static bool TryFromClosedSpline(Spline spline, out Boundary2 boundary, out BoundarySampleInfo info)
        {
            boundary = null;
            info = new BoundarySampleInfo();
            if (spline == null || !spline.Closed)
            {
                info.RejectionReason = "OpenGap";
                return false;
            }

            try
            {
                var length = Math.Abs(spline.GetDistanceAtParameter(spline.EndParam) -
                    spline.GetDistanceAtParameter(spline.StartParam));
                if (length <= 1e-8)
                {
                    info.RejectionReason = "Unsupported";
                    return false;
                }
                var samples = Math.Min(512, Math.Max(64, (int)Math.Ceiling(length / 20.0)));
                var vertices = new List<Point2>(samples);
                var maxZ = 0.0;
                for (var i = 0; i < samples; i++)
                {
                    var position = spline.GetPointAtDist(length * i / samples);
                    maxZ = Math.Max(maxZ, Math.Abs(position.Z));
                    vertices.Add(new Point2(position.X, position.Y));
                }
                info.MaxAbsZ = maxZ;
                if (!BoundaryInputTolerancePolicy.AcceptZ(maxZ))
                {
                    info.RejectionReason = "Z";
                    return false;
                }
                info.TinyZNormalized = BoundaryInputTolerancePolicy.IsTinyNonZeroZ(maxZ);
                boundary = new Boundary2(vertices);
                return true;
            }
            catch
            {
                info.RejectionReason = "SplineSample";
                return false;
            }
        }

        private static bool TryFromClosedMline(Mline mline, out Boundary2 boundary, out BoundarySampleInfo info)
        {
            boundary = null;
            info = new BoundarySampleInfo();
            // Only a closed MLINE path defines an independent area. Its vertices
            // represent the path, not the two visible rails. Open MLINE segments
            // can still bound a room via TraceBoundary, but are not auto-closed here.
            if (mline == null || !mline.IsClosed || mline.NumberOfVertices < 3)
            {
                info.RejectionReason = "OpenGap";
                return false;
            }

            var vertices = new List<Point2>();
            var maxZ = 0.0;
            for (var i = 0; i < mline.NumberOfVertices; i++)
            {
                var position = mline.VertexAt(i);
                maxZ = Math.Max(maxZ, Math.Abs(position.Z));
                vertices.Add(new Point2(position.X, position.Y));
            }
            info.MaxAbsZ = maxZ;
            if (!BoundaryInputTolerancePolicy.AcceptZ(maxZ))
            {
                info.RejectionReason = "Z";
                return false;
            }
            info.TinyZNormalized = BoundaryInputTolerancePolicy.IsTinyNonZeroZ(maxZ);
            boundary = new Boundary2(vertices);
            return true;
        }

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
            if (vertexCount < 2 ||
                (vertexCount == 2 &&
                 (!polyline.Closed ||
                  (Math.Abs(polyline.GetBulgeAt(0)) <= 1e-12 &&
                   Math.Abs(polyline.GetBulgeAt(1)) <= 1e-12))))
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
                var next = (i + 1) % vertexCount;
                var a = polyline.GetPoint2dAt(i);
                var b = polyline.GetPoint2dAt(next);
                var start = new Point2(a.X, a.Y);
                var end = new Point2(b.X, b.Y);
                var bulge = polyline.GetBulgeAt(i);

                points.AddRange(VxtBulgeSampler.SampleSegment(start, end, bulge));
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
            var bulges = new List<double>();
            foreach (ObjectId vertexId in polyline)
            {
                var vertex = tr.GetObject(vertexId, OpenMode.ForRead, false) as Vertex2d;
                if (vertex == null) continue;
                positions.Add(vertex.Position);
                bulges.Add(vertex.Bulge);
            }

            if (positions.Count < 2 ||
                (positions.Count == 2 &&
                 (!polyline.Closed ||
                  (Math.Abs(bulges[0]) <= 1e-12 && Math.Abs(bulges[1]) <= 1e-12))))
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

            var points = new List<Point2>();
            var segmentCount = polyline.Closed ? positions.Count : positions.Count - 1;
            for (var i = 0; i < segmentCount; i++)
            {
                var next = (i + 1) % positions.Count;
                var start = new Point2(positions[i].X, positions[i].Y);
                var end = new Point2(positions[next].X, positions[next].Y);
                var bulge = i < bulges.Count ? bulges[i] : 0.0;
                points.AddRange(VxtBulgeSampler.SampleSegment(start, end, bulge));
            }

            if (!polyline.Closed)
            {
                var p = positions[positions.Count - 1];
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
    }
}
