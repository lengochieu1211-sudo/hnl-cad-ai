using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hnl.CadBridge;

internal static class HnlNativeRibbon
{
    private const string TabId = "HNL_CAD_AI_NATIVE_TAB";
    private static bool _installed;
    private static bool _classicMenuAttempted;

    public static bool IsInstalled => _installed;

    public static bool TryInstall()
    {
        if (_installed) return true;
        try
        {
            var componentManager = Type.GetType("Autodesk.Windows.ComponentManager, AdWindows");
            var ribbon = componentManager?.GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (ribbon == null) { TryInstallClassicMenu(); return false; }
            var tabs = ribbon.GetType().GetProperty("Tabs")?.GetValue(ribbon);
            if (tabs == null) return false;

            foreach (var existing in (IEnumerable)tabs)
            {
                var id = Convert.ToString(existing?.GetType().GetProperty("Id")?.GetValue(existing));
                if (!string.Equals(id, TabId, StringComparison.Ordinal)) continue;
                _installed = true;
                return true;
            }

            var tab = NewRibbon("RibbonTab");
            Set(tab, "Title", "HNL");
            Set(tab, "Id", TabId);

            // HNL-only ribbon: do not duplicate LINE/PLINE/TRIM/LAYER/PROPERTIES/PLOT...
            // which are already available in native AutoCAD tabs.
            // Compact professional HNL-only ribbon.
            // Large = primary workflows. Standard = secondary utilities.
            AddPanel(tab, "AI", new[]
            {
                new ButtonSpec("AI Copilot", "HNLAI ", "AI", true),
            });

            AddPanel(tab, "Shopdrawing", new[]
            {
                new ButtonSpec("Ceiling", "HNLCEILING ", "CEILING", true),
                new ButtonSpec("Wall", "HNLWALL ", "WALL", true),
                new ButtonSpec("Library", "HNLLIBRARY ", "LIBRARY", true),
                new ButtonSpec("Audit", "HNLSHOPAUDIT ", "AUDIT", false),
            });

            AddPanel(tab, "2D Pro", new[]
            {
                new ButtonSpec("Text / Attr", "HNLTEXT ", "TEXT", false),
                new ButtonSpec("Field Doctor", "HNLFIELD ", "FIELD", false),
                new ButtonSpec("Geometry", "HNLGEOM ", "GEOMETRY", false),
                new ButtonSpec("Quick Dim", "HNLDIM ", "DIM", false),
            });

            AddPanel(tab, "Data / BOQ", new[]
            {
                new ButtonSpec("BOQ", "HNLQTY ", "BOQ", true),
                new ButtonSpec("Standards", "HNLLAYERSYNC ", "TOOLS", false),
            });

            AddPanel(tab, "Layout", new[]
            {
                new ButtonSpec("Layout+", "HNLLAYOUTAUTO ", "LAYOUT", true),
            });

            AddPanel(tab, "Tools", new[]
            {
                new ButtonSpec("Lisp Center", "HNLLISP ", "LISP", false),
                new ButtonSpec("Manager", "HNLMANAGER ", "MANAGER", false),
                new ButtonSpec("Bridge", "HNLBRIDGESTATUS ", "BRIDGE", false),
            });

            AddCollection(tabs, tab);
            Set(tab, "IsActive", true);
            _installed = true;
            return true;
        }
        catch
        {
            TryInstallClassicMenu();
            return false;
        }
    }

    public static bool Activate()
    {
        if (!TryInstall()) return false;
        try
        {
            var componentManager = Type.GetType("Autodesk.Windows.ComponentManager, AdWindows");
            var ribbon = componentManager?.GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var tabs = ribbon?.GetType().GetProperty("Tabs")?.GetValue(ribbon) as IEnumerable;
            if (tabs == null) return false;
            foreach (var tab in tabs)
            {
                var id = Convert.ToString(tab?.GetType().GetProperty("Id")?.GetValue(tab));
                if (!string.Equals(id, TabId, StringComparison.Ordinal)) continue;
                Set(tab!, "IsActive", true);
                return true;
            }
        }
        catch { }
        return false;
    }

    private sealed class ButtonSpec
    {
        public string Text { get; }
        public string Command { get; }
        public string Icon { get; }
        public bool Large { get; }
        public ButtonSpec(string text, string command, string icon, bool large = true)
        {
            Text=text; Command=command; Icon=icon; Large=large;
        }
    }

