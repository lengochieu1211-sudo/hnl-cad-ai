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
        public void LowerLeftNotch_AutoSplitsIntoRectangularMainRegions_AndKeepsFurringUnchanged()
        {
  var settings = Settings(autoDimension: true);
  var context = new VxtLayoutContext { GlobalFurringFromFarEdge = false };
  var boundary = LowerLeftNotch();

  var before = new VxtPreviewPlanBuilder().Build(boundary, settings, context);
  var after = VxtMultiBoundaryPlanBuilder.Build(new[] { boundary }, settings, context);

  CollectionAssert.AreEqual(
      FurringKeys(before).ToArray(),
      FurringKeys(after).ToArray(),
      "Auto notch regions must not change XP positions or the single global chase direction.");

  CollectionAssert.AreEqual(
      new[] { 1800.0, 2750.0, 3700.0 },
      MainYsAtX(after, 1250.0),
      "Left notch rectangle must solve its own strict-multiple XC layout.");
  CollectionAssert.AreEqual(
      new[] { 300.0, 1150.0, 2000.0, 2850.0, 3700.0 },
      MainYsAtX(after, 4250.0),
      "Long right rectangle must solve its own strict-multiple XC layout.");
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
      Assert.IsTrue(row[0].X - minX <= 425.1, "Ty first edge exceeds configured max edge.");
      Assert.IsTrue(maxX - row[row.Length - 1].X <= 425.1, "Ty last edge exceeds configured max edge.");
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
    }
}
