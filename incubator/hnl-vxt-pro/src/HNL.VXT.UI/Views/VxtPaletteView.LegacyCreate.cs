using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView
    {
        // V6.7.2 has one special workflow that intentionally needs no ceiling boundary:
        // Draw XC = off, XP = off, Ty = on, Auto DIM = off -> select existing XC and lay Ty.
        // The beta palette originally disabled Create whenever HasBoundary=false, making that
        // legacy branch unreachable from the UI. A class handler patches only the primary Create
        // button while preserving the existing ViewModel contract and all normal validation.
        static VxtPaletteView()
        {
            EventManager.RegisterClassHandler(
                typeof(VxtPaletteView),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnLegacyCreateLoaded));
        }

        private static void OnLegacyCreateLoaded(object sender, RoutedEventArgs e)
        {
            var view = sender as VxtPaletteView;
            if (view == null) return;
            var button = FindCreateButton(view);
            if (button == null || button.Resources.Contains("HNL.LegacyCreatePatched")) return;

            button.Resources["HNL.LegacyCreatePatched"] = true;
            BindingOperations.ClearBinding(button, UIElement.IsEnabledProperty);
            BindingOperations.ClearBinding(button, Button.CommandProperty);
            button.Command = null;
            button.Click += LegacyCreateClick;

            var vm = button.DataContext as VxtPaletteViewModel ?? view.DataContext as VxtPaletteViewModel;
            if (vm != null)
            {
                UpdateLegacyCreateEnabled(button, vm);
                PropertyChangedEventHandler handler = (s, args) => UpdateLegacyCreateEnabled(button, vm);
                vm.PropertyChanged += handler;
                button.Unloaded += (s, args) => vm.PropertyChanged -= handler;
            }
        }

        private static void LegacyCreateClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var vm = button?.DataContext as VxtPaletteViewModel;
            if (vm == null) return;
            if (!vm.CanCreate && !IsLegacyManualTyOnly(vm)) return;

            // RelayCommand.Execute deliberately does not re-check CanExecute. For normal mode
            // vm.CanCreate is already true; for the one approved legacy mode the host coordinator
            // performs the existing-XC selection before entering the transactional Create engine.
            vm.CreateCommand.Execute(null);
        }

        private static void UpdateLegacyCreateEnabled(Button button, VxtPaletteViewModel vm)
        {
            if (button == null || vm == null) return;
            button.IsEnabled = vm.CanCreate || IsLegacyManualTyOnly(vm);
        }

        private static bool IsLegacyManualTyOnly(VxtPaletteViewModel vm)
        {
            return !vm.HasBoundary &&
                   !vm.DrawMain &&
                   !vm.DrawFurring &&
                   vm.DrawHangers &&
                   !vm.AutoDimension;
        }

        private static Button FindCreateButton(DependencyObject root)
        {
            if (root == null) return null;
            var button = root as Button;
            if (button != null)
            {
                var text = button.Content as string;
                if (!string.IsNullOrWhiteSpace(text) &&
                    text.IndexOf("TẠO KHUNG XƯƠNG TRẦN", StringComparison.OrdinalIgnoreCase) >= 0)
                    return button;
            }

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var found = FindCreateButton(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }
    }
}
