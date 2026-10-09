using System.Linq;
using Autodesk.AutoCAD.Runtime;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// The only registered AutoCAD command surface for HNL Ceiling Framing Pro.
    /// Primary user-facing command: HCF (HNL Ceiling Framing).
    /// HVX remains a compatibility alias for existing users/scripts.
    /// Technical commands keep the longer HNLVXT
    /// namespace to minimize collision risk with AutoCAD and other Lisp tools.
    /// VxtCommands remains the implementation class but is intentionally not
    /// registered as a CommandClass, so its legacy VXT* attributes are not exposed.
    /// </summary>
    public sealed class HnlVxtCommands
    {
        [CommandMethod("HCF", CommandFlags.Modal)]
        public void ShowPalette()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().ShowPalette();
        }

        [CommandMethod("HVX", CommandFlags.Modal)]
        public void ShowPaletteLegacyAlias()
        {
            ShowPalette();
        }

        [CommandMethod("HNLVXTCREATE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Create()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            // PickFirst parity: if the user already selected closed ceiling polylines in AutoCAD,
            // consume them as boundaries instead of reporting "Chưa chọn biên trần".
            if (!VxtSession.Current.HasBoundary)
                VxtBoundarySelectionAdapter.TryAdoptImpliedSelection(refreshPreview: true, writeMessage: true);

            VxtLegacyParityCoordinator.ExecuteCreate();
        }

        [CommandMethod("HNLVXTCREATEWARN", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void CreateWithWarning()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            if (!VxtSession.Current.HasBoundary)
                VxtBoundarySelectionAdapter.TryAdoptImpliedSelection(refreshPreview: true, writeMessage: true);

            var diagnostics = VxtSession.Current.ViewModel?.ConstraintDiagnostics
                .Where(VxtConstraintOverridePolicy.IsManualOverrideAllowed)
                .ToList();
            var count = diagnostics?.Count ?? 0;
            var boundaryCodes = diagnostics == null
                ? string.Empty
                : string.Join(", ", diagnostics
                    .Select(x => x.BoundaryCode)
                    .Distinct()
                    .Take(8));

            var detail = count > 0
                ? "Có " + count + " lỗi bố trí HARD có thể chỉnh thủ công" +
                  (string.IsNullOrWhiteSpace(boundaryCodes) ? "." : " tại " + boundaryCodes + ".")
                : "HNL Tool sẽ kiểm tra lại lỗi bố trí HARD trước khi tạo.";

            var answer = System.Windows.MessageBox.Show(
                "HNL Tool - Ceiling Framing Pro\n\n" +
                detail +
                "\n\nNếu tiếp tục, HNL Tool chỉ được phép bỏ chặn các lỗi bố trí có thể chỉnh thủ công." +
                "\nLỗi cấu hình, tài nguyên CAD, parity Preview/Create hoặc runtime vẫn bị chặn và rollback." +
                "\n\nTiếp tục Tạo có cảnh báo?",
                "HNL Tool - Tạo có cảnh báo",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (answer != System.Windows.MessageBoxResult.Yes) return;

            VxtLegacyParityCoordinator.ExecuteCreate(allowConstraintOverride: true);
        }

        [CommandMethod("HNLVXTBOUNDARY", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SelectBoundary()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;

            // AutoCAD PickFirst/preselection is authoritative when it contains at least one valid
            // closed Polyline. State is reset only after a new boundary is actually accepted so
            // Esc/Cancel can preserve the previous VXT session exactly.
            if (VxtBoundarySelectionAdapter.TryAdoptImpliedSelection(refreshPreview: true, writeMessage: true))
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
                VxtCommands.QueueBoundaryFollowUp(doc);
                return;
            }

            new VxtCommands().SelectBoundary();
        }

        [CommandMethod("HNLVXTPICKBOUNDARYPOINT", CommandFlags.Modal)]
        public void PickBoundaryPoint()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().PickBoundaryPoint();
        }

        [CommandMethod("HNLVXTAUTOSETUP", CommandFlags.Modal)]
        public void ConfigureAutoDirection()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtLegacyParityCoordinator.ConfigureAutoShadowlineInteractive(refreshPreview: true, fallbackFromCreate: false);
        }

        [CommandMethod("HNLVXTDIRECTION", CommandFlags.Modal)]
        public void PickDirection()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().PickDirection();
        }

        [CommandMethod("HNLVXTREGION", CommandFlags.Modal)]
        public void RectangleDirectionMode()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().RectangleDirectionMode();
        }

        [CommandMethod("HNLVXTPICKMAIN", CommandFlags.Modal)]
        public void PickMainBlock()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtSelectionParity.PickBlock(BlockTarget.Main);
        }

        [CommandMethod("HNLVXTPICKFURRING", CommandFlags.Modal)]
        public void PickFurringBlock()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtSelectionParity.PickBlock(BlockTarget.Furring);
        }

        [CommandMethod("HNLVXTPICKHANGER", CommandFlags.Modal)]
        public void PickHangerBlock()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtSelectionParity.PickBlock(BlockTarget.Hanger);
        }

        [CommandMethod("HNLVXTMEP", CommandFlags.Modal)]
        public void PickGeneralEquipment()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtSelectionParity.PickEquipment(EquipmentTarget.General);
        }

        [CommandMethod("HNLVXTMEPMAIN", CommandFlags.Modal)]
        public void PickMainEquipment()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtSelectionParity.PickEquipment(EquipmentTarget.Main);
        }

        [CommandMethod("HNLVXTMEPFURRING", CommandFlags.Modal)]
        public void PickFurringEquipment()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtSelectionParity.PickEquipment(EquipmentTarget.Furring);
        }

        [CommandMethod("HNLVXTDIMMAIN", CommandFlags.Modal)]
        public void PickMainDim()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().PickMainDim();
        }

        [CommandMethod("HNLVXTDIMFURRING", CommandFlags.Modal)]
        public void PickFurringDim()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().PickFurringDim();
        }

        [CommandMethod("HNLVXTDIMHANGER", CommandFlags.Modal)]
        public void PickHangerDim()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().PickHangerDim();
        }

        [CommandMethod("HNLVXTANALYZE", CommandFlags.Modal)]
        public void Analyze()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtDiagnosticCommands().Analyze();
        }

        [CommandMethod("HNLVXTDIAG", CommandFlags.Modal)]
        public void ExportZip()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtDiagnosticCommands().ExportZip();
        }

        // Internal command-context marshaling used by the modeless WPF palette. These commands
        // deliberately own all TransientManager work so the palette never manipulates CAD graphics
        // directly from an application-context DispatcherTimer callback.
        [CommandMethod("HNLVXTPREVIEW", CommandFlags.Modal)]
        public void RefreshPreview()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtTransientPreview.Instance.Refresh();
        }

        [CommandMethod("HNLVXTCLEARPREVIEW", CommandFlags.Modal)]
        public void ClearPreview()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtTransientPreview.Instance.Clear();
        }

        // Mxx diagnostic selection must be exposed on the only registered CommandClass.
        // VxtCommands contains the implementation but is intentionally not registered.
        [CommandMethod("HNLVXTFOCUSBOUNDARY", CommandFlags.Modal)]
        public void FocusBoundaryDiagnostic()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            new VxtCommands().FocusBoundaryDiagnostic();
        }

        [CommandMethod("HNLVXTGOLDEN", CommandFlags.Modal)]
        public void RuntimeGolden()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtRuntimeGoldenService.Run();
        }

        [CommandMethod("HNLVXTPROGOLDEN", CommandFlags.Modal)]
        public void RuntimeProGolden()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtRuntimeGoldenService.RunPro();
        }

        [CommandMethod("HNLVXTPROAUTOQA", CommandFlags.Modal)]
        public void RuntimeProAutoQa()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtProAutoRuntimeQaService.Run();
        }

        [CommandMethod("HNLVXTPROMULTIQA", CommandFlags.Modal)]
        public void RuntimeProMultiQa()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtProMultiRuntimeQaService.Run();
        }

        [CommandMethod("HNLVXTSOAKQA", CommandFlags.Modal)]
        public void RuntimeTransientSoakQa()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtTransientSoakQaService.Run();
        }

        // Field QA for the currently selected real ceiling. Unlike the deterministic 5-step
        // suite below, this intentionally validates the active user drawing and Preview state.
        [CommandMethod("HNLVXTPREVIEWQA", CommandFlags.Modal)]
        public void RuntimePreviewQa()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            VxtPreviewRuntimeQaService.Run();
        }

        // One-command replacement for SCRIPT-based QA. Both names intentionally point to the
        // same runner so field verification needs no external .scr file or file chooser.
        [CommandMethod("HNLVXTQA", CommandFlags.Modal)]
        public void RuntimeAllQa()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            RunAllQaCore();
        }

        [CommandMethod("HNLVXTRUNALLQA", CommandFlags.Modal)]
        public void RuntimeAllQaLong()
        {
            if (!VxtAuthorization.EnsureAuthorized()) return;
            RunAllQaCore();
        }

        private static void RunAllQaCore()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            ed.WriteMessage("\nHNL Tool - Ceiling Framing Pro: Bắt đầu chạy 5 bài kiểm tra Runtime QA...");

            RunQaStep(ed, "HNLVXTGOLDEN", VxtRuntimeGoldenService.Run);
            RunQaStep(ed, "HNLVXTPROGOLDEN", VxtRuntimeGoldenService.RunPro);
            RunQaStep(ed, "HNLVXTPROAUTOQA", VxtProAutoRuntimeQaService.Run);
            RunQaStep(ed, "HNLVXTPROMULTIQA", VxtProMultiRuntimeQaService.Run);
            RunQaStep(ed, "HNLVXTSOAKQA", VxtTransientSoakQaService.Run);

            ed.WriteMessage("\nHNL Tool - Ceiling Framing Pro: Đã chạy xong 5 QA. Kiểm tra từng dòng PASS/FAIL phía trên.");
        }

        private static void RunQaStep(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            string commandName,
            System.Func<string> action)
        {
            try
            {
                action();
            }
            catch (System.Exception ex)
            {
                // Continue the remaining QA steps even when one service throws unexpectedly.
                ed.WriteMessage("\nHNL Tool - Ceiling Framing Pro: " + commandName + " lỗi: " + ex.Message);
            }
        }
    }
}
