using Autodesk.AutoCAD.Runtime;

namespace HNL.VXT.AutoCAD
{
    public sealed class VxtDiagnosticCommands
    {
        [CommandMethod("VXTANALYZE", CommandFlags.Modal)]
        public void Analyze()
        {
            var session = VxtSession.Current;
            var settings = session.ViewModel?.Snapshot() ?? session.Settings.Clone();
            VxtDiagnosticService.AnalyzeAndReport(settings);
        }

        [CommandMethod("VXTDIAGZIP", CommandFlags.Modal)]
        public void ExportZip()
        {
            var session = VxtSession.Current;
            var settings = session.ViewModel?.Snapshot() ?? session.Settings.Clone();
            VxtDiagnosticService.ExportInteractive(settings);
        }
    }
}
