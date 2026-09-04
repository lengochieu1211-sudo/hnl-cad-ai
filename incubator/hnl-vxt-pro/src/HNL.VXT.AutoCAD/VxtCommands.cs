using System;
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
        public void Create() => VxtCreateEngine.Execute();

        [CommandMethod("VXTSELECTBOUNDARY", CommandFlags.Modal)]
        public void SelectBoundary()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var options = new PromptEntityOptions("\nHNL Tool - VXT Pro: Chọn Polyline kín làm biên trần: ");
            options.SetRejectMessage("\nHNL Tool - VXT Pro: Đối tượng phải là Polyline.");
            options.AddAllowedClass(typeof(Polyline), true);

            var result = ed.GetEntity(options);
            if (result.Status != PromptStatus.OK) return;

            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var pl = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null || !pl.Closed)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Polyline phải khép kín.");
                    return;
                }

                var boundary = BoundarySampler.FromPolyline(pl);
                VxtSession.Current.Boundary = boundary;
                VxtSession.Current.Regions.Clear();
                VxtSession.Current.ViewModel?.SetBoundaryStatus(
                    $"✓ Đã chọn {pl.NumberOfVertices} đỉnh • Layer: {pl.Layer}", true);
                tr.Commit();
            }
            VxtTransientPreview.Instance.Refresh();
        }

        [CommandMethod("VXTPICKDIRECTION", CommandFlags.Modal)]
        public void PickDirection()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var angle = PromptAngleByTwoPoints(doc.Editor, "hướng Xương chính");
            if (!angle.HasValue) return;

            VxtSession.Current.Settings.MainDirection = MainDirectionMode.TwoPoints;
            VxtSession.Current.Settings.DirectionDegrees = angle.Value;
            VxtSession.Current.Regions.Clear();
            VxtSession.Current.ViewModel?.SetDirection(angle.Value);
            VxtTransientPreview.Instance.Refresh();
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

            session.Regions.Clear();
            ed.WriteMessage("\nHNL Tool - VXT Pro: Chia vùng bằng hình chữ nhật. Enter tại điểm đầu để kết thúc.");

            while (true)
            {
                var firstOptions = new PromptPointOptions("\nChọn góc thứ nhất của vùng (Enter = kết thúc): ") { AllowNone = true };
                var first = ed.GetPoint(firstOptions);
                if (first.Status == PromptStatus.None) break;
                if (first.Status != PromptStatus.OK) return;

                var cornerOptions = new PromptCornerOptions("\nChọn góc đối diện của vùng: ", first.Value);
                var second = ed.GetCorner(cornerOptions);
                if (second.Status != PromptStatus.OK) return;

                var minX = Math.Min(first.Value.X, second.Value.X);
                var minY = Math.Min(first.Value.Y, second.Value.Y);
                var maxX = Math.Max(first.Value.X, second.Value.X);
                var maxY = Math.Max(first.Value.Y, second.Value.Y);
                if (maxX - minX < 1e-6 || maxY - minY < 1e-6)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Vùng quá nhỏ, bỏ qua.");
                    continue;
                }

                var direction = new PromptKeywordOptions("\nHướng Xương chính trong vùng [Ngang/Doc/Goc2Diem] <Ngang>: ");
                direction.Keywords.Add("Ngang");
                direction.Keywords.Add("Doc");
                direction.Keywords.Add("Goc2Diem");
                direction.Keywords.Default = "Ngang";
                var directionResult = ed.GetKeywords(direction);
                if (directionResult.Status != PromptStatus.OK && directionResult.Status != PromptStatus.None) return;
                var key = directionResult.Status == PromptStatus.None ? "Ngang" : directionResult.StringResult;

                double angle = 0.0;
                if (key == "Doc") angle = 90.0;
                else if (key == "Goc2Diem")
                {
                    var picked = PromptAngleByTwoPoints(ed, "hướng vùng");
                    if (!picked.HasValue) return;
                    angle = picked.Value;
                }

                session.Regions.Add(new VxtLayoutRegion(new Box2(minX, minY, maxX, maxY), angle));
                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã nhận vùng " + session.Regions.Count + ".");
            }

            if (session.Regions.Count == 0)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Chưa tạo vùng nào; giữ hướng trước đó.");
                return;
            }

            session.Settings.MainDirection = MainDirectionMode.RectangleRegions;
            VxtTransientPreview.Instance.Refresh();
            ed.WriteMessage("\nHNL Tool - VXT Pro: Đã thiết lập " + session.Regions.Count + " vùng. Preview và Tạo thật dùng đúng các vùng này.");
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

        private static double? PromptAngleByTwoPoints(Editor ed, string label)
        {
            var p1 = ed.GetPoint("\nHNL Tool - VXT Pro: Chọn điểm đầu " + label + ": ");
            if (p1.Status != PromptStatus.OK) return null;
            var p2opt = new PromptPointOptions("\nHNL Tool - VXT Pro: Chọn điểm cuối " + label + ": ")
            {
                BasePoint = p1.Value,
                UseBasePoint = true
            };
            var p2 = ed.GetPoint(p2opt);
            if (p2.Status != PromptStatus.OK) return null;
            var dx = p2.Value.X - p1.Value.X;
            var dy = p2.Value.Y - p1.Value.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < 1e-8) return null;
            return Math.Atan2(dy, dx) * 180.0 / Math.PI;
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
            var options = new PromptSelectionOptions { MessageForAdding = $"\nHNL Tool - VXT Pro: Chọn Block thiết bị {label}: " };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "INSERT") });
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
            ed.WriteMessage($"\nHNL Tool - VXT Pro: Đã chọn {ids.Length} Block thiết bị {label}.");
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
            var radians = settings.DirectionDegrees * Math.PI / 180.0;
            var localPick = Transform2.ToLocal(new Point2(result.Value.X, result.Value.Y), radians);
            var localBoundary = new Boundary2(session.Boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)));
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
