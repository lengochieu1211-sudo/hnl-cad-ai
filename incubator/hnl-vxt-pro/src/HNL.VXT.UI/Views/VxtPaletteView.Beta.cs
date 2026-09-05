using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView
    {
        private static ControlTemplate _hnlComboTemplate;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += OnBetaLoaded;
        }

        private void OnBetaLoaded(object sender, RoutedEventArgs e)
        {
            // Run after the enhancer has created dynamic panels and applied the active HNL theme.
            Dispatcher.BeginInvoke(new Action(() => FinalizeBetaUi(this)), DispatcherPriority.ContextIdle);
        }

        private static void FinalizeBetaUi(DependencyObject root)
        {
            if (root == null) return;

            var primary = FindBrush(root as FrameworkElement, "PrimaryText", Brushes.Black);
            var secondary = FindBrush(root as FrameworkElement, "SecondaryText", Brushes.Gray);
            var input = FindBrush(root as FrameworkElement, "InputBackground", Brushes.White);
            var border = FindBrush(root as FrameworkElement, "InputBorder", Brushes.Gray);
            var selected = FindBrush(root as FrameworkElement, "AccentSoft", Brushes.LightGray);

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                var text = child as TextBlock;
                if (text != null && !string.IsNullOrWhiteSpace(text.Text) &&
                    (text.Text.StartsWith("Alpha.2:", StringComparison.OrdinalIgnoreCase) ||
                     text.Text.StartsWith("Alpha.5:", StringComparison.OrdinalIgnoreCase)))
                {
                    text.Text = "Beta.1: Preview và Tạo thật dùng chung engine Golden V6.7.4 • Create đã mở để kiểm thử chức năng.";
                }

                var combo = child as ComboBox;
                if (combo != null)
                    FixCombo(combo, primary, secondary, input, border, selected);

                FinalizeBetaUi(child);
            }
        }

        private static void FixCombo(ComboBox combo, Brush primary, Brush secondary, Brush input, Brush border, Brush selected)
        {
            combo.Foreground = combo.IsEnabled ? primary : secondary;
            combo.Background = input;
            combo.BorderBrush = border;

            // Override the Windows/AutoCAD system brushes used by the stock ComboBox chrome.
            // This prevents the white rectangle / white disabled text visible in dark AutoCAD themes.
            combo.Resources[SystemColors.WindowBrushKey] = input;
            combo.Resources[SystemColors.ControlBrushKey] = input;
            combo.Resources[SystemColors.ControlTextBrushKey] = primary;
            combo.Resources[SystemColors.GrayTextBrushKey] = secondary;
            combo.Resources[SystemColors.HighlightBrushKey] = selected;
            combo.Resources[SystemColors.HighlightTextBrushKey] = primary;
            combo.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = selected;
            combo.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = primary;

            // Non-editable lists are the main VXT selectors. Give them a deterministic HNL template
            // rather than relying on the host AutoCAD/Windows theme (which caused the white controls).
            if (!combo.IsEditable)
                combo.Template = HnlComboTemplate();

            combo.ApplyTemplate();
            ApplyEditableTextBoxColors(combo, primary, input, border);

            combo.IsEnabledChanged -= ComboIsEnabledChanged;
            combo.IsEnabledChanged += ComboIsEnabledChanged;
        }

        private static void ComboIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;
            var primary = FindBrush(combo, "PrimaryText", Brushes.Black);
            var secondary = FindBrush(combo, "SecondaryText", Brushes.Gray);
            combo.Foreground = combo.IsEnabled ? primary : secondary;
            combo.Opacity = combo.IsEnabled ? 1.0 : 0.82;
        }

        private static ControlTemplate HnlComboTemplate()
        {
            if (_hnlComboTemplate != null) return _hnlComboTemplate;

            const string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type ComboBox}'>
  <Grid SnapsToDevicePixels='True'>
    <ToggleButton x:Name='DropDownToggle'
                  Focusable='False'
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
              Background='{DynamicResource CardBackground}'
              BorderBrush='{DynamicResource InputBorder}'
              BorderThickness='1'
              CornerRadius='4'>
        <ScrollViewer Margin='1' SnapsToDevicePixels='True' VerticalScrollBarVisibility='Auto'>
          <ItemsPresenter/>
        </ScrollViewer>
      </Border>
    </Popup>
  </Grid>
</ControlTemplate>";

            _hnlComboTemplate = (ControlTemplate)XamlReader.Parse(xaml);
            return _hnlComboTemplate;
        }

        private static void ApplyEditableTextBoxColors(DependencyObject root, Brush foreground, Brush background, Brush border)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var box = child as TextBox;
                if (box != null)
                {
                    box.Foreground = foreground;
                    box.Background = background;
                    box.BorderBrush = border;
                    box.CaretBrush = foreground;
                }
                ApplyEditableTextBoxColors(child, foreground, background, border);
            }
        }

        private static Brush FindBrush(FrameworkElement element, string key, Brush fallback)
        {
            if (element != null)
            {
                var value = element.TryFindResource(key) as Brush;
                if (value != null) return value;
            }
            return fallback;
        }
    }
}
