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
    public sealed class ProductionLayoutTests
    {
        [TestMethod]
        public void GoldenHorizontal_MainsAreHorizontalAndXpPerpendicular()
        {
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), new VxtSettings());
            var main = plan.Lines.First(x => x.Kind == PreviewLineKind.Main);
            var xp = plan.Lines.First(x => x.Kind == PreviewLineKind.Furring);
            Assert.IsTrue(Math.Abs(main.A.Y - main.B.Y) < 1e-6, "V6.7.4 Ngang must create horizontal main members.");
            Assert.IsTrue(Math.Abs(xp.A.X - xp.B.X) < 1e-6, "Furring must be perpendicular to main members.");
        }

        [TestMethod]
        public void GoldenVertical_MainsAreVertical()
        {
            var settings = new VxtSettings { MainDirection = MainDirectionMode.Vertical, DirectionDegrees = 90.0 };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings);
            var main = plan.Lines.First(x => x.Kind == PreviewLineKind.Main);
            Assert.IsTrue(Math.Abs(main.A.X - main.B.X) < 1e-6);
        }

        [TestMethod]
        public void SmartLayout_RespectsGoldenSpacingAndEdgeLimits()
        {
            var result = SmartLayout1D.Calculate(4000, 1000, 700, 400, 300, 50, MainLayoutMode.BalancedTwoEnds);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.StartOffset >= 300 - 1e-8 && result.StartOffset <= 400 + 1e-8);
            Assert.IsTrue(result.EndOffset >= 300 - 1e-8 && result.EndOffset <= 400 + 1e-8);
            Assert.IsTrue(result.Steps.All(s => s >= 700 - 1e-8 && s <= 1000 + 1e-8));
            Assert.IsTrue(result.Steps.All(s => Math.Abs(s / 50.0 - Math.Round(s / 50.0)) < 1e-8));
            Assert.AreEqual(4000.0, result.StartOffset + result.Steps.Sum() + result.EndOffset, 1e-6);
        }

        [TestMethod]
        public void MainSkipLimit_SkipsWholeShortRegionLikeV674()
        {
            var settings = new VxtSettings { MainSkipLimit = 500.0 };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 450), settings);
            Assert.AreEqual(0, plan.MainSegmentCount);
            Assert.AreEqual(0, plan.HangerCount, "Ty depends on generated main members.");
            Assert.IsTrue(plan.FurringSegmentCount > 0, "XP remains independently available.");
        }

        [TestMethod]
        public void Avoidance_ShiftAllKeepsMainAxisOutsideObstacleBand()
        {
            var context = new VxtLayoutContext();
            context.GeneralObstacles.Add(new Box2(1000, 250, 5000, 450));
            var settings = new VxtSettings
            {
                UseAvoidance = true,
                ShiftAllForAvoidance = true,
                ClearanceDistance = 20
            };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings, context);
            Assert.IsFalse(plan.Lines.Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x => x.A.Y > 230 && x.A.Y < 470));
        }

        [TestMethod]
        public void FurringDefault_StartsOneStepFromNearEdge()
        {
            var settings = new VxtSettings { DrawMain = false, DrawHangers = false, FurringSpacing = 400.0 };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(2100, 1200), settings, new VxtLayoutContext());
            var xs = plan.Lines.Where(x => x.Kind == PreviewLineKind.Furring).Select(x => x.A.X).Distinct().OrderBy(x => x).ToList();
            Assert.IsTrue(xs.Count > 0);
            Assert.AreEqual(400.0, xs.First(), 1e-6);
        }

        [TestMethod]
        public void FurringFarEdge_UsesGoldenRemainderOffset()
        {
            var settings = new VxtSettings { DrawMain = false, DrawHangers = false, FurringSpacing = 400.0 };
            var context = new VxtLayoutContext { GlobalFurringFromFarEdge = true };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(2100, 1200), settings, context);
            var xs = plan.Lines.Where(x => x.Kind == PreviewLineKind.Furring).Select(x => x.A.X).Distinct().OrderBy(x => x).ToList();
            Assert.IsTrue(xs.Count > 0);
            Assert.AreEqual(100.0, xs.First(), 1e-6);
            Assert.AreEqual(2000.0 - 300.0, 1700.0, 1e-6); // readability guard; far grid is 100,500,...,1700.
            Assert.AreEqual(1700.0, xs.Last(), 1e-6);
        }

        [TestMethod]
        public void Hangers_RespectEdgeLimitsAlongMainMembers()
        {
            var settings = new VxtSettings { DrawFurring = false };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings);
            Assert.IsTrue(plan.HangerPoints.Count > 0);
            foreach (var row in plan.HangerPoints.GroupBy(p => Math.Round(p.Y, 3)))
            {
                var xs = row.Select(p => p.X).OrderBy(x => x).ToList();
                if (xs.Count == 0) continue;
                Assert.IsTrue(xs.First() >= 300 - 1e-6);
                Assert.IsTrue(6000 - xs.Last() >= 300 - 1e-6);
            }
        }

        [TestMethod]
        public void OneSideHangers_ReverseWithFurringStartSide()
        {
            var settings = new VxtSettings
            {
                DrawFurring = false,
                HangerLayout = HangerLayoutMode.OneSideFollowFurring,
                HangerMinEdgeOffset = 300,
                HangerMaxEdgeOffset = 400
            };
            var near = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings, new VxtLayoutContext());
            var farContext = new VxtLayoutContext { GlobalFurringFromFarEdge = true };
            var far = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings, farContext);
            var nearX = near.HangerPoints.OrderBy(p => p.Y).ThenBy(p => p.X).First().X;
            var farX = far.HangerPoints.OrderBy(p => p.Y).ThenBy(p => p.X).First().X;
            Assert.AreNotEqual(nearX, farX, 1e-6);
        }

        [TestMethod]
        public void RealDimensionPlan_IsProducedWithoutFakeDimensionLines()
        {
            var settings = new VxtSettings
            {
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true
            };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings);
            Assert.IsTrue(plan.Dimensions.Count > 0);
            Assert.AreEqual(plan.Dimensions.Count, plan.DimensionSegmentCount);
            Assert.IsFalse(plan.Lines.Any(x => x.Kind == PreviewLineKind.Dimension || x.Kind == PreviewLineKind.DimensionExtension));
        }

        [TestMethod]
        public void ManualRegions_UseDifferentMainDirections()
        {
            var settings = new VxtSettings { MainDirection = MainDirectionMode.RectangleRegions };
            var context = new VxtLayoutContext();
            context.Regions.Add(new VxtLayoutRegion(new Box2(0, 0, 3000, 4000), 0.0));
            context.Regions.Add(new VxtLayoutRegion(new Box2(3000, 0, 6000, 4000), 90.0));
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings, context);
            Assert.IsTrue(plan.Lines.Any(x => x.Kind == PreviewLineKind.Main && Math.Abs(x.A.Y - x.B.Y) < 1e-6));
            Assert.IsTrue(plan.Lines.Any(x => x.Kind == PreviewLineKind.Main && Math.Abs(x.A.X - x.B.X) < 1e-6));
        }

        [TestMethod]
        public void ManualRegionOutsideBoundary_ProducesNoGeometry()
        {
            var settings = new VxtSettings { MainDirection = MainDirectionMode.RectangleRegions };
            var context = new VxtLayoutContext();
            context.Regions.Add(new VxtLayoutRegion(new Box2(10000, 10000, 12000, 12000), 0.0));
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings, context);
            Assert.AreEqual(0, plan.MainSegmentCount);
            Assert.AreEqual(0, plan.FurringSegmentCount);
            Assert.AreEqual(0, plan.HangerCount);
        }

        private static Boundary2 Rectangle(double width, double height)
            => new Boundary2(new[]
            {
                new Point2(0,0), new Point2(width,0), new Point2(width,height), new Point2(0,height)
            });
    }
}
