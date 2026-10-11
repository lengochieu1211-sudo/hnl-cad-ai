using System;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HNL.VXT.Core.Tests
{
    [TestClass]
    public class LayInCeilingPlannerTests
    {
        [TestMethod]
        public void LayInUiDefaults_UseCurrentDimStyleAndConcealedPreferredTyBlock()
        {
            var settings = new LayInCeilingSettings();
            Assert.AreEqual(string.Empty, settings.DimensionStyle);
            Assert.AreEqual("AP_DIEM TY 06.2026", settings.HangerBlockName);
            Assert.AreEqual(LayInCeilingSettings.DefaultHangerBlockName,
                settings.HangerBlockName);
            Assert.IsTrue(settings.DrawStartTileBlock);
            Assert.AreEqual(string.Empty, settings.StartMarkerBlockName);
            Assert.IsFalse(settings.AddOverallDimension, "Default DIM chains must remain unchanged.");
            Assert.AreEqual(350.0, settings.DimensionRowSpacing, 0.001);
            Assert.IsFalse(settings.DimensionHangers);
            Assert.AreEqual(180.0, settings.HangerDimDistance, 0.001);
        }

        [TestMethod]
        public void LayInDrawingAppearanceOptions_CloneWithoutTouchingGeometry()
        {
            var settings = new LayInCeilingSettings
            {
                DimensionStyle = "ICC-250",
                HangerBlockName = "TY-CUSTOM",
                StartMarkerBlockName = "*U6",
                GridSystem = LayInGridSystem.Module610x610
            };
            var clone = settings.Clone();
            Assert.AreEqual("ICC-250", clone.DimensionStyle);
            Assert.AreEqual("TY-CUSTOM", clone.HangerBlockName);
            Assert.AreEqual("*U6", clone.StartMarkerBlockName);

            var basePlan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6100, 4880), null, settings);
            clone.DimensionStyle = string.Empty;
            clone.HangerBlockName = LayInCeilingSettings.DefaultHangerBlockName;
            clone.StartMarkerBlockName = string.Empty;
            var changedPlan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6100, 4880), null, clone);
            Assert.AreEqual(basePlan.TeeSegments.Count, changedPlan.TeeSegments.Count);
            Assert.AreEqual(basePlan.HangerPoints.Count, changedPlan.HangerPoints.Count);
            Assert.AreEqual(basePlan.DimensionRuns.Count, changedPlan.DimensionRuns.Count);
            Assert.AreEqual(basePlan.HatchPatternName, changedPlan.HatchPatternName);
        }

        [TestMethod]
        public void Module600Square_UsesT3600_T1200_T600()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced,
                    DrawHangers = true
                });

            Assert.AreEqual(3600.0, plan.MainTeeStockLength, 0.001);
            Assert.AreEqual(1200.0, plan.LongCrossTeeStockLength, 0.001);
            Assert.AreEqual(600.0, plan.ShortCrossTeeStockLength, 0.001);
            Assert.AreEqual("HNL_CF_600X600", plan.HatchPatternName);
            Assert.IsTrue(plan.MainSegmentCount > 0);
            Assert.IsTrue(plan.LongCrossSegmentCount > 0);
            Assert.IsTrue(plan.ShortCrossSegmentCount > 0);
            Assert.IsTrue(plan.HangerPoints.Count > 0);
        }

        [TestMethod]
        public void Module610Square_UsesT3660_T1220_T610()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6100, 4880),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module610x610,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced
                });

            Assert.AreEqual(3660.0, plan.MainTeeStockLength, 0.001);
            Assert.AreEqual(1220.0, plan.LongCrossTeeStockLength, 0.001);
            Assert.AreEqual(610.0, plan.ShortCrossTeeStockLength, 0.001);
            Assert.AreEqual("HNL_CF_610X610", plan.HatchPatternName);
            Assert.IsTrue(plan.ShortCrossTeeCount > 0);
        }

        [TestMethod]
        public void RectangularPanel_HasNoShortCrossTee()
        {
            foreach (var system in new[]
            {
                LayInGridSystem.Module600x1200,
                LayInGridSystem.Module610x1220
            })
            {
                var plan = LayInCeilingPlanner.Build(
                    Rect(0, 0, 7200, 4800),
                    null,
                    new LayInCeilingSettings
                    {
                        GridSystem = system,
                        MainDirection = LayInMainDirectionMode.Horizontal,
                        StartMode = LayInStartMode.Balanced
                    });

                Assert.AreEqual(0, plan.ShortCrossSegmentCount, system.ToString());
                Assert.AreEqual(0, plan.ShortCrossTeeCount, system.ToString());
                Assert.IsTrue(plan.LongCrossTeeCount > 0, system.ToString());
            }
        }

        [TestMethod]
        public void LongCross_IsPerpendicularToMain_AndShortCrossParallelToMain()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced
                });

            var main = plan.TeeSegments.First(x => x.Kind == LayInTeeKind.MainTee);
            var longCross = plan.TeeSegments.First(x => x.Kind == LayInTeeKind.LongCrossTee);
            var shortCross = plan.TeeSegments.First(x => x.Kind == LayInTeeKind.ShortCrossTee);

            Assert.IsTrue(Math.Abs(main.A.Y - main.B.Y) < 0.001);
            Assert.IsTrue(Math.Abs(longCross.A.X - longCross.B.X) < 0.001);
            Assert.IsTrue(Math.Abs(shortCross.A.Y - shortCross.B.Y) < 0.001);
        }

        [TestMethod]
        public void ParallelLongAndShortSide_ResolveDifferentAxes()
        {
            var boundary = Rect(0, 0, 8000, 3000);

            var alongLong = LayInCeilingPlanner.Build(
                boundary,
                null,
                new LayInCeilingSettings
                {
                    MainDirection = LayInMainDirectionMode.ParallelLongSide,
                    GridSystem = LayInGridSystem.Module600x600
                });

            var alongShort = LayInCeilingPlanner.Build(
                boundary,
                null,
                new LayInCeilingSettings
                {
                    MainDirection = LayInMainDirectionMode.ParallelShortSide,
                    GridSystem = LayInGridSystem.Module600x600
                });

            AssertAngleEquivalent(0.0, alongLong.MainAngleRadians);
            AssertAngleEquivalent(Math.PI * 0.5, alongShort.MainAngleRadians);
        }

        [TestMethod]
        public void FromDoor_UsesNearestBoundaryCornerAsHatchOriginReference()
        {
            var boundary = Rect(0, 0, 6000, 4800);
            var settings = new LayInCeilingSettings
            {
                GridSystem = LayInGridSystem.Module600x600,
                MainDirection = LayInMainDirectionMode.Horizontal,
                StartMode = LayInStartMode.FromDoor,
                DoorPoint = new Point2(40, 80)
            };

            var plan = LayInCeilingPlanner.Build(boundary, null, settings);

            Assert.AreEqual(0.0, plan.HatchOrigin.X, 0.001);
            Assert.AreEqual(0.0, plan.HatchOrigin.Y, 0.001);
        }

        [TestMethod]
        public void Balanced_Start_CentersRemainderOnBothSides()
        {
            var boundary = Rect(0, 0, 6500, 5000);
            var plan = LayInCeilingPlanner.Build(
                boundary,
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced
                });

            // 6500 = 10*600 + 500 => 250 mm balanced X edge.
            // 5000 = 4*1200 + 200 => 100 mm balanced Main Tee edge.
            Assert.AreEqual(250.0, plan.HatchOrigin.X, 0.001);
            Assert.AreEqual(100.0, plan.HatchOrigin.Y, 0.001);
        }

        [TestMethod]
        public void Hole_RemovesAllTeeFamiliesAndHangersFromVoid()
        {
            var hole = Rect(2400, 1800, 3600, 3000);
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                new[] { hole },
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced,
                    DrawHangers = true
                });

            foreach (var tee in plan.TeeSegments)
            {
                var mid = new Point2(
                    (tee.A.X + tee.B.X) * 0.5,
                    (tee.A.Y + tee.B.Y) * 0.5);
                Assert.IsFalse(Inside(mid, hole), tee.Kind + " entered the hole.");
            }

            Assert.IsFalse(plan.HangerPoints.Any(p => Inside(p, hole)));
        }

        [TestMethod]
        public void Hangers_AreGeneratedOnlyOnMainTeeRows()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    DrawHangers = true,
                    HangerMaxSpacing = 1200.0,
                    HangerEdgeTarget = 300.0
                });

            var mainYs = plan.TeeSegments
                .Where(x => x.Kind == LayInTeeKind.MainTee)
                .Select(x => Math.Round(x.A.Y, 3))
                .Distinct()
                .ToList();

            Assert.IsTrue(plan.HangerPoints.Count > 0);
            foreach (var hanger in plan.HangerPoints)
                Assert.IsTrue(mainYs.Contains(Math.Round(hanger.Y, 3)));
        }

        [TestMethod]
        public void GroupedDimension_FormatsTwelveModules()
        {
            var plan600 = LayInCeilingPlanner.Build(
                Rect(0, 0, 12000, 6000),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    DimensionMode = LayInDimensionMode.Grouped,
                    GroupedDimensionCount = 12
                });

            var plan610 = LayInCeilingPlanner.Build(
                Rect(0, 0, 12200, 6100),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module610x610,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    DimensionMode = LayInDimensionMode.Grouped,
                    GroupedDimensionCount = 12
                });

            Assert.IsTrue(plan600.DimensionRuns.Any(x => x.Label == "12 × 600 = 7200"));
            Assert.IsTrue(plan610.DimensionRuns.Any(x => x.Label == "12 × 610 = 7320"));
        }

        [TestMethod]
        public void ManualStart_GroupedDimensionsMatchExactMeasuredSpan()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800), null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.ManualStart,
                    ManualStartPoint = new Point2(300, 150),
                    DimensionMode = LayInDimensionMode.Grouped,
                    GroupedDimensionCount = 12
                });

            Assert.AreEqual(6, plan.DimensionRuns.Count,
                "Two exact full-module groups plus four independently measured cut edges.");
            var x = plan.DimensionRuns.Single(d => d.ModuleCount == 9);
            var y = plan.DimensionRuns.Single(d => d.ModuleCount == 7);
            Assert.AreEqual("9 × 600 = 5400", x.Label);
            Assert.AreEqual("7 × 600 = 4200", y.Label);
            CollectionAssert.AreEquivalent(new[] { 300.0, 300.0, 150.0, 450.0 },
                plan.DimensionRuns.Where(d => d.ModuleCount == 1)
                    .Select(d => d.A.DistanceTo(d.B)).ToArray());
            foreach (var run in plan.DimensionRuns)
                Assert.AreEqual(run.Module * run.ModuleCount, run.A.DistanceTo(run.B), 0.001,
                    "Each DIM label or native measured span must match its geometry.");
        }

        [TestMethod]
        public void NarrowBoundary_MeasuresCutEdgesButNeverManufacturesFullModule()
        {
            foreach (var system in new[]
            {
                LayInGridSystem.Module600x600,
                LayInGridSystem.Module610x610,
                LayInGridSystem.Module600x1200,
                LayInGridSystem.Module610x1220
            })
            {
                var plan = LayInCeilingPlanner.Build(
                    Rect(0, 0, 100, 100), null,
                    new LayInCeilingSettings
                    {
                        GridSystem = system,
                        MainDirection = LayInMainDirectionMode.Horizontal,
                        StartMode = LayInStartMode.ManualStart,
                        ManualStartPoint = new Point2(50, 50),
                        DimensionMode = LayInDimensionMode.Grouped,
                        GroupedDimensionCount = 12
                    });
                // Start at 50 inside a 100-wide boundary: both 50-mm cut edges
                // are measured on each axis; no 600/610 panel may be invented.
                Assert.AreEqual(4, plan.DimensionRuns.Count, system.ToString());
                Assert.IsTrue(plan.DimensionRuns.All(d => d.ModuleCount == 1 &&
                    d.Label == string.Empty && Math.Abs(d.A.DistanceTo(d.B) - 50) < 0.001));
            }
        }

        [TestMethod]
        public void ModuleMode_DimensionsEveryWholePanelForAllFourGridSystems()
        {
            foreach (var item in new[]
            {
                Tuple.Create(LayInGridSystem.Module600x600, 600.0, 1200.0),
                Tuple.Create(LayInGridSystem.Module610x610, 610.0, 1220.0),
                Tuple.Create(LayInGridSystem.Module600x1200, 600.0, 2400.0),
                Tuple.Create(LayInGridSystem.Module610x1220, 610.0, 2440.0)
            })
            {
                var settings = new LayInCeilingSettings
                {
                    GridSystem = item.Item1,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.ManualStart,
                    ManualStartPoint = new Point2(0, 0),
                    DimensionMode = LayInDimensionMode.Module
                };
                var plan = LayInCeilingPlanner.Build(
                    Rect(0, 0, 4 * item.Item2, item.Item3), null, settings);
                var horizontal = plan.DimensionRuns
                    .Where(d => Math.Abs(d.A.Y - d.B.Y) < 0.001).ToArray();
                Assert.AreEqual(4, horizontal.Length, item.Item1.ToString());
                Assert.IsTrue(horizontal.All(d => d.ModuleCount == 1 &&
                    d.Label == string.Empty &&
                    Math.Abs(d.A.DistanceTo(d.B) - item.Item2) < 0.001),
                    "Each complete tile must have its own native measured DIM.");
                Assert.AreEqual(6, plan.DimensionRuns.Count, item.Item1.ToString());
            }
        }

        [TestMethod]
        public void GroupedMode_MeasuresAllGroupsShortGroupAndCutSeparately()
        {
            foreach (var item in new[]
            {
                Tuple.Create(LayInGridSystem.Module600x600, 600.0, 200.0, 4),
                Tuple.Create(LayInGridSystem.Module610x610, 610.0, 530.0, 3)
            })
            {
                var settings = new LayInCeilingSettings
                {
                    GridSystem = item.Item1,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.ManualStart,
                    ManualStartPoint = new Point2(0, 0),
                    DimensionMode = LayInDimensionMode.Grouped,
                    GroupedDimensionCount = 12
                };
                var plan = LayInCeilingPlanner.Build(
                    Rect(0, 0, 17000, item.Item2 * 10), null, settings);
                var h = plan.DimensionRuns
                    .Where(d => Math.Abs(d.A.Y - d.B.Y) < 0.001).ToArray();
                Assert.AreEqual(4, h.Length);
                CollectionAssert.AreEqual(
                    new[] { 12, 12, item.Item4, 1 }, h.Select(d => d.ModuleCount).ToArray());
                Assert.AreEqual("12 × " + item.Item2.ToString("0") + " = " +
                    (12 * item.Item2).ToString("0"), h[0].Label);
                Assert.AreEqual(item.Item4 * item.Item2, h[2].A.DistanceTo(h[2].B), 0.001);
                Assert.AreEqual(item.Item3, h[3].A.DistanceTo(h[3].B), 0.001);
                Assert.AreEqual(string.Empty, h[3].Label);
                Assert.AreEqual(17000, h.Sum(d => d.A.DistanceTo(d.B)), 0.001);
            }
        }

        [TestMethod]
        public void TyDimensions_UseActualHangerPositionsAndDoNotAffectGridOrTy()
        {
            var settings = new LayInCeilingSettings
            {
                MainDirection = LayInMainDirectionMode.Horizontal,
                GridSystem = LayInGridSystem.Module610x610,
                StartMode = LayInStartMode.ManualStart,
                ManualStartPoint = new Point2(0, 0),
                DimensionMode = LayInDimensionMode.Overall,
                DrawHangers = true,
                HangerMaxSpacing = 1200,
                HangerEdgeTarget = 300,
                HangerDimDistance = 180,
                HangerDimSide = LayInHorizontalDimSide.Top
            };
            var ordinary = LayInCeilingPlanner.Build(Rect(0, 0, 6100, 4880), null, settings);
            var withTySettings = settings.Clone();
            withTySettings.DimensionHangers = true;
            var withTy = LayInCeilingPlanner.Build(Rect(0, 0, 6100, 4880), null, withTySettings);
            Assert.AreEqual(ordinary.TeeSegments.Count, withTy.TeeSegments.Count);
            Assert.AreEqual(ordinary.HangerPoints.Count, withTy.HangerPoints.Count);
            Assert.AreEqual(ordinary.DimensionRuns.Count, 2);
            Assert.IsTrue(withTy.DimensionRuns.Count > ordinary.DimensionRuns.Count);
            var tyDims = withTy.DimensionRuns.Skip(ordinary.DimensionRuns.Count).ToArray();
            foreach (var tee in withTy.TeeSegments.Where(t => t.Kind == LayInTeeKind.MainTee))
            {
                var dims = tyDims.Where(d => Math.Abs(d.A.Y - tee.A.Y) < 0.001 &&
                    Math.Abs(d.B.Y - tee.B.Y) < 0.001 &&
                    Math.Min(d.A.X, d.B.X) >= Math.Min(tee.A.X, tee.B.X) - 0.001 &&
                    Math.Max(d.A.X, d.B.X) <= Math.Max(tee.A.X, tee.B.X) + 0.001).ToArray();
                Assert.IsTrue(dims.Length > 0, "DIM Ty should follow each actual T main run.");
                Assert.AreEqual(tee.Length, dims.Sum(d => d.A.DistanceTo(d.B)), 0.001);
                Assert.IsTrue(dims.All(d => Math.Abs(d.DimensionLinePoint.Value.Y -
                    (tee.A.Y + 180)) < 0.001 && d.Label == string.Empty));
            }
        }

        [TestMethod]
        public void TyDimensions_RespectOffAndMissingHangers()
        {
            var settings = new LayInCeilingSettings
            {
                MainDirection = LayInMainDirectionMode.Horizontal,
                DimensionMode = LayInDimensionMode.Off,
                DimensionHangers = true,
                DrawHangers = true
            };
            Assert.AreEqual(0, LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800), null, settings).DimensionRuns.Count);
            settings.DimensionMode = LayInDimensionMode.Overall;
            settings.DrawHangers = false;
            Assert.AreEqual(2, LayInCeilingPlanner.Build(
                Rect(0, 0, 6000, 4800), null, settings).DimensionRuns.Count);
        }

        [TestMethod]
        public void SampleTnDxf_610Grid_Dimensions9760Plus240OnBothAxes()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 10000, 10000), null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module610x610,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.ManualStart,
                    ManualStartPoint = new Point2(0, 0),
                    DimensionMode = LayInDimensionMode.GridAndEdges,
                    HorizontalDimDistance = 1190,
                    VerticalDimDistance = 1293
                });

            Assert.AreEqual("HNL_CF_610X610", plan.HatchPatternName);
            Assert.AreEqual(4, plan.DimensionRuns.Count);
            var horizontal = plan.DimensionRuns.Where(d => Math.Abs(d.A.Y - d.B.Y) < 0.001).ToList();
            var vertical = plan.DimensionRuns.Where(d => Math.Abs(d.A.X - d.B.X) < 0.001).ToList();
            CollectionAssert.AreEqual(new[] { 9760.0, 240.0 },
                horizontal.Select(d => d.A.DistanceTo(d.B)).ToArray());
            CollectionAssert.AreEqual(new[] { 9760.0, 240.0 },
                vertical.Select(d => d.A.DistanceTo(d.B)).ToArray());
            Assert.IsTrue(plan.DimensionRuns.All(d => d.Label == string.Empty),
                "Sample DIM uses the AutoCAD measured value, not a forced text override.");
            Assert.IsTrue(horizontal.All(d => d.DimensionLinePoint.HasValue &&
                Math.Abs(d.DimensionLinePoint.Value.Y + 1190) < 0.001));
            Assert.IsTrue(vertical.All(d => d.DimensionLinePoint.HasValue &&
                Math.Abs(d.DimensionLinePoint.Value.X + 1293) < 0.001));
        }

        [TestMethod]
        public void BalancedGridDims_TwoCutEdgesAreMeasuredSeparately()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 10000, 10000), null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module610x610,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.Balanced,
                    DimensionMode = LayInDimensionMode.GridAndEdges,
                    HorizontalDimSide = LayInHorizontalDimSide.Top,
                    VerticalDimSide = LayInVerticalDimSide.Right,
                    HorizontalDimDistance = 600,
                    VerticalDimDistance = 700
                });

            Assert.AreEqual(6, plan.DimensionRuns.Count);
            var horizontal = plan.DimensionRuns.Where(d => Math.Abs(d.A.Y - d.B.Y) < 0.001).ToList();
            var vertical = plan.DimensionRuns.Where(d => Math.Abs(d.A.X - d.B.X) < 0.001).ToList();
            CollectionAssert.AreEqual(new[] { 120.0, 9760.0, 120.0 },
                horizontal.Select(d => Math.Round(d.A.DistanceTo(d.B), 3)).ToArray());
            CollectionAssert.AreEqual(new[] { 120.0, 9760.0, 120.0 },
                vertical.Select(d => Math.Round(d.A.DistanceTo(d.B), 3)).ToArray());
            Assert.IsTrue(horizontal.All(d => Math.Abs(d.DimensionLinePoint.Value.Y - 10600) < 0.001));
            Assert.IsTrue(vertical.All(d => Math.Abs(d.DimensionLinePoint.Value.X - 10700) < 0.001));
        }

        [TestMethod]
        public void OptionalOverallDim_UsesOuterRowWithoutChangingExistingDimensionsOrTees()
        {
            var settings = new LayInCeilingSettings
            {
                GridSystem = LayInGridSystem.Module610x610,
                MainDirection = LayInMainDirectionMode.Horizontal,
                StartMode = LayInStartMode.Balanced,
                DimensionMode = LayInDimensionMode.GridAndEdges,
                HorizontalDimSide = LayInHorizontalDimSide.Top,
                VerticalDimSide = LayInVerticalDimSide.Right,
                HorizontalDimDistance = 600,
                VerticalDimDistance = 700,
                DimensionRowSpacing = 350
            };
            var original = LayInCeilingPlanner.Build(Rect(0, 0, 10000, 10000), null, settings);
            var addedSettings = settings.Clone();
            addedSettings.AddOverallDimension = true;
            var added = LayInCeilingPlanner.Build(Rect(0, 0, 10000, 10000), null, addedSettings);
            Assert.AreEqual(original.DimensionRuns.Count + 2, added.DimensionRuns.Count);
            Assert.AreEqual(original.TeeSegments.Count, added.TeeSegments.Count);
            Assert.AreEqual(original.HangerPoints.Count, added.HangerPoints.Count);
            for (var i = 0; i < original.DimensionRuns.Count; i++)
            {
                Assert.AreEqual(original.DimensionRuns[i].A.X, added.DimensionRuns[i].A.X, 0.001);
                Assert.AreEqual(original.DimensionRuns[i].B.Y, added.DimensionRuns[i].B.Y, 0.001);
                Assert.AreEqual(original.DimensionRuns[i].DimensionLinePoint.Value.X,
                    added.DimensionRuns[i].DimensionLinePoint.Value.X, 0.001);
                Assert.AreEqual(original.DimensionRuns[i].DimensionLinePoint.Value.Y,
                    added.DimensionRuns[i].DimensionLinePoint.Value.Y, 0.001);
            }
            var h = added.DimensionRuns.Single(d =>
                Math.Abs(d.A.Y - d.B.Y) < 0.001 && Math.Abs(d.A.DistanceTo(d.B) - 10000) < 0.001);
            var v = added.DimensionRuns.Single(d =>
                Math.Abs(d.A.X - d.B.X) < 0.001 && Math.Abs(d.A.DistanceTo(d.B) - 10000) < 0.001);
            Assert.AreEqual(10950, h.DimensionLinePoint.Value.Y, 0.001);
            Assert.AreEqual(11050, v.DimensionLinePoint.Value.X, 0.001);
            Assert.AreEqual(string.Empty, h.Label);
            Assert.AreEqual(string.Empty, v.Label);
        }

        [TestMethod]
        public void OverallMode_DoesNotDuplicateDimensionsWhenExtraOverallIsOn()
        {
            var settings = new LayInCeilingSettings
            {
                DimensionMode = LayInDimensionMode.Overall,
                AddOverallDimension = true
            };
            var plan = LayInCeilingPlanner.Build(Rect(0, 0, 6000, 4800), null, settings);
            Assert.AreEqual(2, plan.DimensionRuns.Count);
        }
        [TestMethod]
        public void SampleGridDims_AllFourSystemsMeasureFullWidthWithoutOverlap()
        {
            foreach (var item in new[]
            {
                Tuple.Create(LayInGridSystem.Module600x600, 9600.0, 400.0),
                Tuple.Create(LayInGridSystem.Module610x610, 9760.0, 240.0),
                Tuple.Create(LayInGridSystem.Module600x1200, 9600.0, 400.0),
                Tuple.Create(LayInGridSystem.Module610x1220, 9760.0, 240.0)
            })
            {
                var plan = LayInCeilingPlanner.Build(Rect(0, 0, 10000, 10000), null,
                    new LayInCeilingSettings
                    {
                        GridSystem = item.Item1,
                        MainDirection = LayInMainDirectionMode.Horizontal,
                        StartMode = LayInStartMode.ManualStart,
                        ManualStartPoint = new Point2(0, 0),
                        DimensionMode = LayInDimensionMode.GridAndEdges
                    });
                var x = plan.DimensionRuns.Where(d => Math.Abs(d.A.Y - d.B.Y) < 0.001).ToList();
                Assert.AreEqual(2, x.Count, item.Item1.ToString());
                Assert.AreEqual(item.Item2, x[0].A.DistanceTo(x[0].B), 0.001);
                Assert.AreEqual(item.Item3, x[1].A.DistanceTo(x[1].B), 0.001);
                Assert.AreEqual(10000, x.Sum(d => d.A.DistanceTo(d.B)), 0.001);
            }
        }

        [TestMethod]
        public void GridDims_RotatedRegionHasLocalOutsideDimLineAndOriginalTeeGeometry()
        {
            const double angle = Math.PI / 6.0;
            var boundary = new Boundary2(Rect(0, 0, 10000, 10000).Vertices
                .Select(p => Transform2.ToWorld(p, angle)));
            var settings = new LayInCeilingSettings
            {
                MainDirection = LayInMainDirectionMode.ParallelLongSide,
                GridSystem = LayInGridSystem.Module610x610,
                StartMode = LayInStartMode.ManualStart,
                ManualStartPoint = new Point2(0, 0),
                DimensionMode = LayInDimensionMode.GridAndEdges,
                HorizontalDimSide = LayInHorizontalDimSide.Bottom,
                VerticalDimSide = LayInVerticalDimSide.Left,
                HorizontalDimDistance = 700,
                VerticalDimDistance = 800
            };
            var result = LayInCeilingPlanner.Build(boundary, null, settings);
            Assert.IsTrue(result.DimensionRuns.Count >= 4);
            foreach (var d in result.DimensionRuns)
            {
                Assert.IsTrue(d.DimensionLinePoint.HasValue);
                var a = Transform2.ToLocal(d.A, result.MainAngleRadians);
                var b = Transform2.ToLocal(d.B, result.MainAngleRadians);
                var p = Transform2.ToLocal(d.DimensionLinePoint.Value, result.MainAngleRadians);
                if (Math.Abs(a.Y - b.Y) < 0.001)
                    Assert.IsTrue(p.Y < Math.Min(a.Y, b.Y) - 100);
                else
                    Assert.IsTrue(p.X < Math.Min(a.X, b.X) - 100);
            }
        }

        [TestMethod]
        public void SampleTnDxf_FirstTileBlockCoversExactlyOne610Panel()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 10000, 10000), null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module610x610,
                    MainDirection = LayInMainDirectionMode.Horizontal,
                    StartMode = LayInStartMode.ManualStart,
                    ManualStartPoint = new Point2(0, 0)
                });
            Assert.IsTrue(plan.FirstTileOrigin.HasValue);
            Assert.AreEqual(0.0, plan.FirstTileOrigin.Value.X, 0.001);
            Assert.AreEqual(0.0, plan.FirstTileOrigin.Value.Y, 0.001);
            Assert.AreEqual(plan.HatchOrigin.X, plan.FirstTileOrigin.Value.X, 0.001);
            Assert.AreEqual(610.0, plan.FirstTileWidth, 0.001);
            Assert.AreEqual(610.0, plan.FirstTileHeight, 0.001);
        }

        [TestMethod]
        public void FirstTileBlock_UsesEachPanelModuleDimensions()
        {
            foreach (var item in new[]
            {
                Tuple.Create(LayInGridSystem.Module600x600, 600.0, 600.0),
                Tuple.Create(LayInGridSystem.Module610x610, 610.0, 610.0),
                Tuple.Create(LayInGridSystem.Module600x1200, 600.0, 1200.0),
                Tuple.Create(LayInGridSystem.Module610x1220, 610.0, 1220.0)
            })
            {
                var plan = LayInCeilingPlanner.Build(
                    Rect(0, 0, 10000, 10000), null,
                    new LayInCeilingSettings
                    {
                        GridSystem = item.Item1,
                        MainDirection = LayInMainDirectionMode.Horizontal,
                        StartMode = LayInStartMode.ManualStart,
                        ManualStartPoint = new Point2(0, 0)
                    });
                Assert.IsTrue(plan.FirstTileOrigin.HasValue, item.Item1.ToString());
                Assert.AreEqual(item.Item2, plan.FirstTileWidth, 0.001);
                Assert.AreEqual(item.Item3, plan.FirstTileHeight, 0.001);
            }
        }

        [TestMethod]
        public void FirstTileBlock_SkipsHoleAndDoesNotDisturbHatchOrigin()
        {
            var settings = new LayInCeilingSettings
            {
                GridSystem = LayInGridSystem.Module610x610,
                MainDirection = LayInMainDirectionMode.Horizontal,
                StartMode = LayInStartMode.ManualStart,
                ManualStartPoint = new Point2(0, 0)
            };
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 3050, 3050),
                new[] { Rect(0, 0, 610, 610) }, settings);
            Assert.IsTrue(plan.FirstTileOrigin.HasValue);
            Assert.AreEqual(0.0, plan.HatchOrigin.X, 0.001);
            Assert.AreEqual(0.0, plan.HatchOrigin.Y, 0.001);
            var tile = plan.FirstTileOrigin.Value;
            Assert.IsFalse(tile.X < 610 && tile.Y < 610);
            Assert.IsTrue(tile.X >= 610 - 0.001 || tile.Y >= 610 - 0.001);
        }

        [TestMethod]
        public void FirstTileBlock_DoesNotPlaceOutsideTinyRegionOrWhenDisabled()
        {
            var settings = new LayInCeilingSettings
            {
                GridSystem = LayInGridSystem.Module610x610,
                MainDirection = LayInMainDirectionMode.Horizontal
            };
            var small = LayInCeilingPlanner.Build(Rect(0, 0, 400, 400), null, settings);
            Assert.IsFalse(small.FirstTileOrigin.HasValue);

            settings.DrawStartTileBlock = false;
            var regular = LayInCeilingPlanner.Build(Rect(0, 0, 5000, 5000), null, settings);
            Assert.IsFalse(regular.FirstTileOrigin.HasValue);
            Assert.IsTrue(regular.TeeSegments.Count > 0,
                "Switching off the marker must not switch off the physical tee layout.");
        }

        [TestMethod]
        public void AutoOptimize_ReturnsOnePrincipalAxis()
        {
            var plan = LayInCeilingPlanner.Build(
                Rect(0, 0, 9000, 3600),
                null,
                new LayInCeilingSettings
                {
                    GridSystem = LayInGridSystem.Module600x600,
                    MainDirection = LayInMainDirectionMode.AutoOptimize,
                    StartMode = LayInStartMode.Balanced
                });

            var horizontal = AngleDistance(plan.MainAngleRadians, 0.0);
            var vertical = AngleDistance(plan.MainAngleRadians, Math.PI * 0.5);
            Assert.IsTrue(Math.Min(horizontal, vertical) < 0.001);
        }

        private static Boundary2 Rect(double minX, double minY, double maxX, double maxY)
            => new Boundary2(new[]
            {
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY)
            });

        private static bool Inside(Point2 p, Boundary2 b)
        {
            var bounds = b.GetBounds();
            return p.X > bounds.Min.X + 0.001 && p.X < bounds.Max.X - 0.001 &&
                   p.Y > bounds.Min.Y + 0.001 && p.Y < bounds.Max.Y - 0.001;
        }

        private static void AssertAngleEquivalent(double expected, double actual)
        {
            Assert.IsTrue(AngleDistance(expected, actual) < 0.001,
                "Expected angle " + expected + ", actual " + actual);
        }

        private static double AngleDistance(double a, double b)
        {
            var d = Math.Abs(a - b) % Math.PI;
            return Math.Min(d, Math.PI - d);
        }
    }
}
