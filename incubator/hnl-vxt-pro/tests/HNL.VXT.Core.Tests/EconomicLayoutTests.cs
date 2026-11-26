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
        public void OneSideTy_NoUniformSolution_DrivesRemainderIntoOneTailGap()
        {
            const double length = 17090.15;

            var legacyOneSide = SmartLayout1D.Calculate(
                length,
                maxSpacing: 1000.0,
                minSpacing: 700.0,
                maxEdge: 400.0,
                minEdge: 300.0,
                increment: 50.0,
                mode: MainLayoutMode.OneSide,
                minEdgeTolerance: 25.0);

            var constructionFriendly = SmartLayout1D.Calculate(
                length,
                maxSpacing: 1000.0,
                minSpacing: 700.0,
                maxEdge: 400.0,
                minEdge: 300.0,
                increment: 50.0,
                mode: MainLayoutMode.OneSide,
                minEdgeTolerance: 25.0,
                preferOneSideTailFallback: true);

            Assert.IsNotNull(legacyOneSide);
            Assert.IsNotNull(constructionFriendly);

            // The opt-in is Ty-only at the call sites; generic/Main OneSide remains unchanged.
            Assert.AreEqual(340.15, legacyOneSide.StartOffset, 0.01);
            Assert.AreEqual(300.0, legacyOneSide.EndOffset, 0.01);
            Assert.IsTrue(legacyOneSide.Steps.Contains(950.0) && legacyOneSide.Steps.Contains(1000.0));

            Assert.AreEqual(300.0, constructionFriendly.StartOffset, 0.01);
            Assert.AreEqual(290.15, constructionFriendly.EndOffset, 0.01);
            Assert.AreEqual(17, constructionFriendly.Steps.Count);
            Assert.IsTrue(constructionFriendly.Steps.Take(16).All(x => Math.Abs(x - 1000.0) < 0.01),
                "OneSide Ty must chase Max continuously from the selected start side.");
            Assert.AreEqual(500.0, constructionFriendly.Steps[16], 0.01,
                "Only the final Ty gap may absorb the no-uniform-solution remainder.");
            Assert.IsTrue(constructionFriendly.IsDense,
                "The 500-mm final gap is a deliberate SOFT-Min fallback.");
            Assert.IsTrue(constructionFriendly.UsedSoftEdge,
                "The 290.15-mm far edge is a deliberate SOFT-Min edge fallback within tolerance.");

            var reversed = SmartLayout1D.Calculate(
                length,
                maxSpacing: 1000.0,
                minSpacing: 700.0,
                maxEdge: 400.0,
                minEdge: 300.0,
                increment: 50.0,
                mode: MainLayoutMode.OneSide,
                reverse: true,
                minEdgeTolerance: 25.0,
                preferOneSideTailFallback: true);

            Assert.IsNotNull(reversed);
            Assert.AreEqual(290.15, reversed.StartOffset, 0.01);
            Assert.AreEqual(300.0, reversed.EndOffset, 0.01);
            Assert.AreEqual(500.0, reversed.Steps[0], 0.01);
            Assert.IsTrue(reversed.Steps.Skip(1).All(x => Math.Abs(x - 1000.0) < 0.01));
        }

        [TestMethod]
        public void HangerOneSide_NoUniformSolution_UsesTailFallbackAndReportsSoftMin()
        {
            const double width = 17090.15;
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                HangerLayout = HangerLayoutMode.OneSideFollowFurring,
                HangerMinSpacing = 700.0,
                HangerMaxSpacing = 1000.0,
                HangerMinEdgeOffset = 300.0,
                HangerMaxEdgeOffset = 400.0,
                HangerBalanceStep = 50.0,
                HangerEdgeTolerance = 25.0,
                UseAvoidance = false
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { Rectangle(width, 4000.0) },
                settings,
                new VxtLayoutContext());

            var firstMain = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .OrderBy(x => Math.Min(x.A.Y, x.B.Y))
                .First();
            var xs = plan.HangerPoints
                .Where(p => Math.Abs(p.Y - firstMain.A.Y) < 0.01)
                .Select(p => p.X)
                .OrderBy(x => x)
                .ToArray();

            Assert.AreEqual(18, xs.Length);
            Assert.AreEqual(300.0, xs[0], 0.01);
            for (var i = 1; i <= 16; i++)
                Assert.AreEqual(1000.0, xs[i] - xs[i - 1], 0.01);
            Assert.AreEqual(500.0, xs[17] - xs[16], 0.01);
            Assert.AreEqual(290.15, width - xs[17], 0.01);

            Assert.IsTrue(plan.Diagnostics.Any(x =>
                    x.Target == VxtConstraintTarget.Hanger &&
                    x.Kind == VxtConstraintKind.MinSpacingSoft &&
                    !x.IsHard),
                "Final 500-mm Ty gap must be surfaced as a SOFT Min warning.");
            Assert.IsTrue(plan.Diagnostics.Any(x =>
                    x.Target == VxtConstraintTarget.Hanger &&
                    x.Kind == VxtConstraintKind.MinEdgeSoft &&
                    !x.IsHard),
                "Final 290.15-mm Ty edge must be surfaced as a SOFT Min warning.");
            Assert.IsFalse(plan.Diagnostics.Any(x => x.IsHard),
                "The construction-friendly fallback must never violate HARD Max or step rules.");
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

            var offYs = MainYs(offPlan);
            var onYs = MainYs(onPlan);
            Assert.AreEqual(offYs.Length, onYs.Length,
                "Economy contract: repair the notch with the same XC count whenever a valid same-count solution exists.");
            Assert.IsFalse(offYs.SequenceEqual(onYs),
                "This fixture must exercise the same-count notch repair rather than preserving an invalid base phase.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x => x.IsHard),
                "Economic same-count repair must remove the notch HARD Max violation.");

            foreach (var main in onPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var y = (main.A.Y + main.B.Y) * 0.5;
                var minX = Math.Min(main.A.X, main.B.X);
                var maxX = Math.Max(main.A.X, main.B.X);
                if (maxX - minX < 1000.0) continue;
                Assert.IsTrue(onPlan.HangerPoints.Any(p =>
                    Math.Abs(p.Y - y) <= 0.1 &&
                    p.X >= minX - 0.1 &&
                    p.X <= maxX + 0.1),
                    "Ty must follow the final economic XC grid after notch repair.");
            }
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

        private static double[] MainYs(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Math.Round((x.A.Y + x.B.Y) * 0.5, 3))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

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