    private static object NewRibbon(string name)
    {
        var type = Type.GetType($"Autodesk.Windows.{name}, AdWindows")
            ?? throw new InvalidOperationException($"Autodesk.Windows.{name} unavailable.");
        return Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Cannot create {name}.");
    }

    private static object CreateRibbonButton(ButtonSpec spec)
    {
        var button = NewRibbon("RibbonButton");
        Set(button, "Text", spec.Text);
        Set(button, "ShowText", true);
        Set(button, "ShowImage", true);
        Set(button, "Image", CreateIcon(spec.Icon, 16));
        Set(button, "LargeImage", CreateIcon(spec.Icon, 32));
        SetEnum(button, "Size", spec.Large ? "Large" : "Standard");
        Set(button, "CommandParameter", spec.Command);
        Set(button, "CommandHandler", new HnlRibbonCommandHandler());
        return button;
    }

    private static void AddPanel(object tab, string title, ButtonSpec[] buttons)
    {
        var source = NewRibbon("RibbonPanelSource");
        Set(source, "Title", title);
        var items = source.GetType().GetProperty("Items")?.GetValue(source)
            ?? throw new InvalidOperationException("RibbonPanelSource.Items unavailable.");

        foreach (var spec in buttons)
        {
            AddCollection(items, CreateRibbonButton(spec));
        }

        var panel = NewRibbon("RibbonPanel");
        Set(panel, "Source", source);
        var panels = tab.GetType().GetProperty("Panels")?.GetValue(tab)
            ?? throw new InvalidOperationException("RibbonTab.Panels unavailable.");
        AddCollection(panels, panel);
    }

