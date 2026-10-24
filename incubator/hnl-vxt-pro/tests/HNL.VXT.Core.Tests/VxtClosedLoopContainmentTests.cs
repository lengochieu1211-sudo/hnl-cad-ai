using System;
using System.Linq;
using HNL.VXT.Core.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtClosedLoopContainmentTests
    {
        [TestMethod]
        public void RectangleWithInnerVoid_IsContained()
        {
            Assert.IsTrue(VxtClosedLoopContainment.ContainsWholeLoop(
                Rect(3, 3, 7, 7), Rect(0, 0, 10, 10)));
        }

        [TestMethod]
        public void ThinConcaveNotch_RejectsCrossingEvenWhenCornersAndMidpointsAppearInside()
        {
            var outer = new Boundary2(new[]
            {
                P(0, 0), P(10, 0), P(10, 10),
                P(3.2, 10), P(3.2, 6), P(3, 6), P(3, 10), P(0, 10)
            });
            var overlapping = Rect(1, 7, 9, 7.5);
            Assert.IsFalse(VxtClosedLoopContainment.ContainsWholeLoop(overlapping, outer),
                "A thin notch can cross between vertex and midpoint probes.");
        }

        [TestMethod]
        public void TouchingBoundary_IsNotAutomaticallyTreatedAsCeilingVoid()
        {
            Assert.IsFalse(VxtClosedLoopContainment.ContainsWholeLoop(
                Rect(2, 0, 4, 4), Rect(0, 0, 10, 10)));
        }

        [TestMethod]
        public void DisjointBoundaries_DoNotBecomeHoles()
        {
            Assert.IsFalse(VxtClosedLoopContainment.ContainsWholeLoop(
                Rect(12, 12, 14, 14), Rect(0, 0, 10, 10)));
        }

        [TestMethod]
        public void TwoClosedCurvedLoops_SimulateSplineRingAsOneHole()
        {
            var outer = Circle(3000, 512);
            var inner = Circle(1200, 512);
            Assert.IsTrue(VxtClosedLoopContainment.ContainsWholeLoop(inner, outer),
                "Nested curved loop must remain a valid inner void.");
            Assert.IsFalse(VxtClosedLoopContainment.ContainsWholeLoop(outer, inner));
        }

        private static Boundary2 Rect(double x1, double y1, double x2, double y2)
            => new Boundary2(new[] { P(x1, y1), P(x2, y1), P(x2, y2), P(x1, y2) });

        private static Boundary2 Circle(double radius, int segments)
            => new Boundary2(Enumerable.Range(0, segments)
                .Select(i =>
                {
                    var a = 2 * Math.PI * i / segments;
                    return P(radius * Math.Cos(a), radius * Math.Sin(a));
                }));

        private static Point2 P(double x, double y) => new Point2(x, y);
    }
}
