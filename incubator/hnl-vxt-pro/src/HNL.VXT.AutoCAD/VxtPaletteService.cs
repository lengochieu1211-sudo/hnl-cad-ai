using System;
using System.Drawing;
using Autodesk.AutoCAD.Windows;
using HNL.VXT.UI.Views;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtPaletteService
    {
        // Beta.1 gets a refreshed Palette GUID so AutoCAD does not restore stale alpha titles/layout.
        private static readonly Guid PaletteGuid = new Guid("8F41C84C-6E27-4E9A-9F1E-1FDC49B1A706");
        private static PaletteSet _palette;
        private static VxtPaletteView _view;

        public static void Show()
        {
            if (_palette == null)
            {
                var bridge = new VxtHostBridge();
                _view = new VxtPaletteView(bridge);
                VxtSession.Current.ViewModel = _view.ViewModel;

                _palette = new PaletteSet("HNL Tool - VXT Pro v7.0.0-beta.1", PaletteGuid)
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
