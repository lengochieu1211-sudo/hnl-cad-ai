using System;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProAutoDirectionQualityTests
    {
        [TestMethod]
        public void LegacyAutoRoute_RemainsCertifiedAndHasNoProTelemetry()
        {
            var settings = BaseSettings(VxtOptimizationMode.Legacy);
            settings.MainDirection = MainDirectionMode.Auto;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) }, settings, new VxtLayoutContext());

            Assert.AreEqual(5, plan.MainSegmentCount);
            Assert.IsNull(plan.Quality, "Legacy route must stay free of Pro scoring side effects.");
            Assert.IsFalse(plan.Texts.Any(x => x.Text != null && x.Text.StartsWith("HNL Pro Q", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void ProAuto_Shadowline_KeepsLongSideOrientationInsteadOfMaterialFlip()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.AutoShadowline = true;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.IsTrue(
                AngularDistance180(plan.Quality.SelectedDirectionDegrees, 0.0) <= 2.0,
                "Pro Auto must keep the legacy Shadowline long-side orientation when legal/clear; material score alone must not flip XC by 90 degrees.");
        }

        [TestMethod]
        public void ProAuto_NoShadowline_KeepsShortSideOrientationInsteadOfMaterialFlip()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.AutoShadowline = false;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.IsTrue(
                AngularDistance180(plan.Quality.SelectedDirectionDegrees, 90.0) <= 2.0,
                "Pro Auto must keep the legacy no-Shadowline short-side orientation when legal/clear.");
        }

        [TestMethod]
        public void ProAuto_AllProModes_KeepNaturalOrientationFamilyWhenClear()
        {
            var modes = new[]
            {
                VxtOptimizationMode.ProBalanced,
                VxtOptimizationMode.ProEconomy,
                VxtOptimizationMode.ProConservative
            };

            foreach (var mode in modes)
            {
                var shadowline = BaseSettings(mode);
                shadowline.MainDirection = MainDirectionMode.Auto;
                shadowline.AutoShadowline = true;
                var longSide = VxtMultiBoundaryPlanBuilder.Build(
                    new[] { Rectangle(6000.0, 4000.0) }, shadowline, new VxtLayoutContext());

                Assert.IsNotNull(longSide.Quality, mode + " should return Pro quality telemetry.");
                Assert.IsTrue(
                    AngularDistance180(longSide.Quality.SelectedDirectionDegrees, 0.0) <= 2.0,
                    mode + " must not rotate a clear Shadowline ceiling 90 degrees for scoring/material reasons.");

                var noShadowline = BaseSettings(mode);
                noShadowline.MainDirection = MainDirectionMode.Auto;
                noShadowline.AutoShadowline = false;
                var shortSide = VxtMultiBoundaryPlanBuilder.Build(
                    new[] { Rectangle(6000.0, 4000.0) }, noShadowline, new VxtLayoutContext());

                Assert.IsNotNull(shortSide.Quality, mode + " should return Pro quality telemetry.");
                Assert.IsTrue(
                    AngularDistance180(shortSide.Quality.SelectedDirectionDegrees, 90.0) <= 2.0,
                    mode + " must preserve the no-Shadowline short-side orientation when clear.");
            }
        }

        [TestMethod]
        public void ProAuto_RotatedRectangle_UsesPolygonAxisCandidate_NotOnlyGlobalZeroNinety()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            var boundary = RotatedRectangle(6000.0, 4000.0, 30.0);

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.IsTrue(plan.Quality.AutoDirectionCandidateCount >= 4);
            var angle = plan.Quality.SelectedDirectionDegrees;
            var aligned = AngularDistance180(angle, 30.0) <= 2.0 || AngularDistance180(angle, 120.0) <= 2.0;
            Assert.IsTrue(aligned, "Pro Auto should discover a dominant rotated polygon axis.");
            Assert.IsTrue(plan.Texts.Any(x => x.Text != null && x.Text.StartsWith("HNL Pro Q", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void ProAuto_RotatedRectangle_ShadowlinePrefersActualLongAxisFamily()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.AutoShadowline = true;
            var boundary = RotatedRectangle(6000.0, 4000.0, 30.0);

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.IsTrue(
                AngularDistance180(plan.Quality.SelectedDirectionDegrees, 30.0) <= 2.0,
                "Rotated ceiling must stay on its actual long-axis family instead of flipping to the perpendicular axis for material score.");
        }

        [TestMethod]
        public void ProAuto_RotatedRectangle_NoShadowlinePrefersActualShortAxisFamily()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.AutoShadowline = false;
            var boundary = RotatedRectangle(6000.0, 4000.0, 30.0);

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.IsTrue(
                AngularDistance180(plan.Quality.SelectedDirectionDegrees, 120.0) <= 2.0,
                "Rotated ceiling without Shadowline must stay on its actual short-axis family instead of reverting to a global 0/90 axis.");
        }

        [TestMethod]
        public void ProQuality_MainObstacleShift_IsClearAndScores100()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Horizontal;
            settings.UseAvoidance = true;
            settings.ShiftAllForAvoidance = true;
            settings.ClearanceDistance = 0.0;
            var context = new VxtLayoutContext();
            context.MainObstacles.Add(new Box2(0.0, 325.0, 6000.0, 375.0));

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4100.0) }, settings, context);

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(0, plan.Quality.HardViolationCount);
            Assert.AreEqual(0, plan.Quality.CollisionCount);
            Assert.AreEqual(100, plan.Quality.QualityScore100);
        }

        [TestMethod]
        public void ProAuto_ConcaveCeiling_ReturnsQualityAndGeometry()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.DrawFurring = true;
            settings.DrawHangers = true;
            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(7000, 0),
                new Point2(7000, 2600),
                new Point2(4200, 2600),
                new Point2(4200, 5200),
                new Point2(0, 5200)
            });

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0);
            Assert.IsTrue(plan.HangerCount > 0);
            Assert.IsTrue(plan.Quality.AutoDirectionCandidateCount >= 2);
            Assert.AreEqual(0, plan.Quality.HardViolationCount);
        }

        [TestMethod]
        public void CandidateAngles_IncludeDominantEdgeAndPerpendicular()
        {
            var boundary = RotatedRectangle(6000.0, 4000.0, 27.0);
            var angles = VxtProAutoDirectionPlanBuilder.BuildCandidateAngles(boundary, 0.0);

            Assert.IsTrue(angles.Any(x => AngularDistance180(x, 27.0) <= 2.0));
            Assert.IsTrue(angles.Any(x => AngularDistance180(x, 117.0) <= 2.0));
            Assert.IsTrue(angles.Any(x => AngularDistance180(x, 0.0) <= 0.01));
            Assert.IsTrue(angles.Any(x => AngularDistance180(x, 90.0) <= 0.01));
        }

        private static VxtSettings BaseSettings(VxtOptimizationMode mode)
            => new VxtSettings
            {
                OptimizationMode = mode,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

        private static Boundary2 Rectangle(double width, double height)
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(width, 0.0),
                new Point2(width, height),
                new Point2(0.0, height)
            });

        private static Boundary2 RotatedRectangle(double width, double height, double degrees)
        {
            var r = degrees * Math.PI / 180.0;
            var c = Math.Cos(r);
            var s = Math.Sin(r);
            Point2 Rotate(double x, double y) => new Point2(x * c - y * s, x * s + y * c);
            return new Boundary2(new[]
            {
                Rotate(0.0, 0.0),
                Rotate(width, 0.0),
                Rotate(width, height),
                Rotate(0.0, height)
            });
        }

        private static double AngularDistance180(double a, double b)
        {
            a %= 180.0; if (a < 0.0) a += 180.0;
            b %= 180.0; if (b < 0.0) b += 180.0;
            var d = Math.Abs(a - b);
            return Math.Min(d, 180.0 - d);
        }
    }
}
