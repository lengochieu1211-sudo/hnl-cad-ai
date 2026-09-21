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
    public sealed class ConstraintContractTests
    {
        [TestMethod]
        public void DenseMinSpacing_IsSoft_ButMaxAndMultipleStayHard()
        {
            var layout = SmartLayout1D.Calculate(
                1200.0,
                maxSpacing: 1000.0,
                minSpacing: 700.0,
                maxEdge: 400.0,
                minEdge: 300.0,
                increment: 50.0,
                mode: MainLayoutMode.BalancedTwoEnds,
                minEdgeTolerance: 25.0);

            Assert.IsNotNull(layout);
            Assert.IsTrue(layout.Steps.Any(x => x < 700.0 - 0.1),
                "Fixture must exercise Dense/soft Min spacing.");
            Assert.IsTrue(layout.Steps.All(x => x <= 1000.0 + 0.1));
            Assert.IsTrue(layout.Steps.All(x =>
                Math.Abs(x / 50.0 - Math.Round(x / 50.0)) <= 1e-8));
            Assert.IsTrue(layout.StartOffset <= 400.0 + 0.1);
            Assert.IsTrue(layout.EndOffset <= 400.0 + 0.1);
        }

        [TestMethod]
        public void MinEdge_IsSoft_ButMaxEdgeIsNeverExtended()
        {
            var layout = SmartLayout1D.Calculate(
                500.0,
                maxSpacing: 1000.0,
                minSpacing: 700.0,
                maxEdge: 400.0,
                minEdge: 300.0,
                increment: 50.0,
                mode: MainLayoutMode.BalancedTwoEnds,
                minEdgeTolerance: 25.0);

            Assert.IsNotNull(layout);
            Assert.IsTrue(layout.StartOffset < 300.0 - 0.1);
            Assert.IsTrue(layout.EndOffset < 300.0 - 0.1);
            Assert.IsTrue(layout.StartOffset <= 400.0 + 0.1);
            Assert.IsTrue(layout.EndOffset <= 400.0 + 0.1);
        }

        [TestMethod]
        public void MultiBoundaryDiagnostics_MapWarningsToM01M02()
        {
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                UseLocalMainAdd = false,
                UseAvoidance = false,
                MainSkipLimit = 0.0
            };

            var boundaries = new[]
            {
                Rectangle(0.0, 0.0, 3000.0, 1200.0),
                Rectangle(4000.0, 0.0, 7000.0, 500.0)
            };

            var plan = VxtMultiBoundaryPlanBuilder.Build(
                boundaries, settings, new VxtLayoutContext());

            Assert.IsTrue(plan.Diagnostics.Any(x =>
                x.BoundaryCode == "M01" &&
                x.Target == VxtConstraintTarget.Main &&
                x.Kind == VxtConstraintKind.MinSpacingSoft &&
                !x.IsHard));

            Assert.IsTrue(plan.Diagnostics.Any(x =>
                x.BoundaryCode == "M02" &&
                x.Target == VxtConstraintTarget.Main &&
                x.Kind == VxtConstraintKind.MinEdgeSoft &&
                !x.IsHard));

            Assert.IsFalse(plan.Diagnostics.Any(x =>
                x.Kind == VxtConstraintKind.MaxSpacingHard ||
                x.Kind == VxtConstraintKind.MaxEdgeHard ||
                x.Kind == VxtConstraintKind.SpacingStepHard));
        }

        [TestMethod]
        public void Auditor_MainSkipLimit_DoesNotReportIntentionalMissingMainAsHard()
        {
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                MainSkipLimit = 500.0,
                UseLocalMainAdd = true
            };

            var boundary = Rectangle(0.0, 0.0, 3000.0, 400.0);
            var plan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.IsFalse(plan.Lines.Any(x => x.Kind == PreviewLineKind.Main),
                "Fixture must exercise intentional whole-boundary XC skip.");
            Assert.IsFalse(plan.Diagnostics.Any(x =>
                x.Target == VxtConstraintTarget.Main && x.IsHard),
                "MainSkipLimit is an intentional construction rule and must not become MissingCoverageHard.");
        }

        [TestMethod]
        public void Auditor_ShortNotchBelowMinLocalMainLength_DoesNotCreateImpossibleHardConflict()
        {
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainBalanceStep = 50.0
            };

            var boundary = ShortLeftNotchBand400();
            var plan = new VxtPreviewPlan();

            // Main body is hard-valid. In the 400-mm-wide notch band only Y=300 crosses,
            // leaving a 500-mm far edge. Repair would require a local XC only 400 mm long,
            // which MinLocalMainLength=500 explicitly forbids.
            plan.Lines.Add(new PreviewLine(
                new Point2(0.0, 300.0), new Point2(3000.0, 300.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(
                new Point2(400.0, 1100.0), new Point2(3000.0, 1100.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(
                new Point2(400.0, 1500.0), new Point2(3000.0, 1500.0), PreviewLineKind.Main));

            VxtPlanConstraintAuditor.Attach(boundary, plan, settings, 0.0, 0);

            Assert.IsFalse(plan.Diagnostics.Any(x => x.IsHard),
                "A notch repair shorter than the configured HARD MinLocalMainLength must not block Create.");
            Assert.IsTrue(plan.Diagnostics.Any(x =>
                x.Kind == VxtConstraintKind.ManualMainRequiredWarning && !x.IsHard),
                "Unresolved notch coverage must stay visible as a manual-XC warning.");
            Assert.IsTrue(plan.Diagnostics.Any(x =>
                x.Kind == VxtConstraintKind.MinSpacingSoft && !x.IsHard),
                "Main-body soft Min spacing diagnostics must still be audited outside the exempt short notch.");
        }

        [TestMethod]
        public void Auditor_NotchCoverageWithLocalMainOff_BecomesManualWarning_NotHard()
        {
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = false,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainBalanceStep = 50.0
            };

            var boundary = ShortLeftNotchBand400();
            var plan = new VxtPreviewPlan();
            plan.Lines.Add(new PreviewLine(
                new Point2(0.0, 300.0), new Point2(3000.0, 300.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(
                new Point2(400.0, 1100.0), new Point2(3000.0, 1100.0), PreviewLineKind.Main));
            plan.Lines.Add(new PreviewLine(
                new Point2(400.0, 1500.0), new Point2(3000.0, 1500.0), PreviewLineKind.Main));

            VxtPlanConstraintAuditor.Attach(boundary, plan, settings, 0.0, 0);

            Assert.IsTrue(plan.Diagnostics.Any(x =>
                x.Kind == VxtConstraintKind.ManualMainRequiredWarning &&
                !x.IsHard &&
                x.BoundaryCode == "M01"));
            Assert.IsFalse(plan.Diagnostics.Any(x =>
                x.Target == VxtConstraintTarget.Main &&
                (x.Kind == VxtConstraintKind.MaxEdgeHard ||
                 x.Kind == VxtConstraintKind.MaxSpacingHard ||
                 x.Kind == VxtConstraintKind.MissingCoverageHard)),
                "A real notch left for manual completion must not block Create.");
        }

        [TestMethod]
        public void ManualNotchWarning_DisplayAndSummary_AreExplicit()
        {
            var item = new VxtConstraintDiagnostic(
                0,
                VxtConstraintTarget.Main,
                VxtConstraintKind.ManualMainRequiredWarning,
                VxtConstraintSeverity.Warning,
                500.0,
                400.0);

            StringAssert.Contains(item.DisplayText, "XC cạnh khuyết");
            StringAssert.Contains(item.DisplayText, "Cần bổ sung thủ công");
            Assert.IsFalse(item.IsHard);
            StringAssert.Contains(
                VxtConstraintReport.FormatSummary(new[] { item }),
                "1 cần bổ sung XC thủ công");
        }

        [TestMethod]
        public void Auditor_StillReportsRealHardMaxEdge_OutsideSkipExemptions()
        {
            var settings = new VxtSettings
            {
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                MainSkipLimit = 0.0,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0,
                MainMaxEdgeOffset = 400.0
            };

            var boundary = Rectangle(0.0, 0.0, 3000.0, 1800.0);
            var plan = new VxtPreviewPlan();
            plan.Lines.Add(new PreviewLine(
                new Point2(0.0, 300.0), new Point2(3000.0, 300.0), PreviewLineKind.Main));

            VxtPlanConstraintAuditor.Attach(boundary, plan, settings, 0.0, 0);

            Assert.IsTrue(plan.Diagnostics.Any(x =>
                x.IsHard && x.Kind == VxtConstraintKind.MaxEdgeHard),
                "Normal/full-width HARD Max-edge violations must still block Create.");
        }

        [TestMethod]
        public void ConstraintReport_IncludesVietnameseActualAndReduction()
        {
            var item = new VxtConstraintDiagnostic(
                1,
                VxtConstraintTarget.Hanger,
                VxtConstraintKind.MinSpacingSoft,
                VxtConstraintSeverity.Warning,
                650.0,
                700.0);

            StringAssert.Contains(item.DisplayText, "M02");
            StringAssert.Contains(item.DisplayText, "Cảnh báo");
            StringAssert.Contains(item.DisplayText, "Khoảng cách Min");
            StringAssert.Contains(item.DisplayText, "650");
            StringAssert.Contains(item.DisplayText, "giảm 50");
            Assert.IsFalse(item.DisplayText.Contains("HARD"));
            Assert.IsFalse(item.DisplayText.Contains("spacing"));
        }

        [TestMethod]
        public void ConstraintReport_HardUsesVietnameseLoiAndMax()
        {
            var item = new VxtConstraintDiagnostic(
                2,
                VxtConstraintTarget.Main,
                VxtConstraintKind.MaxEdgeHard,
                VxtConstraintSeverity.Error,
                500.0,
                400.0);

            StringAssert.Contains(item.DisplayText, "M03");
            StringAssert.Contains(item.DisplayText, "Lỗi");
            StringAssert.Contains(item.DisplayText, "Biên Max");
            StringAssert.Contains(item.DisplayText, "vượt 100");
            Assert.IsFalse(item.DisplayText.Contains("HARD"));
        }

        [TestMethod]
        public void ConstraintReport_SummaryCountsDistinctBoundariesErrorsAndWarnings()
        {
            var diagnostics = new[]
            {
                new VxtConstraintDiagnostic(
                    0, VxtConstraintTarget.Main, VxtConstraintKind.MaxEdgeHard,
                    VxtConstraintSeverity.Error, 500.0, 400.0),
                new VxtConstraintDiagnostic(
                    0, VxtConstraintTarget.Hanger, VxtConstraintKind.MinSpacingSoft,
                    VxtConstraintSeverity.Warning, 650.0, 700.0),
                new VxtConstraintDiagnostic(
                    2, VxtConstraintTarget.Main, VxtConstraintKind.MinEdgeSoft,
                    VxtConstraintSeverity.Warning, 290.0, 300.0)
            };

            Assert.AreEqual(
                "2 mảng có vấn đề • 1 lỗi • 2 cảnh báo",
                VxtConstraintReport.FormatSummary(diagnostics));
        }

        private static Boundary2 ShortLeftNotchBand400()
            => new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(3000.0, 0.0),
                new Point2(3000.0, 1800.0),
                new Point2(400.0, 1800.0),
                new Point2(400.0, 800.0),
                new Point2(0.0, 800.0)
            });

        private static Boundary2 Rectangle(
            double minX,
            double minY,
            double maxX,
            double maxY)
            => new Boundary2(new[]
            {
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY)
            });
    }
}
