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
        public void ConcaveBand_LocalAddPreservesBaseXcAndAutoDimensions()
        {
            var enabled = new VxtSettings
            {
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true,
                DimMain = true,
                UseLocalMainAdd = true
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = NonOrthogonalLowerLeftNotch();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());

            foreach (var baseline in offPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
                Assert.IsTrue(onPlan.Lines.Any(x => x.Kind == PreviewLineKind.Main && SameLine(x, baseline)),
                    "Local-notch ON must preserve every normal/base XC exactly.");

            Assert.IsTrue(onPlan.MainSegmentCount >= offPlan.MainSegmentCount,
                "Local-notch ON may add required XC but must never remove a normal XC.");

            var baseYs = offPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => (x.A.Y + x.B.Y) * 0.5).Distinct().ToArray();
            foreach (var y in baseYs)
                Assert.IsTrue(onPlan.Lines.Any(x => x.Kind == PreviewLineKind.Main &&
                    Math.Abs((x.A.Y + x.B.Y) * 0.5 - y) <= 0.1),
                    "Local-notch ON must not re-phase the normal XC grid.");

            Assert.IsTrue(onPlan.Dimensions.Any(d => d.Target == DimensionTarget.Main),
                "Auto Dim must still be regenerated from the final base-plus-local XC geometry.");
        }

        [TestMethod]
        public void ConcaveBand_SameCountRepairPrecedesLocalAdd_AndRebuildsTy()
        {
            var enabled = new VxtSettings
            {
                MainDirection = MainDirectionMode.Horizontal,
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = true,
                DimMain = false,
                DimHanger = false,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = LowerLeftNotch();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());

            var offYs = MainYs(offPlan);
            var onYs = MainYs(onPlan);
            Assert.AreEqual(offYs.Length, onYs.Length,
                "This notch has a same-count repair; Local ON must not add XC.");
            Assert.IsFalse(offYs.SequenceEqual(onYs),
                "The fixture must exercise re-spacing/movement before local add.");
            Assert.IsFalse(onPlan.Diagnostics.Any(x => x.IsHard),
                "The final same-count XC grid must satisfy every HARD Max constraint.");

            foreach (var main in onPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var minX = Math.Min(main.A.X, main.B.X);
                var maxX = Math.Max(main.A.X, main.B.X);
                if (maxX - minX < 1000.0) continue;
                var y = (main.A.Y + main.B.Y) * 0.5;
                Assert.IsTrue(onPlan.HangerPoints.Any(p =>
                    Math.Abs(p.Y - y) < 0.1 &&
                    p.X >= minX - 0.1 &&
                    p.X <= maxX + 0.1),
                    "Ty must be rebuilt from every final XC after same-count notch repair.");
            }
        }

        [TestMethod]
        public void ConcaveBand_LocalFallbackRefreshesMainAndHangerDimensions()
        {
            var enabled = new VxtSettings
            {
                MainDirection = MainDirectionMode.Horizontal,
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = true,
                DimMain = true,
                DimFurring = false,
                DimHanger = true,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var boundary = LowerLeftNotch();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, new VxtLayoutContext());
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, new VxtLayoutContext());

            var local = AddedMainLines(offPlan, onPlan)
                .OrderBy(x => Math.Abs(x.B.X - x.A.X))
                .FirstOrDefault();
            Assert.IsNotNull(local, "Audit fixture must produce a local concave XC.");
            var y = (local.A.Y + local.B.Y) * 0.5;

            Assert.IsTrue(onPlan.HangerPoints.Any(p => Math.Abs(p.Y - y) < 0.1),
                "Audit fixture must produce Ty on the added local XC.");

            Assert.IsTrue(onPlan.Dimensions.Any(d => d.Target == DimensionTarget.Main &&
                (Math.Abs(d.ExtensionPoint1.Y - y) < 0.1 || Math.Abs(d.ExtensionPoint2.Y - y) < 0.1)),
                "Dim Xương chính must be rebuilt from final base-plus-local XC geometry.");

            Assert.IsTrue(onPlan.Dimensions.Any(d => d.Target == DimensionTarget.Hanger &&
                Math.Abs(d.ExtensionPoint1.Y - y) < 0.1 && Math.Abs(d.ExtensionPoint2.Y - y) < 0.1),
                "Dim Ty must include the final local Ty row.");
        }

        [TestMethod]
        public void RectangleRegion_LocalMainToggleDoesNotInjectAutomaticNotchGeometry()
        {
            var enabled = new VxtSettings
            {
                MainDirection = MainDirectionMode.RectangleRegions,
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = true,
                DimMain = true,
                DimFurring = false,
                DimHanger = true,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;

            var onContext = new VxtLayoutContext();
            onContext.Regions.Add(new VxtLayoutRegion(new Box2(0.0, 0.0, 6000.0, 4000.0), 0.0));
            var offContext = new VxtLayoutContext();
            offContext.Regions.Add(new VxtLayoutRegion(new Box2(0.0, 0.0, 6000.0, 4000.0), 0.0));
            var boundary = LowerLeftNotch();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, disabled, offContext);
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, enabled, onContext);

            CollectionAssert.AreEqual(
                offPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main).Select(LineKey).OrderBy(x => x).ToArray(),
                onPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main).Select(LineKey).OrderBy(x => x).ToArray(),
                "Manual HCN regions own their XC grids; automatic local-notch must not inject or re-phase XC.");

            CollectionAssert.AreEqual(
                offPlan.HangerPoints.Select(PointKey).OrderBy(x => x).ToArray(),
                onPlan.HangerPoints.Select(PointKey).OrderBy(x => x).ToArray(),
                "Manual HCN regions must not receive automatic extra Ty from the local-notch toggle.");

            CollectionAssert.AreEqual(
                offPlan.Dimensions.Select(DimensionKey).OrderBy(x => x).ToArray(),
                onPlan.Dimensions.Select(DimensionKey).OrderBy(x => x).ToArray(),
                "Manual HCN Dim must remain owned by the explicit regional layout.");
        }

        [TestMethod]
        public void ConcaveBand_LocalBarBelowConfiguredMinimumLengthIsSuppressed()
        {
            var settings = new VxtSettings
            {
                MainDirection = MainDirectionMode.RectangleRegions,
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

        private static double[] MainYs(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Math.Round((x.A.Y + x.B.Y) * 0.5, 3))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

        private static string PointKey(Point2 p)
            => Math.Round(p.X, 3).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," +
               Math.Round(p.Y, 3).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

        private static string LineKey(PreviewLine line)
        {
            var a = PointKey(line.A);
            var b = PointKey(line.B);
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private static string DimensionKey(PreviewDimension d)
            => ((int)d.Target).ToString() + "|" + PointKey(d.ExtensionPoint1) + "|" +
               PointKey(d.ExtensionPoint2) + "|" + PointKey(d.DimensionLinePoint);

        private static PreviewLine[] AddedMainLines(VxtPreviewPlan baseline, VxtPreviewPlan actual)
            => actual.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Where(x => !baseline.Lines.Any(b => b.Kind == PreviewLineKind.Main && SameLine(x, b)))
                .ToArray();

        private static bool SameLine(PreviewLine a, PreviewLine b)
            => (a.A.DistanceTo(b.A) <= 0.1 && a.B.DistanceTo(b.B) <= 0.1) ||
               (a.A.DistanceTo(b.B) <= 0.1 && a.B.DistanceTo(b.A) <= 0.1);

        private static Boundary2 NonOrthogonalLowerLeftNotch()
            => new Boundary2(new[]
            {
                new Point2(0, 1500),
                new Point2(2500, 1500),
                new Point2(2500, 0),
                new Point2(6000, 0),
                new Point2(6000, 3900),
                new Point2(5900, 4000),
                new Point2(0, 4000)
            });

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
