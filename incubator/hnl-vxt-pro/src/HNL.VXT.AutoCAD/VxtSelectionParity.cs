using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HNL.VXT.Core.Models;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtSelectionParity
    {
        public static void PickBlock(BlockTarget target)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var label = target == BlockTarget.Main ? "Xương chính" : target == BlockTarget.Furring ? "Xương phụ" : "Ty treo";
            var options = new PromptEntityOptions("\nHNL Tool - VXT Pro: Chọn Block mẫu " + label + ": ");
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
                var layer = br.Layer;

                var session = VxtSession.Current;
                switch (target)
                {
                    case BlockTarget.Main:
                        session.Settings.MainBlockName = blockName;
                        session.Settings.MainLayer = layer;
                        break;
                    case BlockTarget.Furring:
                        session.Settings.FurringBlockName = blockName;
                        session.Settings.FurringLayer = layer;
                        break;
                    case BlockTarget.Hanger:
                        session.Settings.HangerBlockName = blockName;
                        session.Settings.HangerLayer = layer;
                        break;
                }

                // Critical parity fix: RequestCreate snapshots the ViewModel. Update both the
                // block name and captured source layer so Snapshot cannot revert the layer.
                var vm = session.ViewModel;
                if (vm != null)
                {
                    vm.SetBlock(target, blockName);
                    switch (target)
                    {
                        case BlockTarget.Main: vm.MainLayer = layer; break;
                        case BlockTarget.Furring: vm.FurringLayer = layer; break;
                        case BlockTarget.Hanger: vm.HangerLayer = layer; break;
                    }
                }

                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã chọn Block " + label + ": " + blockName + " • Layer: " + layer);
                tr.Commit();
            }
            VxtTransientPreview.Instance.Refresh();
        }

        public static void PickEquipment(EquipmentTarget target)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var session = VxtSession.Current;
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

            // V6.7.2 assigns the result of ssget directly. Cancel/Enter therefore leaves NIL;
            // it must not silently retain the previous obstacle set.
            if (result.Status != PromptStatus.OK)
            {
                StoreEquipmentIds(session, target, Array.Empty<ObjectId>());
                session.ViewModel?.SetEquipmentStatus(target, 0);
                VxtTransientPreview.Instance.Refresh();
                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã xóa tập thiết bị " + label + ".");
                return;
            }

            var ids = result.Value.GetObjectIds();
            StoreEquipmentIds(session, target, ids);
            session.ViewModel?.SetEquipmentStatus(target, ids.Length);
            ed.WriteMessage("\nHNL Tool - VXT Pro: Đã chọn " + ids.Length + " đối tượng thiết bị " + label + ".");
            VxtTransientPreview.Instance.Refresh();
        }

        private static void StoreEquipmentIds(VxtSession session, EquipmentTarget target, ObjectId[] ids)
        {
            switch (target)
            {
                case EquipmentTarget.General: session.GeneralEquipmentIds = ids; break;
                case EquipmentTarget.Main: session.MainEquipmentIds = ids; break;
                case EquipmentTarget.Furring: session.FurringEquipmentIds = ids; break;
            }
        }
    }
}
