using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class FinalPlanMetricsTests
    {
        [TestMethod]
        public void FromPlan_UsesActualFinalGeometry_NotStaleBuilderCounters()
        {
            var plan = new VxtPreviewPlan
            {
                // Deliberately stale bookkeeping values. Final metrics must ignore these.
                MainSegmentCount = 99,
                FurringSegmentCount = 88,
                HangerCount = 77,
                DimensionSegmentCount = 66
            };

            plan.Lines.Add(new PreviewLine(new Point2(0, 0), new Point2(3000, 0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(0, 1000), new Point2(4000, 1000), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(0, 0), new Point2(0, 1000), PreviewLineKind.Furring));
            plan.Lines.Add(new PreviewLine(new Point2(1000, 0), new Point2(1000, 2000), PreviewLineKind.Furring));
            plan.Lines.Add(new PreviewLine(new Point2(0, 0), new Point2(10, 10), PreviewLineKind.Boundary));

            plan.HangerPoints.Add(new Point2(500, 0));
            plan.HangerPoints.Add(new Point2(1500, 0));
            plan.HangerPoints.Add(new Point2(2500, 0));

            plan.Dimensions.Add(new PreviewDimension(
                new Point2(0, 0), new Point2(3000, 0), new Point2(0, -500), 0.0, DimensionTarget.Main));
            plan.Dimensions.Add(new PreviewDimension(
                new Point2(0, 0), new Point2(0, 1000), new Point2(-500, 0), 1.5707963267948966, DimensionTarget.Furring));

            var metrics = VxtFinalPlanMetrics.FromPlan(plan);

            Assert.AreEqual(2, metrics.MainCount);
            Assert.AreEqual(2, metrics.FurringCount);
            Assert.AreEqual(3, metrics.HangerCount);
            Assert.AreEqual(2, metrics.DimensionCount);
            Assert.AreEqual(7000.0, metrics.MainLengthMm, 0.001);
            Assert.AreEqual(3000.0, metrics.FurringLengthMm, 0.001);
            Assert.AreEqual(7.0, metrics.MainLengthM, 0.000001);
            Assert.AreEqual(3.0, metrics.FurringLengthM, 0.000001);
            Assert.AreEqual(10.0, metrics.TotalFrameLengthM, 0.000001);
        }
    }
}
