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
    public sealed class PolylineDirectionLayoutTests
    {
        [TestMethod]
        public void LPath_PartitionsBoundaryBySharedBisectorWithoutAreaLoss()
        {
            var boundary = Rectangle(6000.0, 4000.0);
            var path = new[]
            {
                new Point2(1000.0, 2000.0),
                new Point2(3000.0, 2000.0),
                new Point2(3000.0, 3500.0)
            };

            var regions = VxtPolylineDirectionPartitioner.Partition(boundary, path);

            Assert.AreEqual(2, regions.Count);
            Assert.AreEqual(0.0, regions[0].MainAngleDegrees, 0.001);
            Assert.AreEqual(90.0, regions[1].MainAngleDegrees, 0.001);
            Assert.IsNotNull(regions[0].RegionBoundary);
            Assert.IsNotNull(regions[1].RegionBoundary);

            var totalArea = regions.Sum(x => Math.Abs(Area(x.RegionBoundary)));
            Assert.AreEqual(6000.0 * 4000.0, totalArea, 0.1,
                "Adjacent path regions must cover the ceiling without a gap or area overlap.");
        }

        [TestMethod]
        public void LPath_BuilderProducesMainMembersInBothSegmentDirections()
        {
            var boundary = Rectangle(6000.0, 4000.0);
            var path = new[]
            {
                new Point2(1000.0, 2000.0),
                new Point2(3000.0, 2000.0),
                new Point2(3000.0, 3500.0)
            };
            var regions = VxtPolylineDirectionPartitioner.Partition(boundary, path);

            var settings = new VxtSettings
            {
                MainDirection = MainDirectionMode.PolylinePath,
                DirectionDegrees = 0.0,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true,
                UseAvoidance = false
            };
            var context = new VxtLayoutContext { BoundaryIndex = 0 };
            context.Regions.AddRange(regions);

            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings, context);
            var main = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToList();

            Assert.IsTrue(main.Any(IsHorizontal), "First path leg must produce horizontal XC.");
            Assert.IsTrue(main.Any(IsVertical), "Second path leg must produce vertical XC.");
            Assert.IsTrue(plan.FurringSegmentCount > 0);
            Assert.IsTrue(plan.HangerCount > 0);
            Assert.IsTrue(plan.DimensionSegmentCount > 0);
        }

        [TestMethod]
        public void ClosedSquarePath_PartitionsBoundaryCyclicallyWithoutAreaLoss()
        {
            var boundary = Rectangle(6000.0, 6000.0);
            var path = new[]
            {
                new Point2(1500.0, 1500.0),
                new Point2(4500.0, 1500.0),
                new Point2(4500.0, 4500.0),
                new Point2(1500.0, 4500.0),
                new Point2(1500.0, 1500.0)
            };

            var regions = VxtPolylineDirectionPartitioner.Partition(boundary, path);

            Assert.AreEqual(4, regions.Count);
            Assert.AreEqual(0.0, regions[0].MainAngleDegrees, 0.001);
            Assert.AreEqual(90.0, regions[1].MainAngleDegrees, 0.001);
            Assert.AreEqual(0.0, regions[2].MainAngleDegrees, 0.001);
            Assert.AreEqual(90.0, regions[3].MainAngleDegrees, 0.001);

            var totalArea = regions.Sum(x => Math.Abs(Area(x.RegionBoundary)));
            Assert.AreEqual(6000.0 * 6000.0, totalArea, 0.1,
                "Closed guide regions must cover the ceiling exactly once.");
        }

        [TestMethod]
        public void ClosedSquarePath_WithInnerHole_DoesNotFrameTheVoid()
        {
            var boundary = Rectangle(6000.0, 6000.0);
            var hole = new Boundary2(new[]
            {
                new Point2(2500.0, 2500.0),
                new Point2(3500.0, 2500.0),
                new Point2(3500.0, 3500.0),
                new Point2(2500.0, 3500.0)
            });
            var path = new[]
            {
                new Point2(1500.0, 1500.0),
                new Point2(4500.0, 1500.0),
                new Point2(4500.0, 4500.0),
                new Point2(1500.0, 4500.0),
                new Point2(1500.0, 1500.0)
            };
            var regions = VxtPolylineDirectionPartitioner.Partition(boundary, path).ToList();

            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.PolylinePath,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true,
                UseAvoidance = false,
                UseLocalMainAdd = false
            };
            var context = new VxtLayoutContext();
            context.BoundaryRegionGroups.Add(regions);
            context.BoundaryHoleGroups.Add(new List<Boundary2> { hole });

            var plan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, context);

            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.FurringSegmentCount > 0);
            Assert.IsTrue(plan.HangerCount > 0);
            Assert.IsFalse(plan.HangerPoints.Any(IsInsideCenterHole),
                "Ty must not be created inside the annular void.");

            foreach (var line in plan.Lines.Where(x =>
                x.Kind == PreviewLineKind.Main || x.Kind == PreviewLineKind.Furring))
            {
                Assert.IsFalse(CrossesCenterHole(line),
                    "XC/XP must be split at the annular inner boundary.");
            }
        }

        private static bool IsInsideCenterHole(Point2 point)
            => point.X > 2500.0 && point.X < 3500.0 &&
               point.Y > 2500.0 && point.Y < 3500.0;

        private static bool CrossesCenterHole(PreviewLine line)
        {
            var horizontal = Math.Abs(line.A.Y - line.B.Y) < 0.01;
            if (horizontal &&
                line.A.Y > 2500.0 && line.A.Y < 3500.0)
            {
                var minX = Math.Min(line.A.X, line.B.X);
                var maxX = Math.Max(line.A.X, line.B.X);
                if (minX < 3500.0 && maxX > 2500.0) return true;
            }

            var vertical = Math.Abs(line.A.X - line.B.X) < 0.01;
            if (vertical &&
                line.A.X > 2500.0 && line.A.X < 3500.0)
            {
                var minY = Math.Min(line.A.Y, line.B.Y);
                var maxY = Math.Max(line.A.Y, line.B.Y);
                if (minY < 3500.0 && maxY > 2500.0) return true;
            }

            return false;
        }

        [TestMethod]
        public void ClosedConcavePath_IsRejectedInsteadOfProducingOverlappingRegions()
        {
            var boundary = Rectangle(6000.0, 6000.0);
            var path = new[]
            {
                new Point2(1000.0, 1000.0),
                new Point2(5000.0, 1000.0),
                new Point2(5000.0, 5000.0),
                new Point2(3000.0, 3000.0),
                new Point2(1000.0, 5000.0),
                new Point2(1000.0, 1000.0)
            };

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => VxtPolylineDirectionPartitioner.Partition(boundary, path));
            StringAssert.Contains(ex.Message, "Tuyến kín lõm");
        }

        [TestMethod]
        public void PathWithImmediateReverse_IsRejected()
        {
            var boundary = Rectangle(6000.0, 4000.0);
            var path = new[]
            {
                new Point2(1000.0, 2000.0),
                new Point2(3000.0, 2000.0),
                new Point2(1000.0, 2000.0)
            };

            Assert.ThrowsException<InvalidOperationException>(
                () => VxtPolylineDirectionPartitioner.Partition(boundary, path));
        }

        private static bool IsHorizontal(PreviewLine line)
            => Math.Abs(line.A.Y - line.B.Y) < 0.01 &&
               Math.Abs(line.A.X - line.B.X) > 5.0;

        private static bool IsVertical(PreviewLine line)
            => Math.Abs(line.A.X - line.B.X) < 0.01 &&
               Math.Abs(line.A.Y - line.B.Y) > 5.0;

        private static double Area(Boundary2 boundary)
        {
            var sum = 0.0;
            for (var i = 0; i < boundary.Vertices.Count; i++)
            {
                var a = boundary.Vertices[i];
                var b = boundary.Vertices[(i + 1) % boundary.Vertices.Count];
                sum += a.X * b.Y - b.X * a.Y;
            }
            return sum * 0.5;
        }

        private static Boundary2 Rectangle(double width, double height)
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(width, 0.0),
                new Point2(width, height),
                new Point2(0.0, height)
            });
    }
}
