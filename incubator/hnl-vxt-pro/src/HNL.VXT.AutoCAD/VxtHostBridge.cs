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

        public void PickDirection(MainDirectionMode mode)
        {
            switch (mode)
            {
                case MainDirectionMode.TwoPoints:
                    Send("VXTPICKDIRECTION ");
                    break;
                case MainDirectionMode.RectangleRegions:
                    Send("VXTRECTDIRECTION ");
                    break;
                default:
                    Write("\nHNL Tool - VXT Pro: Hướng hiện tại không cần chọn điểm trên CAD.");
                    break;
            }
        }

        public void PickBlock(BlockTarget target)
        {
            switch (target)
            {
                case BlockTarget.Main: Send("VXTPICKMAINBLOCK "); break;
                case BlockTarget.Furring: Send("VXTPICKFURRINGBLOCK "); break;
                case BlockTarget.Hanger: Send("VXTPICKHANGERBLOCK "); break;
            }
        }

        public void PickEquipment(EquipmentTarget target)
        {
            switch (target)
            {
                case EquipmentTarget.General: Send("VXTPICKEQUIPGENERAL "); break;
                case EquipmentTarget.Main: Send("VXTPICKEQUIPMAIN "); break;
                case EquipmentTarget.Furring: Send("VXTPICKEQUIPFURRING "); break;
            }
        }

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
            Write("\nHNL Tool - VXT Pro v7.0.0-alpha.2: Tạo thật đang khóa cho tới khi Golden Verification với V6.7.4 hoàn tất.");
        }

        private static void Send(string command)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute(command, true, false, false);
        }

        private static void Write(string message)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage(message);
        }
    }
}
