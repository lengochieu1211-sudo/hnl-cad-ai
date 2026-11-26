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
    public sealed class DirectionParityTests
    {
        [TestMethod]
        public void TwoPointDirection_ThirtyDegrees_MainMembersStayParallelToPickedAxis()
        {
            var settings = new VxtSettings
            {
                MainDirection = MainDirectionMode.TwoPoints,
                DirectionDegrees = 30.0,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false
            };

            var plan = new VxtPreviewPlanBuilder().Build(Rectangle(6000, 4000, 0, 0), settings);
            var mains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToList();
            Assert.IsTrue(mains.Count > 0);

            var target = 30.0 * Math.PI / 180.0;
            foreach (var line in mains)
            {
                var dx = line.B.X - line.A.X;
                var dy = line.B.Y - line.A.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                Assert.IsTrue(length > 1e-6);
                var cross = Math.Abs((dx / length) * Math.Sin(target) - (dy / length) * Math.Cos(target));
                Assert.IsTrue(cross < 1e-6, "Main member is not parallel to the 2-point direction.");
            }
        }

        [TestMethod]
        public void MultiBoundary_RectangleRegionsRemainOwnedByEachSelectedPolyline()
        {
            var first = Rectangle(6000, 4000, 0, 0);
            var second = Rectangle(6000, 4000, 10000, 0);
            var settings = new VxtSettings
            {
                MainDirection = MainDirectionMode.RectangleRegions,
                DrawFurring = false,
                DrawHangers = false,
                AutoDimension = false
            };
            var context = new VxtLayoutContext();
            context.BoundaryRegionGroups.Add(new List<VxtLayoutRegion>
            {
                new VxtLayoutRegion(new Box2(0, 0, 3000, 4000), 0.0)
            });
            context.BoundaryRegionGroups.Add(new List<VxtLayoutRegion>
            {
                new VxtLayoutRegion(new Box2(13000, 0, 16000, 4000), 90.0)
            });

            var plan = VxtMultiBoundaryPlanBuilder.Build(new[] { first, second }, settings, context);
            var firstMains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main &&
                Math.Max(x.A.X, x.B.X) < 7000).ToList();
            var secondMains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main &&
                Math.Min(x.A.X, x.B.X) > 9000).ToList();

            Assert.IsTrue(firstMains.Count > 0, "First Polyline should have its own manual region geometry.");
            Assert.IsTrue(secondMains.Count > 0, "Second Polyline should have its own manual region geometry.");
            Assert.IsTrue(firstMains.All(x => Math.Abs(x.A.Y - x.B.Y) < 1e-6),
                "First Polyline must use only its horizontal region direction.");
            Assert.IsTrue(secondMains.All(x => Math.Abs(x.A.X - x.B.X) < 1e-6),
                "Second Polyline must use only its vertical region direction.");
        }

        private static Boundary2 Rectangle(double width, double height, double originX, double originY)
            => new Boundary2(new[]
            {
                new Point2(originX, originY),
                new Point2(originX + width, originY),
                new Point2(originX + width, originY + height),
                new Point2(originX, originY + height)
            });
    }
}
