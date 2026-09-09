using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;

namespace HNL.VXT.UI.Views
{
    /// <summary>
    /// Compact production toggle switch for HNL VXT Pro.
    /// Shrinks only controls using the ToggleSwitch resource; normal checkboxes are untouched.
    /// </summary>
    public static class VxtPaletteToggleCompact
    {
        private static ControlTemplate _compactToggleTemplate;

        public static void Apply(VxtPaletteView view)
        {
            if (view == null) return;

            ApplyNow(view);
            view.Loaded += (sender, args) =>
            {
                view.Dispatcher.BeginInvoke(
                    new Action(() => ApplyNow(view)),
                    DispatcherPriority.ContextIdle);
            };
        }

        private static void ApplyNow(VxtPaletteView view)
        {
            var toggleStyle = view.TryFindResource("ToggleSwitch") as Style;
            if (toggleStyle == null) return;

            foreach (var node in Walk(view))
            {
                var check = node as CheckBox;
                if (check == null || !ReferenceEquals(check.Style, toggleStyle)) continue;

                check.Width = 36.0;
                check.Height = 20.0;
                check.Template = CompactToggleTemplate();
            }
        }

        private static ControlTemplate CompactToggleTemplate()
        {
            if (_compactToggleTemplate != null) return _compactToggleTemplate;

            const string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type CheckBox}'>
  <Grid Width='36' Height='20' SnapsToDevicePixels='True'>
    <Border x:Name='Track'
            CornerRadius='10'
            Background='{DynamicResource InputBorder}'/>
    <Ellipse x:Name='Knob'
             Width='14' Height='14'
             Fill='White'
             Margin='3'
             HorizontalAlignment='Left'
             VerticalAlignment='Center'/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsChecked' Value='True'>
      <Setter TargetName='Track' Property='Background' Value='{DynamicResource AccentStrong}'/>
      <Setter TargetName='Knob' Property='HorizontalAlignment' Value='Right'/>
    </Trigger>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='Track' Property='Opacity' Value='0.92'/>
    </Trigger>
    <Trigger Property='IsEnabled' Value='False'>
      <Setter Property='Opacity' Value='0.45'/>
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";

            _compactToggleTemplate = (ControlTemplate)XamlReader.Parse(xaml);
            return _compactToggleTemplate;
        }

        private static IEnumerable<DependencyObject> Walk(DependencyObject root)
        {
            if (root == null) yield break;

            var seen = new HashSet<DependencyObject>();
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !seen.Add(current)) continue;
                yield return current;

                var panel = current as Panel;
                if (panel != null)
                {
                    foreach (UIElement child in panel.Children)
                        if (child != null) queue.Enqueue(child);
                }

                var border = current as Border;
                if (border?.Child != null) queue.Enqueue(border.Child);

                var headered = current as HeaderedContentControl;
                if (headered != null)
                {
                    var header = headered.Header as DependencyObject;
                    var content = headered.Content as DependencyObject;
                    if (header != null) queue.Enqueue(header);
                    if (content != null) queue.Enqueue(content);
                }
                else
                {
                    var contentControl = current as ContentControl;
                    var content = contentControl?.Content as DependencyObject;
                    if (content != null) queue.Enqueue(content);
                }

                var userControl = current as UserControl;
                var userContent = userControl?.Content as DependencyObject;
                if (userContent != null) queue.Enqueue(userContent);
            }
        }
    }
}
