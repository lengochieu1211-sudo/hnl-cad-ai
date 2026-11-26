using System;
using System.Collections.Generic;

namespace HNL.VXT.Core.Geometry
{
    /// <summary>
    /// Pure-geometry AutoCAD bulge sampler.
    /// The old bridge used a fixed 12 chords for every curved segment. That makes geometric
    /// error depend on radius/sweep and can distort HARD edge checks on large ceiling arcs.
    /// </summary>
    public static class VxtBulgeSampler
    {
        public const double DefaultMaxChordError = 0.5;
        public const int DefaultMaxSegments = 512;

        public static int GetSubdivisionCount(
            Point2 start,
            Point2 end,
            double bulge,
            double maxChordError = DefaultMaxChordError,
            int maxSegments = DefaultMaxSegments)
        {
            var chord = start.DistanceTo(end);
            if (chord <= 1e-9 || Math.Abs(bulge) <= 1e-12)
                return 1;

            var sweep = Math.Abs(4.0 * Math.Atan(bulge));
            if (sweep <= 1e-12)
                return 1;

            var radius = chord * (1.0 + bulge * bulge) / (4.0 * Math.Abs(bulge));
            if (radius <= 1e-9)
                return 1;

            var error = Math.Max(1e-6, maxChordError);
            var ratio = 1.0 - Math.Min(error, radius) / radius;
            ratio = Math.Max(-1.0, Math.Min(1.0, ratio));
            var maxAngle = 2.0 * Math.Acos(ratio);
            if (maxAngle <= 1e-9)
                return Math.Max(1, maxSegments);

            var count = (int)Math.Ceiling(sweep / maxAngle);
            count = Math.Max(1, count);
            return Math.Min(Math.Max(1, maxSegments), count);
        }

        /// <summary>
        /// Returns segment samples including start and excluding end, so adjacent polyline
        /// segments can be concatenated without duplicate vertices.
        /// </summary>
        public static IReadOnlyList<Point2> SampleSegment(
            Point2 start,
            Point2 end,
            double bulge,
            double maxChordError = DefaultMaxChordError,
            int maxSegments = DefaultMaxSegments)
        {
            var count = GetSubdivisionCount(start, end, bulge, maxChordError, maxSegments);
            var result = new List<Point2>(count);

            if (Math.Abs(bulge) <= 1e-12 || start.DistanceTo(end) <= 1e-9)
            {
                result.Add(start);
                return result;
            }

            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var chord = Math.Sqrt(dx * dx + dy * dy);
            var midX = (start.X + end.X) * 0.5;
            var midY = (start.Y + end.Y) * 0.5;
            var leftX = -dy / chord;
            var leftY = dx / chord;
            var centerOffset = chord * (1.0 - bulge * bulge) / (4.0 * bulge);
            var centerX = midX + leftX * centerOffset;
            var centerY = midY + leftY * centerOffset;

            var radius = Math.Sqrt(
                (start.X - centerX) * (start.X - centerX) +
                (start.Y - centerY) * (start.Y - centerY));
            var startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
            var sweep = 4.0 * Math.Atan(bulge);

            for (var i = 0; i < count; i++)
            {
                var angle = startAngle + sweep * i / count;
                result.Add(new Point2(
                    centerX + radius * Math.Cos(angle),
                    centerY + radius * Math.Sin(angle)));
            }

            return result;
        }
    }
}
