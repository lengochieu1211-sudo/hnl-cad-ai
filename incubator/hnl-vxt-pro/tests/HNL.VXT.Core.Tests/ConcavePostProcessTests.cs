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
        public void ConcaveBand_LocalFallbackAddsOnlyRequiredShortXcAndGetsTy()
        {
            var enabled = new VxtSettings
            {
                MainDirection = MainDirectionMode.RectangleRegions,
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

            var added = AddedMainLines(offPlan, onPlan);
            Assert.IsTrue(added.Length > 0,
                "A real unresolved notch must add at least one local XC when the option is ON.");

            foreach (var baseline in offPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
                Assert.IsTrue(onPlan.Lines.Any(x => x.Kind == PreviewLineKind.Main && SameLine(x, baseline)),
                    "Adding a notch XC must not move or delete the normal XC grid.");

            var local = added.OrderBy(x => Math.Abs(x.B.X - x.A.X)).First();
            var localLength = Math.Abs(local.B.X - local.A.X);
            var localY = (local.A.Y + local.B.Y) * 0.5;
            Assert.IsTrue(localLength >= enabled.MinLocalMainLength - 0.1 &&
                          localLength < 3000.0,
                "Notch fallback must add a short local XC, not replace the global grid.");

            Assert.IsTrue(onPlan.HangerPoints.Any(p =>
                Math.Abs(p.Y - localY) < 0.1 &&
                p.X >= Math.Min(local.A.X, local.B.X) - 0.1 &&
                p.X <= Math.Max(local.A.X, local.B.X) + 0.1),
                "Every added local XC must receive Ty using the same strict Ty solver.");
        }

        [TestMethod]
        public void ConcaveBand_LocalFallbackRefreshesMainAndHangerDimensions()
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
        public void RectangleRegion_ConcaveLocalFallbackRefreshesMainAndHangerDimensions()
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

            var local = AddedMainLines(offPlan, onPlan)
                .OrderBy(x => Math.Abs(x.B.X - x.A.X))
                .FirstOrDefault();
            Assert.IsNotNull(local, "Rectangle-region fixture must add a local concave XC without replacing the base grid.");
            var y = (local.A.Y + local.B.Y) * 0.5;

            Assert.IsTrue(onPlan.HangerPoints.Any(p => Math.Abs(p.Y - y) < 0.1),
                "Rectangle-region local XC must receive Ty.");
            Assert.IsTrue(onPlan.Dimensions.Any(d => d.Target == DimensionTarget.Main &&
                (Math.Abs(d.ExtensionPoint1.Y - y) < 0.1 || Math.Abs(d.ExtensionPoint2.Y - y) < 0.1)),
                "Rectangle-region Dim Xương chính must include the added local XC.");
            Assert.IsTrue(onPlan.Dimensions.Any(d => d.Target == DimensionTarget.Hanger &&
                Math.Abs(d.ExtensionPoint1.Y - y) < 0.1 && Math.Abs(d.ExtensionPoint2.Y - y) < 0.1),
                "Rectangle-region Dim Ty must include the local Ty row.");
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
