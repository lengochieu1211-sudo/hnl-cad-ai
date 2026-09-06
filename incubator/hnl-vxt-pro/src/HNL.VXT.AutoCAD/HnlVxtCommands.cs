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

        [CommandMethod("HNLVXTCREATE", CommandFlags.Modal)]
        public void Create() => VxtLegacyParityCoordinator.ExecuteCreate();

        [CommandMethod("HNLVXTBOUNDARY", CommandFlags.Modal)]
        public void SelectBoundary()
        {
            // A fresh Lisp ssget starts a fresh per-ceiling ask_each state. Clear the previous
            // boundary-specific XP directions before the new selection is collected.
            VxtSession.Current.BoundaryFurringFromFarEdges.Clear();
            VxtSession.Current.GlobalFurringFromFarEdge = false;
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
    }
}
