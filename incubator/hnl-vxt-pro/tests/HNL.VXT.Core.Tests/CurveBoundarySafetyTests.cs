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
    public sealed class CurveBoundarySafetyTests
    {
        [TestMethod]
        public void LargeSemicircle_AdaptiveBulgeSamplingUsesMoreThanFixed12()
        {
            var start = new Point2(-10000.0, 0.0);
            var end = new Point2(10000.0, 0.0);

            var count = VxtBulgeSampler.GetSubdivisionCount(start, end, 1.0);
            var samples = VxtBulgeSampler.SampleSegment(start, end, 1.0);

            Assert.IsTrue(count > 12,
                "A 20 m diameter semicircle must not fall back to the old fixed 12-chord approximation.");
            Assert.AreEqual(count, samples.Count);

            var radius = 10000.0;
            var segmentAngle = Math.PI / count;
            var sagitta = radius * (1.0 - Math.Cos(segmentAngle * 0.5));
            Assert.IsTrue(sagitta <= VxtBulgeSampler.DefaultMaxChordError + 1e-6,
                "Adaptive chord error must stay inside the geometric sampling tolerance.");
        }

        [TestMethod]
        public void StraightBulgeSamplingRemainsOneSegment()
        {
            var start = new Point2(0.0, 0.0);
            var end = new Point2(6000.0, 0.0);

            Assert.AreEqual(1, VxtBulgeSampler.GetSubdivisionCount(start, end, 0.0));
            var samples = VxtBulgeSampler.SampleSegment(start, end, 0.0);
            Assert.AreEqual(1, samples.Count);
            Assert.AreEqual(start.X, samples[0].X, 1e-9);
            Assert.AreEqual(start.Y, samples[0].Y, 1e-9);
        }

        [TestMethod]
        public void ConvexCurvedOuterBoundary_LocalMainOff_PreservesBaseGridAndHardWarning()
        {
            var boundary = DomeBoundary();
            var settings = MainOnlySettings();
            settings.UseLocalMainAdd = false;

            var raw = new VxtPreviewPlanBuilder().Build(boundary, settings, new VxtLayoutContext());
            VxtPlanConstraintAuditor.Attach(boundary, raw, settings, 0.0, 0);
            Assert.IsTrue(raw.Diagnostics.Any(x =>
                    x.Target == VxtConstraintTarget.Main &&
                    x.Kind == VxtConstraintKind.MaxEdgeHard),
                "The base grid should expose the curved-edge HARD Max gap in this fixture.");

            var final = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            CollectionAssert.AreEqual(
                MainKeys(raw).ToArray(),
                MainKeys(final).ToArray(),
                "Local XC OFF must not add curved-edge XC behind the user's toggle.");

            Assert.IsTrue(final.Diagnostics.Any(x =>
                    x.Target == VxtConstraintTarget.Main &&
                    x.Kind == VxtConstraintKind.MaxEdgeHard),
                "Local XC OFF must preserve the HARD Max warning so Create can block or require manual override.");
        }

        [TestMethod]
        public void ConvexCurvedOuterBoundary_LocalMainOn_RepairsMaxEdge()
        {
            var boundary = DomeBoundary();
            var settings = MainOnlySettings();
            settings.UseLocalMainAdd = true;

            var raw = new VxtPreviewPlanBuilder().Build(boundary, settings, new VxtLayoutContext());
            VxtPlanConstraintAuditor.Attach(boundary, raw, settings, 0.0, 0);

            var repaired = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            var diagnosticDump = string.Join(" | ", repaired.Diagnostics.Select(x =>
                x.Kind + ":" + x.ActualValue.ToString("0.###") + "/" + x.LimitValue.ToString("0.###")));
            Assert.IsFalse(repaired.Diagnostics.Any(x =>
                    x.Target == VxtConstraintTarget.Main &&
                    x.Kind == VxtConstraintKind.MaxEdgeHard),
                "Local XC ON must remove the curved-edge HARD MaxEdge violation. " +
                "RawMain=" + raw.MainSegmentCount + " FinalMain=" + repaired.MainSegmentCount +
                " Diagnostics=" + diagnosticDump);
            Assert.IsTrue(repaired.MainSegmentCount > raw.MainSegmentCount,
                "Local XC ON must supplement the certified base grid instead of shifting/replacing it.");
        }

        [TestMethod]
        public void OrthogonalRectangle_RemainsUnchangedByOuterCurveSafety()
        {
            var boundary = new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(6000.0, 0.0),
                new Point2(6000.0, 4000.0),
                new Point2(0.0, 4000.0)
            });
            var settings = MainOnlySettings();
            settings.UseLocalMainAdd = false;

            var raw = new VxtPreviewPlanBuilder().Build(boundary, settings, new VxtLayoutContext());
            var final = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.AreEqual(raw.MainSegmentCount, final.MainSegmentCount);
            CollectionAssert.AreEqual(
                MainKeys(raw).ToArray(),
                MainKeys(final).ToArray(),
                "Normal orthogonal ceilings must not be changed by curved-edge safety.");
        }

        [TestMethod]
        public void ConcaveNonOrthogonalBoundary_IsNotAutoRepairedByConvexSafety()
        {
            var boundary = new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(6000.0, 0.0),
                new Point2(6000.0, 4000.0),
                new Point2(3000.0, 3000.0),
                new Point2(0.0, 4000.0)
            });
            var settings = MainOnlySettings();
            settings.UseLocalMainAdd = false;

            var raw = new VxtPreviewPlanBuilder().Build(boundary, settings, new VxtLayoutContext());
            var final = VxtMultiBoundaryPlanBuilder.Build(
                new[] { boundary }, settings, new VxtLayoutContext());

            Assert.AreEqual(raw.MainSegmentCount, final.MainSegmentCount,
                "Concave sloped/curved geometry must stay on the old/manual path until separately certified.");
        }

        private static VxtSettings MainOnlySettings()
            => new VxtSettings
            {
                OptimizationMode = VxtOptimizationMode.Legacy,
                MainDirection = MainDirectionMode.Horizontal,
                MainLayout = MainLayoutMode.BalancedTwoEnds,
                DrawMain = true,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                MainMinSpacing = 700.0,
                MainMaxSpacing = 1000.0,
                MainMinEdgeOffset = 300.0,
                MainMaxEdgeOffset = 400.0,
                MainBalanceStep = 50.0,
                MinLocalMainLength = 500.0,
                MainSkipLimit = 0.0,
                UseAvoidance = false
            };

        private static Boundary2 DomeBoundary()
        {
            var points = new List<Point2>
            {
                new Point2(0.0, 0.0),
                new Point2(6000.0, 0.0)
            };

            points.AddRange(VxtBulgeSampler.SampleSegment(
                new Point2(6000.0, 3000.0),
                new Point2(0.0, 3000.0),
                1.0));
            points.Add(new Point2(0.0, 3000.0));
            return new Boundary2(points);
        }

        private static IEnumerable<string> MainKeys(VxtPreviewPlan plan)
            => plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x => Key(x.A) + "|" + Key(x.B))
                .OrderBy(x => x);

        private static string Key(Point2 p)
            => Math.Round(p.X, 3) + "," + Math.Round(p.Y, 3);
    }
}
