using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.Hosting;
using Microsoft.Win32;

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

        public void RequestCreate()
        {
            var session = VxtSession.Current;
            if (session.ViewModel != null) session.Settings = session.ViewModel.Snapshot();
            Send("VXTCREATE ");
        }

        public void ExportDiagnostics(VxtSettings settings)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "HNL Tool - Xuất dữ liệu kiểm tra lỗi VXT",
                    Filter = "HNL VXT Diagnostic (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = "HNL-VXT-Diagnostic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    AddExtension = true,
                    DefaultExt = ".txt",
                    OverwritePrompt = true
                };
                if (dialog.ShowDialog() != true) return;

                var session = VxtSession.Current;
                var vm = session.ViewModel;
                var sb = new StringBuilder(4096);
                sb.AppendLine("HNL TOOL - VXT PRO DIAGNOSTIC");
                sb.AppendLine("========================================");
                sb.AppendLine("VXT Version: 7.0.0-alpha.5-production-engine");
                sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
                sb.AppendLine("OS: " + Environment.OSVersion);
                sb.AppendLine("CLR: " + Environment.Version);
                sb.AppendLine("64-bit process: " + Environment.Is64BitProcess);
                sb.AppendLine("AutoCAD ACADVER: " + SafeSystemVariable("ACADVER"));
                sb.AppendLine("AutoCAD Product: " + SafeAcadFileVersion());
                sb.AppendLine("Theme: " + (IsDarkTheme ? "Dark" : "Light"));
                sb.AppendLine("Drawing: " + SafeDrawingName(doc));
                sb.AppendLine();
                sb.AppendLine("[SESSION]");
                sb.AppendLine("Boundary: " + (session.HasBoundary ? "Selected" : "Not selected"));
                if (session.HasBoundary)
                {
                    sb.AppendLine("Boundary vertices: " + session.Boundary.Vertices.Count);
                    var bounds = session.Boundary.GetBounds();
                    sb.AppendLine("Boundary min: " + bounds.Min.X.ToString("0.###") + ", " + bounds.Min.Y.ToString("0.###"));
                    sb.AppendLine("Boundary max: " + bounds.Max.X.ToString("0.###") + ", " + bounds.Max.Y.ToString("0.###"));
                }
                sb.AppendLine("Manual regions: " + session.Regions.Count);
                sb.AppendLine("Preview summary: " + (vm?.Summary ?? "N/A"));
                sb.AppendLine("Preview status: " + (vm?.PreviewStatus ?? "N/A"));
                sb.AppendLine("Boundary status: " + (vm?.BoundaryStatus ?? "N/A"));
                sb.AppendLine("Equipment general/main/furring: " + session.GeneralEquipmentIds.Length + "/" + session.MainEquipmentIds.Length + "/" + session.FurringEquipmentIds.Length);
                sb.AppendLine();
                AppendSettings(sb, settings);
                AppendDrawingResources(sb, doc.Database, settings);
                File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                Write("\nHNL Tool - VXT Pro: Đã xuất file kiểm tra lỗi: " + dialog.FileName);
            }
            catch (System.Exception ex)
            {
                Write("\nHNL Tool - VXT Pro: Không thể xuất file kiểm tra lỗi: " + ex.Message);
            }
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

        private static void AppendSettings(StringBuilder sb, VxtSettings s)
        {
            sb.AppendLine("[SETTINGS]");
            sb.AppendLine("Main enabled: " + s.DrawMain);
            sb.AppendLine("Main direction/layout/angle: " + s.MainDirection + " / " + s.MainLayout + " / " + s.DirectionDegrees.ToString("0.###"));
            sb.AppendLine("Main spacing min/max: " + s.MainMinSpacing + " / " + s.MainMaxSpacing);
            sb.AppendLine("Main edge min/max: " + s.MainMinEdgeOffset + " / " + s.MainMaxEdgeOffset);
            sb.AppendLine("Main balance/skip: " + s.MainBalanceStep + " / " + s.MainSkipLimit);
            sb.AppendLine("Main block: " + s.MainBlockName + " | Dynamic=" + s.UseDynamicMainBlock);
            sb.AppendLine("Main layer/linetype/lineweight/color: " + s.MainLayer + " / " + s.MainLinetype + " / " + s.MainLineweight + " / " + s.MainColorIndex);
            sb.AppendLine("Furring enabled/spacing: " + s.DrawFurring + " / " + s.FurringSpacing);
            sb.AppendLine("Furring block: " + s.FurringBlockName + " | Dynamic=" + s.UseDynamicFurringBlock);
            sb.AppendLine("Furring layer/linetype/lineweight/color: " + s.FurringLayer + " / " + s.FurringLinetype + " / " + s.FurringLineweight + " / " + s.FurringColorIndex);
            sb.AppendLine("Ask direction each region: " + s.AskDirectionEachRegion);
            sb.AppendLine("Hanger enabled/layout: " + s.DrawHangers + " / " + s.HangerLayout);
            sb.AppendLine("Hanger spacing min/max: " + s.HangerMinSpacing + " / " + s.HangerMaxSpacing);
            sb.AppendLine("Hanger edge min/max: " + s.HangerMinEdgeOffset + " / " + s.HangerMaxEdgeOffset);
            sb.AppendLine("Hanger balance: " + s.HangerBalanceStep);
            sb.AppendLine("Hanger block: " + s.HangerBlockName);
            sb.AppendLine("Hanger layer/linetype/lineweight/color: " + s.HangerLayer + " / " + s.HangerLinetype + " / " + s.HangerLineweight + " / " + s.HangerColorIndex);
            sb.AppendLine("Avoidance enabled/shift all/clearance: " + s.UseAvoidance + " / " + s.ShiftAllForAvoidance + " / " + s.ClearanceDistance);
            sb.AppendLine("DIM enabled main/furring/hanger: " + s.AutoDimension + " / " + s.DimMain + " / " + s.DimFurring + " / " + s.DimHanger);
            sb.AppendLine("DIM positions: " + s.MainDimPosition + " / " + s.FurringDimPosition + " / " + s.HangerDimPosition);
            sb.AppendLine("DIM distance/spacing: " + s.DimensionDistance + " / " + s.DimensionSpacing);
            sb.AppendLine("DIM layer/linetype/lineweight/color: " + s.DimensionLayer + " / " + s.DimensionLinetype + " / " + s.DimensionLineweight + " / " + s.DimensionColorIndex);
            sb.AppendLine("DIM style: " + (string.IsNullOrWhiteSpace(s.DimensionStyle) ? "<Current>" : s.DimensionStyle));
            sb.AppendLine();
        }

        private static void AppendDrawingResources(StringBuilder sb, Database db, VxtSettings s)
        {
            sb.AppendLine("[DRAWING RESOURCE CHECK]");
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    var dst = tr.GetObject(db.DimStyleTableId, OpenMode.ForRead) as DimStyleTable;
                    sb.AppendLine("Main block exists: " + Has(bt, s.MainBlockName));
                    sb.AppendLine("Furring block exists: " + Has(bt, s.FurringBlockName));
                    sb.AppendLine("Hanger block exists: " + Has(bt, s.HangerBlockName));
                    sb.AppendLine("Main layer exists: " + Has(lt, s.MainLayer));
                    sb.AppendLine("Furring layer exists: " + Has(lt, s.FurringLayer));
                    sb.AppendLine("Hanger layer exists: " + Has(lt, s.HangerLayer));
                    sb.AppendLine("DIM layer exists: " + Has(lt, s.DimensionLayer));
                    sb.AppendLine("DIM style exists/current: " + (string.IsNullOrWhiteSpace(s.DimensionStyle) ? "Current" : Has(dst, s.DimensionStyle).ToString()));
                    tr.Commit();
                }
            }
            catch (System.Exception ex) { sb.AppendLine("Resource check error: " + ex.Message); }
            sb.AppendLine();
        }

        private static bool Has(SymbolTable table, string name)
        {
            try { return table != null && !string.IsNullOrWhiteSpace(name) && table.Has(name); }
            catch { return false; }
        }

        private static string SafeSystemVariable(string name)
        {
            try { return Convert.ToString(Application.GetSystemVariable(name)); }
            catch { return "N/A"; }
        }

        private static string SafeAcadFileVersion()
        {
            try
            {
                var fileName = Process.GetCurrentProcess().MainModule?.FileName;
                return string.IsNullOrWhiteSpace(fileName) ? "N/A" : FileVersionInfo.GetVersionInfo(fileName).FileVersion;
            }
            catch { return "N/A"; }
        }

        private static string SafeDrawingName(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            try { return doc.Name ?? "N/A"; }
            catch { return "N/A"; }
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
