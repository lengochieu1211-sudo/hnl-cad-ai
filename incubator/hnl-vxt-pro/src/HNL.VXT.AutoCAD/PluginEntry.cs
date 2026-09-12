using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using DocumentCollectionEventArgs = Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs;

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
            if (doc != null) VxtSession.SynchronizeDocument(doc);
            doc?.Editor.WriteMessage("\nHNL Tool - Vẽ Xương Trần | VXT Pro v7.0.0-beta.1 | Lệnh: HVX");
        }

        public void Terminate()
        {
            var documents = Application.DocumentManager;
            documents.DocumentActivated -= OnDocumentActivated;
            documents.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            VxtTransientPreview.Instance.Clear();
            VxtSession.ReleaseDocument(null);
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e?.Document;
            if (doc == null) return;

            // AutoCAD can fire DocumentActivated again for the same drawing when focus/modal
            // state changes. VxtSession filters that redundant notification by Document identity,
            // so only a real DWG switch clears drawing-specific selection/transient state.
            if (VxtSession.SynchronizeDocument(doc))
                VxtTransientPreview.Instance.Clear();
        }

        private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e?.Document;
            if (doc == null) return;
            if (VxtSession.ReleaseDocument(doc))
                VxtTransientPreview.Instance.Clear();
        }
    }
}
