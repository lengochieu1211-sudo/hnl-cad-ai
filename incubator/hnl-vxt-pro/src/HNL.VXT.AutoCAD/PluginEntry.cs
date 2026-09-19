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
        private static bool _runtimeHooksEnabled;

        public void Initialize()
        {
            // Startup must be inert. The bundle is command-lazy-loaded, but keep Initialize()
            // safe even if AutoCAD or an older manifest loads this assembly during startup.
            // Do not touch VxtSession, TransientManager, WPF, Document events, Database or Editor.
        }

        public void Terminate()
        {
            if (!_runtimeHooksEnabled) return;

            try
            {
                var documents = Application.DocumentManager;
                documents.DocumentActivated -= OnDocumentActivated;
                documents.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            }
            catch
            {
                // AutoCAD may already be tearing down managed wrappers.
            }

            _runtimeHooksEnabled = false;

            // Do not call TransientManager during shutdown. Abandon only releases HNL ownership
            // of managed wrapper collections and leaves native graphics teardown to AutoCAD.
            try { VxtTransientPreview.Instance.AbandonForDocumentTransition(); } catch { }
            try { VxtSession.ReleaseDocument(null); } catch { }
        }

        internal static void EnableRuntimeHooks()
        {
            if (_runtimeHooksEnabled) return;

            var documents = Application.DocumentManager;
            documents.DocumentActivated += OnDocumentActivated;
            documents.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            _runtimeHooksEnabled = true;

            var doc = documents.MdiActiveDocument;
            if (doc != null)
                VxtSession.SynchronizeDocument(doc);

            doc?.Editor.WriteMessage(
                "\nHNL Tool - Vẽ Xương Trần | " + VxtBuildInfo.VersionLabel + " | Lệnh: HVX");
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e?.Document;
            if (doc == null) return;

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
