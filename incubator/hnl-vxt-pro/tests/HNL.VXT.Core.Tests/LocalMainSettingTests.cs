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
    public sealed class LocalMainSettingTests
    {
        [TestMethod]
        public void ConcaveNotch_LocalMainToggleAddsOnlyLocalGeometry()
        {
            var enabled = new VxtSettings
            {
                MainDirection = MainDirectionMode.Horizontal,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true,
                DimMain = false,
                DimFurring = false,
                DimHanger = false,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = LowerLeftNotch();

            var enabledPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());
            var disabledPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());

            var offYs = MainYs(disabledPlan);
            var onYs = MainYs(enabledPlan);
            Assert.AreEqual(offYs.Length, onYs.Length,
                "Local-main ON must keep the XC count when the notch can be repaired by the existing grid.");
            Assert.IsFalse(offYs.SequenceEqual(onYs),
                "The invalid base phase must be repaired before any local XC is considered.");
            Assert.IsFalse(enabledPlan.Diagnostics.Any(x => x.IsHard),
                "The final same-count notch repair must be HARD-Max safe.");
        }

        [TestMethod]
        public void NotchPipeline_WholeGridEqualSpacing_IsSolvedBeforeLocalXCMove()
        {
            var enabled = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                MainLayout = MainLayoutMode.OneSide,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainBalanceStep = 50.0
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = WholeShiftNotch2350();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());

            CollectionAssert.AreEqual(
                new[] { 300.0, 1150.0, 2000.0 },
                MainYs(offPlan),
                "Fixture must start from the same three-XC OneSide grid.");

            CollectionAssert.AreEqual(
                new[] { 350.0, 1200.0, 2050.0 },
                MainYs(onPlan),
                "Whole-grid repair must keep one common spacing: 350-850-850-300.");

            Assert.AreEqual(MainYs(offPlan).Length, MainYs(onPlan).Length,
                "Whole-grid equal-spacing repair must keep the XC count.");
            Assert.AreEqual(
                MainYs(onPlan)[1] - MainYs(onPlan)[0],
                MainYs(onPlan)[2] - MainYs(onPlan)[1],
                0.1,
                "Every XC spacing in the whole-grid repair must be equal.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x => x.IsHard));
        }

        [TestMethod]
        public void MizukiM01_OneSide_MovesSecondExistingMainBeforeAddingLocalXC()
        {
            var enabled = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                MainLayout = MainLayoutMode.OneSide,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainBalanceStep = 50.0
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = MizukiM01Notch2330();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());

            var offYs = MainYs(offPlan);
            var onYs = MainYs(onPlan);

            CollectionAssert.AreEqual(
                new[] { 300.0, 1150.0, 2000.0 },
                offYs,
                "OneSide base grid for the 2330-mm domain must stay deterministic: 300-850-850-330.");

            CollectionAssert.AreEqual(
                new[] { 400.0, 1200.0, 2000.0 },
                onYs,
                "Whole-grid equal-spacing repair must win first: 400-800-800-330. Only if no equal-spacing solution exists may a local XC move be tried.");

            Assert.AreEqual(offYs.Length, onYs.Length,
                "A same-count notch repair must not add XC.");
            Assert.AreEqual(
                onYs[1] - onYs[0],
                onYs[2] - onYs[1],
                0.1,
                "Whole-grid stage requires equal XC spacing.");
            Assert.IsTrue(onYs[1] - onYs[0] >= enabled.MainMinSpacing - 0.1 &&
                          onYs[1] - onYs[0] <= enabled.MainMaxSpacing + 0.1,
                "The common XC spacing must stay inside configured Min/Max.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x => x.IsHard),
                "The moved same-count M01 grid must satisfy all HARD constraints.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x =>
                x.Kind == VxtConstraintKind.ManualMainRequiredWarning),
                "The repaired M01 notch must not require manual XC completion.");
        }

        [TestMethod]
        public void MizukiM31_OneSide_BreaksSub100NotchSegmentBeforeRequiredLocalXC()
        {
            var enabled = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Vertical,
                MainLayout = MainLayoutMode.OneSide,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainBalanceStep = 50.0
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = MizukiM31Notch2050x5880();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());

            var offXs = MainXs(offPlan);
            var onXs = MainXs(onPlan);

            CollectionAssert.AreEqual(
                new[] { 350.0, 1050.0, 1750.0 },
                offXs,
                "M31 base OneSide grid must stay deterministic before notch repair.");
            CollectionAssert.AreEqual(
                new[] { 400.0, 700.0, 1100.0, 1800.0, 1900.0 },
                onXs,
                "M31 first reaches the same-count 400-1100-1800 grid. Sub-100-mm portions are then broken; x=700 repairs the lower notch band and x=1900 repairs the 250-mm upper notch band on a safe lattice point.");

            Assert.IsFalse(onPlan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x =>
                {
                    var xw = (x.A.X + x.B.X) * 0.5;
                    var minY = Math.Min(x.A.Y, x.B.Y);
                    var maxY = Math.Max(x.A.Y, x.B.Y);
                    return Math.Abs(xw - 400.0) <= 0.1 &&
                           minY < 900.0 && maxY > 900.0;
                }),
                "The unsafe x=400 XC portion must not continue through the lower notch band.");

            Assert.IsTrue(onPlan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x =>
                {
                    var xw = (x.A.X + x.B.X) * 0.5;
                    var length = x.A.DistanceTo(x.B);
                    return Math.Abs(xw - 700.0) <= 0.1 &&
                           length >= enabled.MinLocalMainLength - 0.1;
                }),
                "After the sub-100-mm segment is broken, the local x=700 XC is required and must still satisfy the HARD minimum local-XC length.");

            Assert.IsTrue(onPlan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x =>
                {
                    var xw = (x.A.X + x.B.X) * 0.5;
                    var length = x.A.DistanceTo(x.B);
                    return Math.Abs(xw - 1900.0) <= 0.1 &&
                           length >= enabled.MinLocalMainLength - 0.1;
                }),
                "The narrow upper notch band must receive a safe lattice XC at least 100 mm from both notch walls instead of reusing the unsafe x=1800 row.");

            Assert.IsFalse(onPlan.Diagnostics.Any(x => x.IsHard),
                "M31 final plan after the 100-mm constructability break must satisfy every HARD Max constraint.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x =>
                x.Kind == VxtConstraintKind.ManualMainRequiredWarning),
                "M31 can be completed automatically after the required local XC repair. Diagnostics=" +
                VxtConstraintReport.Format(onPlan.Diagnostics));
        }

        [TestMethod]
        public void LongNotch_MainCloserThan100mm_IsBrokenAndHardCoverageMovesInward()
        {
            var settings = new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                MainLayout = MainLayoutMode.OneSide,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainBalanceStep = 50.0
            };
            var boundary = LongBottomNotch4000x2400();
            var plan = new VxtPreviewPlan();

            plan.Lines.Add(new PreviewLine(new Point2(0.0, 300.0), new Point2(1500.0, 300.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(3000.0, 300.0), new Point2(4000.0, 300.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(0.0, 1000.0), new Point2(4000.0, 1000.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(0.0, 1700.0), new Point2(4000.0, 1700.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(new Point2(0.0, 2100.0), new Point2(4000.0, 2100.0), PreviewLineKind.Main));
            plan.MainSegmentCount = 5;

            var safetyType = typeof(VxtMultiBoundaryPlanBuilder).Assembly.GetType(
                "HNL.VXT.Core.Preview.VxtLocalMainSpacingSafety", throwOnError: true);
            var apply = safetyType.GetMethod(
                "Apply",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(apply, "Internal notch safety entry point must remain available.");
            apply.Invoke(null, new object[]
            {
                boundary, plan, settings, 0.0, new VxtLayoutContext()
            });

            var nearWallPieces = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Where(x => Math.Abs(((x.A.Y + x.B.Y) * 0.5) - 1000.0) <= 0.1)
                .OrderBy(x => Math.Min(x.A.X, x.B.X))
                .ToArray();

            Assert.AreEqual(2, nearWallPieces.Length,
                "The XC only 70 mm from the long notch wall must be broken into two outside pieces.");
            Assert.AreEqual(1500.0, Math.Max(nearWallPieces[0].A.X, nearWallPieces[0].B.X), 0.1);
            Assert.AreEqual(3000.0, Math.Min(nearWallPieces[1].A.X, nearWallPieces[1].B.X), 0.1);
            Assert.IsFalse(plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x =>
                {
                    var y = (x.A.Y + x.B.Y) * 0.5;
                    var minX = Math.Min(x.A.X, x.B.X);
                    var maxX = Math.Max(x.A.X, x.B.X);
                    return Math.Abs(y - 1000.0) <= 0.1 &&
                           minX < 2250.0 && maxX > 2250.0;
                }),
                "No XC may continue through the long notch band while clearance to the notch wall is below 100 mm.");

            Assert.IsTrue(plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x =>
                {
                    var y = (x.A.Y + x.B.Y) * 0.5;
                    var minX = Math.Min(x.A.X, x.B.X);
                    var maxX = Math.Max(x.A.X, x.B.X);
                    return y >= 1230.0 - 0.1 &&
                           minX <= 1500.0 + 0.1 &&
                           maxX >= 3000.0 - 0.1;
                }),
                "After breaking the unsafe XC piece, HARD Max coverage must be restored by a safe local XC farther from the notch wall.");
        }

        [TestMethod]
        public void FieldBlock14_LocalMainOff_DoesNotSplitMainsAtArtificialRegionSeams()
        {
            var settings = FieldBlock14Settings();
            settings.UseLocalMainAdd = false;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { FieldBlock14SteppedNotch() }, settings, new VxtLayoutContext());

            var mains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToArray();
            Assert.IsTrue(mains.Length > 0, "Block14 fixture must retain global XC when local-notch drawing is OFF.");

            foreach (var main in mains)
            {
                var y = (main.A.Y + main.B.Y) * 0.5;
                var minX = Math.Min(main.A.X, main.B.X);
                var maxX = Math.Max(main.A.X, main.B.X);
                double expectedMinX;
                double expectedMaxX;
                ExpectedBlock14HorizontalSpan(y, out expectedMinX, out expectedMaxX);

                Assert.AreEqual(expectedMinX, minX, 0.1,
                    "Local-main OFF must not truncate an XC at an artificial regional seam.");
                Assert.AreEqual(expectedMaxX, maxX, 0.1,
                    "Local-main OFF must keep each XC across the full real polygon span at its Y.");
            }
        }

        [TestMethod]
        public void FieldBlock14_LocalMainOn_PreservesBaseGridAndAllowsOnlyShortLocalSubMinRows()
        {
            var enabled = FieldBlock14Settings();
            enabled.UseLocalMainAdd = true;
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = FieldBlock14SteppedNotch();

            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());
            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());

            Assert.AreEqual(MainYs(offPlan).Length, MainYs(onPlan).Length,
                "Block14 has a same-count repair; do not add material before exhausting that solution.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x => x.IsHard),
                "Block14 same-count repair must satisfy every HARD Max condition.");

            var mains = onPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToArray();
            for (var i = 0; i + 1 < mains.Length; i++)
            for (var j = i + 1; j < mains.Length; j++)
            {
                var a = mains[i];
                var b = mains[j];
                var ay = (a.A.Y + a.B.Y) * 0.5;
                var by = (b.A.Y + b.B.Y) * 0.5;
                var dy = Math.Abs(by - ay);
                if (dy <= 0.5 || dy >= enabled.MainMinSpacing - 0.1) continue;

                var ax1 = Math.Min(a.A.X, a.B.X);
                var ax2 = Math.Max(a.A.X, a.B.X);
                var bx1 = Math.Min(b.A.X, b.B.X);
                var bx2 = Math.Max(b.A.X, b.B.X);
                if (Math.Min(ax2, bx2) < Math.Max(ax1, bx1) - 0.5) continue;

                var lenA = ax2 - ax1;
                var lenB = bx2 - bx1;
                Assert.IsTrue(Math.Abs(lenA - lenB) > 0.5,
                    "Sub-MinSpacing rows may occur only as a short local edge repair; two base/global rows must never be re-phased into a close pair.");
            }
        }

        private static double[] MainXs(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Math.Round((x.A.X + x.B.X) * 0.5, 1))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

        private static double[] MainYs(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Math.Round((x.A.Y + x.B.Y) * 0.5, 1))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

        private static Boundary2 LongBottomNotch4000x2400()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(1500.0, 0.0),
                new Point2(1500.0, 930.0),
                new Point2(3000.0, 930.0),
                new Point2(3000.0, 0.0),
                new Point2(4000.0, 0.0),
                new Point2(4000.0, 2400.0),
                new Point2(0.0, 2400.0)
            });

        private static Boundary2 WholeShiftNotch2350()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(3180.0, 0.0),
                new Point2(3180.0, 1600.0),
                new Point2(1150.0, 1600.0),
                new Point2(1150.0, 2350.0),
                new Point2(0.0, 2350.0)
            });

        private static Boundary2 MizukiM31Notch2050x5880()
            => new Boundary2(new[]
            {
                new Point2(404.355742292, 0.0),
                new Point2(2050.0, 0.0),
                new Point2(2050.0, 1780.0),
                new Point2(2005.0, 1780.0),
                new Point2(2005.0, 5880.0),
                new Point2(1755.0, 5880.0),
                new Point2(1755.0, 2880.0),
                new Point2(0.0, 2880.0),
                new Point2(0.0, 1780.0),
                new Point2(400.0, 1780.0),
                new Point2(400.0, 0.0)
            });

        private static Boundary2 MizukiM01Notch2330()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(3180.0, 0.0),
                new Point2(3180.0, 1600.0),
                new Point2(1150.0, 1600.0),
                new Point2(1150.0, 2330.0),
                new Point2(0.0, 2330.0)
            });

        private static bool SameMain(PreviewLine a, PreviewLine b)
            => (a.A.DistanceTo(b.A) <= 0.1 && a.B.DistanceTo(b.B) <= 0.1) ||
               (a.A.DistanceTo(b.B) <= 0.1 && a.B.DistanceTo(b.A) <= 0.1);

        private static VxtSettings FieldBlock14Settings()
            => new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };

        private static void ExpectedBlock14HorizontalSpan(double y, out double minX, out double maxX)
        {
            if (y < 80.0 - 0.1)
            {
                minX = 0.0;
                maxX = 1800.0;
                return;
            }

            if (y < 930.0 - 0.1)
            {
                minX = 0.0;
                maxX = 2800.0;
                return;
            }

            if (y < 1700.0 - 0.1)
            {
                minX = 0.0;
                maxX = 3250.0;
                return;
            }

            minX = 2200.0;
            maxX = 3250.0;
        }

        private static bool HasExpectedLocalNotchMain(VxtPreviewPlan plan)
        {
            return plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x =>
                {
                    var y = (x.A.Y + x.B.Y) * 0.5;
                    var length = Math.Abs(x.B.X - x.A.X);
                    return Math.Abs(y - 1800.0) < 0.1 && length > 2400.0 && length < 2600.0;
                });
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

        private static Boundary2 FieldBlock14SteppedNotch()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(1800.0, 0.0),
                new Point2(1800.0, 80.0),
                new Point2(2800.0, 80.0),
                new Point2(2800.0, 930.0),
                new Point2(3250.0, 930.0),
                new Point2(3250.0, 2300.0),
                new Point2(2200.0, 2300.0),
                new Point2(2200.0, 1700.0),
                new Point2(0.0, 1700.0)
            });
    }
}
