using System;
using System.IO;
using System.Reflection;
using System.Text;
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
                var assembly = typeof(HnlLogo).Assembly;
                using (var stream = assembly.GetManifestResourceStream("HNL.VXT.UI.HNLLogoOfficial"))
                {
                    if (stream == null) return;
                    using (var reader = new StreamReader(stream, Encoding.ASCII))
                    {
                        var base64 = reader.ReadToEnd().Trim();
                        var bytes = Convert.FromBase64String(base64);
                        using (var imageStream = new MemoryStream(bytes))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = imageStream;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            LogoImage.Source = bitmap;
                        }
                    }
                }
            }
            catch
            {
                // Branding must never block the VXT palette from loading.
                LogoImage.Source = null;
            }
        }
    }
}
