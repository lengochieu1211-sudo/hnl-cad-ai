using System.Windows.Controls;
using System.Windows.Media;
using HNL.VXT.UI.Hosting;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView : UserControl
    {
        public VxtPaletteView(IVxtHostBridge host)
        {
            InitializeComponent();
            ApplyTheme(host.IsDarkTheme);
            ViewModel = new VxtPaletteViewModel(host);
            DataContext = ViewModel;
        }

        public VxtPaletteViewModel ViewModel { get; }

        private void ApplyTheme(bool dark)
        {
            Resources["AppBackground"] = Brush(dark ? "#1B1F23" : "#F3F6F8");
            Resources["CardBackground"] = Brush(dark ? "#252A30" : "#FFFFFF");
            Resources["CardBorder"] = Brush(dark ? "#3B424A" : "#D7E0E7");
            Resources["PrimaryText"] = Brush(dark ? "#F1F5F9" : "#172033");
            Resources["SecondaryText"] = Brush(dark ? "#AEB8C4" : "#64748B");
            Resources["InputBackground"] = Brush(dark ? "#1E2328" : "#FFFFFF");
            Resources["InputBorder"] = Brush(dark ? "#4A535D" : "#C7D2DC");
            Resources["HoverBackground"] = Brush(dark ? "#323941" : "#EEF4F8");
            Resources["Accent"] = Brush("#0EA5E9");
            Resources["AccentStrong"] = Brush(dark ? "#38BDF8" : "#0284C7");
            Resources["AccentSoft"] = Brush(dark ? "#102F3E" : "#EAF8FE");
            Resources["AccentBorder"] = Brush(dark ? "#155E75" : "#BAE6FD");
            Resources["HeaderBackground"] = Brush(dark ? "#0B1118" : "#0F172A");
            Resources["Success"] = Brush("#22C55E");
        }

        private static SolidColorBrush Brush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
