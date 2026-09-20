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
                UseAvoidance = false
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
        public void ConstraintReport_IncludesActualAndReduction()
        {
            var item = new VxtConstraintDiagnostic(
                1,
                VxtConstraintTarget.Hanger,
                VxtConstraintKind.MinSpacingSoft,
                VxtConstraintSeverity.Warning,
                650.0,
                700.0);

            StringAssert.Contains(item.DisplayText, "M02");
            StringAssert.Contains(item.DisplayText, "650");
            StringAssert.Contains(item.DisplayText, "giảm 50");
        }

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
