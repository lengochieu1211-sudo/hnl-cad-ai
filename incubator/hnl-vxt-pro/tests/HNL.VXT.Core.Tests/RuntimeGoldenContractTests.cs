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
    public sealed class RuntimeGoldenContractTests
    {
        [TestMethod]
        public void Default6000x4000_RuntimeGoldenContract_IsStable()
        {
            var settings = new VxtSettings
            {
                UseDynamicMainBlock = false,
                UseDynamicFurringBlock = false,
                AutoDimension = true,
                DimMain = true,
                DimFurring = true,
                DimHanger = true,
                UseAvoidance = false
            };

            var boundary = new Boundary2(new[]
            {
                new Point2(0.0, 0.0),
                new Point2(6000.0, 0.0),
                new Point2(6000.0, 4000.0),
                new Point2(0.0, 4000.0)
            });

            var plan = new VxtPreviewPlanBuilder().Build(boundary, settings, new VxtLayoutContext());

            Assert.AreEqual(1220.0 / 3.0, settings.FurringSpacing, 1e-10, "XP default must remain exact 1220/3.");
            Assert.AreEqual(5, plan.MainSegmentCount, "Golden 4000 main-grid run must create 5 XC lines.");
            Assert.AreEqual(14, plan.FurringSegmentCount, "Golden 6000 XP run at 1220/3 must create 14 XP lines.");
            Assert.AreEqual(35, plan.HangerCount, "Five XC rows × seven economy-spaced Ty positions = 35.");
            Assert.AreEqual(29, plan.DimensionSegmentCount, "DIM contract = 6 XC + 15 XP + 8 unique Ty chain segments.");

            var hangerRows = plan.HangerPoints.GroupBy(p => Math.Round(p.Y, 3)).ToList();
            Assert.AreEqual(5, hangerRows.Count);
            Assert.IsTrue(hangerRows.All(row => row.Count() == 7), "Each 6000 XC row should use 7 Ty positions (6 gaps near max spacing).");

            foreach (var row in hangerRows)
            {
                var xs = row.Select(p => p.X).OrderBy(x => x).ToArray();
                for (var i = 1; i < xs.Length; i++)
                {
                    var gap = xs[i] - xs[i - 1];
                    Assert.IsTrue(gap >= settings.HangerMinSpacing - 1e-8 && gap <= settings.HangerMaxSpacing + 1e-8);
                }
            }
        }
    }
}
