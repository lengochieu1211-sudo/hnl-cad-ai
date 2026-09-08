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

            // GetNestedEntity is intentionally used instead of GetEntity so users can pick a
            // block through nested geometry / dynamic block content / associative-array items.
            var options = new PromptNestedEntityOptions(
                "\nHNL Tool - VXT Pro: Chọn Block mẫu " + label + " (có thể chọn Block lồng/Array): ");
            var result = ed.GetNestedEntity(options);
            if (result.Status != PromptStatus.OK) return;

            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var br = ResolveSelectedBlockReference(result, tr);
                if (br == null)
                {
                    ed.WriteMessage("\nHNL Tool - VXT Pro: Không tìm thấy BlockReference tại vị trí chọn.");
                    return;
                }

                var blockId = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                var btr = tr.GetObject(blockId, OpenMode.ForRead) as BlockTableRecord;
                var blockName = btr?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(blockName)) return;
                var layer = br.Layer;

                string lengthProperty = string.Empty;
                var arraySensitive = false;
                if (target == BlockTarget.Main || target == BlockTarget.Furring)
                {
                    lengthProperty = VxtDynamicBlockAdapter.InspectAndRemember(br, blockName);
                    arraySensitive = VxtDynamicBlockAdapter.IsArraySensitive(br);
                }

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

                // RequestCreate snapshots the ViewModel. Update both the block name and source
                // layer so the selected block is not reverted by a later Snapshot().
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

                var adapterText = string.Empty;
                if (target == BlockTarget.Main || target == BlockTarget.Furring)
                {
                    if (!string.IsNullOrWhiteSpace(lengthProperty))
                        adapterText = " • Length property: " + lengthProperty;
                    else if (arraySensitive)
                        adapterText = " • Có Array/Spacing nhưng chưa nhận diện Length: sẽ dùng Polyline/MLINE an toàn";
                    else
                        adapterText = " • Không nhận diện Stretch rõ: dùng XScale đơn giản";
                }

                ed.WriteMessage(
                    "\nHNL Tool - VXT Pro: Đã chọn Block " + label + ": " + blockName +
                    " • Layer: " + layer + adapterText);
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

        private static BlockReference ResolveSelectedBlockReference(PromptNestedEntityResult result, Transaction tr)
        {
            if (result == null || tr == null) return null;

            try
            {
                var direct = tr.GetObject(result.ObjectId, OpenMode.ForRead, false) as BlockReference;
                if (direct != null) return direct;
            }
            catch { }

            try
            {
                var containers = result.GetContainers();
                if (containers == null) return null;
                // Prefer the nearest enclosing block. If API order differs between releases,
                // either direction still resolves a real BlockReference rather than a line/arc.
                for (var i = containers.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        var br = tr.GetObject(containers[i], OpenMode.ForRead, false) as BlockReference;
                        if (br != null) return br;
                    }
                    catch { }
                }
                for (var i = 0; i < containers.Length; i++)
                {
                    try
                    {
                        var br = tr.GetObject(containers[i], OpenMode.ForRead, false) as BlockReference;
                        if (br != null) return br;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
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
