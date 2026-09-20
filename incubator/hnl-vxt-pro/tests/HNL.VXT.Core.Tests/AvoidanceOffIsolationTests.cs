using System;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class AvoidanceOffIsolationTests
    {
        [TestMethod]
        public void LegacyFixed_AvoidanceOff_SelectedMepIsCompleteGeometryNoOp()
            => AssertSelectedMepIgnored(VxtOptimizationMode.Legacy, MainDirectionMode.Horizontal);

        [TestMethod]
        public void ProFixed_AvoidanceOff_SelectedMepIsCompleteGeometryNoOp()
            => AssertSelectedMepIgnored(VxtOptimizationMode.ProEconomy, MainDirectionMode.Horizontal);

        [TestMethod]
        public void ProAuto_AvoidanceOff_SelectedMepIsCompleteGeometryNoOp()
            => AssertSelectedMepIgnored(VxtOptimizationMode.ProEconomy, MainDirectionMode.Auto);

        private static void AssertSelectedMepIgnored(
            VxtOptimizationMode optimizationMode,
            MainDirectionMode direction)
        {
            var settings = new VxtSettings
            {
                OptimizationMode = optimizationMode,
                MainDirection = direction,
                AutoShadowline = true,
                DrawMain = true,
                DrawFurring = true,
                DrawHangers = true,
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true,
                UseAvoidance = false,
                ShiftAllForAvoidance = false,
                ClearanceDistance = 35.0,
                UseLocalMainAdd = true,
                MainSkipLimit = 0.0,
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false
            };

            var boundary = LowerLeftNotch();
            var empty = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            var selectedMep = new VxtLayoutContext();
            selectedMep.GeneralObstacles.Add(new Box2(0.0, 1050.0, 6000.0, 1250.0));
            selectedMep.MainObstacles.Add(new Box2(0.0, 1850.0, 6000.0, 2150.0));
            selectedMep.FurringObstacles.Add(new Box2(380.0, 0.0, 460.0, 4000.0));

            var withSelectedMep = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, selectedMep);

            CollectionAssert.AreEqual(MainKeys(empty), MainKeys(withSelectedMep),
                "Né MEP OFF: selected General/Main MEP must not move, add, remove, re-phase or re-split XC.");
            CollectionAssert.AreEqual(FurringKeys(empty), FurringKeys(withSelectedMep),
                "Né MEP OFF: selected General/Furring MEP must not move or re-phase XP.");
            CollectionAssert.AreEqual(HangerKeys(empty), HangerKeys(withSelectedMep),
                "Né MEP OFF: selected MEP must not redistribute Ty.");
            CollectionAssert.AreEqual(DimensionKeys(empty), DimensionKeys(withSelectedMep),
                "Né MEP OFF: selected MEP must not alter Dim.");
        }

        private static Boundary2 LowerLeftNotch()
            => new Boundary2(new[]
            {
                new Point2(0.0, 1500.0),
                new Point2(2500.0, 1500.0),
                new Point2(2500.0, 0.0),
                new Point2(6000.0, 0.0),
                new Point2(6000.0, 4000.0),
                new Point2(0.0, 4000.0)
            });

        private static string[] MainKeys(VxtPreviewPlan plan)
            => plan.Lines.Where(x => x.Kind == PreviewLineKind.Main)
                .Select(LineKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        private static string[] FurringKeys(VxtPreviewPlan plan)
            => plan.Lines.Where(x => x.Kind == PreviewLineKind.Furring)
                .Select(LineKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        private static string[] HangerKeys(VxtPreviewPlan plan)
            => plan.HangerPoints.Select(PointKey)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();

        private static string[] DimensionKeys(VxtPreviewPlan plan)
            => plan.Dimensions.Select(d =>
                    ((int)d.Target).ToString() + "|" +
                    PointKey(d.ExtensionPoint1) + "|" +
                    PointKey(d.ExtensionPoint2) + "|" +
                    PointKey(d.DimensionLinePoint))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

        private static string LineKey(PreviewLine line)
        {
            var a = PointKey(line.A);
            var b = PointKey(line.B);
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private static string PointKey(Point2 point)
            => Math.Round(point.X, 3).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," +
               Math.Round(point.Y, 3).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
    }
}
