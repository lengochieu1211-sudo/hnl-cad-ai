using HNL.VXT.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public class NumericExpressionEvaluatorTests
    {
        [DataTestMethod]
        [DataRow("1220/3", 406.6666666666667)]
        [DataRow("600+25", 625.0)]
        [DataRow("2*450", 900.0)]
        [DataRow("2x450", 900.0)]
        [DataRow("2×450", 900.0)]
        [DataRow("1200:3", 400.0)]
        [DataRow("(1200-100)/2", 550.0)]
        [DataRow("1,5*100", 150.0)]
        [DataRow("-50+600", 550.0)]
        [DataRow("=1220/4", 305.0)]
        public void EvaluatesSupportedEngineeringExpressions(string expression, double expected)
        {
            Assert.IsTrue(NumericExpressionEvaluator.TryEvaluate(expression, out var actual));
            Assert.AreEqual(expected, actual, 1e-8);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("1220/")]
        [DataRow("(100+20")]
        [DataRow("100/0")]
        [DataRow("abc")]
        [DataRow("2**3")]
        public void RejectsInvalidExpressions(string expression)
        {
            Assert.IsFalse(NumericExpressionEvaluator.TryEvaluate(expression, out _));
        }

        [TestMethod]
        public void PlainNumberDetectionDoesNotTreatExpressionAsPlainNumber()
        {
            Assert.IsTrue(NumericExpressionEvaluator.IsPlainNumber("406,67", out var number));
            Assert.AreEqual(406.67, number, 1e-8);
            Assert.IsFalse(NumericExpressionEvaluator.IsPlainNumber("1220/3", out _));
        }
    }
}
