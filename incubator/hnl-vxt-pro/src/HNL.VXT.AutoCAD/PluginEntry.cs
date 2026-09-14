using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using HNL.VXT.UI.Infrastructure;
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
            doc?.Editor.WriteMessage("\nHNL Tool - Vẽ Xương Trần | " + VxtBuildInfo.VersionLabel + " | Lệnh: HVX");
        }

        public void Terminate()
        {
            var documents = Application.DocumentManager;
            documents.DocumentActivated -= OnDocumentActivated;
            documents.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;

            // Shutdown/document teardown is exactly where AutoCAD owns native graphics destruction.
            // Never call TransientManager here. Keep wrappers alive and let process teardown reclaim them.
            VxtTransientPreview.Instance.AbandonForDocumentTransition();
            VxtSession.ReleaseDocument(null);
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e?.Document;
            if (doc == null) return;

            // AutoCAD can fire DocumentActivated again for the same drawing when focus/modal
            // state changes. VxtSession filters that redundant notification by Document identity.
            // On a real DWG switch, do not touch TransientManager from this native callback:
            // retain the wrappers until AutoCAD has finished tearing down the old viewport state.
            if (VxtSession.SynchronizeDocument(doc))
                VxtTransientPreview.Instance.AbandonForDocumentTransition();
        }

        private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e?.Document;
            if (doc == null) return;
            if (VxtSession.ReleaseDocument(doc))
                VxtTransientPreview.Instance.AbandonForDocumentTransition();
        }
    }
}
