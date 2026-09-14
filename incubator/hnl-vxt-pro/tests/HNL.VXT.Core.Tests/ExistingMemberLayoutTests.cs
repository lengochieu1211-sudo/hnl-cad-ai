using System;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class ExistingMemberLayoutTests
    {
        [TestMethod]
        public void FromBounds_WiderThanTall_IsHorizontalLikeLisp()
        {
            var axis = ExistingMemberLayout.FromBounds(new Box2(100, 200, 4100, 250));
            Assert.IsTrue(axis.IsHorizontal);
            Assert.AreEqual(100.0, axis.Start.X, 1e-9);
            Assert.AreEqual(4100.0, axis.End.X, 1e-9);
            Assert.AreEqual(225.0, axis.FixedCoordinate, 1e-9);
        }

        [TestMethod]
        public void FromBounds_EqualExtents_IsVerticalBecauseLispUsesStrictGreaterThan()
        {
            var axis = ExistingMemberLayout.FromBounds(new Box2(10, 20, 110, 120));
            Assert.IsFalse(axis.IsHorizontal);
            Assert.AreEqual(60.0, axis.FixedCoordinate, 1e-9);
        }

        [TestMethod]
        public void ManualHangers_UseSameV67615Strict4000Layout()
        {
            var settings = new VxtSettings
            {
                HangerLayout = HangerLayoutMode.BalancedTwoEnds,
                UseAvoidance = false
            };
            var axis = ExistingMemberLayout.FromBounds(new Box2(0, -5, 4000, 5));
            var xs = ExistingMemberLayout.HangerPoints(axis, settings, false).Select(p => p.X).ToArray();
            CollectionAssert.AreEqual(new[] { 300.0, 1150.0, 2000.0, 2850.0, 3700.0 }, xs);
        }

        [TestMethod]
        public void OneSideReverse_MirrorsManualHangerPositions()
        {
            var settings = new VxtSettings
            {
                HangerLayout = HangerLayoutMode.OneSideFollowFurring,
                UseAvoidance = false
            };
            var axis = ExistingMemberLayout.FromBounds(new Box2(0, -5, 3650, 5));
            var normal = ExistingMemberLayout.HangerPoints(axis, settings, false).Select(p => p.X).ToArray();
            var reverse = ExistingMemberLayout.HangerPoints(axis, settings, true).Select(p => p.X).ToArray();

            Assert.AreEqual(normal.Length, reverse.Length);
            for (var i = 0; i < normal.Length; i++)
                Assert.AreEqual(3650.0 - normal[normal.Length - 1 - i], reverse[i], 1e-8);
        }
    }
}
