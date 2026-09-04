using System;
using System.Drawing;
using Autodesk.AutoCAD.Windows;
using HNL.VXT.UI.Views;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtPaletteService
    {
        // Alpha.5 uses a refreshed Palette GUID so AutoCAD does not restore the stale alpha.1 title
        // from the profile cache. A stable final GUID will be frozen after Golden verification.
        private static readonly Guid PaletteGuid = new Guid("925513AF-8743-438C-8F53-BA6C586CF2D2");
        private static PaletteSet _palette;
        private static VxtPaletteView _view;

        public static void Show()
        {
            if (_palette == null)
            {
                var bridge = new VxtHostBridge();
                _view = new VxtPaletteView(bridge);
                VxtSession.Current.ViewModel = _view.ViewModel;

                _palette = new PaletteSet("HNL Tool - VXT Pro v7.0.0-alpha.5", PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowPropertiesMenu,
                    DockEnabled = DockSides.Left | DockSides.Right,
                    MinimumSize = new Size(360, 600),
                    Size = new Size(440, 780),
                    KeepFocus = false
                };

                _palette.AddVisual("Vẽ Xương Trần", _view);
            }

            _palette.Visible = true;
        }
    }
}
