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
    public sealed class LocalMainSettingTests
    {
        [TestMethod]
        public void ConcaveNotch_LocalMainToggleControlsFallbackWithoutChangingFixture()
        {
            var enabled = new VxtSettings
            {
                MainDirection = MainDirectionMode.RectangleRegions,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = true, // isolated region family intentionally uses local fallback
                DimMain = false,
                DimFurring = false,
                DimHanger = false,
                UseLocalMainAdd = true,
                MinLocalMainLength = 500.0
            };

            var enabledPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, enabled, new VxtLayoutContext());

            Assert.IsTrue(HasExpectedLocalNotchMain(enabledPlan),
                "With 'Thêm XC cục bộ cạnh khuyết' ON, the unresolved notch must receive the short local XC.");

            var disabled = enabled.Clone();
            disabled.UseLocalMainAdd = false;
            var disabledPlan = VxtMultiBoundaryPlanBuilder.Build(
                new[] { LowerLeftNotch() }, disabled, new VxtLayoutContext());

            Assert.IsFalse(HasExpectedLocalNotchMain(disabledPlan),
                "With 'Thêm XC cục bộ cạnh khuyết' OFF, the local-notch fallback must not add a short XC.");
        }

        private static bool HasExpectedLocalNotchMain(VxtPreviewPlan plan)
        {
            return plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Any(x =>
                {
                    var y = (x.A.Y + x.B.Y) * 0.5;
                    var length = Math.Abs(x.B.X - x.A.X);
                    return Math.Abs(y - 1800.0) < 0.1 && length > 2400.0 && length < 2600.0;
                });
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
