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
    public sealed class WorkflowParityTests
    {
        [TestMethod]
        public void CreateGate_NormalWorkflow_RequiresBoundary()
        {
            var settings = new VxtSettings();
            Assert.IsFalse(VxtWorkflowEligibility.CanStartCreate(false, settings));
            Assert.IsTrue(VxtWorkflowEligibility.CanStartCreate(true, settings));
        }

        [TestMethod]
        public void CreateGate_ManualHangerOnly_CanStartWithoutBoundary()
        {
            var settings = new VxtSettings
            {
                DrawMain = false,
                DrawFurring = false,
                DrawHangers = true,
                AutoDimension = false
            };

            Assert.IsTrue(VxtWorkflowEligibility.IsManualHangerOnlyStart(settings));
            Assert.IsTrue(VxtWorkflowEligibility.CanStartCreate(false, settings));
        }

        [TestMethod]
        public void CreateGate_NoFeatureSelected_IsRejectedLikeLisp()
        {
            var settings = new VxtSettings
            {
                DrawMain = false,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false
            };

            Assert.IsFalse(VxtWorkflowEligibility.HasAnyTask(settings));
            Assert.IsFalse(VxtWorkflowEligibility.CanStartCreate(true, settings));
        }

        [TestMethod]
        public void MultiBoundary_AskEach_CanStartXpFromDifferentEdges()
        {
            var settings = new VxtSettings
            {
                DrawMain = false,
                DrawFurring = true,
                DrawHangers = false,
                AutoDimension = false,
                MainDirection = MainDirectionMode.Horizontal,
                UseAvoidance = false
            };
            var context = new VxtLayoutContext();
            context.BoundaryFurringFromFarEdges.Add(false);
            context.BoundaryFurringFromFarEdges.Add(true);

            var first = Rectangle(0.0, 0.0, 4000.0, 2000.0);
            var second = Rectangle(10000.0, 0.0, 14000.0, 2000.0);
            var plan = VxtMultiBoundaryPlanBuilder.Build(new[] { first, second }, settings, context);

            var firstXs = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Furring && x.A.X < 5000.0)
                .Select(x => x.A.X)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
            var secondXs = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Furring && x.A.X > 5000.0)
                .Select(x => x.A.X - 10000.0)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            Assert.IsTrue(firstXs.Length > 0 && secondXs.Length > 0);
            Assert.AreEqual(settings.FurringSpacing, firstXs[0], 1e-6);
            var expectedFarOffset = 4000.0 % settings.FurringSpacing;
            Assert.AreEqual(expectedFarOffset, secondXs[0], 1e-6);
            Assert.IsTrue(Math.Abs(firstXs[0] - secondXs[0]) > 1.0,
                "Each selected ceiling must retain its own XP start side.");
        }

        private static Boundary2 Rectangle(double minX, double minY, double maxX, double maxY)
            => new Boundary2(new[]
            {
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY)
            });
    }
}
