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
