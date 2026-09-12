using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.Hosting;

namespace HNL.VXT.AutoCAD
{
    internal sealed class VxtHostBridge : IVxtHostBridge
    {
        private readonly DispatcherTimer _previewTimer;
        private VxtSettings _pendingPreviewSettings;

        public VxtHostBridge()
        {
            _previewTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _previewTimer.Tick += PreviewTimer_Tick;
        }

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

        // Optional UI parity hook. VxtCommands already stores the source BlockReference layer
        // in session settings; the compact palette reads it after SetBlock raises PropertyChanged.
        public string GetSelectedBlockLayer(BlockTarget target)
        {
            var settings = VxtSession.Current.Settings;
            if (settings == null) return string.Empty;
            switch (target)
            {
                case BlockTarget.Main: return settings.MainLayer ?? string.Empty;
                case BlockTarget.Furring: return settings.FurringLayer ?? string.Empty;
                case BlockTarget.Hanger: return settings.HangerLayer ?? string.Empty;
                default: return string.Empty;
            }
        }

        public void SelectBoundary()
        {
            CancelPendingPreview();
            switch (VxtSession.Current.Settings.MainDirection)
            {
                case MainDirectionMode.TwoPoints:
                    Send("HNLVXTBOUNDARY HNLVXTDIRECTION ");
                    break;
                case MainDirectionMode.RectangleRegions:
                    Send("HNLVXTBOUNDARY HNLVXTREGION ");
                    break;
                default:
                    Send("HNLVXTBOUNDARY ");
                    break;
            }
        }

        public void PickDirection(MainDirectionMode mode)
        {
            CancelPendingPreview();
            VxtSession.Current.Settings.MainDirection = mode;
            switch (mode)
            {
                case MainDirectionMode.TwoPoints: Send("HNLVXTDIRECTION "); break;
                case MainDirectionMode.RectangleRegions: Send("HNLVXTREGION "); break;
                default: Write("\nHNL Tool - VXT Pro: Hướng hiện tại không cần chọn điểm trên CAD."); break;
            }
        }

        public void PickBlock(BlockTarget target)
        {
            CancelPendingPreview();
            switch (target)
            {
                case BlockTarget.Main: Send("HNLVXTPICKMAIN "); break;
                case BlockTarget.Furring: Send("HNLVXTPICKFURRING "); break;
                case BlockTarget.Hanger: Send("HNLVXTPICKHANGER "); break;
            }
        }

        public void PickEquipment(EquipmentTarget target)
        {
            CancelPendingPreview();
            switch (target)
            {
                case EquipmentTarget.General: Send("HNLVXTMEP "); break;
                case EquipmentTarget.Main: Send("HNLVXTMEPMAIN "); break;
                case EquipmentTarget.Furring: Send("HNLVXTMEPFURRING "); break;
            }
        }

        public void PickDimensionPosition(DimensionTarget target)
        {
            CancelPendingPreview();
            switch (target)
            {
                case DimensionTarget.Main: Send("HNLVXTDIMMAIN "); break;
                case DimensionTarget.Furring: Send("HNLVXTDIMFURRING "); break;
                case DimensionTarget.Hanger: Send("HNLVXTDIMHANGER "); break;
            }
        }

        public void RequestPreview(VxtSettings settings)
        {
            var session = VxtSession.Current;
            var previousMode = session.Settings?.MainDirection ?? MainDirectionMode.Horizontal;
            session.Settings = settings.Clone();

            // Interactive CAD pick modes must start immediately and are never debounced.
            if (settings.MainDirection != previousMode)
            {
                CancelPendingPreview();
                if (settings.MainDirection == MainDirectionMode.TwoPoints)
                {
                    VxtTransientPreview.Instance.Clear();
                    if (session.HasBoundary)
                        Send("HNLVXTDIRECTION ");
                    else
                        Write("\nHNL Tool - VXT Pro: Đã chọn hướng 2 điểm. Hãy chọn Polyline biên trần; HNL Tool sẽ yêu cầu 2 điểm ngay sau đó.");
                    return;
                }
                if (settings.MainDirection == MainDirectionMode.RectangleRegions)
                {
                    VxtTransientPreview.Instance.Clear();
                    if (session.HasBoundary)
                        Send("HNLVXTREGION ");
                    else
                        Write("\nHNL Tool - VXT Pro: Đã chọn chế độ HCN. Hãy chọn Polyline biên trần; HNL Tool sẽ vào chia vùng ngay sau đó.");
                    return;
                }
            }

            // Numeric typing can produce several Value changes per second. Rebuilding hundreds
            // of transients for every keystroke caused the palette to feel laggy on large floors.
            // Coalesce those changes and render only the latest state after 180 ms of quiet time.
            _pendingPreviewSettings = settings.Clone();
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        public void ClearPreview()
        {
            CancelPendingPreview();
            VxtTransientPreview.Instance.Clear();
        }

        public string AnalyzeDiagnostics(VxtSettings settings)
        {
            CancelPendingPreview();
            VxtSession.Current.Settings = settings.Clone();
            VxtDiagnosticService.AnalyzeAndReport(settings);
            return VxtDiagnosticService.LastAnalysis;
        }

        public string ExportDiagnostics(VxtSettings settings)
        {
            CancelPendingPreview();
            VxtSession.Current.Settings = settings.Clone();
            return VxtDiagnosticService.ExportInteractive(settings);
        }

        public void RequestRuntimeGolden()
        {
            CancelPendingPreview();
            Send("HNLVXTGOLDEN ");
        }

        public void RequestCreate()
        {
            CancelPendingPreview();
            var session = VxtSession.Current;
            if (session.ViewModel != null)
                session.Settings = session.ViewModel.Snapshot();

            // WYSIWYG flush: Preview normally waits 180 ms after typing. If the user changes a
            // setting and immediately clicks Create, render the exact Snapshot synchronously before
            // the AutoCAD command is queued. This guarantees the last visible Preview uses the same
            // settings that HNLVXTCREATE is about to consume.
            if (session.HasBoundary)
                VxtTransientPreview.Instance.Refresh();

            Send("HNLVXTCREATE ");
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            _previewTimer.Stop();
            var settings = _pendingPreviewSettings;
            _pendingPreviewSettings = null;
            if (settings == null) return;

            var session = VxtSession.Current;
            session.Settings = settings.Clone();
            if (session.HasBoundary) VxtTransientPreview.Instance.Refresh();
        }

        private void CancelPendingPreview()
        {
            _previewTimer.Stop();
            _pendingPreviewSettings = null;
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
