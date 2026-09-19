using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace Hnl.CadBridge;

public sealed class BridgeCommands : IExtensionApplication
{
    internal const string PluginVersion = "2.8.1";
    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentQueue<JObject> UiActions = new ConcurrentQueue<JObject>();
    private static readonly ConcurrentDictionary<string, byte> CancelledActionIds = new ConcurrentDictionary<string, byte>();
    private static Timer? _pollTimer;
    private static string? _baseUrl;
    private static string? _token;
    private static bool _registered;
    private static int _pollBusy;
    private static readonly string BridgeInstanceId = Guid.NewGuid().ToString("N");
    private static string _autoCadVersion = "";
    private static string _activeDrawingName = "";
    private static string _lastBridgeError = "";
    private static DateTime _lastHeartbeatUtc = DateTime.MinValue;
    private static DateTime _lastPollUtc = DateTime.MinValue;

    private sealed class PendingLibraryInsert
    {
        public string Action { get; set; } = "";
        public JObject Payload { get; set; } = new JObject();
    }

    private static readonly ConcurrentQueue<PendingLibraryInsert> PendingLibraryInserts =
        new ConcurrentQueue<PendingLibraryInsert>();
    private static string _lastLibraryInsertStatus = "Idle";

    // Bundled legacy Lisp auto-load.
    // AutoLISP definitions are document-scoped, so we track each active document separately.
    private static readonly HashSet<int> LispAutoLoadedDocuments = new HashSet<int>();
    private static bool _lispAutoLoadEnabled = false;
    private static string _lispAutoLoadSummary = "Not checked";

    private static bool IsCoreConsoleProcess()
    {
        try
        {
            return string.Equals(Process.GetCurrentProcess().ProcessName, "accoreconsole", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasInteractiveAutoCadUi(string feature)
    {
        if (!IsCoreConsoleProcess()) return true;
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            $"\nHNL {feature}: full AutoCAD UI is required; Core Console has no Ribbon/Palette.");
        return false;
    }

    public void Initialize()
    {
        // Cache AutoCAD UI state on AutoCAD's own thread. The timer callback below is
        // a ThreadPool thread and must not walk DocumentManager/Editor directly.
        _autoCadVersion = Application.Version.ToString();
        _activeDrawingName = Application.DocumentManager.MdiActiveDocument?.Name ?? "";
        TryLoadPairing();
        Application.Idle += OnIdle;
        _pollTimer = new Timer(_ => PollServer(), null, 500, 750);
        if (!IsCoreConsoleProcess()) HnlNativeRibbon.TryInstall();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            $"\nHNL CAD AI Bridge v{PluginVersion} loaded. Lisp mode=ON_DEMAND. Commands: HNLBRIDGESTATUS, HNLLISPSTATUS, HNLLISPRELOAD");
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
        ed?.WriteMessage($"\nHNL Bridge • Pairing: {(string.IsNullOrWhiteSpace(_baseUrl) ? "NOT FOUND" : _baseUrl)} • Registered: {_registered} • Instance: {BridgeInstanceId.Substring(0, 8)} • Drawing: {doc?.Name ?? "(none)"} • Last HB: {(_lastHeartbeatUtc == DateTime.MinValue ? "never" : _lastHeartbeatUtc.ToString("HH:mm:ss"))} • Last Poll: {(_lastPollUtc == DateTime.MinValue ? "never" : _lastPollUtc.ToString("HH:mm:ss"))} • Error: {(_lastBridgeError.Length == 0 ? "none" : _lastBridgeError)}");
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

    [CommandMethod("HNL", CommandFlags.Session)]
    [CommandMethod("HNLPALETTE", CommandFlags.Session)]
    public void ShowHnlPaletteCommand()
    {
        if (!HasInteractiveAutoCadUi("Palette")) return;
        try
        {
            NativePaletteCommands.ShowPaletteWindow();
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nHNL CAD AI Palette: OPEN.");
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nHNL Palette error: {ex.Message}");
        }
    }

    [CommandMethod("HNLHIDE", CommandFlags.Session)]
    public void HideHnlPaletteCommand()
    {
        if (!HasInteractiveAutoCadUi("Palette")) return;
        try
        {
            NativePaletteCommands.HidePaletteWindow();
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nHNL CAD AI Palette: HIDDEN.");
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nHNL Palette error: {ex.Message}");
        }
    }

    [CommandMethod("HNLPALETTESTATUS", CommandFlags.Session)]
    public void HnlPaletteStatusCommand()
    {
        if (!HasInteractiveAutoCadUi("Palette")) return;
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            $"\nHNL Palette visible: {NativePaletteCommands.IsPaletteVisible}");
    }

    [CommandMethod("HNLVERSION", CommandFlags.Session)]
    public void HnlVersionCommand()
    {
        var asm = typeof(BridgeCommands).Assembly;
        var loc = "";
        try { loc = asm.Location; } catch { }
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            $"\nHNL CAD AI Plugin v{PluginVersion} | Assembly: {asm.GetName().Version} | Path: {loc}");
    }

