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
    public sealed class LegacyAvoidanceParityTests
    {
        [TestMethod]
        public void MainOnlyAvoidance_BestEffortMatchesLispAndDoesNotCollapsePlan()
        {
            var settings = LegacyAvoidanceSettings();
            var context = MainOnlyObstacleContext();

            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000.0, 4000.0), settings, context);

            var mainY = MainCoordinates(plan);

            // Exact V6.7.2 adjust-grid best-effort result for the three projected XC bands.
            // The first XC is allowed to finish at 500 even though that exceeds max_O=400;
            // adjust-grid only repairs the minimum edge during its iterative phase and then
            // returns X instead of discarding the whole grid.
            CollectionAssert.AreEqual(
                new[] { 500.0, 1200.0, 2000.0, 2850.0, 3700.0 },
                mainY);

            Assert.IsTrue(plan.MainSegmentCount > 0, "Legacy XC avoidance must never collapse the complete XC grid because strict validation failed.");
            Assert.IsTrue(plan.FurringSegmentCount > 0, "A Main-only obstacle must not remove XP.");
            Assert.IsTrue(plan.HangerCount > 0, "Ty must still be rebuilt from the final Legacy XC geometry.");
        }

        [TestMethod]
        public void MainOnlyAvoidance_DoesNotChangeFurringGrid()
        {
            var settings = LegacyAvoidanceSettings();
            var boundary = Rectangle(6000.0, 4000.0);

            var baseline = new VxtPreviewPlanBuilder().Build(boundary, settings, new VxtLayoutContext());
            var obstructed = new VxtPreviewPlanBuilder().Build(boundary, settings, MainOnlyObstacleContext());

            var baselineXp = FurringCoordinates(baseline);
            var obstructedXp = FurringCoordinates(obstructed);

            Assert.IsTrue(baselineXp.Length > 0);
            CollectionAssert.AreEqual(
                baselineXp,
                obstructedXp,
                "MainObstacles are local_bboxes_xc in the Lisp and must never leak into the XP grid.");
        }

        [TestMethod]
        public void ShiftAll_WhenNoWholeGridOffsetExists_FallsBackToLispBestEffortInsteadOfEmpty()
        {
            var settings = LegacyAvoidanceSettings();
            settings.ShiftAllForAvoidance = true;
            settings.ClearanceDistance = 20.0;

            var context = new VxtLayoutContext();
            context.MainObstacles.Add(new Box2(1000.0, 250.0, 5000.0, 450.0));

            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000.0, 4000.0), settings, context);
            var mainY = MainCoordinates(plan);

            // 4000 has 300 mm at both ends, exactly the hard minimum, so a whole-grid
            // +/-50 shift is impossible. V6.7.2 therefore falls back to adjust-grid.
            // The first XC repeatedly snaps to 200 then is restored to the 300 minimum edge;
            // after 100 iterations Lisp returns the best-effort grid instead of nil/empty.
            CollectionAssert.AreEqual(
                new[] { 300.0, 1150.0, 2000.0, 2850.0, 3700.0 },
                mainY);
            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0, "Main-only ShiftAll fallback must not remove XP.");
            Assert.IsTrue(plan.HangerCount > 0, "Ty must remain available when ShiftAll falls back to Lisp best-effort repair.");
        }

        private static VxtSettings LegacyAvoidanceSettings()
            => new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                UseAvoidance = true,
                ShiftAllForAvoidance = false,
                ClearanceDistance = 0.0,
                AutoDimension = false
            };

        private static VxtLayoutContext MainOnlyObstacleContext()
        {
            var context = new VxtLayoutContext();
            context.MainObstacles.Add(new Box2(800.0, 0.0, 1800.0, 500.0));
            context.MainObstacles.Add(new Box2(2400.0, 2200.0, 3300.0, 2300.0));
            context.MainObstacles.Add(new Box2(4100.0, 3000.0, 5000.0, 3300.0));
            return context;
        }

        private static double[] MainCoordinates(VxtPreviewPlan plan)
            => plan.Lines
                .Where(line => line.Kind == PreviewLineKind.Main)
                .Select(line => Math.Round(line.A.Y, 3))
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

        private static double[] FurringCoordinates(VxtPreviewPlan plan)
            => plan.Lines
                .Where(line => line.Kind == PreviewLineKind.Furring)
                .Select(line => Math.Round(line.A.X, 6))
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

        private static Boundary2 Rectangle(double width, double height)
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(width, 0.0),
                new Point2(width, height),
                new Point2(0.0, height)
            });
    }
}
