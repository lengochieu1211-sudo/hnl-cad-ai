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
    public sealed class LargeAreaHangerCoverageTests
    {
        [TestMethod]
        public void FiveHundredSquareMetreCeiling_KeepsTyAtBothEndsOfEveryMain()
        {
            var settings = DefaultLargeAreaSettings();

            // 25m x 20m = 500m2.
            var boundary = Rectangle(25000.0, 20000.0);
            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings);
            AssertEveryMainHasLegalEndHangers(plan, settings);
        }

        [TestMethod]
        public void NearSquareFiveHundredSquareMetreCeiling_AlsoKeepsLastTy()
        {
            var settings = DefaultLargeAreaSettings();
            var side = Math.Sqrt(500.0) * 1000.0;
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(side, side), settings);
            AssertEveryMainHasLegalEndHangers(plan, settings);
        }

        [TestMethod]
        public void Regression_501696SquareMetres_Previous296mmEndTyIsNotDropped()
        {
            var settings = DefaultLargeAreaSettings();

            // Exact former failure:
            // 38.592m x 13m = 501.696m2.
            // Legacy soft-penalty winner was 296 + 38*1000 + 296.
            // BuildHangerRow kept the first 296mm Ty but filtered the LAST 296mm Ty because
            // HangerMinEdgeOffset is 300mm, leaving the previous Ty about 1296mm from the edge.
            var width = 38592.0;
            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(width, 13000.0), settings);
            AssertEveryMainHasLegalEndHangers(plan, settings);

            var firstMain = plan.Lines.First(x => x.Kind == PreviewLineKind.Main);
            var xs = plan.HangerPoints
                .Where(p => Math.Abs(p.Y - firstMain.A.Y) < 0.01)
                .Select(p => p.X)
                .OrderBy(x => x)
                .ToArray();

            Assert.AreEqual(39, xs.Length, "Same economic 38-gap layout should be retained, including final Ty.");
            Assert.AreEqual(321.0, xs[0], 0.01);
            Assert.AreEqual(width - 321.0, xs[xs.Length - 1], 0.01);
            Assert.AreEqual(950.0, xs.Zip(xs.Skip(1), (a, b) => b - a).Min(), 0.01,
                "Repair should use one near-centre 950mm residual gap and keep other gaps near Max.");
        }

        [TestMethod]
        public void LongRunSmartLayout_HardRespectsEndBand_WhenLegacyScoreWouldViolateIt()
        {
            // 38.592m used to select 296mm (below Min) and 40.955m used to select 477.5mm
            // (above Max). Both are representative >500m2 widths with a 13m ceiling depth.
            foreach (var length in new[] { 38592.0, 40955.0 })
            {
                var layout = SmartLayout1D.Calculate(
                    length,
                    maxSpacing: 1000.0,
                    minSpacing: 700.0,
                    maxEdge: 400.0,
                    minEdge: 300.0,
                    increment: 50.0,
                    mode: MainLayoutMode.BalancedTwoEnds);

                Assert.IsNotNull(layout);
                Assert.IsTrue(layout.StartOffset >= 300.0 - 0.01 && layout.StartOffset <= 400.0 + 0.01,
                    "Start Ty edge must stay inside 300..400mm for run " + length);
                Assert.IsTrue(layout.EndOffset >= 300.0 - 0.01 && layout.EndOffset <= 400.0 + 0.01,
                    "Last Ty edge must stay inside 300..400mm for run " + length);
                Assert.IsTrue(layout.Steps.All(x => x >= 700.0 - 0.01 && x <= 1000.0 + 0.01));
            }
        }

        private static VxtSettings DefaultLargeAreaSettings()
            => new VxtSettings
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

        private static void AssertEveryMainHasLegalEndHangers(VxtPreviewPlan plan, VxtSettings settings)
        {
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
                    Assert.IsTrue(gap >= settings.HangerMinSpacing - 0.1,
                        "Ty spacing below Min on main at Y=" + main.A.Y);
                    Assert.IsTrue(gap <= settings.HangerMaxSpacing + 0.1,
                        "Ty spacing above Max on main at Y=" + main.A.Y);
                }
            }
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
