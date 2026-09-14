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
    /// Field fixtures block15/block16 showed that AddLocalRepairs could satisfy a local
    /// Max-edge condition by adding a short XC only 150-400 mm from a much longer/global XC.
    /// That is worse for construction than keeping the continuous/global member and accepting
    /// the shallow notch as locally over-constrained. This pass therefore gives continuity and
    /// configured MainMinSpacing higher priority than an optional local notch bar.
    ///
    /// Manual RectangleRegions is intentionally excluded: those regions are user-authored and
    /// own their independent grids. The automatic/fixed-direction workflows are protected.
    /// </summary>
    internal static class VxtLocalMainSpacingSafety
    {
        private const double Tol = 0.5;
        private const double MinOverlap = 1.0;

        public static void Apply(VxtPreviewPlan plan, VxtSettings settings, double angleDegrees)
        {
            if (plan == null || settings == null) return;
            if (!settings.UseLocalMainAdd || settings.MainDirection == MainDirectionMode.RectangleRegions) return;
            if (settings.MainMinSpacing <= Tol) return;

            var radians = NormalizeDegrees(angleDegrees) * Math.PI / 180.0;

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

                        // Construction priority: keep the member with the greater continuous span.
                        // A local repair is normally the shorter member. For an exact tie do not make
                        // an arbitrary destructive choice; leave it for the strategy scorer/QA.
                        if (a.Length < b.Length - Tol)
                            victim = a;
                        else if (b.Length < a.Length - Tol)
                            victim = b;

                        if (victim != null) break;
                    }
                }

                if (victim == null) break;
                RemoveMainAndItsHangers(plan, victim, radians);
            }

            plan.MainSegmentCount = plan.Lines.Count(x => x.Kind == PreviewLineKind.Main);
            plan.HangerCount = plan.HangerPoints.Count;
        }

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
    }
}
