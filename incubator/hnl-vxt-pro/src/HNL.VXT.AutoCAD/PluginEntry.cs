using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(HNL.VXT.AutoCAD.PluginEntry))]
[assembly: CommandClass(typeof(HNL.VXT.AutoCAD.VxtCommands))]

namespace HNL.VXT.AutoCAD
{
    public sealed class PluginEntry : IExtensionApplication
    {
        public void Initialize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\nHNL Tool - Vẽ Xương Trần | VXT Pro v7.0.0-alpha.1 | Lệnh: VXT");
        }

        public void Terminate()
        {
            VxtTransientPreview.Instance.Clear();
        }
    }
}
