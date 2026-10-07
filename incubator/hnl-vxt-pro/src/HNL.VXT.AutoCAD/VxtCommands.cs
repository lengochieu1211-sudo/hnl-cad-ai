using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.AutoCAD
{
    public sealed class VxtCommands
    {
        [CommandMethod("VXT", CommandFlags.Modal)]
        public void ShowPalette() => VxtPaletteService.Show();

        [CommandMethod("HNLVXTFOCUSBOUNDARY", CommandFlags.Modal)]
        public void FocusBoundaryDiagnostic()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var session = VxtSession.Current;
            var index = session.PendingBoundaryHighlightIndex;
            session.PendingBoundaryHighlightIndex = -1;

            if (index < 0 || index >= session.BoundaryIds.Count)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Không tìm thấy mảng Mxx cần highlight.");
                return;
            }

            var id = session.BoundaryIds[index];
            if (id.IsNull)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: M" +
                    (index + 1).ToString("00") +
                    " được tạo bằng Chọn điểm nên không có Polyline nguồn để highlight.");
                return;
            }
            if (!id.IsValid || id.IsErased)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Polyline của M" +
                    (index + 1).ToString("00") + " không còn hợp lệ trong DWG.");
                return;
            }

            string handle = string.Empty;
            try
            {
                using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity != null) handle = entity.Handle.ToString();
                    tr.Commit();
                }

                doc.Editor.SetImpliedSelection(new ObjectId[0]);
                doc.Editor.SetImpliedSelection(new[] { id });
                TryHighlight(doc, id, true);
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: M" +
                    (index + 1).ToString("00") + " -> Polyline Handle " +
                    (string.IsNullOrWhiteSpace(handle) ? "N/A" : handle) + ".");
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Không highlight được M" +
                    (index + 1).ToString("00") + ": " + ex.Message);
            }
        }

        [CommandMethod("VXTCREATE", CommandFlags.Modal)]
        public void Create()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var session = VxtSession.Current;
            if (!session.HasBoundary)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Chưa chọn biên trần.");
                return;
            }

            // Manual rectangle mode already records XP start side for every region exactly
            // when the rectangle is defined, like V6.7.4. Do not ask it a second time here.
            if (session.Settings.AskDirectionEachRegion &&
                session.Settings.MainDirection != MainDirectionMode.RectangleRegions)
            {
                if (!PromptFurringStartSides(doc.Editor, session)) return;
                VxtTransientPreview.Instance.Refresh();
            }

            VxtCreateEngine.Execute();
        }

        [CommandMethod("VXTSELECTBOUNDARY", CommandFlags.Modal)]
        public void SelectBoundary()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nHNL Tool - VXT Pro: Quét chọn Polyline biên trần (kín hoặc hở ≤ 1 mm): ",
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
                var accepted = new List<Boundary2>();
                var acceptedIds = new List<ObjectId>();
                var autoClosed = 0;
                var maxAutoCloseGap = 0.0;
                var tinyZNormalized = 0;
                var skippedOpen = 0;
                var skippedZ = 0;
                var skippedUnsupported = 0;

                foreach (var id in result.Value.GetObjectIds())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    Boundary2 boundary;
                    BoundarySampleInfo info;
                    var acceptedBoundary = false;

                    if (entity is Polyline pl)
                        acceptedBoundary = BoundarySampler.TryFromPolyline(pl, out boundary, out info);
                    else if (entity is Polyline2d pl2)
                        acceptedBoundary = BoundarySampler.TryFromPolyline2d(pl2, tr, out boundary, out info);
                    else
                    {
                        skippedUnsupported++;
                        continue;
                    }

                    if (!acceptedBoundary)
                    {
                        if (string.Equals(info.RejectionReason, "OpenGap", StringComparison.Ordinal))
                            skippedOpen++;
                        else if (string.Equals(info.RejectionReason, "Z", StringComparison.Ordinal))
                            skippedZ++;
                        else
                            skippedUnsupported++;
                        continue;
                    }

                    accepted.Add(boundary);
                    acceptedIds.Add(id);
                    if (info.AutoClosed)
                    {
                        autoClosed++;
                        maxAutoCloseGap = Math.Max(maxAutoCloseGap, info.ClosureGap);
                    }
                    if (info.TinyZNormalized)
                        tinyZNormalized++;
                }

                if (accepted.Count == 0)
                {
                    ed.WriteMessage(
                        "\nHNL Tool - VXT Pro: Không có biên trần hợp lệ. " +
                        "Cho phép khe hở đầu-cuối ≤ 1 mm và |Z| ≤ 0.01 mm.");
                    return;
                }

                var session = VxtSession.Current;
                session.Boundaries.Clear();
                session.Boundaries.AddRange(accepted);
                session.BoundaryIds.Clear();
                session.BoundaryIds.AddRange(acceptedIds);
                session.Regions.Clear();
                session.BoundaryRegionGroups.Clear();
                session.BoundaryHoleGroups.Clear();
                session.GlobalFurringFromFarEdge = false;
                var skipped = skippedOpen + skippedZ + skippedUnsupported;
                session.ViewModel?.SetBoundaryStatus(
                    "✓ Đã chọn " + accepted.Count + " biên trần" +
                    (autoClosed > 0 ? " • Tự khép " + autoClosed : string.Empty) +
                    (tinyZNormalized > 0 ? " • Chuẩn Z≈0 " + tinyZNormalized : string.Empty) +
                    (skipped > 0 ? " • Bỏ qua " + skipped : string.Empty), true);
                tr.Commit();

                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã nhận " + accepted.Count + " mảng trần độc lập");
                if (autoClosed > 0)
                    ed.WriteMessage("; tự khép " + autoClosed + " biên, khe lớn nhất " +
                        maxAutoCloseGap.ToString("0.###") + " mm");
                if (tinyZNormalized > 0)
                    ed.WriteMessage("; chuẩn hóa Z≈0 cho " + tinyZNormalized + " biên");
                if (skippedOpen > 0)
                    ed.WriteMessage("; bỏ " + skippedOpen + " biên hở > 1 mm");
                if (skippedZ > 0)
                    ed.WriteMessage("; bỏ " + skippedZ + " biên có |Z| > 0.01 mm");
                if (skippedUnsupported > 0)
                    ed.WriteMessage("; bỏ " + skippedUnsupported + " đối tượng không hỗ trợ");
                ed.WriteMessage(".");
            }

            var mode = VxtSession.Current.Settings.MainDirection;
            if (mode == MainDirectionMode.TwoPoints || mode == MainDirectionMode.RectangleRegions)
                VxtTransientPreview.Instance.Clear();
            else
                VxtTransientPreview.Instance.Refresh();
        }

        [CommandMethod("VXTPICKBOUNDARYPOINT", CommandFlags.Modal)]
        public void PickBoundaryPoint()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var accepted = new List<Boundary2>();
            var acceptedHoleGroups = new List<List<Boundary2>>();

            while (true)
            {
                var options = new PromptPointOptions(
                    "\nHNL Tool - VXT Pro: Chọn điểm trong vùng trần [Enter để xong]: ")
                {
                    AllowNone = true
                };
                var result = ed.GetPoint(options);
                if (result.Status == PromptStatus.None || result.Status == PromptStatus.Cancel)
                    break;
                if (result.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Dừng Chọn điểm.");
                    break;
                }

                DBObjectCollection traced = null;
                try
                {
                    // detectIslands=true is required for ring-shaped ceiling regions.
                    // The picked seed remains in UCS per AutoCAD API contract; sampled loop
                    // geometry is classified below in WCS before entering the pure Core.
                    traced = ed.TraceBoundary(result.Value, true);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Không tạo được biên tại điểm này: " + ex.Message);
                    continue;
                }

                if (traced == null || traced.Count == 0)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Điểm này không nằm trong vùng kín hợp lệ.");
                    continue;
                }

                var loops = new List<Boundary2>();
                try
                {
                    foreach (DBObject item in traced)
                    {
                        var polyline = item as Polyline;
                        if (polyline == null) continue;

                        BoundarySampleInfo info;
                        Boundary2 sampled;
                        if (BoundarySampler.TryFromPolyline(polyline, out sampled, out info))
                            loops.Add(sampled);
                    }
                }
                finally
                {
                    foreach (DBObject item in traced)
                        item?.Dispose();
                }

                if (loops.Count == 0)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: TraceBoundary không trả về Polyline biên hợp lệ.");
                    continue;
                }

                var seedWorld3d = result.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                var seedWorld = new Point2(seedWorld3d.X, seedWorld3d.Y);

                // The selected region is bounded by the smallest traced loop containing the seed.
                // Larger containing loops are ancestors; loops immediately inside the selected outer
                // loop and not containing the seed are holes of this picked ceiling region.
                var boundary = loops
                    .Where(loop => BoundaryContainsPoint(loop, seedWorld))
                    .OrderBy(loop => Math.Abs(PolygonArea(loop.Vertices)))
                    .FirstOrDefault();

                if (boundary == null)
                {
                    boundary = loops
                        .OrderByDescending(loop => Math.Abs(PolygonArea(loop.Vertices)))
                        .First();
                }

                var nested = loops
                    .Where(loop => !ReferenceEquals(loop, boundary) &&
                                   BoundaryInside(loop, boundary) &&
                                   !BoundaryContainsPoint(loop, seedWorld))
                    .ToList();

                var holes = nested
                    .Where(candidate => !nested.Any(parent =>
                        !ReferenceEquals(parent, candidate) &&
                        Math.Abs(PolygonArea(parent.Vertices)) > Math.Abs(PolygonArea(candidate.Vertices)) &&
                        BoundaryInside(candidate, parent)))
                    .ToList();

                if (ContainsEquivalentBoundary(accepted, boundary))
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Vùng này đã được Chọn điểm trước đó; bỏ qua trùng.");
                    continue;
                }

                accepted.Add(boundary);
                acceptedHoleGroups.Add(holes);
                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã nhận M" +
                    accepted.Count.ToString("00") +
                    " bằng Chọn điểm" +
                    (holes.Count > 0 ? " • " + holes.Count + " lỗ" : string.Empty) +
                    ". Chọn vùng khác hoặc Enter để xong.");
            }

            if (accepted.Count == 0) return;

            var session = VxtSession.Current;
            session.Boundaries.Clear();
            session.Boundaries.AddRange(accepted);
            session.BoundaryIds.Clear();
            for (var i = 0; i < accepted.Count; i++)
                session.BoundaryIds.Add(ObjectId.Null);

            session.BoundaryHoleGroups.Clear();
            foreach (var holes in acceptedHoleGroups)
                session.BoundaryHoleGroups.Add(holes ?? new List<Boundary2>());

            session.Regions.Clear();
            session.BoundaryRegionGroups.Clear();
            session.BoundaryFurringFromFarEdges.Clear();
            session.GlobalFurringFromFarEdge = false;

            var totalHoles = acceptedHoleGroups.Sum(x => x?.Count ?? 0);
            session.ViewModel?.SetBoundaryStatus(
                "✓ Chọn điểm " + accepted.Count + " mảng trần" +
                (totalHoles > 0 ? " • " + totalHoles + " lỗ" : string.Empty), true);

            ed.WriteMessage("\nHNL Tool - VXT Pro: Đã nhận " +
                accepted.Count + " mảng trần bằng Chọn điểm" +
                (totalHoles > 0 ? ", có " + totalHoles + " lỗ trong." : "."));
            VxtTransientPreview.Instance.Refresh();
        }

        private static bool BoundaryInside(Boundary2 candidate, Boundary2 container)
        {
            if (candidate == null || container == null || candidate.Vertices.Count == 0) return false;
            return BoundaryContainsPoint(container, candidate.Vertices[0]);
        }

        private static bool BoundaryContainsPoint(Boundary2 boundary, Point2 point)
        {
            if (boundary == null || boundary.Vertices.Count < 3) return false;
            const double tol = 0.1;
            var inside = false;
            for (var i = 0; i < boundary.Vertices.Count; i++)
            {
                var a = boundary.Vertices[i];
                var b = boundary.Vertices[(i + 1) % boundary.Vertices.Count];

                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var len2 = dx * dx + dy * dy;
                if (len2 > 1e-12)
                {
                    var t = ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / len2;
                    t = Math.Max(0.0, Math.Min(1.0, t));
                    var projected = new Point2(a.X + t * dx, a.Y + t * dy);
                    if (projected.DistanceTo(point) <= tol) return true;
                }

                var crosses = (a.Y > point.Y) != (b.Y > point.Y);
                if (!crosses) continue;
                var x = a.X + (point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (x > point.X) inside = !inside;
            }
            return inside;
        }

        private static bool ContainsEquivalentBoundary(
            IEnumerable<Boundary2> existing,
            Boundary2 candidate)
        {
            if (candidate == null) return false;
            var candidateBounds = candidate.GetBounds();
            var candidateArea = Math.Abs(PolygonArea(candidate.Vertices));

            foreach (var current in existing)
            {
                if (current == null) continue;
                var bounds = current.GetBounds();
                if (Math.Abs(bounds.Min.X - candidateBounds.Min.X) > 1e-4 ||
                    Math.Abs(bounds.Min.Y - candidateBounds.Min.Y) > 1e-4 ||
                    Math.Abs(bounds.Max.X - candidateBounds.Max.X) > 1e-4 ||
                    Math.Abs(bounds.Max.Y - candidateBounds.Max.Y) > 1e-4)
                    continue;

                var area = Math.Abs(PolygonArea(current.Vertices));
                var tolerance = Math.Max(1e-3, Math.Max(area, candidateArea) * 1e-8);
                if (Math.Abs(area - candidateArea) <= tolerance)
                    return true;
            }

            return false;
        }

        private static double PolygonArea(IReadOnlyList<Point2> vertices)
        {
            if (vertices == null || vertices.Count < 3) return 0.0;
            var twiceArea = 0.0;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Count];
                twiceArea += a.X * b.Y - b.X * a.Y;
            }
            return twiceArea * 0.5;
        }

        [CommandMethod("VXTPICKDIRECTION", CommandFlags.Modal)]
        public void PickDirection()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var pathPoints = PromptMainDirectionPath(doc);
            if (pathPoints == null || pathPoints.Count < 2)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Chưa xác định hướng; giữ thiết lập trước đó.");
                return;
            }

            var session = VxtSession.Current;
            var firstAngle = NormalizeAngle(
                Math.Atan2(
                    pathPoints[1].Y - pathPoints[0].Y,
                    pathPoints[1].X - pathPoints[0].X) * 180.0 / Math.PI);

            if (pathPoints.Count == 2)
            {
                // Exact backward-compatible TwoPoints path. No region partition is introduced.
                session.Settings.MainDirection = MainDirectionMode.TwoPoints;
                session.Settings.DirectionDegrees = firstAngle;
                session.Regions.Clear();
                session.BoundaryRegionGroups.Clear();
                session.ViewModel?.SetDirection(firstAngle, MainDirectionMode.TwoPoints);
                VxtTransientPreview.Instance.Refresh();
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Chọn hướng = 1 đoạn, góc " +
                    firstAngle.ToString("0.###") + "°.");
                return;
            }

            if (!session.HasBoundary)
            {
                doc.Editor.WriteMessage(
                    "\nHNL Tool - VXT Pro: Tuyến gấp khúc cần có biên trần trước. " +
                    "Hãy chọn Polyline biên trần rồi Thiết lập hướng lại.");
                return;
            }

            if (session.Boundaries.Count != 1)
            {
                doc.Editor.WriteMessage(
                    "\nHNL Tool - VXT Pro: Chọn hướng bằng tuyến gấp khúc hiện thiết lập theo từng mảng. " +
                    "Hãy chọn 1 Polyline biên trần; chế độ Ngang/Dọc/2 điểm/HCN/Tự động vẫn hỗ trợ nhiều Polyline như cũ.");
                return;
            }

            var groups = new List<List<VxtLayoutRegion>>();
            try
            {
                for (var boundaryIndex = 0; boundaryIndex < session.Boundaries.Count; boundaryIndex++)
                {
                    var partitioned = VxtPolylineDirectionPartitioner.Partition(
                        session.Boundaries[boundaryIndex],
                        pathPoints,
                        session.GlobalFurringFromFarEdge).ToList();

                    if (partitioned.Count != pathPoints.Count - 1)
                        throw new InvalidOperationException(
                            "Số vùng hướng không khớp số đoạn của tuyến.");

                    if (session.Settings.AskDirectionEachRegion &&
                        (session.Settings.DrawFurring ||
                         (session.Settings.AutoDimension && session.Settings.DimFurring)))
                    {
                        for (var regionIndex = 0; regionIndex < partitioned.Count; regionIndex++)
                        {
                            bool fromFar;
                            if (!PromptFurringStartSide(
                                doc.Editor,
                                partitioned[regionIndex].MainAngleDegrees,
                                "vùng tuyến " + (regionIndex + 1) +
                                " / M" + (boundaryIndex + 1).ToString("00"),
                                out fromFar))
                                return;
                            partitioned[regionIndex].FurringFromFarEdge = fromFar;
                        }
                    }

                    groups.Add(partitioned);
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage(
                    "\nHNL Tool - VXT Pro: Không thể chia mảng theo tuyến gấp khúc: " + ex.Message);
                return;
            }

            // Commit only after every selected ceiling boundary partitions successfully.
            session.Settings.MainDirection = MainDirectionMode.PolylinePath;
            session.Settings.DirectionDegrees = firstAngle;
            session.Regions.Clear();
            session.BoundaryRegionGroups.Clear();
            foreach (var group in groups)
            {
                session.BoundaryRegionGroups.Add(group);
                session.Regions.AddRange(group);
            }
            session.BoundaryFurringFromFarEdges.Clear();

            session.ViewModel?.SetDirection(firstAngle, MainDirectionMode.PolylinePath);
            VxtTransientPreview.Instance.Refresh();
            doc.Editor.WriteMessage(
                "\nHNL Tool - VXT Pro: Đã nhận tuyến gấp khúc " +
                (pathPoints.Count - 1) + " đoạn; tự chia " +
                session.Regions.Count + " vùng hướng XC.");
        }

        [CommandMethod("VXTRECTDIRECTION", CommandFlags.Modal)]
        public void RectangleDirectionMode()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var session = VxtSession.Current;
            if (!session.HasBoundary)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Hãy chọn biên trần trước khi chia vùng.");
                return;
            }

            session.Settings.MainDirection = MainDirectionMode.RectangleRegions;
            session.Regions.Clear();
            session.BoundaryRegionGroups.Clear();
            VxtTransientPreview.Instance.Clear();

            ed.WriteMessage("\nHNL Tool - VXT Pro: Chia vùng HCN theo từng Polyline, giống VXT Lisp. Enter tại góc 1 để kết thúc mảng hiện tại.");

            for (var boundaryIndex = 0; boundaryIndex < session.Boundaries.Count; boundaryIndex++)
            {
                var boundary = session.Boundaries[boundaryIndex];
                var group = new List<VxtLayoutRegion>();
                var regionNumber = 1;
                var boundaryId = boundaryIndex < session.BoundaryIds.Count
                    ? session.BoundaryIds[boundaryIndex]
                    : ObjectId.Null;

                TryHighlight(doc, boundaryId, true);
                try
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Mảng trần " + (boundaryIndex + 1) + "/" +
                        session.Boundaries.Count + " đang được thiết lập.");

                    while (true)
                    {
                        var firstOptions = new PromptPointOptions(
                            "\nHNL Tool - -> Chọn góc 1 của HCN quét mảng " + regionNumber +
                            " (Enter = xong mảng hiện tại): ")
                        {
                            AllowNone = true
                        };
                        var first = ed.GetPoint(firstOptions);
                        if (first.Status == PromptStatus.None) break;
                        if (first.Status != PromptStatus.OK) return;

                        var cornerOptions = new PromptCornerOptions("\nHNL Tool - -> Chọn góc 2: ", first.Value);
                        var second = ed.GetCorner(cornerOptions);
                        if (second.Status != PromptStatus.OK)
                        {
                            ed.WriteMessage("\nHNL Tool - VXT Pro: Không chọn được góc 2. Thử lại vùng hiện tại.");
                            continue;
                        }

                        var minX = Math.Min(first.Value.X, second.Value.X);
                        var minY = Math.Min(first.Value.Y, second.Value.Y);
                        var maxX = Math.Max(first.Value.X, second.Value.X);
                        var maxY = Math.Max(first.Value.Y, second.Value.Y);
                        if (maxX - minX < 1e-6 || maxY - minY < 1e-6)
                        {
                            ed.WriteMessage("\nHNL Tool - VXT Pro: Vùng quá nhỏ, bỏ qua.");
                            continue;
                        }

                        // V6.7.4 Manual_Split offers exactly Ngang/Doc for each rectangle.
                        var direction = new PromptKeywordOptions(
                            "\nHNL Tool - -> Hướng rải Xương chính cho mảng " + regionNumber +
                            " [Ngang/Doc] <Ngang>: ");
                        direction.Keywords.Add("Ngang");
                        direction.Keywords.Add("Doc");
                        direction.Keywords.Default = "Ngang";
                        var directionResult = ed.GetKeywords(direction);
                        if (directionResult.Status != PromptStatus.OK && directionResult.Status != PromptStatus.None) return;
                        var key = directionResult.Status == PromptStatus.None ? "Ngang" : directionResult.StringResult;
                        var angle = key == "Doc" ? 90.0 : 0.0;

                        var region = new VxtLayoutRegion(new Box2(minX, minY, maxX, maxY), angle);

                        // In V6.7.4 Manual_Split, XP start side is asked immediately for every HCN,
                        // regardless of the global ask_each toggle. Store it now and never ask twice.
                        bool fromFar;
                        if (!PromptFurringStartSide(ed, angle,
                            "mảng " + regionNumber + " / Polyline " + (boundaryIndex + 1), out fromFar)) return;
                        region.FurringFromFarEdge = fromFar;

                        group.Add(region);
                        session.Regions.Add(region);
                        ed.WriteMessage("\nHNL Tool - VXT Pro: Đã nhận HCN " + regionNumber +
                            " cho Polyline " + (boundaryIndex + 1) + ".");
                        regionNumber++;
                    }
                }
                finally
                {
                    TryHighlight(doc, boundaryId, false);
                }

                if (group.Count == 0)
                {
                    // Safe legacy-style fallback: still process the selected ceiling instead of
                    // silently dropping it when Enter is pressed before defining a rectangle.
                    var bounds = boundary.GetBounds();
                    var fallback = new VxtLayoutRegion(bounds, 0.0, false);
                    group.Add(fallback);
                    session.Regions.Add(fallback);
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Polyline " + (boundaryIndex + 1) +
                        " chưa có HCN; dùng toàn bộ biên với hướng Ngang.");
                }

                session.BoundaryRegionGroups.Add(group);
            }

            VxtTransientPreview.Instance.Refresh();
            ed.WriteMessage("\nHNL Tool - VXT Pro: Đã thiết lập " + session.Regions.Count +
                " HCN trên " + session.BoundaryRegionGroups.Count +
                " Polyline. Preview và Tạo thật xử lý từng Polyline độc lập.");
        }

        [CommandMethod("VXTPICKMAINBLOCK", CommandFlags.Modal)]
        public void PickMainBlock() => PickBlock(BlockTarget.Main);
        [CommandMethod("VXTPICKFURRINGBLOCK", CommandFlags.Modal)]
        public void PickFurringBlock() => PickBlock(BlockTarget.Furring);
        [CommandMethod("VXTPICKHANGERBLOCK", CommandFlags.Modal)]
        public void PickHangerBlock() => PickBlock(BlockTarget.Hanger);

        [CommandMethod("VXTPICKEQUIPGENERAL", CommandFlags.Modal)]
        public void PickGeneralEquipment() => PickEquipment(EquipmentTarget.General);
        [CommandMethod("VXTPICKEQUIPMAIN", CommandFlags.Modal)]
        public void PickMainEquipment() => PickEquipment(EquipmentTarget.Main);
        [CommandMethod("VXTPICKEQUIPFURRING", CommandFlags.Modal)]
        public void PickFurringEquipment() => PickEquipment(EquipmentTarget.Furring);

        [CommandMethod("VXTPICKDIMMAIN", CommandFlags.Modal)]
        public void PickMainDim() => PickDimension(DimensionTarget.Main);
        [CommandMethod("VXTPICKDIMFURRING", CommandFlags.Modal)]
        public void PickFurringDim() => PickDimension(DimensionTarget.Furring);
        [CommandMethod("VXTPICKDIMHANGER", CommandFlags.Modal)]
        public void PickHangerDim() => PickDimension(DimensionTarget.Hanger);

        private static bool PromptFurringStartSides(Editor ed, VxtSession session)
        {
            if (session.Regions.Count == 0)
            {
                bool far;
                var angle = ResolveCurrentMainAngle(session.Settings);
                if (!PromptFurringStartSide(ed, angle, "biên trần", out far)) return false;
                session.GlobalFurringFromFarEdge = far;
                return true;
            }

            for (var i = 0; i < session.Regions.Count; i++)
            {
                bool far;
                var region = session.Regions[i];
                if (!PromptFurringStartSide(ed, region.MainAngleDegrees, "vùng " + (i + 1), out far)) return false;
                region.FurringFromFarEdge = far;
            }
            return true;
        }

        private static bool PromptFurringStartSide(Editor ed, double mainAngleDegrees, string label, out bool fromFarEdge)
        {
            fromFarEdge = false;
            var normalized = NormalizeAngle(mainAngleDegrees);
            var verticalLike = IsVerticalLike(normalized);

            PromptKeywordOptions options;
            if (verticalLike)
            {
                options = new PromptKeywordOptions("\nHNL Tool - Chọn hướng rải Xương phụ cho " + label + " [Duoi/Tren] <Duoi>: ");
                options.Keywords.Add("Duoi");
                options.Keywords.Add("Tren");
                options.Keywords.Default = "Duoi";
            }
            else
            {
                options = new PromptKeywordOptions("\nHNL Tool - Chọn hướng rải Xương phụ cho " + label + " [Trai/Phai] <Trai>: ");
                options.Keywords.Add("Trai");
                options.Keywords.Add("Phai");
                options.Keywords.Default = "Trai";
            }

            var result = ed.GetKeywords(options);
            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None) return false;
            var value = result.Status == PromptStatus.None ? options.Keywords.Default : result.StringResult;
            fromFarEdge = value == "Phai" || value == "Tren";
            return true;
        }

        private static double ResolveCurrentMainAngle(VxtSettings settings)
        {
            if (settings.MainDirection == MainDirectionMode.Vertical) return 90.0;
            if (settings.MainDirection == MainDirectionMode.TwoPoints ||
                settings.MainDirection == MainDirectionMode.PolylinePath ||
                settings.MainDirection == MainDirectionMode.RectangleRegions)
                return settings.DirectionDegrees;
            return 0.0;
        }

        private static bool IsVerticalLike(double angleDegrees)
        {
            var radians = angleDegrees * Math.PI / 180.0;
            return Math.Abs(Math.Sin(radians)) > Math.Abs(Math.Cos(radians));
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 180.0;
            return angle < 0.0 ? angle + 180.0 : angle;
        }

        private static List<Point2> PromptMainDirectionPath(Document doc)
        {
            return ReadDirectionPoints(doc.Editor);
        }

        private static List<Point2> ReadDirectionPoints(Editor ed)
        {
            var first = ed.GetPoint(
                "\nHNL Tool - VXT Pro: Chọn điểm 1 của hướng Xương chính: ");
            if (first.Status != PromptStatus.OK) return null;

            var secondOptions = new PromptPointOptions(
                "\nHNL Tool - VXT Pro: Chọn điểm 2: ")
            {
                BasePoint = first.Value,
                UseBasePoint = true
            };
            var second = ed.GetPoint(secondOptions);
            if (second.Status != PromptStatus.OK) return null;

            var ucsToWcs = ed.CurrentUserCoordinateSystem;
            var firstWorld = first.Value.TransformBy(ucsToWcs);
            var secondWorld = second.Value.TransformBy(ucsToWcs);
            var points = new List<Point2>
            {
                new Point2(firstWorld.X, firstWorld.Y),
                new Point2(secondWorld.X, secondWorld.Y)
            };

            var lastUcsPoint = second.Value;
            while (true)
            {
                var nextOptions = new PromptPointOptions(
                    "\nHNL Tool - VXT Pro: Chọn điểm tiếp theo hoặc Enter để kết thúc hướng: ")
                {
                    AllowNone = true,
                    BasePoint = lastUcsPoint,
                    UseBasePoint = true
                };
                var next = ed.GetPoint(nextOptions);
                if (next.Status == PromptStatus.None) break;
                if (next.Status != PromptStatus.OK) return null;
                var nextWorld = next.Value.TransformBy(ucsToWcs);
                points.Add(new Point2(nextWorld.X, nextWorld.Y));
                lastUcsPoint = next.Value;
            }

            return points;
        }

        private static List<Point2> ReadDirectionEntity(Document doc)
        {
            var ed = doc.Editor;
            var options = new PromptEntityOptions(
                "\nHNL Tool - VXT Pro: Chọn Line hoặc Polyline mở làm tuyến hướng: ");
            options.SetRejectMessage(
                "\nHNL Tool - VXT Pro: Chỉ nhận Line hoặc Polyline 2D mở.");
            options.AddAllowedClass(typeof(Line), true);
            options.AddAllowedClass(typeof(Polyline), true);

            var selected = ed.GetEntity(options);
            if (selected.Status != PromptStatus.OK) return null;

            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                var entity = tr.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Entity;
                if (entity is Line line)
                {
                    return new List<Point2>
                    {
                        new Point2(line.StartPoint.X, line.StartPoint.Y),
                        new Point2(line.EndPoint.X, line.EndPoint.Y)
                    };
                }

                var polyline = entity as Polyline;
                if (polyline == null || polyline.NumberOfVertices < 2)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Đường hướng không hợp lệ.");
                    return null;
                }

                if (polyline.Closed)
                {
                    ed.WriteMessage(
                        "\nHNL Tool - VXT Pro: Tuyến hướng phải là Polyline mở; không dùng Polyline kín.");
                    return null;
                }

                for (var i = 0; i + 1 < polyline.NumberOfVertices; i++)
                {
                    if (Math.Abs(polyline.GetBulgeAt(i)) > 1e-9)
                    {
                        ed.WriteMessage(
                            "\nHNL Tool - VXT Pro: Tuyến hướng hiện chỉ nhận các đoạn thẳng; " +
                            "Polyline có cung/bulge chưa được dùng làm tuyến hướng.");
                        return null;
                    }
                }

                var points = new List<Point2>(polyline.NumberOfVertices);
                for (var i = 0; i < polyline.NumberOfVertices; i++)
                {
                    var point = polyline.GetPoint3dAt(i);
                    points.Add(new Point2(point.X, point.Y));
                }
                return points;
            }
        }

        private static double? PromptAngleByTwoPoints(Editor ed, string label)
        {
            var p1 = ed.GetPoint("\nHNL Tool - VXT Pro: Chọn điểm thứ 1 xác định " + label + ": ");
            if (p1.Status != PromptStatus.OK) return null;
            var p2opt = new PromptPointOptions("\nHNL Tool - VXT Pro: Chọn điểm thứ 2 xác định " + label + ": ")
            {
                BasePoint = p1.Value,
                UseBasePoint = true
            };
            var p2 = ed.GetPoint(p2opt);
            if (p2.Status != PromptStatus.OK) return null;
            var dx = p2.Value.X - p1.Value.X;
            var dy = p2.Value.Y - p1.Value.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < 1e-8)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Hai điểm quá gần nhau.");
                return null;
            }
            return Math.Atan2(dy, dx) * 180.0 / Math.PI;
        }

        private static void TryHighlight(Document doc, ObjectId id, bool highlight)
        {
            if (doc == null || id.IsNull || id.IsErased || !id.IsValid) return;
            try
            {
                using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity != null)
                    {
                        if (highlight) entity.Highlight();
                        else entity.Unhighlight();
                    }
                    tr.Commit();
                }
            }
            catch
            {
                // Highlight is guidance only; it must never block layout.
            }
        }

        private static void PickBlock(BlockTarget target)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var label = target == BlockTarget.Main ? "Xương chính" : target == BlockTarget.Furring ? "Xương phụ" : "Ty treo";
            var options = new PromptEntityOptions($"\nHNL Tool - VXT Pro: Chọn Block mẫu {label}: ");
            options.SetRejectMessage("\nHNL Tool - VXT Pro: Đối tượng phải là Block.");
            options.AddAllowedClass(typeof(BlockReference), true);
            var result = ed.GetEntity(options);
            if (result.Status != PromptStatus.OK) return;

            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var br = tr.GetObject(result.ObjectId, OpenMode.ForRead) as BlockReference;
                if (br == null) return;
                var blockId = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                var btr = tr.GetObject(blockId, OpenMode.ForRead) as BlockTableRecord;
                var blockName = btr?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(blockName)) return;

                var settings = VxtSession.Current.Settings;
                switch (target)
                {
                    case BlockTarget.Main: settings.MainBlockName = blockName; settings.MainLayer = br.Layer; break;
                    case BlockTarget.Furring: settings.FurringBlockName = blockName; settings.FurringLayer = br.Layer; break;
                    case BlockTarget.Hanger: settings.HangerBlockName = blockName; settings.HangerLayer = br.Layer; break;
                }
                VxtSession.Current.ViewModel?.SetBlock(target, blockName);
                ed.WriteMessage($"\nHNL Tool - VXT Pro: Đã chọn Block {label}: {blockName}");
                tr.Commit();
            }
            VxtTransientPreview.Instance.Refresh();
        }

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

        private static void PickDimension(DimensionTarget target)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var session = VxtSession.Current;
            if (!session.HasBoundary)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Hãy chọn biên trần trước.");
                return;
            }

            var result = ed.GetPoint("\nHNL Tool - VXT Pro: Chọn vị trí đặt đường Dim: ");
            if (result.Status != PromptStatus.OK) return;
            var settings = session.Settings;
            var angle = ResolveCurrentMainAngle(settings);
            var radians = angle * Math.PI / 180.0;
            var localPick = Transform2.ToLocal(new Point2(result.Value.X, result.Value.Y), radians);
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
            var bounds = localBoundary.GetBounds();

            var dTop = Math.Abs(localPick.Y - bounds.Max.Y);
            var dBottom = Math.Abs(localPick.Y - bounds.Min.Y);
            var dLeft = Math.Abs(localPick.X - bounds.Min.X);
            var dRight = Math.Abs(localPick.X - bounds.Max.X);
            var min = Math.Min(Math.Min(dTop, dBottom), Math.Min(dLeft, dRight));
            DimensionPosition position;
            double distance;
            if (Math.Abs(min - dTop) < 1e-9) { position = DimensionPosition.Top; distance = dTop; }
            else if (Math.Abs(min - dBottom) < 1e-9) { position = DimensionPosition.Bottom; distance = dBottom; }
            else if (Math.Abs(min - dLeft) < 1e-9) { position = DimensionPosition.Left; distance = dLeft; }
            else { position = DimensionPosition.Right; distance = dRight; }

            settings.DimensionDistance = distance;
            switch (target)
            {
                case DimensionTarget.Main: settings.MainDimPosition = position; break;
                case DimensionTarget.Furring: settings.FurringDimPosition = position; break;
                case DimensionTarget.Hanger: settings.HangerDimPosition = position; break;
            }
            session.ViewModel?.SetDimensionPick(target, position, distance);
            VxtTransientPreview.Instance.Refresh();
        }
    }
}