    private static ImageSource CreateIcon(string kind, int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var white = new SolidColorBrush(Color.FromRgb(230, 238, 246));
            var cyan = new SolidColorBrush(Color.FromRgb(36, 190, 225));
            var amber = new SolidColorBrush(Color.FromRgb(255, 174, 48));
            var green = new SolidColorBrush(Color.FromRgb(82, 210, 140));
            var pen = new Pen(white, Math.Max(1.2, size / 18.0));
            var cpen = new Pen(cyan, Math.Max(1.4, size / 16.0));
            double s=size, m=s*0.16, cx=s/2, cy=s/2;

            switch ((kind ?? "").ToUpperInvariant())
            {
                case "AI":
                    dc.DrawEllipse(null, cpen, new Point(cx, cy), s*.25, s*.25);
                    dc.DrawLine(pen, new Point(cx-s*.33,cy), new Point(cx-s*.18,cy));
                    dc.DrawLine(pen, new Point(cx+s*.18,cy), new Point(cx+s*.33,cy));
                    dc.DrawEllipse(cyan, null, new Point(cx+s*.28,cy-s*.28), s*.07,s*.07); break;
                case "CEILING":
                    dc.DrawRectangle(null, pen, new Rect(m,m,s-2*m,s-2*m));
                    dc.DrawLine(cpen,new Point(m,cy),new Point(s-m,cy));
                    dc.DrawLine(cpen,new Point(cx,m),new Point(cx,s-m));
                    dc.DrawLine(new Pen(green,1.5),new Point(cx,m*.45),new Point(cx,m)); break;
                case "WALL":
                    dc.DrawRectangle(null,cpen,new Rect(m,s*.25,s-2*m,s*.5));
                    dc.DrawLine(pen,new Point(s*.35,s*.25),new Point(s*.35,s*.75));
                    dc.DrawLine(pen,new Point(s*.65,s*.25),new Point(s*.65,s*.75)); break;
                case "LIBRARY":
                    dc.DrawRectangle(null,pen,new Rect(m,s*.22,s*.3,s*.56));
                    dc.DrawRectangle(null,cpen,new Rect(s*.55,s*.22,s*.3,s*.56));
                    dc.DrawLine(pen,new Point(cx,s*.2),new Point(cx,s*.8)); break;
                case "TEXT":
                    dc.DrawLine(cpen,new Point(s*.25,s*.25),new Point(s*.75,s*.25));
                    dc.DrawLine(cpen,new Point(cx,s*.25),new Point(cx,s*.75));
                    dc.DrawLine(pen,new Point(s*.32,s*.75),new Point(s*.68,s*.75)); break;
                case "FIELD":
                    dc.DrawRectangle(null,pen,new Rect(m,m,s-2*m,s-2*m));
                    for(int i=1;i<3;i++){double x=m+(s-2*m)*i/3;dc.DrawLine(cpen,new Point(x,m),new Point(x,s-m));}
                    for(int i=1;i<3;i++){double y=m+(s-2*m)*i/3;dc.DrawLine(cpen,new Point(m,y),new Point(s-m,y));} break;
                case "GEOMETRY":
                    dc.DrawEllipse(null,cpen,new Point(s*.37,s*.38),s*.2,s*.2);
                    dc.DrawLine(pen,new Point(s*.2,s*.76),new Point(s*.82,s*.58));
                    dc.DrawLine(pen,new Point(s*.58,s*.18),new Point(s*.8,s*.42)); break;
                case "DIM":
                    dc.DrawLine(cpen,new Point(s*.18,cy),new Point(s*.82,cy));
                    dc.DrawLine(pen,new Point(s*.18,s*.32),new Point(s*.18,s*.68));
                    dc.DrawLine(pen,new Point(s*.82,s*.32),new Point(s*.82,s*.68));
                    dc.DrawLine(pen,new Point(s*.18,cy),new Point(s*.28,cy-s*.08));
                    dc.DrawLine(pen,new Point(s*.82,cy),new Point(s*.72,cy-s*.08)); break;
                case "BOQ":
                    dc.DrawRectangle(null,pen,new Rect(s*.2,s*.14,s*.6,s*.72));
                    dc.DrawLine(cpen,new Point(s*.3,s*.36),new Point(s*.7,s*.36));
                    dc.DrawLine(cpen,new Point(s*.3,s*.52),new Point(s*.7,s*.52));
                    dc.DrawLine(cpen,new Point(s*.3,s*.68),new Point(s*.58,s*.68)); break;
                case "LAYOUT":
                    dc.DrawRectangle(null,pen,new Rect(s*.18,s*.16,s*.64,s*.68));
                    dc.DrawRectangle(null,cpen,new Rect(s*.3,s*.3,s*.4,s*.32));
                    dc.DrawLine(new Pen(amber,1.6),new Point(s*.3,s*.7),new Point(s*.68,s*.7)); break;
                case "AUDIT":
                    var shield=new StreamGeometry();using(var g=shield.Open()){g.BeginFigure(new Point(cx,s*.14),true,true);g.LineTo(new Point(s*.78,s*.28),true,false);g.LineTo(new Point(s*.7,s*.68),true,false);g.LineTo(new Point(cx,s*.86),true,false);g.LineTo(new Point(s*.3,s*.68),true,false);g.LineTo(new Point(s*.22,s*.28),true,false);} dc.DrawGeometry(null,cpen,shield);
                    dc.DrawLine(new Pen(green,2),new Point(s*.35,s*.5),new Point(s*.47,s*.62));dc.DrawLine(new Pen(green,2),new Point(s*.47,s*.62),new Point(s*.68,s*.38)); break;
                case "LISP":
                    dc.DrawLine(cpen,new Point(s*.35,s*.22),new Point(s*.2,cy));dc.DrawLine(cpen,new Point(s*.2,cy),new Point(s*.35,s*.78));
                    dc.DrawLine(cpen,new Point(s*.65,s*.22),new Point(s*.8,cy));dc.DrawLine(cpen,new Point(s*.8,cy),new Point(s*.65,s*.78));
                    dc.DrawLine(pen,new Point(s*.56,s*.18),new Point(s*.44,s*.82)); break;
                case "BRIDGE":
                    dc.DrawLine(cpen,new Point(s*.16,s*.7),new Point(s*.84,s*.7));
                    dc.DrawLine(pen,new Point(s*.24,s*.7),new Point(s*.24,s*.48));dc.DrawLine(pen,new Point(s*.76,s*.7),new Point(s*.76,s*.48));
                    var bridgeGeom=new StreamGeometry(); using(var bg=bridgeGeom.Open()){bg.BeginFigure(new Point(s*.24,s*.48),false,false);bg.BezierTo(new Point(s*.36,s*.2),new Point(s*.64,s*.2),new Point(s*.76,s*.48),true,false);} dc.DrawGeometry(null,pen,bridgeGeom); break;
                case "MANAGER":
                case "TOOLS":
                    dc.DrawEllipse(null,cpen,new Point(cx,cy),s*.28,s*.28);dc.DrawEllipse(null,pen,new Point(cx,cy),s*.1,s*.1);
                    for(int i=0;i<8;i++){double a=i*Math.PI/4;dc.DrawLine(pen,new Point(cx+Math.Cos(a)*s*.28,cy+Math.Sin(a)*s*.28),new Point(cx+Math.Cos(a)*s*.38,cy+Math.Sin(a)*s*.38));} break;
                default:
                    dc.DrawRectangle(null,cpen,new Rect(m,m,s-2*m,s-2*m)); break;
            }
        }
        var bmp = new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);
        bmp.Render(visual); bmp.Freeze(); return bmp;
    }

    private static void Set(object target, string property, object? value)
    {
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.SetValue(target, value);
    }

    private static void SetEnum(object target, string property, string value)
    {
        try
        {
            var p=target.GetType().GetProperty(property, BindingFlags.Public|BindingFlags.Instance);
            if(p?.PropertyType.IsEnum==true) p.SetValue(target, Enum.Parse(p.PropertyType,value,true));
        }
        catch { }
    }

    private static void AddCollection(object collection, object item)
    {
        foreach(var method in collection.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
        {
            if(method.Name!="Add") continue;
            var ps=method.GetParameters();
            if(ps.Length!=1) continue;
            if(!ps[0].ParameterType.IsAssignableFrom(item.GetType())) continue;
            method.Invoke(collection,new[]{item}); return;
        }
    }

    private static void TryInstallClassicMenu()
    {
        if (_classicMenuAttempted) return;
        _classicMenuAttempted = true;
        try
        {
            dynamic acad = Autodesk.AutoCAD.ApplicationServices.Application.AcadApplication;
            dynamic menus = acad.MenuGroups.Item(0).Menus;
            dynamic hnl = null;
            for (int i=0;i<menus.Count;i++) { dynamic m=menus.Item(i); if(string.Equals(Convert.ToString(m.Name),"HNL",StringComparison.OrdinalIgnoreCase)){hnl=m;break;} }
            if (hnl == null) hnl = menus.Add("HNL");
            if (hnl.Count == 0)
            {
                hnl.AddMenuItem(0, "AI Copilot", "^C^CHNLAI ");
                hnl.AddMenuItem(1, "Smart Ceiling", "^C^CHNLCEILING ");
                hnl.AddMenuItem(2, "Smart Wall", "^C^CHNLWALL ");
                hnl.AddMenuItem(3, "Library Manager", "^C^CHNLLIBRARY ");
                hnl.AddMenuItem(4, "Text / Attribute", "^C^CHNLTEXT ");
                hnl.AddMenuItem(5, "Field Doctor", "^C^CHNLFIELD ");
                hnl.AddMenuItem(6, "Geometry", "^C^CHNLGEOM ");
                hnl.AddMenuItem(7, "Quick Dimension", "^C^CHNLDIM ");
                hnl.AddMenuItem(8, "BOQ", "^C^CHNLQTY ");
                hnl.AddMenuItem(9, "Layout Automation", "^C^CHNLLAYOUTAUTO ");
                hnl.AddMenuItem(10, "Lisp Center", "^C^CHNLLISP ");
                hnl.AddMenuItem(11, "Shop Audit", "^C^CHNLSHOPAUDIT ");
                hnl.AddMenuItem(12, "Manager", "^C^CHNLMANAGER ");
            }
            try { hnl.InsertInMenuBar(acad.MenuBar.Count + 1); } catch { }
        }
        catch { }
    }
}

internal sealed class HnlRibbonCommandHandler : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        try
        {
            if (parameter == null) return;
            var cmd = Convert.ToString(parameter.GetType().GetProperty("CommandParameter")?.GetValue(parameter)) ?? "";
            if (string.IsNullOrWhiteSpace(cmd)) return;
            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.SendStringToExecute(cmd.EndsWith(" ") ? cmd : cmd + " ", true, false, true);
        }
        catch { }
    }
}
