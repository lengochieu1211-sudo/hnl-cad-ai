using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.Hosting;

namespace HNL.VXT.AutoCAD
{
    internal sealed class VxtHostBridge : IVxtHostBridge
    {
        public bool IsDarkTheme
        {
            get
            {
                try { return Convert.ToInt32(Application.GetSystemVariable("COLORTHEME")) == 0; }
                catch { return true; }
            }
        }

        public void SelectBoundary() => Send("VXTSELECTBOUNDARY ");
        public void PickDirection() => Send("VXTPICKDIRECTION ");

        public void PickDimensionPosition(DimensionTarget target)
        {
            switch (target)
            {
                case DimensionTarget.Main: Send("VXTPICKDIMMAIN "); break;
                case DimensionTarget.Furring: Send("VXTPICKDIMFURRING "); break;
                case DimensionTarget.Hanger: Send("VXTPICKDIMHANGER "); break;
            }
        }

        public void RequestPreview(VxtSettings settings)
        {
            VxtSession.Current.Settings = settings.Clone();
            VxtTransientPreview.Instance.Refresh();
        }

        public void ClearPreview() => VxtTransientPreview.Instance.Clear();

        public void RequestCreate()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage(
                "\nHNL Tool - VXT Pro v7.0.0-alpha.2: Tạo thật đang khóa cho tới khi Golden Verification với V6.7.4 hoàn tất.");
        }

        private static void Send(string command)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute(command, true, false, false);
        }
    }
}
