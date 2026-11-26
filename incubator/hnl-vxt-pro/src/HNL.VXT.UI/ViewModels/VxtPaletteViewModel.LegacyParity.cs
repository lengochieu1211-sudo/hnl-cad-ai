namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        /// <summary>
        /// Auto direction in VXT V6.7.2 asks the Shadowline question after closing DCL.
        /// Persist the answer into the ViewModel so a later Snapshot cannot overwrite the
        /// host-side answer captured immediately before Create.
        /// </summary>
        public void SetAutoShadowlineFromHost(bool value, bool configured = true, bool requestPreview = true)
        {
            var changed = _settings.AutoShadowline != value ||
                          _settings.AutoShadowlineConfigured != configured;
            if (!changed) return;

            _settings.AutoShadowline = value;
            _settings.AutoShadowlineConfigured = configured;
            MarkCustom();
            if (requestPreview) RequestPreview();
        }
    }
}
