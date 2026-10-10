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
    public sealed class StrictMultipleV67615Tests
    {
        [TestMethod]
        public void Main4000_StrictMultiple_Prefers850With300Edges()
        {
            var r = SmartLayout1D.Calculate(
                4000.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds);

            Assert.IsNotNull(r);
            Assert.AreEqual(300.0, r.StartOffset, 0.001);
            Assert.AreEqual(300.0, r.EndOffset, 0.001);
            CollectionAssert.AreEqual(
                new[] { 850.0, 850.0, 850.0, 850.0 },
                r.Steps.ToArray());
        }

        [TestMethod]
        public void Main38592_StrictMultiple_KeepsLastTyGeometryAndAllStepsOn50()
        {
            var r = SmartLayout1D.Calculate(
                38592.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds);

            Assert.IsNotNull(r);
            Assert.AreEqual(38, r.Steps.Count);
            Assert.AreEqual(321.0, r.StartOffset, 0.001);
            Assert.AreEqual(321.0, r.EndOffset, 0.001);
            Assert.AreEqual(950.0, r.Steps.Min(), 0.001);
            Assert.AreEqual(1000.0, r.Steps.Max(), 0.001);
            Assert.IsTrue(r.Steps.All(x => Math.Abs(x / 50.0 - Math.Round(x / 50.0)) < 1e-9));
            Assert.AreEqual(38592.0, r.StartOffset + r.Steps.Sum() + r.EndOffset, 0.001);
        }

        [TestMethod]
        public void Hanger40955_StrictMultiple_StaysInsideHardMaxAndPreservesBothEnds()
        {
            var r = SmartLayout1D.Calculate(
                40955.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds);

            Assert.IsNotNull(r);
            Assert.AreEqual(41, r.Steps.Count);
            Assert.IsTrue(r.StartOffset >= 300.0 - 0.1 && r.StartOffset <= 400.0 + 0.1);
            Assert.IsTrue(r.EndOffset >= 300.0 - 0.1 && r.EndOffset <= 400.0 + 0.1);
            Assert.IsTrue(r.Steps.All(x => x >= 700.0 - 0.1 && x <= 1000.0 + 0.1));
            Assert.IsTrue(r.Steps.All(x => Math.Abs(x / 50.0 - Math.Round(x / 50.0)) < 1e-9));
            Assert.AreEqual(40955.0, r.StartOffset + r.Steps.Sum() + r.EndOffset, 0.001);
        }

        [TestMethod]
        public void DenseFallback_LowersMinOnlyWhenNormalSolutionDoesNotExist()
        {
            var r = SmartLayout1D.Calculate(
                1000.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds);

            Assert.IsNotNull(r);
            Assert.IsTrue(r.IsDense);
            Assert.AreEqual(300.0, r.StartOffset, 0.001);
            Assert.AreEqual(300.0, r.EndOffset, 0.001);
            CollectionAssert.AreEqual(new[] { 400.0 }, r.Steps.ToArray());
        }

        [TestMethod]
        public void Furring_Default1220Div3_RemainsExactFixedPitch()
        {
            var settings = new VxtSettings
            {
                DrawMain = false,
                DrawHangers = false,
                DrawFurring = true,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                UseAvoidance = false,
                FurringSpacing = 1220.0 / 3.0
            };

            var boundary = Rectangle(5000.0, 3000.0);
            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings);
            var xs = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Furring)
                .Select(x => x.A.X)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            Assert.IsTrue(xs.Length > 3);
            for (var i = 1; i < xs.Length; i++)
                Assert.AreEqual(1220.0 / 3.0, xs[i] - xs[i - 1], 0.001);
        }

        private static Boundary2 Rectangle(double width, double height)
            => new Boundary2(new[]
            {
                new Point2(0, 0),
                new Point2(width, 0),
                new Point2(width, height),
                new Point2(0, height)
            });
    }
}
