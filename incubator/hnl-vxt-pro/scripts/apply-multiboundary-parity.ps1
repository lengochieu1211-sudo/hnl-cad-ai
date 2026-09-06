param(
  [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
)

$ErrorActionPreference = 'Stop'
$root = Join-Path $RepoRoot 'incubator\hnl-vxt-pro'

function Write-Utf8 {
  param([string]$Path, [string]$Content)
  [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Replace-TextOnce {
  param([string]$Path, [string]$Old, [string]$New)
  $text = [System.IO.File]::ReadAllText($Path)
  $first = $text.IndexOf($Old, [System.StringComparison]::Ordinal)
  if ($first -lt 0) { throw "Patch token not found in $Path" }
  if ($text.IndexOf($Old, $first + $Old.Length, [System.StringComparison]::Ordinal) -ge 0) {
    throw "Patch token occurs more than once in $Path"
  }
  $text = $text.Substring(0, $first) + $New + $text.Substring($first + $Old.Length)
  Write-Utf8 -Path $Path -Content $text
}

function Replace-RegexOnce {
  param([string]$Path, [string]$Pattern, [string]$Replacement)
  $text = [System.IO.File]::ReadAllText($Path)
  $rx = [System.Text.RegularExpressions.Regex]::new(
    $Pattern,
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
  $matches = $rx.Matches($text)
  if ($matches.Count -ne 1) { throw "Regex patch expected one match in $Path, found $($matches.Count)" }
  $text = $rx.Replace($text, $Replacement, 1)
  Write-Utf8 -Path $Path -Content $text
}

$sessionPath = Join-Path $root 'src\HNL.VXT.AutoCAD\VxtSession.cs'
Write-Utf8 -Path $sessionPath -Content @'
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.AutoCAD
{
    internal sealed class VxtSession
    {
        public static VxtSession Current { get; } = new VxtSession();

        // V6.7.4 parity: one command can receive a selection set containing many
        // closed ceiling polylines. Each polygon remains an independent ceiling area.
        public List<Boundary2> Boundaries { get; } = new List<Boundary2>();

        // Backward-compatible first boundary for code paths that need one reference
        // point (for example a manual DIM pick). New Preview/Create paths use Boundaries.
        public Boundary2 Boundary
        {
            get => Boundaries.Count == 0 ? null : Boundaries[0];
            set
            {
                Boundaries.Clear();
                if (value != null) Boundaries.Add(value);
            }
        }

        public VxtSettings Settings { get; set; } = new VxtSettings();
        public VxtPaletteViewModel ViewModel { get; set; }

        public ObjectId[] GeneralEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] MainEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] FurringEquipmentIds { get; set; } = new ObjectId[0];

        // V6.7.4 ask_each: false = Trái/Dưới, true = Phải/Trên.
        public bool GlobalFurringFromFarEdge { get; set; }

        // Manual rectangle regions from the V6.7.4 "Quét HCN" workflow.
        public List<VxtLayoutRegion> Regions { get; } = new List<VxtLayoutRegion>();

        public bool HasBoundary => Boundaries.Count > 0;
    }
}
'@

$boundarySamplerPath = Join-Path $root 'src\HNL.VXT.AutoCAD\BoundarySampler.cs'
Write-Utf8 -Path $boundarySamplerPath -Content @'
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.AutoCAD
{
    internal static class BoundarySampler
    {
        public static Boundary2 FromPolyline(Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (!polyline.Closed) throw new ArgumentException("Polyline must be closed.", nameof(polyline));

            var points = new List<Point2>();
            var segments = polyline.NumberOfVertices;

            for (var i = 0; i < segments; i++)
            {
                var bulge = polyline.GetBulgeAt(i);
                var samples = Math.Abs(bulge) > 1e-9 ? 12 : 1;

                for (var j = 0; j < samples; j++)
                {
                    var parameter = i + (double)j / samples;
                    var p = polyline.GetPointAtParameter(parameter);
                    points.Add(new Point2(p.X, p.Y));
                }
            }

            return new Boundary2(points);
        }

        public static Boundary2 FromPolyline2d(Polyline2d polyline, Transaction tr)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (!polyline.Closed) throw new ArgumentException("Polyline2d must be closed.", nameof(polyline));

            var points = new List<Point2>();
            foreach (ObjectId vertexId in polyline)
            {
                var vertex = tr.GetObject(vertexId, OpenMode.ForRead, false) as Vertex2d;
                if (vertex == null) continue;
                points.Add(new Point2(vertex.Position.X, vertex.Position.Y));
            }
            return new Boundary2(points);
        }
    }
}
'@

$multiBuilderPath = Join-Path $root 'src\HNL.VXT.Core\Preview\VxtMultiBoundaryPlanBuilder.cs'
Write-Utf8 -Path $multiBuilderPath -Content @'
using System;
using System.Collections.Generic;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// V6.7.4 parity adapter: build every selected closed polyline independently,
    /// then merge only the resulting preview/create entities. This deliberately
    /// avoids a shared bounding box between disconnected ceiling areas.
    /// </summary>
    public static class VxtMultiBoundaryPlanBuilder
    {
        public static VxtPreviewPlan Build(
            IEnumerable<Boundary2> boundaries,
            VxtSettings settings,
            VxtLayoutContext context)
        {
            if (boundaries == null) throw new ArgumentNullException(nameof(boundaries));
            var merged = new VxtPreviewPlan();
            var builder = new VxtPreviewPlanBuilder();
            var count = 0;

            foreach (var boundary in boundaries)
            {
                if (boundary == null) continue;
                var part = builder.Build(boundary, settings, context);
                merged.Lines.AddRange(part.Lines);
                merged.Texts.AddRange(part.Texts);
                merged.HangerPoints.AddRange(part.HangerPoints);
                merged.Dimensions.AddRange(part.Dimensions);
                merged.MainSegmentCount += part.MainSegmentCount;
                merged.FurringSegmentCount += part.FurringSegmentCount;
                merged.HangerCount += part.HangerCount;
                merged.DimensionSegmentCount += part.DimensionSegmentCount;
                count++;
            }

            if (count == 0)
                throw new InvalidOperationException("Không có Polyline kín hợp lệ để rải xương.");
            return merged;
        }
    }
}
'@

