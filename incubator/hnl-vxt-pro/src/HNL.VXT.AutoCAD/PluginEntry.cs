using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(HNL.VXT.AutoCAD.PluginEntry))]
[assembly: CommandClass(typeof(HNL.VXT.AutoCAD.HnlVxtCommands))]

namespace HNL.VXT.AutoCAD
{
    public sealed class PluginEntry : IExtensionApplication
    {
        public void Initialize()
        {
            var documents = Application.DocumentManager;
            documents.DocumentActivated += OnDocumentActivated;
            documents.DocumentToBeDestroyed += OnDocumentToBeDestroyed;

            var doc = documents.MdiActiveDocument;
            if (doc != null) VxtSession.SynchronizeDatabase(doc.Database);
            doc?.Editor.WriteMessage("\nHNL Tool - Vẽ Xương Trần | VXT Pro v7.0.0-beta.1 | Lệnh: HVX");
        }

        public void Terminate()
        {
            var documents = Application.DocumentManager;
            documents.DocumentActivated -= OnDocumentActivated;
            documents.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            VxtTransientPreview.Instance.Clear();
            VxtSession.ReleaseDatabase(null);
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e?.Document;
            if (doc == null) return;

            // Transients and ObjectIds are owned by the drawing that created them. When a new
            // DWG becomes active, clear the old preview and reset only drawing-specific session
            // state. Settings and the palette/view-model intentionally survive the switch.
            if (VxtSession.SynchronizeDatabase(doc.Database))
                VxtTransientPreview.Instance.Clear();
        }

        private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e?.Document;
            if (doc == null) return;
            if (VxtSession.ReleaseDatabase(doc.Database))
                VxtTransientPreview.Instance.Clear();
        }
    }
}
