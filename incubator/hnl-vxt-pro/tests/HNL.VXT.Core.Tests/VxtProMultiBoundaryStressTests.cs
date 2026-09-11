using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProMultiBoundaryStressTests
    {
        [TestMethod]
        public void ProEconomy_MultiBoundary_NeverExceedsIndependentAutoMaterialIndex()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProEconomy);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.DrawFurring = true;
            settings.DrawHangers = true;

            var boundaries = new[]
            {
                RotatedRectangle(6200.0, 4100.0, 18.0, 0.0, 0.0),
                RotatedRectangle(5600.0, 3600.0, 63.0, 9000.0, 500.0)
            };

            var independentParts = boundaries
                .Select(x => VxtProAutoDirectionPlanBuilder.Build(x, settings, new VxtLayoutContext()))
                .ToList();
            var independentQuality = VxtProPlanQualityEvaluator.Aggregate(independentParts.Select(x => x.Quality));

            var combined = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());

            Assert.IsNotNull(independentQuality);
            Assert.IsNotNull(combined.Quality);
            Assert.AreEqual(2, combined.Quality.BoundaryCount);
            Assert.AreEqual(0, combined.Quality.HardViolationCount);
            Assert.IsTrue(combined.Quality.MaterialIndex <= independentQuality.MaterialIndex + 0.1,
                "Pro Economy must not spend more aggregate material only to align multiple ceilings.");
        }

        [TestMethod]
        public void ProFixedDirection_MultiBoundary_ReportsSharedAlignment100()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.TwoPoints;
            settings.DirectionDegrees = 27.0;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[]
                {
                    RotatedRectangle(6000.0, 4000.0, 27.0, 0.0, 0.0),
                    RotatedRectangle(5200.0, 3200.0, 27.0, 8000.0, 0.0),
                    RotatedRectangle(4500.0, 2800.0, 27.0, 14500.0, 500.0)
                },
                settings,
                new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(3, plan.Quality.BoundaryCount);
            Assert.AreEqual(1, plan.Quality.DistinctDirectionCount);
            Assert.AreEqual(100, plan.Quality.AlignmentScore100);
            Assert.IsTrue(plan.Quality.UsesSharedDirection);
        }

        [TestMethod]
        public void ProAuto_MultiBoundary_ProducesOneCompactGlobalSummary()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.Auto;
            var boundaries = new[]
            {
                RotatedRectangle(6000.0, 4000.0, 25.0, 0.0, 0.0),
                RotatedRectangle(5800.0, 3900.0, 25.0, 8500.0, 0.0)
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(2, plan.Quality.BoundaryCount);
            Assert.IsTrue(plan.Quality.AlignmentScore100 >= 75);
            Assert.AreEqual(1, plan.Texts.Count(x => x.Text != null && x.Text.StartsWith("HNL Pro Tổng Q", StringComparison.Ordinal)));
            Assert.AreEqual(0, plan.Texts.Count(x => x.Text != null && x.Text.StartsWith("HNL Pro Q", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void ProAuto_MultiBoundary_PerpendicularCeilings_DoNotForceOneSharedAxis()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.AutoShadowline = true;

            var boundaries = new[]
            {
                RotatedRectangle(6000.0, 4000.0, 0.0, 0.0, 0.0),
                RotatedRectangle(6000.0, 4000.0, 90.0, 9000.0, 0.0)
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(2, plan.Quality.BoundaryCount);
            Assert.IsFalse(plan.Quality.UsesSharedDirection,
                "Two ceilings with perpendicular natural axes must not be forced onto one shared Pro Auto direction.");
            Assert.AreEqual(2, plan.Quality.DistinctDirectionCount,
                "Perpendicular ceilings should retain their own stable orientation families when both are clear.");
        }

        [TestMethod]
        public void ProAuto_MultiBoundary_ThirtyDegreeDifference_DoesNotForceSharedAxis()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProBalanced);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.AutoShadowline = true;

            var boundaries = new[]
            {
                RotatedRectangle(6000.0, 4000.0, 0.0, 0.0, 0.0),
                RotatedRectangle(6000.0, 4000.0, 30.0, 9000.0, 0.0)
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(boundaries, settings, new VxtLayoutContext());

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(2, plan.Quality.BoundaryCount);
            Assert.IsFalse(plan.Quality.UsesSharedDirection,
                "Ceilings whose natural construction axes differ by 30 degrees must keep independent Auto directions when both are clear.");
            Assert.AreEqual(2, plan.Quality.DistinctDirectionCount,
                "A moderate angular difference is not a cosmetic alignment case; Pro must not rotate an otherwise valid ceiling merely to share one axis.");
        }

        [TestMethod]
        public void Stress_ConcaveCeiling_WithManyMepBands_RemainsClearAndHardValid()
        {
            var settings = BaseSettings(VxtOptimizationMode.ProConservative);
            settings.MainDirection = MainDirectionMode.Auto;
            settings.DrawFurring = true;
            settings.DrawHangers = true;
            settings.UseAvoidance = true;
            settings.ShiftAllForAvoidance = true;
            settings.ClearanceDistance = 10.0;

            var boundary = new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(9000, 0),
                new Point2(9000, 2800),
                new Point2(6500, 2800),
                new Point2(6500, 6200),
                new Point2(3600, 6200),
                new Point2(3600, 7800),
                new Point2(0, 7800)
            });

            var context = new VxtLayoutContext();
            var mep = new[]
            {
                new Box2(900, 760, 1500, 820),
                new Box2(2400, 1510, 3100, 1570),
                new Box2(4700, 2260, 5400, 2320),
                new Box2(7000, 3110, 7600, 3170),
                new Box2(5200, 3860, 5900, 3920),
                new Box2(2500, 4610, 3200, 4670),
                new Box2(800, 5360, 1400, 5420),
                new Box2(4300, 6110, 5000, 6170),
                new Box2(1800, 6860, 2500, 6920),
                new Box2(700, 7310, 1300, 7370)
            };
            context.GeneralObstacles.AddRange(mep);

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, context);

            Assert.IsNotNull(plan.Quality);
            Assert.AreEqual(0, plan.Quality.HardViolationCount);
            Assert.AreEqual(0, plan.Quality.CollisionCount,
                "Dense MEP stress must remain collision-free when a legal Pro layout exists.");
            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0);
            Assert.IsTrue(plan.HangerCount > 0);

            var finalMain = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToList();
            Assert.IsTrue(finalMain.Count > 0);
            foreach (var hanger in plan.HangerPoints)
            {
                Assert.IsTrue(finalMain.Any(x => DistanceToSegment(hanger, x.A, x.B) <= 0.5),
                    "Every final Ty must remain supported by a surviving final XC segment after MEP split; unsupported Ty=" +
                    hanger.X.ToString("0.###") + "," + hanger.Y.ToString("0.###"));
            }
        }

        private static double DistanceToSegment(Point2 p, Point2 a, Point2 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 1e-12) return p.DistanceTo(a);
            var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return p.DistanceTo(new Point2(a.X + t * dx, a.Y + t * dy));
        }

        private static VxtSettings BaseSettings(VxtOptimizationMode mode)
            => new VxtSettings
            {
                OptimizationMode = mode,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

        private static Boundary2 RotatedRectangle(
            double width,
            double height,
            double degrees,
            double offsetX,
            double offsetY)
        {
            var r = degrees * Math.PI / 180.0;
            var c = Math.Cos(r);
            var s = Math.Sin(r);
            Point2 Rotate(double x, double y)
                => new Point2(offsetX + x * c - y * s, offsetY + x * s + y * c);
            return new Boundary2(new[]
            {
                Rotate(0.0, 0.0),
                Rotate(width, 0.0),
                Rotate(width, height),
                Rotate(0.0, height)
            });
        }
    }
}