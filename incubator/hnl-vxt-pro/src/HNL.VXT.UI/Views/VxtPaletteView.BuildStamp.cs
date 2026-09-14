using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using HNL.VXT.UI.Infrastructure;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView
    {
        // VxtPaletteView.LegacyCreate.cs already owns the explicit static constructor.
        // Register this additional class handler through a static field initializer so both
        // behaviors coexist without introducing a second .cctor.
        private static readonly bool BuildStampHandlerRegistered = RegisterBuildStampHandler();

        private static bool RegisterBuildStampHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(VxtPaletteView),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnBuildStampLoaded));
            return true;
        }

        private static void OnBuildStampLoaded(object sender, RoutedEventArgs e)
        {
            var view = sender as VxtPaletteView;
            if (view == null) return;

            var versionText = FindVersionTextBlock(view);
            if (versionText == null) return;
            if (!string.IsNullOrEmpty(versionText.Text) && versionText.Text.Contains("• Build")) return;

            BindingOperations.ClearBinding(versionText, TextBlock.TextProperty);
            versionText.Text = VxtBuildInfo.VersionLabel;
            versionText.ToolTip = "HNL Tool - thời điểm build của đúng bộ cài đang chạy";
        }

        private static TextBlock FindVersionTextBlock(DependencyObject parent)
        {
            if (parent == null) return null;

            var direct = parent as TextBlock;
            if (direct != null &&
                !string.IsNullOrWhiteSpace(direct.Text) &&
                direct.Text.StartsWith("VXT Pro ", StringComparison.OrdinalIgnoreCase))
            {
                return direct;
            }

            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var found = FindVersionTextBlock(VisualTreeHelper.GetChild(parent, i));
                if (found != null) return found;
            }

            return null;
        }
    }
}
