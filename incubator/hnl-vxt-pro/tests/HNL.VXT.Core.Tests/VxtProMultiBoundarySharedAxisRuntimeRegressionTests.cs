using System;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProMultiBoundarySharedAxisRuntimeRegressionTests
    {
        [TestMethod]
        public void ProBalanced_ThreeCoaxialRotatedRectangles_PreferSharedAxisDeterministically()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProBalanced,
                MainDirection = MainDirectionMode.Auto,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

            var boundaries = new[]
            {
                RotatedRectangle(6000.0, 4000.0, 27.0, 0.0, 0.0),
                RotatedRectangle(5200.0, 3200.0, 27.0, 8000.0, 0.0),
                RotatedRectangle(4500.0, 2800.0, 27.0, 14500.0, 500.0)
            };

            for (var run = 0; run < 12; run++)
            {
                var plan = VxtMultiBoundaryPlanBuilder.Build(
                    boundaries,
                    settings,
                    new VxtLayoutContext());

                Assert.IsNotNull(plan.Quality, "Run " + run + ": quality telemetry is required.");
                Assert.AreEqual(0, plan.Quality.HardViolationCount,
                    "Run " + run + ": coaxial shared-axis plan must remain hard-valid.");
                Assert.AreEqual(0, plan.Quality.CollisionCount,
                    "Run " + run + ": coaxial shared-axis plan must remain collision-free.");
                Assert.AreEqual(3, plan.Quality.BoundaryCount,
                    "Run " + run + ": all three runtime-QA boundaries must participate.");
                Assert.AreEqual(1, plan.Quality.DistinctDirectionCount,
                    "Run " + run + ": the three 27-degree ceilings must resolve to one direction family.");
                Assert.AreEqual(100, plan.Quality.AlignmentScore100,
                    "Run " + run + ": the three coaxial ceilings must report Alignment 100.");
                Assert.IsTrue(plan.Quality.UsesSharedDirection,
                    "Run " + run + ": Pro Balanced must make the common axis explicit instead of falling back to an independent tie.");
            }
        }

        private static Boundary2 RotatedRectangle(
            double width,
            double height,
            double degrees,
            double offsetX,
            double offsetY)
        {
            var radians = degrees * Math.PI / 180.0;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);

            Point2 Rotate(double x, double y)
                => new Point2(
                    offsetX + x * cosine - y * sine,
                    offsetY + x * sine + y * cosine);

            return new Boundary2(new[]
            {
                Rotate(0.0, 0.0),
                Rotate(width, 0.0),
                Rotate(width, height),
                Rotate(0.0, height)
            });
        }
    }
}