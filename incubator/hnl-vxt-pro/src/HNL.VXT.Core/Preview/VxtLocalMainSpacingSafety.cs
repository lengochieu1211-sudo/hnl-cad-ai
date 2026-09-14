using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Final construction-safety pass for automatic concave/notch local XC.
    ///
    /// MainMinSpacing is the preferred spacing between normal/global XC rows. A short local XC
    /// added at a concave edge has a different purpose: it may be required to satisfy the maximum
    /// distance from the nearby ceiling boundary or a local maximum XC gap. Therefore a local row
    /// is allowed to sit below MainMinSpacing when removing it would re-introduce a MaxEdge or
    /// MainMaxSpacing violation in the actual polygon band it supports.
    ///
    /// Only redundant short/local XC is removed. Manual RectangleRegions remains user-authored and
    /// is intentionally excluded from this automatic safety pass.
    /// </summary>
    internal static class VxtLocalMainSpacingSafety
    {
        private const double Tol = 0.5;
        private const double MinOverlap = 1.0;

        public static void Apply(
            Boundary2 boundary,
            VxtPreviewPlan plan,
            VxtSettings settings,
            double angleDegrees)
        {
            if (boundary == null || plan == null || settings == null) return;
            if (!settings.UseLocalMainAdd || settings.MainDirection == MainDirectionMode.RectangleRegions) return;
            if (settings.MainMinSpacing <= Tol) return;

            var radians = NormalizeDegrees(angleDegrees) * Math.PI / 180.0;
            var polygon = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();

            while (true)
            {
                var mains = BuildMainRecords(plan, radians);
                MainRecord victim = null;

                for (var i = 0; i + 1 < mains.Count && victim == null; i++)
                {
                    for (var j = i + 1; j < mains.Count; j++)
                    {
                        var a = mains[i];
                        var b = mains[j];
                        var dy = Math.Abs(a.Y - b.Y);
                        if (dy <= Tol || dy >= settings.MainMinSpacing - Tol) continue;

                        var overlap = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
                        if (overlap <= MinOverlap) continue;

                        // Prefer the longer/continuous row, but never delete a short notch row that
                        // is actually required to satisfy MaxEdge or MainMaxSpacing in its local band.
                        MainRecord candidate = null;
                        if (a.Length < b.Length - Tol)
                            candidate = a;
                        else if (b.Length < a.Length - Tol)
                            candidate = b;

                        if (candidate == null) continue;
                        if (IsRequiredForHardMaxConstraint(candidate, mains, polygon, settings))
                            continue;

                        victim = candidate;
                        break;
                    }
                }

                if (victim == null) break;
                RemoveMainAndItsHangers(plan, victim, radians);
            }

            plan.MainSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Main);
            plan.HangerCount = plan.HangerPoints.Count;
        }

        private static bool IsRequiredForHardMaxConstraint(
            MainRecord candidate,
            IReadOnlyList<MainRecord> mains,
            IReadOnlyList<Point2> polygon,
            VxtSettings settings)
        {
            var width = candidate.X2 - candidate.X1;
            if (width <= MinOverlap) return false;

            var sampleXs = new[]
            {
                candidate.X1 + width * 0.20,
                candidate.X1 + width * 0.50,
                candidate.X1 + width * 0.80
            };

            foreach (var x in sampleXs)
            {
                var ceilingIntervals = PolygonScanline.ClipVertical(polygon, x).ToList();
                foreach (var interval in ceilingIntervals)
                {
                    var a = Math.Min(interval.A.Y, interval.B.Y);
                    var b = Math.Max(interval.A.Y, interval.B.Y);
                    if (candidate.Y < a - Tol || candidate.Y > b + Tol) continue;

                    var withCandidate = mains
                        .Where(m => CrossesX(m, x) && m.Y >= a - Tol && m.Y <= b + Tol)
                        .Select(m => m.Y)
                        .Distinct(new DoubleTolComparer())
                        .OrderBy(y => y)
                        .ToList();

                    var withoutCandidate = mains
                        .Where(m => !ReferenceEquals(m, candidate) && CrossesX(m, x) && m.Y >= a - Tol && m.Y <= b + Tol)
                        .Select(m => m.Y)
                        .Distinct(new DoubleTolComparer())
                        .OrderBy(y => y)
                        .ToList();

                    var withViolations = CountHardMaxViolations(withCandidate, a, b, settings);
                    var withoutViolations = CountHardMaxViolations(withoutCandidate, a, b, settings);
                    if (withoutViolations > withViolations)
                        return true;
                }
            }

            return false;
        }

        private static int CountHardMaxViolations(
            IReadOnlyList<double> ys,
            double edgeA,
            double edgeB,
            VxtSettings settings)
        {
            if (ys == null || ys.Count == 0) return 1;

            var violations = 0;
            var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
            if (ys[0] - edgeA > maxEdge + Tol) violations++;
            if (edgeB - ys[ys.Count - 1] > maxEdge + Tol) violations++;

            for (var i = 0; i + 1 < ys.Count; i++)
                if (ys[i + 1] - ys[i] > settings.MainMaxSpacing + Tol)
                    violations++;

            return violations;
        }

        private static bool CrossesX(MainRecord main, double x)
            => x >= main.X1 - Tol && x <= main.X2 + Tol;

        private static List<MainRecord> BuildMainRecords(VxtPreviewPlan plan, double radians)
        {
            var result = new List<MainRecord>();
            for (var index = 0; index < plan.Lines.Count; index++)
            {
                var line = plan.Lines[index];
                if (line.Kind != PreviewLineKind.Main) continue;

                var a = Transform2.ToLocal(line.A, radians);
                var b = Transform2.ToLocal(line.B, radians);
                if (Math.Abs(a.Y - b.Y) > Tol) continue;

                result.Add(new MainRecord(
                    index,
                    (a.Y + b.Y) * 0.5,
                    Math.Min(a.X, b.X),
                    Math.Max(a.X, b.X)));
            }
            return result;
        }

        private static void RemoveMainAndItsHangers(VxtPreviewPlan plan, MainRecord victim, double radians)
        {
            var removedHangers = plan.HangerPoints
                .Where(point => PointBelongsToMain(point, victim, radians))
                .ToList();

            if (victim.LineIndex >= 0 && victim.LineIndex < plan.Lines.Count)
                plan.Lines.RemoveAt(victim.LineIndex);

            if (removedHangers.Count == 0) return;

            plan.HangerPoints.RemoveAll(point =>
                removedHangers.Any(removed => removed.DistanceTo(point) <= 0.01));

            plan.Lines.RemoveAll(line =>
            {
                if (line.Kind != PreviewLineKind.Hanger) return false;
                var midpoint = new Point2(
                    (line.A.X + line.B.X) * 0.5,
                    (line.A.Y + line.B.Y) * 0.5);
                return removedHangers.Any(point => point.DistanceTo(midpoint) <= 0.01);
            });
        }

        private static bool PointBelongsToMain(Point2 worldPoint, MainRecord main, double radians)
        {
            var local = Transform2.ToLocal(worldPoint, radians);
            return Math.Abs(local.Y - main.Y) <= Tol &&
                   local.X >= main.X1 - Tol && local.X <= main.X2 + Tol;
        }

        private static double NormalizeDegrees(double value)
        {
            value %= 360.0;
            return value < 0.0 ? value + 360.0 : value;
        }

        private sealed class MainRecord
        {
            public MainRecord(int lineIndex, double y, double x1, double x2)
            {
                LineIndex = lineIndex;
                Y = y;
                X1 = x1;
                X2 = x2;
            }

            public int LineIndex { get; }
            public double Y { get; }
            public double X1 { get; }
            public double X2 { get; }
            public double Length => X2 - X1;
        }

        private sealed class DoubleTolComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) <= Tol;
            public int GetHashCode(double obj) => Math.Round(obj / Tol).GetHashCode();
        }
    }
}
