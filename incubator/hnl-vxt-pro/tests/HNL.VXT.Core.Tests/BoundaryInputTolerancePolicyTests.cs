using HNL.VXT.Core.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public class BoundaryInputTolerancePolicyTests
    {
        [TestMethod]
        public void OpenGap_WithinOneMillimeter_IsAccepted()
        {
            Assert.IsTrue(BoundaryInputTolerancePolicy.AcceptOpenGap(0.5));
            Assert.IsTrue(BoundaryInputTolerancePolicy.AcceptOpenGap(1.0));
        }

        [TestMethod]
        public void OpenGap_AboveOneMillimeter_IsRejected()
        {
            Assert.IsFalse(BoundaryInputTolerancePolicy.AcceptOpenGap(1.5));
        }

        [TestMethod]
        public void NearZeroZ_IsAcceptedAndNormalized()
        {
            const double z = 0.00000001;
            Assert.IsTrue(BoundaryInputTolerancePolicy.AcceptZ(z));
            Assert.IsTrue(BoundaryInputTolerancePolicy.IsTinyNonZeroZ(z));
        }

        [TestMethod]
        public void ZAboveTolerance_IsRejected()
        {
            Assert.IsFalse(BoundaryInputTolerancePolicy.AcceptZ(0.02));
        }
    }
}
