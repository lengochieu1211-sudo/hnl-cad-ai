using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class PreviewPlanTests
    {
        [TestMethod]
        public void Rectangle_LegacyDefaults_ProducesChannelsAndHangers_WithDimOff()
        {
            var boundary = Rectangle(6000, 4000);
            var settings = new VxtSettings();
            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings);

            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0);
            Assert.IsTrue(plan.HangerCount > 0);
            Assert.AreEqual(0, plan.DimensionSegmentCount);
            Assert.IsFalse(plan.Lines.Any(x => x.Kind == PreviewLineKind.Dimension));
        }

        [TestMethod]
        public void Rectangle_DimEnabled_ProducesDimensionPreview()
        {
            var boundary = Rectangle(6000, 4000);
            var settings = new VxtSettings
            {
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true
            };

            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings);

            Assert.IsTrue(plan.DimensionSegmentCount > 0);
            Assert.IsTrue(plan.Lines.Any(x => x.Kind == PreviewLineKind.Dimension));
        }

        [TestMethod]
        public void LegacyDefaults_MatchV674PrimaryLayoutValues()
        {
            var s = new VxtSettings();

            Assert.AreEqual(700.0, s.MainMinSpacing, 1e-9);
            Assert.AreEqual(1000.0, s.MainMaxSpacing, 1e-9);
            Assert.AreEqual(300.0, s.MainMinEdgeOffset, 1e-9);
            Assert.AreEqual(400.0, s.MainMaxEdgeOffset, 1e-9);
            Assert.AreEqual(50.0, s.MainBalanceStep, 1e-9);
            Assert.AreEqual(500.0, s.MainSkipLimit, 1e-9);
            Assert.AreEqual(MainDirectionMode.Horizontal, s.MainDirection);
            Assert.AreEqual(MainLayoutMode.BalancedTwoEnds, s.MainLayout);

            Assert.AreEqual(1220.0 / 3.0, s.FurringSpacing, 1e-9);
            Assert.IsTrue(s.UseDynamicMainBlock);
            Assert.IsTrue(s.UseDynamicFurringBlock);
            Assert.IsFalse(s.AskDirectionEachRegion);

            Assert.AreEqual(700.0, s.HangerMinSpacing, 1e-9);
            Assert.AreEqual(1000.0, s.HangerMaxSpacing, 1e-9);
            Assert.AreEqual(300.0, s.HangerMinEdgeOffset, 1e-9);
            Assert.AreEqual(400.0, s.HangerMaxEdgeOffset, 1e-9);
            Assert.AreEqual(50.0, s.HangerBalanceStep, 1e-9);
            Assert.AreEqual(HangerLayoutMode.BalancedTwoEnds, s.HangerLayout);

            Assert.IsTrue(s.UseAvoidance);
            Assert.IsTrue(s.ShiftAllForAvoidance);
            Assert.AreEqual(20.0, s.ClearanceDistance, 1e-9);
            Assert.IsFalse(s.AutoDimension);
            Assert.IsFalse(s.DimMain);
            Assert.IsFalse(s.DimFurring);
            Assert.IsFalse(s.DimHanger);
        }

        [TestMethod]
        public void ConcaveLShape_ClipsGridIntoValidSegments()
        {
            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(6000, 0),
                new Point2(6000, 2000),
                new Point2(3000, 2000),
                new Point2(3000, 5000),
                new Point2(0, 5000)
            });

            var plan = new VxtPreviewPlanBuilder().Build(boundary, new VxtSettings());
            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0);
        }

        [TestMethod]
        public void Rotation_StillProducesPreview()
        {
            var boundary = Rectangle(7000, 3500);
            var settings = new VxtSettings { DirectionDegrees = 30.0 };
            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings);

            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0);
        }

        private static Boundary2 Rectangle(double width, double height)
        {
            return new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(width, 0),
                new Point2(width, height),
                new Point2(0, height)
            });
        }
    }
}
