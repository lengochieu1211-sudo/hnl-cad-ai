using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
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

        public string[] GetLinetypeNames()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc == null ? Array.Empty<string>() : ReadSymbolNames(doc.Database, doc.Database.LinetypeTableId);
        }

        public string[] GetDimStyleNames()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc == null ? Array.Empty<string>() : ReadSymbolNames(doc.Database, doc.Database.DimStyleTableId);
        }

        public void SelectBoundary() => Send("VXTSELECTBOUNDARY ");

        public void PickDirection(MainDirectionMode mode)
        {
            switch (mode)
            {
                case MainDirectionMode.TwoPoints: Send("VXTPICKDIRECTION "); break;
                case MainDirectionMode.RectangleRegions: Send("VXTRECTDIRECTION "); break;
                default: Write("\nHNL Tool - VXT Pro: Hướng hiện tại không cần chọn điểm trên CAD."); break;
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

        public void AnalyzeDiagnostics(VxtSettings settings)
        {
            VxtSession.Current.Settings = settings.Clone();
            VxtDiagnosticService.AnalyzeAndReport(settings);
        }

        public void ExportDiagnostics(VxtSettings settings)
        {
            VxtSession.Current.Settings = settings.Clone();
            VxtDiagnosticService.ExportInteractive(settings);
        }

        public void RequestCreate()
        {
            var session = VxtSession.Current;
            if (session.ViewModel != null) session.Settings = session.ViewModel.Snapshot();
            Send("VXTCREATE ");
        }

        private static string[] ReadSymbolNames(Database db, ObjectId tableId)
        {
            var names = new List<string>();
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var table = tr.GetObject(tableId, OpenMode.ForRead) as SymbolTable;
                    if (table != null)
                    {
                        foreach (ObjectId id in table)
                        {
                            var record = tr.GetObject(id, OpenMode.ForRead) as SymbolTableRecord;
                            if (record != null && !string.IsNullOrWhiteSpace(record.Name)) names.Add(record.Name);
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
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
