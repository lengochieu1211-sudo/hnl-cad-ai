using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Hnl.CadBridge;

public sealed class NativePaletteCommands
{
    private static PaletteSet? _palette;
    private static TabControl? _tabs;
    private static bool _english;

    [CommandMethod("HNL")]
    [CommandMethod("HNLPALETTE")]
    public void ShowPalette()
    {
        EnsurePalette();
        _palette!.Visible = true;
    }

    [CommandMethod("HNLHIDE")]
    public void HidePalette()
    {
        if (_palette != null) _palette.Visible = false;
    }

    private static void EnsurePalette()
    {
        if (_palette != null) return;

        _palette = new PaletteSet("HNL CAD AI")
        {
            Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
            MinimumSize = new Size(300, 420),
            Size = new Size(380, 680),
            DockEnabled = DockSides.Left | DockSides.Right
        };

        var root = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(31, 33, 36),
            ForeColor = Color.Gainsboro,
            Padding = new Padding(6)
        };

        var header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Color.FromArgb(22, 24, 27) };
        var title = new Label { Text = "HNL CAD AI • AutoCAD Native", AutoSize = true, Left = 10, Top = 8, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        var subtitle = new Label { Text = "DWG / Selection / OSNAP / Command = AutoCAD", AutoSize = true, Left = 10, Top = 32, ForeColor = Color.Silver, Font = new Font("Segoe UI", 8) };
        var lang = new Button { Text = "VI | EN", Width = 58, Height = 24, Top = 8, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lang.Left = 300;
        lang.Click += (_, __) => { _english = !_english; RebuildTabs(); };
        header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(lang);

        _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9), Padding = new Point(8, 4) };
        root.Controls.Add(_tabs);
        root.Controls.Add(header);
        _palette.Add("HNL", root);
        RebuildTabs();
    }

    private static void RebuildTabs()
    {
        if (_tabs == null) return;
        _tabs.TabPages.Clear();
        _tabs.TabPages.Add(BuildHomeTab());
        _tabs.TabPages.Add(BuildDrawTab());
        _tabs.TabPages.Add(BuildDataTab());
        _tabs.TabPages.Add(BuildLayoutTab());
        _tabs.TabPages.Add(BuildToolsTab());
    }

    private static TabPage NewTab(string vi, string en)
    {
        var page = new TabPage(_english ? en : vi)
        {
            BackColor = Color.FromArgb(37, 39, 44),
            ForeColor = Color.Gainsboro,
            Padding = new Padding(8)
        };
        return page;
    }

    private static FlowLayoutPanel Flow()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4)
        };
    }

    private static Control Section(string title, params (string label, string command)[] items)
    {
        var group = new GroupBox
        {
            Text = title,
            Width = 320,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ForeColor = Color.Gainsboro,
            Padding = new Padding(8)
        };
        var flow = new FlowLayoutPanel
        {
            Width = 300,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        foreach (var item in items)
        {
            var button = new Button
            {
                Text = item.label,
                Width = 88,
                Height = 32,
                Margin = new Padding(3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 55, 61),
                ForeColor = Color.White
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(75, 78, 84);
            var cmd = item.command;
            button.Click += (_, __) => Run(cmd);
            flow.Controls.Add(button);
        }
        group.Controls.Add(flow);
        return group;
    }

    private static TabPage BuildHomeTab()
    {
        var page = NewTab("Trang chính", "Home");
        var f = Flow();
        f.Controls.Add(Section(_english ? "HNL" : "HNL",
            (_english ? "HNL Manager" : "Mở HNL", "HNL_OPEN_MANAGER"),
            (_english ? "Bridge" : "Kết nối", "HNLBRIDGESTATUS"),
            (_english ? "Palette" : "Palette", "HNLPALETTE")));
        f.Controls.Add(Section(_english ? "Native CAD" : "CAD Native",
            ("LINE", "LINE"), ("PLINE", "PLINE"), ("CIRCLE", "CIRCLE"),
            ("COPY", "COPY"), ("MOVE", "MOVE"), ("TRIM", "TRIM")));
        page.Controls.Add(f);
        return page;
    }

    private static TabPage BuildDrawTab()
    {
        var page = NewTab("2D / Vẽ", "2D / Draw");
        var f = Flow();
        f.Controls.Add(Section(_english ? "Draw" : "Vẽ",
            ("L", "LINE"), ("PL", "PLINE"), ("C", "CIRCLE"), ("REC", "RECTANG"),
            ("ARC", "ARC"), ("HATCH", "HATCH")));
        f.Controls.Add(Section(_english ? "Modify" : "Hiệu chỉnh",
            ("CO", "COPY"), ("M", "MOVE"), ("RO", "ROTATE"), ("SC", "SCALE"),
            ("TR", "TRIM"), ("EX", "EXTEND"), ("O", "OFFSET"), ("MI", "MIRROR"),
            ("F", "FILLET")));
        page.Controls.Add(f);
        return page;
    }

    private static TabPage BuildDataTab()
    {
        var page = NewTab("Dữ liệu", "Data");
        var f = Flow();
        f.Controls.Add(Section(_english ? "Text / Dimension" : "Text / Kích thước",
            ("MTEXT", "MTEXT"), ("DIM", "DIM"), ("DLI", "DIMLINEAR"), ("DI", "DIST")));
        f.Controls.Add(Section(_english ? "HNL Data" : "Dữ liệu HNL",
            (_english ? "Selection" : "Đối tượng chọn", "HNLSELECTION"),
            (_english ? "Layers" : "Layer", "HNLLAYERS")));
        page.Controls.Add(f);
        return page;
    }

    private static TabPage BuildLayoutTab()
    {
        var page = NewTab("Layout / In", "Layout / Publish");
        var f = Flow();
        f.Controls.Add(Section("Layout",
            ("LAYOUT", "LAYOUT"), ("MVIEW", "MVIEW"), ("PAGESETUP", "PAGESETUP"),
            ("PLOT", "PLOT"), ("PUBLISH", "PUBLISH")));
        page.Controls.Add(f);
        return page;
    }

    private static TabPage BuildToolsTab()
    {
        var page = NewTab("Công cụ", "Tools");
        var f = Flow();
        f.Controls.Add(Section(_english ? "Bridge / Diagnostics" : "Bridge / Chẩn đoán",
            (_english ? "Status" : "Trạng thái", "HNLBRIDGESTATUS"),
            (_english ? "Ping" : "Kiểm tra", "HNLBRIDGEPING"),
            (_english ? "Layouts" : "Danh sách Layout", "HNLLAYOUTS"),
            (_english ? "Plot Devices" : "Máy in", "HNLPLOTDEVICES")));
        page.Controls.Add(f);
        return page;
    }

    private static void Run(string command)
    {
        if (command == "HNL_OPEN_MANAGER")
        {
            OpenManager();
            return;
        }
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.SendStringToExecute($"_.{command} ", true, false, true);
    }

    private static void OpenManager()
    {
        try
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var marker = Path.Combine(roaming, "HNL CAD AI", "manager-path.txt");
            if (File.Exists(marker))
            {
                var marked = File.ReadAllText(marker).Trim();
                if (File.Exists(marked))
                {
                    Process.Start(new ProcessStartInfo(marked) { UseShellExecute = true });
                    return;
                }
            }

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new[]
            {
                Path.Combine(local, "Programs", "HNL CAD AI", "HNL CAD AI.exe"),
                Path.Combine(local, "HNL CAD AI", "HNL CAD AI.exe")
            };
            foreach (var file in candidates)
            {
                if (!File.Exists(file)) continue;
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                return;
            }
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nKhông tìm thấy HNL CAD AI EXE. Hãy mở HNL CAD AI từ Desktop/Start Menu.");
        }
        catch (Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nKhông mở được HNL Manager: {ex.Message}");
        }
    }
}
