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
        public void ProEconomy_GoldenRectangle_DoesNotIncreaseMaterialCounts()
        {
            var legacy = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) },
                GoldenSettings(VxtOptimizationMode.Legacy),
                new VxtLayoutContext());
            var pro = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(6000.0, 4000.0) },
                GoldenSettings(VxtOptimizationMode.ProEconomy),
                new VxtLayoutContext());

            Assert.AreEqual(legacy.MainSegmentCount, pro.MainSegmentCount,
                "Pro Economy must not add XC on a clear Golden rectangle.");
            Assert.AreEqual(legacy.FurringSegmentCount, pro.FurringSegmentCount,
                "Pro Economy must preserve the economical XP member count.");
            Assert.AreEqual(legacy.HangerCount, pro.HangerCount,
                "Pro Economy must not add Ty when the Legacy chain is already clear and legal.");
            Assert.IsTrue(pro.DimensionSegmentCount <= legacy.DimensionSegmentCount,
                "DIM packer may remove duplicates but must never add redundant DIM segments.");
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
