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
        public void Block15Real_LocalOn_DoesNotKeepSubMinOverlappingXcRows()
        {
            var settings = FieldSettings();
            settings.UseLocalMainAdd = true;
            settings.DrawHangers = true;
            settings.AutoDimension = true;
            settings.DimMain = true;
            settings.DimHanger = true;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                RealProblemBoundaries(), settings, new VxtLayoutContext());

            AssertNoSubMinOverlappingMains(plan, settings.MainMinSpacing);
            AssertEveryHangerStillBelongsToAMain(plan);

            // The bad field output added rows such as -14523.113 next to the long -14173.113
            // XC and -17125.608 next to -16775.608. Those 350 mm pairs must never survive the
            // final automatic-notch safety pass.
            Assert.IsFalse(HasOverlappingPairNear(plan, -14523.1126400746, -14173.1126400746, 5.0));
            Assert.IsFalse(HasOverlappingPairNear(plan, -17125.6080527564, -16775.6080527564, 5.0));
        }

        [TestMethod]
        public void Block15Vs16Real_OverConstrainedShallowNotchesPreferContinuousGridOverExtraLocalBars()
        {
            var enabled = FieldSettings();
            enabled.UseLocalMainAdd = true;
            enabled.DrawHangers = false;
            enabled.AutoDimension = false;

            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;

            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                RealProblemBoundaries(), enabled, new VxtLayoutContext());
            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                RealProblemBoundaries(), disabled, new VxtLayoutContext());

            AssertNoSubMinOverlappingMains(onPlan, enabled.MainMinSpacing);
            Assert.AreEqual(
                offPlan.MainSegmentCount,
                onPlan.MainSegmentCount,
                "For the real block15/16 shallow-notch fixtures, local-edge repair is over-constrained. " +
                "ON must preserve the longer/continuous XC set instead of adding short XC below MainMinSpacing.");

            Assert.AreEqual(
                TotalMainLength(offPlan),
                TotalMainLength(onPlan),
                0.5,
                "The safe ON result should match the continuous OFF geometry for these four real shallow notches.");
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
            AssertNoSubMinOverlappingMains(plan, settings.MainMinSpacing);
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
                // DXF handle A9 - new block 15/16.
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

                // DXF handle AA.
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

                // DXF handle AB.
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

                // DXF handle A0.
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

        private static void AssertNoSubMinOverlappingMains(VxtPreviewPlan plan, double minSpacing)
        {
            var mains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToArray();
            for (var i = 0; i + 1 < mains.Length; i++)
            {
                var a = mains[i];
                var ay = (a.A.Y + a.B.Y) * 0.5;
                var ax1 = Math.Min(a.A.X, a.B.X);
                var ax2 = Math.Max(a.A.X, a.B.X);

                for (var j = i + 1; j < mains.Length; j++)
                {
                    var b = mains[j];
                    var by = (b.A.Y + b.B.Y) * 0.5;
                    var dy = Math.Abs(by - ay);
                    if (dy <= 0.5 || dy >= minSpacing - 0.1) continue;

                    var bx1 = Math.Min(b.A.X, b.B.X);
                    var bx2 = Math.Max(b.A.X, b.B.X);
                    var overlap = Math.Min(ax2, bx2) - Math.Max(ax1, bx1);
                    if (overlap <= 1.0) continue;

                    Assert.Fail(
                        "Real block15/16 fixture has overlapping XC rows below MainMinSpacing: dy=" +
                        dy.ToString("0.###") + ", overlap=" + overlap.ToString("0.###"));
                }
            }
        }

        private static bool HasOverlappingPairNear(VxtPreviewPlan plan, double y1, double y2, double tolerance)
        {
            var first = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main &&
                Math.Abs((x.A.Y + x.B.Y) * 0.5 - y1) <= tolerance).ToArray();
            var second = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main &&
                Math.Abs((x.A.Y + x.B.Y) * 0.5 - y2) <= tolerance).ToArray();

            foreach (var a in first)
            foreach (var b in second)
            {
                var overlap = Math.Min(Math.Max(a.A.X, a.B.X), Math.Max(b.A.X, b.B.X)) -
                              Math.Max(Math.Min(a.A.X, a.B.X), Math.Min(b.A.X, b.B.X));
                if (overlap > 1.0) return true;
            }
            return false;
        }

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
                }), "Removing an unsafe local XC must also remove its orphan Ty.");
            }
        }

        private static double TotalMainLength(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Sum(x => x.A.DistanceTo(x.B));
    }
}
