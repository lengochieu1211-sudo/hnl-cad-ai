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
    public sealed class VxtProAutoDirectionStabilityAuditTests
    {
        [TestMethod]
        public void ProAuto_SegmentedDominantAxis_DoesNotDriftToChamferAxis()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProEconomy,
                MainDirection = MainDirectionMode.Auto,
                AutoShadowline = true,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

            // Production-scale version of a segmented/chamfered ceiling. The lower construction
            // edge is split into many short collinear pieces while the upper return uses fewer,
            // individually longer chamfer/arc segments. Horizontal accumulated edge length is
            // still dominant and the 5.1 m depth is large enough for a legal XC grid at 0 degrees.
            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0), new Point2(500, 0), new Point2(1000, 0), new Point2(1500, 0),
                new Point2(2000, 0), new Point2(2500, 0), new Point2(3000, 0), new Point2(3500, 0),
                new Point2(4000, 0), new Point2(4500, 0), new Point2(5000, 0), new Point2(5500, 0),
                new Point2(6000, 0), new Point2(6500, 0), new Point2(7000, 0), new Point2(7500, 0),
                new Point2(8000, 0), new Point2(8500, 0), new Point2(9000, 0), new Point2(9500, 0),
                new Point2(10000, 0),
                new Point2(10500, 1000), new Point2(10200, 2200), new Point2(9500, 3400),
                new Point2(8300, 4300), new Point2(6800, 4900), new Point2(5000, 5100),
                new Point2(3200, 4900), new Point2(1700, 4300), new Point2(500, 3400),
                new Point2(-200, 2200), new Point2(-500, 1000)
            });

            var preferred = VxtProAutoDirectionPlanBuilder.ResolvePreferredAutoAngle(boundary, true, 0.0);
            Assert.IsTrue(AngularDistance180(preferred, 0.0) <= 0.01,
                "Audit fixture must have horizontal accumulated edge length as its preferred construction axis.");

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(0, plan.Quality.HardViolationCount,
                "Audit fixture must keep the preferred horizontal direction hard-valid.");
            Assert.IsTrue(
                AngularDistance180(plan.Quality.SelectedDirectionDegrees, preferred) <= 2.0,
                "Pro Auto must keep the accumulated dominant construction axis even when it is composed of many short segments.");
        }

        [TestMethod]
        public void ProAuto_ClearCeiling_PrefersExactNaturalAxisBeforeMaterialScore()
        {
            var modes = new[]
            {
                VxtOptimizationMode.ProBalanced,
                VxtOptimizationMode.ProEconomy,
                VxtOptimizationMode.ProConservative
            };

            foreach (var mode in modes)
            {
                var settings = new VxtSettings
                {
                    OptimizationMode = mode,
                    MainDirection = MainDirectionMode.Auto,
                    AutoShadowline = true,
                    DrawMain = true,
                    DrawFurring = true,
                    DrawHangers = true,
                    AutoDimension = false,
                    UseAvoidance = false,
                    MainSkipLimit = 0.0,
                    UseDynamicMainBlock = false,
                    UseDynamicFurringBlock = false
                };

                var boundary = RotatedRectangle(6400.0, 3900.0, 17.0);
                var preferred = VxtProAutoDirectionPlanBuilder.ResolvePreferredAutoAngle(boundary, true, 0.0);
                var plan = VxtMultiBoundaryPlanBuilder.Build(
                    new[] { boundary }, settings, new VxtLayoutContext());

                Assert.IsNotNull(plan.Quality, mode + " must return quality telemetry.");
                Assert.IsTrue(
                    AngularDistance180(plan.Quality.SelectedDirectionDegrees, preferred) <= 2.0,
                    mode + " must keep the exact natural axis while it is legal and collision-free; material score may optimize spacing/offset but not silently rotate the whole framing system.");
            }
        }

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
