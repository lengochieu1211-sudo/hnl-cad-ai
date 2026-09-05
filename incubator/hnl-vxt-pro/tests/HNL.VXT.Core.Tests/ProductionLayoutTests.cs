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
        public void SmartLayout_ExactLegacyResultFor4000Run_Is400_800x4_400()
        {
            // Exact legacy calc-smart-layout tie-break for L=4000,
            // max/min spacing=1000/700, max/min edge=400/300, multiple=50.
            // S=850/O=300 and S=800/O=400 have the same penalty. The Lisp builds
            // valid_plans with CONS while S descends, then keeps the first strict-best (>),
            // therefore the later-consed S=800 candidate wins the tie.
            var result = SmartLayout1D.Calculate(4000, 1000, 700, 400, 300, 50, MainLayoutMode.BalancedTwoEnds);
            Assert.IsNotNull(result);
            Assert.AreEqual(400.0, result.StartOffset, 1e-8);
            CollectionAssert.AreEqual(new[] { 800.0, 800.0, 800.0, 800.0 }, result.Steps.ToArray());
            Assert.AreEqual(400.0, result.EndOffset, 1e-8);
            CollectionAssert.AreEqual(new[] { 400.0, 1200.0, 2000.0, 2800.0, 3600.0 }, result.Positions(0).ToArray());
        }

        [TestMethod]
        public void AutoDirection_FollowsLegacyShadowlineRule()
        {
            var withShadowline = new VxtSettings
            {
                MainDirection = MainDirectionMode.Auto,
                AutoShadowline = true,
                DrawFurring = false,
                DrawHangers = false
            };
            var withoutShadowline = withShadowline.Clone();
            withoutShadowline.AutoShadowline = false;

            var longSide = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), withShadowline);
            var shortSide = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), withoutShadowline);

            Assert.IsTrue(longSide.Lines.Where(x => x.Kind == PreviewLineKind.Main)
                .All(x => Math.Abs(x.A.Y - x.B.Y) < 1e-6), "Shadowline=Yes must follow the long side on a 6000x4000 region.");
            Assert.IsTrue(shortSide.Lines.Where(x => x.Kind == PreviewLineKind.Main)
                .All(x => Math.Abs(x.A.X - x.B.X) < 1e-6), "Shadowline=No must follow the short side on a 6000x4000 region.");
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
            // Use an asymmetric run so reversing the Golden one-side plan is observable.
            // A 6000 run resolves symmetrically, so reversal is intentionally identical there.
            const double width = 6075.0;
            var settings = new VxtSettings
            {
                DrawFurring = false,
                HangerLayout = HangerLayoutMode.OneSideFollowFurring,
                HangerMinEdgeOffset = 300,
                HangerMaxEdgeOffset = 400
            };
            var near = new VxtPreviewPlanBuilder().Build(Rectangle(width, 4000), settings, new VxtLayoutContext());
            var farContext = new VxtLayoutContext { GlobalFurringFromFarEdge = true };
            var far = new VxtPreviewPlanBuilder().Build(Rectangle(width, 4000), settings, farContext);
            var nearX = near.HangerPoints.OrderBy(p => p.Y).ThenBy(p => p.X).First().X;
            var farX = far.HangerPoints.OrderBy(p => p.Y).ThenBy(p => p.X).First().X;
            Assert.AreNotEqual(nearX, farX, 1e-6, "Golden reverse must swap asymmetric edge offsets.");
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
        public void Dimensions_IncludeBothBoundarySegmentsLikeLegacyProcessDims()
        {
            var settings = new VxtSettings
            {
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true,
                DimMain = true,
                MainDimPosition = DimensionPosition.Left
            };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings);
            var dims = plan.Dimensions.Where(d => d.Target == DimensionTarget.Main).ToList();
            Assert.AreEqual(6, dims.Count, "Legacy process-dims must dimension boundary→first XC, every XC gap, and last XC→boundary.");
            Assert.AreEqual(400.0, dims.First().ExtensionPoint1.DistanceTo(dims.First().ExtensionPoint2), 1e-6);
            Assert.AreEqual(400.0, dims.Last().ExtensionPoint1.DistanceTo(dims.Last().ExtensionPoint2), 1e-6);
        }

        [TestMethod]
        public void DimensionExtensionPoints_StayOnLegacyBaseLine_NotOnDimTextLine()
        {
            var settings = new VxtSettings
            {
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true,
                DimMain = true,
                MainDimPosition = DimensionPosition.Left,
                DimensionDistance = 500.0
            };
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000), settings);
            var dim = plan.Dimensions.First(d => d.Target == DimensionTarget.Main);

            // Legacy process-dims uses bound_X_min (=0) as extension base, while the dimension
            // text/line sits at bound_X_min - dim_dist (=-500).
            Assert.AreEqual(0.0, dim.ExtensionPoint1.X, 1e-6);
            Assert.AreEqual(0.0, dim.ExtensionPoint2.X, 1e-6);
            Assert.AreEqual(-500.0, dim.DimensionLinePoint.X, 1e-6);
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
