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

            foreach (var baseline in disabledPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
                Assert.IsTrue(enabledPlan.Lines.Any(x => x.Kind == PreviewLineKind.Main && SameMain(x, baseline)),
                    "Local-main ON must preserve every OFF/base XC.");

            Assert.IsTrue(enabledPlan.Lines.Count(x => x.Kind == PreviewLineKind.Main) >
                          disabledPlan.Lines.Count(x => x.Kind == PreviewLineKind.Main),
                "With local-main ON, this unresolved notch must add local XC rather than rebalance the base grid.");

            Assert.IsTrue(HasExpectedLocalNotchMain(enabledPlan),
                "Local notch XC must be placed toward the notch edge (Y=1800 in this fixture) " +
                "instead of being pinned near the neighbouring base XC.");
        }

        [TestMethod]
        public void NotchPipeline_WholeGridShift_IsTriedBeforeLocalXCMove()
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
                "A +50 mm whole-grid shift satisfies all preferred Min/Max constraints and must win before any local row move.");

            Assert.AreEqual(MainYs(offPlan).Length, MainYs(onPlan).Length,
                "Whole-grid notch repair must keep the XC count.");
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
                "Equal-gap same-count repair must win before local movement: 400-800-800-330.");

            Assert.AreEqual(offYs.Length, onYs.Length,
                "A same-count notch repair must not add XC.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x => x.IsHard),
                "The moved same-count M01 grid must satisfy all HARD constraints.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x =>
                x.Kind == VxtConstraintKind.ManualMainRequiredWarning),
                "The repaired M01 notch must not require manual XC completion.");
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

            foreach (var baseline in offPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
                Assert.IsTrue(onPlan.Lines.Any(x => x.Kind == PreviewLineKind.Main && SameMain(x, baseline)),
                    "Block14 local-main ON must preserve the complete OFF/base XC geometry.");

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

        private static double[] MainYs(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Math.Round((x.A.Y + x.B.Y) * 0.5, 1))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

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
