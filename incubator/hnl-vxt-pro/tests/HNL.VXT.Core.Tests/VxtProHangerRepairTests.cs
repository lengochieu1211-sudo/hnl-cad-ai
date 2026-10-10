using System;
using System.Linq;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtProHangerRepairTests
    {
        [TestMethod]
        public void ValidLegacyRow_IsPreservedExactly()
        {
            var ideal = new[] { 300.0, 1200.0, 2100.0, 3000.0, 3900.0, 4800.0, 5700.0 };
            var result = VxtProHangerRepair.Repair(
                ideal,
                null,
                0.0, 6000.0,
                700.0, 1000.0,
                300.0, 400.0,
                50.0,
                VxtOptimizationMode.ProEconomy,
                edgeTolerance: 0.0);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Repaired);
            CollectionAssert.AreEqual(ideal, result.Positions.ToArray());
        }

        [TestMethod]
        public void BlockedLegacyTy_IsRepairedToClearLegalChain()
        {
            var ideal = new[] { 300.0, 1200.0, 2100.0, 3000.0, 3900.0, 4800.0, 5700.0 };
            var obstacles = new[]
            {
                Tuple.Create(1100.0, 1450.0),
                Tuple.Create(2850.0, 3150.0)
            };

            var result = VxtProHangerRepair.Repair(
                ideal,
                obstacles,
                0.0, 6000.0,
                700.0, 1000.0,
                300.0, 400.0,
                50.0,
                VxtOptimizationMode.ProBalanced,
                edgeTolerance: 0.0);

            Assert.IsTrue(result.Success, "Repair must find a legal Ty chain when clear lattice positions exist.");
            Assert.IsTrue(result.Repaired);
            Assert.IsTrue(VxtProHangerRepair.IsValidChain(
                result.Positions, obstacles,
                0.0, 6000.0,
                700.0, 1000.0,
                300.0, 400.0));
            Assert.IsTrue(result.Positions.All(x => !(x > 1100.0 && x < 1450.0)));
            Assert.IsTrue(result.Positions.All(x => !(x > 2850.0 && x < 3150.0)));
            Assert.IsTrue(result.MaxGap <= 1000.0 + 0.1);
        }

        [TestMethod]
        public void ImpossibleObstacleBand_FailsClosedWithNoTyRow()
        {
            var ideal = new[] { 300.0, 1200.0, 2100.0, 3000.0, 3900.0, 4800.0, 5700.0 };
            var obstacles = new[] { Tuple.Create(500.0, 5500.0) };

            var result = VxtProHangerRepair.Repair(
                ideal,
                obstacles,
                0.0, 6000.0,
                700.0, 1000.0,
                300.0, 400.0,
                50.0,
                VxtOptimizationMode.ProEconomy,
                edgeTolerance: 0.0);

            Assert.IsFalse(result.Success,
                "When no legal Ty chain can bridge the obstacle without exceeding Max spacing, repair must fail closed.");
            Assert.AreEqual(0, result.Positions.Count);
        }
    }
}
