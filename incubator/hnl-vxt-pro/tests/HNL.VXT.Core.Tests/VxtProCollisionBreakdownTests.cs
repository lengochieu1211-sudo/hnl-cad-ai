using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProCollisionBreakdownTests
    {
        [TestMethod]
        public void DenseMepStress_ReportsCollisionBreakdown()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProConservative,
                MainDirection = MainDirectionMode.Auto,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = false,
                UseAvoidance = true,
                ShiftAllForAvoidance = true,
                ClearanceDistance = 10.0,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0), new Point2(9000, 0), new Point2(9000, 2800),
                new Point2(6500, 2800), new Point2(6500, 6200), new Point2(3600, 6200),
                new Point2(3600, 7800), new Point2(0, 7800)
            });
            var context = new VxtLayoutContext();
            context.GeneralObstacles.AddRange(new[]
            {
                new Box2(900, 760, 1500, 820), new Box2(2400, 1510, 3100, 1570),
                new Box2(4700, 2260, 5400, 2320), new Box2(7000, 3110, 7600, 3170),
                new Box2(5200, 3860, 5900, 3920), new Box2(2500, 4610, 3200, 4670),
                new Box2(800, 5360, 1400, 5420), new Box2(4300, 6110, 5000, 6170),
                new Box2(1800, 6860, 2500, 6920), new Box2(700, 7310, 1300, 7370)
            });

            var plan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, context);
            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(0, plan.Quality.CollisionCount,
                "Collision breakdown: XC=" + plan.Quality.MainCollisionCount +
                ", XP=" + plan.Quality.FurringCollisionCount +
                ", Ty=" + plan.Quality.HangerCollisionCount + ".");
        }
    }
}
