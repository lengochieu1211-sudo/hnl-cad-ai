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
    public sealed class ConcavePostProcessTests
    {
        [TestMethod]
        public void ConcaveBand_RebalancesGlobalXcBeforeAddingLocalBar()
        {
            var settings = new VxtSettings
            {
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseLocalMainAdd = true
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, settings, new VxtLayoutContext());

            var ys = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Math.Round((x.A.Y + x.B.Y) * 0.5, 3))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            Assert.IsTrue(ys.Any(y => Math.Abs(y - 1900.0) < 0.1),
                "V6.7.6.15 must first move the 2000 XC to the strict-multiple 1900 position for the 1500..4000 concave interval.");
            Assert.IsFalse(ys.Any(y => Math.Abs(y - 2000.0) < 0.1));

            var moved = plan.Lines.First(x => x.Kind == PreviewLineKind.Main &&
                Math.Abs((x.A.Y + x.B.Y) * 0.5 - 1900.0) < 0.1);
            Assert.IsTrue(Math.Abs(moved.A.X - moved.B.X) > 5900.0,
                "Rebalance must remain a global XC, not silently turn into a short local bar.");
        }

        [TestMethod]
        public void ConcaveBand_LocalFallbackRepairsMaxEdgeAndGetsTy()
        {
            var settings = new VxtSettings
            {
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = true, // preserves the already-built DIM grid; forces local fallback instead of moving it.
                DimMain = false,
                DimHanger = false,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, settings, new VxtLayoutContext());

            var local = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .FirstOrDefault(x =>
                {
                    var y = (x.A.Y + x.B.Y) * 0.5;
                    var len = Math.Abs(x.B.X - x.A.X);
                    return Math.Abs(y - 1800.0) < 0.1 && len > 2400.0 && len < 2600.0;
                });

            Assert.IsTrue(Math.Abs(local.B.X - local.A.X) > 2400.0,
                "Unresolved concave band must receive the minimum short local XC clipped to the 0..2500 notch band.");
            Assert.IsTrue(plan.HangerPoints.Any(p => Math.Abs(p.Y - 1800.0) < 0.1 && p.X >= -0.1 && p.X <= 2500.1),
                "Local XC must receive Ty using the same strict Ty solver.");

            var crossSectionYs = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main &&
                            Math.Min(x.A.X, x.B.X) <= 1250.0 + 0.1 &&
                            Math.Max(x.A.X, x.B.X) >= 1250.0 - 0.1)
                .Select(x => (x.A.Y + x.B.Y) * 0.5)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            Assert.IsTrue(crossSectionYs.Length > 0);
            Assert.IsTrue(crossSectionYs.First() - 1500.0 <= 425.1);
            Assert.IsTrue(4000.0 - crossSectionYs.Last() <= 425.1);
            for (var i = 1; i < crossSectionYs.Length; i++)
                Assert.IsTrue(crossSectionYs[i] - crossSectionYs[i - 1] <= 1000.1,
                    "Every concave cross-section must remain Max-spacing safe after local repair.");
        }

        [TestMethod]
        public void ConcaveBand_LocalBarBelowConfiguredMinimumLengthIsSuppressed()
        {
            var settings = new VxtSettings
            {
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true,
                UseLocalMainAdd = true,
                MinLocalMainLength = 3000.0
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, settings, new VxtLayoutContext());

            Assert.IsFalse(plan.Lines.Any(x => x.Kind == PreviewLineKind.Main &&
                Math.Abs((x.A.Y + x.B.Y) * 0.5 - 1800.0) < 0.1 &&
                Math.Abs(x.B.X - x.A.X) < 2600.0));
        }

        [TestMethod]
        public void RectangleCeiling_DoesNotReceiveAnyPostProcessExtraXc()
        {
            var settings = new VxtSettings { DrawFurring = false, DrawHangers = false };
            var rectangle = new Boundary2(new[]
            {
                new Point2(0, 0), new Point2(6000, 0), new Point2(6000, 4000), new Point2(0, 4000)
            });

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { rectangle }, settings, new VxtLayoutContext());
            var ys = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Math.Round((x.A.Y + x.B.Y) * 0.5, 3)).Distinct().OrderBy(x => x).ToArray();

            CollectionAssert.AreEqual(new[] { 300.0, 1150.0, 2000.0, 2850.0, 3700.0 }, ys);
        }

        private static Boundary2 LowerLeftNotch()
            => new Boundary2(new[]
            {
                new Point2(0, 1500),
                new Point2(2500, 1500),
                new Point2(2500, 0),
                new Point2(6000, 0),
                new Point2(6000, 4000),
                new Point2(0, 4000)
            });
    }
}
