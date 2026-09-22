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
    public sealed class VxtProBuilderIntegrationTests
    {
        [TestMethod]
        public void MultiBoundary_LegacyRoute_PreservesCertifiedGoldenCounts()
        {
            var settings = GoldenSettings(VxtOptimizationMode.Legacy);
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) },
                settings,
                new VxtLayoutContext());

            Assert.AreEqual(5, plan.MainSegmentCount);
            Assert.AreEqual(14, plan.FurringSegmentCount);
            Assert.AreEqual(35, plan.HangerCount);
            Assert.AreEqual(29, plan.DimensionSegmentCount);
        }

        [TestMethod]
        public void ProEconomy_GoldenRectangle_PreservesRuntimeGoldenCounts()
        {
            var pro = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) },
                GoldenSettings(VxtOptimizationMode.ProEconomy),
                new VxtLayoutContext());

            Assert.AreEqual(5, pro.MainSegmentCount,
                "Pro Economy must not add XC on a clear Golden rectangle.");
            Assert.AreEqual(14, pro.FurringSegmentCount,
                "Pro Economy must preserve the economical XP member count.");
            Assert.AreEqual(35, pro.HangerCount,
                "Pro Economy must not add Ty when the Legacy chain is already clear and legal.");
            Assert.AreEqual(29, pro.DimensionSegmentCount,
                "Pro Runtime Golden uses the same deterministic 5/14/35/29 DB contract.");
        }

        [TestMethod]
        public void ProEconomy_MainObstacle_ProducesNoXCInsideClearanceBand_WhenLegalShiftExists()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProEconomy,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = true,
                ShiftAllForAvoidance = true,
                ClearanceDistance = 0.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainBalanceStep = 50.0
            };
            var context = new VxtLayoutContext();
            // Height 4100 gives legal edge slack. Block the Legacy-balanced first XC band.
            context.MainObstacles.Add(new Box2(0.0, 325.0, 6000.0, 375.0));

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4100.0) },
                settings,
                context);

            var mainYs = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => (x.A.Y + x.B.Y) * 0.5)
                .ToArray();

            Assert.IsTrue(mainYs.Length > 0);
            Assert.IsTrue(mainYs.All(y => y <= 325.0 + 0.1 || y >= 375.0 - 0.1),
                "Pro XC grid must use legal edge slack before accepting an obstacle collision.");
        }

        [TestMethod]
        public void ProEconomy_FurringOnlyAvoidance_RepairsXpWithoutChangingMainGrid()
        {
            var settings = GoldenSettings(VxtOptimizationMode.ProEconomy);
            settings.AutoDimension = false;
            settings.UseAvoidance = true;
            settings.ShiftAllForAvoidance = false;
            settings.ClearanceDistance = 0.0;

            var boundary = Rectangle(6000.0, 4000.0);
            var baseline = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            var xp0 = FurringCoordinates(baseline).First();
            var context = new VxtLayoutContext();
            context.FurringObstacles.Add(new Box2(xp0 - 10.0, 0.0, xp0 + 10.0, 4000.0));
            var obstructed = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, context);

            CollectionAssert.AreEqual(
                MainCoordinates(baseline),
                MainCoordinates(obstructed),
                "XP-only equipment must not change the XC grid in Pro mode.");

            CollectionAssert.AreNotEqual(
                FurringCoordinates(baseline),
                FurringCoordinates(obstructed),
                "XP-only equipment must actively repair the XP grid in Pro mode.");
        }

        [TestMethod]
        public void ProEconomy_GeneralAvoidance_ShiftAllOff_ClearsXpAndPreservesMemberCount()
        {
            var settings = GoldenSettings(VxtOptimizationMode.ProEconomy);
            settings.AutoDimension = false;
            settings.UseAvoidance = true;
            settings.ShiftAllForAvoidance = false;
            settings.ClearanceDistance = 0.0;
            settings.MainDirection = MainDirectionMode.Horizontal;

            var boundary = Rectangle(6000.0, 4100.0);
            var baseline = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            var context = new VxtLayoutContext();
            context.GeneralObstacles.Add(new Box2(390.0, 325.0, 425.0, 375.0));
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, context);

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(0, plan.Quality.FurringCollisionCount,
                "ShiftAll OFF must locally move the colliding XP clear of General MEP.");
            Assert.AreEqual(baseline.FurringSegmentCount, plan.FurringSegmentCount,
                "Local XP repair must preserve the full XP member count.");
            Assert.AreEqual(
                plan.FurringSegmentCount,
                FurringCoordinates(plan).Length,
                "Local XP repair must not collapse two XP rows onto one axis.");
        }

        [TestMethod]
        public void ProEconomy_CombinedMainAndFurringAvoidance_AppliesBothIndependentEquipmentSets()
        {
            var settings = GoldenSettings(VxtOptimizationMode.ProEconomy);
            settings.AutoDimension = false;
            settings.UseAvoidance = true;
            settings.ShiftAllForAvoidance = false;
            settings.ClearanceDistance = 0.0;

            var boundary = Rectangle(6000.0, 4000.0);

            var baseline = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());
            var mainGrid = MainCoordinates(baseline);
            var xpGrid = FurringCoordinates(baseline);
            var main0 = mainGrid.Length > 2 ? mainGrid[1] : mainGrid.First();
            var xp0 = xpGrid.First();

            var mainOnly = new VxtLayoutContext();
            mainOnly.MainObstacles.Add(new Box2(0.0, main0 - 10.0, 6000.0, main0 + 10.0));

            var xpOnly = new VxtLayoutContext();
            xpOnly.FurringObstacles.Add(new Box2(xp0 - 10.0, 0.0, xp0 + 10.0, 4000.0));

            var combined = new VxtLayoutContext();
            combined.MainObstacles.Add(new Box2(0.0, main0 - 10.0, 6000.0, main0 + 10.0));
            combined.FurringObstacles.Add(new Box2(xp0 - 10.0, 0.0, xp0 + 10.0, 4000.0));

            var mainOnlyPlan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, mainOnly);
            var xpOnlyPlan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, xpOnly);
            var combinedPlan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, combined);

            CollectionAssert.AreEqual(
                MainCoordinates(mainOnlyPlan),
                MainCoordinates(combinedPlan),
                "Combined MEP avoidance must preserve the same XC result as XC-only avoidance.");

            CollectionAssert.AreEqual(
                FurringCoordinates(xpOnlyPlan),
                FurringCoordinates(combinedPlan),
                "Combined MEP avoidance must preserve the same XP result as XP-only avoidance.");

            Assert.IsTrue(combinedPlan.MainSegmentCount > 0);
            Assert.IsTrue(combinedPlan.FurringSegmentCount > 0);
        }

        [TestMethod]
        public void ProTyRepair_LeavesNoTyInsideObstacleBand_AndNoOversizeInternalGap()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProBalanced,
                DrawFurring = false,
                AutoDimension = false,
                UseAvoidance = true,
                ShiftAllForAvoidance = false,
                ClearanceDistance = 0.0
            };
            var context = new VxtLayoutContext();
            // Vertical obstacle intersects every horizontal XC row and blocks a normal Ty location.
            context.GeneralObstacles.Add(new Box2(1100.0, 0.0, 1450.0, 4000.0));

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) },
                settings,
                context);

            Assert.IsTrue(plan.HangerCount > 0);
            Assert.IsTrue(plan.HangerPoints.All(p => p.X <= 1100.0 + 0.1 || p.X >= 1450.0 - 0.1));

            foreach (var row in plan.HangerPoints.GroupBy(p => Math.Round(p.Y, 3)))
            {
                var xs = row.Select(p => p.X).OrderBy(x => x).ToArray();
                for (var i = 1; i < xs.Length; i++)
                    Assert.IsTrue(xs[i] - xs[i - 1] <= settings.HangerMaxSpacing + 0.1,
                        "Ty repair must not leave an internal gap above Max after avoidance.");
            }
        }

        private static double[] MainCoordinates(VxtPreviewPlan plan)
            => plan.Lines
                .Where(line => line.Kind == PreviewLineKind.Main)
                .Select(line => Math.Round((line.A.Y + line.B.Y) * 0.5, 6))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

        private static double[] FurringCoordinates(VxtPreviewPlan plan)
            => plan.Lines
                .Where(line => line.Kind == PreviewLineKind.Furring)
                .Select(line => Math.Round((line.A.X + line.B.X) * 0.5, 6))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

        private static VxtSettings GoldenSettings(VxtOptimizationMode mode)
            => new VxtSettings
            {
                OptimizationMode = mode,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false,
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true,
                UseAvoidance = false
            };

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
