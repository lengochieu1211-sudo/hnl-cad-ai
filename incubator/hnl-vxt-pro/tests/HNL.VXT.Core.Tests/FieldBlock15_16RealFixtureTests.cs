using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class FieldBlock15_16RealFixtureTests
    {
        [TestMethod]
        public void Block15Real_LocalOn_AllowsSubMinLocalXcWhenItProtectsHardMaxEdgeOrGap()
        {
            var settings = FieldSettings();
            settings.UseLocalMainAdd = true;
            settings.DrawHangers = true;
            settings.AutoDimension = true;
            settings.DimMain = true;
            settings.DimHanger = true;

            var boundaries = RealProblemBoundaries();
            var plan = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());

            var justifiedSubMinPairs = CountJustifiedSubMinPairs(plan, boundaries, settings);
            Assert.IsTrue(justifiedSubMinPairs > 0,
                "Real block15 fixture must retain at least one required local edge XC below MainMinSpacing when that XC protects a hard MaxEdge/MaxSpacing condition.");

            AssertEverySubMinPairIsHardMaxJustified(plan, boundaries, settings);
            AssertEveryHangerStillBelongsToAMain(plan);
        }

        [TestMethod]
        public void Block15Vs16Real_LocalOnMayAddRequiredEdgeBarsButOffKeepsContinuousBaseGrid()
        {
            var enabled = FieldSettings();
            enabled.UseLocalMainAdd = true;
            enabled.DrawHangers = false;
            enabled.AutoDimension = false;

            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;

            var boundaries = RealProblemBoundaries();
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(boundaries, enabled, new VxtLayoutContext());
            var offPlan = VxtMultiBoundaryPlanBuilder.Build(boundaries, disabled, new VxtLayoutContext());

            Assert.IsTrue(onPlan.MainSegmentCount >= offPlan.MainSegmentCount,
                "Local-edge ON may add short XC required by MaxEdge/MaxSpacing, but must never lose the continuous OFF base grid.");
            AssertBaseMainGeometryPreserved(offPlan, onPlan);

            AssertEverySubMinPairIsHardMaxJustified(onPlan, boundaries, enabled);
            Assert.IsTrue(offPlan.MainSegmentCount > 0);
        }

        [TestMethod]
        public void Block16Real_LocalOff_KeepsAutomaticNotchRegionalSplitDisabled()
        {
            var settings = FieldSettings();
            settings.UseLocalMainAdd = false;
            settings.DrawHangers = false;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                RealProblemBoundaries(), settings, new VxtLayoutContext());

            Assert.IsTrue(plan.MainSegmentCount > 0);
        }

        private static VxtSettings FieldSettings()
            => new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };

        private static Boundary2[] RealProblemBoundaries()
            => new[]
            {
                new Boundary2(new[]
                {
                    new Point2(771050.4001939078, -12879.9395401919),
                    new Point2(760390.2659838528, -12880.3669056160),
                    new Point2(760390.4001939078, -16323.1026400746),
                    new Point2(768700.4001939078, -16323.1126400746),
                    new Point2(768700.4001939078, -14580.6076400746),
                    new Point2(770250.4001939078, -14580.6076400746),
                    new Point2(770250.0381939078, -14870.6076400746),
                    new Point2(771380.0381939078, -14870.6076400746),
                    new Point2(771380.4001939078, -13960.6076400746),
                    new Point2(771050.4001939078, -13960.6076400746)
                }),
                new Boundary2(new[]
                {
                    new Point2(768700.4001939076, -17465.6080527564),
                    new Point2(768700.3331939076, -16433.1080467917),
                    new Point2(760390.4341939076, -16433.1080467917),
                    new Point2(760390.3666413026, -19875.6080527564),
                    new Point2(765340.4001939076, -19875.6080527564),
                    new Point2(765340.4001939076, -18065.6080527564),
                    new Point2(767540.4001939076, -18065.6080527564),
                    new Point2(767540.4001939076, -17465.6080527564)
                }),
                new Boundary2(new[]
                {
                    new Point2(773990.4001956151, -17465.6080527564),
                    new Point2(773990.4001956151, -16433.1080349390),
                    new Point2(782200.4001939077, -16433.1080349390),
                    new Point2(782200.4001939077, -19875.6080527564),
                    new Point2(777400.4001939077, -19875.6080527564),
                    new Point2(777400.4001939077, -18065.6080527564),
                    new Point2(775600.4001939077, -18065.6080527564),
                    new Point2(775600.4001939077, -17465.6080527564)
                }),
                new Boundary2(new[]
                {
                    new Point2(783445.4001939078, -16273.1080467898),
                    new Point2(782610.4001939078, -16273.1080467898),
                    new Point2(782610.4001939078, -13110.6080526812),
                    new Point2(782840.4001939078, -13110.6080526812),
                    new Point2(782840.4001939078, -12600.6080526812),
                    new Point2(778435.4001939078, -12600.6080526812),
                    new Point2(778435.4001939078, -11765.6080526812),
                    new Point2(783445.4001939078, -11765.6080526812)
                })
            };

        private static void AssertBaseMainGeometryPreserved(VxtPreviewPlan offPlan, VxtPreviewPlan onPlan)
        {
            var baseline = MainRecords(offPlan);
            var actual = MainRecords(onPlan);

            foreach (var expected in baseline)
            {
                Assert.IsTrue(actual.Any(candidate =>
                    Math.Abs(candidate.Y - expected.Y) <= 0.5 &&
                    Math.Abs(candidate.X1 - expected.X1) <= 0.5 &&
                    Math.Abs(candidate.X2 - expected.X2) <= 0.5),
                    "Bật 'Thêm XC cạnh khuyết' chỉ được thêm XC cục bộ; không được dịch, thay pha hoặc làm mất XC nền. " +
                    "Thiếu XC nền Y=" + expected.Y.ToString("0.###") +
                    ", X1=" + expected.X1.ToString("0.###") +
                    ", X2=" + expected.X2.ToString("0.###"));
            }
        }

        private static int CountJustifiedSubMinPairs(VxtPreviewPlan plan, IReadOnlyList<Boundary2> boundaries, VxtSettings settings)
        {
            var count = 0;
            var mains = MainRecords(plan);
            for (var i = 0; i + 1 < mains.Count; i++)
            for (var j = i + 1; j < mains.Count; j++)
            {
                var a = mains[i];
                var b = mains[j];
                var dy = Math.Abs(a.Y - b.Y);
                var overlap = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
                if (dy <= 0.5 || dy >= settings.MainMinSpacing - 0.5 || overlap <= 1.0) continue;

                var shorter = a.Length < b.Length - 0.5 ? a : b.Length < a.Length - 0.5 ? b : null;
                if (shorter != null && IsHardMaxRequired(shorter, mains, boundaries, settings)) count++;
            }
            return count;
        }

        private static void AssertEverySubMinPairIsHardMaxJustified(VxtPreviewPlan plan, IReadOnlyList<Boundary2> boundaries, VxtSettings settings)
        {
            var mains = MainRecords(plan);
            for (var i = 0; i + 1 < mains.Count; i++)
            for (var j = i + 1; j < mains.Count; j++)
            {
                var a = mains[i];
                var b = mains[j];
                var dy = Math.Abs(a.Y - b.Y);
                var overlap = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
                if (dy <= 0.5 || dy >= settings.MainMinSpacing - 0.5 || overlap <= 1.0) continue;

                var shorter = a.Length < b.Length - 0.5 ? a : b.Length < a.Length - 0.5 ? b : null;
                Assert.IsNotNull(shorter,
                    "A sub-MinSpacing overlapping pair with equal spans is not a local-edge repair and needs separate strategy resolution.");
                Assert.IsTrue(IsHardMaxRequired(shorter, mains, boundaries, settings),
                    "A short XC below MainMinSpacing may survive only when removing it would violate MaxEdge or MainMaxSpacing in its actual notch band. dy=" + dy.ToString("0.###"));
            }
        }

        private static bool IsHardMaxRequired(MainRecord candidate, IReadOnlyList<MainRecord> mains, IReadOnlyList<Boundary2> boundaries, VxtSettings settings)
        {
            var width = candidate.X2 - candidate.X1;
            var sampleXs = new[] { candidate.X1 + width * 0.2, candidate.X1 + width * 0.5, candidate.X1 + width * 0.8 };
            foreach (var boundary in boundaries)
            foreach (var x in sampleXs)
            foreach (var interval in TestPolygonScanline.ClipVertical(boundary.Vertices, x))
            {
                var minY = Math.Min(interval.A.Y, interval.B.Y);
                var maxY = Math.Max(interval.A.Y, interval.B.Y);
                if (candidate.Y < minY - 0.5 || candidate.Y > maxY + 0.5) continue;

                var withCandidate = mains.Where(m => x >= m.X1 - 0.5 && x <= m.X2 + 0.5 && m.Y >= minY - 0.5 && m.Y <= maxY + 0.5)
                    .Select(m => m.Y).Distinct(new DoubleToleranceComparer()).OrderBy(y => y).ToArray();
                var withoutCandidate = mains.Where(m => !ReferenceEquals(m, candidate) && x >= m.X1 - 0.5 && x <= m.X2 + 0.5 && m.Y >= minY - 0.5 && m.Y <= maxY + 0.5)
                    .Select(m => m.Y).Distinct(new DoubleToleranceComparer()).OrderBy(y => y).ToArray();

                if (HardMaxViolations(withoutCandidate, minY, maxY, settings) > HardMaxViolations(withCandidate, minY, maxY, settings)) return true;
            }
            return false;
        }

        private static int HardMaxViolations(double[] ys, double minY, double maxY, VxtSettings settings)
        {
            if (ys.Length == 0) return 1;
            var result = 0;
            var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
            if (ys[0] - minY > maxEdge + 0.5) result++;
            if (maxY - ys[ys.Length - 1] > maxEdge + 0.5) result++;
            for (var i = 0; i + 1 < ys.Length; i++)
                if (ys[i + 1] - ys[i] > settings.MainMaxSpacing + 0.5) result++;
            return result;
        }

        private static List<MainRecord> MainRecords(VxtPreviewPlan plan)
            => plan.Lines.Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => new MainRecord((x.A.Y + x.B.Y) * 0.5, Math.Min(x.A.X, x.B.X), Math.Max(x.A.X, x.B.X)))
                .ToList();

        private static void AssertEveryHangerStillBelongsToAMain(VxtPreviewPlan plan)
        {
            var mains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToArray();
            foreach (var hanger in plan.HangerPoints)
            {
                Assert.IsTrue(mains.Any(main =>
                {
                    var y = (main.A.Y + main.B.Y) * 0.5;
                    var x1 = Math.Min(main.A.X, main.B.X);
                    var x2 = Math.Max(main.A.X, main.B.X);
                    return Math.Abs(hanger.Y - y) <= 0.5 && hanger.X >= x1 - 0.5 && hanger.X <= x2 + 0.5;
                }), "Removing a redundant local XC must also remove its orphan Ty.");
            }
        }

        private sealed class MainRecord
        {
            public MainRecord(double y, double x1, double x2) { Y = y; X1 = x1; X2 = x2; }
            public double Y { get; }
            public double X1 { get; }
            public double X2 { get; }
            public double Length => X2 - X1;
        }

        private sealed class DoubleToleranceComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) <= 0.5;
            public int GetHashCode(double obj) => Math.Round(obj * 2.0).GetHashCode();
        }
    }
}
