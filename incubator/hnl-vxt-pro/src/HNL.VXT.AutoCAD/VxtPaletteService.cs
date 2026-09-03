using System;
using System.Drawing;
using Autodesk.AutoCAD.Windows;
using HNL.VXT.UI.Views;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtPaletteService
    {
        private static readonly Guid PaletteGuid = new Guid("E197984B-C567-46A2-ABCD-8BD7E43BCA36");
        private static PaletteSet _palette;
        private static VxtPaletteView _view;

        public static void Show()
        {
            if (_palette == null)
            {
                var bridge = new VxtHostBridge();
                _view = new VxtPaletteView(bridge);
                VxtSession.Current.ViewModel = _view.ViewModel;

                _palette = new PaletteSet("HNL Tool - VXT Pro v7.0.0-alpha.1", PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowPropertiesMenu,
                    DockEnabled = DockSides.Left | DockSides.Right,
                    MinimumSize = new Size(360, 580),
                    Size = new Size(430, 760),
                    KeepFocus = false
                };

                _palette.AddVisual("Vẽ Xương Trần", _view);
            }

            _palette.Visible = true;
        }
    }
}
