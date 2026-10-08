using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;
using HNL.VXT.UI.Hosting;

namespace HNL.VXT.AutoCAD
{
    internal sealed class VxtHostBridge : IVxtHostBridge
    {
        private readonly DispatcherTimer _previewTimer;
        private readonly DispatcherTimer _layInPreviewTimer;
        private VxtSettings _pendingPreviewSettings;
        private LayInCeilingSettings _pendingLayInPreviewSettings;
        private Autodesk.AutoCAD.ApplicationServices.Document _pendingLayInPreviewDocument;

        public VxtHostBridge()
        {
            _previewTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _previewTimer.Tick += PreviewTimer_Tick;

            _layInPreviewTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _layInPreviewTimer.Tick += LayInPreviewTimer_Tick;
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
            CancelPendingLayInPreview();
            Send("HNLVXTBOUNDARY ");
        }

        public void PickBoundaryPoint()
        {
            CancelPendingPreview();
            CancelPendingLayInPreview();
            Send("HNLVXTPICKBOUNDARYPOINT ");
        }

        public void PickDirection(MainDirectionMode mode)
        {
            CancelPendingPreview();
            VxtSession.Current.Settings.MainDirection = mode;
            switch (mode)
            {
                case MainDirectionMode.Auto: Send("HNLVXTAUTOSETUP "); break;
                case MainDirectionMode.TwoPoints:
                case MainDirectionMode.PolylinePath:
                    Send("HNLVXTDIRECTION ");
                    break;
                case MainDirectionMode.RectangleRegions: Send("HNLVXTREGION "); break;
                default: Write("\nHNL Tool - VXT Pro: Hướng hiện tại không cần thiết lập thêm."); break;
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

            // Modeless WPF callbacks run in AutoCAD application context. Never call Database or
            // TransientManager preview code directly from here. Interactive modes are queued as
            // AutoCAD commands so all graphics/database work executes on AutoCAD's command stack.
            if (settings.MainDirection != previousMode)
            {
                CancelPendingPreview();
                if (settings.MainDirection == MainDirectionMode.TwoPoints ||
                    settings.MainDirection == MainDirectionMode.PolylinePath)
                {
                    if (session.HasBoundary)
                        Send("HNLVXTCLEARPREVIEW HNLVXTDIRECTION ");
                    else
                    {
                        Send("HNLVXTCLEARPREVIEW ");
                        Write("\nHNL Tool - VXT Pro: Đã chọn 'Chọn hướng'. Hãy dùng Chọn Polyline hoặc Chọn điểm; sau khi nhận vùng trần HNL Tool sẽ cho chọn 2 điểm hoặc tuyến gấp khúc.");
                    }
                    return;
                }
                if (settings.MainDirection == MainDirectionMode.RectangleRegions)
                {
                    if (session.HasBoundary)
                        Send("HNLVXTCLEARPREVIEW HNLVXTREGION ");
                    else
                    {
                        Send("HNLVXTCLEARPREVIEW ");
                        Write("\nHNL Tool - VXT Pro: Đã chọn chế độ HCN. Hãy dùng Chọn Polyline hoặc Chọn điểm; sau khi nhận vùng trần HNL Tool sẽ vào chia vùng.");
                    }
                    return;
                }
            }

            // Numeric typing can produce several Value changes per second. Coalesce those changes
            // on the WPF dispatcher, but perform the actual preview inside HNLVXTPREVIEW.
            _pendingPreviewSettings = settings.Clone();
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        public void ClearPreview()
        {
            CancelPendingPreview();
            Send("HNLVXTCLEARPREVIEW ");
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

        public void HighlightBoundary(int boundaryIndex)
        {
            CancelPendingPreview();
            var session = VxtSession.Current;
            session.PendingBoundaryHighlightIndex = boundaryIndex;
            Send("HNLVXTFOCUSBOUNDARY ");
        }

        public void RequestCreate()
            => RequestCreateCore(allowConstraintOverride: false);

        public void RequestCreateWithWarning()
            => RequestCreateCore(allowConstraintOverride: true);

        public void RequestLayInPreview(LayInCeilingSettings settings)
        {
            if (settings == null) return;
            VxtSession.Current.LayInSettings = settings.Clone();
            _pendingLayInPreviewSettings = settings.Clone();
            _pendingLayInPreviewDocument = Application.DocumentManager.MdiActiveDocument;
            _layInPreviewTimer.Stop();
            _layInPreviewTimer.Start();
        }

        public void ClearLayInPreview()
        {
            CancelPendingLayInPreview();
            Send("HNLCFLAYINCLEAR ");
        }

        public void RequestLayInCreate(LayInCeilingSettings settings)
        {
            if (settings == null) return;
            CancelPendingLayInPreview();
            VxtSession.Current.LayInSettings = settings.Clone();
            Send("HNLCFLAYINCREATE ");
        }

        public void PickLayInManualStart(LayInCeilingSettings settings)
        {
            if (settings == null) return;
            CancelPendingLayInPreview();
            VxtSession.Current.LayInSettings = settings.Clone();
            Send("HNLCFLAYINSTART ");
        }

        public void PickLayInDoor(LayInCeilingSettings settings)
        {
            if (settings == null) return;
            CancelPendingLayInPreview();
            VxtSession.Current.LayInSettings = settings.Clone();
            Send("HNLCFLAYINDOOR ");
        }

        public void PickLayInHangerBlock(LayInCeilingSettings settings)
        {
            if (settings == null) return;
            CancelPendingLayInPreview();
            VxtSession.Current.LayInSettings = settings.Clone();
            Send("HNLCFLAYINHANGERBLOCK ");
        }

        public void PickLayInDimensionPosition(bool horizontal, LayInCeilingSettings settings)
        {
            if (settings == null) return;
            CancelPendingLayInPreview();
            VxtSession.Current.LayInSettings = settings.Clone();
            Send(horizontal ? "HNLCFLAYINDIMH " : "HNLCFLAYINDIMV ");
        }

        public void PickLayInStartMarkerBlock(LayInCeilingSettings settings)
        {
            if (settings == null) return;
            CancelPendingLayInPreview();
            VxtSession.Current.LayInSettings = settings.Clone();
            Send("HNLCFLAYINSTARTBLOCK ");
        }

        private void RequestCreateCore(bool allowConstraintOverride)
        {
            var session = VxtSession.Current;

            // WYSIWYG gate: a palette edit may have updated session.Settings immediately while the
            // transient redraw is still waiting inside the 180 ms debounce window. Creating at that
            // moment would use the new settings against an older on-screen Preview. Never allow
            // normal Create or manual-override Create to overtake a pending Preview.
            if (_pendingPreviewSettings != null)
            {
                _previewTimer.Stop();
                session.Settings = _pendingPreviewSettings.Clone();
                _pendingPreviewSettings = null;
                Send(session.HasBoundary ? "HNLVXTPREVIEW " : "HNLVXTCLEARPREVIEW ");
                Write("\nHNL Tool - VXT Pro: Preview vừa có thay đổi chưa kịp vẽ. HNL Tool đã cập nhật Preview trước; hãy kiểm tra rồi bấm Tạo lại để bảo đảm WYSIWYG.");
                return;
            }

            if (session.ViewModel != null)
                session.Settings = session.ViewModel.Snapshot();

            if (allowConstraintOverride)
            {
                var overrideable = session.ViewModel?.ConstraintDiagnostics
                    .Where(VxtConstraintOverridePolicy.IsManualOverrideAllowed)
                    .ToList() ?? new List<VxtConstraintDiagnostic>();

                // The warning button is driven by the latest Preview diagnostics. If those errors
                // have disappeared, use normal Create rather than manufacture an override state.
                if (overrideable.Count == 0)
                {
                    Send("HNLVXTCREATE ");
                    return;
                }
            }

            // Do not synchronously touch TransientManager from the palette callback. Both paths use
            // the same latest Snapshot and run entirely inside AutoCAD's queued command context.
            Send(allowConstraintOverride ? "HNLVXTCREATEWARN " : "HNLVXTCREATE ");
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            _previewTimer.Stop();
            if (_pendingPreviewSettings == null) return;

            // If a native/interactive AutoCAD command is active, do not inject preview work into it.
            // Keep only the latest settings and retry after another quiet interval.
            if (IsCadCommandActive())
            {
                _previewTimer.Start();
                return;
            }

            var settings = _pendingPreviewSettings;
            _pendingPreviewSettings = null;
            var session = VxtSession.Current;
            session.Settings = settings.Clone();
            Send(session.HasBoundary ? "HNLVXTPREVIEW " : "HNLVXTCLEARPREVIEW ");
        }

        private void LayInPreviewTimer_Tick(object sender, EventArgs e)
        {
            _layInPreviewTimer.Stop();
            if (_pendingLayInPreviewSettings == null) return;
            if (!ReferenceEquals(Application.DocumentManager.MdiActiveDocument, _pendingLayInPreviewDocument))
            {
                CancelPendingLayInPreview();
                return;
            }

            if (IsCadCommandActive())
            {
                _layInPreviewTimer.Start();
                return;
            }

            var settings = _pendingLayInPreviewSettings;
            _pendingLayInPreviewSettings = null;
            _pendingLayInPreviewDocument = null;
            var session = VxtSession.Current;
            session.LayInSettings = settings.Clone();
            Send(session.HasBoundary ? "HNLCFLAYINPREVIEW " : "HNLCFLAYINCLEAR ");
        }

        private void CancelPendingPreview()
        {
            _previewTimer.Stop();
            _pendingPreviewSettings = null;
        }

        private void CancelPendingLayInPreview()
        {
            _layInPreviewTimer.Stop();
            _pendingLayInPreviewSettings = null;
            _pendingLayInPreviewDocument = null;
        }

        private static bool IsCadCommandActive()
        {
            try { return Convert.ToInt32(Application.GetSystemVariable("CMDACTIVE")) != 0; }
            catch { return false; }
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
