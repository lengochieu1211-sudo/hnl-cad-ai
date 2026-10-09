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
    public sealed class VxtProDimensionPackerTests
    {
        [TestMethod]
        public void ExactDuplicateDimension_IsRemoved()
        {
            var d = Horizontal(0, 1000, 100, DimensionTarget.Main);
            var result = VxtProDimensionPacker.Pack(new[] { d, d }, 350.0);

            Assert.AreEqual(1, result.Dimensions.Count);
            Assert.AreEqual(1, result.RemovedDuplicateCount);
        }

        [TestMethod]
        public void OverlappingChains_TooClose_AreSeparatedByMinimumRowSpacing()
        {
            var dimensions = new[]
            {
                Horizontal(0, 1000, 100, DimensionTarget.Main),
                Horizontal(1000, 2000, 100, DimensionTarget.Main),
                Horizontal(500, 1500, 120, DimensionTarget.Furring)
            };

            var result = VxtProDimensionPacker.Pack(dimensions, 350.0);
            Assert.AreEqual(3, result.Dimensions.Count);
            Assert.AreEqual(1, result.ShiftedChainCount);

            var mainY = result.Dimensions.First(x => x.Target == DimensionTarget.Main).DimensionLinePoint.Y;
            var furringY = result.Dimensions.First(x => x.Target == DimensionTarget.Furring).DimensionLinePoint.Y;
            Assert.IsTrue(Math.Abs(mainY - furringY) >= 350.0 - 0.1,
                "Overlapping DIM chains must be packed onto rows separated by the requested spacing.");
        }

        [TestMethod]
        public void NonOverlappingChains_CanShareCloseRowsWithoutArtificialExpansion()
        {
            var dimensions = new[]
            {
                Horizontal(0, 1000, 100, DimensionTarget.Main),
                Horizontal(2000, 3000, 120, DimensionTarget.Furring)
            };

            var result = VxtProDimensionPacker.Pack(dimensions, 350.0);
            Assert.AreEqual(0, result.ShiftedChainCount);
            Assert.AreEqual(100.0, result.Dimensions[0].DimensionLinePoint.Y, 1e-8);
            Assert.AreEqual(120.0, result.Dimensions[1].DimensionLinePoint.Y, 1e-8);
        }

        [TestMethod]
        public void Packing_MovesOnlyDimLinePoint_NotMeasuredExtensionPoints()
        {
            var a = Horizontal(0, 1000, 100, DimensionTarget.Main);
            var b = Horizontal(0, 1000, 120, DimensionTarget.Furring);
            var result = VxtProDimensionPacker.Pack(new[] { a, b }, 350.0);
            var packedB = result.Dimensions.Single(x => x.Target == DimensionTarget.Furring);

            Assert.AreEqual(b.ExtensionPoint1.X, packedB.ExtensionPoint1.X, 1e-10);
            Assert.AreEqual(b.ExtensionPoint1.Y, packedB.ExtensionPoint1.Y, 1e-10);
            Assert.AreEqual(b.ExtensionPoint2.X, packedB.ExtensionPoint2.X, 1e-10);
            Assert.AreEqual(b.ExtensionPoint2.Y, packedB.ExtensionPoint2.Y, 1e-10);
            Assert.AreNotEqual(b.DimensionLinePoint.Y, packedB.DimensionLinePoint.Y);
        }

        private static PreviewDimension Horizontal(double x1, double x2, double y, DimensionTarget target)
            => new PreviewDimension(
                new Point2(x1, 0),
                new Point2(x2, 0),
                new Point2((x1 + x2) * 0.5, y),
                0.0,
                target);
    }
}
