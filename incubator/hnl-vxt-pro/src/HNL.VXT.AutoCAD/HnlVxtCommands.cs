using Autodesk.AutoCAD.Runtime;
using HNL.VXT.Core.Models;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// The only registered AutoCAD command surface for HNL VXT Pro.
    /// User-facing shortcut: HVX (HNL + Ve Xuong), short for daily use and distinct
    /// from the legacy Lisp command VXT. Technical commands keep the longer HNLVXT
    /// namespace to minimize collision risk with AutoCAD and other Lisp tools.
    /// VxtCommands remains the implementation class but is intentionally not
    /// registered as a CommandClass, so its legacy VXT* attributes are not exposed.
    /// </summary>
    public sealed class HnlVxtCommands
    {
        [CommandMethod("HVX", CommandFlags.Modal)]
        public void ShowPalette() => new VxtCommands().ShowPalette();

        [CommandMethod("HNLVXTCREATE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Create()
        {
            // PickFirst parity: if the user already selected closed ceiling polylines in AutoCAD,
            // consume them as boundaries instead of reporting "Chưa chọn biên trần".
            if (!VxtSession.Current.HasBoundary)
                VxtBoundarySelectionAdapter.TryAdoptImpliedSelection(refreshPreview: true, writeMessage: true);

            VxtLegacyParityCoordinator.ExecuteCreate();
        }

        [CommandMethod("HNLVXTBOUNDARY", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SelectBoundary()
        {
            // A fresh Lisp ssget starts a fresh per-ceiling ask_each state. Clear the previous
            // boundary-specific XP directions before the new selection is collected.
            VxtSession.Current.BoundaryFurringFromFarEdges.Clear();
            VxtSession.Current.GlobalFurringFromFarEdge = false;

            // AutoCAD PickFirst/preselection is authoritative when it contains at least one valid
            // closed Polyline. Only fall back to an interactive GetSelection when there is no
            // reusable ceiling boundary in the current implied selection.
            if (VxtBoundarySelectionAdapter.TryAdoptImpliedSelection(refreshPreview: true, writeMessage: true))
                return;

            new VxtCommands().SelectBoundary();
        }

        [CommandMethod("HNLVXTDIRECTION", CommandFlags.Modal)]
        public void PickDirection() => new VxtCommands().PickDirection();

        [CommandMethod("HNLVXTREGION", CommandFlags.Modal)]
        public void RectangleDirectionMode() => new VxtCommands().RectangleDirectionMode();

        [CommandMethod("HNLVXTPICKMAIN", CommandFlags.Modal)]
        public void PickMainBlock() => VxtSelectionParity.PickBlock(BlockTarget.Main);

        [CommandMethod("HNLVXTPICKFURRING", CommandFlags.Modal)]
        public void PickFurringBlock() => VxtSelectionParity.PickBlock(BlockTarget.Furring);

        [CommandMethod("HNLVXTPICKHANGER", CommandFlags.Modal)]
        public void PickHangerBlock() => VxtSelectionParity.PickBlock(BlockTarget.Hanger);

        [CommandMethod("HNLVXTMEP", CommandFlags.Modal)]
        public void PickGeneralEquipment() => VxtSelectionParity.PickEquipment(EquipmentTarget.General);

        [CommandMethod("HNLVXTMEPMAIN", CommandFlags.Modal)]
        public void PickMainEquipment() => VxtSelectionParity.PickEquipment(EquipmentTarget.Main);

        [CommandMethod("HNLVXTMEPFURRING", CommandFlags.Modal)]
        public void PickFurringEquipment() => VxtSelectionParity.PickEquipment(EquipmentTarget.Furring);

        [CommandMethod("HNLVXTDIMMAIN", CommandFlags.Modal)]
        public void PickMainDim() => new VxtCommands().PickMainDim();

        [CommandMethod("HNLVXTDIMFURRING", CommandFlags.Modal)]
        public void PickFurringDim() => new VxtCommands().PickFurringDim();

        [CommandMethod("HNLVXTDIMHANGER", CommandFlags.Modal)]
        public void PickHangerDim() => new VxtCommands().PickHangerDim();

        [CommandMethod("HNLVXTANALYZE", CommandFlags.Modal)]
        public void Analyze() => new VxtDiagnosticCommands().Analyze();

        [CommandMethod("HNLVXTDIAG", CommandFlags.Modal)]
        public void ExportZip() => new VxtDiagnosticCommands().ExportZip();

        [CommandMethod("HNLVXTGOLDEN", CommandFlags.Modal)]
        public void RuntimeGolden() => VxtRuntimeGoldenService.Run();

        [CommandMethod("HNLVXTPROGOLDEN", CommandFlags.Modal)]
        public void RuntimeProGolden() => VxtRuntimeGoldenService.RunPro();

        [CommandMethod("HNLVXTPROAUTOQA", CommandFlags.Modal)]
        public void RuntimeProAutoQa() => VxtProAutoRuntimeQaService.Run();

        [CommandMethod("HNLVXTPROMULTIQA", CommandFlags.Modal)]
        public void RuntimeProMultiQa() => VxtProMultiRuntimeQaService.Run();

        // One-command replacement for SCRIPT-based QA. Both names intentionally point to the
        // same runner so field verification needs no external .scr file or file chooser.
        [CommandMethod("HNLVXTQA", CommandFlags.Modal)]
        public void RuntimeAllQa() => RunAllQaCore();

        [CommandMethod("HNLVXTRUNALLQA", CommandFlags.Modal)]
        public void RuntimeAllQaLong() => RunAllQaCore();

        private static void RunAllQaCore()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            ed.WriteMessage("\nHNL Tool - VXT Pro: Bắt đầu chạy 4 bài kiểm tra Runtime QA...");

            RunQaStep(ed, "HNLVXTGOLDEN", VxtRuntimeGoldenService.Run);
            RunQaStep(ed, "HNLVXTPROGOLDEN", VxtRuntimeGoldenService.RunPro);
            RunQaStep(ed, "HNLVXTPROAUTOQA", VxtProAutoRuntimeQaService.Run);
            RunQaStep(ed, "HNLVXTPROMULTIQA", VxtProMultiRuntimeQaService.Run);

            ed.WriteMessage("\nHNL Tool - VXT Pro: Đã chạy xong 4 QA. Kiểm tra từng dòng PASS/FAIL phía trên.");
        }

        private static void RunQaStep(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            string commandName,
            System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception ex)
            {
                // Continue the remaining QA steps even when one service throws unexpectedly.
                ed.WriteMessage("\nHNL Tool - VXT Pro: " + commandName + " lỗi: " + ex.Message);
            }
        }
    }
}