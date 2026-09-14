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
    public sealed class VxtFinalGeometryDimensionTests
    {
        [TestMethod]
        public void Evaluate_MepRemovesWholeMainRow_ResyncsDimFromFinalGeometry()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProBalanced,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true,
                DimMain = true,
                DimFurring = false,
                DimHanger = false,
                UseAvoidance = true,
                ShiftAllForAvoidance = true,
                ClearanceDistance = 0.0,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

            var boundary = new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(1000.0, 0.0),
                new Point2(1000.0, 1000.0),
                new Point2(0.0, 1000.0)
            });

            var plan = new VxtPreviewPlan();
            plan.Lines.Add(new PreviewLine(new Point2(0.0, 0.0), new Point2(1000.0, 0.0), PreviewLineKind.Boundary));
            plan.Lines.Add(new PreviewLine(new Point2(1000.0, 0.0), new Point2(1000.0, 1000.0), PreviewLineKind.Boundary));
            plan.Lines.Add(new PreviewLine(new Point2(1000.0, 1000.0), new Point2(0.0, 1000.0), PreviewLineKind.Boundary));
            plan.Lines.Add(new PreviewLine(new Point2(0.0, 1000.0), new Point2(0.0, 0.0), PreviewLineKind.Boundary));
            plan.Lines.Add(new PreviewLine(new Point2(0.0, 200.0), new Point2(1000.0, 200.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(0.0, 800.0), new Point2(1000.0, 800.0), PreviewLineKind.Main));
            plan.MainSegmentCount = 2;

            var context = new VxtLayoutContext();
            context.GeneralObstacles.Add(new Box2(-100.0, 150.0, 1100.0, 250.0));

            // Mirror the normal builder order before Quality: DIM exists while both XC rows exist.
            VxtPostProcessDimensionSynchronizer.Synchronize(boundary, settings, context, plan, 0.0);
            Assert.AreEqual(3, plan.DimensionSegmentCount);
            Assert.IsTrue(plan.Dimensions.Any(d => d.Target == DimensionTarget.Main &&
                (Math.Abs(d.ExtensionPoint1.Y - 200.0) < 0.1 || Math.Abs(d.ExtensionPoint2.Y - 200.0) < 0.1)),
                "Fixture must start with a DIM chain that references the XC row at Y=200.");

            var quality = VxtProPlanQualityEvaluator.Evaluate(plan, settings, context, 0.0, 1);

            Assert.AreEqual(1, quality.ObstacleSplitFallbackCount);
            Assert.AreEqual(0, quality.CollisionCount);
            Assert.AreEqual(1, plan.MainSegmentCount,
                "MEP finalizer must remove the fully covered XC row at Y=200.");
            Assert.IsFalse(plan.Lines.Any(x => x.Kind == PreviewLineKind.Main &&
                Math.Abs((x.A.Y + x.B.Y) * 0.5 - 200.0) < 0.1));

            Assert.AreEqual(2, plan.DimensionSegmentCount,
                "Final DIM chain must be rebuilt from boundary + the surviving XC row only.");
            Assert.IsFalse(plan.Dimensions.Any(d => d.Target == DimensionTarget.Main &&
                (Math.Abs(d.ExtensionPoint1.Y - 200.0) < 0.1 || Math.Abs(d.ExtensionPoint2.Y - 200.0) < 0.1)),
                "DIM must not retain an endpoint for an XC row removed by the final MEP safety pass.");
        }
    }
}
