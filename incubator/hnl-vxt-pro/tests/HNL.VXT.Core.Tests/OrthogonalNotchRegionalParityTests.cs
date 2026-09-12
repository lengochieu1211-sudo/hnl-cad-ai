using System;
using System.Globalization;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalNotchRegionalParityTests
    {
        [TestMethod]
        public void OrthogonalNotch_RegionalMainRebuildRedistributesHangersAndPreservesOneSideFurringPhase()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = false,
                UseAvoidance = false,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false,
                MainSkipLimit = 0.0,
                HangerLayout = HangerLayoutMode.BalancedTwoEnds
            };
            var context = new VxtLayoutContext
            {
                GlobalFurringFromFarEdge = true
            };
            var boundary = SteppedNotch();

            // Raw builder = geometry before the concave/notch post-process.
            var raw = new VxtPreviewPlanBuilder().Build(boundary, settings, context);
            var final = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, context);

            var rawFurring = raw.Lines
                .Where(x => x.Kind == PreviewLineKind.Furring)
                .Select(LineKey)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            var finalFurring = final.Lines
                .Where(x => x.Kind == PreviewLineKind.Furring)
                .Select(LineKey)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(rawFurring, finalFurring,
                "Auto notch-region processing must not restart/re-phase XP per rectangle. XP keeps the original one-side chase direction across the whole ceiling.");

            var rawMain = raw.Lines.Where(x => x.Kind == PreviewLineKind.Main).Select(LineKey).OrderBy(x => x).ToArray();
            var finalMain = final.Lines.Where(x => x.Kind == PreviewLineKind.Main).Select(LineKey).OrderBy(x => x).ToArray();
            Assert.IsFalse(rawMain.SequenceEqual(finalMain),
                "Fixture must exercise the orthogonal notch regional XC rebuild, not the untouched raw grid.");

            foreach (var main in final.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var minX = Math.Min(main.A.X, main.B.X);
                var maxX = Math.Max(main.A.X, main.B.X);
                var y = (main.A.Y + main.B.Y) * 0.5;
                var length = maxX - minX;
                if (length <= 5.0) continue;

                var layout = SmartLayout1D.Calculate(
                    length,
                    settings.HangerMaxSpacing,
                    settings.HangerMinSpacing,
                    settings.HangerMaxEdgeOffset,
                    settings.HangerMinEdgeOffset,
                    settings.HangerBalanceStep,
                    MainLayoutMode.BalancedTwoEnds);
                Assert.IsNotNull(layout,
                    "Every final regional XC must have a valid Ty layout for its actual segment length.");

                var expected = layout.Positions(minX)
                    .Where(x => x > minX + 2.0 && x < maxX - 2.0)
                    .OrderBy(x => x)
                    .ToArray();
                var actual = final.HangerPoints
                    .Where(p => Math.Abs(p.Y - y) <= 0.1 && p.X >= minX - 0.1 && p.X <= maxX + 0.1)
                    .Select(p => p.X)
                    .OrderBy(x => x)
                    .ToArray();

                Assert.AreEqual(expected.Length, actual.Length,
                    "Ty must be recalculated after XC is split/merged by the notch regions; it must not retain the pre-process hanger count.");
                for (var i = 0; i < expected.Length; i++)
                    Assert.AreEqual(expected[i], actual[i], 0.1,
                        "Ty position must follow SmartLayout1D of the final XC segment length.");
            }
        }

        private static Boundary2 SteppedNotch()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(6000.0, 0.0),
                new Point2(6000.0, 4200.0),
                new Point2(3000.0, 4200.0),
                new Point2(3000.0, 3000.0),
                new Point2(1000.0, 3000.0),
                new Point2(1000.0, 1800.0),
                new Point2(0.0, 1800.0)
            });

        private static string LineKey(PreviewLine line)
            => PointKey(line.A) + ">" + PointKey(line.B);

        private static string PointKey(Point2 p)
            => Math.Round(p.X, 2).ToString("0.00", CultureInfo.InvariantCulture) + "," +
               Math.Round(p.Y, 2).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
