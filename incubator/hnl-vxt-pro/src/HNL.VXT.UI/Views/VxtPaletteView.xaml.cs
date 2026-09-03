using System.Windows.Controls;
using HNL.VXT.UI.Hosting;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.UI.Views
{
    public partial class VxtPaletteView : UserControl
    {
        public VxtPaletteView(IVxtHostBridge host)
        {
            InitializeComponent();
            ViewModel = new VxtPaletteViewModel(host);
            DataContext = ViewModel;
        }

        public VxtPaletteViewModel ViewModel { get; }
    }
}
