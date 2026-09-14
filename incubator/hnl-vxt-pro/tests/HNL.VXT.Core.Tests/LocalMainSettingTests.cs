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
        public void ConcaveNotch_LocalMainToggleControlsFallbackWithoutChangingFixture()
        {
            var enabled = new VxtSettings
            {
                MainDirection = MainDirectionMode.RectangleRegions,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true, // isolated region family intentionally uses local fallback
                DimMain = false,
                DimFurring = false,
                DimHanger = false,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };

            var enabledPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, enabled, new VxtLayoutContext());

            Assert.IsTrue(HasExpectedLocalNotchMain(enabledPlan),
                "With 'Thêm XC cục bộ cạnh khuyết' ON, the unresolved notch must receive the short local XC.");

            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var disabledPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, disabled, new VxtLayoutContext());

            Assert.IsFalse(HasExpectedLocalNotchMain(disabledPlan),
                "With 'Thêm XC cục bộ cạnh khuyết' OFF, the local-notch fallback must not add a short XC.");
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
        public void FieldBlock14_LocalMainOn_RejectsSubMinSpacingAcrossRegionalSeams()
        {
            var settings = FieldBlock14Settings();
            settings.UseLocalMainAdd = true;

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { FieldBlock14SteppedNotch() }, settings, new VxtLayoutContext());

            var mains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToArray();
            Assert.IsTrue(mains.Length > 0);

            for (var i = 0; i + 1 < mains.Length; i++)
            {
                var a = mains[i];
                var ay = (a.A.Y + a.B.Y) * 0.5;
                var ax1 = Math.Min(a.A.X, a.B.X);
                var ax2 = Math.Max(a.A.X, a.B.X);

                for (var j = i + 1; j < mains.Length; j++)
                {
                    var b = mains[j];
                    var by = (b.A.Y + b.B.Y) * 0.5;
                    var dy = Math.Abs(by - ay);
                    if (dy <= 0.5) continue;

                    var bx1 = Math.Min(b.A.X, b.B.X);
                    var bx2 = Math.Max(b.A.X, b.B.X);
                    var overlapOrTouch = Math.Min(ax2, bx2) >= Math.Max(ax1, bx1) - 0.5;
                    if (!overlapOrTouch) continue;

                    Assert.IsTrue(dy >= settings.MainMinSpacing - 0.1,
                        "Block14 must never create staggered/local XC rows closer than MainMinSpacing where their spans overlap or touch. Actual=" +
                        dy.ToString("0.###"));
                }
            }
        }

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
