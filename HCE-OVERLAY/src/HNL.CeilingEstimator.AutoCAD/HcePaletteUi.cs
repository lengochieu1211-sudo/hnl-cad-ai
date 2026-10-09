using System;
using DrawingSize = System.Drawing.Size;
using System.Globalization;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.CeilingEstimator.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
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
        // CI stamps this once, at build time in Vietnam local time (UTC+07).
        // Never use DateTime.Now here: opening the palette is not a software update.
        internal const string PaletteCaption = "HNL HCE Pro | HCE_UPDATED_AT_VN";
        private static PaletteSet? _palette;
        private static HcePaletteView? _view;

        public static void Show()
        {
            if (_palette == null)
            {
                // Lazy UI initialization: no WPF work on AutoCAD startup.
                var view = new HcePaletteView();
                var palette = new PaletteSet(PaletteCaption, PaletteId)
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
                // Apply the settings of the active DWG on every document switch.
                CadApplication.DocumentManager.DocumentActivated += (sender, args) =>
                {
                    var currentView = _view;
                    if (currentView == null) return;
                    try
                    {
                        currentView.Dispatcher.BeginInvoke(
                            new Action(() => currentView.RefreshLegacyControls()),
                            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                    catch (System.Exception)
                    {
                        // AutoCAD may be tearing down its dispatcher; presentation is fail-open.
                    }
                };
            }
            _view?.RefreshLegacyControls();
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
        private readonly System.Collections.Generic.Dictionary<string, ComboBox> _legacySelectors =
            new System.Collections.Generic.Dictionary<string, ComboBox>();
        private CheckBox? _legacySnap;
        private TextBox? _legacyTolerance;
        private ComboBox? _legacyTableStyle;
        private TextBox? _legacyTableHeight;
        private WpfText? _legacyStatus;
        private WpfText? _legacyValidation;
        private bool _loadingLegacyControls;

        public HcePaletteView()
        {
            var dark = true;
            try { dark = Convert.ToInt32(CadApplication.GetSystemVariable("COLORTHEME"), CultureInfo.InvariantCulture) == 0; }
            catch { /* preserve dark default if host has no theme variable */ }

            _background = Brush(dark ? "#1B1F23" : "#F3F6F8");
            _surface = Brush(dark ? "#252A30" : "#FFFFFF");
            _border = Brush(dark ? "#3B424A" : "#D7E0E7");
            _primary = Brush(dark ? "#F1F5F9" : "#172033");
            _muted = Brush(dark ? "#AEB8C4" : "#64748B");
            _accent = Brush(dark ? "#38BDF8" : "#0284C7");
            _accentSoft = Brush(dark ? "#102F3E" : "#EAF8FE");
            InstallVxtTheme(dark);

            Background = _background;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            MinWidth = 360;
            Content = BuildLayout();
            RefreshLegacyControls();
        }


        private void InstallVxtTheme(bool dark)
        {
            // Mirror the current HNL Ceiling Framing Pro palette resources.
            Resources["AppBackground"] = _background;
            Resources["CardBackground"] = _surface;
            Resources["CardBorder"] = _border;
            Resources["PrimaryText"] = _primary;
            Resources["SecondaryText"] = _muted;
            Resources["AccentStrong"] = _accent;
            Resources["AccentSoft"] = _accentSoft;
            Resources["AccentBorder"] = Brush(dark ? "#155E75" : "#BAE6FD");

            var inputBackground = Brush(dark ? "#1E2328" : "#FFFFFF");
            var inputBorder = Brush(dark ? "#4A535D" : "#C7D2DC");
            Resources["InputBackground"] = inputBackground;
            Resources["InputBorder"] = inputBorder;
            Resources["ComboArrowBackground"] = Brush(dark ? "#303840" : "#E7EEF5");
            Resources[SystemColors.WindowBrushKey] = inputBackground;
            Resources[SystemColors.WindowTextBrushKey] = _primary;
            Resources[SystemColors.ControlBrushKey] = inputBackground;
            Resources[SystemColors.ControlTextBrushKey] = _primary;
            Resources[SystemColors.HighlightBrushKey] = _accentSoft;
            Resources[SystemColors.HighlightTextBrushKey] = _primary;
            Resources[SystemColors.InactiveSelectionHighlightBrushKey] = _accentSoft;
            Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = _primary;

            var popupStyle = new Style(typeof(ComboBoxItem));
            popupStyle.Setters.Add(new Setter(Control.ForegroundProperty, _primary));
            popupStyle.Setters.Add(new Setter(Control.BackgroundProperty, inputBackground));
            popupStyle.Setters.Add(new Setter(Control.BorderBrushProperty, inputBorder));
            popupStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.ForegroundProperty, _primary));
            hover.Setters.Add(new Setter(Control.BackgroundProperty, _accentSoft));
            popupStyle.Triggers.Add(hover);
            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.ForegroundProperty, _primary));
            selected.Setters.Add(new Setter(Control.BackgroundProperty, _accentSoft));
            popupStyle.Triggers.Add(selected);
            Resources[typeof(ComboBoxItem)] = popupStyle;

            // AutoCAD can apply host/Windows chrome that leaves a white ComboBox
            // surface/arrow over light text. Own the non-editable ComboBox visuals.
            const string comboXaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                            xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                            TargetType='{x:Type ComboBox}'>
                <Setter Property='Foreground' Value='{DynamicResource PrimaryText}'/>
                <Setter Property='Background' Value='{DynamicResource InputBackground}'/>
                <Setter Property='BorderBrush' Value='{DynamicResource InputBorder}'/>
                <Setter Property='BorderThickness' Value='1'/>
                <Setter Property='Padding' Value='7,3,30,3'/>
                <Setter Property='MinHeight' Value='27'/>
                <Setter Property='Template'>
                    <Setter.Value>
                        <ControlTemplate TargetType='{x:Type ComboBox}'>
                            <Grid SnapsToDevicePixels='True'>
                                <Border x:Name='Chrome'
                                        Background='{TemplateBinding Background}'
                                        BorderBrush='{TemplateBinding BorderBrush}'
                                        BorderThickness='{TemplateBinding BorderThickness}'
                                        CornerRadius='2'/>
                                <TextBlock Margin='{TemplateBinding Padding}'
                                           VerticalAlignment='Center'
                                           Foreground='{TemplateBinding Foreground}'
                                           Text='{TemplateBinding SelectionBoxItem}'
                                           TextTrimming='CharacterEllipsis'/>
                                <Border Width='27' HorizontalAlignment='Right'
                                        Background='{DynamicResource ComboArrowBackground}'
                                        BorderBrush='{TemplateBinding BorderBrush}'
                                        BorderThickness='1,0,0,0'
                                        CornerRadius='0,2,2,0'>
                                    <Path Width='8' Height='5'
                                          HorizontalAlignment='Center' VerticalAlignment='Center'
                                          Fill='{DynamicResource PrimaryText}'
                                          Stretch='Fill'
                                          Data='M 0 0 L 4 4 L 8 0 Z'/>
                                </Border>
                                <ToggleButton Focusable='False' Opacity='0'
                                              Background='Transparent' BorderThickness='0'
                                              IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'/>
                                <Popup x:Name='PART_Popup'
                                       Placement='Bottom'
                                       AllowsTransparency='True'
                                       IsOpen='{TemplateBinding IsDropDownOpen}'
                                       PopupAnimation='Fade'>
                                    <Border Margin='0,2,0,0'
                                            Background='{DynamicResource InputBackground}'
                                            BorderBrush='{DynamicResource InputBorder}'
                                            BorderThickness='1'
                                            MinWidth='180'
                                            MaxHeight='320'>
                                        <ScrollViewer CanContentScroll='True'>
                                            <ItemsPresenter/>
                                        </ScrollViewer>
                                    </Border>
                                </Popup>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property='IsKeyboardFocusWithin' Value='True'>
                                    <Setter TargetName='Chrome' Property='BorderBrush'
                                            Value='{DynamicResource AccentStrong}'/>
                                </Trigger>
                                <Trigger Property='IsEnabled' Value='False'>
                                    <Setter TargetName='Chrome' Property='Opacity' Value='0.55'/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";
            Resources[typeof(ComboBox)] = (Style)XamlReader.Parse(comboXaml);
        }

        // Copy the proven 36px/30px/106px vertical tab rail from the Framing Pro.
        // The rail is cosmetic: AutoCAD commands, settings and Core are unchanged.
        private static Style VxtModuleRailStyle()
        {
            const string xaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                        TargetType='{x:Type TabControl}'>
                <Setter Property='BorderThickness' Value='0'/>
                <Setter Property='Background' Value='{DynamicResource AppBackground}'/>
                <Setter Property='Template'>
                    <Setter.Value>
                        <ControlTemplate TargetType='{x:Type TabControl}'>
                            <Grid Background='{TemplateBinding Background}'>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width='*'/>
                                    <ColumnDefinition Width='36'/>
                                </Grid.ColumnDefinitions>
                                <ContentPresenter Grid.Column='0' x:Name='PART_SelectedContentHost'
                                                  ContentSource='SelectedContent'
                                                  HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/>
                                <Border Grid.Column='1' Background='{DynamicResource CardBackground}'
                                        BorderBrush='{DynamicResource CardBorder}'
                                        BorderThickness='1,0,0,0' Padding='2,5,2,5'>
                                    <StackPanel Orientation='Vertical' IsItemsHost='True'
                                                HorizontalAlignment='Center' VerticalAlignment='Top'
                                                KeyboardNavigation.TabNavigation='Local'/>
                                </Border>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";
            return (Style)XamlReader.Parse(xaml);
        }

        private static Style VxtModuleTabStyle()
        {
            const string xaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                        TargetType='{x:Type TabItem}'>
                <Setter Property='Foreground' Value='{DynamicResource SecondaryText}'/>
                <Setter Property='Background' Value='Transparent'/>
                <Setter Property='BorderBrush' Value='Transparent'/>
                <Setter Property='FontFamily' Value='Segoe UI'/>
                <Setter Property='FontSize' Value='11'/>
                <Setter Property='FontWeight' Value='SemiBold'/>
                <Setter Property='Width' Value='30'/>
                <Setter Property='Height' Value='106'/>
                <Setter Property='Padding' Value='4,9'/>
                <Setter Property='Cursor' Value='Hand'/>
                <Setter Property='Template'>
                    <Setter.Value>
                        <ControlTemplate TargetType='{x:Type TabItem}'>
                            <Border x:Name='ModeSegment' Background='{TemplateBinding Background}'
                                    BorderBrush='{TemplateBinding BorderBrush}'
                                    BorderThickness='1' CornerRadius='5'
                                    Width='{TemplateBinding Width}' Height='{TemplateBinding Height}'
                                    Margin='0,0,0,5'>
                                <ContentPresenter ContentSource='Header'
                                                  HorizontalAlignment='Center' VerticalAlignment='Center'
                                                  Margin='{TemplateBinding Padding}' RecognizesAccessKey='True'>
                                    <ContentPresenter.LayoutTransform>
                                        <RotateTransform Angle='-90'/>
                                    </ContentPresenter.LayoutTransform>
                                </ContentPresenter>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property='IsMouseOver' Value='True'>
                                    <Setter TargetName='ModeSegment' Property='Background'
                                            Value='{DynamicResource AccentSoft}'/>
                                    <Setter Property='Foreground' Value='{DynamicResource PrimaryText}'/>
                                </Trigger>
                                <Trigger Property='IsSelected' Value='True'>
                                    <Setter TargetName='ModeSegment' Property='Background'
                                            Value='{DynamicResource AccentStrong}'/>
                                    <Setter TargetName='ModeSegment' Property='BorderBrush'
                                            Value='{DynamicResource AccentStrong}'/>
                                    <!-- Dark ink on cyan is legible; fixes white-on-light-blue contrast. -->
                                    <Setter Property='Foreground' Value='#082F49'/>
                                </Trigger>
                                <Trigger Property='IsEnabled' Value='False'>
                                    <Setter TargetName='ModeSegment' Property='Opacity' Value='0.45'/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";
            return (Style)XamlReader.Parse(xaml);
        }

        private UIElement BuildFramingModule()
        {
            // The Framing Pro is a separate AutoCAD plugin. Do not pretend
            // its trần chìm engine has been merged into HCE.
            var content = new StackPanel { Margin = new Thickness(3, 9, 3, 10) };
            var section = Section("Trần chìm", "#38BDF8");
            section.Children.Add(Text(
                "Module khung trần chìm thuộc HNL Ceiling Framing Pro (HCF). Chức năng tính tấm trong HCE không thay thế thuật toán xương trần.",
                11, _muted, FontWeights.Normal));
            section.Children.Add(CommandButton("Mở HNL Ceiling Framing Pro (HCF)", "HCF", true));
            section.Children.Add(Text("Cần cài HNL Ceiling Framing Pro. Đây là lệnh mở module gốc, không tạo khung từ HCE.",
                10, _muted, FontWeights.Normal));
            content.Children.Add(Card(section));
            return Scroll(content);
        }

        private UIElement BuildLayout()
        {
            // PaletteSet supplies the one and only app name and update time in
            // AutoCAD's native dock title bar. No inner logo, title or footer.
            // HCE is the ceiling-panel estimator. HCF is a separate plugin;
            // do not expose its launcher as a misleading HCE mode tab.
            return BuildCalculationTab();
        }

        private UIElement BuildCalculationTab()
        {
            var content = new StackPanel { Margin = new Thickness(0, 9, 0, 10) };

            var selection = Section("01  Ch\u1ecdn m\u1ea3ng tr\u1ea7n", "#2497FF");
            selection.Children.Add(Text("Ch\u1ecdn m\u1ed9t ho\u1eb7c nhi\u1ec1u Hatch tr\u1ea7n n\u1ed5i tr\u00ean CAD.",
                11, _muted, FontWeights.Normal));
            selection.Children.Add(CommandButton("Ch\u1ecdn Hatch v\u00e0 t\u00ednh t\u1ea5m", "HCECALC", true));
            selection.Children.Add(Text("B\u1ea5m n\u00fat, qu\u00e9t ch\u1ecdn Hatch trong CAD r\u1ed3i Enter.",
                10, _muted, FontWeights.Normal));
            content.Children.Add(Card(selection));

            content.Children.Add(Card(BuildLegacySettingsPanel()));

            var result = Section("03  Xem tr\u01b0\u1edbc v\u00e0 b\u1ea3ng", "#F59E0B");
            result.Children.Add(Text("Sau khi chọn Hatch, kết quả đếm tấm hiển thị trên Command Line. " +
                "Hatch không chứng minh được bước lưới hoặc đơn vị chưa đúng cần xác nhận riêng.",
                11, _muted, FontWeights.Normal));
            result.Children.Add(Text("Ch\u1ecdn Table \u0111\u1ec3 \u0111\u1eb7t b\u1ea3ng, " +
                "ho\u1eb7c Exit \u0111\u1ec3 kh\u00f4ng ghi g\u00ec v\u00e0o DWG.",
                11, _muted, FontWeights.Normal));
            result.Children.Add(Text("Preview h\u00ecnh h\u1ecdc t\u1eebng t\u1ea5m: ch\u01b0a m\u1edf " +
                "(c\u1ea7n Golden parity).",
                10, Brush("#E5A84E"), FontWeights.SemiBold));
            content.Children.Add(Card(result));
            var check = Section("05  Kiểm tra", "#A78BFA");
            check.Children.Add(CommandButton("Kiểm tra Hatch (HCEQA)", "HCEQA", false));
            check.Children.Add(CommandButton("Quét Golden (HCEGOLDEN)", "HCEGOLDEN", false));
            check.Children.Add(Text("Đây là công cụ kiểm tra của module Trần nổi, không phải một module thứ ba.",
                10, _muted, FontWeights.Normal));
            content.Children.Add(Card(check));

            return Scroll(content);
        }


        private StackPanel BuildLegacySettingsPanel()
        {
            var config = Section("02  Hệ tấm & module", "#22C55E");
            config.Children.Add(Choice("Family", "Hệ trần", new[] { "600 mm", "610 mm" },
                new[] { "600", "610" }));
            config.Children.Add(Choice("Module", "Loại tấm", new[] {
                "Tấm ngắn", "Tấm dài", "Kết hợp"
            }, new[] { "S", "D", "M" }));
            config.Children.Add(Choice("Priority", "Ưu tiên kết hợp", new[] {
                "Tấm ngắn làm chính", "Tấm dài làm chính"
            }, new[] { "S", "D" }));
            config.Children.Add(Choice("Direction", "Hướng tấm dài", new[] {
                "Theo X", "Theo Y"
            }, new[] { "X", "Y" }));

            var gridSection = Section("03  Căn lưới", "#2497FF");
            gridSection.Children.Add(Choice("Grid", "Chế độ lưới", new[] {
                "Theo Hatch (gốc/hướng)", "Chọn gốc + hướng", "WCS (0,0)"
            }, new[] { "3", "2", "1" }));
            var snapRow = new Grid { Margin = new Thickness(0, 3, 0, 8) };
            snapRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            snapRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _legacySnap = new CheckBox { Content = "Bật sai số nhỏ", Foreground = _primary };
            Grid.SetColumn(_legacySnap, 1);
            _legacySnap.Checked += (sender, args) => SaveLegacySnap(true);
            _legacySnap.Unchecked += (sender, args) => SaveLegacySnap(false);
            snapRow.Children.Add(Text("Sai số lưới", 11, _muted, FontWeights.Normal));
            snapRow.Children.Add(_legacySnap);
            gridSection.Children.Add(snapRow);
            var tol = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            tol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            tol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tol.Children.Add(Text("Dung sai (0-10 mm)", 11, _muted, FontWeights.Normal));
            _legacyTolerance = new TextBox { MinWidth = 85, MaxWidth = 130,
                HorizontalAlignment = HorizontalAlignment.Left, Foreground = _primary,
                Background = _surface, BorderBrush = _border, Padding = new Thickness(6, 3, 6, 3) };
            _legacyTolerance.LostFocus += (sender, args) => SaveLegacyTolerance();
            Grid.SetColumn(_legacyTolerance, 1);
            tol.Children.Add(_legacyTolerance);
            gridSection.Children.Add(tol);

            var wrapped = new StackPanel();
            wrapped.Children.Add(Card(config));
            wrapped.Children.Add(Card(gridSection));
            var table = Section("04  Định dạng bảng CAD", "#A78BFA");
            var styleRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            styleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            styleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            styleRow.Children.Add(Text("Kiểu chữ bảng", 11, _muted, FontWeights.Normal));
            _legacyTableStyle = new ComboBox
            {
                MinHeight = 27, Foreground = _primary
            };
            _legacyTableStyle.SelectionChanged += (sender, args) =>
            {
                if (_loadingLegacyControls || _legacyTableStyle.SelectedIndex < 0) return;
                var profile = ActiveLegacyProfile();
                if (profile == null) return;
                profile.TableTextStyle = _legacyTableStyle.SelectedIndex == 0 ? string.Empty :
                    _legacyTableStyle.SelectedItem?.ToString() ?? string.Empty;
            };
            Grid.SetColumn(_legacyTableStyle, 1);
            styleRow.Children.Add(_legacyTableStyle);
            table.Children.Add(styleRow);

            var heightRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            heightRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            heightRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heightRow.Children.Add(Text("Cao chữ bảng (mm)", 11, _muted, FontWeights.Normal));
            _legacyTableHeight = new TextBox
            {
                MinWidth = 85, MaxWidth = 130, HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = _primary, Background = _surface, BorderBrush = _border,
                Padding = new Thickness(6, 3, 6, 3)
            };
            _legacyTableHeight.LostFocus += (sender, args) => SaveLegacyTableHeight();
            Grid.SetColumn(_legacyTableHeight, 1);
            heightRow.Children.Add(_legacyTableHeight);
            table.Children.Add(heightRow);
            table.Children.Add(Text(
                "0 = giữ kích thước chữ RC5.3.3. Giá trị khác 0 chỉ thay bảng xuất CAD, không đổi phép tính.",
                10, _muted, FontWeights.Normal));
            wrapped.Children.Add(Card(table));
            var current = Section("05  Kết quả cấu hình", "#F59E0B");
            _legacyStatus = Text("", 11, _primary, FontWeights.SemiBold);
            _legacyValidation = Text("", 10, Brush("#F87171"), FontWeights.SemiBold);
            current.Children.Add(_legacyStatus);
            current.Children.Add(_legacyValidation);
            current.Children.Add(Text(
                "Mỗi bản vẽ giữ cấu hình riêng. Chế độ 600, tấm dài, G2/G1 và ưu tiên D cần đối chiếu Runtime với LISP trước khi chứng nhận Golden.",
                10, _muted, FontWeights.Normal));
            wrapped.Children.Add(Card(current));
            var root = Section("Cài đặt DEMTC gốc", "#22C55E");
            root.Children.Add(wrapped);
            return root;
        }

        private UIElement Choice(string key, string label, string[] names, string[] values)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(Text(label, 11, _muted, FontWeights.Normal));
            var selector = new ComboBox
            {
                MinHeight = 27, Foreground = _primary, Tag = values
            };
            foreach (var name in names) selector.Items.Add(name);
            selector.SelectionChanged += (sender, args) =>
            {
                if (_loadingLegacyControls || selector.SelectedIndex < 0) return;
                var profile = ActiveLegacyProfile();
                if (profile == null) return;
                var value = values[selector.SelectedIndex];
                switch (key)
                {
                    case "Family": profile.Family = value == "600" ? 600 : 610; break;
                    case "Module": profile.Module = value; break;
                    case "Priority": profile.Priority = value; break;
                    case "Direction": profile.Direction = value; break;
                    case "Grid": profile.GridMode = value; break;
                }
                RefreshLegacyControls();
            };
            _legacySelectors[key] = selector;
            Grid.SetColumn(selector, 1);
            row.Children.Add(selector);
            return row;
        }

        private static HceLegacyProfile? ActiveLegacyProfile()
        {
            var doc = CadApplication.DocumentManager.MdiActiveDocument;
            return doc == null ? null : HceLegacyProfiles.For(doc.Database);
        }

        private void SaveLegacySnap(bool enabled)
        {
            if (_loadingLegacyControls) return;
            var profile = ActiveLegacyProfile();
            if (profile == null) return;
            profile.SnapEnabled = enabled;
            RefreshLegacyControls();
        }

        private void SaveLegacyTolerance()
        {
            if (_loadingLegacyControls || _legacyTolerance == null) return;
            var profile = ActiveLegacyProfile();
            if (profile == null) return;
            if (double.TryParse(_legacyTolerance.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
                value >= 0 && value <= 10 && !double.IsInfinity(value) && !double.IsNaN(value))
            {
                profile.Tolerance = value;
                if (_legacyValidation != null) _legacyValidation.Text = "";
                RefreshLegacyControls();
            }
            else
            {
                if (_legacyValidation != null)
                    _legacyValidation.Text = "HNL Tool: Dung sai phải từ 0 đến 10 mm.";
            }
        }

        private void SaveLegacyTableHeight()
        {
            if (_loadingLegacyControls || _legacyTableHeight == null) return;
            var profile = ActiveLegacyProfile();
            if (profile == null) return;
            if (double.TryParse(_legacyTableHeight.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
                value >= 0 && value <= 10000 && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                profile.TableTextHeight = value;
                if (_legacyValidation != null) _legacyValidation.Text = string.Empty;
            }
            else if (_legacyValidation != null)
                _legacyValidation.Text = "HNL Tool: Cao chữ bảng phải từ 0 đến 10000 mm.";
        }

        private void RefreshTableStyles(HceLegacyProfile profile)
        {
            if (_legacyTableStyle == null) return;
            _legacyTableStyle.Items.Clear();
            _legacyTableStyle.Items.Add("(Theo bảng hiện tại)");
            var doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                try
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var styles = (TextStyleTable)tr.GetObject(doc.Database.TextStyleTableId, OpenMode.ForRead);
                        foreach (ObjectId id in styles)
                        {
                            var style = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                            if (style.Name != null) _legacyTableStyle.Items.Add(style.Name);
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Selection stays read-only: do not change a DWG because of GUI refresh.
                }
            }
            var selected = 0;
            for (var i = 1; i < _legacyTableStyle.Items.Count; i++)
                if (string.Equals(_legacyTableStyle.Items[i]?.ToString(), profile.TableTextStyle,
                    StringComparison.OrdinalIgnoreCase)) { selected = i; break; }
            // Preserve old choice in per-DWG memory even if its CAD style was deleted:
            // InsertTable will reject missing styles instead of silently picking another.
            if (selected == 0 && !string.IsNullOrEmpty(profile.TableTextStyle))
            {
                _legacyTableStyle.Items.Add(profile.TableTextStyle);
                selected = _legacyTableStyle.Items.Count - 1;
            }
            _legacyTableStyle.SelectedIndex = selected;
        }

        internal void RefreshLegacyControls()
        {
            var profile = ActiveLegacyProfile();
            if (profile == null) return;
            _loadingLegacyControls = true;
            try
            {
                SetChoice("Family", profile.Family.ToString(CultureInfo.InvariantCulture));
                SetChoice("Module", profile.Module);
                SetChoice("Priority", profile.Priority);
                SetChoice("Direction", profile.Direction);
                SetChoice("Grid", profile.GridMode);
                if (_legacySnap != null) _legacySnap.IsChecked = profile.SnapEnabled;
                if (_legacyTolerance != null)
                {
                    _legacyTolerance.Text = profile.Tolerance.ToString("0.###", CultureInfo.CurrentCulture);
                    _legacyTolerance.IsEnabled = profile.SnapEnabled;
                }
                RefreshTableStyles(profile);
                if (_legacyTableHeight != null)
                    _legacyTableHeight.Text = profile.TableTextHeight.ToString("0.###", CultureInfo.CurrentCulture);
                if (_legacySelectors.TryGetValue("Priority", out var p))
                    p.IsEnabled = profile.Module == "M";
                if (_legacySelectors.TryGetValue("Direction", out var d))
                    d.IsEnabled = profile.Module != "S";
                if (_legacyStatus != null) _legacyStatus.Text = profile.Summary();
                if (_legacyValidation != null) _legacyValidation.Text = "";
            }
            finally { _loadingLegacyControls = false; }
        }

        private void SetChoice(string key, string value)
        {
            if (!_legacySelectors.TryGetValue(key, out var combo)) return;
            var values = (string[])combo.Tag;
            var found = Array.IndexOf(values, value);
            if (found >= 0) combo.SelectedIndex = found;
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
                CanContentScroll = false
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

    internal static class HceLegacyProfiles
    {
        private static readonly ConditionalWeakTable<Database, HceLegacyProfile> Values =
            new ConditionalWeakTable<Database, HceLegacyProfile>();

        public static HceLegacyProfile For(Database database) => Values.GetValue(
            database, key => new HceLegacyProfile());
    }

}