    [CommandMethod("HNLRIBBONRESET", CommandFlags.Session)]
    public void HnlRibbonResetCommand()
    {
        if (!HasInteractiveAutoCadUi("Ribbon")) return;
        var ok = HnlNativeRibbon.Rebuild();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            ok ? $"\nHNL Ribbon v{PluginVersion}: REBUILT." : $"\nHNL Ribbon v{PluginVersion}: rebuild failed.");
    }

    [CommandMethod("HNLRIBBON", CommandFlags.Session)]
    public void HnlRibbonCommand()
    {
        if (!HasInteractiveAutoCadUi("Ribbon")) return;
        var ok = HnlNativeRibbon.Activate();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            ok ? "\nHNL Ribbon: READY." : "\nHNL Ribbon chưa sẵn sàng. Palette HNL vẫn dùng được; thử lại HNLRIBBON sau khi Ribbon AutoCAD load.");
    }

    private static void ShowPaletteTabCommand(int index)
    {
        if (!HasInteractiveAutoCadUi("Palette")) return;
        try
        {
            NativePaletteCommands.ShowPaletteTab(index);
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nHNL Palette error: {ex.Message}");
        }
    }

    private static void OpenManagerWindowCommand(string? tool = null)
    {
        if (!HasInteractiveAutoCadUi("Manager")) return;
        try
        {
            NativePaletteCommands.OpenManagerWindow(tool);
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nHNL Manager error: {ex.Message}");
        }
    }

    [CommandMethod("HNLAI", CommandFlags.Session)]
    public void HnlAiCommand() => ShowPaletteTabCommand(0);

    [CommandMethod("HNL2D", CommandFlags.Session)]
    public void Hnl2DCommand() => ShowPaletteTabCommand(2);

    [CommandMethod("HNLDATA", CommandFlags.Session)]
    public void HnlDataCommand() => ShowPaletteTabCommand(2);

    [CommandMethod("HNLLAYOUT", CommandFlags.Session)]
    public void HnlLayoutCommand() => ShowPaletteTabCommand(2);

    [CommandMethod("HNLTOOLS", CommandFlags.Session)]
    public void HnlToolsCommand() => ShowPaletteTabCommand(3);

    [CommandMethod("HNLMANAGER", CommandFlags.Session)]
    public void HnlManagerCommand() => OpenManagerWindowCommand();

    [CommandMethod("HNLTEXT", CommandFlags.Session)]
    public void HnlTextCommand() => OpenManagerWindowCommand("TEXT");

    [CommandMethod("HNLBLOCK", CommandFlags.Session)]
    public void HnlBlockCommand() => OpenManagerWindowCommand("BLOCK");


    [CommandMethod("HNLFIELD", CommandFlags.Session)]
    public void HnlFieldCommand() => OpenManagerWindowCommand("FIELD");

    [CommandMethod("HNLGEOM", CommandFlags.Session)]
    public void HnlGeometryCommand() => OpenManagerWindowCommand("GEOMETRY");

    [CommandMethod("HNLDIM", CommandFlags.Session)]
    public void HnlDimensionCommand() => OpenManagerWindowCommand("DIMENSION");

    [CommandMethod("HNLLAYER", CommandFlags.Session)]
    public void HnlLayerDataCommand() => OpenManagerWindowCommand("LAYER");

    [CommandMethod("HNLQTY", CommandFlags.Session)]
    public void HnlQuantityCommand() => OpenManagerWindowCommand("QUANTITY");

    [CommandMethod("HNLSHOP2D", CommandFlags.Session)]
    public void HnlShopdrawing2DCommand() => OpenManagerWindowCommand("SHOPDRAWING");

    [CommandMethod("HNLLAYOUTAUTO", CommandFlags.Session)]
    public void HnlLayoutAutomationCommand() => OpenManagerWindowCommand("LAYOUT");

    [CommandMethod("HNLLISP", CommandFlags.Session)]
    public void HnlLispCenterCommand() => OpenManagerWindowCommand("SOURCES");

    [CommandMethod("HNLWALL", CommandFlags.Session)]
    public void HnlWallCommand()
    {
        try
        {
            var result = CreateWallSystem(new JObject());
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nHNL Smart Wall: {JsonConvert.SerializeObject(result)}");
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nHNL Wall error: {ex.Message}");
        }
    }

    [CommandMethod("HNLLISPSTATUS", CommandFlags.Session)]
    public void HnlLispStatusCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage(
            "\nHNL Lisp AutoLoad Status: " + JsonConvert.SerializeObject(GetBundledLispAutoLoadStatus()));
    }

    [CommandMethod("HNLLISPRELOAD", CommandFlags.Session)]
    public void HnlLispReloadCommand()
    {
        try
        {
            var result = AutoLoadBundledLisp(new JObject { ["force"] = true });
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nHNL Lisp Reload: " + JsonConvert.SerializeObject(result));
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nHNL Lisp Reload ERROR: " + ex.Message);
        }
    }

    [CommandMethod("HNLLISPAUTOON", CommandFlags.Session)]
    public void HnlLispAutoOnCommand()
    {
        _lispAutoLoadEnabled = true;
        _lispAutoLoadSummary = "AutoLoad-all enabled for this session";
        TryAutoLoadBundledLispForActiveDocument();
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nHNL Lisp: AUTOLOAD ALL enabled for this session. On-demand is the recommended default.");
    }

    [CommandMethod("HNLLISPAUTOOFF", CommandFlags.Session)]
    public void HnlLispAutoOffCommand()
    {
        _lispAutoLoadEnabled = false;
        _lispAutoLoadSummary = "On-demand mode";
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            "\nHNL Lisp mode: ON_DEMAND. Only the Lisp you use will be loaded.");
    }

    [CommandMethod("HNLLIBRARY", CommandFlags.Session)]
    public void HnlLibraryManagerCommand()
    {
        OpenManagerWindowCommand("LIBRARY");
    }

    [CommandMethod("HNLINSERTPENDING", CommandFlags.Session)]
    public void HnlInsertPendingLibraryCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        if (!PendingLibraryInserts.TryDequeue(out var pending))
        {
            doc.Editor.WriteMessage("\nHNL Library: không có block nào đang chờ chèn.");
            _lastLibraryInsertStatus = "No pending insert";
            return;
        }

        try
        {
            var title = (string?)pending.Payload["name"]
                ?? (string?)pending.Payload["definitionName"]
                ?? "HNL Block";

            var pointResult = doc.Editor.GetPoint(new PromptPointOptions($"\nHNL Library - chọn điểm chèn [{title}]: "));
            if (pointResult.Status != PromptStatus.OK)
            {
                _lastLibraryInsertStatus = "Insertion point cancelled";
                doc.Editor.WriteMessage("\nHNL Library: đã hủy chọn điểm chèn.");
                return;
            }

            pending.Payload["point"] = JObject.FromObject(new
            {
                x = pointResult.Value.X,
                y = pointResult.Value.Y
            });

            object result;
            if (string.Equals(pending.Action, "IMPORT_LIBRARY_DEFINITION", StringComparison.OrdinalIgnoreCase))
                result = ImportLibraryDefinition(pending.Payload);
            else
                result = InsertLibraryBlock(pending.Payload);

            _lastLibraryInsertStatus = "Inserted: " + JsonConvert.SerializeObject(result);
            doc.Editor.WriteMessage("\nHNL Library: chèn block thành công.");
        }
        catch (System.Exception ex)
        {
            _lastLibraryInsertStatus = "Insert ERROR: " + ex.Message;
            doc.Editor.WriteMessage($"\nHNL Library INSERT ERROR: {ex.Message}");
        }
    }

    [CommandMethod("HNLINSERT", CommandFlags.Session)]
    public void HnlInsertLibraryBlockCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument; if (doc == null) return;
        var opt = new PromptKeywordOptions("\nHNL Library [Section/Level/Detail/BoardStart/Main/Cross/Hanger/Stud/Track/RHS/Plate] <Level>: ");
        foreach (var k in new[] {"Section","Level","Detail","BoardStart","Main","Cross","Hanger","Stud","Track","RHS","Plate"}) opt.Keywords.Add(k);
        opt.Keywords.Default="Level"; opt.AllowNone=true;
        var r=doc.Editor.GetKeywords(opt); if(r.Status==PromptStatus.Cancel) return;
        var key=(r.StringResult ?? "Level").ToUpperInvariant();
        var map=new Dictionary<string,string>{
            ["SECTION"]="SECTION_MARK",["LEVEL"]="LEVEL_MARK",["DETAIL"]="DETAIL_MARK",["BOARDSTART"]="BOARD_START",
            ["MAIN"]="CEILING_MAIN",["CROSS"]="CEILING_CROSS",["HANGER"]="CEILING_HANGER",
            ["STUD"]="WALL_STUD",["TRACK"]="WALL_TRACK",["RHS"]="STEEL_RHS",["PLATE"]="STEEL_PLATE"
        };
        try { InsertLibraryBlock(new JObject{{"symbolKey",map[key]},{"name",map[key]},{"layer","HNL_LIBRARY"}}); }
        catch(System.Exception ex){ doc.Editor.WriteMessage($"\nHNL Library error: {ex.Message}"); }
    }

    [CommandMethod("HNLBOQ", CommandFlags.Session)]
    public void HnlBoqCommand()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage($"\nHNL BOQ: {JsonConvert.SerializeObject(GetHnlBoq())}");
    }

    [CommandMethod("HNLSHOPAUDIT", CommandFlags.Session)]
    public void HnlShopAuditCommand()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage($"\nHNL Shopdrawing Audit: {JsonConvert.SerializeObject(AuditHnlShopdrawing())}");
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



    [CommandMethod("HNLLAYERSYNC", CommandFlags.Session)]
    public void HnlLayerSyncCommand()
    {
        try { Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nHNL Layer Standards: {JsonConvert.SerializeObject(EnsureHnlStandards())}"); }
        catch (System.Exception ex) { Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nHNL Layer Standards error: {ex.Message}"); }
    }

    [CommandMethod("HNLDRAFTSTATUS")]
    public void DraftingStatusCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage($"\nHNL Drafting: {JsonConvert.SerializeObject(GetDraftingStatus())}");
    }

    [CommandMethod("HNLSETLAYOUT")]
    public void SetCurrentLayoutCommand()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var opt = new PromptStringOptions("\nTên Layout cần mở <Model>: ")
        {
            AllowSpaces = true,
            DefaultValue = "Model",
            UseDefaultValue = true
        };
        var result = doc.Editor.GetString(opt);
        if (result.Status != PromptStatus.OK) return;

        try
        {
            var payload = new JObject { ["name"] = result.StringResult.Trim() };
            var output = SetCurrentLayout(payload);
            doc.Editor.WriteMessage($"\nHNL: {JsonConvert.SerializeObject(output)}");
        }
        catch (System.Exception ex)
        {
            doc.Editor.WriteMessage($"\nHNL: Không chuyển được Layout: {ex.Message}");
        }
    }

    [CommandMethod("HNLRENLAYOUT")]
    public void RenameCurrentLayoutCommand()
    {
        var doc=Application.DocumentManager.MdiActiveDocument; if(doc==null) return;
        var current=LayoutManager.Current.CurrentLayout;
        if(string.Equals(current,"Model",StringComparison.OrdinalIgnoreCase)) { doc.Editor.WriteMessage("\nModel cannot be renamed."); return; }
        var opt=new PromptStringOptions($"\nTên mới cho Layout [{current}]: "){AllowSpaces=true};
        var res=doc.Editor.GetString(opt); if(res.Status!=PromptStatus.OK) return;
        try { RenameLayout(new JObject{{"oldName",current},{"newName",res.StringResult.Trim()}}); }
        catch(System.Exception ex){ doc.Editor.WriteMessage($"\nHNL Rename Layout error: {ex.Message}"); }
    }

    [CommandMethod("HNLCEILING")]
    public void CreateCeilingCommand()
    {
        var doc=Application.DocumentManager.MdiActiveDocument; if(doc==null) return;
        var ed=doc.Editor;
        var eo=new PromptEntityOptions("\nChọn Polyline kín làm biên trần: ");
        eo.SetRejectMessage("\nChỉ chấp nhận LWPOLYLINE."); eo.AddAllowedClass(typeof(Polyline),true);
        var er=ed.GetEntity(eo); if(er.Status!=PromptStatus.OK) return;
        using(var tr=doc.Database.TransactionManager.StartTransaction()) {
            var pl=tr.GetObject(er.ObjectId,OpenMode.ForRead,false) as Polyline;
            if(pl==null || !pl.Closed) { ed.WriteMessage("\nPolyline phải kín."); return; }
            tr.Commit();
        }

        var ko=new PromptKeywordOptions("\nLoại trần [Chim/Noi] <Chim>: ");
        ko.Keywords.Add("Chim"); ko.Keywords.Add("Noi"); ko.Keywords.Default="Chim"; ko.AllowNone=true;
        var kr=ed.GetKeywords(ko); if(kr.Status==PromptStatus.Cancel) return;
        var exposed=string.Equals(kr.StringResult,"Noi",StringComparison.OrdinalIgnoreCase);

        double Ask(string label,double def) {
            var o=new PromptDoubleOptions($"\n{label} <{def:0.##}>: "){DefaultValue=def,UseDefaultValue=true,AllowNegative=false,AllowZero=false};
            var r=ed.GetDouble(o); return r.Status==PromptStatus.OK?r.Value:def;
        }
        var main=Ask("Xương chính @ (mm)",exposed?1200:800);
        var cross=Ask("Xương phụ @ (mm)",exposed?610:(1220.0/3.0));
        var hanger=Ask("Ty treo @ (mm)",exposed?1200:900);
        var ao=new PromptDoubleOptions("\nGóc xoay hệ xương (độ) <0>: "){DefaultValue=0,UseDefaultValue=true,AllowNegative=true,AllowZero=true};
        var ar=ed.GetDouble(ao); var angle=ar.Status==PromptStatus.OK?ar.Value:0;

        try {
            var payload=new JObject{
                ["mainSpacing"]=main,["crossSpacing"]=cross,["hangerSpacing"]=hanger,
                ["rotationDeg"]=angle,["originMode"]="CENTER",["drawHangers"]=true,
                ["mainLayer"]="HNL-CLG-MAIN",["crossLayer"]="HNL-CLG-CROSS",["hangerLayer"]="HNL-CLG-HANGER"
            };
            var result=CreateCeilingGridForPolyline(doc,er.ObjectId,payload);
            ed.WriteMessage($"\nHNL Ceiling: {JsonConvert.SerializeObject(result)}");
        } catch(System.Exception ex) { ed.WriteMessage($"\nHNL Ceiling error: {ex.Message}"); }
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
                // Never let work fetched from an old HNL process execute after the
                // pairing marker switches to another HNL instance/port/token.
                while (UiActions.TryDequeue(out _)) { }
                while (PendingLibraryInserts.TryDequeue(out _)) { }
                CancelledActionIds.Clear();
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
        req.Headers.TryAddWithoutValidation("x-hnl-bridge-instance", BridgeInstanceId);
        if (body != null) req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        return req;
    }

    private static async void PollServer()
    {
        if (Interlocked.Exchange(ref _pollBusy, 1) == 1) return;
        try
        {
            // Re-read pairing every cycle so HNL can start/restart after AutoCAD without NETLOAD/restart.
            TryLoadPairing();
            if (string.IsNullOrWhiteSpace(_baseUrl)) return;
            if (!_registered)
            {
                using var req = MakeRequest(HttpMethod.Post, "/api/autocad/register", new {
                    version = _autoCadVersion,
                    drawingName = _activeDrawingName,
                    pluginVersion = PluginVersion,
                    bridgeInstanceId = BridgeInstanceId,
                    processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                    capabilities = new[] { "GET_STATUS","GET_DRAFTING_STATUS","SET_DRAFTING_MODE","GET_PLOT_DEVICES","GET_LAYOUTS","SET_CURRENT_LAYOUT","RENAME_LAYOUT","EXECUTE_COMMAND","LOAD_LISP_FILE","AUTOLOAD_LISP_PACK","GET_LISP_AUTOLOAD_STATUS","CANCEL_COMMAND","OPEN_DWG","CONVERT_DWG_TO_DXF_PREVIEW","GET_MODELSPACE_SNAPSHOT","SELECT_HANDLES","CREATE_NATIVE_ENTITY","APPLY_ENTITY_TRANSFORM","ERASE_HANDLES","SET_ENTITY_LAYER","UPDATE_TEXT_CONTENTS","INSERT_EXISTING_BLOCK","GET_DYNAMIC_BLOCK_PROPERTIES","SET_DYNAMIC_BLOCK_PROPERTIES","SAVE_CURRENT_DWG","SAVE_AS_DWG","GET_SELECTION","SELECT_ALL","GET_LAYERS","ENSURE_HNL_STANDARDS","CREATE_CEILING_GRID","CREATE_CEILING_SMART","CREATE_WALL_SYSTEM","INSERT_LIBRARY_BLOCK","INSPECT_LIBRARY_DWG","IMPORT_LIBRARY_DEFINITION","GET_LIBRARY_INSERT_STATUS","GET_HNL_BOQ","AUDIT_HNL_SHOPDRAWING","PUBLISH_LAYOUTS_PDF","PLOT_CURRENT_PDF","SAVE_DXF_AS_DWG","GET_SHEETSET_INFO","UPDATE_SHEET" }
                });
                using var res = await Http.SendAsync(req);
                _registered = res.IsSuccessStatusCode;
                if (!_registered)
                {
                    var detail = await res.Content.ReadAsStringAsync();
                    if (detail.Length > 240) detail = detail.Substring(0, 240);
                    _lastBridgeError = $"REGISTER HTTP {(int)res.StatusCode}: {detail}";
                    return;
                }
                _lastBridgeError = "";
            }
            else
            {
                using var hb = MakeRequest(HttpMethod.Post, "/api/autocad/heartbeat", new { drawingName = _activeDrawingName, bridgeInstanceId = BridgeInstanceId });
                using var hbRes = await Http.SendAsync(hb);
                if (!hbRes.IsSuccessStatusCode)
                {
                    _registered = false;
                    var detail = await hbRes.Content.ReadAsStringAsync();
                    if (detail.Length > 240) detail = detail.Substring(0, 240);
                    _lastBridgeError = $"HEARTBEAT HTTP {(int)hbRes.StatusCode}: {detail}";
                    return;
                }
                _lastHeartbeatUtc = DateTime.UtcNow;
            }

            using var poll = MakeRequest(HttpMethod.Get, "/api/autocad/poll");
            using var pollRes = await Http.SendAsync(poll);
            _lastPollUtc = DateTime.UtcNow;
            if (!pollRes.IsSuccessStatusCode)
            {
                var detail = await pollRes.Content.ReadAsStringAsync();
                if (detail.Length > 240) detail = detail.Substring(0, 240);
                _lastBridgeError = $"POLL HTTP {(int)pollRes.StatusCode}: {detail}";
                return;
            }
            var text = await pollRes.Content.ReadAsStringAsync();
            var root = JObject.Parse(text);
            if (root["cancelledActionIds"] is JArray cancelledIds)
            {
                foreach (var token in cancelledIds)
                {
                    var cancelledId = (string?)token ?? "";
                    if (!string.IsNullOrWhiteSpace(cancelledId)) CancelledActionIds[cancelledId] = 1;
                }
            }
            if (root["item"] is JObject item)
            {
                var expiresAt = (long?)item["expiresAt"] ?? 0L;
                if (expiresAt > 0L && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= expiresAt)
                {
                    var expiredId = (string?)item["id"] ?? "";
                    if (!string.IsNullOrWhiteSpace(expiredId))
                        SendResult(expiredId, false, null, "AUTOCAD_ACTION_EXPIRED_BEFORE_QUEUE");
                }
                else UiActions.Enqueue(item);
            }
            _lastBridgeError = "";
        }
        catch (System.Exception ex)
        {
            _registered = false;
            _lastBridgeError = ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            Interlocked.Exchange(ref _pollBusy, 0);
        }
    }

    private static void OnIdle(object? sender, EventArgs e)
    {
        // All AutoCAD API reads/writes stay on AutoCAD's thread.
        _activeDrawingName = Application.DocumentManager.MdiActiveDocument?.Name ?? "";
        if (!IsCoreConsoleProcess() && !HnlNativeRibbon.IsInstalled) HnlNativeRibbon.TryInstall();

        var isBusy = IsAutoCadBusy();

        // Default remains ON_DEMAND. This only runs after an explicit session-level
        // opt-in to preload the bundle, and never while another AutoCAD command is active.
        if (!isBusy) TryAutoLoadBundledLispForActiveDocument();

        // Most bridge work must wait until AutoCAD is idle. CANCEL_COMMAND is the one
        // deliberate exception: it exists specifically so HNL can send ESC while a
        // native command is in progress.
        if (isBusy)
        {
            if (!UiActions.TryPeek(out var pendingBusyAction) ||
                !string.Equals((string?)pendingBusyAction["action"], "CANCEL_COMMAND", StringComparison.OrdinalIgnoreCase))
                return;
        }
        if (!UiActions.TryDequeue(out var item)) return;
        ExecuteQueuedAction(item);
    }

    private static bool IsAutoCadBusy()
    {
        try
        {
            var cmdNames = Convert.ToString(Application.GetSystemVariable("CMDNAMES")) ?? "";
            return !string.IsNullOrWhiteSpace(cmdNames);
        }
        catch
        {
            // If AutoCAD cannot report command state, fail safe and leave the action
            // queued for a later Idle tick rather than mutating a drawing blindly.
            return true;
        }
    }

    private static async void SendResult(string id, bool ok, object? result = null, string? error = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) return;
            using var req = MakeRequest(HttpMethod.Post, "/api/autocad/result", new { id, ok, result, error });
            using var res = await Http.SendAsync(req);
        }
        catch { }
    }

    private static void ExecuteQueuedAction(JObject item)
    {
        var id = (string?)item["id"] ?? "";
        var action = ((string?)item["action"] ?? "").ToUpperInvariant();
        var payload = item["payload"] as JObject ?? new JObject();
        if (!string.IsNullOrWhiteSpace(id) && CancelledActionIds.TryRemove(id, out _)) return;
        var ownerInstanceId = (string?)item["bridgeInstanceId"] ?? "";
        if (!string.IsNullOrWhiteSpace(ownerInstanceId) &&
            !string.Equals(ownerInstanceId, BridgeInstanceId, StringComparison.Ordinal))
        {
            SendResult(id, false, null, "AUTOCAD_BRIDGE_OWNER_MISMATCH_BEFORE_EXECUTION");
            return;
        }
        var expiresAt = (long?)item["expiresAt"] ?? 0L;
        if (expiresAt > 0L && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= expiresAt)
        {
            SendResult(id, false, null, "AUTOCAD_ACTION_EXPIRED_BEFORE_EXECUTION");
            return;
        }
        var targetDrawingName = (string?)item["targetDrawingName"] ?? "";
        if (RequiresStableActiveDocument(action) && !string.IsNullOrWhiteSpace(targetDrawingName))
        {
            var currentDrawingName = Application.DocumentManager.MdiActiveDocument?.Name ?? "";
            if (!string.Equals(currentDrawingName, targetDrawingName, StringComparison.OrdinalIgnoreCase))
            {
                SendResult(id, false, null,
                    $"AUTOCAD_ACTIVE_DOCUMENT_CHANGED: expected={targetDrawingName}; actual={currentDrawingName}");
                return;
            }
        }
        try
        {
            object result = action switch
            {
                "GET_STATUS" => GetStatusPayload(),
                "GET_DRAFTING_STATUS" => GetDraftingStatus(),
                "SET_DRAFTING_MODE" => SetDraftingMode(payload),
                "GET_PLOT_DEVICES" => GetPlotDevicesPayload(),
                "GET_LAYOUTS" => GetLayoutsPayload(),
                "SET_CURRENT_LAYOUT" => SetCurrentLayout(payload),
                "RENAME_LAYOUT" => RenameLayout(payload),
                "EXECUTE_COMMAND" => ExecuteNativeCommand(payload),
                "LOAD_LISP_FILE" => LoadLispFile(payload),
                "AUTOLOAD_LISP_PACK" => AutoLoadBundledLisp(payload),
                "GET_LISP_AUTOLOAD_STATUS" => GetBundledLispAutoLoadStatus(),
                "CANCEL_COMMAND" => CancelNativeCommand(),
                "OPEN_DWG" => OpenDwg(payload),
                "CONVERT_DWG_TO_DXF_PREVIEW" => ConvertDwgToDxfPreview(payload),
                "GET_MODELSPACE_SNAPSHOT" => GetModelspaceSnapshot(payload),
                "SELECT_HANDLES" => SelectHandles(payload),
                "CREATE_NATIVE_ENTITY" => CreateNativeEntity(payload),
                "APPLY_ENTITY_TRANSFORM" => ApplyEntityTransform(payload),
                "ERASE_HANDLES" => EraseHandles(payload),
                "SET_ENTITY_LAYER" => SetEntityLayer(payload),
                "UPDATE_TEXT_CONTENTS" => UpdateTextContents(payload),
                "INSERT_EXISTING_BLOCK" => InsertExistingBlock(payload),
                "GET_DYNAMIC_BLOCK_PROPERTIES" => GetDynamicBlockProperties(payload),
                "SET_DYNAMIC_BLOCK_PROPERTIES" => SetDynamicBlockProperties(payload),
                "SAVE_CURRENT_DWG" => SaveCurrentDwg(),
                "SAVE_AS_DWG" => SaveAsDwg(payload),
                "GET_SELECTION" => GetSelectionPayload(),
                "SELECT_ALL" => SelectAllObjects(),
                "GET_LAYERS" => GetLayersPayload(),
                "ENSURE_HNL_STANDARDS" => EnsureHnlStandards(),
                "CREATE_CEILING_GRID" => CreateCeilingGrid(payload),
                "CREATE_CEILING_SMART" => CreateCeilingSmart(payload),
                "CREATE_WALL_SYSTEM" => CreateWallSystem(payload),
                "INSERT_LIBRARY_BLOCK" => QueueOrInsertLibraryBlock(payload),
                "INSPECT_LIBRARY_DWG" => InspectLibraryDwg(payload),
                "IMPORT_LIBRARY_DEFINITION" => QueueOrImportLibraryDefinition(payload),
                "GET_LIBRARY_INSERT_STATUS" => GetLibraryInsertStatus(),
                "GET_HNL_BOQ" => GetHnlBoq(),
                "AUDIT_HNL_SHOPDRAWING" => AuditHnlShopdrawing(),
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

    private static bool RequiresStableActiveDocument(string action)
    {
        switch (action)
        {
            case "GET_STATUS":
            case "GET_DRAFTING_STATUS":
            case "GET_PLOT_DEVICES":
            case "GET_LAYOUTS":
            case "GET_LISP_AUTOLOAD_STATUS":
            case "GET_MODELSPACE_SNAPSHOT":
            case "GET_SELECTION":
            case "GET_LAYERS":
            case "GET_LIBRARY_INSERT_STATUS":
            case "GET_HNL_BOQ":
            case "AUDIT_HNL_SHOPDRAWING":
            case "GET_SHEETSET_INFO":
            case "GET_DYNAMIC_BLOCK_PROPERTIES":
            case "OPEN_DWG":
            case "CANCEL_COMMAND":
                return false;
            default:
                return true;
        }
    }

    private static object GetStatusPayload()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        return new { connected = true, version = Application.Version.ToString(), drawingName = doc?.Name ?? "", pluginVersion = PluginVersion };
    }


    private static int GetSysInt(string name, int fallback = 0)
    {
        try { return Convert.ToInt32(Application.GetSystemVariable(name)); }
        catch { return fallback; }
    }

    private static object GetDraftingStatus()
    {
        var snapMode = GetSysInt("SNAPMODE");
        var orthoMode = GetSysInt("ORTHOMODE");
        var gridMode = GetSysInt("GRIDMODE");
        var osMode = GetSysInt("OSMODE");
        var dynMode = GetSysInt("DYNMODE");
        var osnapSuppressed = (osMode & 16384) != 0;
        var osnapMask = osMode & 16383;

        return new
        {
            snap = snapMode != 0,
            ortho = orthoMode != 0,
            grid = gridMode != 0,
            osnap = !osnapSuppressed && osnapMask != 0,
            dyn = dynMode > 0,
            raw = new
            {
                SNAPMODE = snapMode,
                ORTHOMODE = orthoMode,
                GRIDMODE = gridMode,
                OSMODE = osMode,
                DYNMODE = dynMode
            }
        };
    }

    private static object SetDraftingMode(JObject payload)
    {
        var mode = ((string?)payload["mode"] ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(mode))
            throw new ArgumentException("mode required");

        bool enabled;
        if (payload["enabled"]?.Type == JTokenType.Boolean)
            enabled = (bool)payload["enabled"]!;
        else
        {
            var state = JObject.FromObject(GetDraftingStatus());
            enabled = !((bool?)state[mode.ToLowerInvariant()] ?? false);
        }

        switch (mode)
        {
            case "SNAP":
                Application.SetSystemVariable("SNAPMODE", enabled ? 1 : 0);
                break;

            case "ORTHO":
                Application.SetSystemVariable("ORTHOMODE", enabled ? 1 : 0);
                break;

            case "GRID":
                Application.SetSystemVariable("GRIDMODE", enabled ? 1 : 0);
                break;

            case "OSNAP":
            {
                var osMode = GetSysInt("OSMODE");
                if (enabled)
                {
                    osMode &= ~16384;
                    if ((osMode & 16383) == 0)
                        osMode |= 1 | 2 | 4 | 32; // endpoint, midpoint, center, intersection
                }
                else
                {
                    osMode |= 16384; // temporary OSNAP suppression, preserving user's mask
                }
                Application.SetSystemVariable("OSMODE", osMode);
                break;
            }

            case "DYN":
            {
                var dynMode = GetSysInt("DYNMODE");
                if (enabled)
                    Application.SetSystemVariable("DYNMODE", dynMode == 0 ? 3 : Math.Abs(dynMode));
                else
                    Application.SetSystemVariable("DYNMODE", dynMode > 0 ? -dynMode : dynMode);
                break;
            }

            default:
                throw new InvalidOperationException($"Unsupported drafting mode: {mode}");
        }

        return new { mode, enabled, status = GetDraftingStatus() };
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
                    handle = layout.Handle.ToString(),
                    name = layout.LayoutName,
                    tabOrder = layout.TabOrder,
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



    private static object SetCurrentLayout(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");

        var name = ((string?)payload["name"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name required");

        // Resolve against the real Layout dictionary first so a bad UI name
        // cannot leave AutoCAD in an inconsistent state.
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var dict = (DBDictionary)tr.GetObject(doc.Database.LayoutDictionaryId, OpenMode.ForRead);
            if (!dict.Contains(name))
                throw new InvalidOperationException($"Layout not found: {name}");
            tr.Commit();

            LayoutManager.Current.CurrentLayout = name;
        }

        return new
        {
            activated = true,
            name,
            currentLayout = LayoutManager.Current.CurrentLayout
        };
    }


    private static object RenameLayout(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var oldName = ((string?)payload["oldName"] ?? "").Trim();
        var newName = ((string?)payload["newName"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("oldName/newName required");
        if (string.Equals(oldName, "Model", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(newName, "Model", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Model layout cannot be renamed/overwritten.");
        using (doc.LockDocument()) LayoutManager.Current.RenameLayout(oldName, newName);
        return new { renamed = true, oldName, newName };
    }

    private sealed class HnlLayerProfile
    {
        public string Name = "";
        public short Aci;
        public LineWeight Weight;
        public string Linetype = "Continuous";
        public bool Plottable = true;
    }

    private static HnlLayerProfile? GetHnlLayerProfile(string? name)
    {
        var normalizedName = (name ?? "").Trim();
        switch (normalizedName.ToUpperInvariant())
        {
            case "HNL-CLG-BOARD": return new HnlLayerProfile { Name=normalizedName, Aci=151, Weight=LineWeight.LineWeight018, Linetype="Continuous" };
            case "HNL-CLG-MAIN": return new HnlLayerProfile { Name=normalizedName, Aci=30, Weight=LineWeight.LineWeight035, Linetype="Continuous" };
            case "HNL-CLG-CROSS": return new HnlLayerProfile { Name=normalizedName, Aci=2, Weight=LineWeight.LineWeight025, Linetype="Continuous" };
            case "HNL-CLG-HANGER": return new HnlLayerProfile { Name=normalizedName, Aci=3, Weight=LineWeight.LineWeight018, Linetype="HIDDEN2" };
            case "HNL-CLG-START": return new HnlLayerProfile { Name=normalizedName, Aci=4, Weight=LineWeight.LineWeight025, Linetype="CENTER2" };
            case "HNL-WALL-BOARD": return new HnlLayerProfile { Name=normalizedName, Aci=7, Weight=LineWeight.LineWeight018, Linetype="Continuous" };
            case "HNL-WALL-STUD": return new HnlLayerProfile { Name=normalizedName, Aci=6, Weight=LineWeight.LineWeight025, Linetype="Continuous" };
            case "HNL-WALL-TRACK": return new HnlLayerProfile { Name=normalizedName, Aci=5, Weight=LineWeight.LineWeight035, Linetype="Continuous" };
            case "HNL-WALL-REINF": return new HnlLayerProfile { Name=normalizedName, Aci=1, Weight=LineWeight.LineWeight040, Linetype="Continuous" };
            case "HNL-STEEL-RHS": return new HnlLayerProfile { Name=normalizedName, Aci=1, Weight=LineWeight.LineWeight035, Linetype="Continuous" };
            case "HNL-STEEL-PLATE": return new HnlLayerProfile { Name=normalizedName, Aci=30, Weight=LineWeight.LineWeight035, Linetype="Continuous" };
            case "HNL-ANNO-SECTION": return new HnlLayerProfile { Name=normalizedName, Aci=7, Weight=LineWeight.LineWeight035, Linetype="Continuous" };
            case "HNL-ANNO-LEVEL": return new HnlLayerProfile { Name=normalizedName, Aci=4, Weight=LineWeight.LineWeight025, Linetype="Continuous" };
            case "HNL-ANNO-DETAIL": return new HnlLayerProfile { Name=normalizedName, Aci=2, Weight=LineWeight.LineWeight025, Linetype="Continuous" };
            case "HNL-DATA-FIELD": return new HnlLayerProfile { Name=normalizedName, Aci=92, Weight=LineWeight.LineWeight018, Linetype="Continuous" };
            case "HNL-NOPLOT-HELPER": return new HnlLayerProfile { Name=normalizedName, Aci=8, Weight=LineWeight.LineWeight005, Linetype="DASHED", Plottable=false };
            default: return null;
        }
    }

    private static void EnsureLineType(Database db, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "Continuous", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            using var tr = db.TransactionManager.StartOpenCloseTransaction();
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
        }
        catch { }
        try { db.LoadLineTypeFile(name, "acadiso.lin"); return; } catch { }
        try { db.LoadLineTypeFile(name, "acad.lin"); } catch { }
    }

    private static void ApplyHnlLayerProfile(Transaction tr, Database db, LayerTableRecord rec, HnlLayerProfile profile)
    {
        EnsureLineType(db, profile.Linetype);
        if (!rec.IsWriteEnabled) rec.UpgradeOpen();
        rec.Color = Color.FromColorIndex(ColorMethod.ByAci, profile.Aci);
        rec.LineWeight = profile.Weight;
        rec.IsPlottable = profile.Plottable;
        try
        {
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(profile.Linetype)) rec.LinetypeObjectId = lt[profile.Linetype];
        }
        catch { }
    }

    private static void EnsureLayer(Transaction tr, Database db, string name)
    {
        var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        LayerTableRecord rec;
        if (table.Has(name))
        {
            rec = (LayerTableRecord)tr.GetObject(table[name], OpenMode.ForRead);
        }
        else
        {
            table.UpgradeOpen();
            rec = new LayerTableRecord { Name = name };
            table.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
        }
        var profile = GetHnlLayerProfile(name);
        if (profile != null) ApplyHnlLayerProfile(tr, db, rec, profile);
    }


    private const string HnlRegApp = "HNL_CAD_AI";

    private static void EnsureHnlRegApp(Transaction tr, Database db)
    {
        var table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (table.Has(HnlRegApp)) return;
        table.UpgradeOpen();
        var rec = new RegAppTableRecord { Name = HnlRegApp };
        table.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
    }

    private static void TagHnlEntity(Entity ent, string component, string smartId, string? meta = null)
    {
        ent.XData = new ResultBuffer(
            new TypedValue(1001, HnlRegApp),
            new TypedValue(1000, component ?? ""),
            new TypedValue(1000, smartId ?? ""),
            new TypedValue(1000, meta ?? "")
        );
    }

    private static (string component, string smartId, string meta)? ReadHnlTag(Entity ent)
    {
        var rb = ent.GetXDataForApplication(HnlRegApp);
        if (rb == null) return null;
        var arr = rb.AsArray();
        if (arr.Length < 3) return null;
        return (
            arr.Length > 1 ? Convert.ToString(arr[1].Value) ?? "" : "",
            arr.Length > 2 ? Convert.ToString(arr[2].Value) ?? "" : "",
            arr.Length > 3 ? Convert.ToString(arr[3].Value) ?? "" : ""
        );
    }

    private static object EnsureHnlStandards()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var names = new[] {
            "HNL-CLG-BOARD","HNL-CLG-MAIN","HNL-CLG-CROSS","HNL-CLG-HANGER","HNL-CLG-START",
            "HNL-WALL-BOARD","HNL-WALL-STUD","HNL-WALL-TRACK","HNL-WALL-REINF",
            "HNL-STEEL-RHS","HNL-STEEL-PLATE","HNL-ANNO-SECTION","HNL-ANNO-LEVEL","HNL-ANNO-DETAIL",
            "HNL-DATA-FIELD","HNL-NOPLOT-HELPER"
        };
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            foreach (var name in names) EnsureLayer(tr, doc.Database, name);
            tr.Commit();
        }
        return new { updated = names.Length, layers = names };
    }

    private static Point2d ToLocal(Point2d p, Point2d origin, double radians)
    {
        var x = p.X - origin.X; var y = p.Y - origin.Y;
        var c = Math.Cos(radians); var sn = Math.Sin(radians);
        return new Point2d(x * c - y * sn, x * sn + y * c);
    }

    private static Point2d ToWorld(Point2d p, Point2d origin, double radians)
    {
        var c = Math.Cos(radians); var sn = Math.Sin(radians);
        return new Point2d(origin.X + p.X * c - p.Y * sn, origin.Y + p.X * sn + p.Y * c);
    }

    private static List<double> VHits(IReadOnlyList<Point2d> poly, double x)
    {
        var values = new List<double>();
        for (var i=0;i<poly.Count;i++) {
            var a=poly[i]; var b=poly[(i+1)%poly.Count];
            if (Math.Abs(a.X-b.X)<1e-9) continue;
            if (!((a.X<=x && b.X>x)||(b.X<=x && a.X>x))) continue;
            values.Add(a.Y+(x-a.X)*(b.Y-a.Y)/(b.X-a.X));
        }
        values.Sort(); return values;
    }

    private static List<double> HHits(IReadOnlyList<Point2d> poly, double y)
    {
        var values = new List<double>();
        for (var i=0;i<poly.Count;i++) {
            var a=poly[i]; var b=poly[(i+1)%poly.Count];
            if (Math.Abs(a.Y-b.Y)<1e-9) continue;
            if (!((a.Y<=y && b.Y>y)||(b.Y<=y && a.Y>y))) continue;
            values.Add(a.X+(y-a.Y)*(b.X-a.X)/(b.Y-a.Y));
        }
        values.Sort(); return values;
    }

    private static bool Inside(IReadOnlyList<Point2d> poly, Point2d p)
    {
        var inside=false;
        for(int i=0,j=poly.Count-1;i<poly.Count;j=i++) {
            var a=poly[i]; var b=poly[j];
            var hit=((a.Y>p.Y)!=(b.Y>p.Y)) &&
                (p.X<(b.X-a.X)*(p.Y-a.Y)/((b.Y-a.Y)==0?1e-12:(b.Y-a.Y))+a.X);
            if(hit) inside=!inside;
        }
        return inside;
    }

    private static IEnumerable<double> GridValues(double min,double max,double spacing,string mode,double offset)
    {
        spacing=Math.Max(1,spacing);
        var first=string.Equals(mode,"FROM_EDGE",StringComparison.OrdinalIgnoreCase)
            ? min+Math.Max(0,offset)
            : Math.Ceiling((min-offset)/spacing)*spacing+offset;
        for(var v=first;v<=max+1e-7;v+=spacing) yield return v;
    }

    private static object CreateCeilingGrid(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var sel=doc.Editor.SelectImplied();
        if(sel.Status!=PromptStatus.OK || sel.Value==null)
            throw new InvalidOperationException("Select one closed Polyline boundary first.");
        ObjectId boundary=ObjectId.Null;
        using(var tr=doc.Database.TransactionManager.StartTransaction()) {
            foreach(var id in sel.Value.GetObjectIds()) {
                if(tr.GetObject(id,OpenMode.ForRead,false) is Polyline pl && pl.Closed && pl.NumberOfVertices>=3) { boundary=id; break; }
            }
            tr.Commit();
        }
        if(boundary.IsNull) throw new InvalidOperationException("Selection does not contain a closed LWPOLYLINE.");
        return CreateCeilingGridForPolyline(doc,boundary,payload);
    }

    private static object CreateCeilingGridForPolyline(Document doc,ObjectId boundaryId,JObject payload)
    {
        var mainSpacing=Math.Max(100.0,(double?)payload["mainSpacing"]??800.0);
        var crossSpacing=Math.Max(100.0,(double?)payload["crossSpacing"]??(1220.0/3.0));
        var hangerSpacing=Math.Max(100.0,(double?)payload["hangerSpacing"]??900.0);
        var rotationDeg=(double?)payload["rotationDeg"]??0.0;
        var mode=((string?)payload["originMode"]??"CENTER").ToUpperInvariant();
        var offsetX=(double?)payload["offsetX"]??0.0;
        var offsetY=(double?)payload["offsetY"]??0.0;
        var drawHangers=(bool?)payload["drawHangers"]??true;
        var mainLayer=((string?)payload["mainLayer"]??"HNL-CLG-MAIN").Trim();
        var crossLayer=((string?)payload["crossLayer"]??"HNL-CLG-CROSS").Trim();
        var hangerLayer=((string?)payload["hangerLayer"]??"HNL-CLG-HANGER").Trim();
        var mainCount=0; var crossCount=0; var hangerCount=0;

        using(doc.LockDocument())
        using(var tr=doc.Database.TransactionManager.StartTransaction()) {
            var pl=tr.GetObject(boundaryId,OpenMode.ForRead,false) as Polyline
                ?? throw new InvalidOperationException("Boundary is not a Polyline.");
            if(!pl.Closed || pl.NumberOfVertices<3) throw new InvalidOperationException("Boundary must be closed.");

            var world=new List<Point2d>();
            for(var i=0;i<pl.NumberOfVertices;i++) world.Add(pl.GetPoint2dAt(i));
            var origin=new Point2d((world.Min(q=>q.X)+world.Max(q=>q.X))/2.0,(world.Min(q=>q.Y)+world.Max(q=>q.Y))/2.0);
            var rad=rotationDeg*Math.PI/180.0;
            var local=world.Select(q=>ToLocal(q,origin,-rad)).ToList();
            var minX=local.Min(q=>q.X); var maxX=local.Max(q=>q.X);
            var minY=local.Min(q=>q.Y); var maxY=local.Max(q=>q.Y);

            EnsureLayer(tr,doc.Database,mainLayer); EnsureLayer(tr,doc.Database,crossLayer);
            if(drawHangers) EnsureLayer(tr,doc.Database,hangerLayer);
            EnsureHnlRegApp(tr, doc.Database);
            var smartId=((string?)payload["id"] ?? (string?)payload["smartId"] ?? $"CEILING_{Guid.NewGuid():N}").Trim();
            var meta=$"main={mainSpacing:0.###};cross={crossSpacing:0.###};hanger={hangerSpacing:0.###};board=1220x2440";
            var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
            var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);

            foreach(var x in GridValues(minX,maxX,mainSpacing,mode,offsetX)) {
                var ys=VHits(local,x);
                for(var i=0;i+1<ys.Count;i+=2) {
                    if(ys[i+1]-ys[i]<1e-6) continue;
                    var a=ToWorld(new Point2d(x,ys[i]),origin,rad);
                    var b=ToWorld(new Point2d(x,ys[i+1]),origin,rad);
                    var ln=new Line(new Point3d(a.X,a.Y,0),new Point3d(b.X,b.Y,0)){Layer=mainLayer};
                    TagHnlEntity(ln,"CEILING_MAIN",smartId,meta);
                    ms.AppendEntity(ln); tr.AddNewlyCreatedDBObject(ln,true); mainCount++;
                }
                if(drawHangers) {
                    foreach(var y in GridValues(minY,maxY,hangerSpacing,mode,offsetY)) {
                        var lp=new Point2d(x,y); if(!Inside(local,lp)) continue;
                        var wp=ToWorld(lp,origin,rad);
                        var c=new Circle(new Point3d(wp.X,wp.Y,0),Vector3d.ZAxis,20){Layer=hangerLayer};
                        TagHnlEntity(c,"CEILING_HANGER",smartId,meta);
                        ms.AppendEntity(c); tr.AddNewlyCreatedDBObject(c,true); hangerCount++;
                    }
                }
            }

            foreach(var y in GridValues(minY,maxY,crossSpacing,mode,offsetY)) {
                var xs=HHits(local,y);
                for(var i=0;i+1<xs.Count;i+=2) {
                    if(xs[i+1]-xs[i]<1e-6) continue;
                    var a=ToWorld(new Point2d(xs[i],y),origin,rad);
                    var b=ToWorld(new Point2d(xs[i+1],y),origin,rad);
                    var ln=new Line(new Point3d(a.X,a.Y,0),new Point3d(b.X,b.Y,0)){Layer=crossLayer};
                    TagHnlEntity(ln,"CEILING_CROSS",smartId,meta);
                    ms.AppendEntity(ln); tr.AddNewlyCreatedDBObject(ln,true); crossCount++;
                }
            }
            tr.Commit();
        }
        return new {created=true,boundaryHandle=boundaryId.Handle.ToString(),mainSegments=mainCount,crossSegments=crossCount,hangers=hangerCount,mainSpacing,crossSpacing,hangerSpacing,rotationDeg,originMode=mode};
    }


    private static object CreateCeilingSmart(JObject payload)
    {
        payload["crossSpacing"] = (double?)payload["crossSpacing"] ?? (1220.0 / 3.0);
        payload["rotationDeg"] = (double?)payload["rotationDeg"] ?? (double?)payload["boardDirectionDeg"] ?? 0.0;
        if (payload["startMode"] != null && payload["originMode"] == null)
            payload["originMode"] = string.Equals((string?)payload["startMode"], "CENTER", StringComparison.OrdinalIgnoreCase) ? "CENTER" : "FROM_EDGE";
        payload["boardWidth"] = (double?)payload["boardWidth"] ?? 1220.0;
        payload["boardLength"] = (double?)payload["boardLength"] ?? 2440.0;
        payload["smartId"] = (string?)payload["id"] ?? $"CEILING_{Guid.NewGuid():N}";
        return CreateCeilingGrid(payload);
    }

    private static Point3d PromptPoint(Editor ed, string message)
    {
        var r = ed.GetPoint(new PromptPointOptions(message));
        if (r.Status != PromptStatus.OK) throw new InvalidOperationException("Point selection cancelled.");
        return r.Value;
    }

    private static object CreateWallSystem(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var ed = doc.Editor;
        Point3d p1, p2;
        if (payload["p1"] is JObject p1j && payload["p2"] is JObject p2j)
        {
            p1 = new Point3d((double?)p1j["x"] ?? 0, (double?)p1j["y"] ?? 0, 0);
            p2 = new Point3d((double?)p2j["x"] ?? 0, (double?)p2j["y"] ?? 0, 0);
        }
        else
        {
            p1 = PromptPoint(ed, "\nHNL Smart Wall - điểm đầu: ");
            var opt = new PromptPointOptions("\nĐiểm cuối: ") { BasePoint = p1, UseBasePoint = true };
            var r2 = ed.GetPoint(opt);
            if (r2.Status != PromptStatus.OK) throw new InvalidOperationException("Point selection cancelled.");
            p2 = r2.Value;
        }

        var boardWidth = Math.Max(100.0, (double?)payload["boardWidthMm"] ?? 1220.0);
        var division = (int?)payload["studDivision"] ?? 3;
        if (division != 2 && division != 3) throw new InvalidOperationException("HNL wall studDivision must be 2 or 3.");
        var studSpacing = boardWidth / division;
        var height = Math.Max(100.0, (double?)payload["heightMm"] ?? 3000.0);
        var studLayer = ((string?)payload["studLayer"] ?? "HNL-WALL-STUD").Trim();
        var trackLayer = ((string?)payload["trackLayer"] ?? "HNL-WALL-TRACK").Trim();
        var smartId = ((string?)payload["id"] ?? $"WALL_{Guid.NewGuid():N}").Trim();

        var vector = p2 - p1;
        var length = vector.Length;
        if (length < 1e-6) throw new InvalidOperationException("Wall length is zero.");
        var dir = vector.GetNormal();
        var perp = new Vector3d(-dir.Y, dir.X, 0);
        var studHalf = 60.0;
        var studCount = Math.Max(2, (int)Math.Ceiling(length / studSpacing) + 1);

        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            EnsureLayer(tr, doc.Database, studLayer);
            EnsureLayer(tr, doc.Database, trackLayer);
            EnsureHnlRegApp(tr, doc.Database);
            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var meta = $"board=1220x2440;division={division};spacing={studSpacing:0.###};height={height:0.###}";

            var track = new Line(p1, p2) { Layer = trackLayer };
            TagHnlEntity(track, "WALL_TRACK", smartId, meta);
            ms.AppendEntity(track); tr.AddNewlyCreatedDBObject(track, true);

            for (var i = 0; i < studCount; i++)
            {
                var d = Math.Min(length, i * studSpacing);
                if (i == studCount - 1) d = length;
                var c = p1 + dir * d;
                var stud = new Line(c - perp * studHalf, c + perp * studHalf) { Layer = studLayer };
                TagHnlEntity(stud, "WALL_STUD", smartId, meta);
                ms.AppendEntity(stud); tr.AddNewlyCreatedDBObject(stud, true);
            }
            tr.Commit();
        }

        return new {
            created = true, smartId,
            boardWidthMm = boardWidth, boardLengthMm = 2440.0,
            studDivision = division, studSpacingMm = studSpacing,
            wallLengthMm = length, heightMm = height, studCount
        };
    }

    private static ObjectId EnsureBuiltinLibraryBlock(Transaction tr, Database db, string symbolKey, string blockName)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        if (bt.Has(blockName)) return bt[blockName];

        bt.UpgradeOpen();
        var btr = new BlockTableRecord { Name = blockName, Origin = Point3d.Origin };
        var id = bt.Add(btr);
        tr.AddNewlyCreatedDBObject(btr, true);

        void Add(Entity e) { btr.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true); }
        DBText Text(string value, Point3d pos, double h=80) => new DBText { TextString=value, Position=pos, Height=h };

        switch ((symbolKey ?? "").ToUpperInvariant())
        {
            case "LEVEL_MARK":
                Add(new Line(new Point3d(-180,0,0), new Point3d(180,0,0)));
                Add(new Line(new Point3d(0,0,0), new Point3d(60,60,0)));
                Add(new Line(new Point3d(0,0,0), new Point3d(60,-60,0)));
                Add(Text("±0.000", new Point3d(80,20,0), 70));
                break;
            case "SECTION_MARK":
                Add(new Circle(Point3d.Origin, Vector3d.ZAxis, 120));
                Add(new Line(new Point3d(-260,0,0), new Point3d(260,0,0)));
                Add(Text("A", new Point3d(-30,-35,0), 80));
                break;
            case "DETAIL_MARK":
                Add(new Circle(Point3d.Origin, Vector3d.ZAxis, 140));
                Add(Text("D", new Point3d(-35,-40,0), 90));
                break;
            case "BOARD_START":
                Add(new Circle(Point3d.Origin, Vector3d.ZAxis, 50));
                Add(new Line(Point3d.Origin, new Point3d(300,0,0)));
                Add(new Line(new Point3d(300,0,0), new Point3d(230,45,0)));
                Add(new Line(new Point3d(300,0,0), new Point3d(230,-45,0)));
                Add(Text("1220x2440", new Point3d(70,60,0), 55));
                break;
            case "CEILING_MAIN":
                Add(new Line(new Point3d(-250,0,0), new Point3d(250,0,0)));
                Add(Text("MAIN", new Point3d(-100,40,0), 55));
                break;
            case "CEILING_CROSS":
                Add(new Line(new Point3d(0,-250,0), new Point3d(0,250,0)));
                Add(Text("CROSS", new Point3d(40,-25,0), 55));
                break;
            case "CEILING_HANGER":
                Add(new Circle(Point3d.Origin, Vector3d.ZAxis, 40));
                Add(new Line(new Point3d(-65,0,0), new Point3d(65,0,0)));
                Add(new Line(new Point3d(0,-65,0), new Point3d(0,65,0)));
                break;
            case "WALL_STUD":
                Add(new Line(new Point3d(-37.5,-15,0), new Point3d(37.5,-15,0)));
                Add(new Line(new Point3d(37.5,-15,0), new Point3d(37.5,15,0)));
                Add(new Line(new Point3d(37.5,15,0), new Point3d(-37.5,15,0)));
                Add(new Line(new Point3d(-37.5,15,0), new Point3d(-37.5,-15,0)));
                break;
            case "WALL_TRACK":
                Add(new Line(new Point3d(-150,-35,0), new Point3d(150,-35,0)));
                Add(new Line(new Point3d(-150,35,0), new Point3d(150,35,0)));
                break;
            case "DOOR_JAMB":
                Add(new Line(new Point3d(-55,-45,0), new Point3d(-55,45,0)));
                Add(new Line(new Point3d(-25,-45,0), new Point3d(-25,45,0)));
                Add(new Line(new Point3d(25,-45,0), new Point3d(25,45,0)));
                Add(new Line(new Point3d(55,-45,0), new Point3d(55,45,0)));
                break;
            case "STEEL_RHS":
                Add(new Line(new Point3d(-40,-20,0), new Point3d(40,-20,0)));
                Add(new Line(new Point3d(40,-20,0), new Point3d(40,20,0)));
                Add(new Line(new Point3d(40,20,0), new Point3d(-40,20,0)));
                Add(new Line(new Point3d(-40,20,0), new Point3d(-40,-20,0)));
                Add(Text("RHS", new Point3d(-25,-10,0), 25));
                break;
            case "STEEL_PLATE":
                Add(new Line(new Point3d(-75,-50,0), new Point3d(75,-50,0)));
                Add(new Line(new Point3d(75,-50,0), new Point3d(75,50,0)));
                Add(new Line(new Point3d(75,50,0), new Point3d(-75,50,0)));
                Add(new Line(new Point3d(-75,50,0), new Point3d(-75,-50,0)));
                break;
            default:
                Add(new Line(new Point3d(-150,0,0), new Point3d(150,0,0)));
                Add(new Line(new Point3d(0,-75,0), new Point3d(0,75,0)));
                Add(Text(symbolKey ?? "HNL", new Point3d(20,30,0), 45));
                break;
        }
        return id;
    }

    private static bool HasExplicitPoint(JObject payload)
    {
        return payload["point"] is JObject;
    }

    private static object GetLibraryInsertStatus()
    {
        return new
        {
            pendingCount = PendingLibraryInserts.Count,
            status = _lastLibraryInsertStatus,
            drawingName = Application.DocumentManager.MdiActiveDocument?.Name
        };
    }

    private static object QueueLibraryInsert(string action, JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");

        PendingLibraryInserts.Enqueue(new PendingLibraryInsert
        {
            Action = action,
            Payload = (JObject)payload.DeepClone()
        });

        _lastLibraryInsertStatus = $"Queued {action}; waiting for insertion point";
        doc.SendStringToExecute("HNLINSERTPENDING ", true, false, true);

        return new
        {
            queued = true,
            awaitingPoint = true,
            action,
            pendingCount = PendingLibraryInserts.Count,
            drawingName = doc.Name,
            message = "Switch to AutoCAD and pick the insertion point."
        };
    }

    private static object QueueOrInsertLibraryBlock(JObject payload)
    {
        return HasExplicitPoint(payload)
            ? InsertLibraryBlock(payload)
            : QueueLibraryInsert("INSERT_LIBRARY_BLOCK", payload);
    }

    private static object QueueOrImportLibraryDefinition(JObject payload)
    {
        return HasExplicitPoint(payload)
            ? ImportLibraryDefinition(payload)
            : QueueLibraryInsert("IMPORT_LIBRARY_DEFINITION", payload);
    }

    private static object InsertLibraryBlock(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var ed = doc.Editor;
        var symbolKey = ((string?)payload["symbolKey"] ?? "HNL_SYMBOL").Trim().ToUpperInvariant();
        var name = ((string?)payload["name"] ?? symbolKey).Trim();
        var layer = ((string?)payload["layer"] ?? "HNL_LIBRARY").Trim();
        var sourceDwg = ((string?)payload["sourceDwg"] ?? "").Trim();
        var blockName = "HNL_" + new string(name.Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_').ToArray());
        var point = payload["point"] is JObject pj
            ? new Point3d((double?)pj["x"] ?? 0, (double?)pj["y"] ?? 0, 0)
            : PromptPoint(ed, $"\nĐiểm chèn {name}: ");
        var scale = Math.Max(0.001, (double?)payload["scale"] ?? 1.0);
        var rotation = ((double?)payload["rotationDeg"] ?? 0.0) * Math.PI / 180.0;

        using (doc.LockDocument())
        {
            ObjectId blockId;
            if (!string.IsNullOrWhiteSpace(sourceDwg))
            {
                if (!File.Exists(sourceDwg)) throw new FileNotFoundException("Library DWG not found.", sourceDwg);
                using var srcDb = new Database(false, true);
                srcDb.ReadDwgFile(sourceDwg, FileOpenMode.OpenForReadAndAllShare, true, "");
                srcDb.CloseInput(true);
                blockId = doc.Database.Insert(blockName, srcDb, false);
            }
            else
            {
                using var tr0 = doc.Database.TransactionManager.StartTransaction();
                blockId = EnsureBuiltinLibraryBlock(tr0, doc.Database, symbolKey, blockName);
                tr0.Commit();
            }

            using var tr = doc.Database.TransactionManager.StartTransaction();
            EnsureLayer(tr, doc.Database, layer);
            EnsureHnlRegApp(tr, doc.Database);
            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var br = new BlockReference(point, blockId) {
                Layer = layer,
                Rotation = rotation,
                ScaleFactors = new Scale3d(scale)
            };
            TagHnlEntity(br, "LIBRARY_BLOCK", blockName, $"symbol={symbolKey};source={sourceDwg}");
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            tr.Commit();
        }
        string handle = "";
        bool isDynamicBlock = false;
        object[] dynamicProperties = Array.Empty<object>();

        using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead, false) is BlockReference br)) continue;
                if (!string.Equals(br.Name, blockName, StringComparison.OrdinalIgnoreCase)) continue;
                handle = br.Handle.ToString();
            }
            if (!string.IsNullOrWhiteSpace(handle))
            {
                var id = ObjectIdFromHandle(doc.Database, handle);
                if (!id.IsNull && tr.GetObject(id, OpenMode.ForRead, false) is BlockReference inserted)
                {
                    isDynamicBlock = inserted.IsDynamicBlock;
                    if (isDynamicBlock) dynamicProperties = ReadDynamicProperties(inserted);
                }
            }
        }

        return new {
            inserted=true, blockName, symbolKey, layer, sourceDwg,
            point=new {x=point.X,y=point.Y}, scale, rotationDeg=rotation*180.0/Math.PI,
            handle, isDynamicBlock, dynamicProperties
        };
    }

    private static object InspectLibraryDwg(JObject payload)
    {
        var filePath = ((string?)payload["filePath"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("filePath required");
        if (!File.Exists(filePath)) throw new FileNotFoundException("Library DWG not found.", filePath);

        using var db = new Database(false, true);
        db.ReadDwgFile(filePath, FileOpenMode.OpenForReadAndAllShare, true, "");
        db.CloseInput(true);

        var definitions = new List<object>();
        int modelEntityCount = 0;
        int dynamicDefinitionCount = 0;

        using (var tr = db.TransactionManager.StartOpenCloseTransaction())
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in bt)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead, false) is BlockTableRecord btr)) continue;
                if (btr.IsLayout || btr.IsAnonymous) continue;

                var name = btr.Name ?? "";
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith("*", StringComparison.Ordinal)) continue;

                int entityCount = 0;
                int attributeCount = 0;
                foreach (ObjectId eid in btr)
                {
                    entityCount++;
                    if (tr.GetObject(eid, OpenMode.ForRead, false) is AttributeDefinition) attributeCount++;
                }

                bool isDynamic = false;
                try
                {
                    // Reflection avoids hard compile dependency if one AutoCAD API version exposes this differently.
                    var prop = btr.GetType().GetProperty("IsDynamicBlock");
                    if (prop?.GetValue(btr) is bool dyn) isDynamic = dyn;
                }
                catch { }

                if (isDynamic) dynamicDefinitionCount++;
                definitions.Add(new { name, entityCount, attributeCount, isDynamic });
            }

            if (bt.Has(BlockTableRecord.ModelSpace))
            {
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId _ in ms) modelEntityCount++;
            }
        }

        return new {
            filePath,
            fileName = Path.GetFileName(filePath),
            modelEntityCount,
            definitions,
            definitionCount = definitions.Count,
            dynamicDefinitionCount,
            units = db.Insunits.ToString()
        };
    }

    private static object ImportLibraryDefinition(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var filePath = ((string?)payload["filePath"] ?? "").Trim();
        var definitionName = ((string?)payload["definitionName"] ?? "").Trim();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Library DWG not found.", filePath);
        if (string.IsNullOrWhiteSpace(definitionName))
            throw new ArgumentException("definitionName required");

        var layer = ((string?)payload["layer"] ?? "HNL-DATA-FIELD").Trim();
        var replaceDefinition = (bool?)payload["replaceDefinition"] ?? false;
        var point = payload["point"] is JObject pj
            ? new Point3d((double?)pj["x"] ?? 0, (double?)pj["y"] ?? 0, 0)
            : PromptPoint(doc.Editor, $"\nĐiểm chèn {definitionName}: ");
        var scale = Math.Max(0.001, (double?)payload["scale"] ?? 1.0);
        var rotationDeg = (double?)payload["rotationDeg"] ?? 0.0;
        var rotation = rotationDeg * Math.PI / 180.0;

        using var srcDb = new Database(false, true);
        srcDb.ReadDwgFile(filePath, FileOpenMode.OpenForReadAndAllShare, true, "");
        srcDb.CloseInput(true);

        ObjectId sourceDefinitionId;
        using (var srcTr = srcDb.TransactionManager.StartOpenCloseTransaction())
        {
            var srcBt = (BlockTable)srcTr.GetObject(srcDb.BlockTableId, OpenMode.ForRead);
            if (!srcBt.Has(definitionName))
                throw new InvalidOperationException($"Block definition not found in source DWG: {definitionName}");
            sourceDefinitionId = srcBt[definitionName];
        }

        string handle = "";
        bool isDynamicBlock = false;
        object[] dynamicProperties = Array.Empty<object>();

        using (doc.LockDocument())
        {
            var ids = new ObjectIdCollection(new[] { sourceDefinitionId });
            var mapping = new IdMapping();

            srcDb.WblockCloneObjects(
                ids,
                doc.Database.BlockTableId,
                mapping,
                replaceDefinition ? DuplicateRecordCloning.Replace : DuplicateRecordCloning.Ignore,
                false
            );

            using var tr = doc.Database.TransactionManager.StartTransaction();
            EnsureLayer(tr, doc.Database, layer);
            EnsureHnlRegApp(tr, doc.Database);

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(definitionName))
                throw new InvalidOperationException($"Imported block definition missing: {definitionName}");

            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var br = new BlockReference(point, bt[definitionName]) {
                Layer = layer,
                Rotation = rotation,
                ScaleFactors = new Scale3d(scale)
            };

            TagHnlEntity(br, "LIBRARY_BLOCK", definitionName, $"source={filePath};definition={definitionName}");
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);

            var def = (BlockTableRecord)tr.GetObject(bt[definitionName], OpenMode.ForRead);
            if (def.HasAttributeDefinitions)
            {
                foreach (ObjectId aid in def)
                {
                    if (!(tr.GetObject(aid, OpenMode.ForRead, false) is AttributeDefinition ad) || ad.Constant) continue;
                    var ar = new AttributeReference();
                    ar.SetAttributeFromBlock(ad, br.BlockTransform);
                    ar.TextString = ad.TextString;
                    br.AttributeCollection.AppendAttribute(ar);
                    tr.AddNewlyCreatedDBObject(ar, true);
                }
            }

            tr.Commit();
            handle = br.Handle.ToString();
        }

        using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var id = ObjectIdFromHandle(doc.Database, handle);
            if (!id.IsNull && tr.GetObject(id, OpenMode.ForRead, false) is BlockReference inserted)
            {
                isDynamicBlock = inserted.IsDynamicBlock;
                if (isDynamicBlock) dynamicProperties = ReadDynamicProperties(inserted);
            }
        }

        return new {
            inserted = true,
            blockName = definitionName,
            definitionName,
            handle,
            layer,
            point = new { x = point.X, y = point.Y },
            scale,
            rotationDeg,
            isDynamicBlock,
            dynamicProperties,
            sourceDwg = filePath,
            replaceDefinition
        };
    }

    private static object GetHnlBoq()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        double mainLm=0, crossLm=0, wallTrackLm=0;
        int hanger=0, wallStud=0, libraryBlocks=0, smartEntities=0;
        using var tr = doc.Database.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity ent)) continue;
            var tag = ReadHnlTag(ent);
            if (tag == null) continue;
            smartEntities++;
            switch (tag.Value.component)
            {
                case "CEILING_MAIN": if (ent is Line ml) mainLm += ml.Length / 1000.0; break;
                case "CEILING_CROSS": if (ent is Line cl) crossLm += cl.Length / 1000.0; break;
                case "CEILING_HANGER": hanger++; break;
                case "WALL_TRACK": if (ent is Line wl) wallTrackLm += wl.Length / 1000.0; break;
                case "WALL_STUD": wallStud++; break;
                case "LIBRARY_BLOCK": libraryBlocks++; break;
            }
        }
        tr.Commit();
        return new {
            smartEntities,
            ceilingMainLm=Math.Round(mainLm,2),
            ceilingCrossLm=Math.Round(crossLm,2),
            ceilingHangerPcs=hanger,
            wallTrackLm=Math.Round(wallTrackLm,2),
            wallStudPcs=wallStud,
            libraryBlocks
        };
    }

    private static object AuditHnlShopdrawing()
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var issues = new List<object>();
        int smartEntities=0;
        using var tr = doc.Database.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity ent)) continue;
            var tag = ReadHnlTag(ent);
            if (tag != null) smartEntities++;
            if (ent is BlockReference br && br.Name.StartsWith("HNL_", StringComparison.OrdinalIgnoreCase))
            {
                var sc = br.ScaleFactors;
                if (Math.Abs(sc.X-sc.Y)>1e-6 || Math.Abs(sc.X-1.0)>1e-6)
                    issues.Add(new { severity="WARNING", handle=ent.Handle.ToString(), category="BLOCK_SCALE", message=$"{br.Name} scale = {sc.X:0.###},{sc.Y:0.###},{sc.Z:0.###}" });
            }
        }
        tr.Commit();
        if (smartEntities == 0)
            issues.Add(new { severity="INFO", handle="", category="SMART_DATA", message="DWG chưa có HNL Smart Object/XData." });
        return new { smartEntities, issueCount=issues.Count, issues };
    }


    private static ObjectId ObjectIdFromHandle(Database db, string? handleText)
    {
        if (string.IsNullOrWhiteSpace(handleText)) return ObjectId.Null;
        if (!long.TryParse(handleText, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
            return ObjectId.Null;
        try { return db.GetObjectId(false, new Handle(value), 0); }
        catch { return ObjectId.Null; }
    }

    private static string CadColorHex(Entity ent)
    {
        try
        {
            var c = ent.Color;
            if (c.ColorMethod == ColorMethod.ByLayer || c.ColorMethod == ColorMethod.ByBlock) return "";
            switch (c.ColorIndex)
            {
                case 1: return "#FF0000"; case 2: return "#FFFF00"; case 3: return "#00FF00";
                case 4: return "#00FFFF"; case 5: return "#0000FF"; case 6: return "#FF00FF";
                case 7: return "#FFFFFF"; case 8: return "#808080"; case 30: return "#FF7F00";
                case 92: return "#80E680"; case 151: return "#99D9FF";
                default: return "";
            }
        }
        catch { return ""; }
    }

    private static object? SnapshotEntity(Transaction tr, Entity ent)
    {
        var common = new Dictionary<string, object?> {
            ["id"] = "dwg_" + ent.Handle,
            ["handle"] = ent.Handle.ToString(),
            ["layer"] = ent.Layer,
            ["color"] = CadColorHex(ent),
            ["nativeType"] = ent.GetType().Name,
        };
        if (ent is Line ln)
        {
            common["type"]="LINE";
            common["start"]=new { x=ln.StartPoint.X, y=ln.StartPoint.Y };
            common["end"]=new { x=ln.EndPoint.X, y=ln.EndPoint.Y };
            return common;
        }
        if (ent is Polyline pl)
        {
            var pts=new List<object>();
            for(int i=0;i<pl.NumberOfVertices;i++) { var q=pl.GetPoint2dAt(i); pts.Add(new {x=q.X,y=q.Y}); }
            common["type"]="POLYLINE"; common["points"]=pts; common["closed"]=pl.Closed;
            try { common["area"]=pl.Closed ? pl.Area : 0.0; } catch { }
            try { common["length"]=pl.Length; } catch { }
            return common;
        }
        if (ent is Circle cir)
        {
            common["type"]="CIRCLE";
            common["center"]=new {x=cir.Center.X,y=cir.Center.Y}; common["radius"]=cir.Radius;
            return common;
        }
        if (ent is DBText tx)
        {
            common["type"]="TEXT"; common["position"]=new {x=tx.Position.X,y=tx.Position.Y};
            common["text"]=tx.TextString; common["height"]=tx.Height; common["rotation"]=tx.Rotation*180.0/Math.PI;
            return common;
        }
        if (ent is MText mt)
        {
            common["type"]="MTEXT"; common["position"]=new {x=mt.Location.X,y=mt.Location.Y};
            common["text"]=mt.Contents; common["height"]=mt.TextHeight; common["rotation"]=mt.Rotation*180.0/Math.PI;
            return common;
        }
        if (ent is BlockReference br)
        {
            common["type"]="BLOCK_REF"; common["position"]=new {x=br.Position.X,y=br.Position.Y};
            common["blockName"]=br.Name; common["rotation"]=br.Rotation*180.0/Math.PI;
            common["scale"]=new {x=br.ScaleFactors.X,y=br.ScaleFactors.Y,z=br.ScaleFactors.Z};
            return common;
        }
        return null;
    }

    private static object GetModelspaceSnapshot(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var max=Math.Max(100,Math.Min(50000,(int?)payload["maxEntities"] ?? 12000));
        var result=new List<object>(); var unsupported=0; var total=0; var truncated=false;
        using var tr=doc.Database.TransactionManager.StartTransaction();
        var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
        var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
        foreach(ObjectId id in ms)
        {
            total++;
            if(result.Count>=max){truncated=true;break;}
            if(!(tr.GetObject(id,OpenMode.ForRead,false) is Entity ent)){unsupported++;continue;}
            var item=SnapshotEntity(tr,ent); if(item==null){unsupported++;continue;} result.Add(item);
        }
        tr.Commit();
        var selected=GetSelectionPayload();
        return new {
            drawingName=doc.Name,
            currentLayout=LayoutManager.Current.CurrentLayout,
            entities=result,
            returned=result.Count,total,unsupported,truncated,
            selection=selected,
            mode="DIRECT_DWG"
        };
    }

    private static object SelectHandles(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var handles=payload["handles"]?.Values<string>().Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        var ids=new List<ObjectId>();
        foreach(var h in handles){var id=ObjectIdFromHandle(doc.Database,h);if(!id.IsNull&&id.IsValid&&!id.IsErased)ids.Add(id);}
        doc.Editor.SetImpliedSelection(ids.ToArray());
        return new {count=ids.Count,handles};
    }

    private static object CreateNativeEntity(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var entity=payload["entity"] as JObject ?? payload;
        var type=((string?)entity["type"] ?? "").ToUpperInvariant();
        var layer=((string?)entity["layer"] ?? "0").Trim();
        Entity created;
        static Point3d P3(JToken? t) => t is JObject o ? new Point3d((double?)o["x"]??0,(double?)o["y"]??0,0) : Point3d.Origin;

        switch(type)
        {
            case "LINE": created=new Line(P3(entity["start"]),P3(entity["end"])); break;
            case "WALL":
                var wp1=P3(entity["p1"]); var wp2=P3(entity["p2"]); var thickness=Math.Max(1e-6,(double?)entity["thickness"]??100.0);
                var wv=wp2-wp1; if(wv.Length<1e-9) throw new InvalidOperationException("Wall length is zero.");
                var wn=wv.GetNormal(); var wperp=new Vector3d(-wn.Y,wn.X,0)*(thickness/2.0);
                var wpl=new Polyline(4);
                var w0=wp1+wperp; var w1=wp2+wperp; var w2=wp2-wperp; var w3=wp1-wperp;
                wpl.AddVertexAt(0,new Point2d(w0.X,w0.Y),0,0,0); wpl.AddVertexAt(1,new Point2d(w1.X,w1.Y),0,0,0);
                wpl.AddVertexAt(2,new Point2d(w2.X,w2.Y),0,0,0); wpl.AddVertexAt(3,new Point2d(w3.X,w3.Y),0,0,0); wpl.Closed=true; created=wpl; break;
            case "CIRCLE": created=new Circle(P3(entity["center"]),Vector3d.ZAxis,Math.Max(1e-6,(double?)entity["radius"]??1)); break;
            case "POLYLINE":
                var points=entity["points"] as JArray ?? new JArray();
                var pl=new Polyline(); int pi=0; foreach(var token in points){var q=P3(token);pl.AddVertexAt(pi++,new Point2d(q.X,q.Y),0,0,0);} pl.Closed=(bool?)entity["closed"]??false; created=pl; break;
            case "RECTANGLE":
                var x=(double?)entity["x"]??0;var y=(double?)entity["y"]??0;var w=(double?)entity["width"]??0;var h=(double?)entity["height"]??0;
                var rp=new Polyline(4);rp.AddVertexAt(0,new Point2d(x,y),0,0,0);rp.AddVertexAt(1,new Point2d(x+w,y),0,0,0);rp.AddVertexAt(2,new Point2d(x+w,y+h),0,0,0);rp.AddVertexAt(3,new Point2d(x,y+h),0,0,0);rp.Closed=true;created=rp;break;
            case "TEXT":
                created=new DBText{Position=P3(entity["position"]),TextString=(string?)entity["text"]??"",Height=Math.Max(1,(double?)entity["height"]??250),Rotation=((double?)entity["rotation"]??0)*Math.PI/180.0};break;
            case "MTEXT":
                created=new MText{Location=P3(entity["position"]),Contents=(string?)entity["text"]??"",TextHeight=Math.Max(1,(double?)entity["height"]??250),Rotation=((double?)entity["rotation"]??0)*Math.PI/180.0};break;
            default: throw new InvalidOperationException($"Direct DWG create does not support entity type: {type}");
        }
        using(doc.LockDocument()) using(var tr=doc.Database.TransactionManager.StartTransaction())
        {
            EnsureLayer(tr,doc.Database,layer); created.Layer=layer;
            var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
            var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
            ms.AppendEntity(created);tr.AddNewlyCreatedDBObject(created,true);tr.Commit();
        }
        return new {created=true,handle=created.Handle.ToString(),type,layer};
    }

    private static object ApplyEntityTransform(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var handles=payload["handles"]?.Values<string>().Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        var op=((string?)payload["operation"]??"MOVE").ToUpperInvariant(); int changed=0;
        using(doc.LockDocument()) using(var tr=doc.Database.TransactionManager.StartTransaction())
        {
            foreach(var h in handles)
            {
                var id=ObjectIdFromHandle(doc.Database,h); if(id.IsNull||!id.IsValid||id.IsErased)continue;
                if(!(tr.GetObject(id,OpenMode.ForWrite,false) is Entity ent))continue;
                Matrix3d m;
                if(op=="MOVE") m=Matrix3d.Displacement(new Vector3d((double?)payload["dx"]??0,(double?)payload["dy"]??0,0));
                else if(op=="ROTATE")
                {
                    var basePt=payload["basePoint"] is JObject bp ? new Point3d((double?)bp["x"]??0,(double?)bp["y"]??0,0) : Point3d.Origin;
                    m=Matrix3d.Rotation(((double?)payload["angleDeg"]??0)*Math.PI/180.0,Vector3d.ZAxis,basePt);
                }
                else if(op=="SCALE")
                {
                    var basePt=payload["basePoint"] is JObject bp ? new Point3d((double?)bp["x"]??0,(double?)bp["y"]??0,0) : Point3d.Origin;
                    m=Matrix3d.Scaling(Math.Max(1e-6,(double?)payload["factor"]??1),basePt);
                }
                else throw new InvalidOperationException($"Unsupported direct transform: {op}");
                ent.TransformBy(m); changed++;
            }
            tr.Commit();
        }
        return new {changed,operation=op,handles};
    }

    private static object EraseHandles(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var handles=payload["handles"]?.Values<string>().Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();int erased=0;
        using(doc.LockDocument()) using(var tr=doc.Database.TransactionManager.StartTransaction())
        {
            foreach(var h in handles){var id=ObjectIdFromHandle(doc.Database,h);if(id.IsNull||!id.IsValid||id.IsErased)continue;if(tr.GetObject(id,OpenMode.ForWrite,false) is DBObject obj){obj.Erase();erased++;}}
            tr.Commit();
        }
        doc.Editor.SetImpliedSelection(Array.Empty<ObjectId>());
        return new {erased,handles};
    }

    private static object SetEntityLayer(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var handles=payload["handles"]?.Values<string>().Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        var layer=((string?)payload["layer"]??"").Trim();if(string.IsNullOrWhiteSpace(layer))throw new ArgumentException("layer required");int changed=0;
        using(doc.LockDocument()) using(var tr=doc.Database.TransactionManager.StartTransaction())
        {
            EnsureLayer(tr,doc.Database,layer);
            foreach(var h in handles){var id=ObjectIdFromHandle(doc.Database,h);if(id.IsNull||!id.IsValid||id.IsErased)continue;if(tr.GetObject(id,OpenMode.ForWrite,false) is Entity ent){ent.Layer=layer;changed++;}}
            tr.Commit();
        }
        return new {changed,layer,handles};
    }

    private static object UpdateTextContents(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var updates=payload["updates"] as JArray ?? new JArray();
        int changed=0, skipped=0;
        using(doc.LockDocument()) using(var tr=doc.Database.TransactionManager.StartTransaction())
        {
            foreach(var token in updates.OfType<JObject>())
            {
                var handle=(string?)token["handle"] ?? "";
                var text=(string?)token["text"] ?? "";
                var id=ObjectIdFromHandle(doc.Database,handle);
                if(id.IsNull||!id.IsValid||id.IsErased){skipped++;continue;}
                var obj=tr.GetObject(id,OpenMode.ForWrite,false);
                if(obj is DBText tx){tx.TextString=text;changed++;}
                else if(obj is MText mt){mt.Contents=text;changed++;}
                else skipped++;
            }
            tr.Commit();
        }
        return new {changed,skipped};
    }

    private static object InsertExistingBlock(JObject payload)
    {
        var doc=Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var blockName=((string?)payload["blockName"] ?? "").Trim();
        if(string.IsNullOrWhiteSpace(blockName)) throw new ArgumentException("blockName required");
        var layer=((string?)payload["layer"] ?? "0").Trim();
        var point=payload["point"] is JObject pj
            ? new Point3d((double?)pj["x"]??0,(double?)pj["y"]??0,0)
            : Point3d.Origin;
        var rotation=((double?)payload["rotationDeg"]??0)*Math.PI/180.0;
        var scale=Math.Max(1e-6,(double?)payload["scale"]??1.0);
        var attrs=payload["attributes"] as JObject;
        string handle="";
        using(doc.LockDocument()) using(var tr=doc.Database.TransactionManager.StartTransaction())
        {
            var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
            if(!bt.Has(blockName)) throw new InvalidOperationException($"Block definition not found in DWG: {blockName}");
            EnsureLayer(tr,doc.Database,layer);
            var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
            var br=new BlockReference(point,bt[blockName]){Layer=layer,Rotation=rotation,ScaleFactors=new Scale3d(scale)};
            ms.AppendEntity(br);tr.AddNewlyCreatedDBObject(br,true);
            var def=(BlockTableRecord)tr.GetObject(bt[blockName],OpenMode.ForRead);
            if(def.HasAttributeDefinitions)
            {
                foreach(ObjectId id in def)
                {
                    if(!(tr.GetObject(id,OpenMode.ForRead,false) is AttributeDefinition ad) || ad.Constant) continue;
                    var ar=new AttributeReference();
                    ar.SetAttributeFromBlock(ad,br.BlockTransform);
                    var supplied=attrs?[ad.Tag];
                    if(supplied!=null) ar.TextString=Convert.ToString(supplied) ?? ad.TextString;
                    br.AttributeCollection.AppendAttribute(ar);tr.AddNewlyCreatedDBObject(ar,true);
                }
            }
            tr.Commit();handle=br.Handle.ToString();
        }
        object[] dynamicProperties = Array.Empty<object>();
        bool isDynamicBlock = false;
        using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var id = ObjectIdFromHandle(doc.Database, handle);
            if (!id.IsNull && tr.GetObject(id, OpenMode.ForRead, false) is BlockReference inserted)
            {
                isDynamicBlock = inserted.IsDynamicBlock;
                if (isDynamicBlock) dynamicProperties = ReadDynamicProperties(inserted);
            }
        }
        return new {inserted=true,blockName,handle,layer,point=new{x=point.X,y=point.Y},isDynamicBlock,dynamicProperties};
    }

    private static object NormalizeDynamicValue(object? value)
    {
        if (value == null) return "";
        if (value is double || value is float || value is decimal || value is int || value is long || value is short || value is bool || value is string) return value;
        return Convert.ToString(value) ?? "";
    }

    private static object[] ReadDynamicProperties(BlockReference br)
    {
        if (!br.IsDynamicBlock) return Array.Empty<object>();
        var result = new List<object>();
        foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
        {
            object[] allowed = Array.Empty<object>();
            try { allowed = prop.GetAllowedValues().Select(NormalizeDynamicValue).ToArray(); } catch { }
            result.Add(new {
                name = prop.PropertyName,
                value = NormalizeDynamicValue(prop.Value),
                readOnly = prop.ReadOnly,
                unitsType = prop.UnitsType.ToString(),
                allowedValues = allowed
            });
        }
        return result.ToArray();
    }

    private static object GetDynamicBlockProperties(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var handle = ((string?)payload["handle"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentException("handle required");
        using var tr = doc.Database.TransactionManager.StartOpenCloseTransaction();
        var id = ObjectIdFromHandle(doc.Database, handle);
        if (id.IsNull || !id.IsValid || id.IsErased) throw new InvalidOperationException("Block handle not found.");
        var br = tr.GetObject(id, OpenMode.ForRead, false) as BlockReference ?? throw new InvalidOperationException("Handle is not a BlockReference.");
        return new { handle, blockName = br.Name, isDynamicBlock = br.IsDynamicBlock, properties = ReadDynamicProperties(br) };
    }

    private static object ConvertDynamicInput(JToken token, object current)
    {
        if (current is double || current is float || current is decimal) return token.Value<double>();
        if (current is int) return token.Value<int>();
        if (current is short) return token.Value<short>();
        if (current is long) return token.Value<long>();
        if (current is bool) return token.Value<bool>();
        return token.Type == JTokenType.String ? token.Value<string>() ?? "" : token.ToString();
    }

    private static object SetDynamicBlockProperties(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No active drawing.");
        var handle = ((string?)payload["handle"] ?? "").Trim();
        var updates = payload["properties"] as JObject ?? throw new ArgumentException("properties required");
        int changed = 0, skipped = 0;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var id = ObjectIdFromHandle(doc.Database, handle);
            if (id.IsNull || !id.IsValid || id.IsErased) throw new InvalidOperationException("Block handle not found.");
            var br = tr.GetObject(id, OpenMode.ForWrite, false) as BlockReference ?? throw new InvalidOperationException("Handle is not a BlockReference.");
            if (!br.IsDynamicBlock) throw new InvalidOperationException("Block is not dynamic.");
            foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
            {
                var token = updates[prop.PropertyName];
                if (token == null) continue;
                if (prop.ReadOnly) { skipped++; continue; }
                try { prop.Value = ConvertDynamicInput(token, prop.Value); changed++; }
                catch { skipped++; }
            }
            tr.Commit();
        }
        return new { handle, changed, skipped, updated = true };
    }

    private static object ConvertDwgToDxfPreview(JObject payload)
    {
        var filePath = (string?)payload["filePath"] ?? throw new ArgumentException("filePath required");
        if (!File.Exists(filePath)) throw new FileNotFoundException("DWG not found.", filePath);
        if (!string.Equals(Path.GetExtension(filePath), ".dwg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Input must be .dwg.");
        var dir = Path.Combine(Path.GetTempPath(), "HNL_CAD_AI", "dwg-preview");
        Directory.CreateDirectory(dir);
        var outputPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}.dxf");
        using var db = new Database(false, true);
        db.ReadDwgFile(filePath, FileOpenMode.OpenForReadAndAllShare, true, "");
        db.CloseInput(true);
        db.DxfOut(outputPath, 16, DwgVersion.Current);
        return new {
            outputPath,
            sourceDwg=filePath,
            bytes=new FileInfo(outputPath).Length,
            mode="HNL_CANVAS_PREVIEW",
            warning="DXF preview does not guarantee full DWG fidelity for dynamic blocks, fields, xrefs, proxy/custom objects."
        };
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

    private static string GetBundledLispFolder()
    {
        try
        {
            var assemblyPath = typeof(BridgeCommands).Assembly.Location;
            var yearFolder = Path.GetDirectoryName(assemblyPath);
            var contentsFolder = string.IsNullOrWhiteSpace(yearFolder)
                ? null
                : Directory.GetParent(yearFolder)?.FullName;
            return string.IsNullOrWhiteSpace(contentsFolder)
                ? ""
                : Path.Combine(contentsFolder, "Lisp");
        }
        catch
        {
            return "";
        }
    }

    private static string[] GetBundledLispFiles()
    {
        var folder = GetBundledLispFolder();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Array.Empty<string>();

        return Directory.GetFiles(folder, "*.lsp", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsPathInsideFolder(string filePath, string folderPath)
    {
        try
        {
            var folder = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var file = Path.GetFullPath(filePath);
            return file.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryResolveBundledLisp(string? requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName)) return null;

        var folder = GetBundledLispFolder();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;

        var safeName = Path.GetFileName(requestedName);
        if (string.IsNullOrWhiteSpace(safeName)) return null;

        var direct = Path.Combine(folder, safeName);
        if (File.Exists(direct) && IsPathInsideFolder(direct, folder)) return direct;

        return GetBundledLispFiles().FirstOrDefault(file =>
            string.Equals(Path.GetFileName(file), safeName, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveLoadLispPath(JObject payload)
    {
        var requestedFilePath = ((string?)payload["filePath"] ?? "").Trim();
        var sourceFile = ((string?)payload["sourceFile"] ?? "").Trim();
        var fileName = ((string?)payload["fileName"] ?? "").Trim();
        var bundled = (bool?)payload["bundled"] ?? false;

        if (bundled)
        {
            var bundledPath =
                TryResolveBundledLisp(sourceFile) ??
                TryResolveBundledLisp(fileName) ??
                TryResolveBundledLisp(requestedFilePath);
            if (!string.IsNullOrWhiteSpace(bundledPath)) return bundledPath;
        }

        if (!string.IsNullOrWhiteSpace(requestedFilePath) && File.Exists(requestedFilePath))
            return requestedFilePath;

        return
            TryResolveBundledLisp(sourceFile) ??
            TryResolveBundledLisp(fileName) ??
            TryResolveBundledLisp(requestedFilePath) ??
            requestedFilePath;
    }

    private static object GetBundledLispAutoLoadStatus()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var files = GetBundledLispFiles();
        var docKey = doc?.GetHashCode() ?? 0;
        return new
        {
            enabled = _lispAutoLoadEnabled,
            mode = _lispAutoLoadEnabled ? "AUTOLOAD_ALL_SESSION" : "ON_DEMAND",
            folder = GetBundledLispFolder(),
            fileCount = files.Length,
            expectedCount = 44,
            complete = files.Length == 44,
            activeDocument = doc?.Name,
            activeDocumentLoaded = doc != null && LispAutoLoadedDocuments.Contains(docKey),
            loadedDocumentCount = LispAutoLoadedDocuments.Count,
            summary = _lispAutoLoadSummary,
            legacyArxAutoLoad = false
        };
    }

    private static void TryAutoLoadBundledLispForActiveDocument()
    {
        if (!_lispAutoLoadEnabled) return;

        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var key = doc.GetHashCode();
        if (LispAutoLoadedDocuments.Contains(key)) return;

        try
        {
            var result = AutoLoadBundledLisp(new JObject());
            var queuedProp = result.GetType().GetProperty("queued");
            var queued = queuedProp?.GetValue(result) as bool?;
            if (queued == true)
                LispAutoLoadedDocuments.Add(key);
        }
        catch (System.Exception ex)
        {
            _lispAutoLoadSummary = "AutoLoad error: " + ex.Message;
        }
    }

    private static object AutoLoadBundledLisp(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");

        var force = (bool?)payload["force"] ?? false;
        if (!_lispAutoLoadEnabled && !force)
        {
            _lispAutoLoadSummary = "AutoLoad disabled";
            return new { queued = false, skipped = true, reason = "DISABLED", count = 0 };
        }

        var files = GetBundledLispFiles();
        if (files.Length == 0)
            throw new DirectoryNotFoundException(
                "Bundled Lisp folder not found or empty: " + GetBundledLispFolder());

        if (files.Length != 44)
            _lispAutoLoadSummary = $"Warning: bundled Lisp count {files.Length}/44";

        // Queue each LOAD separately and catch Lisp-level errors per file so one bad source
        // does not prevent the remaining sources from loading.
        foreach (var filePath in files)
        {
            var safePath = filePath.Replace("\\", "/").Replace("\"", "\\\"");
            var expression = $"(vl-catch-all-apply 'load (list \\\"{safePath}\\\")) ";
            doc.SendStringToExecute(expression, true, false, false);
        }

        LispAutoLoadedDocuments.Add(doc.GetHashCode());
        _lispAutoLoadSummary = $"Queued {files.Length} Lisp files for {Path.GetFileName(doc.Name)}";

        return new
        {
            queued = true,
            count = files.Length,
            expectedCount = 44,
            complete = files.Length == 44,
            force,
            drawingName = doc.Name,
            folder = GetBundledLispFolder(),
            note = "LOAD only; HNL does not auto-run the Lisp commands. AutoCAD Command Line is authoritative for individual source errors."
        };
    }

    private static object LoadLispFile(JObject payload)
    {
        var doc = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");

        var requestedFilePath = ((string?)payload["filePath"] ?? "").Trim();
        var filePath = ResolveLoadLispPath(payload);
        var runCommand = ((string?)payload["runCommand"] ?? "").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath required");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Lisp file not found.", filePath);
        if (!string.Equals(Path.GetExtension(filePath), ".lsp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LOAD_LISP_FILE only accepts .lsp.");

        if (!string.IsNullOrWhiteSpace(runCommand) &&
            !runCommand.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '$'))
            throw new InvalidOperationException("Unsafe Lisp command name.");

        // AutoLISP accepts forward slashes on Windows; escape quotes defensively.
        var safePath = filePath.Replace("\\", "/").Replace("\"", "\\\"");
        doc.SendStringToExecute($"(load \\\"{safePath}\\\") ", true, false, true);

        if (!string.IsNullOrWhiteSpace(runCommand))
            doc.SendStringToExecute($"{runCommand} ", true, false, true);

        return new
        {
            queued = true,
            filePath,
            requestedFilePath,
            resolvedFromBundle = IsPathInsideFolder(filePath, GetBundledLispFolder()),
            runCommand = string.IsNullOrWhiteSpace(runCommand) ? null : runCommand,
            note = "AutoCAD Command Line is authoritative for LOAD/SECURELOAD/DCL/dependency errors."
        };
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
                    lineweight = (int)layer.LineWeight,
                    isPlottable = layer.IsPlottable,
                    linetype = layer.LinetypeObjectId.IsNull ? "" : ((LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead)).Name,
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
            try
            {
                if (locked && db != null)
                {
                    object dbToUnlock = db!;
                    ((dynamic)dbToUnlock).UnlockDb((dynamic)dbToUnlock, false);
                }
            }
            catch { }
            try
            {
                if (db != null)
                {
                    object dbToClose = db!;
                    mgr.Close((dynamic)dbToClose);
                }
            }
            catch { }
        }
    }

}
