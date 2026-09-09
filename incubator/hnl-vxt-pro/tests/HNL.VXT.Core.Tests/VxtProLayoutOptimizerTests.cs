using System;
using System.Linq;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProLayoutOptimizerTests
    {
        [TestMethod]
        public void LegacyMode_IsBitForBitEquivalentToCertifiedSmartLayout()
        {
            var legacy = SmartLayout1D.Calculate(
                6000.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds);

            var proEntry = VxtProLayoutOptimizer.Calculate(
                6000.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds,
                VxtOptimizationMode.Legacy);

            Assert.IsNotNull(legacy);
            Assert.IsNotNull(proEntry);
            Assert.IsNotNull(proEntry.Layout);
            Assert.AreEqual(legacy.StartOffset, proEntry.Layout.StartOffset, 1e-10);
            Assert.AreEqual(legacy.EndOffset, proEntry.Layout.EndOffset, 1e-10);
            CollectionAssert.AreEqual(legacy.Steps.ToArray(), proEntry.Layout.Steps.ToArray());
            Assert.IsTrue(proEntry.Quality.IsValid);
        }

        [TestMethod]
        public void ProEconomy_UsesAvailableEdgeSlackToClearObstacle_WithoutAddingMembers()
        {
            // L=6100 gives 350/350 legacy edges with six 900-mm gaps.
            // The obstacle covers the legacy first XC, while 300/400 or 400/300
            // edge alternatives remain legal and clear.
            var result = VxtProLayoutOptimizer.Calculate(
                6100.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds,
                VxtOptimizationMode.ProEconomy,
                new[] { Tuple.Create(325.0, 375.0) },
                edgeTolerance: 0.0);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Layout);
            Assert.IsNotNull(result.Quality);
            Assert.IsTrue(result.Quality.IsValid);
            Assert.AreEqual(0, result.Quality.CollisionCount,
                "Pro Economy should use legal edge slack before accepting a collision.");
            Assert.AreEqual(7, result.Layout.PointCount,
                "Obstacle avoidance must not add XC when a same-count legal offset exists.");
            Assert.IsTrue(
                Math.Abs(result.Layout.StartOffset - 300.0) < 1e-8 ||
                Math.Abs(result.Layout.StartOffset - 400.0) < 1e-8);
        }

        [TestMethod]
        public void ProConservative_CanChooseDenserLegalGrid_AndKeepsAllConstraints()
        {
            var economy = VxtProLayoutOptimizer.Calculate(
                6000.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds,
                VxtOptimizationMode.ProEconomy,
                edgeTolerance: 0.0);

            var conservative = VxtProLayoutOptimizer.Calculate(
                6000.0, 1000.0, 700.0, 400.0, 300.0, 50.0,
                MainLayoutMode.BalancedTwoEnds,
                VxtOptimizationMode.ProConservative,
                edgeTolerance: 0.0);

            Assert.IsNotNull(economy?.Layout);
            Assert.IsNotNull(conservative?.Layout);
            Assert.IsTrue(conservative.Quality.IsValid);
            Assert.IsTrue(conservative.Quality.MaxGap <= economy.Quality.MaxGap + 1e-8);
            Assert.IsTrue(conservative.Layout.Steps.All(g => g >= 700.0 - 0.1 && g <= 1000.0 + 0.1));
            Assert.IsTrue(conservative.Layout.StartOffset >= 300.0 - 0.1 && conservative.Layout.StartOffset <= 400.0 + 0.1);
            Assert.IsTrue(conservative.Layout.EndOffset >= 300.0 - 0.1 && conservative.Layout.EndOffset <= 400.0 + 0.1);
        }

        [TestMethod]
        public void QualityReport_FailsClosed_WhenHardConstraintIsBroken()
        {
            var invalid = new SmartLayout1D.Result(
                100.0,
                new[] { 1200.0, 1200.0 },
                100.0);

            var quality = VxtProLayoutOptimizer.Evaluate(
                invalid,
                2600.0,
                700.0,
                1000.0,
                300.0,
                400.0,
                null,
                VxtOptimizationMode.ProBalanced,
                edgeTolerance: 0.0);

            Assert.IsNotNull(quality);
            Assert.IsFalse(quality.IsValid);
            Assert.IsTrue(quality.HardViolationCount >= 4);
            Assert.AreEqual(0, quality.QualityScore100);
        }
    }
}
