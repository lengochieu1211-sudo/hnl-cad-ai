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
        public void Rectangle_Defaults_ProducesChannelsHangersAndDims()
        {
            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(6000, 0),
                new Point2(6000, 4000),
                new Point2(0, 4000)
            });

            var plan = new VxtPreviewPlanBuilder().Build(boundary, new VxtSettings());

            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0);
            Assert.IsTrue(plan.HangerCount > 0);
            Assert.IsTrue(plan.DimensionSegmentCount > 0);
            Assert.IsTrue(plan.Lines.Any(x => x.Kind == PreviewLineKind.Dimension));
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
            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(7000, 0),
                new Point2(7000, 3500),
                new Point2(0, 3500)
            });

            var settings = new VxtSettings { DirectionDegrees = 30.0 };
            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings);

            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.DimensionSegmentCount > 0);
        }
    }
}
