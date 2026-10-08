using System;
using DrawingSize = System.Drawing.Size;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfBorder = System.Windows.Controls.Border;
using WpfText = System.Windows.Controls.TextBlock;

namespace HNL.CeilingEstimator.AutoCAD
{
    /// <summary>
    /// Dedicated HCE presentation surface derived from the established VXT
    /// compact Palette layout. No geometry or packing code lives here.
    /// Only existing public CAD commands execute calculation/QA.
    /// </summary>
    public sealed class HcePaletteCommands
    {
        [CommandMethod("HCEUI", CommandFlags.Modal)]
        public void Open()
        {
            try { HcePaletteService.Show(); }
            catch (System.Exception ex)
            {
                CadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\nHNL Tool - HCE palette could not open: " + ex.Message +
                    ". The original HCE/DTC/DEMTC commands are still available.");
            }
        }
    }

    internal static class HcePaletteService
    {
        private static readonly Guid PaletteId = new Guid("3214F7D5-9F11-4DB9-8369-6D40F07C2A8E");
        private static PaletteSet? _palette;
        private static HcePaletteView? _view;

        public static void Show()
        {
            if (_palette == null)
            {
                // Lazy UI initialization: no WPF work on AutoCAD startup.
                var view = new HcePaletteView();
                var palette = new PaletteSet("HNL Tool - Ceiling Estimator Pro", PaletteId)
                {
                    Style = PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowPropertiesMenu,
                    DockEnabled = DockSides.Left | DockSides.Right,
                    MinimumSize = new DrawingSize(360, 470),
                    Size = new DrawingSize(420, 730),
                    KeepFocus = false
                };
                palette.AddVisual("T\u00ednh t\u1ea5m tr\u1ea7n", view);
                _view = view;
                _palette = palette;
            }
            _palette.Visible = true;
        }
    }

    internal sealed class HcePaletteView : UserControl
    {
        private readonly SolidColorBrush _background;
        private readonly SolidColorBrush _surface;
        private readonly SolidColorBrush _border;
        private readonly SolidColorBrush _primary;
        private readonly SolidColorBrush _muted;
        private readonly SolidColorBrush _accent;
        private readonly SolidColorBrush _accentSoft;

        public HcePaletteView()
        {
            var dark = true;
            try { dark = Convert.ToInt32(CadApplication.GetSystemVariable("COLORTHEME"), CultureInfo.InvariantCulture) == 0; }
            catch { /* preserve dark default if host has no theme variable */ }

            _background = Brush(dark ? "#111827" : "#F3F6FB");
            _surface = Brush(dark ? "#1B2638" : "#FFFFFF");
            _border = Brush(dark ? "#324057" : "#DCE3EF");
            _primary = Brush(dark ? "#F3F6FF" : "#122440");
            _muted = Brush(dark ? "#ACBBD1" : "#52637D");
            _accent = Brush("#2497FF");
            _accentSoft = Brush(dark ? "#183754" : "#EAF4FF");

            Background = _background;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            MinWidth = 360;
            Content = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Background = _background };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new WpfBorder
            {
                Background = Brush("#142948"), Padding = new Thickness(14, 12, 14, 12),
                BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brush("#2761A7")
            };
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var logo = new System.Windows.Controls.Image
            {
                Width = 43, Height = 43, Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 12, 0)
            };
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(
                    "pack://application:,,,/HNL.CeilingEstimator.AutoCAD;component/Assets/HNL-Logo-Official.png",
                    UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                logo.Source = bitmap;
            }
            catch
            {
                // Logo load errors must never disable estimator commands.
            }

            topGrid.Children.Add(logo);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(Text("HNL Tool", 10.5, Brushes.White, FontWeights.SemiBold));
            titleStack.Children.Add(Text("Ceiling Estimator Pro", 17, Brushes.White, FontWeights.Bold));
            titleStack.Children.Add(Text("v0.3.0  \u2022  Palette RC5  \u2022  AutoCAD 2023\u20132027",
                10, Brush("#BED7F6"), FontWeights.Normal));
            Grid.SetColumn(titleStack, 1);
            topGrid.Children.Add(titleStack);
            top.Child = topGrid;
            root.Children.Add(top);

            var tabs = new TabControl
            {
                Margin = new Thickness(9, 10, 9, 6), Background = _background,
                BorderThickness = new Thickness(0), Foreground = _primary
            };
            tabs.Items.Add(new TabItem { Header = "T\u00ednh t\u1ea5m", Content = BuildCalculationTab() });
            tabs.Items.Add(new TabItem { Header = "Ki\u1ec3m tra", Content = BuildAuditTab() });
            tabs.SelectedIndex = 0;
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            var footer = new WpfBorder
            {
                Padding = new Thickness(14, 10, 14, 10),
                Background = _surface, BorderBrush = _border,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            footer.Child = Text("HNL Tool  |  Golden Core gi\u1eef nguy\u00ean  |  Runtime Candidate",
                10, _muted, FontWeights.Normal);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            return root;
        }

        private UIElement BuildCalculationTab()
        {
            var content = new StackPanel { Margin = new Thickness(0, 9, 0, 10) };

            var selection = Section("01  Ch\u1ecdn m\u1ea3ng tr\u1ea7n", "#2497FF");
            selection.Children.Add(Text("Ch\u1ecdn m\u1ed9t ho\u1eb7c nhi\u1ec1u Hatch tr\u1ea7n n\u1ed5i tr\u00ean CAD.",
                11, _muted, FontWeights.Normal));
            selection.Children.Add(CommandButton("Ch\u1ecdn Hatch v\u00e0 t\u00ednh t\u1ea5m", "HCE", true));
            selection.Children.Add(Text("B\u1ea5m n\u00fat, qu\u00e9t ch\u1ecdn Hatch trong CAD r\u1ed3i Enter.",
                10, _muted, FontWeights.Normal));
            content.Children.Add(Card(selection));

            var config = Section("02  H\u1ec7 t\u1ea5m v\u00e0 ki\u1ec3m tra", "#22C55E");
            config.Children.Add(Field("H\u1ec7 t\u1ea5m", "610 \u00d7 610 mm  (Golden c\u1ed1 \u0111\u1ecbnh)"));
            config.Children.Add(Field("Kh\u1ed5 t\u1ea5m d\u00e0i", "1220 \u00d7 610 mm"));
            config.Children.Add(Field("B\u1ea3n v\u1ebd", "mm  \u2022  Hatch \u0111o\u1ea1n th\u1eb3ng"));
            config.Children.Add(Text("C\u00e1c th\u00f4ng s\u1ed1 tr\u00ean \u0111ang \u0111\u01b0\u1ee3c kho\u00e1 theo Golden; " +
                "ch\u01b0a cho ch\u1ec9nh trong UI \u0111\u1ec3 tr\u00e1nh sai s\u1ed1 l\u01b0\u1ee3ng.",
                10, _muted, FontWeights.Normal));
            content.Children.Add(Card(config));

            var result = Section("03  Xem tr\u01b0\u1edbc v\u00e0 b\u1ea3ng", "#F59E0B");
            result.Children.Add(Text("Sau khi ch\u1ecdn Hatch v\u00e0 x\u00e1c nh\u1eadn Use610, " +
                "k\u1ebft qu\u1ea3 s\u1ed1 l\u01b0\u1ee3ng hi\u1ec3n th\u1ecb tr\u00ean Command Line.",
                11, _muted, FontWeights.Normal));
            result.Children.Add(Text("Ch\u1ecdn Table \u0111\u1ec3 \u0111\u1eb7t b\u1ea3ng, " +
                "ho\u1eb7c Exit \u0111\u1ec3 kh\u00f4ng ghi g\u00ec v\u00e0o DWG.",
                11, _muted, FontWeights.Normal));
            result.Children.Add(Text("Preview h\u00ecnh h\u1ecdc t\u1eebng t\u1ea5m: ch\u01b0a m\u1edf " +
                "(c\u1ea7n Golden parity).",
                10, Brush("#E5A84E"), FontWeights.SemiBold));
            content.Children.Add(Card(result));

            return Scroll(content);
        }

        private UIElement BuildAuditTab()
        {
            var content = new StackPanel { Margin = new Thickness(0, 9, 0, 10) };
            var audit = Section("Ki\u1ec3m tra Hatch", "#2497FF");
            audit.Children.Add(Text("Xem pattern, scale, v\u00f2ng bi\u00ean v\u00e0 l\u00fd do t\u1eeb ch\u1ed1i. " +
                "Kh\u00f4ng ghi th\u00eam \u0111\u1ed1i t\u01b0\u1ee3ng.",
                11, _muted, FontWeights.Normal));
            audit.Children.Add(CommandButton("Ki\u1ec3m tra Hatch \u0111\u00e3 ch\u1ecdn", "HCEQA", false));
            content.Children.Add(Card(audit));

            var golden = Section("Ki\u1ec3m tra Golden", "#22C55E");
            golden.Children.Add(Text("Qu\u00e9t Hatch c\u1ee7a kh\u00f4ng gian hi\u1ec7n h\u00e0nh v\u00e0 " +
                "in k\u1ebft qu\u1ea3 theo handle; kh\u00f4ng t\u1ea1o b\u1ea3ng.",
                11, _muted, FontWeights.Normal));
            golden.Children.Add(CommandButton("Qu\u00e9t Golden (read-only)", "HCEGOLDEN", false));
            golden.Children.Add(Text("Ki\u1ec3m th\u1eed tr\u00ean b\u1ea3n sao DWG/DXF tr\u01b0\u1edbc khi \u0111\u01b0a v\u00e0o thi c\u00f4ng.",
                10, _muted, FontWeights.Normal));
            content.Children.Add(Card(golden));
            var legacy = Section("L\u1ec7nh t\u01b0\u01a1ng th\u00edch", "#A78BFA");
            legacy.Children.Add(CommandButton("DTC", "DTC", false));
            legacy.Children.Add(CommandButton("DEMTC", "DEMTC", false));
            content.Children.Add(Card(legacy));

            return Scroll(content);
        }

        private ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer
            {
                Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = true
            };
        }

        private StackPanel Section(string title, string accent)
        {
            var panel = new StackPanel();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            row.Children.Add(new WpfBorder
            {
                Background = Brush(accent), Width = 4, Height = 19,
                CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 9, 0)
            });
            row.Children.Add(Text(title, 12.5, _primary, FontWeights.SemiBold));
            panel.Children.Add(row);
            return panel;
        }

        private WpfBorder Card(StackPanel content)
        {
            return new WpfBorder
            {
                Background = _surface, BorderBrush = _border, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(12),
                Margin = new Thickness(2, 0, 2, 10), Child = content
            };
        }

        private UIElement Field(string label, string value)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(Text(label, 11, _muted, FontWeights.Normal));
            var valueText = Text(value, 11, _primary, FontWeights.SemiBold);
            valueText.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);
            return row;
        }

        private WpfButton CommandButton(string title, string command, bool emphasized)
        {
            var button = new WpfButton
            {
                Content = title, MinHeight = emphasized ? 38 : 31,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = emphasized ? _accent : _accentSoft,
                Foreground = emphasized ? Brushes.White : _primary,
                BorderBrush = emphasized ? _accent : _border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 8, 0, 5),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            button.Click += (sender, args) =>
            {
                var document = CadApplication.DocumentManager.MdiActiveDocument;
                if (document == null) return;
                document.Editor.WriteMessage("\nHNL Tool - Palette executing " + command + ".");
                // Invoke from the normal AutoCAD command stack, never from a WPF
                // Click transaction. This preserves selection and Undo semantics.
                document.SendStringToExecute(command + "\n", true, false, false);
            };
            return button;
        }

        private static WpfText Text(string value, double size, Brush color, FontWeight weight)
        {
            return new WpfText
            {
                Text = value, FontSize = size, FontWeight = weight, Foreground = color,
                TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static SolidColorBrush Brush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }
}
