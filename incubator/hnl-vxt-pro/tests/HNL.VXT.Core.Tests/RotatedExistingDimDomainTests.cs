using System;
using HNL.VXT.Core.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class RotatedExistingDimDomainTests
    {
        [DataTestMethod]
        [DataRow(0.0)]
        [DataRow(30.0)]
        [DataRow(45.0)]
        [DataRow(90.0)]
        public void LocalDomainContainsAllFourWorldExtentCorners(double angleDegrees)
        {
            var worldBox = new Box2(200.0, -300.0, 500.0, 100.0);
            var radians = angleDegrees * Math.PI / 180.0;
            var local = Transform2.ToLocalBounds(worldBox, radians);

            var corners = new[]
            {
                new Point2(worldBox.MinX, worldBox.MinY),
                new Point2(worldBox.MinX, worldBox.MaxY),
                new Point2(worldBox.MaxX, worldBox.MinY),
                new Point2(worldBox.MaxX, worldBox.MaxY)
            };
            foreach (var corner in corners)
                Assert.IsTrue(local.Contains(Transform2.ToLocal(corner, radians), 1e-8),
                    "The local DIM domain must include every corner of the transformed block extents.");
        }

        [TestMethod]
        public void DiagonalRotatedExtent_TwoCornerProjectionWouldMissTheOtherCorners()
        {
            var box = new Box2(0.0, 0.0, 1000.0, 1000.0);
            var radians = Math.PI / 4.0;
            var oldTwoCornerDomain = Box2.FromPoints(new[]
            {
                Transform2.ToLocal(new Point2(box.MinX, box.MinY), radians),
                Transform2.ToLocal(new Point2(box.MaxX, box.MaxY), radians)
            });
            var newDomain = Transform2.ToLocalBounds(box, radians);
            Assert.IsTrue(newDomain.Height > 1000.0, "Rotated 45 degree square must have a nonzero local Y span.");
            Assert.IsTrue(oldTwoCornerDomain.Height < 1e-6,
                "This fixture must reproduce the former two-corner underestimation.");
            Assert.IsTrue(newDomain.Contains(Transform2.ToLocal(new Point2(1000.0, 0.0), radians), 1e-6));
            Assert.IsTrue(newDomain.Contains(Transform2.ToLocal(new Point2(0.0, 1000.0), radians), 1e-6));
        }
    }
}
