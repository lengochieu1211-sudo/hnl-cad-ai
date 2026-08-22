using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.PublishingServices;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace Hnl.CadBridge;

public sealed class BridgeCommands : IExtensionApplication
{
    private static readonly HttpClient Http = new HttpClient();
    private static readonly ConcurrentQueue<JObject> UiActions = new ConcurrentQueue<JObject>();
    private static Timer? _pollTimer;
    private static string? _baseUrl;
    private static string? _token;
    private static bool _registered;

    public void Initialize()
    {
        TryLoadPairing();
        Application.Idle += OnIdle;
        _pollTimer = new Timer(_ => PollServer(), null, 500, 750);
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nHNL CAD AI Bridge v2.4.0 loaded. Commands: HNLBRIDGESTATUS, HNLBRIDGEPING, HNLPLOTDEVICES, HNLLAYOUTS");
    }

    public void Terminate()
    {
        Application.Idle -= OnIdle;
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    [CommandMethod("HNLBRIDGESTATUS")]
    public void BridgeStatusCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var ed = doc?.Editor;
        TryLoadPairing();
        ed?.WriteMessage($"\nHNL Bridge • Pairing: {(string.IsNullOrWhiteSpace(_baseUrl) ? "NOT FOUND" : _baseUrl)} • Registered: {_registered} • Drawing: {doc?.Name ?? "(none)"}");
    }

