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
    public sealed class EconomicLayoutTests
    {
        [TestMethod]
        public void MainLayout_WideRun_PrefersFewestMembersThenSpacingNearMax()
        {
            // User requirement: economy first. With L=6000, edge 300..400,
            // spacing 700..1000 and 50-mm adjustment, five gaps cannot satisfy
            // max=1000. The minimum legal count is six gaps, and the largest
            // legal rounded spacing is 900 with 300-mm edges.
            var result = SmartLayout1D.Calculate(
                6000.0,
                maxSpacing: 1000.0,
                minSpacing: 700.0,
                maxEdge: 400.0,
                minEdge: 300.0,
                increment: 50.0,
                mode: MainLayoutMode.BalancedTwoEnds);

            Assert.IsNotNull(result);
            Assert.AreEqual(6, result.Steps.Count, "Economic layout must use the minimum legal number of gaps.");
            Assert.IsTrue(result.Steps.All(x => Math.Abs(x - 900.0) < 1e-8),
                "After minimizing member count, spacing should stay as close to Max as the edge constraints allow.");
            Assert.AreEqual(300.0, result.StartOffset, 1e-8);
            Assert.AreEqual(300.0, result.EndOffset, 1e-8);
        }

        [TestMethod]
        public void HangerLayout_WideMain_PrefersNearMaxSpacingForEconomy()
        {
            var settings = new VxtSettings
            {
                DrawFurring = false,
                HangerMinSpacing = 700.0,
                HangerMaxSpacing = 1000.0,
                HangerMinEdgeOffset = 300.0,
                HangerMaxEdgeOffset = 400.0,
                HangerBalanceStep = 50.0
            };

            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000.0, 4000.0), settings);
            var firstRow = plan.HangerPoints
                .GroupBy(p => Math.Round(p.Y, 3))
                .OrderBy(g => g.Key)
                .First()
                .Select(p => p.X)
                .OrderBy(x => x)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { 300.0, 1200.0, 2100.0, 3000.0, 3900.0, 4800.0, 5700.0 },
                firstRow,
                "Ty treo must use the same economic near-Max rule along a 6000-mm main member.");
        }

        [TestMethod]
        public void OrthogonalNotch_LocalAddPreservesEconomicBaseAndAddsOnlyWhenRequired()
        {
            var enabled = new VxtSettings
            {
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = true,
                DimMain = true,
                DimHanger = true,
                UseLocalMainAdd = true
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;

            var notch = new Boundary2(new[]
            {
                new Point2(0, 1500),
                new Point2(2500, 1500),
                new Point2(2500, 0),
                new Point2(6000, 0),
                new Point2(6000, 4000),
                new Point2(0, 4000)
            });

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { notch }, disabled, new VxtLayoutContext());
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { notch }, enabled, new VxtLayoutContext());

            Assert.IsTrue(onPlan.MainSegmentCount >= offPlan.MainSegmentCount,
                "Local-notch ON may only add XC to the economic base grid.");
            foreach (var baseline in offPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
                Assert.IsTrue(onPlan.Lines.Any(x => x.Kind == PreviewLineKind.Main &&
                    ((x.A.DistanceTo(baseline.A) <= 0.1 && x.B.DistanceTo(baseline.B) <= 0.1) ||
                     (x.A.DistanceTo(baseline.B) <= 0.1 && x.B.DistanceTo(baseline.A) <= 0.1))),
                    "Economic base XC must remain unchanged when local-notch is enabled.");
            Assert.IsTrue(onPlan.HangerCount >= offPlan.HangerCount,
                "Adding a required local XC may add Ty, but must not delete Ty belonging to the base grid.");
        }

        [TestMethod]
        public void Furring_DefaultIsExact1220Div3_ButRemainsUserEditable()
        {
            var defaults = new VxtSettings();
            Assert.AreEqual(1220.0 / 3.0, defaults.FurringSpacing, 1e-10,
                "Default XP must remain 1220/3 for 1220-mm board workflow.");

            defaults.FurringSpacing = 400.0;
            Assert.AreEqual(400.0, defaults.FurringSpacing, 1e-10,
                "XP spacing must remain freely editable for other board widths/systems.");
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
