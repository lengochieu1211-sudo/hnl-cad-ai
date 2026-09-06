using Autodesk.AutoCAD.Runtime;

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
        public void SelectBoundary() => new VxtCommands().SelectBoundary();

        [CommandMethod("HNLVXTDIRECTION", CommandFlags.Modal)]
        public void PickDirection() => new VxtCommands().PickDirection();

        [CommandMethod("HNLVXTREGION", CommandFlags.Modal)]
        public void RectangleDirectionMode() => new VxtCommands().RectangleDirectionMode();

        [CommandMethod("HNLVXTPICKMAIN", CommandFlags.Modal)]
        public void PickMainBlock() => new VxtCommands().PickMainBlock();

        [CommandMethod("HNLVXTPICKFURRING", CommandFlags.Modal)]
        public void PickFurringBlock() => new VxtCommands().PickFurringBlock();

        [CommandMethod("HNLVXTPICKHANGER", CommandFlags.Modal)]
        public void PickHangerBlock() => new VxtCommands().PickHangerBlock();

        [CommandMethod("HNLVXTMEP", CommandFlags.Modal)]
        public void PickGeneralEquipment() => new VxtCommands().PickGeneralEquipment();

        [CommandMethod("HNLVXTMEPMAIN", CommandFlags.Modal)]
        public void PickMainEquipment() => new VxtCommands().PickMainEquipment();

        [CommandMethod("HNLVXTMEPFURRING", CommandFlags.Modal)]
        public void PickFurringEquipment() => new VxtCommands().PickFurringEquipment();

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
