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

            // Exact V6.7.2 best-effort result when the ceiling grid phase is the WCS-zero phase.
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
            // +/-50 shift is impossible. Legacy therefore falls back to best-effort adjust-grid
            // and keeps a usable grid instead of returning empty.
            CollectionAssert.AreEqual(
                new[] { 300.0, 1150.0, 2000.0, 2850.0, 3700.0 },
                mainY);
            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0, "Main-only ShiftAll fallback must not remove XP.");
            Assert.IsTrue(plan.HangerCount > 0, "Ty must remain available when ShiftAll falls back to Legacy best-effort repair.");
        }

        [TestMethod]
        public void FieldDxf_OffsetWorldOrigin_MainAvoidanceKeepsConfigured50MillimetreGridPhase()
        {
            // Regression from new block11.dxf. The field drawing lives at a large, non-round WCS
            // coordinate. The previous absolute-zero snap mixed the ceiling phase with the WCS
            // 50 mm phase and produced XC DIM values such as 823.113 and 876.887 mm.
            const double minX = 760390.265984;
            const double minY = -16323.112640;
            const double width = 10990.0;
            const double height = 4000.0;
            const double increment = 50.0;

            var settings = LegacyAvoidanceSettings();
            settings.MainBalanceStep = increment;

            var context = new VxtLayoutContext();
            // Bands deliberately intersect interior ideal XC rows. Their WCS coordinates are not
            // on the same 50 mm phase as the ceiling-origin grid.
            context.MainObstacles.Add(new Box2(minX + 1000.0, -15200.0, minX + 2500.0, -15100.0));
            context.MainObstacles.Add(new Box2(minX + 3200.0, -14350.0, minX + 4700.0, -14250.0));
            context.MainObstacles.Add(new Box2(minX + 5500.0, -13500.0, minX + 7000.0, -13400.0));

            var boundary = RectangleAt(minX, minY, width, height);
            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings, context);
            var mainY = MainCoordinatesRaw(plan);

            Assert.IsTrue(mainY.Length >= 4, "Field-offset fixture must retain a usable XC grid.");
            for (var i = 1; i < mainY.Length; i++)
            {
                var spacing = mainY[i] - mainY[i - 1];
                var units = spacing / increment;
                Assert.AreEqual(
                    Math.Round(units),
                    units,
                    1e-6,
                    "Every final XC-to-XC spacing after avoidance must remain on the configured 50 mm grid phase. Actual spacing=" + spacing.ToString("0.######"));
            }

            // All repaired XC coordinates must stay on one common phase, even though that phase
            // is intentionally not WCS zero.
            var phaseOrigin = mainY[0];
            foreach (var y in mainY)
            {
                var units = (y - phaseOrigin) / increment;
                Assert.AreEqual(Math.Round(units), units, 1e-6);
            }

            Assert.IsFalse(mainY.Zip(mainY.Skip(1), (a, b) => b - a)
                .Any(spacing => Math.Abs(spacing - 823.112640) < 0.01 || Math.Abs(spacing - 876.887360) < 0.01),
                "The exact odd field spacings from new block11.dxf must not recur.");
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
            => MainCoordinatesRaw(plan)
                .Select(value => Math.Round(value, 3))
                .ToArray();

        private static double[] MainCoordinatesRaw(VxtPreviewPlan plan)
            => plan.Lines
                .Where(line => line.Kind == PreviewLineKind.Main)
                .Select(line => line.A.Y)
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
            => RectangleAt(0.0, 0.0, width, height);

        private static Boundary2 RectangleAt(double minX, double minY, double width, double height)
            => new Boundary2(new[]
            {
                new Point2(minX, minY),
                new Point2(minX + width, minY),
                new Point2(minX + width, minY + height),
                new Point2(minX, minY + height)
            });
    }
}
