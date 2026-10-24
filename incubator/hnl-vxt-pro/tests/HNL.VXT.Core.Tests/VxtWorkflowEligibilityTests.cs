using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class VxtWorkflowEligibilityTests
    {
        [TestMethod]
        public void AutoDimensionWithoutAnyDimTarget_IsNotATask()
        {
            var settings = EmptySettings();
            settings.AutoDimension = true;

            Assert.IsFalse(VxtWorkflowEligibility.HasAnyTask(settings));
            Assert.IsFalse(VxtWorkflowEligibility.CanStartCreate(true, settings));
        }

        [TestMethod]
        public void AutoDimensionWithMainDim_IsATaskWhenBoundaryExists()
        {
            var settings = EmptySettings();
            settings.AutoDimension = true;
            settings.DimMain = true;

            Assert.IsTrue(VxtWorkflowEligibility.HasAnyTask(settings));
            Assert.IsTrue(VxtWorkflowEligibility.CanStartCreate(true, settings));
            Assert.IsTrue(VxtWorkflowEligibility.IsManualDimensionOnlyStart(settings));
            Assert.IsTrue(VxtWorkflowEligibility.CanStartCreate(false, settings));
        }

        [TestMethod]
        public void NoBoundaryDimensionOnlyRequiresTargetsAndNoNewMemberTasks()
        {
            var s = EmptySettings();
            s.AutoDimension = true;
            Assert.IsFalse(VxtWorkflowEligibility.CanStartCreate(false, s));
            s.DimFurring = true;
            Assert.IsTrue(VxtWorkflowEligibility.IsManualDimensionOnlyStart(s));
            Assert.IsTrue(VxtWorkflowEligibility.CanStartCreate(false, s));
            s.DrawMain = true;
            Assert.IsFalse(VxtWorkflowEligibility.IsManualDimensionOnlyStart(s));
            Assert.IsFalse(VxtWorkflowEligibility.CanStartCreate(false, s));
        }

        [TestMethod]
        public void ManualHangerOnly_RemainsValidWithoutBoundary()
        {
            var settings = EmptySettings();
            settings.DrawHangers = true;

            Assert.IsTrue(VxtWorkflowEligibility.IsManualHangerOnlyStart(settings));
            Assert.IsTrue(VxtWorkflowEligibility.CanStartCreate(false, settings));
        }

        private static VxtSettings EmptySettings()
            => new VxtSettings
            {
                DrawMain = false,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false,
                DimMain = false,
                DimFurring = false,
                DimHanger = false,
                UseAvoidance = false
            };
    }
}
