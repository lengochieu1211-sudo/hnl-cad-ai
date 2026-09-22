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
        public void OrthogonalNotch_FinalMainStrategyRedistributesHangersAndPreservesOneSideFurringPhase()
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

            var raw = new VxtPreviewPlanBuilder().Build(boundary, settings, context);
            var final = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, context);

            var rawFurring = raw.Lines.Where(x => x.Kind == PreviewLineKind.Furring)
                .Select(LineKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var finalFurring = final.Lines.Where(x => x.Kind == PreviewLineKind.Furring)
                .Select(LineKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            CollectionAssert.AreEqual(rawFurring, finalFurring,
                "Auto notch processing must not restart/re-phase XP per rectangle. XP keeps the original one-side chase direction across the whole ceiling.");

            Assert.IsTrue(final.MainSegmentCount > 0,
                "The final continuity-first notch strategy must retain valid XC geometry.");
            AssertHardMaxCoverage(final, boundary, settings);

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
                    "Every final XC must have a valid Ty layout for its actual segment length.");

                var expected = layout.Positions(minX)
                    .Where(x => x > minX + 2.0 && x < maxX - 2.0)
                    .OrderBy(x => x).ToArray();
                var actual = final.HangerPoints
                    .Where(p => Math.Abs(p.Y - y) <= 0.1 && p.X >= minX - 0.1 && p.X <= maxX + 0.1)
                    .Select(p => p.X).OrderBy(x => x).ToArray();

                Assert.AreEqual(expected.Length, actual.Length,
                    "Ty must be recalculated from the final XC segment length after any rebalance/extend/local decision.");
                for (var i = 0; i < expected.Length; i++)
                    Assert.AreEqual(expected[i], actual[i], 0.1,
                        "Ty position must follow SmartLayout1D of the final XC segment length.");
            }
        }

        private static void AssertHardMaxCoverage(VxtPreviewPlan plan, Boundary2 boundary, VxtSettings settings)
        {
            var maxEdge = settings.MainMaxEdgeOffset;
            var xs = boundary.Vertices.Select(p => p.X).Distinct().OrderBy(x => x).ToArray();
            for (var i = 0; i + 1 < xs.Length; i++)
            {
                if (xs[i + 1] - xs[i] <= 2.0) continue;
                var sampleX = (xs[i] + xs[i + 1]) * 0.5;
                foreach (var interval in TestPolygonScanline.ClipVertical(boundary.Vertices, sampleX))
                {
                    var minY = Math.Min(interval.A.Y, interval.B.Y);
                    var maxY = Math.Max(interval.A.Y, interval.B.Y);
                    var ys = plan.Lines
                        .Where(line => line.Kind == PreviewLineKind.Main &&
                                       sampleX >= Math.Min(line.A.X, line.B.X) - 0.1 &&
                                       sampleX <= Math.Max(line.A.X, line.B.X) + 0.1)
                        .Select(line => (line.A.Y + line.B.Y) * 0.5)
                        .Where(y => y >= minY - 0.1 && y <= maxY + 0.1)
                        .Distinct().OrderBy(y => y).ToArray();

                    Assert.IsTrue(ys.Length > 0, "Every real notch interval must be covered by at least one XC.");
                    Assert.IsTrue(ys[0] - minY <= maxEdge + 0.5,
                        "First XC exceeds configured MaxEdge at notch sample X=" + sampleX.ToString("0.###", CultureInfo.InvariantCulture));
                    Assert.IsTrue(maxY - ys[ys.Length - 1] <= maxEdge + 0.5,
                        "Last XC exceeds configured MaxEdge at notch sample X=" + sampleX.ToString("0.###", CultureInfo.InvariantCulture));
                    for (var j = 0; j + 1 < ys.Length; j++)
                        Assert.IsTrue(ys[j + 1] - ys[j] <= settings.MainMaxSpacing + 0.5,
                            "XC gap exceeds configured MainMaxSpacing at notch sample X=" + sampleX.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }
        }

        private static Boundary2 SteppedNotch()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0), new Point2(6000.0, 0.0),
                new Point2(6000.0, 4200.0), new Point2(3000.0, 4200.0),
                new Point2(3000.0, 3000.0), new Point2(1000.0, 3000.0),
                new Point2(1000.0, 1800.0), new Point2(0.0, 1800.0)
            });

        private static string LineKey(PreviewLine line)
            => PointKey(line.A) + ">" + PointKey(line.B);

        private static string PointKey(Point2 p)
            => Math.Round(p.X, 2).ToString("0.00", CultureInfo.InvariantCulture) + "," +
               Math.Round(p.Y, 2).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
