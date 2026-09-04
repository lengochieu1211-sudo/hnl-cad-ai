using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.AutoCAD
{
    public sealed class VxtCommands
    {
        [CommandMethod("VXT", CommandFlags.Modal)]
        public void ShowPalette()
        {
            VxtPaletteService.Show();
        }

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
            var ed = doc.Editor;

            var p1 = ed.GetPoint("\nHNL Tool - VXT Pro: Chọn điểm đầu hướng Xương chính: ");
            if (p1.Status != PromptStatus.OK) return;

            var p2opt = new PromptPointOptions("\nHNL Tool - VXT Pro: Chọn điểm cuối hướng Xương chính: ")
            {
                BasePoint = p1.Value,
                UseBasePoint = true
            };
            var p2 = ed.GetPoint(p2opt);
            if (p2.Status != PromptStatus.OK) return;

            var dx = p2.Value.X - p1.Value.X;
            var dy = p2.Value.Y - p1.Value.Y;
            var degrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            VxtSession.Current.Settings.MainDirection = MainDirectionMode.TwoPoints;
            VxtSession.Current.Settings.DirectionDegrees = degrees;
            VxtSession.Current.ViewModel?.SetDirection(degrees);
            VxtTransientPreview.Instance.Refresh();
        }

        [CommandMethod("VXTRECTDIRECTION", CommandFlags.Modal)]
        public void RectangleDirectionMode()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            VxtSession.Current.Settings.MainDirection = MainDirectionMode.RectangleRegions;
            doc.Editor.WriteMessage(
                "\nHNL Tool - VXT Pro: Chế độ chia vùng bằng hình chữ nhật đã chọn. Việc quét từng vùng sẽ chạy trong Create engine sau Golden Verification.");
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

        private static void PickBlock(BlockTarget target)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var label = target == BlockTarget.Main
                ? "Xương chính"
                : target == BlockTarget.Furring ? "Xương phụ" : "Ty treo";

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
                    case BlockTarget.Main:
                        settings.MainBlockName = blockName;
                        settings.MainLayer = br.Layer;
                        break;
                    case BlockTarget.Furring:
                        settings.FurringBlockName = blockName;
                        settings.FurringLayer = br.Layer;
                        break;
                    case BlockTarget.Hanger:
                        settings.HangerBlockName = blockName;
                        settings.HangerLayer = br.Layer;
                        break;
                }

                VxtSession.Current.ViewModel?.SetBlock(target, blockName);
                ed.WriteMessage($"\nHNL Tool - VXT Pro: Đã chọn Block {label}: {blockName}");
                tr.Commit();
            }
        }

        private static void PickEquipment(EquipmentTarget target)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var label = target == EquipmentTarget.General
                ? "dùng chung"
                : target == EquipmentTarget.Main ? "cho Xương chính" : "cho Xương phụ";

            var options = new PromptSelectionOptions
            {
                MessageForAdding = $"\nHNL Tool - VXT Pro: Chọn Block thiết bị {label}: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT")
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
            ed.WriteMessage($"\nHNL Tool - VXT Pro: Đã chọn {ids.Length} Block thiết bị {label}.");
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