    [CommandMethod("HNLBRIDGEPING")]
    public void BridgePingCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        TryLoadPairing();
        doc?.Editor.WriteMessage(string.IsNullOrWhiteSpace(_baseUrl)
            ? "\nHNL Bridge: chưa tìm thấy pairing file. Hãy mở HNL CAD AI trước."
            : $"\nHNL Bridge: pairing OK → {_baseUrl}. Poll/heartbeat chạy nền.");
    }

    [CommandMethod("HNLPLOTDEVICES")]
    public void PlotDevicesCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var devices = PlotSettingsValidator.Current.GetPlotDeviceList().Cast<string>().ToArray();
        doc?.Editor.WriteMessage($"\nHNL Plot Devices ({devices.Length}):\n - {string.Join("\n - ", devices)}");
    }

    [CommandMethod("HNLLAYOUTS")]
    public void LayoutsCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var names = new List<string>();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var dict = (DBDictionary)tr.GetObject(doc.Database.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in dict)
            {
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                names.Add(layout.LayoutName);
            }
            tr.Commit();
        }
        doc.Editor.WriteMessage($"\nHNL Layouts ({names.Count}): {string.Join(", ", names)}");
    }

    [CommandMethod("HNLSELECTION")]
    public void SelectionCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var implied = doc.Editor.SelectImplied();
        var ids = implied.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK && implied.Value != null
            ? implied.Value.GetObjectIds()
            : Array.Empty<ObjectId>();
        doc.Editor.WriteMessage($"\nHNL Selection: {ids.Length} object(s).");
    }

    [CommandMethod("HNLLAYERS")]
    public void LayersCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var names = new List<string>();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var table = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                names.Add(layer.Name);
            }
            tr.Commit();
        }
        doc.Editor.WriteMessage($"\nHNL Layers ({names.Count}): {string.Join(", ", names.Take(30))}{(names.Count > 30 ? " ..." : "")}");
    }

    private static void TryLoadPairing()
    {
        try
        {
            var file = Path.Combine(Path.GetTempPath(), "HNL_CAD_AI", "bridge.json");
            if (!File.Exists(file)) return;
            var j = JObject.Parse(File.ReadAllText(file));
            var host = (string?)j["host"] ?? "127.0.0.1";
            var port = (int?)j["port"] ?? 32145;
            var nextToken = (string?)j["token"];
            var nextBaseUrl = $"http://{host}:{port}";
            if (!string.Equals(_token, nextToken, StringComparison.Ordinal) ||
                !string.Equals(_baseUrl, nextBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                _registered = false;
            }
            _token = nextToken;
            _baseUrl = nextBaseUrl;
        }
        catch { _baseUrl = null; _token = null; }
    }

    private static HttpRequestMessage MakeRequest(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, (_baseUrl ?? "") + path);
        if (!string.IsNullOrWhiteSpace(_token)) req.Headers.TryAddWithoutValidation("x-hnl-token", _token);
        if (body != null) req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        return req;
    }

    private static async void PollServer()
    {
        try
        {
            // Re-read pairing every cycle so HNL can start/restart after AutoCAD without NETLOAD/restart.
            TryLoadPairing();
            if (string.IsNullOrWhiteSpace(_baseUrl)) return;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (!_registered)
            {
                using var req = MakeRequest(HttpMethod.Post, "/api/autocad/register", new {
                    version = Application.Version.ToString(),
                    drawingName = doc?.Name ?? "",
                    pluginVersion = "2.4.0",
                    capabilities = new[] { "GET_STATUS","GET_PLOT_DEVICES","GET_LAYOUTS","EXECUTE_COMMAND","CANCEL_COMMAND","OPEN_DWG","SAVE_CURRENT_DWG","SAVE_AS_DWG","GET_SELECTION","SELECT_ALL","GET_LAYERS","PUBLISH_LAYOUTS_PDF","PLOT_CURRENT_PDF","SAVE_DXF_AS_DWG","GET_SHEETSET_INFO","UPDATE_SHEET" }
                });
                var res = await Http.SendAsync(req);
                _registered = res.IsSuccessStatusCode;
                if (!_registered) return;
            }
            else
            {
                using var hb = MakeRequest(HttpMethod.Post, "/api/autocad/heartbeat", new { drawingName = doc?.Name ?? "" });
                await Http.SendAsync(hb);
            }

            using var poll = MakeRequest(HttpMethod.Get, "/api/autocad/poll");
            var pollRes = await Http.SendAsync(poll);
            if (!pollRes.IsSuccessStatusCode) return;
            var text = await pollRes.Content.ReadAsStringAsync();
            var root = JObject.Parse(text);
            if (root["item"] is JObject item) UiActions.Enqueue(item);
        }
        catch { _registered = false; }
    }

    private static void OnIdle(object? sender, EventArgs e)
    {
        if (!UiActions.TryDequeue(out var item)) return;
        ExecuteQueuedAction(item);
    }

    private static async void SendResult(string id, bool ok, object? result = null, string? error = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) return;
            using var req = MakeRequest(HttpMethod.Post, "/api/autocad/result", new { id, ok, result, error });
            await Http.SendAsync(req);
        }
        catch { }
    }

    private static void ExecuteQueuedAction(JObject item)
    {
        var id = (string?)item["id"] ?? "";
        var action = ((string?)item["action"] ?? "").ToUpperInvariant();
        var payload = item["payload"] as JObject ?? new JObject();
        try
        {
            object result = action switch
            {
                "GET_STATUS" => GetStatusPayload(),
                "GET_PLOT_DEVICES" => GetPlotDevicesPayload(),
                "GET_LAYOUTS" => GetLayoutsPayload(),
                "EXECUTE_COMMAND" => ExecuteNativeCommand(payload),
                "CANCEL_COMMAND" => CancelNativeCommand(),
                "OPEN_DWG" => OpenDwg(payload),
                "SAVE_CURRENT_DWG" => SaveCurrentDwg(),
                "SAVE_AS_DWG" => SaveAsDwg(payload),
                "GET_SELECTION" => GetSelectionPayload(),
                "SELECT_ALL" => SelectAllObjects(),
                "GET_LAYERS" => GetLayersPayload(),
                "PUBLISH_LAYOUTS_PDF" => PublishLayoutsPdf(payload),
                "PLOT_CURRENT_PDF" => PlotCurrentLayoutPdf(payload),
                "SAVE_DXF_AS_DWG" => SaveDxfAsDwg(payload),
                "GET_SHEETSET_INFO" => GetSheetSetInfo(payload),
                "UPDATE_SHEET" => UpdateSheet(payload),
                _ => throw new InvalidOperationException($"Unsupported bridge action: {action}")
            };
            SendResult(id, true, result);
        }
        catch (System.Exception ex)
        {
            SendResult(id, false, null, ex.ToString());
        }
    }

    private static object GetStatusPayload()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        return new { connected = true, version = Application.Version.ToString(), drawingName = doc?.Name ?? "", pluginVersion = "2.4.0" };
    }

    private static object GetPlotDevicesPayload()
    {
        var validator = PlotSettingsValidator.Current;
        return new {
            devices = validator.GetPlotDeviceList().Cast<string>().ToArray(),
            styles = validator.GetPlotStyleSheetList().Cast<string>().ToArray()
        };
    }

    private static object GetLayoutsPayload()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var list = new List<object>();
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var dict = (DBDictionary)tr.GetObject(doc.Database.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in dict)
            {
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                list.Add(new {
                    name = layout.LayoutName,
                    modelType = layout.ModelType,
                    device = layout.PlotConfigurationName,
                    media = layout.CanonicalMediaName,
                    style = layout.CurrentStyleSheet,
                    rotation = layout.PlotRotation.ToString(),
                    plotType = layout.PlotType.ToString(),
                    scale = layout.UseStandardScale ? layout.StdScaleType.ToString() : layout.CustomPrintScale.ToString()
                });
            }
            tr.Commit();
        }
        return new { layouts = list };
    }


    private static object ExecuteNativeCommand(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var command = ((string?)payload["command"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("command required");
        if (command.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
            throw new InvalidOperationException("Only one native command name is allowed.");
        if (!command.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.'))
            throw new InvalidOperationException("Unsafe characters in command name.");
        doc.SendStringToExecute($"_.{command} ", true, false, true);
        return new { queued = true, command, drawingName = doc.Name };
    }

    private static object CancelNativeCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        doc.SendStringToExecute("\x1b\x1b", true, false, true);
        return new { queued = true, command = "ESC ESC", drawingName = doc.Name };
    }

    private static object OpenDwg(JObject payload)
    {
        var filePath = (string?)payload["filePath"] ?? throw new ArgumentException("filePath required");
        if (!File.Exists(filePath)) throw new FileNotFoundException("DWG not found.", filePath);
        if (!string.Equals(Path.GetExtension(filePath), ".dwg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OPEN_DWG only accepts .dwg.");
        var doc = Application.DocumentManager.Open(filePath, false);
        Application.DocumentManager.MdiActiveDocument = doc;
        return new { opened = true, filePath, drawingName = doc.Name };
    }

    private static object SaveCurrentDwg()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var filePath = doc.Name;
        if (string.IsNullOrWhiteSpace(filePath)) throw new InvalidOperationException("Active drawing has no file path. Use SAVE_AS_DWG.");
        using (doc.LockDocument())
        {
            doc.Database.SaveAs(filePath, DwgVersion.Current);
        }
        return new { saved = true, filePath, bytes = File.Exists(filePath) ? new FileInfo(filePath).Length : 0L };
    }

    private static object SaveAsDwg(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var outputPath = (string?)payload["outputPath"] ?? throw new ArgumentException("outputPath required");
        if (!string.Equals(Path.GetExtension(outputPath), ".dwg", StringComparison.OrdinalIgnoreCase))
            outputPath = Path.ChangeExtension(outputPath, ".dwg");
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        using (doc.LockDocument())
        {
            doc.Database.SaveAs(outputPath, DwgVersion.Current);
        }
        return new { saved = true, outputPath, bytes = new FileInfo(outputPath).Length, dwgVersion = DwgVersion.Current.ToString() };
    }

    private static object GetSelectionPayload()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var editor = doc.Editor;
        var implied = editor.SelectImplied();
        if (implied.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK || implied.Value == null)
            return new { count = 0, entities = Array.Empty<object>() };

        var result = new List<object>();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            foreach (var id in implied.Value.GetObjectIds())
            {
                if (!id.IsValid || id.IsErased) continue;
                var ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (ent == null) continue;
                result.Add(new {
                    handle = ent.Handle.ToString(),
                    type = ent.GetType().Name,
                    dxfName = ent.GetRXClass()?.DxfName ?? "",
                    layer = ent.Layer,
                    colorIndex = ent.ColorIndex
                });
            }
            tr.Commit();
        }
        return new { count = result.Count, entities = result };
    }

    private static object SelectAllObjects()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var selected = doc.Editor.SelectAll();
        if (selected.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK || selected.Value == null)
        {
            doc.Editor.SetImpliedSelection(Array.Empty<ObjectId>());
            return new { count = 0 };
        }
        var ids = selected.Value.GetObjectIds();
        doc.Editor.SetImpliedSelection(ids);
        return new { count = ids.Length };
    }

    private static object GetLayersPayload()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var result = new List<object>();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var table = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                result.Add(new {
                    name = layer.Name,
                    isOff = layer.IsOff,
                    isFrozen = layer.IsFrozen,
                    isLocked = layer.IsLocked,
                    colorIndex = layer.Color.ColorIndex,
                    linetypeObjectId = layer.LinetypeObjectId.ToString()
                });
            }
            tr.Commit();
        }
        return new { layers = result };
    }

    private static object PublishLayoutsPdf(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var output = (string?)payload["outputPath"] ?? throw new ArgumentException("outputPath required");
        var requested = payload["layouts"]?.Values<string>().Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (requested.Length == 0) throw new ArgumentException("layouts required");
        if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting) throw new InvalidOperationException("AutoCAD is already plotting.");

        var oldBg = Application.GetSystemVariable("BACKGROUNDPLOT");
        try
        {
            Application.SetSystemVariable("BACKGROUNDPLOT", 0);
            using var entries = new DsdEntryCollection();
            foreach (var layoutName in requested)
            {
                using var entry = new DsdEntry();
                entry.DwgName = doc.Name;
                entry.Layout = layoutName;
                entry.Title = layoutName;
                entry.Nps = "";
                entry.NpsSourceDwg = "";
                entries.Add(entry);
            }
            using var data = new DsdData();
            data.SetDsdEntryCollection(entries);
            data.DestinationName = output;
            data.ProjectPath = Path.GetDirectoryName(output) ?? "";
            data.LogFilePath = Path.ChangeExtension(output, ".publish.log");
            data.SheetType = SheetType.MultiPdf;

            using var cfg = PlotConfigManager.SetCurrentConfig("DWG To PDF.pc3");
            Application.Publisher.PublishExecute(data, cfg);
            return new { outputPath = output, layouts = requested, logFile = data.LogFilePath };
        }
        finally
        {
            try { Application.SetSystemVariable("BACKGROUNDPLOT", oldBg); } catch { }
        }
    }

    private static object PlotCurrentLayoutPdf(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var output = (string?)payload["outputPath"] ?? throw new ArgumentException("outputPath required");
        var layoutName = LayoutManager.Current.CurrentLayout;
        return PublishLayoutsPdf(new JObject {
            ["outputPath"] = output,
            ["layouts"] = new JArray(layoutName)
        });
    }

    private static object SaveDxfAsDwg(JObject payload)
    {
        var inputPath = (string?)payload["inputPath"] ?? throw new ArgumentException("inputPath required");
        var outputPath = (string?)payload["outputPath"] ?? throw new ArgumentException("outputPath required");
        if (!File.Exists(inputPath)) throw new FileNotFoundException("DXF input not found.", inputPath);
        if (!string.Equals(Path.GetExtension(outputPath), ".dwg", StringComparison.OrdinalIgnoreCase))
            outputPath = Path.ChangeExtension(outputPath, ".dwg");

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir)) Directory.CreateDirectory(outputDir);

        // Convert the HNL-generated DXF through Autodesk's native database engine.
        // This produces a real DWG file; it is not an extension rename.
        using var db = new Database(false, true);
        var logPath = Path.Combine(Path.GetTempPath(), $"HNL_DXF_IN_{Guid.NewGuid():N}.log");
        try
        {
            db.DxfIn(inputPath, logPath);
            db.SaveAs(outputPath, DwgVersion.Current);
        }
        finally
        {
            try { if (File.Exists(logPath)) File.Delete(logPath); } catch { }
        }

        return new {
            outputPath,
            bytes = new FileInfo(outputPath).Length,
            dwgVersion = DwgVersion.Current.ToString()
        };
    }


    private static dynamic CreateSheetSetManager()
    {
        var progIds = new[] {
            "AcSmComponents.AcSmSheetSetMgr",
            "AcSmComponents.AcSmSheetSetMgr.26",
            "AcSmComponents.AcSmSheetSetMgr.25",
            "AcSmComponents.AcSmSheetSetMgr.24",
            "AcSmComponents.AcSmSheetSetMgr.23",
            "AcSmComponents.AcSmSheetSetMgr.22",
            "AcSmComponents.AcSmSheetSetMgr.21"
        };
        foreach (var progId in progIds)
        {
            try
            {
                var t = Type.GetTypeFromProgID(progId);
                if (t != null) return Activator.CreateInstance(t)!;
            }
            catch { }
        }
        throw new InvalidOperationException("Không tạo được AcSmSheetSetMgr COM object. Hãy kiểm tra AutoCAD legacy Sheet Set Manager.");
    }

    private static void WalkSheetComponents(dynamic subset, string subsetPath, List<object> sheets)
    {
        dynamic en = subset.GetSheetEnumerator();
        while (true)
        {
            dynamic comp = en.Next();
            if (comp == null) break;
            string typeName = "";
            try { typeName = (string)comp.GetTypeName(); } catch { }
            if (string.Equals(typeName, "AcSmSubset", StringComparison.OrdinalIgnoreCase))
            {
                string name = "";
                try { name = (string)comp.GetName(); } catch { }
                WalkSheetComponents(comp, string.IsNullOrWhiteSpace(subsetPath) ? name : subsetPath + "/" + name, sheets);
            }
            else if (string.Equals(typeName, "AcSmSheet", StringComparison.OrdinalIgnoreCase))
            {
                string number = "", title = "", desc = "";
                try { number = (string)comp.GetNumber(); } catch { }
                try { title = (string)comp.GetTitle(); } catch { try { title = (string)comp.GetName(); } catch { } }
                try { desc = (string)comp.GetDesc(); } catch { }
                string dwg = "", layout = "";
                try
                {
                    dynamic layoutRef = comp.GetLayout();
                    if (layoutRef != null)
                    {
                        try { dwg = (string)layoutRef.GetFileName(); } catch { }
                        try { layout = (string)layoutRef.GetName(); } catch { }
                    }
                }
                catch { }
                sheets.Add(new { number, title, description = desc, subset = subsetPath, dwgPath = dwg, layoutName = layout });
            }
        }
    }

    private static object GetSheetSetInfo(JObject payload)
    {
        var filePath = (string?)payload["filePath"] ?? throw new ArgumentException("filePath required");
        if (!File.Exists(filePath)) throw new FileNotFoundException("DST not found", filePath);
        dynamic mgr = CreateSheetSetManager();
        dynamic? db = null;
        try
        {
            db = mgr.OpenDatabase(filePath, true);
            dynamic ss = db.GetSheetSet();
            string name = "", desc = "";
            try { name = (string)ss.GetName(); } catch { name = Path.GetFileNameWithoutExtension(filePath); }
            try { desc = (string)ss.GetDesc(); } catch { }
            var sheets = new List<object>();
            WalkSheetComponents(ss, "", sheets);
            return new { filePath, name, description = desc, sheets };
        }
        finally
        {
            try { if (db != null) mgr.Close(db); } catch { }
        }
    }

    private static object UpdateSheet(JObject payload)
    {
        var filePath = (string?)payload["filePath"] ?? throw new ArgumentException("filePath required");
        var oldNumber = (string?)payload["oldNumber"] ?? "";
        var newNumber = (string?)payload["number"];
        var newTitle = (string?)payload["title"];
        dynamic mgr = CreateSheetSetManager();
        dynamic? db = null;
        bool locked = false, changed = false;
        try
        {
            db = mgr.OpenDatabase(filePath, false);
            db.LockDb(db); locked = true;
            dynamic ss = db.GetSheetSet();
            var stack = new Stack<dynamic>(); stack.Push(ss);
            while (stack.Count > 0)
            {
                dynamic subset = stack.Pop();
                dynamic en = subset.GetSheetEnumerator();
                while (true)
                {
                    dynamic comp = en.Next(); if (comp == null) break;
                    string typeName = ""; try { typeName = (string)comp.GetTypeName(); } catch { }
                    if (string.Equals(typeName, "AcSmSubset", StringComparison.OrdinalIgnoreCase)) stack.Push(comp);
                    else if (string.Equals(typeName, "AcSmSheet", StringComparison.OrdinalIgnoreCase))
                    {
                        string no = ""; try { no = (string)comp.GetNumber(); } catch { }
                        if (no == oldNumber)
                        {
                            if (newNumber != null) comp.SetNumber(newNumber);
                            if (newTitle != null) comp.SetTitle(newTitle);
                            changed = true; break;
                        }
                    }
                }
                if (changed) break;
            }
            db.UnlockDb(db, changed); locked = false;
            return new { changed, oldNumber, number = newNumber, title = newTitle };
        }
        finally
        {
            try { if (locked && db != null) db.UnlockDb(db, false); } catch { }
            try { if (db != null) mgr.Close(db); } catch { }
        }
    }

    [CommandMethod("HNLBRIDGESTATUS", CommandFlags.Session)]
    public void BridgeStatus()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage($"\nHNL Bridge v2.4.0: {(_registered ? "Paired" : "Waiting for HNL EXE")} | AutoCAD {Application.Version} | Drawing: {doc?.Name}");
    }

    [CommandMethod("HNLBRIDGEPING", CommandFlags.Session)]
    public void Ping() => Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nHNL_PONG");

    [CommandMethod("HNLPLOTDEVICES", CommandFlags.Session)]
    public void PlotDevices()
    {
        var data = GetPlotDevicesPayload();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + JsonConvert.SerializeObject(data, Formatting.Indented));
    }

    [CommandMethod("HNLLAYOUTS", CommandFlags.Session)]
    public void Layouts()
    {
        var data = GetLayoutsPayload();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + JsonConvert.SerializeObject(data, Formatting.Indented));
    }
}