$commandsPath = Join-Path $root 'src\HNL.VXT.AutoCAD\VxtCommands.cs'
$selectReplacement = @'
        [CommandMethod("VXTSELECTBOUNDARY", CommandFlags.Modal)]
        public void SelectBoundary()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nHNL Tool - VXT Pro: Quét chọn các Polyline kín làm biên trần: ",
                MessageForRemoval = "\nHNL Tool - VXT Pro: Bỏ Polyline khỏi tập chọn: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE")
            });
            var result = ed.GetSelection(options, filter);
            if (result.Status != PromptStatus.OK) return;

            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var accepted = new System.Collections.Generic.List<Boundary2>();
                var skippedOpen = 0;
                var skippedUnsupported = 0;
                foreach (var id in result.Value.GetObjectIds())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity is Polyline pl)
                    {
                        if (!pl.Closed) { skippedOpen++; continue; }
                        accepted.Add(BoundarySampler.FromPolyline(pl));
                    }
                    else if (entity is Polyline2d pl2)
                    {
                        if (!pl2.Closed) { skippedOpen++; continue; }
                        accepted.Add(BoundarySampler.FromPolyline2d(pl2, tr));
                    }
                    else
                    {
                        skippedUnsupported++;
                    }
                }

                if (accepted.Count == 0)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Không có Polyline kín hợp lệ trong tập chọn.");
                    return;
                }

                var session = VxtSession.Current;
                session.Boundaries.Clear();
                session.Boundaries.AddRange(accepted);
                session.Regions.Clear();
                session.GlobalFurringFromFarEdge = false;
                var skipped = skippedOpen + skippedUnsupported;
                session.ViewModel?.SetBoundaryStatus(
                    "✓ Đã chọn " + accepted.Count + " Polyline kín" +
                    (skipped > 0 ? " • Bỏ qua " + skipped + " đối tượng không hợp lệ" : string.Empty), true);
                tr.Commit();

                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã nhận " + accepted.Count +
                    " mảng trần độc lập" + (skipped > 0 ? "; bỏ qua " + skipped + " đối tượng." : "."));
            }
            VxtTransientPreview.Instance.Refresh();
        }

        [CommandMethod("VXTPICKDIRECTION"
'@
Replace-RegexOnce -Path $commandsPath -Pattern '        \[CommandMethod\("VXTSELECTBOUNDARY".*?        \[CommandMethod\("VXTPICKDIRECTION"' -Replacement $selectReplacement

$equipmentReplacement = @'
        private static void PickEquipment(EquipmentTarget target)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var label = target == EquipmentTarget.General ? "dùng chung" : target == EquipmentTarget.Main ? "cho Xương chính" : "cho Xương phụ";
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nHNL Tool - VXT Pro: Quét chọn đối tượng thiết bị " + label + " (Block/Polyline/Circle/Spline/Hatch/Line): ",
                MessageForRemoval = "\nHNL Tool - VXT Pro: Bỏ đối tượng khỏi tập chọn: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT,LWPOLYLINE,POLYLINE,CIRCLE,SPLINE,HATCH,LINE")
            });
            var result = ed.GetSelection(options, filter);
            if (result.Status != PromptStatus.OK)
            {
                VxtSession.Current.ViewModel?.SetEquipmentStatus(target, 0);
                return;
            }

            var ids = result.Value.GetObjectIds();
            switch (target)
            {
                case EquipmentTarget.General: VxtSession.Current.GeneralEquipmentIds = ids; break;
                case EquipmentTarget.Main: VxtSession.Current.MainEquipmentIds = ids; break;
                case EquipmentTarget.Furring: VxtSession.Current.FurringEquipmentIds = ids; break;
            }
            VxtSession.Current.ViewModel?.SetEquipmentStatus(target, ids.Length);
            ed.WriteMessage("\nHNL Tool - VXT Pro: Đã chọn " + ids.Length + " đối tượng thiết bị " + label + ".");
            VxtTransientPreview.Instance.Refresh();
        }

        private static void PickDimension
