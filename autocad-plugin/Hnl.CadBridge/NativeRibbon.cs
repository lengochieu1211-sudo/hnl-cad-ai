using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Collections;
using System.Reflection;
using System.Windows.Input;

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
            if (ribbon == null)
            {
                TryInstallClassicMenu();
                return false;
            }

            var tabs = ribbon.GetType().GetProperty("Tabs")?.GetValue(ribbon);
            if (tabs == null) return false;

            foreach (var existing in (IEnumerable)tabs)
            {
                var id = Convert.ToString(existing?.GetType().GetProperty("Id")?.GetValue(existing));
                if (string.Equals(id, TabId, StringComparison.Ordinal))
                {
                    _installed = true;
                    return true;
                }
            }

            var tab = NewRibbon("RibbonTab");
            Set(tab, "Title", "HNL");
            Set(tab, "Id", TabId);

            AddPanel(tab, "AI", new[]
            {
                ("HNL AI", "HNLAI "),
                ("AI Settings", "HNLMANAGER "),
                ("Selection", "HNLSELECTION "),
            });

            AddPanel(tab, "Shopdrawing", new[]
            {
                ("Smart Ceiling", "HNLCEILING "),
                ("Smart Wall", "HNLWALL "),
                ("Library", "HNLINSERT "),
                ("Polyline", "_.PLINE "),
            });

            AddPanel(tab, "Data / BOQ", new[]
            {
                ("HNL BOQ", "HNLBOQ "),
                ("Data / Field", "HNLDATA "),
                ("Layers", "HNLLAYERS "),
                ("Properties", "_.PROPERTIES "),
            });

            AddPanel(tab, "Layout", new[]
            {
                ("HNL Layout", "HNLLAYOUT "),
                ("Rename", "HNLRENLAYOUT "),
                ("Plot", "_.PLOT "),
                ("Publish", "_.PUBLISH "),
            });

            AddPanel(tab, "Tools", new[]
            {
                ("Shop Audit", "HNLSHOPAUDIT "),
                ("HNL Tools", "HNLTOOLS "),
                ("Manager", "HNLMANAGER "),
                ("Bridge Status", "HNLBRIDGESTATUS "),
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

    private static object NewRibbon(string name)
    {
        var type = Type.GetType($"Autodesk.Windows.{name}, AdWindows")
            ?? throw new InvalidOperationException($"Autodesk.Windows.{name} unavailable.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Cannot create {name}.");
    }

    private static void AddPanel(object tab, string title, (string text, string command)[] buttons)
    {
        var source = NewRibbon("RibbonPanelSource");
        Set(source, "Title", title);

        var items = source.GetType().GetProperty("Items")?.GetValue(source)
            ?? throw new InvalidOperationException("RibbonPanelSource.Items unavailable.");
        foreach (var spec in buttons)
        {
            var button = NewRibbon("RibbonButton");
            Set(button, "Text", spec.text);
            Set(button, "ShowText", true);
            Set(button, "CommandParameter", spec.command);
            Set(button, "CommandHandler", new HnlRibbonCommandHandler());
            AddCollection(items, button);
        }

        var panel = NewRibbon("RibbonPanel");
        Set(panel, "Source", source);
        var panels = tab.GetType().GetProperty("Panels")?.GetValue(tab)
            ?? throw new InvalidOperationException("RibbonTab.Panels unavailable.");
        AddCollection(panels, panel);
    }

    private static void Set(object target, string property, object? value)
    {
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.SetValue(target, value);
    }

    private static void AddCollection(object collection, object item)
    {
        var add = collection.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
        add?.Invoke(collection, new[] { item });
    }

    // Fallback classic HNL menu. It becomes visible when AutoCAD MENUBAR=1.
    private static void TryInstallClassicMenu()
    {
        if (_classicMenuAttempted) return;
        _classicMenuAttempted = true;
        try
        {
            dynamic acad = Autodesk.AutoCAD.ApplicationServices.Application.AcadApplication;
            dynamic menuGroups = acad.MenuGroups;
            dynamic group = menuGroups.Item(0);
            dynamic menus = group.Menus;

            dynamic hnl = null;
            for (int i = 0; i < menus.Count; i++)
            {
                dynamic m = menus.Item(i);
                if (string.Equals(Convert.ToString(m.Name), "HNL", StringComparison.OrdinalIgnoreCase))
                {
                    hnl = m; break;
                }
            }
            if (hnl == null) hnl = menus.Add("HNL");

            if (hnl.Count == 0)
            {
                hnl.AddMenuItem(0, "HNL AI", "^C^CHNLAI ");
                hnl.AddMenuItem(1, "Smart Ceiling", "^C^CHNLCEILING ");
                hnl.AddMenuItem(2, "Smart Wall", "^C^CHNLWALL ");
                hnl.AddMenuItem(3, "HNL Library", "^C^CHNLINSERT ");
                hnl.AddMenuItem(4, "HNL BOQ", "^C^CHNLBOQ ");
                hnl.AddMenuItem(5, "Shopdrawing Audit", "^C^CHNLSHOPAUDIT ");
                hnl.AddMenuItem(6, "Layout / Publish", "^C^CHNLLAYOUT ");
                hnl.AddMenuItem(7, "HNL Manager", "^C^CHNLMANAGER ");
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
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute(cmd.EndsWith(" ") ? cmd : cmd + " ", true, false, true);
        }
        catch { }
    }
}
