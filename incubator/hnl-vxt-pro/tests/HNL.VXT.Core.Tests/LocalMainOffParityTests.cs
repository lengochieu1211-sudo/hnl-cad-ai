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
    public sealed class LocalMainOffParityTests
    {
        [TestMethod]
        public void Legacy_LocalMainOff_ConcaveBoundaryMatchesNormalBuilderExactly()
        {
            var settings = Settings(VxtOptimizationMode.Legacy);
            var boundary = LowerLeftNotch();
            var context = new VxtLayoutContext();

            var normal = new VxtPreviewPlanBuilder().Build(boundary, settings, context);
            var throughMulti = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, context);

            AssertStructuralParity(normal, throughMulti);
        }

        [TestMethod]
        public void ProFixed_LocalMainOff_ConcaveBoundaryMatchesNormalBuilderExactly()
        {
            var settings = Settings(VxtOptimizationMode.ProEconomy);
            var boundary = LowerLeftNotch();
            var context = new VxtLayoutContext();

            var normal = new VxtProPreviewPlanBuilder().Build(boundary, settings, context);
            var throughMulti = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, context);

            AssertStructuralParity(normal, throughMulti);
        }

        private static VxtSettings Settings(VxtOptimizationMode mode)
            => new VxtSettings
            {
                OptimizationMode = mode,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = false,
                UseAvoidance = false,
                UseLocalMainAdd = false,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

        private static Boundary2 LowerLeftNotch()
            => new Boundary2(new[]
            {
                new Point2(0.0, 1500.0),
                new Point2(2500.0, 1500.0),
                new Point2(2500.0, 0.0),
                new Point2(6000.0, 0.0),
                new Point2(6000.0, 4000.0),
                new Point2(0.0, 4000.0)
            });

        private static void AssertStructuralParity(VxtPreviewPlan expected, VxtPreviewPlan actual)
        {
            CollectionAssert.AreEqual(
                MainKeys(expected),
                MainKeys(actual),
                "OFF must preserve the normal XC split/phase exactly; notch processing must not rebalance the base grid.");

            CollectionAssert.AreEqual(
                FurringKeys(expected),
                FurringKeys(actual),
                "OFF must preserve the normal XP geometry exactly.");

            CollectionAssert.AreEqual(
                HangerKeys(expected),
                HangerKeys(actual),
                "OFF must preserve the normal Ty layout exactly.");
        }

        private static string[] MainKeys(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(LineKey)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

        private static string[] FurringKeys(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Furring)
                .Select(LineKey)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

        private static string[] HangerKeys(VxtPreviewPlan plan)
            => plan.HangerPoints
                .Select(p => Math.Round(p.X, 3).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                             Math.Round(p.Y, 3).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

        private static string LineKey(PreviewLine line)
        {
            var ax = Math.Round(line.A.X, 3);
            var ay = Math.Round(line.A.Y, 3);
            var bx = Math.Round(line.B.X, 3);
            var by = Math.Round(line.B.Y, 3);

            if (ax > bx || (Math.Abs(ax - bx) <= 0.001 && ay > by))
            {
                var tx = ax; ax = bx; bx = tx;
                var ty = ay; ay = by; by = ty;
            }

            return ax.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   ay.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                   bx.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   by.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
