using System;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public class LayInCeilingPlannerTests
    {
        [TestMethod]
        public void Module600Square_UsesT3600_T1200_T600()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced,
                    DrawHangers = true
                });

            Assert.AreEqual(3600.0, plan.MainTeeStockLength, 0.001);
            Assert.AreEqual(1200.0, plan.LongCrossTeeStockLength, 0.001);
            Assert.AreEqual(600.0, plan.ShortCrossTeeStockLength, 0.001);
            Assert.AreEqual("HNL_CF_600X600", plan.HatchPatternName);
            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.LongCrossSegmentCount > 0);
            Assert.IsTrue(plan.ShortCrossSegmentCount > 0);
            Assert.IsTrue(plan.HangerPoints.Count > 0);
        }

        [TestMethod]
        public void Module610Square_UsesT3660_T1220_T610()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6100, 4880),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module610x610,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced
                });

            Assert.AreEqual(3660.0, plan.MainTeeStockLength, 0.001);
            Assert.AreEqual(1220.0, plan.LongCrossTeeStockLength, 0.001);
            Assert.AreEqual(610.0, plan.ShortCrossTeeStockLength, 0.001);
            Assert.AreEqual("HNL_CF_610X610", plan.HatchPatternName);
            Assert.IsTrue(plan.ShortCrossTeeCount > 0);
        }

        [TestMethod]
        public void RectangularPanel_HasNoShortCrossTee()
        {
            foreach (var system in new[]
            {
                LayInGridSystem.Module600x1200,
                LayInGridSystem.Module610x1220
            })
            {
                var plan = LayInCeilingPlanner.Build(
                    Rect(0, 0, 7200, 4800),
                    null,
                    new LayInCeilingSettings
                    {
                        GridSystem = system,
                        MainDirection = LayInMainDirectionMode.Horizontal,
                        StartMode = LayInStartMode.Balanced
                    });

                Assert.AreEqual(0, plan.ShortCrossSegmentCount, system.ToString());
                Assert.AreEqual(0, plan.ShortCrossTeeCount, system.ToString());
                Assert.IsTrue(plan.LongCrossTeeCount > 0, system.ToString());
            }
        }

        [TestMethod]
        public void LongCross_IsPerpendicularToMain_AndShortCrossParallelToMain()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced
                });

            var main = plan.TeeSegments.First(x => x.Kind == LayInTeeKind.MainTee);
            var longCross = plan.TeeSegments.First(x => x.Kind == LayInTeeKind.LongCrossTee);
            var shortCross = plan.TeeSegments.First(x => x.Kind == LayInTeeKind.ShortCrossTee);

            Assert.IsTrue(Math.Abs(main.A.Y - main.B.Y) < 0.001);
            Assert.IsTrue(Math.Abs(longCross.A.X - longCross.B.X) < 0.001);
            Assert.IsTrue(Math.Abs(shortCross.A.Y - shortCross.B.Y) < 0.001);
        }

        [TestMethod]
        public void ParallelLongAndShortSide_ResolveDifferentAxes()
        {
            var boundary = Rect(0, 0, 8000, 3000);

            var alongLong = LayInCeilingPlanner.Build(
                boundary,
                null,
                new LayInCeilingSettings
                {
                    MainDirection = LayInMainDirectionMode.ParallelLongSide,
                    GridSystem = LayInGridSystem.Module600x600
                });

            var alongShort = LayInCeilingPlanner.Build(
                boundary,
                null,
                new LayInCeilingSettings
                {
                    MainDirection = LayInMainDirectionMode.ParallelShortSide,
                    GridSystem = LayInGridSystem.Module600x600
                });

            AssertAngleEquivalent(0.0, alongLong.MainAngleRadians);
            AssertAngleEquivalent(Math.PI * 0.5, alongShort.MainAngleRadians);
        }

        [TestMethod]
        public void FromDoor_UsesNearestBoundaryCornerAsHatchOriginReference()
        {
            var boundary = Rect(0, 0, 6000, 4800);
            var settings = new LayInCeilingSettings
            {
                GridSystem = LayInGridSystem.Module600x600,
                MainDirection = LayInMainDirectionMode.Horizontal,
                StartMode = LayInStartMode.FromDoor,
                DoorPoint = new Point2(40, 80)
            };

            var plan = LayInCeilingPlanner.Build(boundary, null, settings);

            Assert.AreEqual(0.0, plan.HatchOrigin.X, 0.001);
            Assert.AreEqual(0.0, plan.HatchOrigin.Y, 0.001);
        }

        [TestMethod]
        public void Balanced_Start_CentersRemainderOnBothSides()
        {
            var boundary = Rect(0, 0, 6500, 5000);
            var plan = LayInCeilingPlanner.Build(
                boundary,
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced
                });

            // 6500 = 10*600 + 500 => 250 mm balanced X edge.
            // 5000 = 4*1200 + 200 => 100 mm balanced Main Tee edge.
            Assert.AreEqual(250.0, plan.HatchOrigin.X, 0.001);
            Assert.AreEqual(100.0, plan.HatchOrigin.Y, 0.001);
        }

        [TestMethod]
        public void Hole_RemovesAllTeeFamiliesAndHangersFromVoid()
        {
            var hole = Rect(2400, 1800, 3600, 3000);
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                new[] { hole },
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced,
                    DrawHangers = true
                });

            foreach (var tee in plan.TeeSegments)
            {
                var mid = new Point2(
                    (tee.A.X + tee.B.X) * 0.5,
                    (tee.A.Y + tee.B.Y) * 0.5);
                Assert.IsFalse(Inside(mid, hole), tee.Kind + " entered the hole.");
            }

            Assert.IsFalse(plan.HangerPoints.Any(p => Inside(p, hole)));
        }

        [TestMethod]
        public void Hangers_AreGeneratedOnlyOnMainTeeRows()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    DrawHangers = true,
                    HangerMaxSpacing = 1200.0,
                    HangerEdgeTarget = 300.0
                });

            var mainYs = plan.TeeSegments
                .Where(x => x.Kind == LayInTeeKind.MainTee)
                .Select(x => Math.Round(x.A.Y, 3))
                .Distinct()
                .ToList();

            Assert.IsTrue(plan.HangerPoints.Count > 0);
            foreach (var hanger in plan.HangerPoints)
                Assert.IsTrue(mainYs.Contains(Math.Round(hanger.Y, 3)));
        }

        [TestMethod]
        public void GroupedDimension_FormatsTwelveModules()
        {
            var plan600 = LayInCeilingPlanner.Build(
                Rect(0, 0, 12000, 6000),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    DimensionMode = LayInDimensionMode.Grouped,
                    GroupedDimensionCount = 12
                });

            var plan610 = LayInCeilingPlanner.Build(
                Rect(0, 0, 12200, 6100),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module610x610,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    DimensionMode = LayInDimensionMode.Grouped,
                    GroupedDimensionCount = 12
                });

            Assert.IsTrue(plan600.DimensionRuns.Any(x => x.Label == "12 × 600 = 7200"));
            Assert.IsTrue(plan610.DimensionRuns.Any(x => x.Label == "12 × 610 = 7320"));
        }

        [TestMethod]
        public void AutoOptimize_ReturnsOnePrincipalAxis()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 9000, 3600),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.AutoOptimize,
                    StartMode = LayInStartMode.Balanced
                });

            var horizontal = AngleDistance(plan.MainAngleRadians, 0.0);
            var vertical = AngleDistance(plan.MainAngleRadians, Math.PI * 0.5);
            Assert.IsTrue(Math.Min(horizontal, vertical) < 0.001);
        }

        private static Boundary2 Rect(double minX, double minY, double maxX, double maxY)
            => new Boundary2(new[]
            {
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY)
            });

        private static bool Inside(Point2 p, Boundary2 b)
        {
            var bounds = b.GetBounds();
            return p.X > bounds.Min.X + 0.001 && p.X < bounds.Max.X - 0.001 &&
                   p.Y > bounds.Min.Y + 0.001 && p.Y < bounds.Max.Y - 0.001;
        }

        private static void AssertAngleEquivalent(double expected, double actual)
        {
            Assert.IsTrue(AngleDistance(expected, actual) < 0.001,
                "Expected angle " + expected + ", actual " + actual);
        }

        private static double AngleDistance(double a, double b)
        {
            var d = Math.Abs(a - b) % Math.PI;
            return Math.Min(d, Math.PI - d);
        }
    }
}