'@
Replace-RegexOnce -Path $commandsPath -Pattern '        private static void PickEquipment\(EquipmentTarget target\).*?        private static void PickDimension' -Replacement $equipmentReplacement

$oldDim = '            var localBoundary = new Boundary2(session.Boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)));'
$newDim = @'
            var localBoundary = session.Boundaries
                .Select(b => new Boundary2(b.Vertices.Select(p => Transform2.ToLocal(p, radians))))
                .OrderBy(b =>
                {
                    var bb = b.GetBounds();
                    var dx = localPick.X < bb.Min.X ? bb.Min.X - localPick.X : localPick.X > bb.Max.X ? localPick.X - bb.Max.X : 0.0;
                    var dy = localPick.Y < bb.Min.Y ? bb.Min.Y - localPick.Y : localPick.Y > bb.Max.Y ? localPick.Y - bb.Max.Y : 0.0;
                    return dx * dx + dy * dy;
                })
                .First();
'@
Replace-TextOnce -Path $commandsPath -Old $oldDim -New ($newDim.TrimEnd("`r", "`n"))

$createPath = Join-Path $root 'src\HNL.VXT.AutoCAD\VxtCreateEngine.cs'
Replace-TextOnce -Path $createPath `
  -Old '                    var plan = new VxtPreviewPlanBuilder().Build(session.Boundary, settings, context);' `
  -New '                    var plan = VxtMultiBoundaryPlanBuilder.Build(session.Boundaries, settings, context);'

$previewPath = Join-Path $root 'src\HNL.VXT.AutoCAD\VxtTransientPreview.cs'
Replace-TextOnce -Path $previewPath `
  -Old '                    plan = new VxtPreviewPlanBuilder().Build(session.Boundary, settings, context);' `
  -New '                    plan = VxtMultiBoundaryPlanBuilder.Build(session.Boundaries, settings, context);'

Write-Host 'HNL VXT multi-polyline parity patch applied.'
Write-Host '  - Multiple closed ceiling polylines per selection set'
Write-Host '  - Independent plan per ceiling polygon, merged only after layout'
Write-Host '  - Preview/Create both consume the same multi-boundary plan'
Write-Host '  - Equipment selection restored to Block/Polyline/Circle/Spline/Hatch/Line'
Write-Host '  - Manual DIM pick resolves against the nearest selected ceiling polygon'
