using System;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class LargeAreaHangerCoverageTests
    {
        [TestMethod]
        public void FiveHundredSquareMetreCeiling_KeepsTyAtBothEndsOfEveryMain()
        {
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                HangerMinSpacing = 700.0,
                HangerMaxSpacing = 1000.0,
                HangerMinEdgeOffset = 300.0,
                HangerMaxEdgeOffset = 400.0,
                HangerBalanceStep = 50.0,
                UseAvoidance = false
            };

            // 25m x 20m = 500m2, representative of the user's reported runtime case.
            var boundary = Rectangle(25000.0, 20000.0);
            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings);
            var mains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToArray();

            Assert.IsTrue(mains.Length > 1, "Large preview must contain many main members, not only one.");
            foreach (var main in mains)
            {
                var minX = Math.Min(main.A.X, main.B.X);
                var maxX = Math.Max(main.A.X, main.B.X);
                var xs = plan.HangerPoints
                    .Where(p => Math.Abs(p.Y - main.A.Y) < 0.01 && p.X >= minX - 0.01 && p.X <= maxX + 0.01)
                    .Select(p => p.X)
                    .OrderBy(x => x)
                    .ToArray();

                Assert.IsTrue(xs.Length >= 2, "Every main member must receive Ty at both ends.");
                Assert.IsTrue(xs[0] - minX >= settings.HangerMinEdgeOffset - 0.1);
                Assert.IsTrue(xs[0] - minX <= settings.HangerMaxEdgeOffset + 0.1);
                Assert.IsTrue(maxX - xs[xs.Length - 1] >= settings.HangerMinEdgeOffset - 0.1);
                Assert.IsTrue(maxX - xs[xs.Length - 1] <= settings.HangerMaxEdgeOffset + 0.1,
                    "Last Ty must stay inside the maximum end offset.");

                for (var i = 0; i + 1 < xs.Length; i++)
                {
                    var gap = xs[i + 1] - xs[i];
                    Assert.IsTrue(gap >= settings.HangerMinSpacing - 0.1);
                    Assert.IsTrue(gap <= settings.HangerMaxSpacing + 0.1);
                }
            }
        }

        [TestMethod]
        public void NearSquareFiveHundredSquareMetreCeiling_AlsoKeepsLastTy()
        {
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                UseAvoidance = false
            };

            var side = Math.Sqrt(500.0) * 1000.0;
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(side, side), settings);
            var main = plan.Lines.First(x => x.Kind == PreviewLineKind.Main);
            var minX = Math.Min(main.A.X, main.B.X);
            var maxX = Math.Max(main.A.X, main.B.X);
            var xs = plan.HangerPoints.Where(p => Math.Abs(p.Y - main.A.Y) < 0.01).Select(p => p.X).OrderBy(x => x).ToArray();

            Assert.IsTrue(xs.Length > 2);
            Assert.IsTrue(maxX - xs[xs.Length - 1] <= settings.HangerMaxEdgeOffset + 0.1);
            Assert.IsTrue(maxX - xs[xs.Length - 1] >= settings.HangerMinEdgeOffset - 0.1);
            Assert.IsTrue(xs[0] - minX <= settings.HangerMaxEdgeOffset + 0.1);
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
