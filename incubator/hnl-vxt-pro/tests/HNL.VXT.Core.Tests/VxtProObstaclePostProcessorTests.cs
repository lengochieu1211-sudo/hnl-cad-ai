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
    public sealed class VxtProObstaclePostProcessorTests
    {
        [TestMethod]
        public void Evaluate_SplitsResidualMain_ButPreservesFullFurringMember()
        {
            var settings = BuildSettings();
            var context = BuildContext();
            var plan = BuildCrossingPlan();

            var quality = VxtProPlanQualityEvaluator.Evaluate(plan, settings, context, 0.0, 1);

            Assert.AreEqual(1, quality.CollisionCount,
                "The impossible residual XP crossing must stay visible to QA instead of being hidden by a split.");
            Assert.AreEqual(0, quality.MainCollisionCount);
            Assert.AreEqual(1, quality.FurringCollisionCount);
            Assert.AreEqual(1, quality.ObstacleSplitFallbackCount,
                "Only XC is allowed to use the final split fallback.");
            Assert.AreEqual(2, plan.MainSegmentCount);
            Assert.AreEqual(1, plan.FurringSegmentCount,
                "XP must remain one full member after Lisp-parity repair.");
            Assert.AreEqual(0, quality.HardViolationCount);
        }

        [TestMethod]
        public void DxfFieldFixture_DenseXpAvoidance_PreservesTwentyFullMembersAndClearsMep()
        {
            // Geometry reconstructed from the supplied "ne xcxp.dxf" field case.
            // Pro keeps every XP as one full member. When the exact V6.7.2 periodic repair leaves
            // residual collisions, the recovery stage may move only the affected XP rows to nearby
            // clear coordinates instead of preserving a known-colliding historical coordinate.
            var boundary = new Boundary2(new[]
            {
                new Point2(768700.400, 1488.608),
                new Point2(768700.300, 2521.108),
                new Point2(760390.367, 2521.108),
                new Point2(760390.367, -921.392),
                new Point2(765340.400, -921.392),
                new Point2(765340.400, 888.608),
                new Point2(767540.400, 888.608),
                new Point2(767540.400, 1488.608)
            });

            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProBalanced,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = false,
                DrawFurring = true,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = true,
                ShiftAllForAvoidance = true,
                ClearanceDistance = 0.0,
                FurringSpacing = 1220.0 / 3.0,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

            var context = new VxtLayoutContext();
            context.GeneralObstacles.Add(new Box2(761650.2, -31.8, 761870.2, 188.2));
            context.GeneralObstacles.Add(new Box2(763100.2, -31.8, 763320.2, 188.2));
            context.GeneralObstacles.Add(new Box2(764550.2, -31.8, 764770.2, 188.2));
            context.GeneralObstacles.Add(new Box2(762735.2, 567.0, 763685.2, 1517.0));
            context.GeneralObstacles.Add(new Box2(761650.2, 1895.9, 761870.2, 2115.9));
            context.GeneralObstacles.Add(new Box2(764550.2, 1895.9, 764770.2, 2115.9));
            context.GeneralObstacles.Add(new Box2(766280.1, 1895.9, 766500.1, 2115.9));
            context.GeneralObstacles.Add(new Box2(768010.1, 1895.8, 768230.1, 2115.8));

            var plan = new VxtProPreviewPlanBuilder().Build(boundary, settings, context);
            var before = plan.Lines.Where(x => x.Kind == PreviewLineKind.Furring).ToArray();
            Assert.AreEqual(20, before.Length,
                "The field fixture must preserve the complete 20-member XP chain.");

            var uniqueCoordinates = before
                .Select(x => Math.Round((x.A.X + x.B.X) * 0.5, 3))
                .Distinct()
                .Count();
            Assert.AreEqual(20, uniqueCoordinates,
                "XP MEP recovery must not collapse two full members onto one axis.");

            var quality = VxtProPlanQualityEvaluator.Evaluate(plan, settings, context, 0.0, 1);
            var after = plan.Lines.Where(x => x.Kind == PreviewLineKind.Furring).ToArray();

            Assert.AreEqual(20, after.Length,
                "Final Pro QA must not cut or delete XP members.");
            Assert.AreEqual(0, quality.FurringCollisionCount,
                "Recoverable field XP collisions must be cleared instead of preserving known-colliding Lisp coordinates.");
            Assert.AreEqual(0, quality.ObstacleSplitFallbackCount,
                "Furring-only field fixture must never use the XC split fallback.");
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
