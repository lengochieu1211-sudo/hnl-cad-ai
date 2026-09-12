using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProObstaclePostProcessorTests
    {
        [TestMethod]
        public void Evaluate_SplitsResidualMainAndFurringCrossings_AndReturnsVC0()
        {
            var settings = BuildSettings();
            var context = BuildContext();
            var plan = BuildCrossingPlan();

            var quality = VxtProPlanQualityEvaluator.Evaluate(plan, settings, context, 0.0, 1);

            Assert.AreEqual(0, quality.CollisionCount);
            Assert.AreEqual(0, quality.MainCollisionCount);
            Assert.AreEqual(0, quality.FurringCollisionCount);
            Assert.AreEqual(2, quality.ObstacleSplitFallbackCount);
            Assert.AreEqual(2, plan.MainSegmentCount);
            Assert.AreEqual(2, plan.FurringSegmentCount);
            Assert.AreEqual(0, quality.HardViolationCount);
        }

        [TestMethod]
        public void Evaluate_RepeatedOnFinalizedPlan_PreservesFallbackTelemetryAndScore()
        {
            var settings = BuildSettings();
            var context = BuildContext();
            var plan = BuildCrossingPlan();

            var first = VxtProPlanQualityEvaluator.Evaluate(plan, settings, context, 0.0, 1);
            plan.Quality = first;
            var mainAfterFirst = plan.MainSegmentCount;
            var furringAfterFirst = plan.FurringSegmentCount;

            var second = VxtProPlanQualityEvaluator.Evaluate(plan, settings, context, 0.0, 1);

            Assert.AreEqual(first.ObstacleSplitFallbackCount, second.ObstacleSplitFallbackCount,
                "Repeated Quality evaluation of the same finalized plan must preserve MEP split telemetry.");
            Assert.AreEqual(first.QualityScore100, second.QualityScore100,
                "Repeated Quality evaluation must not improve Q merely because the first pass already split the geometry.");
            Assert.AreEqual(first.SortScore, second.SortScore, 0.001,
                "Repeated Quality evaluation must remain score-idempotent for the same finalized plan.");
            Assert.AreEqual(mainAfterFirst, plan.MainSegmentCount,
                "Second evaluation must not fragment XC again.");
            Assert.AreEqual(furringAfterFirst, plan.FurringSegmentCount,
                "Second evaluation must not fragment XP again.");
        }

        private static VxtSettings BuildSettings()
            => new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProBalanced,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = true,
                ShiftAllForAvoidance = true,
                ClearanceDistance = 0.0,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

        private static VxtLayoutContext BuildContext()
        {
            var context = new VxtLayoutContext();
            context.GeneralObstacles.Add(new Box2(400.0, 50.0, 600.0, 150.0));
            return context;
        }

        private static VxtPreviewPlan BuildCrossingPlan()
        {
            var plan = new VxtPreviewPlan();
            plan.Lines.Add(new PreviewLine(
                new Point2(0.0, 100.0), new Point2(1000.0, 100.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(
                new Point2(500.0, 0.0), new Point2(500.0, 1000.0), PreviewLineKind.Furring));
            plan.MainSegmentCount = 1;
            plan.FurringSegmentCount = 1;
            return plan;
        }
    }
}
