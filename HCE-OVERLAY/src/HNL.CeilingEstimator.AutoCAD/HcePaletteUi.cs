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
        [CommandMethod("HCESTOP", CommandFlags.Modal)]
        public void Stop() { HcePaletteService.StopInCommandContext(); }

        // Called only from the public HCE launcher; do not register a second
        // user-facing command for this same Palette.
        public void Open()
        {
            try { HcePaletteService.Show(); }
            catch (System.Exception ex)
            {
                CadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\nHNL Tool - HCE palette could not open: " + ex.Message +
                    ". Run HCECALC to calculate directly.");
            }
        }
    }

    internal static class HcePaletteService
    {
        private static bool _active;
        private static bool _calculationRunning;
        internal static bool IsCalculating => _calculationRunning;

        // Progress is presentation-only. No AutoCAD database mutations or engine
        // calls occur here. The native AutoCAD progress meter runs independently.
        internal static void BeginCalculation()
        {
            _calculationRunning = true;
            _view?.UpdateWorkProgress(0, "Chuẩn bị phân tích Hatch...", false);
        }

        internal static void ReportCalculation(int percent, string stage, bool indeterminate = false)
        {
            _view?.UpdateWorkProgress(percent, stage, indeterminate);
        }

        internal static void EndCalculation(bool complete)
        {
            _calculationRunning = false;
            _view?.UpdateWorkProgress(complete ? 100 : 0,
                complete ? "Hoàn tất. Đã tạo bảng CAD." : "Đã dừng hoặc hủy thao tác.", false);
        }

        internal static void StopInCommandContext()
        {
            var doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (_calculationRunning)
            {
                doc?.Editor.WriteMessage("\nHNL Tool - HCE đang tính; không thể đóng giữa phép tính.");
                return;
            }
            _active = false;
            if (_palette != null) _palette.Visible = false;
            // No longer refresh the hidden UI when another DWG activates.
            doc?.Editor.WriteMessage("\nHNL Tool - Đã tắt HCE; AutoCAD vẫn chạy. Gõ HCE để mở lại.");
        }

        private static readonly Guid PaletteId = new Guid("3214F7D5-9F11-4DB9-8369-6D40F07C2A8E");
        // CI stamps this once, at build time in Vietnam local time (UTC+07).
        // Never use DateTime.Now here: opening the palette is not a software update.
        // Update the native AutoCAD PaletteSet.Name, NOT an extra WPF line.
        // Keep the caption short enough for docked panels; preserve the GUID.
        internal const string PaletteCaption = "HNL Ceiling Estimator Pro • HCE_UPDATED_AT_VN";
        internal const string BuildInfo = "v0.3.0  ·  Cập nhật HCE_UPDATED_AT_VN";
        private static PaletteSet? _palette;
        private static HcePaletteView? _view;

        public static void Show()
        {
            _active = true;
            if (_palette == null)
            {
                // Lazy UI initialization: no WPF work on AutoCAD startup.
                var view = new HcePaletteView();
                var palette = new PaletteSet(PaletteCaption, PaletteId)
                {
                    // Constructor's first argument doubles as an AutoCAD
                    // restore command with a GUID. Name is the native window
                    // caption, so set it separately without changing the GUID.
                    Name = PaletteCaption,
                    Style = PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowPropertiesMenu,
                    DockEnabled = DockSides.Left | DockSides.Right,
                    MinimumSize = new DrawingSize(360, 470),
                    Size = new DrawingSize(420, 730),
                    KeepFocus = false
                };
                // Internal tab ID only. A single PaletteSet visual needs no displayed tab;
                // never enable ShowTabForSingle or add a duplicate visible header.
                palette.AddVisual("HCE", view);
                _view = view;
                _palette = palette;
                // Apply the settings of the active DWG on every document switch.
                CadApplication.DocumentManager.DocumentActivated += (sender, args) =>
                {
                    if (!_active || _palette == null || !_palette.Visible) return;
                    var currentView = _view;
                    if (currentView == null) return;
                    try
                    {
                        currentView.Dispatcher.BeginInvoke(
                            new Action(() => currentView.RefreshLegacyControls(onlyOnDocumentChange: true)),
                            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                    catch (System.Exception)
                    {
                        // AutoCAD may be tearing down its dispatcher; presentation is fail-open.
                    }
                };
            }
            _view?.RefreshLegacyControls();
            _view?.UpdateWorkProgress(0, "Sẵn sàng tính tấm.", false);
            // AutoCAD can restore an older caption from profile persistence.
            // Reapply on every show; do not reset dock/size/position settings.
            _palette.Name = PaletteCaption;
            _palette.Visible = true;
            // Emit binary build identity to detect a stale AutoCAD-loaded DLL.
            CadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nHNL Tool - " + PaletteCaption);
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
        private readonly SolidColorBrush _workSuccess = Brush("#22C55E");
        private readonly SolidColorBrush _workCanceled = Brush("#F59E0B");
        private readonly System.Collections.Generic.Dictionary<string, ComboBox> _legacySelectors =
            new System.Collections.Generic.Dictionary<string, ComboBox>();
        private Grid? _legacyPriorityRow;
        private CheckBox? _legacySnap;
        private TextBox? _legacyTolerance;
        private ComboBox? _legacyTableStyle;
        private ComboBox? _legacyCadTableStyle;
        private TextBox? _legacyTableHeight;
        private TextBox? _legacyLabelHeight;
        private CheckBox? _legacyCutList;
        private ComboBox? _legacyCutMode;
        private WpfText? _legacyStatus;
        private WpfText? _legacyValidation;
        private WpfText? _workStage;
        private WpfText? _workPercent;
        private WpfBorder? _workIndicator;
        private ProgressBar? _workProgress;
        private WpfButton? _stopHceButton;
        private bool _loadingLegacyControls;
        private Database? _lastLegacyDatabase;

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
            Resources["HoverBackground"] = _accentSoft;
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
                <Setter Property='MinHeight' Value='30'/>
                <Setter Property='HorizontalAlignment' Value='Stretch'/>
                <Setter Property='MaxDropDownHeight' Value='320'/>
                <Setter Property='Template'>
                    <Setter.Value>
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type ComboBox}'>
  <Grid SnapsToDevicePixels='True'>
    <ToggleButton x:Name='DropDownToggle'
                  Focusable='False'
                  Foreground='{DynamicResource PrimaryText}'
                  ClickMode='Press'
                  IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
      <ToggleButton.Template>
        <ControlTemplate TargetType='{x:Type ToggleButton}'>
          <Border x:Name='Chrome'
                  Background='{DynamicResource InputBackground}'
                  BorderBrush='{DynamicResource InputBorder}'
                  BorderThickness='1'
                  CornerRadius='4'>
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='28'/>
              </Grid.ColumnDefinitions>
              <ContentPresenter Grid.Column='0'
                                TextElement.Foreground='{DynamicResource PrimaryText}'
                                Margin='8,0,4,0'
                                HorizontalAlignment='Left'
                                VerticalAlignment='Center'
                                IsHitTestVisible='False'
                                Content='{Binding SelectionBoxItem, RelativeSource={RelativeSource AncestorType={x:Type ComboBox}}}'
                                ContentTemplate='{Binding SelectionBoxItemTemplate, RelativeSource={RelativeSource AncestorType={x:Type ComboBox}}}'
                                ContentTemplateSelector='{Binding ItemTemplateSelector, RelativeSource={RelativeSource AncestorType={x:Type ComboBox}}}'/>
              <Border Grid.Column='1' BorderBrush='{DynamicResource InputBorder}' BorderThickness='1,0,0,0'>
                <Path Width='8' Height='5' Stretch='Fill'
                      HorizontalAlignment='Center' VerticalAlignment='Center'
                      Fill='{DynamicResource PrimaryText}' Data='M 0 0 L 8 0 L 4 5 Z'/>
              </Border>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource AccentStrong}'/>
              <Setter TargetName='Chrome' Property='Background' Value='{DynamicResource HoverBackground}'/>
            </Trigger>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='Chrome' Property='BorderBrush' Value='{DynamicResource AccentStrong}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter TargetName='Chrome' Property='Background' Value='{DynamicResource InputBackground}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </ToggleButton.Template>
    </ToggleButton>

    <Popup x:Name='PART_Popup'
           Placement='Bottom'
           IsOpen='{TemplateBinding IsDropDownOpen}'
           AllowsTransparency='True'
           Focusable='False'
           PopupAnimation='Fade'>
      <Border Margin='0,2,0,0'
              MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'
              MaxHeight='{TemplateBinding MaxDropDownHeight}'
              Background='{DynamicResource InputBackground}'
              BorderBrush='{DynamicResource InputBorder}'
              BorderThickness='1'
              CornerRadius='4'>
        <ScrollViewer Margin='1' SnapsToDevicePixels='True' VerticalScrollBarVisibility='Auto'
                      ScrollViewer.CanContentScroll='True'
                      KeyboardNavigation.DirectionalNavigation='Contained'>
          <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/>
        </ScrollViewer>
      </Border>
    </Popup>
  </Grid>
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
            // Caption belongs exclusively to PaletteSet.Name above the UI.
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var body = BuildCalculationTab();
            Grid.SetRow(body, 0);
            root.Children.Add(body);

            // Compact data-driven progress; height remains fixed on every stage.
            var strip = new Grid();
            strip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            strip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = new Grid { MinHeight = 26 };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stageRow = new Grid { VerticalAlignment = VerticalAlignment.Center };
            stageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            stageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _workIndicator = new WpfBorder
            {
                Width = 7, Height = 7, CornerRadius = new CornerRadius(4),
                Background = _muted, Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            stageRow.Children.Add(_workIndicator);
            _workStage = Text("Sẵn sàng tính tấm.", 11, _primary, FontWeights.SemiBold);
            _workStage.TextWrapping = TextWrapping.NoWrap;
            _workStage.TextTrimming = TextTrimming.CharacterEllipsis;
            _workStage.VerticalAlignment = VerticalAlignment.Center;
            _workStage.ToolTip = "Sẵn sàng tính tấm.";
            Grid.SetColumn(_workStage, 1);
            stageRow.Children.Add(_workStage);
            top.Children.Add(stageRow);

            _workPercent = Text("—", 14, _accent, FontWeights.Bold);
            _workPercent.HorizontalAlignment = HorizontalAlignment.Center;
            _workPercent.VerticalAlignment = VerticalAlignment.Center;
            _workPercent.TextAlignment = System.Windows.TextAlignment.Center;
            _workPercent.MinWidth = 40;
            var percentBadge = new WpfBorder
            {
                Child = _workPercent,
                MinWidth = 58, MinHeight = 26,
                Background = _accentSoft, BorderBrush = _border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(7, 0, 0, 0),
                ToolTip = "Tiến độ theo công đoạn HCE. Khi Core đang bận, AutoCAD có thể chờ cập nhật."
            };
            Grid.SetColumn(percentBadge, 1);
            top.Children.Add(percentBadge);
            strip.Children.Add(top);

            _workProgress = new ProgressBar
            {
                Minimum = 0, Maximum = 100, Value = 0, Height = 6,
                IsIndeterminate = false,
                Foreground = _accent, Background = _border,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 6, 0, 0),
                ToolTip = "Phần trăm HCE theo từng giai đoạn: đọc Hatch, phân tích, ghép tấm, lập bảng."
            };
            Grid.SetRow(_workProgress, 1);
            strip.Children.Add(_workProgress);

            var progressFooter = new WpfBorder
            {
                Background = _surface, BorderBrush = _border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(10, 5, 10, 7), Child = strip
            };
            Grid.SetRow(progressFooter, 1);
            root.Children.Add(progressFooter);

            // Match HCF's independent right-hand 40px navigation rail.
            // STOP is not a calculation-mode tab and never cancels Core mid-run.
            var rightRail = new Grid
            {
                Width = 40, Background = _surface,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            rightRail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightRail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var hceTab = Text("HCE", 11, _primary, FontWeights.SemiBold);
            hceTab.LayoutTransform = new RotateTransform(-90);
            hceTab.HorizontalAlignment = HorizontalAlignment.Center;
            hceTab.VerticalAlignment = VerticalAlignment.Top;
            hceTab.Margin = new Thickness(0, 26, 0, 0);
            hceTab.ToolTip = "HNL Tool - Ceiling Estimator Pro";
            rightRail.Children.Add(hceTab);

            var stopContent = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stopContent.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M10,2 L10,10 M5,5 A8,8 0 1 0 15,5"),
                Width = 19, Height = 19, Stretch = Stretch.Uniform,
                Stroke = Brush("#DC2626"), StrokeThickness = 2.3
            });
            var stopLabel = Text("Tắt", 11, Brush("#B91C1C"), FontWeights.SemiBold);
            stopLabel.HorizontalAlignment = HorizontalAlignment.Center;
            stopLabel.Margin = new Thickness(0, 5, 0, 0);
            stopContent.Children.Add(stopLabel);
            _stopHceButton = new WpfButton
            {
                Content = stopContent, Width = 34, Height = 64,
                Padding = new Thickness(2, 3, 2, 3),
                Margin = new Thickness(2, 5, 2, 5),
                Background = Brush("#FFF1F2"), BorderBrush = Brush("#FECACA"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Tắt HCE; AutoCAD và bản vẽ vẫn mở."
            };
            // Native WPF Button corners are square even with a rounded rail:
            // use the HCF-style rounded border as the actual button template.
            const string stopTemplateXaml = @"<ControlTemplate
                    xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                    TargetType='{x:Type Button}'>
                <Border x:Name='StopSurface' CornerRadius='8'
                        Background='{TemplateBinding Background}'
                        BorderBrush='{TemplateBinding BorderBrush}'
                        BorderThickness='{TemplateBinding BorderThickness}'>
                    <ContentPresenter HorizontalAlignment='Center'
                                      VerticalAlignment='Center'
                                      ContentSource='Content'/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                        <Setter TargetName='StopSurface' Property='Background' Value='#FFE4E6'/>
                    </Trigger>
                    <Trigger Property='IsPressed' Value='True'>
                        <Setter TargetName='StopSurface' Property='Background' Value='#FECDD3'/>
                    </Trigger>
                    <Trigger Property='IsEnabled' Value='False'>
                        <Setter TargetName='StopSurface' Property='Opacity' Value='0.45'/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>";
            _stopHceButton.Template = (ControlTemplate)XamlReader.Parse(stopTemplateXaml);
            _stopHceButton.Click += (sender, args) => RequestStopHce();
            Grid.SetRow(_stopHceButton, 1);
            rightRail.Children.Add(_stopHceButton);
            Grid.SetColumn(rightRail, 1);
            Grid.SetRowSpan(rightRail, 2);
            root.Children.Add(rightRail);
            return root;
        }

        internal void UpdateWorkProgress(int percent, string stage, bool indeterminate)
        {
            // AutoCAD remains in charge of the real modal progress meter.
            // This Palette mirrors supplied stage percentages; never fake
            // a smooth percentage with timers or re-enter CAD transactions.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(
                    () => UpdateWorkProgress(percent, stage, indeterminate)));
                return;
            }
            if (_workProgress == null || _workStage == null || _workPercent == null)
                return;

            var stageText = string.IsNullOrWhiteSpace(stage) ? "Đang xử lý..." : stage;
            var value = Math.Max(0, Math.Min(100, percent));
            var idle = !indeterminate && value == 0 && stageText == "Sẵn sàng tính tấm.";
            var canceled = !indeterminate && value == 0 &&
                stageText == "Đã dừng hoặc hủy thao tác.";
            var completed = !indeterminate && value == 100 &&
                !HcePaletteService.IsCalculating;

            _workStage.Text = stageText;
            _workStage.ToolTip = stageText;
            _workProgress.IsIndeterminate = indeterminate;
            if (!indeterminate)
                _workProgress.Value = idle || canceled ? 0 : value;
            _workPercent.Text = idle || canceled ? "—" :
                (indeterminate ? "···" : value.ToString(CultureInfo.InvariantCulture) + "%");
            if (_workIndicator != null)
                _workIndicator.Background = completed ? _workSuccess :
                    canceled ? _workCanceled : idle ? _muted : _accent;
            if (_stopHceButton != null)
                _stopHceButton.IsEnabled = !HcePaletteService.IsCalculating;
        }

        private void RequestStopHce()
        {
            if (HcePaletteService.IsCalculating) return;
            var confirmation = System.Windows.MessageBox.Show(
                "Tắt HCE? AutoCAD và bảng đã tạo vẫn được giữ nguyên.",
                "HNL Tool - Tắt HCE",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No);
            if (confirmation != System.Windows.MessageBoxResult.Yes) return;
            var document = CadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                HcePaletteService.StopInCommandContext();
            else
                document.SendStringToExecute("HCESTOP\n", true, false, false);
        }

        private UIElement BuildCalculationTab()
        {
            var content = new StackPanel { Margin = new Thickness(0, 9, 0, 10) };

            var selection = Section("01  Chọn Hatch", "#2497FF");
            selection.Children.Add(CommandButton("Chọn Hatch và tạo bảng", "HCECALC", true));
            selection.Children.Add(Text("Chọn Hatch trên bản vẽ, nhấn Enter; HCE tính và cho chọn vị trí đặt bảng.",
                10, _muted, FontWeights.Normal));
            content.Children.Add(Card(selection));

            content.Children.Add(Card(BuildLegacySettingsPanel()));

            var result = Section("03  Kết quả", "#F59E0B");
            result.Children.Add(Text("Kết quả hiển thị trên dòng lệnh. Hatch chưa rõ lưới hoặc đơn vị cần xác nhận.",
                11, _muted, FontWeights.Normal));
            result.Children.Add(Text("Chọn Hatch và tạo bảng ở phía trên; nhấn Esc để hủy đặt bảng.",
                11, _muted, FontWeights.Normal));
            result.Children.Add(Text("Chưa hỗ trợ xem trước hình học từng tấm.",
                10, Brush("#E5A84E"), FontWeights.SemiBold));
            content.Children.Add(Card(result));
            var check = Section("04  Kiểm tra", "#A78BFA");
            check.Children.Add(CommandButton("Kiểm tra Hatch (HCEQA)", "HCEQA", false));
            check.Children.Add(CommandButton("Đối chiếu Golden (HCEGOLDEN)", "HCEGOLDEN", false));
            check.Children.Add(Text("Chỉ kiểm tra, không sửa DWG.", 10, _muted, FontWeights.Normal));
            content.Children.Add(Card(check));

            return Scroll(content);
        }


        private StackPanel BuildLegacySettingsPanel()
        {
            var config = Section("Hệ tấm", "#22C55E");
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

            var gridSection = Section("Căn lưới", "#2497FF");
            gridSection.Children.Add(Choice("Grid", "Chế độ lưới", new[] {
                "Theo Hatch (gốc/hướng)", "Chọn gốc + hướng", "WCS (0,0)"
            }, new[] { "3", "2", "1" }));
            var snapRow = new Grid { Margin = new Thickness(0, 3, 0, 8) };
            snapRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            snapRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _legacySnap = new CheckBox { Content = "Bật", Foreground = _primary, MinHeight = 30, VerticalContentAlignment = VerticalAlignment.Center };
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
            _legacyTolerance = new TextBox { MinHeight = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = _primary, Background = (Brush)Resources["InputBackground"],
                BorderBrush = (Brush)Resources["InputBorder"],
                Padding = new Thickness(6, 3, 6, 3) };
            _legacyTolerance.LostFocus += (sender, args) => SaveLegacyTolerance();
            Grid.SetColumn(_legacyTolerance, 1);
            tol.Children.Add(_legacyTolerance);
            gridSection.Children.Add(tol);

            var wrapped = new StackPanel();
            wrapped.Children.Add(Card(config));
            wrapped.Children.Add(Card(gridSection));
            var table = Section("Bảng CAD", "#A78BFA");
            var cadStyleRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            cadStyleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            cadStyleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cadStyleRow.Children.Add(Text("Kiểu bảng CAD", 11, _muted, FontWeights.Normal));
            _legacyCadTableStyle = new ComboBox { MinHeight = 30, Foreground = _primary };
            _legacyCadTableStyle.SelectionChanged += (sender, args) =>
            {
                if (_loadingLegacyControls || _legacyCadTableStyle.SelectedIndex < 0) return;
                var profile = ActiveLegacyProfile();
                if (profile == null) return;
                profile.CadTableStyle = _legacyCadTableStyle.SelectedIndex == 0 ? string.Empty :
                    _legacyCadTableStyle.SelectedItem?.ToString() ?? string.Empty;
                RefreshSelectionState(profile);
            };
            Grid.SetColumn(_legacyCadTableStyle, 1);
            cadStyleRow.Children.Add(_legacyCadTableStyle);
            table.Children.Add(cadStyleRow);
            var styleRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            styleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            styleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            styleRow.Children.Add(Text("Kiểu chữ CAD", 11, _muted, FontWeights.Normal));
            _legacyTableStyle = new ComboBox
            {
                MinHeight = 30, Foreground = _primary
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
            heightRow.Children.Add(Text("Cao chữ (mm)", 11, _muted, FontWeights.Normal));
            _legacyTableHeight = new TextBox
            {
                MinHeight = 30, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = _primary, Background = (Brush)Resources["InputBackground"],
                BorderBrush = (Brush)Resources["InputBorder"],
                Padding = new Thickness(6, 3, 6, 3)
            };
            _legacyTableHeight.LostFocus += (sender, args) => SaveLegacyTableHeight();
            Grid.SetColumn(_legacyTableHeight, 1);
            heightRow.Children.Add(_legacyTableHeight);
            table.Children.Add(heightRow);
            var labelHeightRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            labelHeightRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            labelHeightRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            labelHeightRow.Children.Add(Text("Cao mã ô (mm)", 11, _muted, FontWeights.Normal));
            _legacyLabelHeight = new TextBox
            {
                MinHeight = 30, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = _primary, Background = (Brush)Resources["InputBackground"],
                BorderBrush = (Brush)Resources["InputBorder"],
                Padding = new Thickness(6, 3, 6, 3)
            };
            _legacyLabelHeight.LostFocus += (sender, args) => SaveLegacyLabelHeight();
            Grid.SetColumn(_legacyLabelHeight, 1);
            labelHeightRow.Children.Add(_legacyLabelHeight);
            table.Children.Add(labelHeightRow);
            var cutListRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            cutListRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            cutListRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cutListLabel = Text("Liệt kê mảnh biên", 11, _muted, FontWeights.Normal);
            cutListLabel.Cursor = System.Windows.Input.Cursors.Hand;
            cutListLabel.MouseLeftButtonUp += (sender, args) =>
            {
                if (_legacyCutList != null)
                    _legacyCutList.IsChecked = _legacyCutList.IsChecked != true;
            };
            cutListRow.Children.Add(cutListLabel);
            _legacyCutList = new CheckBox
            {
                Content = "Hiển thị", Foreground = _primary, MinHeight = 30,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _legacyCutList.Checked += (sender, args) => SaveLegacyCutList(true);
            _legacyCutList.Unchecked += (sender, args) => SaveLegacyCutList(false);
            Grid.SetColumn(_legacyCutList, 1);
            cutListRow.Children.Add(_legacyCutList);
            table.Children.Add(cutListRow);
            var cutModeRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            cutModeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            cutModeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cutModeLabel = Text("Chế độ liệt kê", 11, _muted, FontWeights.Normal);
            cutModeLabel.Cursor = System.Windows.Input.Cursors.Hand;
            cutModeLabel.MouseLeftButtonUp += (sender, args) =>
            {
                if (_legacyCutMode != null)
                {
                    _legacyCutMode.Focus();
                    _legacyCutMode.IsDropDownOpen = true;
                }
            };
            cutModeRow.Children.Add(cutModeLabel);
            _legacyCutMode = new ComboBox { MinHeight = 30, Foreground = _primary };
            _legacyCutMode.Items.Add("Tổng hợp");
            _legacyCutMode.Items.Add("Chi tiết (chậm)");
            _legacyCutMode.SelectionChanged += (sender, args) =>
            {
                if (_loadingLegacyControls || _legacyCutMode.SelectedIndex < 0) return;
                var profile = ActiveLegacyProfile();
                if (profile == null) return;
                profile.CutListMode = _legacyCutMode.SelectedIndex == 1 ? "D" : "S";
                // Choosing Summary/Detail should also turn on the cut list.
                if (!profile.CutListEnabled)
                    profile.CutListEnabled = true;
                RefreshSelectionState(profile);
            };
            Grid.SetColumn(_legacyCutMode, 1);
            cutModeRow.Children.Add(_legacyCutMode);
            table.Children.Add(cutModeRow);
            table.Children.Add(Text(
                "Cỡ chữ 0 = theo Table Style. Chiều rộng cột và chiều cao hàng tự khớp MTEXT; chỉ áp dụng cho bảng mới.",
                10, _muted, FontWeights.Normal));
            wrapped.Children.Add(Card(table));
            var current = Section("Cấu hình hiện tại", "#F59E0B");
            _legacyStatus = Text("", 11, _primary, FontWeights.SemiBold);
            _legacyValidation = Text("", 10, Brush("#F87171"), FontWeights.SemiBold);
            current.Children.Add(_legacyStatus);
            current.Children.Add(_legacyValidation);
            current.Children.Add(Text(
                "Mỗi DWG lưu cấu hình riêng. Một số chế độ chưa xác nhận Golden trên AutoCAD.",
                10, _muted, FontWeights.Normal));
            wrapped.Children.Add(Card(current));
            var root = Section("02  Cài đặt tính tấm", "#22C55E");
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
                MinHeight = 30, Foreground = _primary, Tag = values
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
                // Keep the user's open ComboBox and focused row intact:
                // reloading every CAD TableStyle during SelectionChanged closes
                // the WPF popup and can discard its newly selected item.
                RefreshSelectionState(profile);
            };
            _legacySelectors[key] = selector;
            if (key == "Priority")
            {
                // This choice applies only to mixed stock. Keep its per-DWG
                // value for the next mixed selection but never show it for
                // short-only or long-only modes.
                _legacyPriorityRow = row;
                row.Visibility = System.Windows.Visibility.Collapsed;
            }
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
            RefreshSelectionState(profile);
        }

        // Accept either comma or dot decimal entry in AutoCAD's Vietnamese/
        // international Windows locale. This does not change numeric limits,
        // quantity calculations or the user's saved units (always millimeters).
        private static bool TryParseUiMm(string text, out double value)
        {
            return double.TryParse(text.Trim().Replace(',', '.'),
                       NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   !double.IsInfinity(value) && !double.IsNaN(value);
        }

        // Read the ACTUAL WPF dropdown selection at click-time. The prior
        // implementation committed only numeric TextBoxes: an AutoCAD activation
        // could reload an older per-DWG mode before the queued HCECALC began.
        private bool TryReadLegacyChoice(string key, out string value)
        {
            value = string.Empty;
            if (!_legacySelectors.TryGetValue(key, out var selector) ||
                !(selector.Tag is string[] choices) ||
                selector.SelectedIndex < 0 || selector.SelectedIndex >= choices.Length)
                return false;
            value = choices[selector.SelectedIndex];
            return true;
        }

        private bool CommitPendingCalculationSettings()
        {
            var profile = ActiveLegacyProfile();
            if (profile == null || _loadingLegacyControls ||
                _legacyTableHeight == null || _legacyLabelHeight == null)
                return false;
            if (!TryReadLegacyChoice("Family", out var familyChoice) ||
                !TryReadLegacyChoice("Module", out var moduleChoice) ||
                !TryReadLegacyChoice("Priority", out var priorityChoice) ||
                !TryReadLegacyChoice("Direction", out var directionChoice) ||
                !TryReadLegacyChoice("Grid", out var gridChoice) ||
                !int.TryParse(familyChoice, NumberStyles.Integer, CultureInfo.InvariantCulture, out var family) ||
                (moduleChoice != "S" && moduleChoice != "D" && moduleChoice != "M"))
            {
                if (_legacyValidation != null)
                    _legacyValidation.Text = "HNL Tool: Chọn đầy đủ Hệ trần / Loại tấm / Chế độ lưới trước khi tính.";
                return false;
            }
            if (!TryParseUiMm(_legacyTableHeight.Text, out var tableHeight) ||
                tableHeight < 0 || tableHeight > 10000)
            {
                if (_legacyValidation != null)
                    _legacyValidation.Text = "HNL Tool: Cao chữ bảng phải từ 0 đến 10000 mm.";
                _legacyTableHeight.Focus();
                return false;
            }
            if (!TryParseUiMm(_legacyLabelHeight.Text, out var labelHeight) ||
                labelHeight <= 0 || labelHeight > 10000)
            {
                if (_legacyValidation != null)
                    _legacyValidation.Text = "HNL Tool: Cao mã ô phải lớn hơn 0 và không quá 10000 mm.";
                _legacyLabelHeight.Focus();
                return false;
            }
            var candidate = profile.Clone();
            candidate.Family = family;
            candidate.Module = moduleChoice;
            candidate.Priority = priorityChoice;
            candidate.Direction = directionChoice;
            candidate.GridMode = gridChoice;
            candidate.SnapEnabled = _legacySnap?.IsChecked == true;
            if (candidate.SnapEnabled)
            {
                if (_legacyTolerance == null ||
                    !TryParseUiMm(_legacyTolerance.Text, out var tolerance) ||
                    tolerance < 0 || tolerance > 10)
                {
                    if (_legacyValidation != null)
                        _legacyValidation.Text = "HNL Tool: Dung sai phải từ 0 đến 10 mm.";
                    _legacyTolerance?.Focus();
                    return false;
                }
                candidate.Tolerance = tolerance;
            }
            candidate.TableTextHeight = tableHeight;
            candidate.LabelTextHeight = labelHeight;
            if (_legacyTableStyle != null && _legacyTableStyle.SelectedIndex >= 0)
                candidate.TableTextStyle = _legacyTableStyle.SelectedIndex == 0
                    ? string.Empty : _legacyTableStyle.SelectedItem?.ToString() ?? string.Empty;
            if (_legacyCadTableStyle != null && _legacyCadTableStyle.SelectedIndex >= 0)
                candidate.CadTableStyle = _legacyCadTableStyle.SelectedIndex == 0
                    ? string.Empty : _legacyCadTableStyle.SelectedItem?.ToString() ?? string.Empty;
            try { candidate.ToOptions(); }
            catch (System.Exception ex)
            {
                if (_legacyValidation != null)
                    _legacyValidation.Text = "HNL Tool: Cấu hình tấm không hợp lệ: " + ex.Message;
                return false;
            }

            // Commit the visible selection and numeric settings together before
            // SendStringToExecute queues a command in AutoCAD's document context.
            profile.Family = candidate.Family;
            profile.Module = candidate.Module;
            profile.Priority = candidate.Priority;
            profile.Direction = candidate.Direction;
            profile.GridMode = candidate.GridMode;
            profile.SnapEnabled = candidate.SnapEnabled;
            profile.Tolerance = candidate.Tolerance;
            profile.TableTextHeight = candidate.TableTextHeight;
            profile.LabelTextHeight = candidate.LabelTextHeight;
            profile.TableTextStyle = candidate.TableTextStyle;
            profile.CadTableStyle = candidate.CadTableStyle;
            if (_legacyValidation != null) _legacyValidation.Text = string.Empty;
            RefreshSelectionState(profile);
            CadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nHNL Tool - HCE Palette chốt lựa chọn: mode=" + profile.Module +
                " priority=" + profile.Priority + " family=" + profile.Family +
                " grid=" + profile.GridMode + "; " + profile.Summary());
            return true;
        }

        private void SaveLegacyTolerance()
        {
            if (_loadingLegacyControls || _legacyTolerance == null) return;
            var profile = ActiveLegacyProfile();
            if (profile == null) return;
            if (TryParseUiMm(_legacyTolerance.Text, out var value) &&
                value >= 0 && value <= 10)
            {
                profile.Tolerance = value;
                if (_legacyValidation != null) _legacyValidation.Text = "";
                // A numeric blur must not rebuild all ComboBoxes and reset S/D/M.
                if (_legacyTolerance != null)
                    _legacyTolerance.Text = value.ToString("0.###", CultureInfo.CurrentCulture);
                RefreshSelectionState(profile);
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
            if (TryParseUiMm(_legacyTableHeight.Text, out var value) &&
                value >= 0 && value <= 10000)
            {
                profile.TableTextHeight = value;
                if (_legacyValidation != null) _legacyValidation.Text = string.Empty;
            }
            else if (_legacyValidation != null)
                _legacyValidation.Text = "HNL Tool: Cao chữ bảng phải từ 0 đến 10000 mm.";
        }

        private void SaveLegacyCutList(bool enabled)
        {
            if (_loadingLegacyControls) return;
            var profile = ActiveLegacyProfile();
            if (profile == null) return;
            profile.CutListEnabled = enabled;
            RefreshSelectionState(profile);
        }

        private void SaveLegacyLabelHeight()
        {
            if (_loadingLegacyControls || _legacyLabelHeight == null) return;
            var profile = ActiveLegacyProfile();
            if (profile == null) return;
            if (TryParseUiMm(_legacyLabelHeight.Text, out var height) &&
                height > 0 && height <= 10000)
            {
                profile.LabelTextHeight = height;
                if (_legacyValidation != null) _legacyValidation.Text = string.Empty;
            }
            else if (_legacyValidation != null)
                _legacyValidation.Text = "HNL Tool: Cao mã ô phải lớn hơn 0 và không quá 10000 mm.";
        }

        private void RefreshCadTableStyles(HceLegacyProfile profile)
        {
            if (_legacyCadTableStyle == null) return;
            _legacyCadTableStyle.Items.Clear();
            _legacyCadTableStyle.Items.Add("(Theo CTABLESTYLE hiện hành)");
            var doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                try
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var styles = (DBDictionary)tr.GetObject(
                            doc.Database.TableStyleDictionaryId, OpenMode.ForRead);
                        var names = new System.Collections.Generic.List<string>();
                        foreach (DBDictionaryEntry entry in styles) names.Add(entry.Key);
                        names.Sort(StringComparer.OrdinalIgnoreCase);
                        foreach (var name in names) _legacyCadTableStyle.Items.Add(name);
                    }
                }
                catch (System.Exception)
                {
                    // UI refresh never mutates the DWG; creation validates missing styles.
                }
            }
            var selected = 0;
            for (var i = 1; i < _legacyCadTableStyle.Items.Count; i++)
                if (string.Equals(_legacyCadTableStyle.Items[i]?.ToString(),
                    profile.CadTableStyle, StringComparison.OrdinalIgnoreCase))
                { selected = i; break; }
            // Preserve a style that was later removed; InsertTable reports it explicitly.
            if (selected == 0 && !string.IsNullOrEmpty(profile.CadTableStyle))
            {
                _legacyCadTableStyle.Items.Add(profile.CadTableStyle);
                selected = _legacyCadTableStyle.Items.Count - 1;
            }
            _legacyCadTableStyle.SelectedIndex = selected;
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

        // Lightweight UI state only. Never clear or repopulate a ComboBox
        // while its SelectionChanged event is running, and never overwrite a
        // partially typed numeric value on an unrelated option change.
        private void RefreshSelectionState(HceLegacyProfile profile)
        {
            // Do not display "Tấm ngắn làm chính" in D-only mode (or the
            // inverse). The remembered priority has no meaning until M.
            if (_legacyPriorityRow != null)
                _legacyPriorityRow.Visibility = profile.Module == "M"
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            if (_legacySelectors.TryGetValue("Priority", out var priority))
            {
                priority.IsEnabled = profile.Module == "M";
                priority.ToolTip = "Chỉ dùng khi Loại tấm là Kết hợp.";
                ToolTipService.SetShowOnDisabled(priority, true);
            }
            if (_legacySelectors.TryGetValue("Direction", out var direction))
            {
                direction.IsEnabled = profile.Module != "S";
                direction.ToolTip = "Chỉ dùng với Tấm dài hoặc Kết hợp.";
                ToolTipService.SetShowOnDisabled(direction, true);
            }
            if (_legacyTolerance != null)
            {
                _legacyTolerance.IsEnabled = profile.SnapEnabled;
                _legacyTolerance.ToolTip = profile.SnapEnabled
                    ? "Nhập dung sai lưới từ 0 đến 10 mm."
                    : "Bật Sai số lưới để nhập dung sai.";
                ToolTipService.SetShowOnDisabled(_legacyTolerance, true);
            }
            if (_legacyCutList != null &&
                _legacyCutList.IsChecked != profile.CutListEnabled)
            {
                // Prevent a programmatic checkbox synchronization from
                // raising another user-originated configuration event.
                var wasLoading = _loadingLegacyControls;
                _loadingLegacyControls = true;
                try { _legacyCutList.IsChecked = profile.CutListEnabled; }
                finally { _loadingLegacyControls = wasLoading; }
            }
            if (_legacyCutMode != null) _legacyCutMode.IsEnabled = true;
            if (_legacyStatus != null)
                _legacyStatus.Text = profile.Summary() +
                    "\nKiểu bảng: " + (string.IsNullOrEmpty(profile.CadTableStyle)
                        ? "Theo DWG" : profile.CadTableStyle) +
                    " | Liệt kê: " + (profile.CutListEnabled ? "BẬT" : "TẮT") +
                    " | Chế độ: " + (profile.CutListMode == "D" ? "Chi tiết" : "Tổng hợp");
        }

        internal void RefreshLegacyControls(bool onlyOnDocumentChange = false)
        {
            var doc = CadApplication.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            // DocumentActivated can fire while a WPF ComboBox is open; do not
            // reapply a stale selection for the same DWG while the user edits.
            if (onlyOnDocumentChange && ReferenceEquals(_lastLegacyDatabase, doc.Database)) return;
            var profile = HceLegacyProfiles.For(doc.Database);
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
                RefreshCadTableStyles(profile);
                RefreshTableStyles(profile);
                if (_legacyTableHeight != null)
                    _legacyTableHeight.Text = profile.TableTextHeight.ToString("0.###", CultureInfo.CurrentCulture);
                if (_legacyLabelHeight != null)
                    _legacyLabelHeight.Text = profile.LabelTextHeight.ToString("0.###", CultureInfo.CurrentCulture);
                if (_legacyCutList != null) _legacyCutList.IsChecked = profile.CutListEnabled;
                if (_legacyCutMode != null)
                    _legacyCutMode.SelectedIndex = profile.CutListMode == "D" ? 1 : 0;
                RefreshSelectionState(profile);
                if (_legacyValidation != null) _legacyValidation.Text = "";
                _lastLegacyDatabase = doc.Database;
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
            // The active HCE panel already contains the Calculate button.
            // Do not revive the obsolete DTC alias in auxiliary views.

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
                if (command == "HCECALC" && !CommitPendingCalculationSettings()) return;
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
