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
                var accepted = new List<Boundary2>();
                var acceptedIds = new List<ObjectId>();
                var skippedOpen = 0;
                var skippedUnsupported = 0;
                foreach (var id in result.Value.GetObjectIds())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity is Polyline pl)
                    {
                        if (!pl.Closed) { skippedOpen++; continue; }
                        accepted.Add(BoundarySampler.FromPolyline(pl));
                        acceptedIds.Add(id);
                    }
                    else if (entity is Polyline2d pl2)
                    {
                        if (!pl2.Closed) { skippedOpen++; continue; }
                        accepted.Add(BoundarySampler.FromPolyline2d(pl2, tr));
                        acceptedIds.Add(id);
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
                session.BoundaryIds.Clear();
                session.BoundaryIds.AddRange(acceptedIds);
                session.Regions.Clear();
                session.BoundaryRegionGroups.Clear();
                session.GlobalFurringFromFarEdge = false;
                var skipped = skippedOpen + skippedUnsupported;
                session.ViewModel?.SetBoundaryStatus(
                    "✓ Đã chọn " + accepted.Count + " Polyline kín" +
                    (skipped > 0 ? " • Bỏ qua " + skipped + " đối tượng không hợp lệ" : string.Empty), true);
                tr.Commit();

                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã nhận " + accepted.Count +
                    " mảng trần độc lập" + (skipped > 0 ? "; bỏ qua " + skipped + " đối tượng." : "."));
            }

            var mode = VxtSession.Current.Settings.MainDirection;
            if (mode == MainDirectionMode.TwoPoints || mode == MainDirectionMode.RectangleRegions)
                VxtTransientPreview.Instance.Clear();
            else
                VxtTransientPreview.Instance.Refresh();
        }

        [CommandMethod("VXTPICKDIRECTION", CommandFlags.Modal)]
        public void PickDirection()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var angle = PromptAngleByTwoPoints(doc.Editor, "hướng Xương chính");
            if (!angle.HasValue)
            {
                doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Chưa xác định hướng 2 điểm; giữ hướng trước đó.");
                return;
            }

            var session = VxtSession.Current;
            session.Settings.MainDirection = MainDirectionMode.TwoPoints;
            session.Settings.DirectionDegrees = NormalizeAngle(angle.Value);
            session.Regions.Clear();
            session.BoundaryRegionGroups.Clear();
            session.ViewModel?.SetDirection(angle.Value);
            VxtTransientPreview.Instance.Refresh();
            doc.Editor.WriteMessage("\nHNL Tool - VXT Pro: Hướng Xương chính 2 điểm = " +
                NormalizeAngle(angle.Value).ToString("0.###") + "°.");
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
            if (settings.MainDirection == MainDirectionMode.TwoPoints || settings.MainDirection == MainDirectionMode.RectangleRegions)
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

            var result = ed.GetPoint("\nHNL Tool - VXT Pro: Chọn vị trí đặt đường DIM: ");
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
