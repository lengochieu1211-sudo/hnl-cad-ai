using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
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
            "\nHNL CAD AI Bridge v2.4.7 loaded. Commands: HNLBRIDGESTATUS, HNLBRIDGEPING, HNLPLOTDEVICES, HNLLAYOUTS");
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
            var o=new PromptDoubleOptions($"\n{label} <{def:0}>: "){DefaultValue=def,UseDefaultValue=true,AllowNegative=false,AllowZero=false};
            var r=ed.GetDouble(o); return r.Status==PromptStatus.OK?r.Value:def;
        }
        var main=Ask("Xương chính @ (mm)",exposed?1200:800);
        var cross=Ask("Xương phụ @ (mm)",exposed?600:400);
        var hanger=Ask("Ty treo @ (mm)",exposed?1200:900);
        var ao=new PromptDoubleOptions("\nGóc xoay hệ xương (độ) <0>: "){DefaultValue=0,UseDefaultValue=true,AllowNegative=true,AllowZero=true};
        var ar=ed.GetDouble(ao); var angle=ar.Status==PromptStatus.OK?ar.Value:0;

        try {
            var payload=new JObject{
                ["mainSpacing"]=main,["crossSpacing"]=cross,["hangerSpacing"]=hanger,
                ["rotationDeg"]=angle,["originMode"]="CENTER",["drawHangers"]=true,
                ["mainLayer"]="HNL_CEILING_MAIN",["crossLayer"]="HNL_CEILING_CROSS",["hangerLayer"]="HNL_CEILING_HANGER"
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
                    pluginVersion = "2.4.7",
                    capabilities = new[] { "GET_STATUS","GET_PLOT_DEVICES","GET_LAYOUTS","SET_CURRENT_LAYOUT","RENAME_LAYOUT","EXECUTE_COMMAND","CANCEL_COMMAND","OPEN_DWG","SAVE_CURRENT_DWG","SAVE_AS_DWG","GET_SELECTION","SELECT_ALL","GET_LAYERS","CREATE_CEILING_GRID","PUBLISH_LAYOUTS_PDF","PLOT_CURRENT_PDF","SAVE_DXF_AS_DWG","GET_SHEETSET_INFO","UPDATE_SHEET" }
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
                "SET_CURRENT_LAYOUT" => SetCurrentLayout(payload),
                "RENAME_LAYOUT" => RenameLayout(payload),
                "EXECUTE_COMMAND" => ExecuteNativeCommand(payload),
                "CANCEL_COMMAND" => CancelNativeCommand(),
                "OPEN_DWG" => OpenDwg(payload),
                "SAVE_CURRENT_DWG" => SaveCurrentDwg(),
                "SAVE_AS_DWG" => SaveAsDwg(payload),
                "GET_SELECTION" => GetSelectionPayload(),
                "SELECT_ALL" => SelectAllObjects(),
                "GET_LAYERS" => GetLayersPayload(),
                "CREATE_CEILING_GRID" => CreateCeilingGrid(payload),
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
        return new { connected = true, version = Application.Version.ToString(), drawingName = doc?.Name ?? "", pluginVersion = "2.4.7" };
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

    private static void EnsureLayer(Transaction tr, Database db, string name)
    {
        var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (table.Has(name)) return;
        table.UpgradeOpen();
        var rec = new LayerTableRecord { Name = name };
        table.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
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
        var crossSpacing=Math.Max(100.0,(double?)payload["crossSpacing"]??400.0);
        var hangerSpacing=Math.Max(100.0,(double?)payload["hangerSpacing"]??900.0);
        var rotationDeg=(double?)payload["rotationDeg"]??0.0;
        var mode=((string?)payload["originMode"]??"CENTER").ToUpperInvariant();
        var offsetX=(double?)payload["offsetX"]??0.0;
        var offsetY=(double?)payload["offsetY"]??0.0;
        var drawHangers=(bool?)payload["drawHangers"]??true;
        var mainLayer=((string?)payload["mainLayer"]??"HNL_CEILING_MAIN").Trim();
        var crossLayer=((string?)payload["crossLayer"]??"HNL_CEILING_CROSS").Trim();
        var hangerLayer=((string?)payload["hangerLayer"]??"HNL_CEILING_HANGER").Trim();
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
            var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
            var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);

            foreach(var x in GridValues(minX,maxX,mainSpacing,mode,offsetX)) {
                var ys=VHits(local,x);
                for(var i=0;i+1<ys.Count;i+=2) {
                    if(ys[i+1]-ys[i]<1e-6) continue;
                    var a=ToWorld(new Point2d(x,ys[i]),origin,rad);
                    var b=ToWorld(new Point2d(x,ys[i+1]),origin,rad);
                    var ln=new Line(new Point3d(a.X,a.Y,0),new Point3d(b.X,b.Y,0)){Layer=mainLayer};
                    ms.AppendEntity(ln); tr.AddNewlyCreatedDBObject(ln,true); mainCount++;
                }
                if(drawHangers) {
                    foreach(var y in GridValues(minY,maxY,hangerSpacing,mode,offsetY)) {
                        var lp=new Point2d(x,y); if(!Inside(local,lp)) continue;
                        var wp=ToWorld(lp,origin,rad);
                        var c=new Circle(new Point3d(wp.X,wp.Y,0),Vector3d.ZAxis,20){Layer=hangerLayer};
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
                    ms.AppendEntity(ln); tr.AddNewlyCreatedDBObject(ln,true); crossCount++;
                }
            }
            tr.Commit();
        }
        return new {created=true,boundaryHandle=boundaryId.Handle.ToString(),mainSegments=mainCount,crossSegments=crossCount,hangers=hangerCount,mainSpacing,crossSpacing,hangerSpacing,rotationDeg,originMode=mode};
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

    [CommandMethod("HNLBRIDGESTATUS", CommandFlags.Session)]
    public void BridgeStatus()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage($"\nHNL Bridge v2.4.7: {(_registered ? "Paired" : "Waiting for HNL EXE")} | AutoCAD {Application.Version} | Drawing: {doc?.Name}");
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
