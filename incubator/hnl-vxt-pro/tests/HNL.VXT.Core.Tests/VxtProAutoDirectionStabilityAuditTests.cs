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

            // The lower construction edge is deliberately split into many short collinear
            // segments. The upper return uses fewer, individually longer chamfer/arc segments.
            // Total horizontal edge length is still dominant. Auto must not lose the 0-degree
            // construction axis merely because candidate truncation sees the longer individual
            // chamfer segments first.
            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0), new Point2(50, 0), new Point2(100, 0), new Point2(150, 0),
                new Point2(200, 0), new Point2(250, 0), new Point2(300, 0), new Point2(350, 0),
                new Point2(400, 0), new Point2(450, 0), new Point2(500, 0), new Point2(550, 0),
                new Point2(600, 0), new Point2(650, 0), new Point2(700, 0), new Point2(750, 0),
                new Point2(800, 0), new Point2(850, 0), new Point2(900, 0), new Point2(950, 0),
                new Point2(1000, 0),
                new Point2(1050, 100), new Point2(1020, 220), new Point2(950, 340),
                new Point2(830, 430), new Point2(680, 490), new Point2(500, 510),
                new Point2(320, 490), new Point2(170, 430), new Point2(50, 340),
                new Point2(-20, 220), new Point2(-50, 100)
            });

            var preferred = VxtProAutoDirectionPlanBuilder.ResolvePreferredAutoAngle(boundary, true, 0.0);
            Assert.IsTrue(AngularDistance180(preferred, 0.0) <= 0.01,
                "Audit fixture must have horizontal accumulated edge length as its preferred construction axis.");

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
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
