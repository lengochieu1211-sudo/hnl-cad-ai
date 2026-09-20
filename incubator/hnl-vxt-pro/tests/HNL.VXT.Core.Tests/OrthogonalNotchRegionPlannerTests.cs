using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalNotchRegionPlannerTests
    {
        [TestMethod]
        public void LowerLeftNotch_LocalAddPreservesBaseGrid_AndKeepsFurringUnchanged()
        {
            var enabled = Settings(autoDimension: true);
            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var context = new VxtLayoutContext { GlobalFurringFromFarEdge = false };
            var boundary = LowerLeftNotch();

            var offPlan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, disabled, context);
            var onPlan = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, enabled, context);

            CollectionAssert.AreEqual(
                FurringKeys(offPlan).ToArray(),
                FurringKeys(onPlan).ToArray(),
                "Local-notch processing must never change XP positions or the single global chase direction.");

            foreach (var baseline in offPlan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
                Assert.IsTrue(onPlan.Lines.Any(x => x.Kind == PreviewLineKind.Main &&
                    ((x.A.DistanceTo(baseline.A) <= 0.1 && x.B.DistanceTo(baseline.B) <= 0.1) ||
                     (x.A.DistanceTo(baseline.B) <= 0.1 && x.B.DistanceTo(baseline.A) <= 0.1))),
                    "Local-notch ON must preserve every base XC exactly.");

            Assert.IsTrue(onPlan.MainSegmentCount >= offPlan.MainSegmentCount);
            Assert.IsTrue(onPlan.HangerCount >= offPlan.HangerCount);
        }

        [TestMethod]
        public void LowerLeftNotch_HangersAreRebuiltFromEachFinalMainSegment()
        {
            var settings = Settings(autoDimension: false);
            settings.DrawFurring = false;
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, settings, new VxtLayoutContext());

            foreach (var main in plan.Lines.Where(x => x.Kind == PreviewLineKind.Main))
            {
                var minX = Math.Min(main.A.X, main.B.X);
                var maxX = Math.Max(main.A.X, main.B.X);
                var length = maxX - minX;
                if (length < 1000.0) continue;
                var y = (main.A.Y + main.B.Y) * 0.5;
                var row = plan.HangerPoints
                    .Where(p => Math.Abs(p.Y - y) <= 0.1 && p.X >= minX - 0.1 && p.X <= maxX + 0.1)
                    .OrderBy(p => p.X)
                    .ToArray();
                Assert.IsTrue(row.Length > 0, "Every final XC segment must receive its own Ty row.");
                Assert.IsTrue(row[0].X - minX <= 400.1, "Ty first edge exceeds configured HARD max edge.");
                Assert.IsTrue(maxX - row[row.Length - 1].X <= 400.1, "Ty last edge exceeds configured HARD max edge.");
                for (var i = 0; i + 1 < row.Length; i++)
                    Assert.IsTrue(row[i + 1].X - row[i].X <= 1000.1,
                        "Ty on a rebuilt XC segment exceeds configured max spacing.");
            }
        }

        [TestMethod]
        public void LowerLeftNotch_AutoDimensionDoesNotChangeFinalMainGeometry()
        {
            var noDim = Settings(autoDimension: false);
            var withDim = Settings(autoDimension: true);
            var boundary = LowerLeftNotch();
            var a = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, noDim, new VxtLayoutContext());
            var b = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, withDim, new VxtLayoutContext());
            CollectionAssert.AreEqual(MainKeys(a).ToArray(), MainKeys(b).ToArray(),
                "Auto DIM must follow final XC geometry; it must never freeze or alter the notch optimizer.");
        }

        [TestMethod]
        public void FieldDxf_Shallow110Step_ExtendsTwoSharedMainRowsInsteadOfSplittingRegionPhase()
        {
            // Fixture simplified directly from the user's new block10.dxf field case:
            // total ceiling 2675 x 1800; the right 800 mm band starts 110 mm higher.
            // The normal base layout is two continuous XC rows at Y=400/1400.
            // Enabling local-notch repair must not re-phase/translate that base grid when MEP
            // avoidance is OFF; the notch feature may only add a truly required local XC.
            var settings = Settings(autoDimension: false);
            settings.DrawFurring = false;
            settings.DrawHangers = false;
            var boundary = Shallow110StepFromFieldDxf();

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());
            var mains = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .OrderBy(x => (x.A.Y + x.B.Y) * 0.5)
                .ToArray();

            Assert.AreEqual(2, mains.Length,
                "Shallow 110 mm step must keep two continuous XC rows; do not split the ceiling into two independent XC phases.");

            var expectedY = new[] { 400.0, 1400.0 };
            for (var i = 0; i < mains.Length; i++)
            {
                var minX = Math.Min(mains[i].A.X, mains[i].B.X);
                var maxX = Math.Max(mains[i].A.X, mains[i].B.X);
                var y = (mains[i].A.Y + mains[i].B.Y) * 0.5;
                Assert.AreEqual(0.0, minX, 0.1,
                    "Field DXF XC must start at the left ceiling edge.");
                Assert.AreEqual(2675.0, maxX, 0.1,
                    "Field DXF XC must extend through the shallow step to the right ceiling edge.");
                Assert.AreEqual(expectedY[i], y, 0.1,
                    "Field DXF local-notch mode must preserve the normal base XC phase when MEP avoidance is OFF.");
            }

            AssertMainBandValid(plan, 900.0, 0.0, 1800.0, settings,
                "Full-height left band must remain valid after XC extension.");
            AssertMainBandValid(plan, 2275.0, 110.0, 1800.0, settings,
                "Raised right band must remain valid after XC extension.");
        }

        private static void AssertMainBandValid(
            VxtPreviewPlan plan,
            double x,
            double minY,
            double maxY,
            VxtSettings settings,
            string message)
        {
            var ys = MainYsAtX(plan, x);
            Assert.IsTrue(ys.Length > 0, message + " No XC intersects the test band.");
            var maxEdge = settings.MainMaxEdgeOffset;
            Assert.IsTrue(ys[0] - minY <= maxEdge + 0.1,
                message + " First edge exceeds HARD Max.");
            Assert.IsTrue(maxY - ys[ys.Length - 1] <= maxEdge + 0.1,
                message + " Last edge exceeds HARD Max.");
            for (var i = 0; i + 1 < ys.Length; i++)
            {
                var gap = ys[i + 1] - ys[i];
                Assert.IsTrue(gap <= settings.MainMaxSpacing + 0.1,
                    message + " XC spacing exceeds HARD Max.");
                Assert.AreEqual(0.0,
                    Math.Abs(gap / settings.MainBalanceStep - Math.Round(gap / settings.MainBalanceStep)),
                    1e-6,
                    message + " XC spacing must stay on the configured multiple lattice.");
            }
        }

        private static VxtSettings Settings(bool autoDimension)
            => new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = autoDimension,
                DimMain = autoDimension,
                DimFurring = autoDimension,
                DimHanger = autoDimension,
                UseAvoidance = false,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true
            };

        private static double[] MainYsAtX(VxtPreviewPlan plan, double x)
            => plan.Lines
                .Where(l => l.Kind == PreviewLineKind.Main &&
                            Math.Min(l.A.X, l.B.X) <= x + 0.1 &&
                            Math.Max(l.A.X, l.B.X) >= x - 0.1)
                .Select(l => Math.Round((l.A.Y + l.B.Y) * 0.5, 3))
                .Distinct()
                .OrderBy(v => v)
                .ToArray();

        private static List<string> FurringKeys(VxtPreviewPlan plan)
            => plan.Lines.Where(l => l.Kind == PreviewLineKind.Furring)
                .Select(LineKey).OrderBy(x => x, StringComparer.Ordinal).ToList();

        private static List<string> MainKeys(VxtPreviewPlan plan)
            => plan.Lines.Where(l => l.Kind == PreviewLineKind.Main)
                .Select(LineKey).OrderBy(x => x, StringComparer.Ordinal).ToList();

        private static string LineKey(PreviewLine l)
        {
            var ax = Math.Round(l.A.X, 2); var ay = Math.Round(l.A.Y, 2);
            var bx = Math.Round(l.B.X, 2); var by = Math.Round(l.B.Y, 2);
            if (ax > bx || (Math.Abs(ax - bx) < 0.01 && ay > by))
            { var tx = ax; ax = bx; bx = tx; var ty = ay; ay = by; by = ty; }
            return ax.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   ay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                   bx.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   by.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
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

        private static Boundary2 Shallow110StepFromFieldDxf()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(1875.0, 0.0),
                new Point2(1875.0, 110.0),
                new Point2(2675.0, 110.0),
                new Point2(2675.0, 1800.0),
                new Point2(0.0, 1800.0)
            });
    }
}
