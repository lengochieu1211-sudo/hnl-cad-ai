using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using SysException = System.Exception;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;

namespace Hnl.CadBridge;

public sealed class NativePaletteCommands
{
    private static PaletteSet? _palette;
    private static TabControl? _tabs;
    private static bool _english;
    private static readonly HttpClient AiHttp = new HttpClient();
    private static TextBox? _aiPrompt;
    private static RichTextBox? _aiOutput;
    private static Label? _aiStatus;
    private static Button? _aiSend;

    public static void ShowPaletteWindow() => ShowPaletteTab(0);

    public static void ShowPaletteTab(int index)
    {
        EnsurePalette();
        _palette!.Visible = true;
        var safe = Math.Max(0, Math.Min(index, (_tabs?.TabPages.Count ?? 1) - 1));
        _palette.Activate(0);
        if (_tabs != null) _tabs.SelectedIndex = safe;
    }

    public static void OpenManagerWindow(string? tool = null) => OpenManager(tool);

    public static void HidePaletteWindow()
    {
        if (_palette != null) _palette.Visible = false;
    }

    public static bool IsPaletteVisible =>
        _palette != null && _palette.Visible;

    private static void EnsurePalette()
    {
        if (_palette != null) return;

        _palette = new PaletteSet("HNL CAD AI")
        {
            Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
            MinimumSize = new Size(300, 400),
            Size = new Size(340, 640),
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

        var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(22, 24, 27) };
        var title = new Label { Text = "HNL CAD AI • AutoCAD Native", AutoSize = true, Left = 10, Top = 8, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        var subtitle = new Label { Text = "HNL workflows on native AutoCAD DWG", AutoSize = true, Left = 10, Top = 29, ForeColor = Color.Silver, Font = new Font("Segoe UI", 7.5f) };
        var lang = new Button { Text = "VI | EN", Width = 58, Height = 24, Top = 8, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        lang.Left = 230;
        header.Resize += (_, __) => lang.Left = Math.Max(220, header.ClientSize.Width - lang.Width - 8);
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
        _tabs.TabPages.Add(BuildAiTab());
        _tabs.TabPages.Add(BuildShopTab());
        _tabs.TabPages.Add(BuildDataTab());
        _tabs.TabPages.Add(BuildToolsTab());
    }

    private static TabPage NewTab(string vi, string en)
    {
        var page = new TabPage(_english ? en : vi)
        {
            BackColor = Color.FromArgb(37, 39, 44),
            ForeColor = Color.Gainsboro,
            Padding = new Padding(7)
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

    private static string GlyphFor(string command)
    {
        var c = (command ?? "").ToUpperInvariant();
        if (c.Contains("AI")) return "✦";
        if (c.Contains("CEILING")) return "▦";
        if (c.Contains("WALL")) return "▥";
        if (c.Contains("LIBRARY") || c.Contains("INSERT")) return "▣";
        if (c.Contains("TEXT")) return "T";
        if (c.Contains("FIELD")) return "ƒ";
        if (c.Contains("GEOM")) return "⌁";
        if (c.Contains("DIM")) return "↔";
        if (c.Contains("QTY") || c.Contains("BOQ")) return "Σ";
        if (c.Contains("LAYOUT")) return "▤";
        if (c.Contains("LISP")) return "λ";
        if (c.Contains("LAYER")) return "◫";
        if (c.Contains("AUDIT")) return "✓";
        if (c.Contains("BRIDGE")) return "⇄";
        return "•";
    }

    private static Control Section(string title, params (string label, string command)[] items)
    {
        var group = new GroupBox
        {
            Text = title,
            Width = 292,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ForeColor = Color.Gainsboro,
            Padding = new Padding(8)
        };
        var flow = new FlowLayoutPanel
        {
            Width = 276,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        foreach (var item in items)
        {
            var button = new Button
            {
                Text = $"{GlyphFor(item.command)}  {item.label}",
                Width = 84,
                Height = 28,
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


    private static TabPage BuildAiTab()
    {
        var page = NewTab("AI", "AI");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(37, 39, 44)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = _english ? "HNL AI CAD — Describe what you want to do" : "HNL AI CAD — Nhập yêu cầu cần thực hiện",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _aiPrompt = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(28, 30, 34),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9)
        };
        _aiPrompt.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && e.Control)
            {
                e.SuppressKeyPress = true;
                await SubmitAiPromptAsync();
            }
        };
        panel.Controls.Add(_aiPrompt, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        _aiSend = new Button
        {
            Text = _english ? "Send AI  Ctrl+Enter" : "Gửi AI  Ctrl+Enter",
            Width = 150, Height = 30,
            BackColor = Color.FromArgb(0, 130, 160), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        _aiSend.Click += async (_, __) => await SubmitAiPromptAsync();

        var clear = new Button
        {
            Text = _english ? "Clear" : "Xóa", Width = 70, Height = 30,
            BackColor = Color.FromArgb(52, 55, 61), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        clear.Click += (_, __) => { _aiPrompt?.Clear(); _aiOutput?.Clear(); _aiPrompt?.Focus(); };

        var manager = new Button
        {
            Text = _english ? "Manager" : "Mở HNL", Width = 80, Height = 30,
            BackColor = Color.FromArgb(52, 55, 61), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        manager.Click += (_, __) => OpenManager();

        actions.Controls.Add(_aiSend);
        actions.Controls.Add(clear);
        actions.Controls.Add(manager);
        panel.Controls.Add(actions, 0, 2);

        _aiStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = _english ? "Ready • local HNL server / offline fallback" : "Sẵn sàng • HNL server cục bộ / fallback offline",
            ForeColor = Color.Silver, Font = new Font("Segoe UI", 8), TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(_aiStatus, 0, 3);

        _aiOutput = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            BackColor = Color.FromArgb(24, 26, 29), ForeColor = Color.Gainsboro,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 8.5f), DetectUrls = false
        };
        panel.Controls.Add(_aiOutput, 0, 4);

        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = _english
                ? "AI proposes a plan only. Review destructive changes before applying."
                : "AI chỉ đề xuất kế hoạch. Kiểm tra kỹ trước thao tác thay đổi/xóa.",
            ForeColor = Color.FromArgb(255, 190, 80), Font = new Font("Segoe UI", 8), TextAlign = ContentAlignment.MiddleLeft
        }, 0, 5);

        page.Controls.Add(panel);
        page.Enter += (_, __) => _aiPrompt?.Focus();
        return page;
    }

    private static async Task SubmitAiPromptAsync()
    {
        var prompt = _aiPrompt?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(prompt))
        {
            if (_aiStatus != null) _aiStatus.Text = _english ? "Enter a request first." : "Hãy nhập yêu cầu trước.";
            _aiPrompt?.Focus();
            return;
        }

        if (_aiSend != null) _aiSend.Enabled = false;
        if (_aiStatus != null) _aiStatus.Text = _english ? "AI is analyzing…" : "AI đang phân tích…";

        try
        {
            var pairingFile = Path.Combine(Path.GetTempPath(), "HNL_CAD_AI", "bridge.json");
            if (!File.Exists(pairingFile))
                throw new InvalidOperationException(_english
                    ? "HNL Manager/server is not running. Open HNL CAD AI first."
                    : "HNL Manager/server chưa chạy. Hãy mở HNL CAD AI trước.");

            var pairing = JObject.Parse(File.ReadAllText(pairingFile));
            var host = (string?)pairing["host"] ?? "127.0.0.1";
            var port = (int?)pairing["port"] ?? 32145;
            var token = (string?)pairing["token"] ?? "";

            var doc = AcApplication.DocumentManager.MdiActiveDocument;
            var selectionCount = 0;
            try
            {
                var implied = doc?.Editor.SelectImplied();
                if (implied != null && implied.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    selectionCount = implied.Value?.Count ?? 0;
            }
            catch { }

            var body = new
            {
                prompt,
                cadContext = new
                {
                    source = "AutoCAD Native Palette",
                    drawingName = doc?.Name ?? "",
                    selectionCount,
                    activeUnits = "drawing units",
                    autoCadNative = true
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"http://{host}:{port}/api/ai/plan");
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.TryAddWithoutValidation("x-hnl-token", token);
            req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            using var res = await AiHttp.SendAsync(req);
            var raw = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)res.StatusCode}: {raw}");

            var json = JObject.Parse(raw);
            var plan = json["plan"] as JObject ?? throw new InvalidOperationException("AI server không trả Command Plan.");
            var intent = (string?)plan["intent"] ?? prompt;
            var explanation = (string?)plan["explanation"] ?? "";
            var actionType = (string?)plan["actionType"] ?? "";
            var destructive = (bool?)plan["isDestructive"] ?? false;
            var offline = (bool?)json["isOfflineFallback"] ?? false;
            var provider = (string?)json["provider"] ?? (offline ? "OFFLINE" : "AI");
            var model = (string?)json["model"] ?? "";

            var sb = new StringBuilder();
            sb.AppendLine($"INTENT: {intent}");
            if (!string.IsNullOrWhiteSpace(actionType)) sb.AppendLine($"ACTION: {actionType}");
            sb.AppendLine($"MODE: {provider}{(string.IsNullOrWhiteSpace(model) ? "" : $" / {model}")}");
            sb.AppendLine($"RISK: {(destructive ? "DESTRUCTIVE — REVIEW REQUIRED" : "SAFE / REVIEW")}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(explanation)) sb.AppendLine(explanation);

            if (plan["steps"] is JArray steps)
            {
                sb.AppendLine();
                sb.AppendLine("STEPS:");
                foreach (var item in steps)
                {
                    if (item is not JObject step) continue;
                    sb.AppendLine($"{(int?)step["stepIndex"] ?? 0}. [{(string?)step["command"] ?? ""}] {(string?)step["description"] ?? ""}");
                }
            }

            if (_aiOutput != null) _aiOutput.Text = sb.ToString();
            if (_aiStatus != null)
                _aiStatus.Text = offline
                    ? (_english ? "Completed with offline rules." : "Đã phân tích bằng rule offline.")
                    : (_english ? "AI response received." : "Đã nhận phản hồi AI.");
        }
        catch (SysException ex)
        {
            if (_aiOutput != null) _aiOutput.Text = (_english ? "AI connection error:\r\n" : "Lỗi kết nối AI:\r\n") + ex.Message;
            if (_aiStatus != null) _aiStatus.Text = _english ? "AI unavailable — open HNL Manager / check server." : "AI chưa sẵn sàng — mở HNL Manager / kiểm tra server.";
        }
        finally
        {
            if (_aiSend != null) _aiSend.Enabled = true;
            _aiPrompt?.Focus();
        }
    }

    private static TabPage BuildShopTab()
    {
        var page = NewTab("SHOP", "SHOP");
        var f = Flow();
        f.Controls.Add(Section(_english ? "Smart Shopdrawing" : "Shopdrawing thông minh",
            (_english ? "Smart Ceiling" : "Smart Ceiling", "HNLCEILING"),
            (_english ? "Smart Wall" : "Smart Wall", "HNLWALL"),
            (_english ? "Library" : "Library", "HNLLIBRARY"),
            (_english ? "Shop Audit" : "Shop Audit", "HNLSHOPAUDIT")));
        f.Controls.Add(Section(_english ? "Quick Insert" : "Chèn nhanh",
            (_english ? "Insert Symbol" : "Chèn ký hiệu", "HNLINSERT")));
        page.Controls.Add(f);
        return page;
    }

    private static TabPage BuildDataTab()
    {
        var page = NewTab("DATA", "DATA");
        var f = Flow();
        f.Controls.Add(Section(_english ? "2D Pro / Data" : "2D Pro / Dữ liệu",
            (_english ? "Text / Attribute" : "Text / Attribute", "HNLTEXT"),
            (_english ? "Field Doctor" : "Field Doctor", "HNLFIELD"),
            (_english ? "Geometry Doctor" : "Geometry Doctor", "HNLGEOM"),
            (_english ? "Quick Dimension" : "Quick Dimension", "HNLDIM")));
        f.Controls.Add(Section(_english ? "BOQ / Layout" : "BOQ / Layout",
            (_english ? "Quantity / BOQ" : "Quantity / BOQ", "HNLQTY"),
            (_english ? "Layout+" : "Layout+", "HNLLAYOUTAUTO")));
        page.Controls.Add(f);
        return page;
    }

    private static TabPage BuildToolsTab()
    {
        var page = NewTab("TOOLS", "TOOLS");
        var f = Flow();
        f.Controls.Add(Section(_english ? "HNL Tools" : "Công cụ HNL",
            (_english ? "Lisp Center" : "Lisp Center", "HNLLISP"),
            (_english ? "Layer Standards" : "Chuẩn Layer", "HNLLAYERSYNC"),
            (_english ? "Manager" : "HNL Manager", "HNL_OPEN_MANAGER")));

        var advanced = new GroupBox
        {
            Text = _english ? "Advanced / Diagnostics" : "Nâng cao / Chẩn đoán",
            Width = 292,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ForeColor = Color.Silver,
            Padding = new Padding(7)
        };
        var toggle = new Button
        {
            Text = _english ? "Show advanced" : "Hiện nâng cao",
            Width = 120,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 47, 52),
            ForeColor = Color.Gainsboro
        };
        var body = new FlowLayoutPanel
        {
            Width = 276,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Visible = false
        };
        foreach (var item in new[]
        {
            (_english ? "Bridge Status" : "Bridge Status", "HNLBRIDGESTATUS"),
            (_english ? "Bridge Ping" : "Bridge Ping", "HNLBRIDGEPING"),
            (_english ? "Palette Status" : "Palette Status", "HNLPALETTESTATUS")
        })
        {
            var b = new Button
            {
                Text = $"{GlyphFor(item.Item2)}  {item.Item1}",
                Width = 84,
                Height = 28,
                Margin = new Padding(3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 55, 61),
                ForeColor = Color.White
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(75, 78, 84);
            var cmd = item.Item2;
            b.Click += (_, __) => Run(cmd);
            body.Controls.Add(b);
        }
        toggle.Click += (_, __) =>
        {
            body.Visible = !body.Visible;
            toggle.Text = body.Visible
                ? (_english ? "Hide advanced" : "Ẩn nâng cao")
                : (_english ? "Show advanced" : "Hiện nâng cao");
        };
        var wrap = new FlowLayoutPanel
        {
            Width = 276,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        wrap.Controls.Add(toggle);
        wrap.Controls.Add(body);
        advanced.Controls.Add(wrap);
        f.Controls.Add(advanced);

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
        var doc = AcApplication.DocumentManager.MdiActiveDocument;
        var cadCommand = command.StartsWith("HNL", StringComparison.OrdinalIgnoreCase) ? command : $"_.{command}";
        doc?.SendStringToExecute($"{cadCommand} ", true, false, true);
    }

    private static void OpenManager(string? tool = null)
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
                    var psi = new ProcessStartInfo(marked) { UseShellExecute = true };
                    if (!string.IsNullOrWhiteSpace(tool)) psi.Arguments = $"--hnl-tool={tool}";
                    Process.Start(psi);
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
                var psi = new ProcessStartInfo(file) { UseShellExecute = true };
                if (!string.IsNullOrWhiteSpace(tool)) psi.Arguments = $"--hnl-tool={tool}";
                Process.Start(psi);
                return;
            }
            AcApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nKhông tìm thấy HNL CAD AI EXE. Hãy mở HNL CAD AI từ Desktop/Start Menu.");
        }
        catch (SysException ex)
        {
            AcApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nKhông mở được HNL Manager: {ex.Message}");
        }
    }
}
