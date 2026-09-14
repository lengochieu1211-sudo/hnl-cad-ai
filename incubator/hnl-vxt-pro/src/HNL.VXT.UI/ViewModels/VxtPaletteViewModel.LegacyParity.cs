namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        /// <summary>
        /// Auto direction in VXT V6.7.2 asks the Shadowline question after closing DCL.
        /// Persist the answer into the ViewModel so a later Snapshot cannot overwrite the
        /// host-side answer captured immediately before Create.
        /// </summary>
        public void SetAutoShadowlineFromHost(bool value)
        {
            if (_settings.AutoShadowline == value) return;
            _settings.AutoShadowline = value;
            MarkCustom();
            RequestPreview();
        }
    }
}
