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
    public sealed class VxtProLocalAvoidanceGridPhaseTests
    {
        [TestMethod]
        public void ProEconomy_ShiftAllOff_MainAvoidancePreservesConfiguredBalanceStepPhaseAtOffsetWcs()
        {
            const double minX = 760390.265984;
            const double minY = -16323.112640;
            const double width = 10990.0;
            const double height = 4000.0;
            const double step = 50.0;

            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.ProEconomy,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = true,
                ShiftAllForAvoidance = false,
                ClearanceDistance = 0.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainBalanceStep = step
            };

            var boundary = new Boundary2(new[]
            {
                new Point2(minX, minY),
                new Point2(minX + width, minY),
                new Point2(minX + width, minY + height),
                new Point2(minX, minY + height)
            });

            var baseline = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings.Clone(), new VxtLayoutContext());
            var baselineYs = MainYs(baseline);
            Assert.IsTrue(baselineYs.Length >= 4);

            var context = new VxtLayoutContext();
            var hit = baselineYs[0];
            context.MainObstacles.Add(new Box2(minX, hit - 10.0, minX + width, hit + 10.0));

            var repaired = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, context);
            var ys = MainYs(repaired);

            Assert.AreEqual(baselineYs.Length, ys.Length,
                "Local XC MEP repair must keep the normal member count when a legal same-lattice move exists.");
            Assert.IsTrue(ys.All(y => y <= hit - 10.0 + 0.1 || y >= hit + 10.0 - 0.1),
                "ShiftAll OFF must still move the colliding XC clear of the MEP band.");

            var phaseOrigin = baselineYs[0];
            foreach (var y in ys)
            {
                var units = (y - phaseOrigin) / step;
                Assert.AreEqual(Math.Round(units), units, 1e-6,
                    "Every repaired XC must remain on the configured MainBalanceStep lattice.");
            }

            for (var i = 1; i < ys.Length; i++)
            {
                var spacingUnits = (ys[i] - ys[i - 1]) / step;
                Assert.AreEqual(Math.Round(spacingUnits), spacingUnits, 1e-6,
                    "XC spacing after local MEP avoidance must remain an exact multiple of MainBalanceStep.");
            }
        }

        [TestMethod]
        public void ProEconomy_ShiftAllOn_UsesWholeGridWhenLegal_AndDiffersFromLocalOff()
        {
            var found = false;
            foreach (var height in new[] { 4100.0, 4200.0, 4300.0, 4400.0, 4500.0, 4600.0 })
            {
                var baseSettings = new VxtSettings
                {
                    OptimizationMode = VxtOptimizationMode.ProEconomy,
                    MainDirection = MainDirectionMode.Horizontal,
                    DrawMain = true,
                    DrawFurring = false,
                    DrawHangers = false,
                    AutoDimension = false,
                    UseAvoidance = true,
                    ClearanceDistance = 0.0,
                    MainMinSpacing = 700.0,
                    MainMaxSpacing = 1000.0,
                    MainMinEdgeOffset = 300.0,
                    MainMaxEdgeOffset = 400.0,
                    MainEdgeTolerance = 25.0,
                    MainBalanceStep = 50.0
                };

                var boundary = new Boundary2(new[]
                {
                    new Point2(0.0, 0.0),
                    new Point2(6000.0, 0.0),
                    new Point2(6000.0, height),
                    new Point2(0.0, height)
                });

                var baselineSettings = baseSettings.Clone();
                baselineSettings.UseAvoidance = false;
                var baseline = VxtMultiBoundaryPlanBuilder.Build(
                    new[] { boundary }, baselineSettings, new VxtLayoutContext());
                var baselineYs = MainYs(baseline);
                if (baselineYs.Length < 3) continue;

                var hit = baselineYs[baselineYs.Length / 2];
                var context = new VxtLayoutContext();
                context.MainObstacles.Add(new Box2(0.0, hit - 10.0, 6000.0, hit + 10.0));

                var offSettings = baseSettings.Clone();
                offSettings.ShiftAllForAvoidance = false;
                var onSettings = baseSettings.Clone();
                onSettings.ShiftAllForAvoidance = true;

                var offYs = MainYs(VxtMultiBoundaryPlanBuilder.Build(
                    new[] { boundary }, offSettings, context));
                var onYs = MainYs(VxtMultiBoundaryPlanBuilder.Build(
                    new[] { boundary }, onSettings, context));

                if (offYs.Length != baselineYs.Length || onYs.Length != baselineYs.Length) continue;
                if (offYs.SequenceEqual(onYs)) continue;

                var deltas = onYs.Zip(baselineYs, (y, b) => y - b).ToArray();
                var wholeGrid = deltas.All(d => Math.Abs(d - deltas[0]) <= 1e-6) &&
                                Math.Abs(deltas[0]) >= baseSettings.MainBalanceStep - 1e-6;
                if (!wholeGrid) continue;

                Assert.IsTrue(onYs.All(y => y <= hit - 10.0 + 0.1 || y >= hit + 10.0 - 0.1),
                    "ShiftAll ON whole-grid solution must be MEP clear.");
                Assert.IsTrue(offYs.All(y => y <= hit - 10.0 + 0.1 || y >= hit + 10.0 - 0.1),
                    "ShiftAll OFF local repair must also be MEP clear.");
                found = true;
                break;
            }

            Assert.IsTrue(found,
                "At least one legal fixture must prove that ShiftAll ON performs a real whole-grid move while OFF remains local.");
        }

        private static double[] MainYs(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => (x.A.Y + x.B.Y) * 0.5)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
    }
}
