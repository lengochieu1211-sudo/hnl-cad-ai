using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public class BoundaryHolePostProcessorTests
    {
        [TestMethod]
        public void Apply_SplitsXCXP_AndRemovesTyInsideHole()
        {
            var outer = Rect(0, 0, 1000, 1000);
            var hole = Rect(400, 400, 600, 600);
            var context = new VxtLayoutContext();
            context.BoundaryHoles.Add(hole);

            var plan = new VxtPreviewPlan();
            plan.Lines.Add(new PreviewLine(new Point2(0, 500), new Point2(1000, 500), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(500, 0), new Point2(500, 1000), PreviewLineKind.Furring));
            plan.HangerPoints.Add(new Point2(200, 500));
            plan.HangerPoints.Add(new Point2(500, 500));
            plan.HangerPoints.Add(new Point2(800, 500));
            plan.MainSegmentCount = 1;
            plan.FurringSegmentCount = 1;
            plan.HangerCount = 3;

            Assert.IsTrue(VxtBoundaryHolePostProcessor.Apply(outer, context, plan));
            Assert.AreEqual(2, plan.Lines.Count(x => x.Kind == PreviewLineKind.Main));
            Assert.AreEqual(2, plan.Lines.Count(x => x.Kind == PreviewLineKind.Furring));
            Assert.AreEqual(2, plan.HangerPoints.Count);

            var main = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).OrderBy(x => x.A.X).ToList();
            Assert.AreEqual(0.0, main[0].A.X, 0.001);
            Assert.AreEqual(400.0, main[0].B.X, 0.001);
            Assert.AreEqual(600.0, main[1].A.X, 0.001);
            Assert.AreEqual(1000.0, main[1].B.X, 0.001);
        }

        [TestMethod]
        public void Apply_NoHoles_IsExactNoOp()
        {
            var outer = Rect(0, 0, 1000, 1000);
            var context = new VxtLayoutContext();
            var plan = new VxtPreviewPlan();
            plan.Lines.Add(new PreviewLine(new Point2(0, 500), new Point2(1000, 500), PreviewLineKind.Main));
            plan.MainSegmentCount = 1;

            Assert.IsFalse(VxtBoundaryHolePostProcessor.Apply(outer, context, plan));
            Assert.AreEqual(1, plan.MainSegmentCount);
            Assert.AreEqual(1, plan.Lines.Count);
        }

        [TestMethod]
        public void Auditor_TreatsHoleAsVoid_NotMissingCoverage()
        {
            var outer = Rect(0, 0, 1000, 1000);
            var hole = Rect(400, 400, 600, 600);
            var context = new VxtLayoutContext();
            context.BoundaryHoles.Add(hole);

            var plan = new VxtPreviewPlan();
            foreach (var y in new[] { 100.0, 300.0, 700.0, 900.0 })
                plan.Lines.Add(new PreviewLine(new Point2(0, y), new Point2(1000, y), PreviewLineKind.Main));
            plan.MainSegmentCount = 4;

            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                UseLocalMainAdd = false,
                MainDirection = MainDirectionMode.Horizontal,
                MainMinSpacing = 100.0,
                MainMaxSpacing = 500.0,
                MainMinEdgeOffset = 50.0,
                MainMaxEdgeOffset = 200.0,
                MainBalanceStep = 100.0,
                MainSkipLimit = 0.0
            };

            VxtPlanConstraintAuditor.Attach(outer, plan, settings, 0.0, 0, context);
            Assert.IsFalse(plan.Diagnostics.Any(x => x.Kind == VxtConstraintKind.MissingCoverageHard),
                "Hole void must not be audited as drawable ceiling.");
        }

        [TestMethod]
        public void MultiBoundaryBuilder_AppliesHoleClipping_ForLegacyAndProFixed()
        {
            var outer = Rect(0, 0, 2000, 2000);
            var hole = Rect(700, 700, 1300, 1300);

            foreach (var mode in new[] { VxtOptimizationMode.Legacy, VxtOptimizationMode.ProBalanced })
            {
                var context = new VxtLayoutContext();
                context.BoundaryHoleGroups.Add(new System.Collections.Generic.List<Boundary2> { hole });

                var settings = new VxtSettings
                {
                    OptimizationMode = mode,
                    MainDirection = MainDirectionMode.Horizontal,
                    DrawMain = true,
                    DrawFurring = true,
                    DrawHangers = true,
                    AutoDimension = false,
                    UseLocalMainAdd = false,
                    MainSkipLimit = 0.0
                };

                var plan = VxtMultiBoundaryPlanBuilder.Build(new[] { outer }, settings, context);

                Assert.IsTrue(plan.Lines.Any(x => x.Kind == PreviewLineKind.Main));
                Assert.IsTrue(plan.Lines.Any(x => x.Kind == PreviewLineKind.Furring));
                Assert.IsFalse(plan.HangerPoints.Any(IsStrictlyInsideHole),
                    mode + " left a Ty point inside the picked hole.");

                foreach (var line in plan.Lines.Where(x =>
                    x.Kind == PreviewLineKind.Main || x.Kind == PreviewLineKind.Furring))
                {
                    var mid = new Point2((line.A.X + line.B.X) * 0.5, (line.A.Y + line.B.Y) * 0.5);
                    Assert.IsFalse(IsStrictlyInsideHole(mid),
                        mode + " left structural geometry inside the picked hole.");
                }
            }
        }

        private static bool IsStrictlyInsideHole(Point2 p)
            => p.X > 700.0 && p.X < 1300.0 && p.Y > 700.0 && p.Y < 1300.0;

        private static Boundary2 Rect(double minX, double minY, double maxX, double maxY)
            => new Boundary2(new[]
            {
                new Point2(minX, minY), new Point2(maxX, minY),
                new Point2(maxX, maxY), new Point2(minX, maxY)
            });
    }
}
