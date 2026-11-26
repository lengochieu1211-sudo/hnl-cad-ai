using System;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProFurringOffsetOptimizerTests
    {
        private const double Spacing = 1220.0 / 3.0;

        [TestMethod]
        public void ProEconomy_KeepsLegacyMemberCount_AndExactSpacing()
        {
            var result = VxtProFurringOffsetOptimizer.Calculate(
                0.0, 6000.0,
                Spacing,
                Spacing,
                null,
                VxtOptimizationMode.ProEconomy,
                sampleStep: 10.0);

            Assert.IsNotNull(result);
            Assert.AreEqual(14, result.Positions.Count,
                "Economy should not add XP compared with the legacy near-edge grid on 6000 mm.");
            for (var i = 1; i < result.Positions.Count; i++)
                Assert.AreEqual(Spacing, result.Positions[i] - result.Positions[i - 1], 1e-8,
                    "XP spacing must remain exactly 1220/3; only offset may change.");
        }

        [TestMethod]
        public void ProBalanced_CentersTwoEdges_WithoutIncreasingLegacyCount()
        {
            var result = VxtProFurringOffsetOptimizer.Calculate(
                0.0, 6000.0,
                Spacing,
                Spacing,
                null,
                VxtOptimizationMode.ProBalanced,
                sampleStep: 10.0);

            Assert.IsNotNull(result);
            Assert.AreEqual(14, result.Positions.Count);
            Assert.AreEqual(result.StartEdge, result.EndEdge, 1e-6,
                "Balanced XP should use the same member count and split the remaining edge space evenly.");
        }

        [TestMethod]
        public void ProEconomy_ShiftsWholeXpGridToAvoidObstacle_BeforeAddingMember()
        {
            var obstacles = new[] { Tuple.Create(400.0, 420.0) };
            var result = VxtProFurringOffsetOptimizer.Calculate(
                0.0, 6000.0,
                Spacing,
                Spacing,
                obstacles,
                VxtOptimizationMode.ProEconomy,
                sampleStep: 10.0);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsClear);
            Assert.AreEqual(14, result.Positions.Count);
        }

        [TestMethod]
        public void ProConservative_MayAddOneXp_WhenItGreatlyReducesEdgeDistance()
        {
            var economy = VxtProFurringOffsetOptimizer.Calculate(
                0.0, 6000.0,
                Spacing,
                Spacing,
                null,
                VxtOptimizationMode.ProEconomy,
                sampleStep: 10.0);
            var conservative = VxtProFurringOffsetOptimizer.Calculate(
                0.0, 6000.0,
                Spacing,
                Spacing,
                null,
                VxtOptimizationMode.ProConservative,
                sampleStep: 10.0);

            Assert.IsNotNull(economy);
            Assert.IsNotNull(conservative);
            Assert.IsTrue(conservative.MaxEdge <= economy.MaxEdge + 1e-6);
            Assert.IsTrue(conservative.Positions.Count >= economy.Positions.Count);
        }
    }
}
