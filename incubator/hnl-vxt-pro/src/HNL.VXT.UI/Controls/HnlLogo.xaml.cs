using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace HNL.VXT.UI.Controls
{
    public partial class HnlLogo : UserControl
    {
        public HnlLogo()
        {
            InitializeComponent();
            LoadOfficialLogo();
        }

        private void LoadOfficialLogo()
        {
            try
            {
                // Use the exact same repository logo as the installer. Keeping one canonical
                // source avoids the old 128x128 Base64 palette asset becoming cropped/stale.
                var uri = new Uri(
                    "pack://application:,,,/HNL.VXT.UI;component/Assets/HNL-Logo-Official.png",
                    UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = uri;
                bitmap.EndInit();
                bitmap.Freeze();
                LogoImage.Source = bitmap;
            }
            catch
            {
                // Branding must never block the VXT palette from loading.
                LogoImage.Source = null;
            }
        }
    }
}
