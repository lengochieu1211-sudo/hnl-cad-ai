using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtPreviewLoadSheddingPolicyTests
    {
        [TestMethod]
        public void HiddenHangerCrossLines_DoNotPrematurelySuppressDim()
        {
            var plan = new VxtPreviewPlan();
            AddStructural(plan, 20);

            for (var i = 0; i < 1000; i++)
            {
                plan.HangerPoints.Add(new Point2(i, 0.0));
                // Core stores two helper cross-lines per Ty, but AutoCAD preview renders
                // one Circle instead. These 2000 lines must not consume the drawable budget.
                plan.Lines.Add(new PreviewLine(
                    new Point2(i - 1.0, 0.0), new Point2(i + 1.0, 0.0), PreviewLineKind.Hanger));
                plan.Lines.Add(new PreviewLine(
                    new Point2(i, -1.0), new Point2(i, 1.0), PreviewLineKind.Hanger));
            }

            AddDimensions(plan, 20);
            var decision = VxtPreviewLoadSheddingPolicy.Evaluate(plan, PreviewSettings());

            Assert.AreEqual(1040, decision.FullDrawableCount);
            Assert.IsTrue(decision.RenderHangers);
            Assert.IsTrue(decision.RenderDimensions);
            Assert.IsTrue(decision.RenderGuides);
            Assert.IsFalse(decision.IsReduced);
        }

        [TestMethod]
        public void LargeHangerPopulation_KeepsDimAndDropsTyAndGuidesFirst()
        {
            var plan = new VxtPreviewPlan();
            AddStructural(plan, 100);
            for (var i = 0; i < 4000; i++)
                plan.HangerPoints.Add(new Point2(i, 0.0));
            AddDimensions(plan, 100);
            plan.Lines.Add(new PreviewLine(
                new Point2(0.0, 0.0), new Point2(1.0, 1.0), PreviewLineKind.Direction));

            var decision = VxtPreviewLoadSheddingPolicy.Evaluate(plan, PreviewSettings());

            Assert.IsTrue(decision.FullDrawableCount > VxtPreviewLoadSheddingPolicy.DefaultDrawableLimit);
            Assert.IsFalse(decision.RenderHangers);
            Assert.IsTrue(decision.RenderDimensions,
                "DIM must remain visible when XC/XP + DIM fit even if Ty pushes the full preview over budget.");
            Assert.IsFalse(decision.RenderGuides);
            Assert.IsTrue(decision.IsReduced);
        }

        [TestMethod]
        public void DisabledOptionalOverlays_AreNotReportedAsReduced()
        {
            var plan = new VxtPreviewPlan();
            AddStructural(plan, 20);
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = false,
                AutoDimension = false
            };

            var decision = VxtPreviewLoadSheddingPolicy.Evaluate(plan, settings);

            Assert.IsFalse(decision.RenderHangers);
            Assert.IsFalse(decision.RenderDimensions);
            Assert.IsTrue(decision.RenderGuides);
            Assert.IsFalse(decision.IsReduced,
                "User-disabled overlays must not be confused with automatic large-preview load shedding.");
        }

        [TestMethod]
        public void VeryLargeDimensionSet_FallsBackToStructuralOnly()
        {
            var plan = new VxtPreviewPlan();
            AddStructural(plan, 100);
            AddDimensions(plan, 3000);

            var decision = VxtPreviewLoadSheddingPolicy.Evaluate(plan, PreviewSettings());

            Assert.IsFalse(decision.RenderHangers);
            Assert.IsFalse(decision.RenderDimensions);
            Assert.IsFalse(decision.RenderGuides);
            Assert.IsTrue(decision.IsReduced);
        }

        private static VxtSettings PreviewSettings()
            => new VxtSettings
            {
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = true
            };

        private static void AddStructural(VxtPreviewPlan plan, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var kind = i % 2 == 0 ? PreviewLineKind.Main : PreviewLineKind.Furring;
                plan.Lines.Add(new PreviewLine(
                    new Point2(0.0, i), new Point2(1000.0, i), kind));
            }
        }

        private static void AddDimensions(VxtPreviewPlan plan, int count)
        {
            for (var i = 0; i < count; i++)
            {
                plan.Dimensions.Add(new PreviewDimension(
                    new Point2(i, 0.0),
                    new Point2(i + 1.0, 0.0),
                    new Point2(i + 0.5, 10.0),
                    0.0,
                    DimensionTarget.Main));
            }
        }
    }
}
