using HNL.VXT.Core.Layout;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public sealed class DynamicBlockPropertyPolicyTests
    {
        [TestMethod]
        public void ArrayAndSpacingProperties_AreNeverLengthTargets()
        {
            Assert.IsTrue(DynamicBlockPropertyPolicy.IsRisky("Array Distance"));
            Assert.IsTrue(DynamicBlockPropertyPolicy.IsRisky("Khoảng cách dãy"));
            Assert.IsTrue(DynamicBlockPropertyPolicy.IsRisky("Column Count"));
            Assert.IsTrue(DynamicBlockPropertyPolicy.IsRisky("Distance2", "Array spacing"));
        }

        [TestMethod]
        public void LengthLikeProperty_IsPreferredOverSmallDistanceOffset()
        {
            var lengthScore = DynamicBlockPropertyPolicy.ScoreLengthProperty(
                "Chiều dài", string.Empty, 6000.0, 6000.0);
            var offsetScore = DynamicBlockPropertyPolicy.ScoreLengthProperty(
                "Distance1", string.Empty, 120.0, 6000.0);

            Assert.IsTrue(lengthScore > offsetScore);
            Assert.IsTrue(lengthScore >= 100);
        }

        [TestMethod]
        public void VietnameseArrayNames_AreNormalizedAndRejected()
        {
            Assert.IsTrue(DynamicBlockPropertyPolicy.IsRisky("Khoảng cách mảng"));
            Assert.IsTrue(DynamicBlockPropertyPolicy.IsRisky("Số lượng cột"));
        }
    }
}
