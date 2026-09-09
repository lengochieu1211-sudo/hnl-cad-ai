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
            var settings = new VxtSettings
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

            var context = new VxtLayoutContext();
            context.GeneralObstacles.Add(new Box2(400.0, 50.0, 600.0, 150.0));

            var plan = new VxtPreviewPlan();
            plan.Lines.Add(new PreviewLine(
                new Point2(0.0, 100.0), new Point2(1000.0, 100.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(
                new Point2(500.0, 0.0), new Point2(500.0, 1000.0), PreviewLineKind.Furring));
            plan.MainSegmentCount = 1;
            plan.FurringSegmentCount = 1;

            var quality = VxtProPlanQualityEvaluator.Evaluate(plan, settings, context, 0.0, 1);

            Assert.AreEqual(0, quality.CollisionCount);
            Assert.AreEqual(0, quality.MainCollisionCount);
            Assert.AreEqual(0, quality.FurringCollisionCount);
            Assert.AreEqual(2, quality.ObstacleSplitFallbackCount);
            Assert.AreEqual(2, plan.MainSegmentCount);
            Assert.AreEqual(2, plan.FurringSegmentCount);
            Assert.AreEqual(0, quality.HardViolationCount);
        }
    }
}
