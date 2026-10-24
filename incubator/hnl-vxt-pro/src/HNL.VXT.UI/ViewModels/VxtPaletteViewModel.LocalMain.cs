namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        /// <summary>
        /// Lisp-parity switch for concave/notch repair. The Core engine first tries to move
        /// an existing global main member; only unresolved Max-edge/Max-spacing violations
        /// receive a short local main member when this switch is enabled.
        /// </summary>
        public bool UseLocalMainAdd
        {
            get => _settings.UseLocalMainAdd;
            set
            {
                if (_settings.UseLocalMainAdd == value) return;
                _settings.UseLocalMainAdd = value;
                Changed();
            }
        }

        public double MinLocalMainLength
        {
            get => _settings.MinLocalMainLength;
            set => SetNumberSetting(
                () => _settings.MinLocalMainLength,
                v => _settings.MinLocalMainLength = v,
                value);
        }
    }
}
